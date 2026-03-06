## arch-review
全ファイルの確認が完了しました。レビュー結果をまとめます。

---

## アーキテクチャレビュー結果

### 結果: APPROVE

---

## 前回 finding の追跡

| finding_id | 状態 | 根拠 |
|---|---|---|
| ARCH-NEW-IAnalysisLogger-L5 | **resolved** | `Roslyntic.Core/Logging/IAnalysisLogger.cs` は `LogWarning`/`LogError` の2メソッドのみ（`LogInfo` 削除確認）。`StderrLogger.cs` も同様に2実装のみ。 |

---

## 確認した観点

- [x] **構造・設計**: 各プロジェクトの責務分離・レイヤー構成に問題なし
- [x] **モジュール化**: Core/Rules/Analysis/Sarif/Cli の分割は高凝集・低結合・循環依存なし
- [x] **関数設計**: 1関数1責務が守られている。`ResolveAssemblyName` の分離も適切
- [x] **コード品質**: デッドコードなし
- [x] **変更スコープ**: 前回 finding への修正が適切に限定されている
- [x] **テストカバレッジ**: 統合テスト9件（全 Skip 解除済）・単体テスト56件で仕様をカバー
- [x] **呼び出しチェーン**: `Program → CheckCommand → IRule.AnalyzeAsync → DiagnosticSorter → SarifSerializer → Console.WriteLine` の連鎖に欠落・逆依存なし
- [x] **契約文字列のハードコード散在**: `"AGARCH0001"` / `"AGCOMP0001"` / `"2.1.0"` 等は各クラス内の定数・フィールドに集約済み。外部散在なし

---

## 新規指摘

なし。

---

## APPROVE 判定理由

- 前回ブロッキング finding（ARCH-NEW-IAnalysisLogger-L5）が解消され、`IAnalysisLogger` は実際に呼ばれる `LogWarning`/`LogError` の2メソッドのみになった
- `LayerViolationRule` のシンタックスレベル検出・`SarifSerializer` の URI 変換・統合テストの Skip 解除・`CheckCommand` の出力フェーズ分離、いずれも設計上の問題なし
- ブロッキング問題（new / persists）がゼロのため APPROVE

---

## security-review
全ての変更ファイルを確認しました。セキュリティレビューの結果をまとめます。

---

## セキュリティレビュー結果

## 判定: APPROVE

## チェック結果

| カテゴリ | 結果 | 詳細 |
|---------|------|------|
| インジェクション（SQL/コマンド/XSS） | ✅ | 問題なし |
| 認証・認可 | ✅ | ローカルCLIツール、認証不要 |
| データ保護・機密情報露出 | ✅ | 問題なし |
| 暗号化 | ✅ | 暗号処理なし |
| ファイル操作・パストラバーサル | ✅ | 問題なし |
| 依存関係 | ✅ | バージョン固定済み標準ライブラリのみ |

---

## 各変更の詳細評価

### 1. `IAnalysisLogger.cs` / `StderrLogger.cs` — `LogInfo` 削除
セキュリティ上の問題なし。インターフェースのサーフェス縮小は望ましい方向。

### 2. `CheckCommand.cs` — シリアライズフェーズの独立 try/catch 追加
- L99: `logger.LogError($"Analysis failed: {ex.Message}")` — 例外メッセージはSTDERR（人間向け）に出力。AGENTS.md の「STDERR is for humans」に準拠。機密情報は含まれない。
- L116: `logger.LogError($"Serialization failed: {ex.Message}")` — 同上。
- コマンドインジェクションなし（MSBuild API直接呼び出し）。

### 3. `SarifSerializer.cs` — `file://` URI 変換
- L60: `new Uri(Path.GetFullPath(d.Location.FilePath)).AbsoluteUri` — Roslynのコンパイルモデルから取得したパスを正規化。ユーザー制御の入力ではなく、解析対象コードのソースパス。
- SARIF に絶対パスが含まれるのは仕様通り（SARIF 2.1.0 の ArtifactLocation は URI を要求）。

### 4. `LayerViolationRule.cs` — 構文レベル検出への変更
- Roslyn のセマンティックモデル経由の解析。シェル実行なし、外部通信なし。
- `document.FilePath ?? document.Name` — 診断出力のパス情報として使用。シェル/SQL コンテキストに渡されない。
- `ResolveAssemblyName` はNull安全に実装されている（`?.` 演算子使用）。

### 5. `CheckCommandIntegrationTests.cs` — Skip 解除・パス修正
- `ProcessStartInfo.ArgumentList.Add(a)` — シェル文字列ではなく引数リストを使用。コマンドインジェクション不可。
- テスト用パスは `AppContext.BaseDirectory` からの相対パスで構築（ユーザー制御入力なし）。

---

## 警告（非ブロッキング）

