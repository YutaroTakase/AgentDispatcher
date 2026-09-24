# アーキテクチャ

このディレクトリはAgentDispatcherの現在有効な技術構成を定義する。

プロダクト上の振る舞いは[プロダクト要件](../product/product-requirements.md)、実行の流れは[システム概要](system-overview.md)、採用理由は[アーキテクチャ判断](decisions.md)を正本とする。

## 技術構成

- フロントエンド: Nuxt 4 + TypeScript
- 管理API: .NET 10 / ASP.NET Core
- 常駐処理: .NET 10 Worker
- データベース: SQLite
- 実行ログ: ファイル保存
- ソース管理・Issue取得: Git / GitHub CLI
- AI開発作業者: Codex CLI
- サービス管理: systemd
- 実行単位の分離: Git worktree

フロントエンドはSPAとして構築し、初期構成ではASP.NET Coreから静的ファイルとして配信する。SSRは必須としない。

## リポジトリ構成

```text
AgentDispatcher/
├─ .agents/
├─ .github/
├─ apps/
│  ├─ AgentDispatcher.slnx
│  ├─ Directory.Build.props
│  ├─ web/
│  │  └─ AgentDispatcher.Web/
│  ├─ backend/
│  │  └─ AgentDispatcher.Api/
│  ├─ worker/
│  │  └─ AgentDispatcher.Worker/
│  ├─ shared/
│  │  ├─ AgentDispatcher.Domain/
│  │  └─ AgentDispatcher.Infrastructure/
│  └─ tests/
├─ docs/
│  ├─ product/
│  ├─ architecture/
│  ├─ development/
│  ├─ planning/
│  └─ reference/
├─ infra/
│  └─ self-hosted/
└─ tools/
   └─ installer/
```

- `apps/`: アプリケーション本体とテスト
- `docs/`: 現在有効なプロダクト・アーキテクチャ・開発文書
- `infra/`: セルフホスト実行環境の定義
- `tools/`: 導入、更新、削除、状態確認用ツール
- `.github/`: GitHub上の自動化
- `.agents/`: AIを利用した開発手順の補助資産

## 実行時の責務分離

- **Web画面・管理API**: 設定、参照、取消要求、履歴表示
- **常駐処理**: Issue確認、候補選定、作業ツリー管理、Codex実行、定期整理、再起動後の復旧
- **実行作業者用Unix利用者**: GitHub・Codex認証情報の保持とCodex実行
- **SQLite**: AgentDispatcher固有の永続データ
- **ログ保存領域**: 実行時の完全なログ
- **GitHub**: Issue、Pull Request、レビュー、CIの正本

管理APIと常駐処理は永続データを共有できるが、Web要求の処理と長時間実行処理の責務は分離する。

Codexは専用Unix利用者で起動し、Web・管理APIの実行利用者から認証情報を分離する。
