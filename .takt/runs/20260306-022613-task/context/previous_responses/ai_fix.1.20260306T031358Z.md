54テスト全て通過しました。

---

## 確認したファイル
- `Roslyntic.Analysis/DependencyExtractor.cs`（L1-15）: クラス全体が未使用を確認
- `Roslyntic.Sarif/SarifSerializer.cs`（L22-89, L132-136）: `Serialize()` シグネチャ・`SarifRule` 定義・`MapLevel()` フォールバックを確認
- `Roslyntic.Cli/Commands/CheckCommand.cs`（L39-92）: `OpenSolutionAsync()` のみで `.csproj` 非対応を確認
- `Roslyntic.Analysis/CyclomaticComplexityCalculator.cs`（L20-33）: `SwitchExpressionArmSyntax` 欠落を確認
- `Roslyntic.Tests/Sarif/SarifSerializerTests.cs`（全体）: 旧シグネチャ呼び出しを確認
- `Roslyntic.Tests/Analysis/CyclomaticComplexityCalculatorTests.cs`（全体）: `SwitchExpressionArm` テスト欠落を確認

## 実行した検索
- `grep -n "SwitchExpressionArmSyntax"` → 修正後に存在確認
- `grep -n "DependencyExtractor"` → 削除後に存在しないことを確認
- `grep -n "IEnumerable<RuleMetadata>"` → `SarifSerializer.cs` L24 に存在確認
- `grep -n "ArgumentOutOfRangeException"` → `SarifSerializer.cs` L103 に存在確認

## 修正内容
- **FINDING-001**: `Roslyntic.Analysis/DependencyExtractor.cs` を削除（未使用のデッドコード）
- **FINDING-002**: `SarifSerializer.Serialize()` に `IEnumerable<RuleMetadata>` パラメータを追加、`SarifRule` に `shortDescription`/`fullDescription`/`helpUri`/`properties.category` フィールドを追加し、メタデータから値を設定するよう実装
- **FINDING-003**: `CheckCommand.cs` を `.sln`/`.slnx` は `OpenSolutionAsync()`、`.csproj` は `OpenProjectAsync()` で分岐するよう変更。また `rules.Select(r => r.Metadata)` を収集して `Serialize()` に渡すよう変更
- **FINDING-004**: `CyclomaticComplexityCalculator.cs` に `SwitchExpressionArmSyntax => 1` を追加。テスト追加
- **FINDING-005**: `MapLevel()` の `_ => "warning"` を `throw new ArgumentOutOfRangeException(...)` に変更
- テスト更新: `SarifSerializerTests.cs` の全 `Serialize()` 呼び出しを新シグネチャに合わせて更新（`NoMetadata` ヘルパー・`MakeMetadata()` ヘルパー追加）、必須フィールドの検証テスト追加

## テスト結果
```
dotnet test Roslyntic.Tests/Roslyntic.Tests.csproj --no-build
Passed! - Failed: 0, Passed: 54, Skipped: 9, Total: 63
```
（Skipped 9件は統合テスト・既存のスキップ設定によるもの）

[STEP:1]