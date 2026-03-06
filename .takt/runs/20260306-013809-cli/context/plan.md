# 調査・実装計画書 — Roslyntic OTel・ネットワークポリシー実装

作成日: 2026-03-06（Iteration 2）
担当: Planner → Digger (Movement 2/4) へ引き継ぎ

---

## 背景と状況整理

### 前回調査（Iteration 1）の完了事項

前回の deep-research で以下が確定している（`.takt/runs/20260306-013809-cli/reports/` 参照）：

| 事項 | 結論 | 出典 |
|---|---|---|
| コードベース状態 | 完全スケルトン（Program.cs 3行、PackageReference 0件） | `data-internal-scan.md` |
| OTel採用パターン | `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数による完全opt-in が最適 | `data-otel-patterns.md` |
| ネットワークポリシー | 「no network calls」= ランタイム時アウトバウンド通信禁止。NuGet restore は「事前工程」 | `data-network-policy.md` |
| Semgrep教訓 | OTel SDKは条件付きPackageReferenceにすること（Issue #10408） | `research-report.md` §2 |
| ADR-0001ギャップ | ビルド時スコープが未明文化（中重要度）| `analysis-1.md` |

### 現在のタスク

上記調査結果を踏まえ、**実装（ADR文書作成 + コード実装の基盤構築）** を行う。
Diggerは「調査」と「実装準備」の両方を担う。

---

## 依頼分解

### What（何を達成するか）
1. **ADR-0004 作成**: OTel opt-in 観測性戦略の意思決定記録
2. **ADR-0005 作成**: ネットワークポリシー・エアギャップ対応の意思決定記録
3. **ADR-0001 更新**: ネットワークポリシー詳細を ADR-0005 に移譲する旨の記載追加
4. **OTel opt-in 基盤実装**: `OTEL_EXPORTER_OTLP_ENDPOINT` 環境変数によるプロビジョニング
5. **ネットワーク通信制御実装**: MSBuildWorkspace の NuGet restore 無効化

### Why（なぜ今か）
調査フェーズが完了し、設計判断に必要な情報が揃った。ADR なしに実装を始めると意思決定の根拠が失われる。スケルトン段階の今が ADR・基盤実装の最適タイミング。

### Scope（どこまでか）
- **In scope**: ADR-0004/0005 の作成、ADR-0001 の Notes 追記、OTel 初期化コードの骨格、MSBuildWorkspace NuGet 制御の方式決定
- **Out of scope**: 分析パイプライン実装（MSBuildWorkspace の本体統合）、Rules 実装、SARIF 出力実装

---

## 調査項目（実装の事前確認として必要）

### P1: 必須（実装前に確認が必要）

#### P1-1. 既存ファイルの全文確認

**目的**: ADR フォーマット統一、実装の接合点把握

| ファイル | 確認内容 |
|---|---|
| `docs/adr/_template.md` | セクション構造の正確な順序・フィールド名 |
| `docs/adr/ADR-0001-output-contract.md` | Notes セクションの現内容、Status 値 |
| `docs/adr/ADR-0002-sarif-strategy.md` | 参照関係の確認（OTel と干渉がないか） |
| `docs/adr/ADR-0003-plugin-safety-model.md` | プラグイン IPC + OTel コンテキスト伝播への言及有無 |
| `docs/architecture.md` | `Roslyntic.Core` / `Roslyntic.Cli` 等のプロジェクト一覧と責務 |
| `docs/observability.md` | NF5.2/NF5.3 の NDJSON キー名（ADR-0004 の整合性確認） |

**Digger への具体的指示**:
- 全ファイルを Read ツールで読み込む
- `_template.md` のセクション順を抽出してメモする（ADR 作成時の雛形とする）
- `ADR-0001` の Notes セクションに「ネットワークポリシー詳細は ADR-0005 を参照」と追記できるか確認
- `architecture.md` から OTel 初期化を配置すべきプロジェクト（`Roslyntic.Cli`）を特定する

#### P1-2. 既存レポートのキーデータ再確認

**目的**: Digger が前回調査の詳細に依存せず実装できるよう、必要データを抽出する

| レポート | 抽出するデータ |
|---|---|
| `data-otel-patterns.md` | §4 パターンD の C# コードスニペット全文、`.csproj` 条件付き PackageReference 記法の有無 |
| `data-network-policy.md` | MSBuildWorkspace NuGet 無効化の具体的方法（`EnableNuGetPackageRestore=false` の渡し方） |
| `data-internal-scan.md` | スキャン対象ファイル一覧（`**/*.csproj`, `**/*.cs`）の現状 |
| `analysis-1.md` | 残存ギャップのうち実装タスクに影響するもの |

**Digger への具体的指示**:
- `data-otel-patterns.md` を読み、`.csproj` の `<PackageReference Condition="...">` 記法が記載されているか確認する
  - **記載あり**: そのまま使用する
  - **記載なし**: 以下の Web 調査（P1-3）を実施する

#### P1-3. OTel SDK 条件付き PackageReference の記法確認（P1-2で不足の場合）

**目的**: ビルドフラグによる optional 依存を正確な記法で実装する

**仮定**: MSBuild の `Condition` 属性で `'$(EnableOtel)' == 'true'` のような形式が使える

**データソース候補（優先順）**:
1. `.takt/runs/20260306-013809-cli/reports/data-otel-patterns.md` を先に確認（上述）
2. `https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-conditions` - MSBuild Condition 構文
3. GitHub 検索: `"PackageReference Condition" "OpenTelemetry"` で実例を確認

**Digger への具体的指示**:
- 上記ソース 1 で記法が見つからない場合のみ、ソース 2 または 3 を参照する
- 取得した記法を `dig-implementation-spec.md` の「OTel SDK 条件付き参照」セクションに記録する

---

### P2: 重要（実装品質に影響）

#### P2-1. MSBuildWorkspace NuGet 制御の詳細確認

**目的**: `EnableNuGetPackageRestore=false` を渡す正確な API を特定する

**仮定**: `MSBuildWorkspace.Create(Dictionary<string, string>)` の `properties` 引数で渡す

**Digger への具体的指示**:
- `data-network-policy.md` を読み、MSBuildWorkspace の制御方法が記載されているか確認する
- 記載されている場合: そのまま `dig-implementation-spec.md` に転記する
- 記載がない場合: 以下を Web 検索する
  - 検索クエリ: `MSBuildWorkspace.Create properties EnableNuGetPackageRestore site:github.com OR site:learn.microsoft.com`
  - 期待する回答: `MSBuildWorkspace.Create(new Dictionary<string, string> { { "EnableNuGetPackageRestore", "false" } })` の動作確認

#### P2-2. ADR-0003 プラグインと OTel 伝播の影響確認

**目的**: ADR-0004 の「将来の留意事項」セクションに正確な記述をするため

**Digger への具体的指示**:
- `ADR-0003-plugin-safety-model.md` を読み、プラグインワーカープロセスへの言及を確認する
- OTel W3C TraceContext 伝播が IPC（Named Pipe / stdin/stdout）で問題になり得るか、ADR-0003 の設計から推測して記録する（推測は明示）

---

### P3: あれば良い

#### P3-1. OTel SDK 起動コストのデータ補完

**Digger への具体的指示**:
- `data-otel-patterns.md` に起動コストの数値（ms単位）があれば記録する
- なければ「調査不可（実装後実測を推奨）」と記録する。追加 Web 調査は不要

---

## 実装指示（Digger が直接実施する作業）

Digger は調査完了後、以下の成果物を直接作成・更新する。

### 成果物 1: ADR-0004（新規作成）

**ファイルパス**: `docs/adr/ADR-0004-otel-observability-strategy.md`

**内容の要件（`_template.md` のフォーマットに従うこと）**:

```
Status: Accepted
Date: 2026-03-06

