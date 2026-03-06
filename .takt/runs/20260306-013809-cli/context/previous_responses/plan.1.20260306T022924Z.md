調査計画を作成しました。以下が計画の概要です。

---

## 調査計画サマリー

### 依頼の分解

| 観点 | 内容 |
|------|------|
| **What** | ①OTelとの関係性（現状・適用可能性）②ネットワーク通信の必要性・スコープ |
| **Why** | NDJSON→OTel移行判断材料の確保、「no network calls」制約の具体的解釈の確立 |
| **Scope** | コード内部スキャン＋外部事例調査＋改善提案の設計パターン収集 |

### 重要な前提（仮定）

コードベースはスケルトン段階（`Program.cs` に "Hello, World!" のみ、PackageReference なし）。これにより：
- **OTel依存：現時点でなし**（csproj確認済み）
- **ネットワーク通信コード：現時点でなし**（cs ファイル1つのみ）

内部スキャンは即完了させ、外部事例・設計パターン調査に主力を注ぐ方針。

### 調査項目と優先度

| 優先度 | 項目 | 内容 |
|--------|------|------|
| **P1: 必須** | A. コードスキャン | OTel依存・通信コードの有無、MSBuildWorkspaceのネットワーク動作 |
| **P1: 必須** | B. OTel採用パターン | dotnet CLI事例、短寿命プロセス課題、パッケージサイズ |
| **P1: 必須** | C. ネットワークポリシー比較 | Roslyn/StyleCop/SonarScanner/Semgrep のポリシー比較 |
| **P2: 重要** | D. 現行方式との比較 | NF5.2/5.3→OTelマッピング、opt-in実装パターン |
| **P3: あれば** | E. 改善提案事前調査 | ActivitySource vs OTel SDK、ILogger Bridge |

### Diggerへの主要調査指示

1. **コードスキャン（Grep）**: `HttpClient` `OpenTelemetry` 等のキーワードで全.csファイルを走査
2. **Web検索（B-1）**: `dotnet-monitor OpenTelemetry OTLP site:github.com/dotnet`
3. **Web検索（B-3）**: `OpenTelemetry "short-lived" process flush CLI best practices`
4. **Web検索（B-4）**: NuGet.org でパッケージサイズ・依存ツリー確認
5. **Web検索（C-1〜4）**: 各ツールのネットワークポリシーをドキュメント・GitHub から確認

計画書は `.takt/runs/20260306-013809-cli/context/plan.md` に保存済みです。