# 類似静的解析CLIツールのネットワークポリシー比較

**調査日**: 2026-03-06
**調査者**: TAKT Digger (dig.network-policy)

---

## 1. ツール別ネットワークポリシー比較表

| ツール | ポリシー種別 | オフライン対応 | 主なネットワーク用途 | 無効化手段 | 出典URL |
|---|---|---|---|---|---|
| **Roslyn Analyzers** (DiagnosticAnalyzer) | ネットワーク通信なし（設計上） | ◎ 完全オフライン | なし（コンパイラ API 内で完結） | N/A | [DiagnosticAnalyzer.cs](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/DiagnosticAnalyzer.cs) |
| **StyleCop.Analyzers** | ネットワーク通信なし | ◎ 完全オフライン | なし（Roslyn 上の純粋アナライザー） | N/A | [DotNetAnalyzers/StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) |
| **SonarScanner for .NET** | サーバー接続必須 | ✗ オフライン不可 | 結果アップロード（begin/end フェーズ）、設定取得、認証 | なし（サーバー URL・トークン必須） | [Sonar Docs](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/scanners/dotnet/using) |
| **Semgrep** | 部分的オフライン対応 | △ フラグ組み合わせで可 | バージョンチェック、メトリクス送信、ルール Registry 取得 | `--disable-version-check --metrics=off --oss-only` | [Semgrep CLI Reference](https://semgrep.dev/docs/cli-reference) |
| **MSBuildWorkspace** | 条件付きネットワーク | △ キャッシュ済みなら可 | NuGet パッケージ復元（未キャッシュ時） | `EnableNuGetPackageRestore=false` または事前 restore | [NuGet Package Restore](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore) |

### 凡例
- ◎ 完全オフライン：ネットワーク不要
- △ 条件付き：設定・フラグ次第でオフライン化可
- ✗ オフライン不可：常時ネットワーク必須

---

## 2. 各ツールの詳細

### A. Roslyn Analyzers (Microsoft.CodeAnalysis)

**ネットワーク通信**: **なし**

Roslyn の `DiagnosticAnalyzer` は Roslyn コンパイラ API のシンボルツリー・セマンティックモデルのみを参照する。ランタイム中に外部ネットワークへアクセスする機構を持たない。

- .NET の Code Access Security (CAS) はすでに廃止されており、技術的にはアナライザーがネットワーク呼び出しを行うことも可能だが、**Roslyn のサンドボックスポリシーはネットワーク禁止を強制するものではない**（信頼境界はプロセス分離で実現）。
- Roslyn チームは「信頼できないコードを実行するにはプロセス分離を使え」という立場（issue #29525 の Won't Fix 回答）。
- つまり、**公式の Roslyn アナライザーは設計上ネットワーク不要**だが、カスタムアナライザーが悪意を持てば通信を行える点に留意が必要。

**出典**:
- [Roslyn GitHub issue #29525 - Roslyn demands unrestricted permissions](https://github.com/dotnet/roslyn/issues/29525)（取得日: 2026-03-06）
- [Roslyn GitHub issue #10830 - Securely Sandboxing Roslyn Code Execution](https://github.com/dotnet/roslyn/issues/10830)（取得日: 2026-03-06）

---

### B. StyleCop.Analyzers

**ネットワーク通信**: **なし**

StyleCop.Analyzers は Roslyn アナライザーとして実装されており、外部ネットワーク依存はゼロ。パッケージ自体に外部依存関係もない（`no dependencies` と NuGet に明記）。設定は `stylecop.json` ファイルで完結する。

**出典**:
- [StyleCopAnalyzers GitHub](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)（取得日: 2026-03-06）
- [NuGet Gallery - StyleCop.Analyzers](https://www.nuget.org/packages/StyleCop.Analyzers/)（取得日: 2026-03-06）

---

### C. SonarScanner for .NET

**ネットワーク通信**: **常時必須（サーバー接続が前提）**

SonarScanner for .NET の動作フロー:
1. **begin フェーズ**: SonarQube Server/Cloud から品質プロファイル・設定を取得（ネットワーク必須）
2. **build フェーズ**: コード分析（ローカル処理）
3. **end フェーズ**: 分析結果・カバレッジデータを SonarQube Server へアップロード（ネットワーク必須）

**オフライン対応状況**:
- 通常の `dotnet tool install` によるインストール時はネットワーク必須だが、**スタンドアローン実行ファイルのダウンロード**でインストールは回避可能
- ただし **分析実行そのものはサーバーなしでは機能しない**（`sonar.host.url` + `sonar.token` が必須）
- Azure DevOps 拡張機能は v6.2.0（2024-07-01）からデフォルトスキャナーを埋め込みオフラインインストール対応
- Sonar Community フォーラムでのオフラインインストール相談への回答: 「スタンドアローン実行ファイルを直接ダウンロードして使え」

**プロキシ設定**: `SONAR_SCANNER_OPTS` + `HTTP_PROXY`/`HTTPS_PROXY` 環境変数で設定可能（.NET Framework バリアントでは不可）

**出典**:
- [SonarScanner for .NET Configuring - Sonar Docs](https://docs.sonarsource.com/sonarqube-server/latest/analyzing-source-code/scanners/dotnet/configuring/)（取得日: 2026-03-06）
- [Offline .NET Sonar scanner installation - Sonar Community](https://community.sonarsource.com/t/offline-net-sonar-scanner-installation/126631)（取得日: 2026-03-06）

---

### D. Semgrep

**ネットワーク通信**: **デフォルトで複数の外部通信が存在（無効化可能）**

Semgrep がデフォルトで行うネットワーク通信:
1. **バージョンチェック**: `semgrep.dev` へのバージョン確認（ツール起動時）
2. **メトリクス送信**: 使用状況統計の送信
3. **ルール Registry 取得**: `--config auto` 使用時に semgrep.dev からルールを取得
4. **バリデーション時**: `--validate` オプション使用時に `semgrep.dev/c/p/...` へアクセス（2024年5月時点で未解決）

**オフライン化フラグ**（組み合わせが必要）:

| フラグ / 環境変数 | 効果 |
|---|---|
| `--disable-version-check` | バージョンチェックのネットワーク呼び出しを無効化 |
| `--metrics=off` または `SEMGREP_SEND_METRICS=off` | メトリクス送信を無効化 |
| `--oss-only` | Pro サービスへの接続を無効化 |
| ローカルルールファイル使用 | Registry からのルール取得を回避 |

**専用の `--offline` フラグは存在しない**（GitHub issue #8793 はクローズ済みだが、`--validate` 時の通信は別途要対処）。

**出典**:
- [Semgrep CLI Reference](https://semgrep.dev/docs/cli-reference)（取得日: 2026-03-06）
- [Offline execution issue #8793 - semgrep/semgrep](https://github.com/semgrep/semgrep/issues/8793)（取得日: 2026-03-06）

---

### E. MSBuildWorkspace の暗黙的ネットワーク通信

**ネットワーク通信**: **未キャッシュパッケージが存在する場合に NuGet 復元でネットワーク通信が発生**

`MSBuildWorkspace.OpenSolutionAsync()` は内部でデザインタイムビルドを実行する。このビルドが NuGet パッケージ復元を暗黙的にトリガーする場合がある。

**NuGet 復元のネットワーク動作**:
1. まずローカルキャッシュ（`global-packages` フォルダ / HTTP キャッシュ）を確認
2. キャッシュにない場合のみ設定済みパッケージソース（nuget.org 等）へアクセス
3. **すべてのパッケージがキャッシュ済みであればネットワーク通信は発生しない**

**ネットワーク通信を回避する手段**:

| 手段 | 説明 |
|---|---|
| 事前に `dotnet restore` 実行 | パッケージをローカルキャッシュに保存しておく |
| `EnableNuGetPackageRestore=false` 環境変数 | NuGet 自動復元を無効化 |
| `RestorePackagesPath` を指定 | ローカルディレクトリのパッケージのみ使用 |
| `--no-restore` フラグ（build/run 時） | 暗黙的復元をスキップ |
| `NuGet.Config` の `packageRestore/enabled = False` | 復元全体を無効化 |

**重要**: MSBuildWorkspace 自体（Roslyn ワークスペース API）はネットワーク通信を行わない。ネットワーク通信は NuGet 復元フェーズ（**ビルドタイム**）にのみ発生し得る。

**出典**:
- [NuGet Package Restore - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore)（取得日: 2026-03-06）
- [Using MSBuildWorkspace - Gist by DustinCampbell](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)（取得日: 2026-03-06）

---

## 3. エアギャップ / オフライン環境対応プラクティス

### アップデートチェックの opt-in 化事例

| ツール | 手段 |
|---|---|
| Semgrep | `--disable-version-check` フラグ |
| .NET SDK | `DOTNET_CLI_TELEMETRY_OPTOUT=1` 環境変数 |
| ML.NET CLI | `MLDOTNET_CLI_TELEMETRY_OPTOUT=1` 環境変数 |
| Upgrade Assistant | `DOTNET_UPGRADEASSISTANT_TELEMETRY_OPTOUT=1` 環境変数 |

**業界標準パターン**: 環境変数による opt-out が .NET エコシステムの標準。専用 CLI フラグも補完手段として提供するツールが多い。ただし telemetry の opt-out 標準仕様（統一規格）は現時点で広く採用されていない（参考: [CLI telemetry best practices by marcon.me](https://marcon.me/articles/cli-telemetry-best-practices/)）。

### ライセンス認証のオフライン対応パターン

- **SonarScanner**: トークンベースの認証はサーバーへの接続前提。オフライン認証の仕組みはない。
- **Semgrep**: OSS 版（`--oss-only`）はライセンス認証不要。Pro 版のみサーバー認証必要。
- **一般的パターン**: エアギャップ対応が必要なツールは、オフラインライセンスファイル（XML/JWT 形式）や事前取得したシグネチャを用いる（例: JetBrains Toolbox のオフラインアクティベーション）。

### エアギャップ対応の CI/CD ベストプラクティス

1. **パッケージキャッシュの事前配布**: NuGet パッケージを内部フィード（Artifactory / Azure Artifacts 等）にミラーリング
2. **スタンドアローン実行ファイルの配布**: dotnet global tool より standalone ZIP 配布が確実
3. **ネットワーク呼び出しの自動テスト**: CI でネットワーク通信の有無を regression テストとして検証
4. **テレメトリの非同期・非ブロッキング設計**: 失敗してもツールの終了をブロックしない（best-effort）
5. **プロキシ設定のサポート**: `HTTP_PROXY`/`HTTPS_PROXY` 環境変数への対応

**出典**:
- [6 telemetry best practices for CLI tools - marcon.me](https://marcon.me/articles/cli-telemetry-best-practices/)（取得日: 2026-03-06）
- [.NET SDK telemetry - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/telemetry)（取得日: 2026-03-06）
- [Enterprise AI Code Assistants for Air-Gapped Environments - IntuitionLabs](https://intuitionlabs.ai/articles/enterprise-ai-code-assistants-air-gapped-environments)（取得日: 2026-03-06）

---

## 4. 「no network calls」のスコープ解釈（業界標準との照合）

### ビルド時 vs ランタイム時の区別

| フェーズ | 内容 | 「no network」制約の対象か |
|---|---|---|
| **インストール時** | NuGet パッケージの取得、dotnet tool install | 通常は対象外（事前準備フェーズ） |
| **ビルド時 / 依存関係復元時** | `dotnet restore`、NuGet パッケージ復元 | 対象外（CI の前工程として許容） |
| **分析ランタイム時** | CLIツールが `roslyntic check` を実行する際 | **対象（制約が適用される）** |
| **結果送信時** | 外部サービスへの結果アップロード | **対象（制約が適用される）** |
| **テレメトリ送信時** | 使用統計の非同期送信 | **対象（制約が適用される）** |

### CI/CD 環境での「no network」の一般的定義

調査により以下の業界標準的解釈が確認された:

1. **「no network calls」= 分析処理中（ランタイム）のアウトバウンド通信禁止** が主流の解釈
2. **NuGet restore は「ビルド前工程」**として分離されており、`dotnet restore` 済みの環境での実行を前提とするツールは多い
3. **テレメトリは opt-out 可能であること**が事実上の要件（Microsoft 自身の .NET SDK が模範例）
4. **エアギャップ環境での「no network calls」は絶対要件**（通信を試みることで接続待ちタイムアウトが発生し、CI/CD を著しく遅延させる）

**Roslyntic の CLAUDE.md に記載の「no network calls」の解釈**:
> Local-only execution: no network calls and no telemetry by default.

これは「ランタイム中のネットワーク通信なし、テレメトリなし」という意味と解釈するのが妥当。NuGet パッケージが事前にキャッシュ済みであれば、`MSBuildWorkspace` 経由のソリューションロードもネットワーク通信を行わない。

---

## 5. MSBuildWorkspace のネットワーク動作に関する詳細情報

Roslyntic.Core は `MSBuildWorkspace` を使用してソリューションをロードする（architecture.md L23）。

**動作メカニズム**:
- `OpenSolutionAsync()` → 内部でデザインタイムビルドを実行
- デザインタイムビルド = ソースファイル・参照・コンパイルオプションを取得（バイナリ出力なし）
- このフェーズで NuGet パッケージ参照を解決しようとする可能性がある

**実際のネットワーク通信の有無**:
- **パッケージがグローバルキャッシュ（`~/.nuget/packages`）に存在する** → ネットワーク通信なし
- **パッケージがキャッシュにない** → `nuget.org` 等へのダウンロードが発生（ネットワーク通信あり）

**Roslyntic における対策**:
- ドキュメントやREADMEで「実行前に `dotnet restore` を済ませること」を前提条件とする
- または `EnableNuGetPackageRestore=false` を設定して意図しない復元を防ぐ
- CI 環境では NuGet パッケージを事前キャッシュしておく（`--packages` ディレクトリ等）

**出典**:
- [Using MSBuildWorkspace - Gist by DustinCampbell](https://gist.github.com/DustinCampbell/32cd69d04ea1c08a16ae5c4cd21dd3a3)（取得日: 2026-03-06）
- [NuGet Package Restore - Microsoft Learn](https://learn.microsoft.com/en-us/nuget/consume-packages/package-restore)（取得日: 2026-03-06）

---

## 6. Roslyntic の制約への示唆（分析）

### 現在の制約（CLAUDE.md より）
```
Local-only execution: no network calls and no telemetry by default.
```

### 示唆

#### 6.1 ランタイムネットワーク通信は問題なし（設計上）
Roslyntic.Core が使用する技術スタック（Roslyn DiagnosticAnalyzer、MSBuildWorkspace）は、パッケージが事前に復元済みであれば**ランタイム中にネットワーク通信を行わない**。これは業界標準の Roslyn アナライザー（StyleCop 等）と同等のポジション。

#### 6.2 NuGet 復元は「事前工程」として明示が必要
MSBuildWorkspace がパッケージ復元を暗黙的にトリガーするリスクがある。対策:
- `EnableNuGetPackageRestore=false` を明示的に設定するか
- ドキュメントで「`dotnet restore` 済みの環境での実行を前提とする」と明記

#### 6.3 テレメトリは実装しない（または opt-out デフォルト）
競合ツール（Semgrep, SonarScanner）はデフォルトでメトリクス送信を行い、ユーザーが opt-out を求める。Roslyntic の「no telemetry by default」は業界で差別化要素となり得る好ポリシー。ただし将来的にテレメトリを追加する場合は:
- 環境変数（例: `ROSLYNTIC_TELEMETRY_OPTOUT=1`）による opt-out を必ず実装
- デフォルト OFF（opt-in のみ）

#### 6.4 SonarScanner との根本的な設計差異
SonarScanner はサーバー中心型アーキテクチャ（結果をサーバーへ送信必須）。Roslyntic は**ローカル完結型 SARIF 出力**（STDOUT への機械可読出力）。この差異が「no network calls」ポリシーの実現可能性を高めており、エアギャップ CI 環境での競争優位になる。

#### 6.5 Semgrep の `--disable-version-check` を参考に
バージョンチェック通知を実装するとしても、ネットワーク通信を発生させてはならない。バージョン情報は SARIF の `tool.driver.version` に埋め込むだけで十分（サーバー問い合わせ不要）。

---

## 7. 調査できなかった項目とその理由

| 項目 | 理由 |
|---|---|
| `MSBuildWorkspace` が NuGet 復元を暗黙的にトリガーするかの直接的な再現検証 | ローカル環境でのコード実行が必要。Web 調査範囲外。 |
| Roslyn アナライザーのネットワークブロッキング（AppContainer 等）の公式仕様 | CAS 廃止後の公式代替ポリシーに関するドキュメントが存在しない。GitHub issue では「プロセス分離を使え」との回答のみ。 |
| SonarScanner のエアギャップ対応の最新仕様（2025.1 版） | 公式ドキュメントの `/configuring/` ページが 404、詳細な offline-mode ページが見つからなかった。 |
| Semgrep `--validate` 時のネットワーク通信の現在の状況 | 2024年5月の GitHub コメントでは未解決とのことだったが、2026年3月時点での最新状況は未確認。 |
| MSBuildWorkspace の `SkipUnrecognizedProjects` プロパティとネットワーク通信の関係 | 公式ドキュメントが薄く、検索結果に有効な情報が得られなかった。 |

---

*取得日: すべての URL は 2026-03-06 に取得*
