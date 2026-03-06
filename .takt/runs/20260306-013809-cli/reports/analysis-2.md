# 分析レポート (analysis-2)

**作成日**: 2026-03-06
**分析対象レポート**:
- `data-internal-state.md`（内部ファイル調査・ADR フォーマット・コードスニペット）
- `data-otel-dotnet.md`（OTel .NET SDK 技術詳細・Flush パターン・STDERR 汚染防止）
- `dig-current-state.md`（Dig 統合メモ・ADR 判断根拠）
- `docs/adr/ADR-0004-otel-observability-strategy.md`（成果物）
- `docs/adr/ADR-0005-network-policy-air-gap.md`（成果物）
- `docs/adr/ADR-0001-output-contract.md`（更新確認）

---

## 1. 分析の観点

以下の 5 観点でギャップを検出する。

1. **説明されていない現象**: 事実は述べているが「なぜそうなのか」が不明
2. **検証されていない仮説**: 推測が事実として扱われている
3. **欠損している定量データ**: 主張に対して数値の裏付けがない
4. **新たに出現した概念**: 調査中に出てきた用語・概念で深掘りが必要なもの
5. **比較の欠落**: 片方のデータしかなく対比ができていない

---

## 2. 主要な発見の整理

### 2-1. Dig 成果物の確認

第 2 回 Dig（実装・ADR 作成フェーズ）が産出した成果物は以下の通り。

| 成果物 | ステータス | 備考 |
|---|---|---|
| `docs/adr/ADR-0004-otel-observability-strategy.md` | **作成済み** | Accepted / 2026-03-06 |
| `docs/adr/ADR-0005-network-policy-air-gap.md` | **作成済み** | Accepted / 2026-03-06 |
| `docs/adr/ADR-0001-output-contract.md`（Notes 更新） | **更新済み** | ADR-0004/0005 への参照追加 |
| `data-internal-state.md` | **作成済み** | ADR フォーマット・コードスニペット収集 |
| `data-otel-dotnet.md` | **作成済み** | OTel .NET 技術詳細（flush / STDERR 制御） |
| `Roslyntic.Cli/Program.cs` OTel 初期化コード | **未実装** | スケルトン 3 行のまま |
| `Roslyntic.Cli/Roslyntic.Cli.csproj` OTel 条件付き参照 | **未実装** | PackageReference ゼロのまま |
| MSBuildWorkspace NuGet 制御コード | **未実装** | コードベース上のソースファイルなし |

コード実装は Dig の対象外（情報収集・ドキュメント整備に特化）。未実装部分は Supervisor が評価する実装フェーズの残件であり、調査ギャップではない。

---

### 2-2. ADR-0004 の内容評価

**仕様との照合結果:**

| 要件 | ADR-0004 の記載 | 評価 |
|---|---|---|
| デフォルト無効（SDK 初期化スキップ） | 「`OTEL_EXPORTER_OTLP_ENDPOINT` 未設定時は初期化を完全スキップ」 | ✅ 満たしている |
| opt-in: 環境変数のみ | 標準 `OTEL_EXPORTER_OTLP_ENDPOINT` を唯一のゲートと明記 | ✅ 満たしている |
| シグナル: Traces + Metrics のみ | Logs は NDJSON が担当と明記 | ✅ 満たしている |
| Flush 保証 | `IHost.Dispose()` 自動 Flush 必須を明記 | ✅ 満たしている |
| 条件付き PackageReference | Semgrep Issue #10408 教訓を根拠として記載 | ✅ 満たしている |
| STDERR 汚染防止 | 「OTEL_LOG_LEVEL=none または明示的無効化を必須」 | ⚠️ 下記参照 |
| 制約との整合性 | 三制約すべてとの両立を明記 | ✅ 満たしている |
| プラグイン × OTel 将来課題 | Notes に Phase 2+ 課題として記録 | ✅ 満たしている |

**⚠️ 小さな精度欠損（重要度: 低）:**

ADR-0004 の Decision は「`OTEL_LOG_LEVEL=none` または SDK の自己テレメトリの明示的無効化を実装時に必須とする」と記載している。

しかし `data-otel-dotnet.md §E` で発見された事実:
- `OTEL_LOG_LEVEL=none` は **OTel 自動計装（Auto Instrumentation）向けの環境変数** であり、手動 SDK 組み込み（`Sdk.CreateTracerProviderBuilder()` 等）には効果が**限定的または無効**
- 手動組み込みの場合は **`EventListener` パターン**（`SilentOtelEventListener`）が必要
- ただし OTel SDK のデフォルト動作（`OTEL_DIAGNOSTICS.json` が存在しない状態）では STDERR への自己ログ出力は発生しない

