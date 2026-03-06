# 調査レポート

## 調査概要

RoslynticにおけるOpenTelemetry（OTel）の関係性と、ツールのネットワーク通信必要性について調査した。コードベースの現状スキャンと、外部OSS事例・類似ツールのネットワークポリシー比較を通じて、「no network calls / no telemetry by default」制約を維持しつつOTel統合を可能にするアーキテクチャ案を整理する。

---

## 主要な発見

- **現時点でOTel・ネットワーク通信コードはともにゼロ**。Roslynticは完全なスケルトン段階（`Program.cs` 3行）であり、PackageReferenceも0件
- **`OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による完全opt-in**はすべての制約（「no telemetry by default」「STDOUT汚染なし」「決定論的出力」）と両立する
- **ADR-0001「no network calls」はランタイム時のアウトバウンド通信禁止**と解釈するのが業界標準。ビルド時NuGet restoreは「事前工程」として除外されるが、ADR上は明文化されていない（中重要ギャップ）
- **Semgrep失敗教訓**: OTel SDKを必須依存関係にすると、opt-in機能でも全ユーザーにパッケージサイズ・依存関係競合の影響が出る（Issue #10408未解決）。条件付き参照が必須
- **競争優位**: ローカル完結SARIF出力はSonarScanner（サーバー必須）と対極的な設計で、エアギャップCI環境における差別化要素となる

---

## 調査結果

### 1. コードベース現状スキャン

| 検索対象 | キーワード | 結果 |
|---|---|---|
| `**/*.cs`（1件） | OpenTelemetry, ActivitySource, OTLP, Meter, Tracer | **検出なし** |
| `**/*.csproj`（1件） | 同上 + PackageReference全件 | **PackageReference: 0件** |
| `**/*.cs`（1件） | HttpClient, WebClient, WebRequest, TcpClient | **検出なし** |

Roslynticは現時点で実装前のスケルトン段階。`Roslyntic.Core`/`Roslyntic.Analysis`/`Roslyntic.Rules`/`Roslyntic.Sarif`の各プロジェクトの`.csproj`は未作成。

**ADR-0001のスコープ確認**（`docs/adr/ADR-0001-output-contract.md`）:

| スコープ | 制約の対象 |
|---|---|
| ランタイム時（分析実行中）のアウトバウンド通信 | **対象**（明示） |
| テレメトリ送信 | **対象**（明示） |
| ビルド時・NuGet restore | **明文化なし**（ギャップ） |

---

### 2. CLIツールにおけるOpenTelemetry採用パターン

#### 2-1. OSS採用事例（7ツール調査）

| プロジェクト | OTel採用 | opt-in方式 | シグナル |
|---|---|---|---|
| **Docker CLI** | ◎ | 環境変数 `DOCKER_CLI_OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は完全無効 | Metrics |
| **Semgrep** | ◎ | `--trace` フラグ必須 | Traces |
| **otel-cli** | ◎ | `OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時はnon-recording mode | Traces |
| **dotnet-monitor** | ✗ | Prometheus形式エンドポイントのみ（OTel非採用） | — |
| **Trivy** | ✗ | 独自匿名統計のみ（OTel非採用） | — |
| **dotnet-trace/counters** | ✗ | EventPipeプロトコル使用（OTel非採用） | — |

CLIツールでのOTel採用は**少数派**。採用する場合は環境変数またはCLIフラグによる完全opt-inが標準パターン。

#### 2-2. 短寿命プロセス特有の課題と対策

**Flushタイミング問題**: `BatchSpanProcessor`（デフォルト5000ms間隔）はCLIの短寿命プロセスではexportされないまま終了するリスクがある。

対策:

| 方法 | 説明 |
|---|---|
| `using var host = builder.Build()` | IHost.Dispose()でLoggerFactory自動Flush（最推奨） |
| `TracerProvider.ForceFlush()` | プロセス終了前に明示呼び出し |
| `ScheduledDelayMilliseconds = 1000` | デフォルト5000msを短縮 |

出典: [opentelemetry-dotnet #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)、[#2979](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2979)

#### 2-3. .NET OTel SDKパッケージサイズ（NuGet.org、2026-03-06取得）

| パッケージ | バージョン | サイズ |
|---|---|---|
| `OpenTelemetry` | 1.15.0 | 782.97 KB |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB |
| `OpenTelemetry.Extensions.Hosting` | 1.15.0 | 89.4 KB |
| **合計** | — | **≒ 1.36 MB** |

---

### 3. ネットワークポリシー比較

#### 3-1. 類似ツールのネットワークポリシー（5ツール）

| ツール | ポリシー | オフライン対応 | 無効化手段 |
|---|---|---|---|
| **Roslyn Analyzers** | 通信なし（設計上） | ◎ 完全オフライン | N/A |
| **StyleCop.Analyzers** | 通信なし | ◎ 完全オフライン | N/A |
| **SonarScanner for .NET** | サーバー接続必須 | ✗ オフライン不可 | なし（サーバーURL必須） |
| **Semgrep** | 部分的オフライン | △ フラグ組み合わせで可 | `--disable-version-check --metrics=off --oss-only` |
| **MSBuildWorkspace** | 条件付き | △ キャッシュ済みなら可 | `EnableNuGetPackageRestore=false` |

#### 3-2. 業界標準の「no network calls」解釈

調査で確認された業界標準的解釈:
1. **「no network calls」= 分析ランタイム時のアウトバウンド通信禁止**が主流
2. **NuGet restoreは「ビルド前工程」**として分離（`dotnet restore`済みを前提とするツールが多い）
3. **テレメトリはopt-out可能であること**が事実上の要件（.NET SDK自体が模範例: `DOTNET_CLI_TELEMETRY_OPTOUT=1`）
4. **エアギャップ環境での「no network calls」は絶対要件**（通信試行によるタイムアウトがCI/CDを著しく遅延させる）

出典: [6 telemetry best practices for CLI tools - marcon.me](https://marcon.me/articles/cli-telemetry-best-practices/)

---

### 4. 統合的改善提案

#### OTel opt-in導入方針

| 判断ポイント | 推奨 |
|---|---|
| デフォルト動作 | OTel完全無効（`OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時） |
| opt-in方法 | 環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` を設定するだけで有効化 |
| シグナル | Traces + Metricsを優先（LogsはNDJSONで既にカバー済み） |
| Flush保証 | `using var host = builder.Build()` パターン必須 |
| パッケージ依存 | 条件付き参照（Semgrep Issue #10408の教訓を反映） |
| STDERR汚染対策 | `OTEL_LOG_LEVEL=none` またはOTel自己テレメトリの明示的無効化 |
| 制約との整合性 | ✅「no telemetry by default」・「STDOUT汚染なし」・「決定論的出力」すべてと両立 |

**.NET実装例**:
```csharp
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
if (otlpEndpoint is not null)
{
    services.AddOpenTelemetry()
        .WithTracing(b => b.AddSource("Roslyntic").AddOtlpExporter())
        .WithMetrics(b => b.AddMeter("Roslyntic").AddOtlpExporter());
}
// using var host = builder.Build(); で自動Flush保証
```

#### ネットワークポリシー方針

| 判断ポイント | 推奨 |
|---|---|
| ADR-0001スコープの明確化 | 「ランタイム中のアウトバウンド通信禁止」と明文化（ビルド時除外を明示） |
| MSBuildWorkspace NuGet通信 | 「`dotnet restore`済みを前提条件とする」をドキュメントに必須記載 |
| NuGet自動復元の制御 | `EnableNuGetPackageRestore=false` または `--no-restore` の適用を検討 |
| バージョンチェック | 実装しない（SARIFの `tool.driver.version` に埋め込むのみ） |
| エアギャップ対応 | 事前NuGetキャッシュ + スタンドアローン実行ファイル配布のガイドを提供 |

---

## データソース

| # | ソース | 種別 | 信頼度 |
|---|--------|------|--------|
| 1 | `Roslyntic.Cli/Program.cs`（直接スキャン） | コードベース | High |
| 2 | `Roslyntic.Cli/Roslyntic.Cli.csproj`（直接スキャン） | コードベース | High |
| 3 | `docs/adr/ADR-0001-output-contract.md`（直接参照） | コードベース | High |
| 4 | `docs/observability.md`（直接参照） | コードベース | High |
| 5 | [Docker CLI OTel Docs](https://docs.docker.com/engine/cli/otel/) | Web | High |
| 6 | [equinix-labs/otel-cli README](https://github.com/equinix-labs/otel-cli) | Web | High |
| 7 | [semgrep/metrics.md](https://github.com/semgrep/semgrep/blob/develop/metrics.md) | Web | High |
| 8 | [Semgrep Issue #10408](https://github.com/semgrep/semgrep/issues/10408) | Web | High |
| 9 | [NuGet - OpenTelemetry 1.15.0](https://www.nuget.org/packages/OpenTelemetry) | Web | High |
| 10 | [NuGet - OTLP Exporter 1.15.0](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol) | Web | High |
| 11 | [opentelemetry-dotnet #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102) | Web | High |
| 12 | [dotnet/runtime #107158](https://github.com/dotnet/runtime/issues/107158) | Web | High |
| 13 | [SonarScanner Docs](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/scanners/dotnet/using) | Web | High |
| 14 | [Semgrep CLI Reference](https://semgrep.dev/docs/cli-reference) | Web | High |
| 15 | [NuGet Package Restore - MS Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore) | Web | High |
| 16 | [Using MSBuildWorkspace - DustinCampbell](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3) | Web | Medium |
| 17 | [marcon.me - CLI telemetry best practices](https://marcon.me/articles/cli-telemetry-best-practices/) | Web | Medium |
| 18 | [Coroot - OTel Go overhead](https://coroot.com/blog/opentelemetry-for-go-measuring-the-overhead/) | Web | Medium |

---

## 結論と推奨

**現状**: Roslynticはスケルトン段階であり、OTelコード・ネットワーク通信コードともにゼロ。設計フェーズでの意思決定ガイダンスとして本調査の価値がある。

**OTel統合**: `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による完全opt-in方式を採用すれば、「no telemetry by default」「STDOUT汚染なし」「決定論的出力」の全制約と両立可能。Semgrep Issue #10408の教訓から、OTel SDKは条件付き参照（必須依存関係にしない）とすることが必須。

**ネットワークポリシー**: ADR-0001の「no network calls」はランタイム時のアウトバウンド通信禁止と解釈するのが業界標準に合致する。MSBuildWorkspaceのNuGet restoreは「事前工程」として扱い、実行前提条件として「`dotnet restore`済みの環境」をドキュメントに明記すべき。

**競争優位**: ローカル完結SARIF出力の設計はエアギャップCI環境（金融・政府系）で差別化要素となる。この優位性を維持するため、「no network calls by default」ポリシーは堅持すべき。

---

## 残存ギャップ

- **ADR-0001のビルド時スコープ不明確**（重要度: 中）: 「no network calls」がビルド時NuGet restoreを含むかどうかが未明文化。後続ADRまたはREADMEへの明文化を推奨
- **.NET OTel SDK初期化コストの実測値なし**（重要度: 低）: 公開ベンチマークが存在しない。静的解析ツールの文脈では数十ms程度と推定するが推測（実装後に計測を推奨）
- **MSBuildWorkspace NuGetトリガーの実動作確認**（重要度: 低）: ローカル実行でのみ検証可能。実装フェーズでの確認を推奨
- **ADR-0003プラグインワーカーとOTelコンテキスト伝播**（重要度: 低）: 将来的にOTelをopt-in導入した場合、Phase 2+のプラグインワーカープロセスへのコンテキスト伝播がIPC設計に影響する可能性あり。Phase 2 ADRで対処