Context:
- Roslyntic は CLI ツールであり「no telemetry by default」「STDOUT 汚染なし」「決定論的出力」の制約がある（ADR-0001）
- オブザーバビリティは既に NDJSON ログ（STDERR/--log-file）で実現（docs/observability.md NF5.2/NF5.3）
- 一部ユーザーが既存の OTel コレクター（Jaeger, Grafana等）にデータを統合したいニーズがある
- Semgrep Issue #10408: OTel SDK を必須依存にするとすべてのユーザーに影響が出る（教訓）

Decision:
- デフォルト: OTel SDK 初期化を完全スキップ（OTEL_EXPORTER_OTLP_ENDPOINT 未設定時）
- opt-in: OTEL_EXPORTER_OTLP_ENDPOINT 環境変数設定のみで有効化
- シグナル: Traces + Metrics を優先（Logs は NDJSON で既にカバー済み）
- Flush 保証: using var host = builder.Build() パターンで自動 Flush
- パッケージ依存: OTel SDK は条件付き PackageReference（Semgrep #10408 の教訓）
- STDERR 汚染防止: OTel 自己テレメトリを明示的に無効化

Consequences:
- Easier: 制約との完全整合（no telemetry by default, no STDOUT pollution）
- Easier: opt-in ユーザーは既存 OTel コレクターと統合可能
- Harder: OTel SDK の条件付きパッケージ管理がビルド複雑性を若干増す
- Risk: 短寿命プロセスでのバッチ export フラッシュ（using host パターンで対処）