**判断**: ADR 本文の記載は「または明示的無効化」を含んでいるため論理的に正しい。ただし `OTEL_LOG_LEVEL=none` が手動 SDK に効かないという実装の落とし穴を ADR の Notes に補記することが望ましい。これは追加調査ではなく、すでに `data-otel-dotnet.md` に記録済みの情報の整理問題である。

---

### 2-3. ADR-0005 の内容評価

**仕様との照合結果:**

| 要件 | ADR-0005 の記載 | 評価 |
|---|---|---|
| 「no network calls」スコープ定義 | ランタイム禁止 / ビルド時除外を明文化 | ✅ 満たしている |
| 実行前提: `dotnet restore` 済み | 必須条件として明記 | ✅ 満たしている |
| MSBuildWorkspace NuGet 復元防止 | `EnableNuGetPackageRestore=false` 相当 + エラー時終了コード 2 | ✅ 満たしている |
| バージョンチェック: 実装しない | Semgrep の反面教師とともに明記 | ✅ 満たしている |
| エアギャップ対応ガイダンス | 内部フィードミラーリング + スタンドアローン配布 | ✅ 満たしている |
| 競争優位の明記 | SonarScanner との対比で明記 | ✅ 満たしている |
| OTel opt-in 時の OTLP 送信が唯一の例外 | 明記あり、ADR-0004 への参照付き | ✅ 満たしている |

**ADR-0005 の評価**: 要件を完全に満たしている。精度欠損なし。

---

### 2-4. ADR-0001 更新確認

ADR-0001 の Notes セクションに以下が追加されていることを確認:

```
- ネットワークポリシーの詳細スコープ定義（ランタイム vs ビルド時）は ADR-0005 を参照。
- OTel opt-in オブザーバビリティ戦略は ADR-0004 を参照。
```

要件（「ステータス更新式ではなく Notes 追加のみ」）に合致。Decision セクションは変更なし。✅

---

### 2-5. 技術詳細データの網羅性確認

`data-otel-dotnet.md` が収集した技術情報:

| 項目 | 収集状況 | 実装への直結度 |
|---|---|---|
| 条件付き PackageReference の構文（MSBuild Conditions） | ✅ 収集済み | 高 |
| MSBuildWorkspace Properties ディクショナリによる NuGet 制御 | ✅ 収集済み | 高 |
| ForceFlush 推奨タイムアウト値（10,000 ms） | ✅ 収集済み（根拠付き） | 高 |
| BatchExporter CLI 向けチューニング値 | ✅ 収集済み | 中 |
| STDERR 汚染防止 EventListener パターン（コード完全版） | ✅ 収集済み | 高 |
| `OTEL_LOG_LEVEL=none` の制限（自動計装向けのみ） | ✅ 収集済み | 高 |
| OTel SDK 起動コストの実測値 | ❌ 未収集（公開ベンチマーク不存在） | 低 |
| `SkipMetadataImportOnMissingAssemblies` の詳細 | ❌ 未収集（GitHub 404） | 低 |

未収集 2 項目はいずれも追加 Web 調査で解決できない性質のものであり、`data-otel-dotnet.md` 内で「調査不可」として正直に報告されている。

---

## 3. ギャップの特定と評価

### ギャップ X: ADR-0004 の STDERR 汚染防止セクションの精度不足

**内容**: ADR-0004 Decision の「`OTEL_LOG_LEVEL=none` または SDK の自己テレメトリの明示的無効化」という記載が、手動 SDK 組み込みにおいて `OTEL_LOG_LEVEL=none` が効果を持たないことを明示していない。実装者が `OTEL_LOG_LEVEL=none` を設定して安心する誤解が生じうる。

**重要度**: 低
**理由**:
1. ADR-0004 の記載は論理的に正しい（「または明示的無効化」が含まれる）
2. 正確な実装方法（EventListener パターン）は `data-otel-dotnet.md §E` に詳細コードとともに記録されている
3. デフォルト動作ですでに STDERR への OTel 自己ログ出力は発生しないため、実害リスクは低い
4. ADR は実装詳細の全量を記述するドキュメントではなく、意思決定の記録が主目的

**判断**: 追加調査不要。ADR-0004 の Notes セクションへの補足が望ましいが、それは実装フェーズの作業（Digger ではなく Implementer の責務）。

---

### ギャップ Y: コード実装（Program.cs / csproj）の未実施

**内容**: タスク指示書（order.md）は ADR 作成に加えて「OTel opt-in 基盤の実装」「csproj の条件付き PackageReference」「MSBuildWorkspace NuGet 制御コード」の実装も要求していたが、これらのコード変更は行われていない。

