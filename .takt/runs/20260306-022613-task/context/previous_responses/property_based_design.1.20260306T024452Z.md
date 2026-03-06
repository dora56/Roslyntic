これで全体像が把握できました。プロパティーベースドテスト計画を作成します。

---

# プロパティーベースドテスト計画

## 1. 対象候補の抽出

| 候補 | 種別 | PBT 適性 |
|------|------|---------|
| `CyclomaticComplexityCalculator` | 計算ロジック（加算的） | ◎ |
| `LayerClassifier` | 文字列パターン分類 | ◎ |
| `DiagnosticSorter` | ソート・順序不変条件 | ◎ |
| `SarifSerializer` | 変換・決定論 | ○ |
| `CheckCommand`（CLI） | E2E | ✗（PBT 不向き、統合テストで対処済み） |

---

## 2. 使用ライブラリ

| ライブラリ | 採択理由 |
|-----------|---------|
| **FsCheck.Xunit** | .NET の標準的 PBT ライブラリ。`[Property]` 属性で xUnit と統合。既存 xUnit テスト環境と共存可能 |

`Roslyntic.Tests.csproj` に追加:
```xml
<PackageReference Include="FsCheck.Xunit" Version="3.*" />
```

---

## 3. 各候補のプロパティ定義

### 3-1. `CyclomaticComplexityCalculator`

**既存テストとの差分:**
- 既存: 特定の構文要素（`if` 1個、`for` 1個…）に対する **固定値** の検証
- PBT: **任意の個数 N** に対する数学的不変条件

#### Property A — 加算性（`if` 文）
> N 個の独立した `if` 文を持つメソッドの複雑度は常に `1 + N`

```
∀ n: int, 0 ≤ n ≤ 50 →
  Complexity(method_with_n_ifs(n)) = 1 + n
```

**生成戦略:**
- `Arb.Generate<int>()` で 0〜50 の整数 n を生成
- `string.Concat(Enumerable.Repeat("if (x) {} ", n))` でメソッドを動的に生成

#### Property B — 下限保証
> 有効な任意のメソッド構文に対して複雑度は常に ≥ 1

```
∀ method →
  Complexity(method) ≥ 1
```

**生成戦略:**
- 既知の構文片（`if/for/while/catch/&&/||/? :`）をランダムに 0〜10 個組み合わせたメソッドを生成するカスタム `Arbitrary<MethodDeclarationSyntax>`

#### Property C — 単調性
> 任意のメソッドに分岐構文を1つ追加すると複雑度は必ず増加する

```
∀ method →
  Complexity(with_one_more_if(method)) > Complexity(method)
```

**既存テストとの補完関係:** 既存は構文ごとの「増分が +1」を個別確認。PBT は「追加で必ず増える」という方向性を大量のランダム入力で確認。

---

### 3-2. `LayerClassifier`

**既存テストとの差分:**
- 既存: InlineData で3種類のプレフィックス × 4レイヤー + 未知3件。固定例のみ
- PBT: **任意の有効な名前空間プレフィックス**に対するパターンマッチ普遍性

#### Property D — 既知サフィックスの普遍的マッチ
> 任意の有効プレフィックス `prefix` に対して `prefix + ".UI"` は常に `Layer.UI` を返す（他3レイヤーも同様）

```
∀ prefix: string (valid namespace segment) →
  Classify(prefix + ".UI")          = Layer.UI
  Classify(prefix + ".Application") = Layer.Application
  Classify(prefix + ".Domain")      = Layer.Domain
  Classify(prefix + ".Infrastructure") = Layer.Infrastructure
```

**生成戦略:**
- `Gen.Identifier()` 相当（英字・数字・`_`・`.` を含むカスタムジェネレータ）で任意プレフィックスを生成
- 空文字を除外

#### Property E — 非サフィックス文字列の棄却
> 既知4サフィックスのいずれで終わらない任意の文字列は常に `null` を返す

