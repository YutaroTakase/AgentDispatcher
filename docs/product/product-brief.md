# AgentDispatcher — Product Brief v0.1

## 1. プロダクトビジョン

AgentDispatcherは、GitHub Issueを起点として、セルフホスト環境上のAI coding workerへ作業を安全かつ再現可能にdispatchするためのローカルWebアプリケーションである。

第一段階では、WSL2上で動作し、GitHubから対象Issueを選択し、ProjectごとのModel Routing規則に従ってChatGPTアカウントで認証済みのCodex CLIを起動し、実行結果と履歴をWeb UIから確認できる状態を提供する。

特定Repositoryや特定開発フローへ依存せず、複数Projectへ再利用できることを最優先する。

## 2. 対象ユーザー

初期対象は、1台の開発PC上で複数RepositoryをAI coding workerに処理させたい個人開発者である。

初期リリースでは組織向けSaaS、複数ユーザー共同利用、インターネット公開を主目的にしない。

## 3. 解決する課題

AI coding workerを複数Projectで運用すると、次の管理がProjectごとに重複する。

- どのIssueを実行対象にするか
- どのModel / Reasoning Effortを使うか
- 同一Issueの二重実行防止
- Worktreeの作成と破棄
- Workerの起動・停止
- 実行結果、ログ、失敗理由の確認
- 実行履歴の保持と削除
- Codex / GitHub認証状態の確認

AgentDispatcherはこれらをProject外の共通Control Planeへ集約する。

## 4. 中核ユースケース

1. ユーザーがWeb UIからGitHub RepositoryをProjectとして登録する。
2. ProjectごとにIssue Selectorを設定する。
3. ProjectごとにDefault Routeとordered Routing Rulesを設定する。
4. Dispatcherが定期的に対象Issueを取得する。
5. 同一Issueの実行中排他とProject concurrencyを確認する。
6. 対象Issue専用Worktreeを準備する。
7. Routing結果に従うModel / Reasoning EffortでCodexを起動する。
8. Codexのstdout / stderr / exit code / final resultを回収する。
9. Web UIで実行中状態と過去Executionを確認する。
10. Retention Policyに従って履歴、ログ、Worktreeを整理する。

## 5. 設計原則

### GitHubをWork Itemの正本とする

AgentDispatcherはIssue本文、PR、Review、CI状態の独自正本を持たない。

Project設定、Routing設定、Execution履歴などAgentDispatcher固有情報だけを保持する。

### Worker認証情報をWebアプリへ持ち込まない

CodexとGitHubの認証は専用WorkerユーザーのCLI環境で管理する。

ChatGPT認証情報、GitHub credential、API keyをAgentDispatcher DBへ保存しない。

### Project固有ルールをCoreへ埋め込まない

Repository固有の開発ルール、Bootstrap、AGENTS.md、Test command、CI規約等は各Project側の責務とする。

AgentDispatcherはIssue選択、Routing、Execution lifecycleを担当する。

### 1 Execution = 1 Project + 1 Issue

1つのExecutionは1つのProjectと1つのGitHub Issueに対応する。

同一Issueを複数Workerが同時実行しない。

## 6. 初期提供形態

- Windows 11 + WSL2 Ubuntu
- systemd常駐
- localhostからWeb UIへアクセス
- ASP.NET Core Backend / Worker
- Nuxt 4 + TypeScript frontend
- SQLite
- GitHub CLI
- Codex CLI
- Git worktree

クラウドサービスへの依存を初期必須としない。

## 7. 初期リリースで提供しないもの

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

## 8. 成功条件

初期MVPは、クリーンなWSL環境にAgentDispatcherを導入し、Web UIだけでProjectを登録した後、対象GitHub Issueを検出してCodexを起動し、そのExecution結果を履歴画面で確認できることを完成条件とする。
