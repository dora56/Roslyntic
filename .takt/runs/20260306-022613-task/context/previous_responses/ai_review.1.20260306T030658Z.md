## AI Review 結果

**判定: REJECT**

ブロッキング問題を3件検出しました。

---

### BLOCKING 問題

#### FINDING-001 — `DependencyExtractor` がデッドコード
**ファイル**: `Roslyntic.Analysis/DependencyExtractor.cs`

`DependencyExtractor.GetReferencedAssemblyNames()` はコードベース全体で一切呼び出されていない。`LayerViolationRule` は `compilation.ReferencedAssemblyNames` を直接参照しており、このクラスは完全に未使用。"念のため" 追加された典型的なAI生成の未使用コード。

**修正**: `DependencyExtractor.cs` を削除する。

---

#### FINDING-002 — SARIF `rules[]` に必須フィールドが欠落 / `RuleMetadata` が未使用
**ファイル**: `Roslyntic.Sarif/SarifSerializer.cs`（`SarifRule` クラス）

`docs/rules.md` の仕様では `rules[]` エントリに `shortDescription.text`, `fullDescription.text`, `properties.category`, `helpUri` の包含を **must** と明記。しかし `SarifRule` は `id` のみを持ち、他は全て欠落。

`IRule.Metadata`（`RuleMetadata` 型）は両ルールに実装されているが、`CheckCommand.RunAsync` パイプラインで一切参照されず、`SarifSerializer.Serialize()` にも渡されていない。**`RuleMetadata` は実質デッドデータ**。

**修正**: `SarifSerializer.Serialize()` が `IEnumerable<RuleMetadata>` も受け取るよう拡張し、`shortDescription`/`fullDescription`/`properties.category` を出力する。

---

#### FINDING-003 — `.csproj` 対応が未実装なのに usage に記載
**ファイル**: `Roslyntic.Cli/Program.cs:8`, `Roslyntic.Cli/Commands/CheckCommand.cs:47`

usage は `roslyntic check <path-to-sln-or-csproj>` と記載し `.csproj` サポートを示唆するが、実装は `workspace.OpenSolutionAsync(path)` のみ。`.csproj` を渡すと実行時エラー。`docs/architecture.md` にも「Loads .sln/.csproj via MSBuildWorkspace」と明記されており、スコープ内の未実装。

**修正**: 拡張子判定を追加し `.csproj` の場合は `OpenProjectAsync()` を使用する。

---

### WARNING 問題

- **FINDING-004**: `CyclomaticComplexityCalculator.cs` — C# 8+ の switch 式（`SwitchExpressionArmSyntax`）が計算対象外でテストもない
- **FINDING-005**: `SarifSerializer.cs:88` — `MapLevel()` の `_ => "warning"` フォールバックは新しい enum 値を黙って誤マッピングする