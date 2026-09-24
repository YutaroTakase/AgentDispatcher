# AgentDispatcher — Product Requirements v0.1

## 1. Scope

AgentDispatcher MVPは、複数GitHub Repositoryに対するCodex execution dispatcherを提供する。

初期RuntimeはWSL2 Ubuntu上のChatGPTアカウント認証済みCodex CLIとする。

技術構成の正本は[Architecture Baseline](../architecture/README.md)とする。

## 2. Project Management

ユーザーはWeb UIからProjectを登録、編集、一時停止、削除できる。

Projectは最低限次を設定できる。

- Display Name
- GitHub Repository
- Enabled
- Default Branch
- Issue Scan Interval
- Max Concurrent Executions
- Execution Retention Days
- Failure Worktree Retention Days

登録時にRepositoryとDefault Branchへ到達可能であることを検証する。

AgentDispatcherはProjectごとのmanaged repository cloneとIssueごとのworktreeを管理する。

## 3. Issue Selection

ProjectごとにGitHub Issue Search条件を設定できる。

MVPでは次を設定対象とする。

- Query fragment
- Sort field
- Sort order
- Max candidates per scan

Repository scopeはProject設定から自動付与する。

dispatch対象はopenなIssueに限定し、Pull Requestは候補に含めない。

次の場合は新規dispatchしない。

- 同一Project / IssueにActive Executionがある
- ProjectがDisabled
- Project concurrency上限に達している
- 必要なGitHub / Repository health checkに失敗している

## 4. Model Routing

ProjectはDefault Routeを1つ持ち、必要に応じてordered Routing Rulesを複数定義できる。

Routeは次を保持する。

- Model identifier
- Reasoning effort

Model identifierは特定Model世代へ固定せず、Codex Runtimeへ渡せる設定値として扱う。

MVPのRouting Rule条件はIssue labelとする。

各Ruleは次を設定できる。

- Order
- Required labels
- Excluded labels
- Model identifier
- Reasoning effort
- Enabled

Ruleは上から順に評価し、最初の一致を採用する。一致しない場合はDefault Routeを採用する。

Executionには実行時のRoute判定をsnapshotとして保存する。

## 5. Dispatch

Enabled Projectは設定された分単位intervalでscanする。

scanではIssue Selectorに一致する候補から、Project concurrencyの空き数まで決定的な順序でExecutionを作成する。

ユーザーはWeb UIから次を手動実行できる。

- Project scan
- 特定Issueのdispatch

手動実行も通常の排他、concurrency、health checkを迂回しない。

## 6. Execution

Executionは次の状態を持つ。

- Queued
- Preparing
- Running
- Succeeded
- Failed
- Canceled

Preparingでは、最新Default Branchの取得、Issue worktree準備、Worker health確認、Routing snapshot確定を完了してからCodexを起動する。

1つのExecutionは1つのProjectと1つのIssueに対応し、Issue専用worktreeをworking directoryとして使用する。

DispatcherはCodexへ最低限次を渡す。

- Project / Repository identity
- target Issue identifier
- selected model
- selected reasoning effort
- working directory

Repository固有の開発PolicyをDispatcher promptへ複製しない。

Dispatcherはprocess start、process identifier、stdout、stderr、exit code、開始・終了時刻、cancelを管理する。

MVPではPause / Resume / interactive approval UIを必須としない。

## 7. Execution History

Executionには最低限次を保存する。

- Execution ID
- Project ID
- Issue number
- Issue title / label snapshot
- Trigger type
- Route snapshot
- Base revision
- Status
- StartedAt / FinishedAt
- Exit code
- Final result
- stdout / stderr log reference
- Failure summary

Web UIからProject横断でExecution一覧を確認でき、最低限次でfilterできる。

- Project
- Status
- Model
- Trigger type
- Date range

Execution詳細ではIssue link、Route、state timeline、result、log、duration、failure informationを確認できる。

## 8. Retention

ProjectごとにExecution履歴と失敗Worktreeの保存期間を設定できる。

Background cleanupは期限を超えたAgentDispatcher固有データ、log、local worktreeを削除する。

GitHub上のIssue、branch、PR、commitはRetention処理で削除しない。

## 9. Health

Web UIからHost Healthとして最低限次を確認できる。

- Git
- GitHub CLI
- GitHub authentication
- Codex CLI
- Codex authentication / usability
- systemd
- data directory write access
- free disk space

Project Healthとして最低限次を確認できる。

- Repository reachability
- Default Branch reachability
- managed repository sync
- worktree root write access

## 10. Web UI

MVPは最低限次の画面を持つ。

- Dashboard
- Projects
- Project Create / Edit / Detail
- Routing Rules
- Executions
- Execution Detail
- Host Health

DashboardではRunning / Failed / Recent executionsとProject healthを確認できる。

## 11. Installation and Access

初期サポート対象はWindows 11 + WSL2 Ubuntuで、systemdを利用可能であることを前提とする。

RepositoryはWSL向けinstallerを提供し、必要componentの導入または利用可否確認を行う。

GitHub / ChatGPTへの対話ログインをcredentialコピーで自動化しない。

初期状態のWeb UIはlocalhostからのみ利用可能とし、LAN / Internet公開はMVP対象外とする。

## 12. Persistence and Source of Truth

AgentDispatcherはProject設定、Routing設定、Execution、Execution event、Retention metadataを保持する。

GitHub Issue / PR / Review / CIの独自正本を作らない。

Full stdout / stderrは無制限にDBへ格納せず、log storageとmetadataを分離できること。

## 13. Security

- Web processとCodex worker processのUnix identityを分離する。
- Worker credentialをWeb processへ露出しない。
- ChatGPT credentialをAgentDispatcher DBへ保存しない。
- GitHub credentialをAgentDispatcher DBへ保存しない。
- Codexは対象worktreeをworking directoryとして実行する。
- Web UIから任意shell commandを入力・実行できない。
- SecretをRepositoryへcommitしない。

## 14. Reliability and Observability

- service restart後もProject設定とExecution履歴を保持する。
- process消失を検出し、Executionを永続的にRunningのまま残さない。
- 同一Issueの二重dispatchを防止する。
- Application log、Dispatcher decision、Execution state transition、cleanup resultを確認可能にする。

## 15. Maintainability

- 特定Project名、label名、Model名をCoreへハードコードしない。
- GitHub access、Codex execution、Persistence、Process Runtimeを交換可能な境界として分離する。
- Project固有の開発Policyは対象Repository側へ保持する。

## 16. MVP Acceptance Criteria

- [ ] 対応WSL環境へinstallerで導入できる。
- [ ] localhostのWeb UIへアクセスできる。
- [ ] GitHub RepositoryをProject登録できる。
- [ ] Issue Selectorで対象Issueを取得できる。
- [ ] label条件からModel / Reasoning EffortをRouteできる。
- [ ] ChatGPT認証済みCodex CLIをIssue専用worktreeで起動できる。
- [ ] 同一Issueを二重実行しない。
- [ ] Executionの成功 / 失敗 / cancelを記録できる。
- [ ] Execution一覧、詳細、stdout / stderr、final resultを確認できる。
- [ ] ProjectごとのRetention Policyで履歴、log、local worktreeをcleanupできる。
- [ ] Host / Project Healthを確認できる。
