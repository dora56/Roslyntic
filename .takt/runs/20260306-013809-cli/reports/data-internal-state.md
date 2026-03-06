# 内部ファイル調査レポート (data-internal-state)

**調査日**: 2026-03-06
**調査担当**: TAKT Digger (dig.part-1-internal-files)
**対象ムーブメント**: 実装・ADR作成に必要な情報収集

---

## 1. ADRフォーマットのテンプレート（実例）

出典: `docs/adr/ADR-0001-output-contract.md`、`docs/adr/ADR-0002-sarif-strategy.md`、`docs/adr/ADR-0003-plugin-safety-model.md`

### ヘッダー構成（H1 + フラットフィールド）

```markdown
# ADR-XXXX: タイトル
Status: Accepted
Date: YYYY-MM-DD
```

- H1見出しに番号とタイトルを記載
- `Status:` と `Date:` はH1の直下にフラットテキストで記載（見出しなし）

### セクション構成（H2見出し）

```markdown
## Context
## Decision
## Consequences
## Alternatives considered
## Notes
```

- すべてH2（`##`）で統一
- セクション順序: Context → Decision → Consequences → Alternatives considered → Notes
- Notesは参照リンクや補足のみ

### 具体的なDecisionセクションの書き方

箇条書き（`-`）で決定事項を列挙。サブ箇条書きはインデントで表現。

```markdown
## Decision
- 決定1: ...
- 決定2:
  - サブ1)
  - サブ2)
- 決定3: ...
```

ADR-0002の例（数値リスト使用なし、ハイフン箇条書きのみ）:
```markdown
## Decision
- Adopt SARIF 2.1.0 as the default output format (JSON via --format json).
- Minimal required fields:
  - version: "2.1.0"
  - runs[].tool.driver (name, version, informationUri)
```

---

## 2. 「no network calls / no telemetry」の正確な文言

出典: `docs/adr/ADR-0001-output-contract.md` — Decisionセクション（L23）

### 引用（ADR-0001 Decisionセクション）

```
- Local-only execution: no network calls, no telemetry by default.
```

### CLAUDE.md の文言（AGENTS.md）

```
Local-only execution: **no network calls** and no telemetry by default.
```

**差異**: CLAUDE.md では「no network calls」が太字（強調）。ADR-0001ではコンマで区切ったリスト形式。本質的な意味は同一。

### ADR-0001 Decision セクション全文（L12-L23）

```markdown
## Decision
- STDOUT is machine-readable only: SARIF 2.1.0 or JSON. No decoration or ANSI.
- STDERR is for humans: progress, warnings, execution errors, timing, observability logs.
- Deterministic ordering for all diagnostics:
  1) file path (ordinal string comparison)
  2) start line
  3) start column
  4) ruleId
- Exit codes:
  - 0: no findings above threshold
  - 1: findings exist (analysis succeeded)
  - 2: tool execution failure
- Local-only execution: no network calls, no telemetry by default.
```

---

## 3. ADR-0003プラグインとOTelコンテキスト伝播の課題

出典: `docs/adr/ADR-0003-plugin-safety-model.md`、`analysis-1.md` ギャップC

### ADR-0003 Decision（Phase 2+）引用

```markdown
## Decision
- Phase 1 (MVP): no plugin support; built-in rules only.
- Phase 2+:
  - Run plugins in an isolated worker process (no in-process execution).
  - Enforce timeouts; kill the worker if execution exceeds the limit.
  - Report deterministic failures:
    - AGSAFE9001 for plugin timeout
    - AGSAFE9002 for plugin crash
    - Include plugin name, phase, and timeout duration in the message
  - Exit with code 2 if any plugin fails to load.
- Explicitly disallow:
  - In-process Assembly.LoadFrom for untrusted plugins
  - Dynamic compilation of untrusted source without isolation
  - Reflection-based discovery without validation
```

### OTelコンテキスト伝播の問題点

- **問題**: Phase 2+ でプラグインを「独立したワーカープロセス」で実行する設計となっている
- **OTel導入時の影響**: メインプロセスのOTelトレースコンテキスト（TraceId/SpanId）をワーカープロセスへ伝播させるには、W3C TraceContext等のコンテキスト伝播プロトコルをIPCメッセージに組み込む必要が生じる
- **具体的な設計上の問題**:
  1. IPCプロトコル（名前付きパイプ/ソケット等）にヘッダーフィールドの追加が必要
  2. ワーカープロセス側にもOTel SDKが必要（依存関係が増加）
  3. `AGSAFE9001`（timeout）/`AGSAFE9002`（crash）のエラー報告とトレーススパンの終了タイミングの整合性が難しい
  4. ワーカープロセス起動コスト（プロセス分離）とOTel初期化コストが重なる

- **重要度**: 低（Phase 2以降の話。MVP段階では非該当）
- **対処**: Phase 2 IPC設計ADRで規定すべき事項

---

## 4. 現行NDJSON仕様（NF5.2/NF5.3）

出典: `docs/observability.md`

