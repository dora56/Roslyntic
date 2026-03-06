# 変更スコープ宣言

## タスク
Roslyntic MVP の全面実装（テストがパスするようにプロダクションコードを実装する）

## 変更予定
| 種別 | ファイル |
|------|---------|
| 作成 | `Roslyntic.Core/Roslyntic.Core.csproj` |
| 作成 | `Roslyntic.Core/Models/DiagnosticLevel.cs` |
| 作成 | `Roslyntic.Core/Models/DiagnosticLocation.cs` |
| 作成 | `Roslyntic.Core/Models/Diagnostic.cs` |
| 作成 | `Roslyntic.Core/Models/RuleMetadata.cs` |
| 作成 | `Roslyntic.Core/Rules/IRule.cs` |
| 作成 | `Roslyntic.Core/Logging/IAnalysisLogger.cs` |
| 作成 | `Roslyntic.Core/LayerClassifier.cs` |
| 作成 | `Roslyntic.Core/DiagnosticSorter.cs` |
| 作成 | `Roslyntic.Analysis/Roslyntic.Analysis.csproj` |
| 作成 | `Roslyntic.Analysis/CyclomaticComplexityCalculator.cs` |
| 作成 | `Roslyntic.Analysis/DependencyExtractor.cs` |
| 作成 | `Roslyntic.Rules/Roslyntic.Rules.csproj` |
| 作成 | `Roslyntic.Rules/GlobalUsings.cs` |
| 作成 | `Roslyntic.Rules/Arch/LayerViolationRule.cs` |
| 作成 | `Roslyntic.Rules/Complexity/CyclomaticComplexityRule.cs` |
| 作成 | `Roslyntic.Sarif/Roslyntic.Sarif.csproj` |
| 作成 | `Roslyntic.Sarif/SarifSerializer.cs` |
| 変更 | `Roslyntic.Cli/Roslyntic.Cli.csproj` |
| 変更 | `Roslyntic.Cli/Program.cs` |
| 作成 | `Roslyntic.Cli/GlobalUsings.cs` |
| 作成 | `Roslyntic.Cli/Commands/CheckCommand.cs` |
| 作成 | `Roslyntic.Cli/Logging/StderrLogger.cs` |
| 作成 | `samples/Samples.Domain/Samples.Domain.csproj` |
| 作成 | `samples/Samples.Domain/Entity.cs` |
| 作成 | `samples/Samples.Application/Samples.Application.csproj` |
| 作成 | `samples/Samples.Application/ApplicationService.cs` |
| 作成 | `samples/Samples.Infrastructure/Samples.Infrastructure.csproj` |
| 作成 | `samples/Samples.Infrastructure/Repository.cs` |
| 作成 | `samples/Samples.UI/Samples.UI.csproj` |
| 作成 | `samples/Samples.UI/Controller.cs` |
| 作成 | `samples/Samples.sln` |
| 変更 | `Roslyntic.slnx` |

## 推定規模
Large

## 影響範囲
- `Roslyntic.Core` — 共有モデル（Diagnostic / DiagnosticLocation / DiagnosticLevel / RuleMetadata）・IRule インターフェース・LayerClassifier・DiagnosticSorter・IAnalysisLogger
- `Roslyntic.Analysis` — CyclomaticComplexityCalculator（if/else if/switch case/for/foreach/while/do/catch/?:/&&/|| をカウント）・DependencyExtractor
- `Roslyntic.Rules` — AGARCH0001 LayerViolationRule・AGCOMP0001 CyclomaticComplexityRule（デフォルト閾値 15）
- `Roslyntic.Sarif` — SARIF 2.1.0 シリアライザー（タイムスタンプ・GUID なし、決定論的出力）
- `Roslyntic.Cli` — Program.cs（コマンドディスパッチ）・CheckCommand（MSBuildWorkspace 経由でルール実行・終了コード制御）・StderrLogger
- `samples/` — 統合テスト用 4 プロジェクト（UI→Infrastructure/Domain の禁止依存と複雑度超過メソッドを意図的に含む）