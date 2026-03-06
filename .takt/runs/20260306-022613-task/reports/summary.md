# タスク完了サマリー

## タスク
Roslyntic MVP の全面実装。`roslyntic check <path>` コマンドが SARIF 2.1.0 を STDOUT に出力し、AGARCH0001（レイヤー違反）・AGCOMP0001（循環的複雑度超過）の2ルールを決定論的に検出する CLI ツールを実装した。

## 結果
完了

## 変更内容
| 種別 | ファイル | 概要 |
|------|---------|------|
| 変更 | `Roslyntic.Cli/Program.cs` | stub（Hello World）から CLI エントリポイントに全面置き換え |
| 変更 | `Roslyntic.Cli/Roslyntic.Cli.csproj` | MSBuildLocator・System.CommandLine 等の依存追加 |
| 変更 | `Roslyntic.slnx` | 全プロジェクト（Core/Analysis/Rules/Sarif/Tests）を追加 |
| 作成 | `Roslyntic.Cli/Commands/CheckCommand.cs` | check サブコマンド実装（MSBuild ロード・ルール実行・SARIF 出力・終了コード制御） |
| 作成 | `Roslyntic.Cli/Logging/StderrLogger.cs` | STDERR 専用ロガー実装 |
| 作成 | `Roslyntic.Core/Models/` | `RoslynticDiagnostic`・`DiagnosticLocation`・`DiagnosticLevel`・`RuleMetadata` レコード |
| 作成 | `Roslyntic.Core/Rules/IRule.cs` | ルールインターフェース定義 |
| 作成 | `Roslyntic.Core/Logging/IAnalysisLogger.cs` | ロガーインターフェース（LogWarning/LogError） |
| 作成 | `Roslyntic.Core/DiagnosticSorter.cs` | 決定論的ソート（パス→行→列→ruleId、Ordinal 比較） |
| 作成 | `Roslyntic.Analysis/CyclomaticComplexityCalculator.cs` | if/switch/for/while/catch/??/&&/\|\| 等をカウントする計算機 |
| 作成 | `Roslyntic.Rules/Arch/LayerClassifier.cs` | アセンブリ名から UI/Application/Domain/Infrastructure を判定 |
| 作成 | `Roslyntic.Rules/Arch/LayerViolationRule.cs` | AGARCH0001：禁止エッジ（UI→Infrastructure, UI→Domain, Domain→Infrastructure）を using 構文ノードレベルで検出 |
| 作成 | `Roslyntic.Rules/Complexity/CyclomaticComplexityRule.cs` | AGCOMP0001：閾値15超のメソッドを検出 |
| 作成 | `Roslyntic.Sarif/SarifSerializer.cs` | SARIF 2.1.0 JSON 出力（タイムスタンプ/GUID なし・file:/// 絶対 URI） |
| 作成 | `Roslyntic.Tests/Analysis/CyclomaticComplexityCalculatorTests.cs` | 複雑度計算のユニットテスト |
| 作成 | `Roslyntic.Tests/Core/DiagnosticSorterTests.cs` | 決定論的ソートのユニットテスト |
| 作成 | `Roslyntic.Tests/Core/LayerClassifierTests.cs` | レイヤー分類のユニットテスト |
| 作成 | `Roslyntic.Tests/Sarif/SarifSerializerTests.cs` | SARIF 出力のユニットテスト（スナップショット） |
| 作成 | `Roslyntic.Tests/Integration/CheckCommandIntegrationTests.cs` | E2E 統合テスト9件（終了コード・SARIF 正当性・ruleId・ソート・STDERR 分離） |
| 作成 | `samples/Samples.sln` | サンプルソリューション |
| 作成 | `samples/Samples.Domain/` | ドメイン層サンプルプロジェクト |
| 作成 | `samples/Samples.Application/` | アプリケーション層サンプルプロジェクト |
| 作成 | `samples/Samples.Infrastructure/` | インフラ層サンプルプロジェクト |
| 作成 | `samples/Samples.UI/Controller.cs` | 禁止依存（UI→Infrastructure）と複雑度超過メソッドを意図的に含む |

## 確認コマンド
```bash
dotnet build Roslyntic.slnx
dotnet test Roslyntic.Tests/Roslyntic.Tests.csproj
```