### NF5.2 必須シグナル（STDERR またはログファイルへ出力）

```
### NF5.2 Minimum signals (must emit)
Emit the following signals to STDERR and/or log file:

**Timing**
- total duration
- phase durations:
    - workspace load
    - rule execution
    - output write

**Scale**
- projects count
- documents count

**Quality signals**
- rules executed count
- findings count by level (error/warning/note)

**Failure classification**
- workspace load failure
- rule execution failure
- unexpected exception
- (future) plugin timeout / plugin crash
```

### NF5.3 構造化ログ仕様（`--log-format json` 時）

```
### NF5.3 Structured logs (when enabled)
When `--log-format json` is set:
- output newline-delimited JSON (NDJSON): one JSON object per line
- keys must be stable across versions
- do not include source code content
```

### NF5.3 スキーマ（フィールド定義）

| フィールド | 型 | 値域 |
|---|---|---|
| `event` | string | `run_start` \| `phase_end` \| `run_end` \| `error` |
| `runId` | string | 実行識別子 |
| `toolVersion` | string | ツールバージョン |
| `command` | string | 実行コマンド文字列 |
| `path` | string | 解析対象パス |
| `format` | string | `sarif` \| `json` |
| `exitCode` | number | `0` \| `1` \| `2` |
| `durMs` | number | 経過時間（ms） |
| `phase` | string | `workspace_load` \| `rules` \| `output_write` |
| `counts` | object | `{ projects, documents, rulesExecuted, findingsError, findingsWarning, findingsNote }` |
| `error` | object | `{ category, message }`（messageは短く非機密） |

### OTel LogsでNDJSONを置き換えない理由の裏付け

- NF5.2は「STDERR またはログファイルに出力」と明記。OTel LogsはOTLPエクスポーター経由で外部送信が前提であり、デフォルトOFF（「no telemetry by default」）と矛盾
- NDJSONのキーは「バージョン間で安定」が要件。OTel Logs の仕様変化に依存することはリスク
- NF5.3の`--log-file`オプションはローカルファイルへの書き込みを想定。OTelは通常ローカルファイルエクスポーターを別途追加する必要がある
- **結論**: NDJSONはデフォルト動作のオブザーバビリティ要件を満たす最小実装。OTel LogsはNDJSONの置き換えではなく、opt-in追加シグナルとして共存すべき

---

## 5. OTel実装コードスニペット（パターンD）

出典: `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` §4「パターンD: .NETでの短寿命プロセス実装例」

### パターンD 完全コードスニペット

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