Alternatives considered:
- OTel 必須依存（rejected: Semgrep #10408 教訓。opt-in 機能でも全ユーザーに影響）
- CLI フラグ --otel-export（rejected: 環境変数方式が OTel 標準に準拠し CI との親和性が高い）
- Logs の OTel 統合（rejected: NDJSON で既にカバー済み。二重管理を避ける）

Notes:
- 将来課題: ADR-0003 プラグインワーカープロセスへの W3C TraceContext 伝播は Phase 2 ADR で対処
- 実装参照: opentelemetry-dotnet #5102（Flush タイミング）、Semgrep #10408（条件付き参照）
```

### 成果物 2: ADR-0005（新規作成）

**ファイルパス**: `docs/adr/ADR-0005-network-policy-air-gap.md`

**内容の要件**:

```
Status: Accepted
Date: 2026-03-06

Context:
- ADR-0001 は「no network calls, no telemetry by default」を定めているが、スコープ（ビルド時 vs ランタイム時）が未明文化
- MSBuildWorkspace はソリューションロード時に暗黙的な NuGet restore を試みる可能性がある
- エアギャップ CI 環境（金融・政府系）での採用を競争優位とする（SonarScanner との対極的設計）
- Roslyn Analyzers / StyleCop は完全オフライン動作。SonarScanner はサーバー接続必須。

Decision:
- 「no network calls」= ランタイム時（roslyntic check 実行中）のアウトバウンド通信禁止
- ビルド時 NuGet restore（dotnet restore）は「事前工程」としてスコープ外
- 実行前提条件: dotnet restore 済みの環境での実行を前提とする
- MSBuildWorkspace NuGet 自動復元: EnableNuGetPackageRestore=false を設定して防止
- パッケージ未キャッシュ時: 明確なエラーメッセージを出力（exit code 2）
- バージョンチェック: 実装しない（tool.driver.version を SARIF に埋め込むのみ）
- OTel opt-in 通信: 唯一の例外（OTEL_EXPORTER_OTLP_ENDPOINT による明示的 opt-in）

Consequences:
- Easier: エアギャップ CI での完全オフライン動作（NuGet キャッシュ + スタンドアローン exe）
- Easier: 分析中のネットワーク通信ゼロ（タイムアウトによる CI 遅延なし）
- Harder: ユーザーは事前に dotnet restore を実行する必要がある
- Risk: 暗黙的 NuGet restore をブロックすると未 restore 環境でのエラーが増える（明確なメッセージで対処）

