# 調査計画: CLIにおけるオブザーバビリティとネットワーク関係性

作成日: 2026-03-06
担当: Plan Movement → Dig Movement へ引き継ぎ

---

## 前提・仮定

以下の仮定を置いて計画を策定する（仮定は明示）。

- **仮定A**: コードベースは初期段階（Program.cs に "Hello, World!" のみ、csproj に PackageReference なし）。内部コード調査は短時間で完了する。調査の主価値は外部事例・設計パターンの収集にある。
- **仮定B**: 「no network calls」の制約はランタイム実行時（`roslyntic check` コマンド実行中）の通信を指す。ビルド時の NuGet restore はスコープ外（ユーザー事前実施）とする。
- **仮定C**: OTel は将来の opt-in 機能として検討対象。現時点では採用されていない。
- **仮定D**: 調査対象ファイル群（docs/, adr/）は現在の設計意図を正確に反映している。

---

## 調査項目一覧

### P1（必須）: これがないと回答できない

#### A. コードベース内部スキャン

**A-1. OTel 関連依存の有無確認**
- 調査対象: `**/*.csproj` の PackageReference
- 調査方法: Grep で `OpenTelemetry` `System.Diagnostics.ActivitySource` を検索
- データソース: リポジトリ内ファイル
- 期待結果: 現時点では OTel 依存なし（仮定C）。発見した場合は詳細記録。

**A-2. ネットワーク通信コードの有無確認**
- 調査対象: `**/*.cs` 全ファイル
- 調査方法: Grep で以下を検索
  - `HttpClient`, `HttpClientFactory`
  - `WebClient`, `WebRequest`, `HttpWebRequest`
  - `TcpClient`, `UdpClient`, `Socket`
  - `RestClient`, `Flurl`
  - `NuGet` / `PackageReference` での通信誘発コード
- データソース: リポジトリ内ファイル
- 期待結果: 現時点では通信コードなし（スケルトン状態）。発見した場合は詳細記録。

**A-3. MSBuildWorkspace のロード時ネットワーク動作確認**
- 調査方法: Web検索で MSBuildWorkspace + NuGet restore 暗黙通信に関する公式ドキュメント / Issue を調査
- 検索クエリ例: `MSBuildWorkspace "implicit restore" network NuGet site:github.com OR site:learn.microsoft.com`
- データソース: Microsoft公式ドキュメント、GitHub Issues
- 期待結果: `--no-restore` オプションまたは事前 restore 前提での動作確認

---

#### B. CLIツールにおける OpenTelemetry 採用パターン

**B-1. dotnet CLI ツールの OTel 採用事例**
- 調査対象: dotnet-counters, dotnet-trace, dotnet-monitor
- 調査方法: Web検索 + GitHub リポジトリ確認
- 検索クエリ例:
  - `dotnet-monitor OpenTelemetry OTLP site:github.com/dotnet`
  - `"dotnet tool" "OpenTelemetry" CLI "short-lived"`
- 確認項目:
  - OTel SDK の採用有無
  - OTLP exporter の設定方法（環境変数 vs CLI フラグ）
  - ログ/トレース/メトリクスの使い分け
- データソース: GitHub リポジトリ（dotnet/dotnet-monitor 等）、Microsoft Blog

**B-2. 非 dotnet CLI ツールの OTel 採用事例（参考）**
- 調査対象: Trivy（Go）, Semgrep（Python/OCaml）
- 調査方法: Web検索
- 検索クエリ例:
  - `Trivy OpenTelemetry tracing site:github.com/aquasecurity`
  - `Semgrep telemetry opt-in architecture`
- 確認項目: opt-in テレメトリの実装パターン、ユーザー向けフラグ設計

**B-3. 短寿命プロセス特有の OTel 課題**
- 調査方法: Web検索 + OTel 公式ドキュメント
- 検索クエリ例:
  - `OpenTelemetry "short-lived" process flush CLI best practices`
  - `OpenTelemetry .NET "ForceFlush" "BatchExportProcessor" CLI`
  - `OpenTelemetry OTLP exporter cold start latency .NET`
