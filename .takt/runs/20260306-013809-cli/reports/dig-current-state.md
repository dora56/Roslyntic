# 調査統合メモ（dig-current-state）

**作成日**: 2026-03-06
**対象ピース**: deep-research / dig.part-3-adr-creation
**参照調査データ**: data-otel-patterns.md, data-network-policy.md, data-internal-scan.md, research-report.md

---

## 1. 主要発見のまとめ

### 1-1. OTel Pattern D（.NET 短寿命プロセス実装例）コード全文

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

出典: [opentelemetry-dotnet Issue #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102), [OTLP Exporter README](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)

### 1-2. NF5.2 / NF5.3 フィールド一覧（docs/observability.md より）

#### NF5.2 必須シグナル（STDERR またはログファイルへ出力）

| カテゴリ | フィールド |
|---|---|
| Timing | total duration, workspace load, rule execution, output write |
| Scale | projects count, documents count |
| Quality signals | rules executed count, findings count（error / warning / note 別） |
| Failure classification | workspace load failure, rule execution failure, unexpected exception, (将来) plugin timeout / plugin crash |

#### NF5.3 構造化ログ（`--log-format json` 時）フィールド

| フィールド | 型 | 説明 |
|---|---|---|
| `event` | string | `run_start` \| `phase_end` \| `run_end` \| `error` |
| `runId` | string | 実行識別子 |
| `toolVersion` | string | ツールバージョン |
| `command` | string | 実行コマンド文字列 |
| `path` | string | 解析対象パス |
| `format` | string | `sarif` \| `json` |
| `exitCode` | number | 0 \| 1 \| 2 |
| `durMs` | number | 経過時間（ms） |
| `phase` | string | `workspace_load` \| `rules` \| `output_write` |
| `counts` | object | `{ projects, documents, rulesExecuted, findingsError, findingsWarning, findingsNote }` |
| `error` | object | `{ category, message }`（message は短く非機密） |

### 1-3. MSBuildWorkspace ネットワーク制御方法

`MSBuildWorkspace.OpenSolutionAsync()` はデザインタイムビルド中に NuGet パッケージ復元を暗黙的にトリガーする場合がある。

**制御手段**（data-network-policy.md §E より）:

| 手段 | 説明 |
|---|---|
| `EnableNuGetPackageRestore=false` 環境変数 | NuGet 自動復元を無効化（MSBuild プロパティ） |
| 事前に `dotnet restore` 実行 | パッケージをローカルキャッシュに保存 |
| `RestorePackagesPath` を指定 | ローカルディレクトリのパッケージのみ使用 |
| `NuGet.Config` の `packageRestore/enabled = False` | 復元全体を無効化 |

ADR-0005 では `EnableNuGetPackageRestore=false` 相当の設定適用と「未 restore 時は STDERR エラー + 終了コード 2」を決定した。

### 1-4. プラグイン × OTel 将来課題

ADR-0003 のプラグインワーカープロセス（Phase 2+）に OTel を導入した場合、以下の課題が生じる:
- ホストプロセスのトレースコンテキスト（TraceId / SpanId）をワーカープロセスへ伝播する必要がある（W3C TraceContext 相当）
- 現在の IPC 設計（Phase 2 ADR 未作成）に TraceId / SpanId 埋め込みフィールドを追加する必要がある
- **判断**: Phase 2+ 以降の課題として ADR-0004 Notes に記録。Phase 1 では対応不要

---

## 2. 作成した ADR 一覧と判断根拠の要約

### ADR-0004: OpenTelemetry Observability Strategy (opt-in)

**判断**: `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による完全 opt-in

**根拠**:
- OTel 標準環境変数を唯一のゲートとすることで、ツール固有フラグを追加せずに制約（no telemetry by default）を満たす
- Docker CLI（`DOCKER_CLI_OTEL_EXPORTER_OTLP_ENDPOINT`）と otel-cli（`OTEL_EXPORTER_OTLP_ENDPOINT`）の採用事例が同パターンを採用
- Semgrep Issue #10408（OTel SDK を必須依存関係にした失敗例）から条件付き PackageReference を選択
- opentelemetry-dotnet Issue #5102 から Flush 保証として `IHost` パターンを選択

**対象シグナル**: Traces + Metrics のみ（Logs は NF5.2/NF5.3 NDJSON が担当）

### ADR-0005: Network Policy and Air-Gap Execution

**判断**: 「no network calls」= ランタイム時アウトバウンド通信禁止。ビルド時 NuGet restore は対象外

**根拠**:
- 業界調査（data-network-policy.md §4）で確認された主流の解釈と一致
- Roslyn Analyzers / StyleCop.Analyzers は設計上ネットワーク通信ゼロ（Roslyntic と同等ポジション）
- SonarScanner（サーバー必須）との対比で、ローカル完結 SARIF 出力はエアギャップ CI 環境での差別化要素
- ADR-0001 の「no network calls」スコープ不明確ギャップ（data-internal-scan.md §3）を解消

---

## 3. 実装時の注意事項

### STDERR 汚染防止

OTel SDK は自身のデバッグログを STDERR に出力する場合がある。ADR-0001 では「STDERR は人間向け診断情報（progress, warnings, execution errors, timing）」と規定されており、SDK の自己テレメトリが混入すると契約違反となる。

**対策**:
```bash
# 環境変数で OTel SDK の自己ログを無効化
OTEL_LOG_LEVEL=none
```

または SDK 初期化時に明示的に自己テレメトリを無効化する API を呼び出す。

### Flush 保証タイミング

短寿命プロセス（静的解析ツールは数秒〜数十秒で終了）では `BatchSpanProcessor`（デフォルト 5000ms 間隔）と `PeriodicExportingMetricReader`（デフォルト 60 秒間隔）が export 完了前にプロセス終了するリスクがある（opentelemetry-dotnet Issue #5102）。

**必須パターン**:
```csharp
// 方法 1（最推奨）: IHost を using で管理 → Dispose 時に自動 Flush
using var host = builder.Build();
await host.RunAsync();

// 方法 2: 明示的 ForceFlush（IHost を使わない場合）
tracerProvider.ForceFlush();
meterProvider.ForceFlush();
```

**補助設定**:
```csharp
// バッチ間隔を CLI に適した短い値に設定（デフォルト 5000ms は CLI に長すぎる）
options.ScheduledDelayMilliseconds = 1000;
```

### 条件付き PackageReference の記法

Semgrep Issue #10408 の教訓から、OTel SDK は必須依存関係にしない。実装時の選択肢:

```xml
<!-- 方法 A: MSBuild 条件付き参照（ビルド時フラグで制御） -->
<PackageReference Include="OpenTelemetry" Version="1.15.0"
                  Condition="'$(EnableOTel)' == 'true'" />

<!-- 方法 B: 別プロジェクト（Roslyntic.Telemetry 等）に分離し、
           メインプロジェクトからは ProjectReference で条件参照 -->
```

方法 A はシンプル。方法 B は依存関係の境界が明確になる。プロジェクト構成が固まった Phase 1 実装時に選択する。

---

## 4. 調査不可・未確認事項

| 項目 | 状況 | 理由・補足 |
|---|---|---|
| .NET OTel SDK 初期化コストの実測値（ms） | **未確認** | 公開ベンチマークが存在しない。静的解析ツールの文脈では数十 ms 程度と推定するが推測。実装後に計測を推奨 |
| `MSBuildWorkspace` が NuGet 復元を暗黙的にトリガーするかの直接再現検証 | **未実施** | ローカル環境でのコード実行が必要（Web 調査範囲外）。実装フェーズでの動作確認を推奨 |
| `Roslyntic.Core` / `Roslyntic.Analysis` 等の PackageReference | **調査不可** | 対応する `.csproj` がリポジトリに存在しない（スケルトン未作成段階） |
| `EnableNuGetPackageRestore=false` の将来 MSBuild バージョンでの動作保証 | **未確認** | 公式の廃止予告は見つからないが、MSBuild 内部挙動の変更リスクは存在する |
| Semgrep Issue #10408 の解決状況 | **未確認（2025年12月時点で Open）** | 2026年3月時点での最新状況は調査時に未確認。教訓としての価値は変わらない |
| ADR-0003 プラグイン × OTel コンテキスト伝播の具体的 IPC 設計 | **Phase 2+ 以降** | 意図的に対象外。Phase 2 ADR で対処 |

---

## 5. 参照資料

| 資料 | パス / URL |
|---|---|
| OTel 実装パターン詳細 | `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` |
| ネットワークポリシー比較 | `.takt/runs/20260306-013809-cli/reports/data-network-policy.md` |
| 内部コードスキャン結果 | `.takt/runs/20260306-013809-cli/reports/data-internal-scan.md` |
| 調査統合レポート | `.takt/runs/20260306-013809-cli/reports/research-report.md` |
| opentelemetry-dotnet Issue #5102 | https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102 |
| Semgrep Issue #10408 | https://github.com/semgrep/semgrep/issues/10408 |
| Docker CLI OTel Docs | https://docs.docker.com/engine/cli/otel/ |
| NuGet Package Restore - MS Learn | https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore |
