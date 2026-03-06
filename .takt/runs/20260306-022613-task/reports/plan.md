# タスク計画

## 元の要求

`order.md` の内容は "プロジェクトの" のみで欠落しているが、プロジェクトドキュメント群（`docs/agent-playbook.md`, `docs/architecture.md`, `docs/rules.md`, `docs/observability.md`, ADR-0001〜0003）から **Roslyntic MVP の全面実装**タスクであることが確認できた。

## 分析結果

### 目的

`roslyntic check <path> [--format sarif|json] [--fail-on warning|error]` コマンドを動作させ、SARIF 2.1.0 を STDOUT に出力する MVP を実装する。

- AGARCH0001（レイヤー違反）・AGCOMP0001（循環的複雑度超過）の 2 ルールを実装
- STDOUT は SARIF/JSON のみ、STDERR はログ/エラー（混在禁止）
- 決定論的順序（パス→行→列→ruleId、Ordinal 比較）を保証
- 終了コード: 0（findings なし）、1（findings あり）、2（ツール失敗）

### 参照資料の調査結果

| ドキュメント | 主要な要件 |
|------------|----------|
| `docs/agent-playbook.md` | MVP ゴール、CLI 契約、テスト要件、DoD |
| `docs/architecture.md` | プロジェクト分割・内部モデル・決定論ルール |
| `docs/rules.md` | ルール ID・デフォルト閾値・SARIF マッピング要件 |
| `docs/observability.md` | STDERR ログ・フェーズ計測・NDJSON オプション |
| `ADR-0001` | STDOUT/STDERR 分離・終了コード・Ordinal ソート |
| `ADR-0002` | SARIF 2.1.0 必須フィールド・タイムスタンプ/GUID 省略 |

**現状との差分**: `Roslyntic.Cli/Program.cs:3` に `Console.WriteLine("Hello, World!")` のみ。その他のプロジェクト（Core / Analysis / Rules / Sarif / Tests）および `samples/` は存在しない。ソリューションファイル（`Roslyntic.slnx`）も Cli のみ参照。

### スコープ

| 対象 | 変更要否 | 根拠 |
|------|---------|------|
| `Roslyntic.Cli/Program.cs` | **要（全面実装）** | stub のみ（L:3） |
| `Roslyntic.Cli/Roslyntic.Cli.csproj` | **要（依存追加）** | System.CommandLine 未追加 |
| `Roslyntic.slnx` | **要** | 他プロジェクトへの参照なし |
| `Roslyntic.Core/` | **要（新規）** | 存在しない |
| `Roslyntic.Analysis/` | **要（新規）** | 存在しない |
| `Roslyntic.Rules/` | **要（新規）** | 存在しない |
| `Roslyntic.Sarif/` | **要（新規）** | 存在しない |
| `Roslyntic.Tests/` | **要（新規）** | 存在しない |
| `samples/` | **要（新規）** | 存在しない |

### 検討したアプローチ

| アプローチ | 採否 | 理由 |
|-----------|------|------|
| 共有モデルを独立プロジェクト（Roslyntic.Models）に切り出す | 不採用 | MVP スケールでは過剰分割。Core に含める |
| Roslyntic.Core が Rules を直接参照する | 不採用 | Core → Rules の依存が生じ、逆転リスクがある。Cli が Rules を Core に渡す形を採る |
| Sarif を Core に統合する | 不採用 | SARIF は交換可能な出力形式。分離することで JSON 出力と対称に扱える |
| NativeAOT 対応 | スコープ外 | AGENTS.md に明記「out of scope」 |
| プラグイン（Phase 2 ルール） | スコープ外 | ADR-0003 により Phase 2+ |

### 実装アプローチ

#### プロジェクト依存グラフ（循環なし）

```
Roslyntic.Cli
  ├── Roslyntic.Core
  ├── Roslyntic.Rules
  └── Roslyntic.Sarif

Roslyntic.Rules
  ├── Roslyntic.Core   (IRule, 共有モデル)
  └── Roslyntic.Analysis

Roslyntic.Sarif
  └── Roslyntic.Core   (Diagnostic モデルのみ)

Roslyntic.Analysis
  └── (Microsoft.CodeAnalysis.* のみ)
```

#### 実装順序

**Step 1: ソリューション構成**
- 各 `.csproj` を作成し `Roslyntic.slnx` に追加
- プロジェクト間参照を依存グラフどおりに設定
- ターゲットフレームワーク: `net10.0`（既存 Cli に合わせる）

**Step 2: Roslyntic.Core**
- `Models/Diagnostic.cs` — `record Diagnostic(string RuleId, DiagnosticLevel Level, string Message, Location Location, IReadOnlyDictionary<string,string>? Properties, string? Fingerprint)`
- `Models/Location.cs`, `Models/Region.cs`, `Models/DiagnosticLevel.cs`, `Models/RuleMetadata.cs`
- `Rules/IRule.cs`, `Rules/AnalysisContext.cs`
- `Logging/IAnalysisLogger.cs`（STDERR/ファイルへの抽象）
- `Workspace/WorkspaceLoader.cs`（MSBuildLocator + MSBuildWorkspace）
- `Pipeline/AnalysisPipeline.cs`（ルール実行 + 決定論的ソート）

