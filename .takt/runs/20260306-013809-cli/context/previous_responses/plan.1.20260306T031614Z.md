調査計画書の作成が完了しました。以下に計画の概要を示します。

---

## 調査計画サマリー

### 依頼の分解

| 観点 | 内容 |
|---|---|
| **What** | ADR-0004（OTel戦略）・ADR-0005（ネットワークポリシー）の作成 + ADR-0001更新 + OTel/ネットワーク制御実装基盤の構築 |
| **Why** | 前回research-report.mdで方向性確定済み。スケルトン段階の今が設計記録・基盤実装の最適タイミング |
| **Scope** | ADR文書作成・Notes追記・OTel初期化骨格・NuGet制御方式決定。分析パイプライン本体はOut of scope |

### 調査項目の優先順位

| 優先度 | 項目 | 理由 |
|---|---|---|
| **P1-1** | 既存 ADR・docs 全文確認 | フォーマット統一・実装接合点の把握が必須 |
| **P1-2** | 前回レポートのキーデータ抽出 | OTel実装コード・MSBuildWorkspace制御方法が必要 |
| **P1-3** | `.csproj` 条件付きPackageReference記法（P1-2で不足時のみ） | 仮定: data-otel-patterns.mdに記載あり |
| **P2-1** | MSBuildWorkspace NuGet制御の詳細確認 | EnableNuGetPackageRestore=falseの正確なAPI |
| **P2-2** | ADR-0003プラグインとOTelコンテキスト伝播の影響 | ADR-0004「将来の留意事項」セクション用 |
| **P3-1** | OTel SDK起動コストのデータ補完 | レポートになければ「調査不可」で即終了 |

### Digger が作成する成果物

1. **`docs/adr/ADR-0004-otel-observability-strategy.md`** — OTel opt-in戦略のADR（新規）
2. **`docs/adr/ADR-0005-network-policy-air-gap.md`** — ネットワークポリシーのADR（新規）
3. **`docs/adr/ADR-0001-output-contract.md`** — Notes セクションに2行追記のみ
4. **`.takt/runs/20260306-013809-cli/reports/dig-current-state.md`** — 調査メモ

### 実行順序
```
Step 1: 全ファイル並列読み込み（docs/adr/, docs/, reports/）
Step 2: キーデータ抽出（OTel実装コード、MSBuildWorkspace制御方法）
Step 3: 不足情報のWeb調査（P1-2で不足の場合のみ）
Step 4: dig-current-state.md 作成
Step 5: ADR-0004, ADR-0005 新規作成 + ADR-0001 Notes追記
```