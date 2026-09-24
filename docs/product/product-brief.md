# AgentDispatcher — Product Brief v0.1

## 1. プロダクトビジョン

AgentDispatcherは、GitHub Issueを起点としてAI coding workerへ作業をdispatchし、複数Projectの実行を一元管理するためのセルフホスト型Webアプリケーションである。

特定Repositoryや特定開発フローへ依存せず、ProjectごとのIssue選択条件とModel Routingを設定するだけで再利用できることを重視する。

## 2. 対象ユーザー

初期対象は、1台の開発PC上で複数RepositoryをAI coding workerに処理させたい個人開発者とする。

組織向けSaaSや複数ユーザー共同利用は初期対象としない。

## 3. 解決する課題

AI coding workerを複数Projectで運用すると、次の管理がProjectごとに重複する。

- 実行対象Issueの選択
- Model / Reasoning Effortの選択
- 同一Issueの二重実行防止
- 実行環境とWorktreeの管理
- Workerの起動、停止、結果確認
- 実行履歴とログの保持、削除
- GitHub / Codex実行環境の健全性確認

AgentDispatcherはこれらを共通Control Planeへ集約する。

## 4. MVPで実現すること

- 複数GitHub RepositoryをProjectとして登録できる。
- ProjectごとにIssue取得条件を設定できる。
- ProjectごとにModel / Reasoning Routingを設定できる。
- 対象IssueごとにCodex executionを起動できる。
- 実行中状態、結果、ログ、失敗理由をWebから確認できる。
- 保存期間をProjectごとに設定し、不要な履歴と実行資産を自動整理できる。
- GitHub / Codexを含むHost Healthを確認できる。

詳細な振る舞いは[Product Requirements](product-requirements.md)を正本とする。

## 5. MVPで提供しないこと

- ChatGPT Chatそのものの管理
- ChatGPT Scheduled Tasksの管理
- OpenAI APIを利用した独自Agent runtime
- PM / Reviewer / Architect等の複数Agent orchestration
- GitHub CIの包括的な操作
- Pull Requestの自動Merge
- Remote Worker
- Windows native Worker
- Multi-user / RBAC
- SaaS hosting
- 課金、ライセンス管理
- Model使用量の正確な金額換算

## 6. 成功条件

クリーンな対応環境へAgentDispatcherを導入し、Web UIからProjectを登録した後、対象GitHub Issueを検出してCodexを起動し、その結果を履歴画面で確認できることをMVPの完成条件とする。