| finding_id | 状態 | 内容 |
|-----------|------|------|
| SEC-WARN-001 | persists | `Roslyntic.Cli/Program.cs` L13: `CancellationToken.None` — Ctrl+C によるグレースフルシャットダウンが機能しない。セキュリティ問題ではなく運用上の懸念。変更外ファイルのため非ブロッキング。 |

---

## 総括

今回の変更（`LogInfo`削除、シリアライズ try/catch 追加、`file://` URI 正規化、構文レベルレイヤー違反検出）はいずれもセキュリティ脆弱性を導入していない。OWASP Top 10 の各カテゴリに該当する問題はなく、AGENTS.md の「no network calls」「STDOUT is machine-readable only」「STDERR is for humans」の制約にも適合している。

---

## qa-review
全ての変更ファイルを確認しました。テスト結果も `65/65 passed` です。

---

# QAレビュー

## 結果: APPROVE

## 前回指摘の追跡

| finding_id | 前回状態 | 今回状態 | 根拠 |
|------------|---------|---------|------|
| QA-NEW-001 | new | **resolved** | `CheckCommandIntegrationTests.cs` 全9件 `[Fact]` に変更済み。`SkipReason` 定数も削除済み（L1-L199確認） |
| QA-NEW-002 | new | **resolved** | `Roslyntic.Tests.csproj` L30 に `<ProjectReference Include="..\Roslyntic.Cli\Roslyntic.Cli.csproj" />` 追加済み |

## 確認した観点

| 観点 | 結果 | 備考 |
|------|------|------|
| テストカバレッジ | ✅ | 統合テスト9件 Skip解除済み、単体テスト56件合計65件全て pass |
| テスト品質 | ✅ | 統合テストが CLI バイナリ実行→SARIF検証→終了コード検証・STDOUT/STDERR分離・決定論性を網羅 |
| エラーハンドリング | ✅ | `CheckCommand.cs` L103-118: 出力フェーズを独立 try/catch で保護し return 2 実装済み |
| ログ / モニタリング | ✅ | `IAnalysisLogger` から `LogInfo` 削除済み。`StderrLogger` が STDERR のみに出力 |
| 保守性 | ✅ | `LayerViolationRule` が構文ノードレベルの精確な位置を報告。`SarifSerializer` が `file://` URI 形式で絶対パス化 |

## 新規指摘

なし。

## テスト実行結果

```
Passed! - Failed: 0, Passed: 65, Skipped: 0, Total: 65, Duration: 14 s
```

単体テスト56件（CyclomaticComplexityCalculator / LayerClassifier / DiagnosticSorter / SarifSerializer）、統合テスト9件（CheckCommand E2E: 終了コード0/1/2・SARIF構造・ルールID・決定論性・順序・STDERR分離）の全件合格を確認。

## APPROVE 理由

- 前回REJECTの原因であったQA-NEW-001・QA-NEW-002が共に解消済み
- 今回変更の全ファイル（`IAnalysisLogger.cs`, `StderrLogger.cs`, `CheckCommand.cs`, `SarifSerializer.cs`, `LayerViolationRule.cs`, `CheckCommandIntegrationTests.cs`, `Roslyntic.Tests.csproj`）においてブロッキング問題なし
- 全65テスト合格によりリグレッションなし

---

## testing-review
全テストファイルとテスト計画の確認が完了しました。以下にレビュー結果をまとめます。

---

## テストレビュー

### 結果: APPROVE

### 前回 REJECT 指摘（FINDING-001）の解消確認

| finding_id | 状態 | 根拠 |
|------------|------|------|
| FINDING-001 | resolved | `StderrDoesNotPolluteSarif`（L177-187）が修正済み。`Assert.DoesNotContain("{\"version\"", stderr, ...)` によるSTDERR非汚染検証を追加し、`StdoutIsValidJson` とは異なるアサーション構造になった |

**詳細**:
- 修正前: `StderrDoesNotPolluteSarif` と `StdoutIsValidJson` が同一の `JsonValueKind.Object` アサーションを持つ DRY 違反
- 修正後: `StderrDoesNotPolluteSarif` は `Assert.DoesNotContain("{\"version\"", stderr)` + `version == "2.1.0"` の複合検証、`StdoutIsValidJson` は `JsonValueKind.Object` 確認と主目的・アサーションが明確に分離された

---

### テスト計画との突合

| テストクラス | 計画数 | 実装数 | 判定 |
|------------|-------|-------|------|
| CyclomaticComplexityCalculatorTests | 15 | 16（超過） | ✅ |
| LayerClassifierTests | 11メソッド | 11メソッド（Theory含む18ケース） | ✅ |
| DiagnosticSortingTests | 8 | 7 | ⚠️ Warning（非ブロッキング） |
| SarifSerializerTests | 13 | 13（Theory含む15ケース） | ✅ |
| CheckCommandIntegrationTests | 8 | 9（`Check_NonExistentPath_ExitsWithCode2`を追加） | ✅ |

