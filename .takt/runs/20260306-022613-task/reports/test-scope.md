# 変更スコープ宣言

## タスク
Roslyntic MVP の TDD テスト先行作成（複雑度計算・レイヤー分類・診断ソート・SARIF シリアライズ・CLI 統合テストを網羅）

## 変更予定
| 種別 | ファイル |
|------|---------|
| 作成 | `Roslyntic.Tests/Roslyntic.Tests.csproj` |
| 作成 | `Roslyntic.Tests/Analysis/CyclomaticComplexityCalculatorTests.cs` |
| 作成 | `Roslyntic.Tests/Core/LayerClassifierTests.cs` |
| 作成 | `Roslyntic.Tests/Core/DiagnosticSortingTests.cs` |
| 作成 | `Roslyntic.Tests/Sarif/SarifSerializerTests.cs` |
| 作成 | `Roslyntic.Tests/Integration/CheckCommandIntegrationTests.cs` |

## 推定規模
Large

## 影響範囲
- `Roslyntic.Analysis.CyclomaticComplexityCalculator` — 複雑度計算の正確性（15 テスト）
- `Roslyntic.Core.LayerClassifier` — プロジェクト名からのレイヤー推論（11 テスト）
- `Roslyntic.Core.DiagnosticSorter` — 決定論的ソート順（8 テスト）
- `Roslyntic.Sarif.SarifSerializer` — SARIF 2.1.0 出力の形式・必須フィールド・決定論（13 テスト）
- CLI エンドツーエンド — `samples/Samples.sln` を使った SARIF 出力検証（8 テスト、implement フェーズまで Skip）