**Step 3: Roslyntic.Analysis**
- `Complexity/CyclomaticComplexityCalculator.cs`（`if/else if/switch case/for/foreach/while/do/catch/?:/&&/||` をカウント）
- `Dependencies/DependencyExtractor.cs`（セマンティックモデルからプロジェクト間依存を抽出）

**Step 4: Roslyntic.Rules**
- `Arch/LayerClassifier.cs`（`*.UI` / `*.Application` / `*.Domain` / `*.Infrastructure` のパターンマッチ）
- `Arch/LayerViolationRule.cs`（AGARCH0001、禁止エッジ: UI→Infrastructure, UI→Domain, Domain→Infrastructure）
- `Complexity/CyclomaticComplexityRule.cs`（AGCOMP0001、デフォルト閾値 15）

**Step 5: Roslyntic.Sarif**
- `Models/SarifDocument.cs`（SARIF 2.1.0 POCOs、`System.Text.Json` シリアライズ用）
- `SarifWriter.cs`（Diagnostic[] → SARIF 2.1.0 JSON）
- `JsonWriter.cs`（Diagnostic[] → 簡易 JSON）

**Step 6: Roslyntic.Cli**
- `Logging/StderrLogger.cs`, `Logging/NdJsonLogger.cs`
- `Commands/CheckCommand.cs`（`System.CommandLine` ベース）
- `Program.cs`（コマンド登録・DI 配線・終了コード制御）

**Step 7: samples/**
- 4 プロジェクト（Domain / Application / Infrastructure / UI）
- `Samples.UI.csproj` が `Samples.Infrastructure` を参照（禁止依存を意図的に含む）
- `Samples.UI/Controller.cs` に複雑度 > 15 のメソッドを含む

**Step 8: Roslyntic.Tests**
- ユニットテスト: `CyclomaticComplexityCalculatorTests`, `LayerClassifierTests`, `SarifWriterTests`（スナップショット）
- 統合テスト: `samples/` で `roslyntic check` を実行し SARIF の正当性・ruleId の存在・ソート安定性を検証

---

## 実装ガイドライン

### 必須制約

- **STDOUT 汚染禁止**: 進捗・エラー・タイミングはすべて `Console.Error` またはロガー経由。`Console.WriteLine` は最終出力（SARIF/JSON）のみ
- **決定論的ソート**: `StringComparer.Ordinal` を使用。`StringComparer.CurrentCulture` は非決定的になるため禁止

```csharp
diagnostics
    .OrderBy(d => d.Location.FilePath, StringComparer.Ordinal)
    .ThenBy(d => d.Location.Region.StartLine)
    .ThenBy(d => d.Location.Region.StartColumn)
    .ThenBy(d => d.RuleId, StringComparer.Ordinal)
```

- **MSBuildLocator 初期化順**: `MSBuildLocator.RegisterDefaults()` は `MSBuildWorkspace.Create()` より前に一度だけ呼ぶ
- **SARIF にタイムスタンプ/GUID 禁止**: `invocationStartTimeUtc`・実行 GUID は出力しない（決定論的出力のため）
- **ファイルサイズ 300 行以内**: 超える場合は責務単位で分割
- **TODO コメント禁止**: 実装するか削除

### SARIF の uri フォーマット

`file:///` 形式のフルパス（URI）を使用。相対パスは CI 環境で動作が変わるため禁止。

### 終了コード

| コード | 条件 |
|--------|------|
| `0` | findings なし（分析成功） |
| `1` | findings あり（分析成功） |
| `2` | ツール実行失敗（ワークスペースロード失敗等） |

### ファイル参照（Coder 向け）

- `Roslyntic.Cli/Roslyntic.Cli.csproj:1-10` — `net10.0`, Nullable enabled, ImplicitUsings の設定を他プロジェクトで踏襲
- `Roslyntic.Cli/Program.cs:1-3` — 全面置き換え対象

---

## スコープ外

| 項目 | 除外理由 |
|------|---------|
| NativeAOT 対応 | `AGENTS.md` に明記「out of scope」 |
| プラグイン安全モデル（AGSAFE9001） | ADR-0003 により Phase 2+ |
| AGARCH0002（依存サイクル検出） | `docs/rules.md` に Phase 2 と明記 |
| `roslyntic rules init/list`, `roslyntic explain` | `docs/agent-playbook.md` に Phase 2 と明記 |
| ベースライン比較（`--baseline`） | README に "planned" と記載、MVP スコープ外 |

---

## 確認事項

1. **`order.md` の欠落内容**: ファイルは "プロジェクトの" のみで切れている。ドキュメントから MVP 実装タスクと判断したが、追加スコープや優先順位の指定がある場合は確認が必要。
2. **MSBuild バージョン**: `Microsoft.Build.Locator.RegisterDefaults()` は .NET SDK の MSBuild を自動検出する。開発環境・CI 環境での動作確認が必要。
3. **`System.CommandLine` バージョン**: GA 版（2.0+）と beta 版で API が異なる。`net10.0` に対応した安定版を使用すること。