`DiagnosticSortingTests` は計画8件に対し7件（1件少ない）が実装されているが、全4ソートキー（パス・行・列・ruleId）とエッジケース（空・単一要素・冪等性）がカバーされており機能的に充足している。

---

### テスト品質評価

**Given-When-Then 構造**:
- `CheckCommandIntegrationTests`: 全テストに `// Given` / `// When` / `// Then` コメントあり ✅
- `CyclomaticComplexityCalculatorTests`: 主要テストにコメントあり ✅
- `DiagnosticSortingTests`: セクションコメントで構造が明確 ✅
- `SarifSerializerTests`: セクション区分と Given/When/Then が統一 ✅
- `LayerClassifierTests`: `Classify_ProjectEndingWithUI_*` はコメントあり、Application/Domain/Infrastructure の Theory テストはコメント省略。テスト名で意図が読み取れるため許容範囲

**命名規約**:
全テストが `[Subject]_[Condition]_[Expected]` 形式で統一されており、テスト名から振る舞いが読み取れる ✅

**テスト独立性・再現性**:
- ユニットテスト: 外部状態・実行順序依存なし ✅
- 統合テスト: `RunCliAsync` ヘルパーで I/O 完全分離、CLIバイナリとサンプルソリューションへの依存は E2E として正当 ✅
- Skipなし（全9統合テストが `[Fact]` で実行可能）✅

**モック・フィクスチャ**:
- `MakeDiagnostic` / `MakeMetadata` / `ParseMethod` ヘルパーで共通化されており、テストごとにフィクスチャが独立 ✅
- `SarifSerializerTests`: `MakeDiagnostic` の `filePath = "/src/Foo.cs"` はLinuxパス前提だが、プロダクションコードと同じ `new Uri(Path.GetFullPath(...)).AbsoluteUri` 変換で期待値が組まれており一貫性あり ✅

**過不足チェック**:
- 新しい振る舞い（AGARCH0001 位置報告、SARIF URI フォーマット、出力フェーズの独立 try/catch、exit code 2）にすべて対応するテストが存在する ✅
- 不要なテスト: なし

---

### 非ブロッキング（Warning）

| # | 場所 | 内容 |
|---|------|------|
| W-001 | `DiagnosticSortingTests.cs` | 計画8件に対し7件実装（1件少ない）。現在のカバレッジは機能的に充足しているが、機会があれば追加を検討 |
| W-002 | `CheckCommandIntegrationTests.cs` L185-186 | `StderrDoesNotPolluteSarif` 内の `version == "2.1.0"` アサーションは `OutputIsSarif21` と重複するが、STDERR非汚染のサニティチェックとして意味があるため許容範囲 |

---

### 総合判定: APPROVE

前回 REJECT の原因であった FINDING-001（DRY 違反・テスト名と検証内容の乖離）が適切に解消された。テスト計画のカバレッジは充足しており、構造・命名・独立性・再現性いずれも品質基準を満たしている。

---

## requirements-review
---

## 要件充足レビュー

### 結果: APPROVE

---

## 前回指摘の追跡

| finding_id | 前回状態 | 今回状態 | 根拠 |
|------------|----------|----------|------|
| REQ-NEW-Integration-Skip | new | **resolved** | `CheckCommandIntegrationTests.cs` 全9テスト（L48, L59, L70, L85, L103, L121, L133, L177, L189）が `[Fact]` のみ。`SkipReason` 定数・`Skip =` 属性ゼロ件を確認 |
| REQ-NEW-SarifSerializer-L60 | new | **resolved** | `SarifSerializer.cs` L60: `new Uri(Path.GetFullPath(d.Location.FilePath)).AbsoluteUri` に変更済み。`SarifSerializerTests.cs` L175: `new Uri(Path.GetFullPath("/src/Bar.cs")).AbsoluteUri` に期待値変更済み |
| REQ-NEW-LayerViolationRule-L52 | new | **resolved** | `LayerViolationRule.cs` L57-83: `document.GetSyntaxRootAsync()` → `DescendantNodes().OfType<UsingDirectiveSyntax>()` で構文レベル走査し、`usingDir.GetLocation().GetLineSpan()` から実際の行/列を報告。`StartLine: 1, StartColumn: 1` の合成ロケーション問題は解消 |

---

## 要件照合

### plan.md / docs/rules.md から抽出した主要要件

