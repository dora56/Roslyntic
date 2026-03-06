# ADR-0005: Network Policy and Air-Gap Execution
Status: Accepted
Date: 2026-03-06

## Context
CLAUDE.md は「no network calls」を非交渉的制約として規定しているが、そのスコープが曖昧である。具体的には以下の二点が未明文化のままだった:

1. **ビルド時 NuGet restore とランタイム通信の区別**: `MSBuildWorkspace.OpenSolutionAsync()` は内部でデザインタイムビルドを実行し、NuGet パッケージ参照の解決を試みる場合がある。このフェーズのネットワーク通信が「no network calls」制約の対象かどうかが不明確だった（内部スキャン `data-internal-scan.md` §3 で確認）。
2. **エアギャップ CI 環境での利用要件**: 金融・政府系 CI 環境ではランタイム中の外部通信が絶対禁止となる。接続試行によるタイムアウトが CI/CD を著しく遅延させるため、通信が発生しないことの保証が必要。

また、類似ツールとの比較調査（`data-network-policy.md`）により、SonarScanner がサーバー接続必須設計であるのに対し、ローカル完結 SARIF 出力がエアギャップ環境における明確な差別化要素となることが確認された。

## Decision
- **「no network calls」の定義**: ランタイム（分析実行中）のアウトバウンド通信禁止を意味する。ビルド時 NuGet restore は「分析実行前の事前工程」として本制約の対象外とする。
- **実行前提**: `dotnet restore` 済みの環境での実行を前提とする。Roslyntic は「`dotnet restore` 済みのプロジェクト/ソリューションに対して実行する」ことを必須条件とし、ドキュメント（README および `--help`）にこれを明記する。
- **MSBuildWorkspace の暗黙的 NuGet restore 防止**: `MSBuildWorkspace` がデザインタイムビルド中に NuGet 復元を暗黙的にトリガーしないよう、`EnableNuGetPackageRestore=false` 相当の設定を適用する。パッケージがキャッシュに存在しない場合（未 restore 状態）は、SARIF 出力を行わず「`dotnet restore` を先に実行してください」旨のエラーメッセージを STDERR に出力して終了コード 2 で終了する。
- **バージョンチェック機能は実装しない**: バージョン情報は SARIF の `tool.driver.version` に埋め込むのみとし、外部サーバーへのバージョン問い合わせは一切行わない。Semgrep の `--disable-version-check` フラグが必要になった背景（デフォルトで `semgrep.dev` へ通信）を反面教師とする。
- **opt-in 時の OTLP export は唯一のランタイム通信例外**: `OTEL_EXPORTER_OTLP_ENDPOINT` が設定されている場合の OTLP 送信は、ユーザーが明示的に opt-in した結果であるため、ランタイム通信禁止の例外として許容する。詳細は ADR-0004 を参照。
- **エアギャップ CI 対応の推奨ガイダンス**: 以下を推奨ガイダンスとしてドキュメントに記載する:
  - 事前 NuGet キャッシュ（内部フィード Artifactory / Azure Artifacts 等へのミラーリング）
  - スタンドアローン実行ファイル配布（`dotnet global tool` より standalone ZIP 配布を推奨）

## Consequences
- Easier: ランタイム通信のスコープが明確になり、エアギャップ CI 環境での動作保証が可能になる。
- Easier: `dotnet restore` 済みを前提とすることで、ランタイム中の偶発的なネットワーク通信リスクが排除される。
- Easier: バージョンチェック未実装により、完全オフライン実行の純粋性が保たれる。
- Harder: 実行前に `dotnet restore` が完了していることを前提とするため、使用側の CI スクリプトにステップ追加が必要になる場合がある。
- Harder: `MSBuildWorkspace` の NuGet 自動復元を明示的に無効化する設定コードが必要。
- Risk: `EnableNuGetPackageRestore=false` 相当の設定が将来の MSBuild バージョンで動作が変わる可能性がある。実装フェーズでの動作確認が必要。

## Alternatives considered
- **ランタイム中の条件付き通信許可**（却下: キャッシュヒット率・ネットワーク状況によって実行結果が変わり、決定論的動作が損なわれる。エアギャップ CI での接続待ちタイムアウトが CI/CD を遅延させる）。
- **完全オフラインビルド強制**（ビルド時 NuGet restore も禁止）（却下: 開発体験を過度に制限する。dotnet エコシステムの標準ワークフロー（`dotnet restore` → `dotnet build` → ツール実行）を尊重するのが業界標準）。

## Notes
- SonarScanner for .NET はサーバー接続必須設計（begin/end フェーズでサーバー通信が必須）であり、本ツールのローカル完結 SARIF 出力はエアギャップ環境（金融・政府系）での明確な差別化要素となる。
- OTel opt-in オブザーバビリティ戦略（`OTEL_EXPORTER_OTLP_ENDPOINT` によるランタイム通信例外の詳細）は ADR-0004 を参照。