```
∀ s: string, ¬EndsWith(s, ".UI") ∧ ¬EndsWith(s, ".Application") ∧ ... →
  Classify(s) = null
```

**生成戦略:**
- `Gen.AlphaNumericString` から生成し、既知サフィックスを除外するフィルタを適用（`Gen.Where()`）

**既存テストとの補完関係:** 既存はプレフィックスの**代表例**を検証。PBT はプレフィックスが何であってもサフィックスルールが成立することを網羅的に確認。

---

### 3-3. `DiagnosticSorter`

PBT 適性が最高。ソートアルゴリズムは古典的なプロパティーベースドテスト対象。

**既存テストとの差分:**
- 既存: 3〜4要素の固定リストで各ソートキーを個別確認
- PBT: 任意サイズ・任意内容のリストに対する**不変条件**

#### Property F — 全順序（4キーの優先順位）
> 任意のリストをソートすると隣接要素間で以下が成立する

```
∀ sorted[i], sorted[i+1] →
  sorted[i].FilePath ≤ordinal sorted[i+1].FilePath
  ∨ (FilePath同一 ∧ Line[i] ≤ Line[i+1])
  ∨ (FilePath・Line同一 ∧ Column[i] ≤ Column[i+1])
  ∨ (FilePath・Line・Column同一 ∧ RuleId[i] ≤ordinal RuleId[i+1])
```

**生成戦略:**
- `Arbitrary<Diagnostic>` をカスタム定義（ファイルパスは英数字＋`.cs` 拡張子、行番号は 1〜9999、列は 0〜999）
- リストサイズ 0〜50

#### Property G — 冪等性
> Sort(Sort(list)) は Sort(list) と等しい（要素単位の比較）

```
∀ list → Sort(Sort(list)) == Sort(list)
```

#### Property H — 要素の保存
> ソート前後で要素数が変わらず、全要素が保持される

```
∀ list → |Sort(list)| = |list| ∧ Sort(list).ToHashSet() ⊇ list.ToHashSet()
```

**既存テストとの補完関係:** 既存は各キーを「単独で変化させた場合」に分けて検証。PBT は全キーが混在する任意のリストで全体的な整合性を確認。

---

### 3-4. `SarifSerializer`

**既存テストとの差分:**
- 既存: 空リスト・単一要素・固定2要素で SARIF 構造を確認
- PBT: 任意の診断リストに対する普遍的不変条件

#### Property I — result 件数の保存
> 任意の診断リストをシリアライズすると `runs[0].results` の件数が入力と等しい

```
∀ diagnostics: Diagnostic[] →
  Parse(Serialize(diagnostics)).results.Length = diagnostics.Length
```

#### Property J — rules セクションの重複排除
> 任意の診断リストに対して rules セクションの ruleId は一意

```
∀ diagnostics →
  rules.Select(r => r.id).Distinct().Count() = rules.Count()
```

#### Property K — 決定論の普遍性（任意入力）
> 任意の診断リストに対して同一入力から2回シリアライズすると結果が一致

```
∀ diagnostics →
  Serialize(diagnostics) = Serialize(diagnostics)
```

**既存テストとの補完関係:** 既存の決定論テストは固定2要素のみ。PBT は任意サイズ・任意 ruleId・任意レベルで決定論が成立することを確認。

---

## 4. テストファイル配置と実装指針

### ファイル構成
```
Roslyntic.Tests/
├── Properties/
│   ├── CyclomaticComplexityProperties.cs   # Property A, B, C
│   ├── LayerClassifierProperties.cs         # Property D, E
│   ├── DiagnosticSorterProperties.cs        # Property F, G, H
│   └── SarifSerializerProperties.cs         # Property I, J, K
└── Properties/Arbitraries/
    ├── DiagnosticArbitraries.cs             # Arbitrary<Diagnostic>, Arbitrary<DiagnosticLocation>
    └── SyntaxArbitraries.cs                 # Arbitrary<MethodDeclarationSyntax>（部分的）
```