| # | 要件 | 実装箇所 | 充足状態 |
|---|------|---------|---------|
| 1 | STDOUT は SARIF/JSON のみ | `CheckCommand.cs` L110: `Console.WriteLine(sarif)` のみ。エラーはすべて `logger.LogError/LogWarning` → STDERR | ✅ |
| 2 | 終了コード 0/1/2 | L22-25 (返 2), L112 (`sorted.Count > 0 ? 1 : 0`), L99-100 (返 2), L116-117 (返 2) | ✅ |
| 3 | SARIF `artifactLocation.uri` が `file:///` 形式 | `SarifSerializer.cs` L60: `AbsoluteUri` | ✅ |
| 4 | 決定論的ソート (path→line→column→ruleId, Ordinal) | `DiagnosticSorter.cs` L11-14: `OrderBy FilePath Ordinal`, `ThenBy StartLine`, `ThenBy StartColumn`, `ThenBy RuleId Ordinal` | ✅ |
| 5 | AGARCH0001 — Location が依存を導入する構文ノード | `LayerViolationRule.cs` L77-83: `UsingDirectiveSyntax` の行/列を報告 | ✅ |
| 6 | AGARCH0001 — Properties: fromLayer/toLayer/fromSymbol/toSymbol/dependencyKind | `LayerViolationRule.cs` L85-92 で5プロパティすべて出力 | ✅ |
| 7 | AGCOMP0001 — Properties: methodName/complexity/threshold | `CyclomaticComplexityRule.cs` L60-65 | ✅ |
| 8 | AGCOMP0001 — Location がメソッド識別子領域 | `CyclomaticComplexityRule.cs` L49: `method.Identifier.GetLocation()` | ✅ |
| 9 | SARIF にタイムスタンプ/GUID なし | `SarifSerializer.cs` — `SarifDocument`/`SarifRun` に該当フィールドなし | ✅ |
| 10 | `IAnalysisLogger` は LogWarning/LogError のみ（LogInfo なし） | `IAnalysisLogger.cs` L5-6: 2メソッドのみ | ✅ |
| 11 | 統合テスト全件通過 | `CheckCommandIntegrationTests.cs` 全9件 `[Fact]`（前回報告で65件全通過確認済み） | ✅ |
| 12 | 出力フェーズ例外時に exit 2 | `CheckCommand.cs` L103-118: serialize/write を独立 `try/catch` で囲み `return 2` | ✅ |

---

## スコープクリープ確認

変更内容を精査した結果、要求にない追加変更は検出されなかった。  
パッケージバージョン更新（`4.12.0→4.13.0`）は既存競合解消のための修正であり正当。

---

## ブロッキング問題

なし。

---

## 判定: **APPROVE**

前回指摘 3件（REQ-NEW-Integration-Skip / REQ-NEW-SarifSerializer-L60 / REQ-NEW-LayerViolationRule-L52）がすべて解消され、主要要件 12項目を実コードで確認した。ブロッキング問題なし。

---

## sre-review
コードの確認が完了しました。レビュー結果をまとめます。

---

# SREレビュー結果

## 結果: APPROVE

---

## 前回 open findings の追跡

| finding_id | 前回状態 | 今回状態 | 根拠 |
|------------|----------|----------|------|
| FINDING-SRE-001 | new (REJECT) | **resolved** | `CheckCommand.cs` L103–118 に独立した `try/catch` が追加され、`SarifSerializer.Serialize()` がスローした場合に `return 2` を返す経路が確立された |

**確認詳細（FINDING-SRE-001 解消）:**

```csharp
// CheckCommand.cs L103–118（現在のコード）
try
{
    var sorted    = DiagnosticSorter.Sort(allDiagnostics).ToList();
    var rulesMeta = rules.Select(r => r.Metadata);
    var sarif     = SarifSerializer.Serialize(sorted, rulesMeta, ToolVersion);
    Console.WriteLine(sarif);
    return sorted.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    logger.LogError($"Serialization failed: {ex.Message}");
    return 2;
}
```

前回指摘した `SarifSerializer.MapLevel()` の `ArgumentOutOfRangeException`（`SarifSerializer.cs:103`）は、このブロックで捕捉され exit code `2` として確定する。終了コード契約（`0`/`1`/`2`）が全障害パスで成立している。

---

## 新規ブロッキング問題

なし。

---

## SRE観点スコアカード

| 観点 | 判定 | 根拠 |
|------|------|------|
| SLO/SLI 影響 | ✅ | 終了コード契約が全障害パスで守られており、CI/CD の障害検知が機能する |
| 障害時挙動（timeout/retry/fallback） | ✅ | 分析フェーズ・シリアライズフェーズそれぞれに独立した障害境界があり、いずれも `return 2` で安全に終了する |
| 劣化動作（連鎖障害防止） | ✅ | コンパイル失敗プロジェクトは `LogWarning + continue`（L83–85）で部分劣化し、ツールはクラッシュしない |

FINDING-SRE-001 が解消されており、新規ブロッキング問題も検出されないため **APPROVE** とする。