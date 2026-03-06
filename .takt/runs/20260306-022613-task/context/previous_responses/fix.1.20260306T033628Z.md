All 65 tests pass (56 unit tests + 9 integration tests). Build succeeds with 0 errors.

## 作業結果
- レビュアー指摘（ARCH・QA・SRE・テスト・要件の5件 REJECT）を全て修正し、ビルド・テスト通過を確認

## 変更内容

| # | Finding | ファイル | 変更内容 |
|---|---------|---------|---------|
| 1 | ARCH-NEW-IAnalysisLogger-L5 | `Roslyntic.Core/Logging/IAnalysisLogger.cs` | `LogInfo` を削除 |
| 2 | ARCH-NEW-IAnalysisLogger-L5 | `Roslyntic.Cli/Logging/StderrLogger.cs` | `LogInfo` 実装を削除 |
| 3 | FINDING-SRE-001 | `Roslyntic.Cli/Commands/CheckCommand.cs` | 出力フェーズ（Serialize→Console.WriteLine）を独立した `try/catch` で囲み、例外時に `return 2` |
| 4 | QA-NEW-001 / REQ-NEW-Integration-Skip | `Roslyntic.Tests/Integration/CheckCommandIntegrationTests.cs` | 全9テストの `[Fact(Skip = SkipReason)]` を `[Fact]` に変更、`SkipReason` 定数を削除 |
| 5 | FINDING-001 (testing) | 同上 | `StderrDoesNotPolluteSarif` を修正：STDERR に SARIF JSON が含まれないこと＋STDOUT が valid SARIF であることを検証 |
| 6 | RepoRoot パス誤り | 同上 | `..` を5個→4個に修正（テスト出力ディレクトリからリポジトリルートへの正確なパス） |
| 7 | QA-NEW-002 | `Roslyntic.Tests/Roslyntic.Tests.csproj` | `Roslyntic.Cli` への `ProjectReference` を追加、Roslyn パッケージ `4.12.0→4.13.0` に更新 |
| 8 | REQ-NEW-SarifSerializer-L60 | `Roslyntic.Sarif/SarifSerializer.cs` | `Uri = d.Location.FilePath` を `Uri = new Uri(Path.GetFullPath(d.Location.FilePath)).AbsoluteUri` に変更 |
| 9 | REQ-NEW-SarifSerializer-L60 | `Roslyntic.Tests/Sarif/SarifSerializerTests.cs` | テスト L175 の期待値を `file:///` URI 形式に更新 |
| 10 | REQ-NEW-LayerViolationRule-L52 | `Roslyntic.Rules/Arch/LayerViolationRule.cs` | プロジェクトレベル検出から構文レベル検出に変更：`UsingDirectiveSyntax` をセマンティックモデルで解決し、実際の依存構文ノードの位置を報告 |
| 11 | パッケージ競合 | `Roslyntic.Rules/Roslyntic.Rules.csproj` | `Microsoft.CodeAnalysis.CSharp.Workspaces 4.12.0→4.13.0` に更新 |

## ビルド結果
- `dotnet build`: **成功** (0 Errors, 2 Warnings NU1903 のみ)

## テスト結果
- `dotnet test`: **Passed! - Failed: 0, Passed: 65, Skipped: 0, Total: 65, Duration: 14 s**
  - 単体テスト: 56件 ✅
  - 統合テスト: 9件 ✅（全 Skip 解除済み）

## 証拠
- CLI 実行確認：`file:///...samples/Samples.UI/Controller.cs` の `using Samples.Infrastructure;`（L1:1-30）を AGARCH0001 として正しく報告
- AGCOMP0001 も `Controller.Process` メソッド（complexity=17）を正しく報告
- STDOUT のみに SARIF、STDERR は空（非汚染）