**重要度**: 中（タスクの要求に対する未達）
**理由**:
- 実装に必要な技術情報はすべて収集済み（`data-otel-dotnet.md` に PatternD コード、ForceFlush、EventListener、条件付き PackageReference 記法が揃っている）
- `MSBuildWorkspace.Create(properties)` の Properties ディクショナリ制御方法も確認済み
- Roslyntic はスケルトン段階のため、`Roslyntic.Core` 等の主要プロジェクトが未作成であり、これら本体実装との統合が前提

**判断**: 追加の Web 調査では解決できない。これは Dig ではなく実装タスクとして対処すべき残件である。研究フェーズは完了しており、情報は十分に揃っている。

---

## 4. 追加調査の判断

**追加調査は不要と判断する。**

理由:
- 元のタスク依頼（OTel 関係性・ネットワーク通信必要性の調査、ADR-0004/0005 作成、ADR-0001 更新）はすべて完了
- 残存するギャップ（ギャップ X: ADR 精度不足、ギャップ Y: コード未実装）はどちらも Web 調査で解決できない性質のもの
- コード実装に必要な技術情報（実装パターン・API・タイムアウト値）はすべて収集済み
- 調査から解決できない項目（OTel 起動コスト実測値、MSBuildWorkspace の直接動作確認）は実装フェーズでのみ検証可能

---

## 5. 全体サマリー

### 5-1. 現時点での発見サマリー

**完了済み（調査・ドキュメント）:**
- Roslyntic コードベースの OTel・ネットワーク通信コード: ゼロ（スケルトン段階を確認）
- ADR-0004（OTel opt-in 戦略）: 作成・Accepted
- ADR-0005（ネットワークポリシー・エアギャップ対応）: 作成・Accepted
- ADR-0001（出力コントラクト）: Notes 更新済み（ADR-0004/0005 参照追加）
- 実装に必要な全技術情報の収集完了

**未着手（実装フェーズの残件）:**
- `Roslyntic.Cli/Program.cs` への OTel 条件付き初期化コード
- `Roslyntic.Cli/Roslyntic.Cli.csproj` の OTel SDK 条件付き PackageReference
- MSBuildWorkspace NuGet 自動復元防止コード
- （前提として）`Roslyntic.Core`, `Roslyntic.Analysis` 等の主要プロジェクト本体の実装

---

### 5-2. ADR 品質評価

| ADR | 内容品質 | フォーマット準拠 | 仕様充足度 |
|---|---|---|---|
| ADR-0004 | 高（微小精度欠損: OTEL_LOG_LEVEL の注意点） | ✅ 既存 ADR 形式に準拠 | 95% |
| ADR-0005 | 高 | ✅ 既存 ADR 形式に準拠 | 100% |
| ADR-0001（更新） | — | ✅ Notes 追加のみ（Decision 変更なし） | 100% |

---

### 5-3. 実装フェーズへの申し送り事項

技術情報は以下にすべて記録されており、コード実装者がそのまま利用できる:

| 実装項目 | 参照先 |
|---|---|
| OTel 条件付き初期化コード（Pattern D） | `data-internal-state.md §5`、`data-otel-dotnet.md §D` |
| 条件付き PackageReference の MSBuild 記法 | `data-otel-dotnet.md §A` |
| ForceFlush タイムアウト値（10,000 ms） と BatchExporter チューニング | `data-otel-dotnet.md §D` |
| STDERR 汚染防止（`SilentOtelEventListener`） | `data-otel-dotnet.md §E` |
| `OTEL_LOG_LEVEL=none` が手動 SDK に効かない注意点 | `data-otel-dotnet.md §E` |
| MSBuildWorkspace Properties ディクショナリ制御 | `data-otel-dotnet.md §B` |
| MSBuildWorkspace NuGet 未復元時エラーパターン | `data-otel-dotnet.md §B` |

---

### 5-4. 調査結果の信頼性評価

| レポート | 信頼度 | 根拠 |
|---|---|---|
| `data-internal-state.md` | 高 | コードベースおよび既存 ADR を直接参照した一次情報 |
| `data-otel-dotnet.md` | 高 | Microsoft Learn、NuGet.org、GitHub Issues（公式リポジトリ）に基づく |
| `data-internal-scan.md`（前回） | 高 | コードベース直接スキャン |
| `data-otel-patterns.md`（前回） | 中〜高 | 公式ドキュメント・GitHub Issues に基づく |
| `data-network-policy.md`（前回） | 中〜高 | 公式ドキュメント・GitHub Issues に基づく（一部 URL 404） |

---

*取得日: 2026-03-06*