- 確認項目:
  - バッチ export のフラッシュタイミング問題（プロセス終了前の flush）
  - コールドスタートコスト（SDK 初期化時間）
  - SimpleExportProcessor vs BatchExportProcessor の CLI 適合性
- データソース: OpenTelemetry 公式ドキュメント (opentelemetry.io)、GitHub Issues

**B-4. OTel .NET SDK のパッケージサイズ・起動コスト**
- 調査方法: NuGet.org の依存関係確認 + ベンチマーク記事検索
- 検索クエリ例:
  - `OpenTelemetry .NET SDK package size NuGet dependencies`
  - `"OpenTelemetry.Exporter.OpenTelemetryProtocol" dependencies size`
- 確認項目:
  - 主要パッケージ（`OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`）の依存ツリーと合計サイズ
  - 起動時間への影響（定量値があれば）
- データソース: NuGet.org、Microsoft パフォーマンスブログ

---

#### C. 静的解析 CLI ツールのネットワークポリシー比較

**C-1. Roslyn Analyzers / StyleCop のネットワークポリシー**
- 調査方法: GitHub + 公式ドキュメント検索
- 検索クエリ例:
  - `StyleCopAnalyzers network offline air-gap`
  - `Roslyn Analyzers telemetry network policy`
- 確認項目: ランタイム時のネットワーク通信有無、テレメトリの有無

**C-2. SonarScanner CLI のネットワークポリシー**
- 調査方法: Web検索
- 検索クエリ例:
  - `SonarScanner CLI network calls "SonarQube server" offline`
  - `SonarScanner "air-gap" OR "offline" mode`
- 確認項目: ランタイム通信の必要性（サーバーとの通信が必須か否か）

**C-3. Semgrep のネットワークポリシー**
- 調査方法: Web検索 + 公式ドキュメント
- 検索クエリ例:
  - `Semgrep telemetry opt-out offline mode`
  - `semgrep "no telemetry" OR "disable telemetry" CLI flag`
- 確認項目: デフォルトテレメトリの有無、opt-out フラグ設計

**C-4. エアギャップ・オフライン環境対応の一般プラクティス**
- 調査方法: Web検索
- 検索クエリ例:
  - `static analysis CLI "air-gap" "offline" best practices`
  - `CLI tool offline first design "no network" telemetry opt-in pattern`
- 確認項目: オフライン対応 CLI の設計パターン（フラグ、環境変数）

---

### P2（重要）: 回答の質が上がる

#### D. 現行オブザーバビリティと OTel の比較

**D-1. NDJSON ログ（現行方式）と OTel の機能比較**
- 調査方法: 公式ドキュメント参照 + 既存コードベース（docs/observability.md）分析
- 確認項目:
  - NF5.2/NF5.3 で定義されたシグナル（タイミング、カウント、エラー分類）が OTel のどのシグナル種別（Logs/Traces/Metrics）にマッピングされるか
  - OTel の Logs API（`ILogger` + OTel exporter）での実現可能性
  - 「STDOUT を汚さない」制約との両立（OTel の export 先は STDERR/ファイル/OTLP に限定可能か）

**D-2. opt-in OTel 統合の実装パターン**
- 調査方法: Web検索 + GitHub事例
- 検索クエリ例:
  - `.NET CLI "opt-in" OpenTelemetry "--otel-endpoint" OR "OTEL_EXPORTER_OTLP_ENDPOINT"`
  - `OpenTelemetry .NET "ConditionalExporter" OR "no-op" default`
- 確認項目:
  - 環境変数 `OTEL_EXPORTER_OTLP_ENDPOINT` でのアクティベーションパターン
  - デフォルト NoOp exporter + 条件付き OTLP exporter の実装パターン

---

### P3（あれば良い）: 時間があれば

#### E. 改善提案の事前調査

**E-1. `System.Diagnostics.ActivitySource` (built-in) vs OTel SDK の比較**
- 調査方法: Microsoft 公式ドキュメント
- 検索クエリ例:
  - `"ActivitySource" vs "OpenTelemetry SDK" .NET CLI tradeoffs`
- 確認項目: SDK なしで OTel 互換トレースを出せる built-in API の能力・限界