Alternatives considered:
- ランタイム中の NuGet restore を許容（rejected: エアギャップ環境での失敗・CI 遅延のリスク）
- バージョンチェック実装（rejected: ランタイム通信禁止ポリシーと矛盾）

Notes:
- ADR-0001 の「no network calls」の詳細スコープは本 ADR に分離
- エアギャップ対応ガイド: 事前 NuGetキャッシュ + dotnet publish -r <rid> --self-contained
```

### 成果物 3: ADR-0001 の Notes 更新

**対象ファイル**: `docs/adr/ADR-0001-output-contract.md`

**変更内容（追記のみ、既存内容は変更しない）**:

```
## Notes
- Observability details: docs/observability.md
- SARIF ordering requirements are referenced in ADR-0002.
- Network policy scope (runtime vs build-time): ADR-0005.    ← 追記
- OTel opt-in observability strategy: ADR-0004.              ← 追記
```

### 成果物 4: 調査メモ `dig-current-state.md`

**ファイルパス**: `.takt/runs/20260306-013809-cli/reports/dig-current-state.md`

**内容**:
- P1-1 で確認した各ファイルの要点（テンプレート構造、architecture.md のプロジェクト一覧）
- P1-2 で抽出した OTel 実装コードスニペット
- P1-3 / P2-1 の調査結果（`.csproj` 記法、MSBuildWorkspace API）
- コードスキャン最新状態の確認結果

---

## 実行順序（Digger への指示）

```
Step 1: ファイル読み込み（並列実行可能）
  - docs/adr/_template.md
  - docs/adr/ADR-0001, 0002, 0003
  - docs/architecture.md
  - docs/observability.md
  - .takt/.../reports/ の全レポートファイル

Step 2: レポートからキーデータ抽出
  - OTel 実装コードスニペット（data-otel-patterns.md §4）
  - MSBuildWorkspace NuGet 制御方法（data-network-policy.md）
  - 残存ギャップ一覧（analysis-1.md）

Step 3: 不足情報の Web 調査（P1-2 で不足の場合のみ）
  - .csproj 条件付き PackageReference 記法
  - MSBuildWorkspace.Create() の正確な API

Step 4: dig-current-state.md を作成（調査メモ）

Step 5: 成果物を作成・更新
  - docs/adr/ADR-0004-otel-observability-strategy.md（新規）
  - docs/adr/ADR-0005-network-policy-air-gap.md（新規）
  - docs/adr/ADR-0001-output-contract.md（Notes セクションに 2行追記）
```

---

## 品質基準

| 基準 | 判定 |
|---|---|
| ADR が `_template.md` のフォーマットに従っていない | REJECT |
| ADR の Decision に根拠（出典 or 前回レポートの参照）がない | REJECT |
| ADR-0001 の既存内容を削除・変更した | REJECT |
| 推測を事実として記述した | REJECT |
| 「no telemetry by default」制約に反する実装方針を記載した | REJECT |
| ADR が `_template.md` 形式で根拠付きで書かれている | OK |
| 前回レポートを出典として明示している | OK |
| 実装未確認項目を「実装フェーズで確認を推奨」と明示した | OK |

---

## 前提・仮定（Digger が引き継ぐべきもの）

1. **仮定A（前回調査確定事項）**: Roslynticはスケルトン段階（Program.cs 3行、PackageReference 0件）。再スキャンで最新状態を確認せよ。
2. **仮定B**: 新 ADR 番号は 0004, 0005。`docs/adr/` に ADR-0004.md, ADR-0005.md が未存在であることをスキャンで確認せよ。
3. **制約C**: `git commit`, `git add` は実行しないこと。
4. **制約D**: CLAUDE.md の非交渉制約（STDOUT = SARIF/JSON のみ、ローカル実行のみ、決定論的出力）はすべての ADR に貫徹すること。
5. **仮定E**: `.csproj` 条件付き参照は `<PackageReference Include="..." Condition="'$(EnableOtel)'=='true'">` のような形式が標準的と推測する。`data-otel-patterns.md` で確認せよ。
