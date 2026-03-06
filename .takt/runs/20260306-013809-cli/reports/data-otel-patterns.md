# OpenTelemetry × CLI ツール採用パターン外部調査レポート

**調査実施日**: 2026-03-06
**調査担当**: Research Digger (dig.otel-patterns)

---

## 1. 採用 OSS 事例のサマリー表

| プロジェクト | 採用形態 | OTel シグナル | opt-in/opt-out | 出典URL |
|---|---|---|---|---|
| **Semgrep** | OTel tracing を直接組み込み（OCaml製ランタイム） | Traces のみ | opt-in（`--trace` フラグ必須） | [metrics.md](https://github.com/semgrep/semgrep/blob/develop/metrics.md) |
| **Docker CLI** | 環境変数ベースの opt-in metrics export | Metrics のみ（`command.time`） | opt-in（`DOCKER_CLI_OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は無効） | [Docker OTel Docs](https://docs.docker.com/engine/cli/otel/) |
| **otel-cli** | OTel CLI ラッパーツール（shell script 向け） | Traces | opt-in（`OTEL_EXPORTER_OTLP_ENDPOINT` 未設定 → non-recording mode） | [GitHub](https://github.com/equinix-labs/otel-cli) |
| **dotnet-monitor** | OTel を使わず、Prometheus 形式 `/metrics` エンドポイントを提供（サイドカー型） | なし（OTel非採用） | 常時有効（`-m false` で無効化） | [MS Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-monitor) |
| **Trivy** | 独自の匿名使用統計テレメトリ（OTel 非採用） | なし（OTel非採用） | opt-out（`--disable-telemetry`） | [trivy.dev](https://trivy.dev/latest/docs/configuration/) |
| **dotnet-trace / dotnet-counters** | OTel SDK を利用せず、EventPipe プロトコルを使用して収集（OTel非採用） | なし（OTel非採用） | 常時有効（ツール自体がモニタリングツール） | [MS Learn OTel](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel) |
| **grype（Anchore）** | OTel 未採用（調査時点で実装なし） | なし | — | [GitHub Issue #3179](https://github.com/anchore/grype/issues/3179)（OTelコンテナをスキャンした際の問題のみ） |

### 追加調査: Semgrep の OTel 依存関係問題

Semgrep は OTel を `--trace` フラグ付きの場合のみ使用するにもかかわらず、OTel パッケージを必須依存関係として含めた。これによりパッケージサイズ増大・依存関係競合の問題が発生し、Issue [#10408](https://github.com/semgrep/semgrep/issues/10408) が Open のまま残っている（2025年12月時点）。

**教訓**: OTel SDK を必須依存関係にすると、opt-in 機能であっても全ユーザーに影響が出る。

---

## 2. 短寿命プロセスにおける OTel 技術的課題と対策

### 課題 1: バッチ export のフラッシュタイミング

**問題**:
OTel SDK は既定でバッチエクスポート（`BatchSpanProcessor` / `PeriodicExportingMetricReader`）を使用する。短寿命プロセス（実行時間 < 1秒）では、プロセス終了前にデータが export されない場合がある。

**具体的な症状（.NET）**:
- コンソール出力にはログが表示されるが、OTLP export されない
- `MetricReaderType.Periodic`（デフォルト60秒間隔）ではメトリクスが全く出ない
- Issue: [opentelemetry-dotnet #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)
- Issue: [opentelemetry-dotnet #2979](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979)

**対策（.NET）**:

| 方法 | 説明 |
|---|---|
| **IHost を正しく Dispose する** | `using var app = builder.Build();` で Host を Dispose すると、LoggerFactory が Flush される。最も推奨される方法 |
| **`ForceFlush()` を明示的に呼ぶ** | `TracerProvider.ForceFlush()` / `MeterProvider.ForceFlush()` をプロセス終了前に呼び出す（実験的 API あり） |
| **Export 間隔を短縮する** | `MetricReaderOptions.ExportIntervalMilliseconds` を短く設定（例: 1000ms）|
| **Simple エクスポートプロセッサを使う** | Traces のみ: `ExportProcessorType.Simple` で同期エクスポートに変更 |
| **localhost Collector 経由にする** | otel-cli 推奨パターン: ローカルに OTel Collector を立て、バッファリングを委譲する |

出典:
- [GitHub Issue #5102 - opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102) (2024)
- [GitHub Issue #2979 - opentelemetry-dotnet](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979)
- [OTel Spec - ForceFlush](https://opentelemetry.io/docs/specs/otel/trace/sdk/)

### 課題 2: コールドスタート・SDK 初期化コスト

**測定値（参考値）**:

| 環境 | オーバーヘッド | 備考 |
|---|---|---|
| Go（HTTP サーバー、10k RPS） | CPU +35%、P99 レイテンシ +5ms | Tracing enabled、バッチ export込み |
| Go（ネットワーク） | 約 4 MB/秒 の追加通信 | span 送信によるもの |
| .NET（ASP.NET、高スループット） | スループット -15%（270k→230k RPS） | OTel instrumentation 追加後 |
| Lambda（コールドスタート） | SDK 初期化・span processor 起動分が追加 | 数十〜数百 ms の追加が報告される |

出典:
- [OpenTelemetry for Go: measuring the overhead | Coroot](https://coroot.com/blog/opentelemetry-for-go-measuring-the-overhead/) (2024)
- [GitHub dotnet/runtime #107158](https://github.com/dotnet/runtime/issues/107158) (.NET 15% RPS 低下)
- [How to Monitor Cold Start Performance Degradation in Serverless Functions](https://oneuptime.com/blog/post/2026-02-06-serverless-cold-start-otel-metrics/view) (2026)

**CLI ツールへの影響**:
- 実行時間が数秒〜数十秒程度の静的解析ツール（Roslyntic 等）では、SDK 初期化は数十 ms 程度で無視できるレベルと推定される
- 一方、<100ms で終わる短命プロセスでは export が完了しない可能性が高い

### 課題 3: OTel Collector 不在時の挙動

**otel-cli のアプローチ（推奨パターン）**:
> "otel-cli will run in non-recording mode and not attempt to contact any servers"
（エンドポイント未設定時は完全に無効化 → "first, do no harm" 哲学）

出典: [equinix-labs/otel-cli README](https://github.com/equinix-labs/otel-cli/blob/main/README.md)

---

## 3. .NET OTel SDK のパッケージサイズ・依存数

（取得日: 2026-03-06、NuGet.org より）

| パッケージ | 最新バージョン | サイズ | 依存数（.NET 9.0/10.0） | 出典URL |
|---|---|---|---|---|
| `OpenTelemetry` | 1.15.0 | 782.97 KB (≒ 0.76 MB) | 3 (Microsoft.Extensions.Diagnostics.Abstractions, Microsoft.Extensions.Logging.Configuration, OpenTelemetry.Api.ProviderBuilderExtensions) | [NuGet](https://www.nuget.org/packages/OpenTelemetry) |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB (≒ 0.49 MB) | 1 (OpenTelemetry ≥ 1.15.0) ※.NET 9/10 | [NuGet](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol) |
| `OpenTelemetry.Extensions.Hosting` | 1.15.0 | 89.4 KB (≒ 0.09 MB) | 2 (Microsoft.Extensions.Hosting.Abstractions, OpenTelemetry) | [NuGet](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting) |

**合計（3パッケージ）**: ≒ 1.36 MB（ダウンロードサイズ）
※ 実際にはパッケージの推移的依存関係（`OpenTelemetry.Api` 等）が加算される

**注記**:
- `OpenTelemetry.Extensions.Hosting` は `IHost` / `IHostedService` 連携に必要。CLI ツールで Host を使わない場合は不要になる可能性がある
- `.NET 8.0` では `Microsoft.Extensions.Configuration.Binder` が追加依存として発生する（OTLP exporter）

---

## 4. opt-in 実装パターンの代表例

### パターン A: 環境変数による完全 opt-in（Docker CLI スタイル）

```bash
# 未設定時は完全に無効（テレメトリゼロ）
export DOCKER_CLI_OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
docker build .
```

- **採用プロジェクト**: Docker CLI
- **シグナル**: Metrics のみ（`command.time`）
- **特徴**: 環境変数が存在しない場合、OTel SDK 初期化自体をスキップ

出典: [Docker CLI OTel Docs](https://docs.docker.com/engine/cli/otel/)

---

### パターン B: CLI フラグ + 環境変数の組み合わせ（Semgrep スタイル）

```bash
# opt-in（--trace フラグが必要）
semgrep scan --trace --trace-endpoint http://localhost:4317 .

# 詳細レベルは環境変数で制御
SEMGREP_TRACE_LEVEL=debug semgrep scan --trace .
```

- **採用プロジェクト**: Semgrep
- **シグナル**: Traces のみ
- **特徴**: フラグなしでは全く送信しない。`--trace` フラグが「有効化スイッチ」として機能

出典: [semgrep/metrics.md](https://github.com/semgrep/semgrep/blob/develop/metrics.md)

---

### パターン C: 標準環境変数による no-op モード（otel-cli スタイル）

```bash
# OTel 標準環境変数が未設定 → non-recording mode（完全スキップ）
otel-cli exec --name "my-job" -- my-script.sh

# 有効化
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_SERVICE_NAME=my-cli \
otel-cli exec --name "my-job" -- my-script.sh
```

- **採用プロジェクト**: otel-cli (equinix-labs)
- **シグナル**: Traces
- **特徴**: OTel 標準 env var のみで制御。ツール固有フラグなし。"first, do no harm" 原則

出典: [equinix-labs/otel-cli](https://github.com/equinix-labs/otel-cli)

---

### パターン D: .NET での短寿命プロセス実装例

```csharp
// 環境変数チェックで条件付き初期化
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
if (otlpEndpoint is not null)
{
    services.AddOpenTelemetry()
        .WithTracing(b => b
            .AddSource("Roslyntic")
            .AddOtlpExporter())  // エンドポイントは env var から自動読み込み
        .WithMetrics(b => b
            .AddMeter("Roslyntic")
            .AddOtlpExporter());
}

// プロセス終了時に明示的 Flush
// using var host = builder.Build(); で自動 Flush（IHost.Dispose()経由）
```

- **採用根拠**: opentelemetry-dotnet 推奨パターン（#5102 の回答）
- **シグナル**: Traces + Metrics（Logs はオプション）

出典: [GitHub Issue #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)
出典: [OTLP Exporter README](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)

---

## 5. Roslyntic への適用可能性の初期評価

### 5.1 採用推奨度

**採用推奨**: 条件付き ⭕
**採用形態**: `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による opt-in（パターン A + D の組み合わせ）

### 5.2 Roslyntic の特性と OTel の整合性

| 特性 | OTel との整合性 |
|---|---|
| **STDOUT は機械可読（SARIF/JSON）のみ** | ✅ OTel は OTLP（gRPC/HTTP）でエクスポート。STDOUT を汚染しない |
| **ローカル実行のみ（デフォルトでネットワーク呼び出しなし）** | ✅ `OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時はゼロネットワーク。env var が唯一のゲート |
| **決定論的な出力** | ✅ OTel はサイドバンド（OTLP送信）であり、SARIF 出力には影響しない |
| **STDERR は人間向け（診断情報）** | ⚠️ OTel SDK のデバッグログが STDERR に出る可能性がある。`OTEL_LOG_LEVEL=none` または自己テレメトリ無効化が必要 |
| **短寿命プロセス** | ⚠️ `IHost` または明示的 `ForceFlush()` が必要。`using var` パターン必須 |
| **NativeAOT 対象外** | ✅ NativeAOT 対象外のため OTel SDK の反射使用は問題なし |

### 5.3 採用時の推奨実装方針

1. **デフォルト無効**: `OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は OTel 初期化をスキップ
2. **Flush 保証**: `using var host = ...` パターン、または `TracerProvider.Shutdown()` を明示呼び出し
3. **バッチサイズ調整**: `ScheduledDelayMilliseconds = 1000` 程度に短縮（デフォルト 5000ms は CLI には長すぎる）
4. **OTel 自己テレメトリ無効化**: Semgrep が遭遇した「OTel が自分自身のトレースを送信してくる」問題を回避
5. **パッケージの条件付き参照**: 本番ビルドに不要な場合は依存を分離（Semgrep Issue #10408 の教訓）

### 5.4 採用コスト見積もり

| 項目 | 推定工数 |
|---|---|
| NuGet パッケージ追加（3パッケージ、≒1.36 MB） | 1h |
| 条件付き初期化コードの実装 | 4h |
| `ForceFlush` / Dispose パターンの組み込み | 2h |
| 基本 Span（scan 開始・完了・rule 数など）の追加 | 4h |
| テスト・動作確認 | 4h |
| **合計** | **≒ 15h** |

---

## 6. 調査できなかった項目とその理由

| 調査項目 | 状況 | 理由 |
|---|---|---|
| `dotnet-monitor` の OTel 採用詳細 | **調査不可（未採用と判明）** | GitHub 検索 (`repo:dotnet/dotnet-monitor OpenTelemetry`) で 0 件。metrics は Prometheus 形式 `/metrics` エンドポイントのみ提供。OTel 採用なし |
| `dotnet-trace` / `dotnet-counters` の OTel 採用 | **調査不可（未採用）** | これらは OTel の消費者（監視対象から収集するツール）であり、自身が OTel SDK を使うツールではない |
| `Trivy` の OTel tracing 機能 | **調査不可（未採用）** | Trivy の telemetry は独自の匿名使用統計（非 OTel）のみ。OTel import もなし |
| `grype` の OTel 採用 | **調査不可（未採用）** | grype 自体に OTel 実装を示すコード・ドキュメントは発見できず。唯一の言及は OTelコンテナをスキャンした際のバグレポートのみ |
| .NET OTel SDK の実際の初期化時間（ms） | **数値なし** | .NET 固有の初期化コスト測定値は公開ベンチマークで発見できず。Go 等は参考値あり |
| `OpenTelemetry.Api.ProviderBuilderExtensions` のサイズ | **未確認** | `OpenTelemetry` パッケージの推移的依存 `OpenTelemetry.Api.ProviderBuilderExtensions` のサイズを個別確認できず |

---

## 参考 URL 一覧

- [OpenTelemetry .NET - GitHub](https://github.com/open-telemetry/opentelemetry-dotnet)
- [OTLP Exporter README (.NET)](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
- [OpenTelemetry - NuGet](https://www.nuget.org/packages/OpenTelemetry)
- [OpenTelemetry.Exporter.OpenTelemetryProtocol - NuGet](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
- [OpenTelemetry.Extensions.Hosting - NuGet](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting)
- [Docker CLI OTel Documentation](https://docs.docker.com/engine/cli/otel/)
- [otel-cli - equinix-labs](https://github.com/equinix-labs/otel-cli)
- [Semgrep metrics.md](https://github.com/semgrep/semgrep/blob/develop/metrics.md)
- [Semgrep Issue #10408 - OTel deps optional](https://github.com/semgrep/semgrep/issues/10408)
- [dotnet-monitor docs - MS Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-monitor)
- [opentelemetry-dotnet Issue #5102 - ForceFlush logs](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)
- [opentelemetry-dotnet Issue #2979 - metrics not flushed](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979)
- [OpenTelemetry for Go: measuring overhead - Coroot](https://coroot.com/blog/opentelemetry-for-go-measuring-the-overhead/)
- [dotnet/runtime Issue #107158 - 15% RPS degradation](https://github.com/dotnet/runtime/issues/107158)
- [OTel Tracing SDK Spec - ForceFlush](https://opentelemetry.io/docs/specs/otel/trace/sdk/)
- [Trivy Configuration - trivy.dev](https://trivy.dev/latest/docs/configuration/)
