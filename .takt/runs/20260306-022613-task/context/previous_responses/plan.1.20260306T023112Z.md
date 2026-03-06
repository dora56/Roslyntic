## 実装計画サマリー

### タスク概要

`order.md` は "プロジェクトの" のみで内容が欠落していますが、プロジェクトドキュメント（`docs/agent-playbook.md`, `docs/architecture.md`, `docs/rules.md`）から**Roslyntic MVP の全面実装**タスクであることが確認できました。

### 現状と目標

**現状**: `Roslyntic.Cli/Program.cs` に `Console.WriteLine("Hello, World!")` のみ、ソリューションも Cli プロジェクトのみ  
**目標**: `roslyntic check <path>` を動作させ、SARIF 2.1.0 を STDOUT に出力する

---

### 設計方針

#### プロジェクト構成（新規作成が必要）

| プロジェクト | 役割 |
|------------|------|
| `Roslyntic.Core` | 共有モデル・IRule・ワークスペースロード・パイプライン |
| `Roslyntic.Analysis` | Roslyn ベースのユーティリティ（複雑度計算・依存抽出） |
| `Roslyntic.Rules` | AGARCH0001・AGCOMP0001 の実装 |
| `Roslyntic.Sarif` | SARIF 2.1.0 / JSON シリアライズ |
| `Roslyntic.Tests` | ユニット＋統合テスト |
| `samples/` | 4プロジェクトのサンプルソリューション |

#### 依存グラフ（循環なし）
```
Cli → Core, Rules, Sarif
Rules → Core, Analysis
Sarif → Core（モデルのみ）
Analysis → Roslyn パッケージのみ
```

---

### 実装の重要制約（Coder への申し送り）

1. **STDOUT 汚染禁止**: すべての進捗・エラーは `Console.Error` またはロガー経由で STDERR へ
2. **決定論的ソート必須**: `StringComparer.Ordinal` を使用（パス→行→列→ruleId）
3. **MSBuildLocator 初期化順**: `MSBuildLocator.RegisterDefaults()` を MSBuildWorkspace 作成より先に（一度だけ）呼ぶ
4. **SARIF にタイムスタンプ/GUID 禁止**: 決定論的出力のため省略
5. **ファイルサイズ 300 行以内**: 各クラスは単一責務で収める
6. **TODO コメント禁止**: 実装するか削除