### FsCheck 実装パターン（xUnit 統合）

```csharp
// DiagnosticSorterProperties.cs の例
public class DiagnosticSorterProperties
{
    // Property F: 全順序
    [Property(Arbitrary = new[] { typeof(DiagnosticArbitraries) })]
    public bool Sort_AnyList_IsFullyOrdered(List<Diagnostic> diagnostics)
    {
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (CompareByAllKeys(sorted[i], sorted[i + 1]) > 0) return false;
        }
        return true;
    }

    // Property G: 冪等性
    [Property(Arbitrary = new[] { typeof(DiagnosticArbitraries) })]
    public bool Sort_IsIdempotent(List<Diagnostic> diagnostics)
    {
        var once  = DiagnosticSorter.Sort(diagnostics).ToList();
        var twice = DiagnosticSorter.Sort(once).ToList();
        return once.SequenceEqual(twice, DiagnosticEqualityComparer.Instance);
    }

    // Property H: 要素保存
    [Property(Arbitrary = new[] { typeof(DiagnosticArbitraries) })]
    public bool Sort_PreservesAllElements(List<Diagnostic> diagnostics)
    {
        var sorted = DiagnosticSorter.Sort(diagnostics).ToList();
        return sorted.Count == diagnostics.Count
            && diagnostics.All(d => sorted.Contains(d));
    }
}
```

### `DiagnosticArbitraries` 生成仕様

| フィールド | 生成方法 |
|-----------|---------|
| FilePath | `Gen.Elements(new[]{"a.cs","b.cs","c.cs"})` + 任意サブパス（深さ0〜3） |
| StartLine | `Gen.Choose(1, 9999)` |
| StartColumn | `Gen.Choose(0, 999)` |
| RuleId | `Gen.Elements(new[]{"AGARCH0001","AGCOMP0001","AGTEST9999"})` |
| Level | `Gen.Elements(Enum.GetValues<DiagnosticLevel>())` |
| Message | `Arb.Generate<NonEmptyString>().Select(s => s.Get)` |

---

## 5. 優先度と実装順序

| 優先度 | Property | 根拠 |
|--------|----------|------|
| 高 | F, G, H（DiagnosticSorter） | ソート不変条件は高い発見力。既存テストのカバー範囲を最も強化できる |
| 高 | A, B（CyclomaticComplexity） | 加算性・下限は数学的に明確で反例発見が容易 |
| 中 | D, E（LayerClassifier） | プレフィックスの多様性に対して普遍性を確認 |
| 中 | I, J, K（SarifSerializer） | 任意入力での決定論・構造確認 |
| 低 | C（単調性） | 実装コスト高（動的メソッド変換）、F/G で代替可能 |

---

## 6. 既存テストとの補完マトリクス

| ロジック | 既存テストが確認すること | PBT が追加で確認すること |
|---------|----------------------|----------------------|
| 複雑度計算 | 構文種別ごとの固定増分 | 任意N個での総和、下限保証 |
| レイヤー分類 | 代表プレフィックス3種 | 任意プレフィックスでのサフィックスルール普遍性 |
| ソート | 各キー単独変化のソート順 | 任意混合リストの全順序・冪等・要素保存 |
| SARIF出力 | 特定フィールドの値マッピング | 任意入力での件数保存・重複排除・決定論 |

---

## 判定

**プロパティーベースドテスト計画が完了した**

対象ロジック（`CyclomaticComplexityCalculator`・`LayerClassifier`・`DiagnosticSorter`・`SarifSerializer`）はいずれも数学的に明確な不変条件を持つドメインプリミティブ/変換ロジックであり、PBT との親和性が高い。既存の単体テストが代表例の正確性を確認するのに対し、PBT は**任意入力に対する普遍的不変条件**を補完する。実装は `FsCheck.Xunit` を使用し、`Roslyntic.Tests/Properties/` 配下に4ファイルで構成する。