- **採用根拠**: opentelemetry-dotnet 推奨パターン（Issue #5102 の回答）
- **シグナル**: Traces + Metrics（LogsはオプションでNDJSONがカバー）
- **出典1**: [GitHub Issue #5102](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5102)
- **出典2**: [OTLP Exporter README](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)

### Flush保証パターン（短寿命プロセス対応）

`BatchSpanProcessor`のデフォルト間隔（5000ms）はCLIの短寿命プロセスに不適。以下で対処:

| 方法 | 説明 |
|---|---|
| `using var app = builder.Build()` | Host.Dispose()でLoggerFactory自動Flush。**最推奨** |
| `TracerProvider.ForceFlush()` | プロセス終了前に明示呼び出し |
| `ScheduledDelayMilliseconds = 1000` | デフォルト5000msを短縮 |

---

## 6. MSBuildWorkspace NuGet制御

出典: `.takt/runs/20260306-013809-cli/reports/data-network-policy.md` §2-E、§5

### NuGet復元を回避する手段一覧

| 手段 | 説明 | 正確なAPI/設定名 |
|---|---|---|
| **事前に `dotnet restore` 実行** | パッケージをグローバルキャッシュに保存 | `dotnet restore <solution>` |
| **環境変数で自動復元を無効化** | NuGet自動復元をプロセス全体で無効化 | `EnableNuGetPackageRestore=false` |
| **NuGet.Config でグローバル無効化** | プロジェクト/ソリューション単位で復元全体を無効化 | `<packageRestore><add key="enabled" value="False" /></packageRestore>` |
| **ローカルディレクトリ指定** | 特定ディレクトリのパッケージのみ使用 | `RestorePackagesPath` プロパティ |
| **`--no-restore` フラグ（build/run時）** | 暗黙的復元をスキップ（CLI引数） | `dotnet build --no-restore` |

### MSBuildWorkspaceの動作メカニズム

```
OpenSolutionAsync() → デザインタイムビルドを内部実行
→ ソースファイル・参照・コンパイルオプションを取得（バイナリ出力なし）
→ NuGetパッケージ参照の解決を試みる場合がある
  → グローバルキャッシュ(~/.nuget/packages)にあり → ネットワーク通信なし
  → キャッシュになし → nuget.org等へダウンロード（ネットワーク通信あり）
```

**出典**: [Using MSBuildWorkspace - Gist by DustinCampbell](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)

---

## 7. OTel SDKパッケージ情報

出典: `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` §3（NuGet.org、2026-03-06取得）

| パッケージ | 最新バージョン | ダウンロードサイズ | .NET 9.0/10.0 依存数 |
|---|---|---|---|
| `OpenTelemetry` | 1.15.0 | 782.97 KB (≒0.76 MB) | 3 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.0 | 489.93 KB (≒0.49 MB) | 1 (OpenTelemetry ≥ 1.15.0) |
| `OpenTelemetry.Extensions.Hosting` | 1.15.0 | 89.4 KB (≒0.09 MB) | 2 |
| **合計（3パッケージ）** | — | **≒ 1.36 MB** | — |

**注記**:
- 推移的依存関係（`OpenTelemetry.Api`等）を含めると実際のサイズはさらに大きくなる
- `OpenTelemetry.Extensions.Hosting`はIHost/IHostedService連携に必要。CLIでHostを使わない場合は省略可能
- .NET OTel SDK初期化コストの実測値は公開ベンチマーク未発見。静的解析ツール（MSBuildWorkspaceロードで数秒〜数十秒）の文脈では数十ms程度と推定（推測値）

---

## 8. csproj現在状態

出典: `Roslyntic.Cli/Roslyntic.Cli.csproj`（直接読み込み）

### 現在のRoslyntic.Cli.csproj全文

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <OutputType>Exe</OutputType>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
    </PropertyGroup>

</Project>
```

### 確認事項

| 項目 | 値 |
|---|---|
| `<Project Sdk>` | `Microsoft.NET.Sdk`（通常SDK形式） |
| `<OutputType>` | `Exe` |
| `<TargetFramework>` | `net10.0` |
| `<ImplicitUsings>` | `enable` |
| `<Nullable>` | `enable` |
| `<PackageReference>` | **0件（未定義）** |

### Program.cs現在状態

```csharp
// See https://aka.ms/new-console-template for more information

Console.WriteLine("Hello, World!");
```

**完全なスケルトン（3行）**。OTelコード・ネットワーク通信コードなし。

### 他プロジェクトの状況

`docs/architecture.md`によると以下のプロジェクトが設計上存在するが、`.csproj`は**未作成**:
- `Roslyntic.Core`
- `Roslyntic.Analysis`
- `Roslyntic.Rules`
- `Roslyntic.Sarif`
- `Roslyntic.Tests`

---

## 9. ADR-0003プラグイン課題サマリー

出典: `docs/adr/ADR-0003-plugin-safety-model.md`、`analysis-1.md` ギャップC

| 課題 | 詳細 |
|---|---|
| **プロセス分離のIPC設計** | ADR-0003では「isolated worker process」と決定しているが、IPC詳細は別ADRに委ねられている（Phase 2） |
| **OTelコンテキスト伝播** | OTelをopt-in導入した場合、TraceId/SpanIdをIPCメッセージで伝播する設計が必要（W3C TraceContextヘッダー相当） |
| **プラグイン側OTel依存** | ワーカープロセスでトレースを継続するにはプラグイン側にもOTel SDKが必要 |
| **エラーとスパン終了の整合** | AGSAFE9001(timeout)/AGSAFE9002(crash)発生時のスパン終了処理が複雑になる |
| **現在フェーズでの影響** | Phase 1 MVP（ビルトインルールのみ）では非該当。設計時の留意事項として記録 |

---

## 10. 調査できなかった項目

| 項目 | 理由 |
|---|---|
| `Roslyntic.Core`等のcsproj内PackageReference | 対応する`.csproj`がリポジトリに未作成（スケルトン段階） |
| .NET OTel SDK初期化コストの実測値（ms） | 公開ベンチマークが存在しない（CLI + .NETの短寿命プロセスシナリオ） |
| MSBuildWorkspaceがNuGet復元を実際にトリガーするかの確認 | ローカル実行が必要。Web調査範囲外 |
| `OpenTelemetry.Api.ProviderBuilderExtensions`の個別サイズ | NuGetページで個別確認できなかった |

---

## 付録: 読み込んだファイル一覧

| ファイル | 状態 |
|---|---|
| `docs/adr/ADR-0001-output-contract.md` | 読み込み完了 |
| `docs/adr/ADR-0002-sarif-strategy.md` | 読み込み完了 |
| `docs/adr/ADR-0003-plugin-safety-model.md` | 読み込み完了 |
| `docs/observability.md` | 読み込み完了 |
| `docs/architecture.md` | 読み込み完了 |
| `.takt/runs/20260306-013809-cli/reports/research-report.md` | 読み込み完了 |
| `.takt/runs/20260306-013809-cli/reports/analysis-1.md` | 読み込み完了 |
| `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` | 読み込み完了 |
| `.takt/runs/20260306-013809-cli/reports/data-network-policy.md` | 読み込み完了 |
| `.takt/runs/20260306-013809-cli/reports/data-internal-scan.md` | 読み込み完了 |
| `Roslyntic.Cli/Roslyntic.Cli.csproj` | 読み込み完了 |
| `Roslyntic.Cli/Program.cs` | 読み込み完了 |