**E-2. Microsoft.Extensions.Logging + OTel Bridge の活用可能性**
- 調査方法: 公式ドキュメント
- 検索クエリ例:
  - `"OpenTelemetry.Extensions.Logging" ILogger bridge .NET CLI`
- 確認項目: `ILogger` を OTel にブリッジする際の設定量・副作用

---

## 調査の実行順序

```
1. A-1, A-2 （コードスキャン：Grep で即完了）
2. A-3, B-3, B-4 （技術的制約の確認：重要判断材料）
3. B-1, B-2 （事例調査：設計参考）
4. C-1〜C-4 （競合比較：ポジショニング）
5. D-1, D-2 （統合比較：改善提案の材料）
6. E-1, E-2 （時間があれば）
```

---

## 調査結果のアウトプット形式

Digger は以下の構造でレポートを作成すること。

```markdown
# 調査結果レポート: CLIにおけるオブザーバビリティとネットワーク関係性

## セクション1: コードベース内部スキャン結果
- A-1: OTel依存の有無（発見/未発見 + 根拠）
- A-2: ネットワーク通信コードの有無（発見/未発見 + 根拠）
- A-3: MSBuildWorkspace のネットワーク動作（公式情報 + 出典URL）

## セクション2: CLIツールにおける OTel 採用パターン
- B-1: dotnet ツール事例（ツール名 + 実装パターン + 出典URL）
- B-2: 非dotnet ツール事例（ツール名 + opt-in設計 + 出典URL）
- B-3: 短寿命プロセス特有の課題（課題名 + 解決策 + 出典URL）
- B-4: パッケージサイズ・起動コスト（数値 + 出典URL）

## セクション3: 静的解析CLIのネットワークポリシー比較

| ツール | デフォルト通信 | opt-out手段 | エアギャップ対応 |
|--------|--------------|------------|----------------|
| ...    | ...          | ...        | ...            |

出典URL を各行に付記。

## セクション4: 現行方式と OTel の比較分析
- D-1: NF5.2/NF5.3 シグナルの OTel マッピング表
- D-2: opt-in 実装パターンの具体例（コード片または設定例）

## セクション5: 調査不可・未発見項目
- 調査したが見つからなかった情報（探した場所を明記）
- ネットワーク制限等で調査不可だった項目
```

---

## 品質基準（Digger への注意事項）

- **数値には必ず出典（URL + 調査日）を付けること**
- **推測は「推測:」と明記して、根拠を添えること**
- **調査できなかった項目は「調査不可: 理由」と正直に記録すること**
- **片方のデータのみで対比を主張しないこと**
- 80% 基準: 完璧を求めない。主要事例が 2〜3 件揃えば十分。

---

## 参照すべきコードベース内ファイル

| ファイル | 目的 |
|---------|------|
| `Roslyntic.Cli/Roslyntic.Cli.csproj` | 依存関係の現状確認 |
| `Roslyntic.Cli/Program.cs` | 実装状況の確認 |
| `docs/observability.md` | NF5.1〜5.3 の定義（OTel マッピング比較の基準） |
| `docs/architecture.md` | コンポーネント構成とロガー抽象化の確認 |
| `docs/adr/ADR-0001-output-contract.md` | 「no network / no telemetry」制約の一次情報 |
| `docs/adr/ADR-0002-sarif-strategy.md` | SARIF 戦略の確認（OTel と競合しないか） |
| `docs/adr/ADR-0003-plugin-safety-model.md` | プラグイン安全モデル（ネットワーク通信との関係） |

---

## コンテキスト補足（現時点のコードベース状態）

> Digger へ: コードベースは **スケルトン段階** である。
> - `Roslyntic.Cli/Program.cs`: `Console.WriteLine("Hello, World!");` のみ
> - `Roslyntic.Cli/Roslyntic.Cli.csproj`: PackageReference なし（OTel依存なし確定）
> - `**/*.cs` ファイル: 1ファイルのみ（通信コードなし確定）
>
> したがって A-1, A-2 の内部スキャンは「未採用・未実装」の確認として完了させ、
> 主力は B〜D の外部事例・設計パターン調査に注力すること。
