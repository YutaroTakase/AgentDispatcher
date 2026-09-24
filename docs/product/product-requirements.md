# AgentDispatcher — Product Requirements v0.1

## 1. Scope

AgentDispatcher MVPは、複数GitHub Repositoryに対するCodex execution dispatcherを提供する。

対象Runtimeは、WSL2 Ubuntu上にインストールされたChatGPTアカウント認証済みCodex CLIとする。

## 2. Project

### 2.1 Project登録

ユーザーはWeb UIからProjectを登録、編集、一時停止、削除できる。

Projectは最低限次を保持する。

- Project ID
- Display Name
- GitHub Repository（owner/repository）
- Enabled
- Default Branch
- Issue Scan Interval
- Max Concurrent Executions
- Execution Retention Days
- Failure Worktree Retention Days

Repository URLやowner/repositoryはGitHub上で到達可能であることを登録時に検証する。

### 2.2 Repository管理

AgentDispatcherはProjectごとのmanaged repository cloneとIssueごとのworktreeをデータ領域配下で管理する。

ユーザーが任意のローカル作業Directoryを指定することをMVP必須要件にしない。

## 3. GitHub Integration

### 3.1 認証

GitHub操作は専用Workerユーザーで認証済みのGitHub CLIまたは同等のcredential helperを使用する。

GitHub tokenをAgentDispatcher DBへ保存しない。

### 3.2 Issue Selector

ProjectごとにGitHub Issue Search条件を設定できる。

MVPでは次を設定可能とする。

- Query fragment
- Sort field
- Sort order
- Max candidates per scan

Repository scopeはProject設定からAgentDispatcherが付与する。

実行候補はGitHub上でopenなIssueに限定する。

Pull RequestはIssue候補としてdispatchしない。

### 3.3 Dispatch exclusion

次のIssueは候補から除外する。

- 同一Project / IssueでExecutionがQueued、Preparing、Runningのもの
- ProjectがDisabled
- Project concurrency上限到達時
- Repository / GitHub health checkに失敗している場合

## 4. Model Routing

### 4.1 Default Route

Projectは必ずDefault Routeを1つ持つ。

Routeは最低限次を保持する。

- Model identifier
- Reasoning effort

Model identifierは特定世代へハードコードせず、Codex CLIへ渡せる文字列として保持する。

### 4.2 Routing Rules

Default Routeより前に評価するordered Routing Rulesを複数定義できる。

MVPの条件はIssue labelベースとする。

各Ruleは次を持つ。

- Priority / order
- Required labels
- Excluded labels
- Model identifier
- Reasoning effort
- Enabled

上から順に評価し、最初に一致したRuleを採用する。

一致RuleがなければDefault Routeを採用する。

### 4.3 Route evidence

Executionには、どのRuleまたはDefaultが選択されたかをsnapshotとして保存する。

後からProject設定が変更されても過去ExecutionのRoute判定を再現できるようにする。

## 5. Scheduling / Dispatch

### 5.1 Scan

Background WorkerはEnabled Projectを設定されたintervalでscanする。

MVPではcron式ではなく、分単位intervalを基本とする。

### 5.2 Candidate selection

1回のscanで取得した候補から、Project concurrencyに空きがある分だけExecutionを作成する。

候補選択は設定されたsort/orderに従い決定的に行う。

### 5.3 Manual trigger

ユーザーはWeb UIから次を実行できる。

- Project scanを今すぐ実行
- 特定Issueを手動dispatch

手動dispatchでも同一Issue排他、Project concurrency、health checkを迂回しない。

## 6. Execution

### 6.1 State

Executionは最低限次の状態を持つ。

- Queued
- Preparing
- Running
- Succeeded
- Failed
- Canceled

状態遷移は履歴として記録する。

### 6.2 Preparation

Preparingでは最低限次を行う。

1. managed repositoryのremote更新
2. Project default branchの最新revision確定
3. Issue専用worktree作成または安全な再利用
4. Worker health再確認
5. Model Routing snapshot確定

準備に失敗した場合、Codexを起動せずFailedとする。

### 6.3 Worktree isolation

ExecutionはIssue専用worktreeをworking directoryとしてCodexを起動する。

異なるIssueのCodex processが同一working treeを共有しない。

### 6.4 Codex invocation

MVPはnon-interactiveなCodex CLI executionを利用する。

Dispatcherは最低限次を指定する。

- working directory
- selected model
- selected reasoning effort
- target Issue identifier
- Project / Repository identity

Repository固有の詳細な開発指示をDispatcher promptへ複製しない。

Repository側のAGENTS.md等のinstruction sourceをCodexが利用できる構造とする。

### 6.5 Process control

DispatcherはCodex processについて最低限次を管理する。

- process start
- process identifier
- stdout
- stderr
- exit code
- start time
- finish time
- cancel

MVPではPause / Resume / interactive approval UIを必須としない。

## 7. Execution History

### 7.1 保存情報

Executionには最低限次を保存する。

- Execution ID
- Project ID
- Issue number
- Issue title snapshot
- Issue label snapshot
- Trigger type（Scheduled / Manual）
- Routing rule snapshot
- Model
- Reasoning effort
- Base revision
- Worktree path
- Status
- StartedAt / FinishedAt
- Exit code
- Final result
- stdout / stderr log reference
- Failure summary

### 7.2 一覧

Web UIからProject横断でExecution一覧を確認できる。

最低限次でfilterできる。

- Project
- Status
- Model
- Trigger type
- Date range

### 7.3 詳細

Execution詳細では次を確認できる。

- Issueへのlink
- selected route
- state timeline
- final result
- stdout / stderr
- start / finish / duration
- failure information

## 8. Retention

ProjectごとにExecution retention daysを設定できる。

Retention期限を超えたExecutionについて、Background cleanupが保存データとlogを削除する。

成功Executionのworktreeは終了後速やかに削除可能とする。

失敗ExecutionのworktreeはProject設定の日数だけ保持し、調査後にcleanupする。

RetentionでGitHub上のIssue、branch、PR、commitを削除しない。

## 9. Host Health

Web UIから最低限次の状態を確認できる。

- Git available
- GitHub CLI available
- GitHub authenticated
- Codex CLI available
- Codex authenticated / usable
- systemd available
- data directory writable
- free disk space

Projectごとに最低限次を確認できる。

- GitHub Repository reachable
- default branch reachable
- managed repository sync
- worktree root writable

## 10. Web UI

MVPは最低限次の画面を持つ。

- Dashboard
- Projects
- Project Create / Edit / Detail
- Routing Rules
- Executions
- Execution Detail
- Host Health

Dashboardでは少なくともRunning / Failed / Recent executionsとProject healthを確認できる。

## 11. Installation

### 11.1 Supported environment

初期サポート対象:

- Windows 11
- WSL2
- Ubuntu
- systemd enabled

### 11.2 Installer

RepositoryはWSL向けinstallerを提供する。

Installerは最低限次を実施または検証する。

- AgentDispatcher service user / worker user
- application files
- data directories
- systemd services
- .NET runtime
- Node.js build prerequisites
- Git
- GitHub CLI
- Codex CLI

ChatGPT / GitHubへのログインそのものをcredentialのコピーで自動化しない。

必要な対話ログイン手順を案内し、Health Checkで利用可能性を検証する。

### 11.3 Network

初期状態ではlocalhostからのみ利用可能とする。

LAN / Internetへ公開する構成はMVP対象外とする。

## 12. Persistence

MVPはSQLiteを使用する。

SQLiteへ保存するのはAgentDispatcher固有の次のデータを中心とする。

- Projects
- Issue selector settings
- Routing settings
- Executions
- Execution events
- Retention metadata

GitHub Issue / PR / CIの独自コピーを正本として保持しない。

Full stdout / stderrはDBへ無制限に格納せず、file storageとmetadataを分離可能な設計とする。

## 13. Security

- Web processとCodex worker processのUnix identityを分離する。
- Worker credentialをWeb processへ露出しない。
- ChatGPT credentialをDBへ保存しない。
- GitHub credentialをDBへ保存しない。
- Codexは対象worktreeをworking directoryとして実行する。
- Web UIから任意shell commandを入力・実行する機能をMVPに含めない。
- AgentDispatcher repositoryへsecretをcommitしない。

## 14. Non-functional Requirements

### Reliability

- service restart後もProject設定とExecution履歴を保持する。
- Running process消失を検出し、Executionを永続的にRunningのまま残さない。
- 同一Issueの二重dispatchを防止する。

### Observability

- Application log
- Dispatcher decision log
- Execution state transition
- cleanup result

を確認可能にする。

### Maintainability

特定Project名、特定label名、特定Model名をCoreへハードコードしない。

GitHub access、Codex execution、Persistenceを交換可能な境界として分離する。

## 15. MVP Acceptance Criteria

- [ ] WSL2 Ubuntuへinstallerで導入できる。
- [ ] localhostのWeb UIへアクセスできる。
- [ ] GitHub RepositoryをProject登録できる。
- [ ] Issue Selectorで対象Issueを取得できる。
- [ ] label条件からModel / Reasoning EffortをRouteできる。
- [ ] ChatGPT認証済みCodex CLIをIssue専用worktreeで起動できる。
- [ ] 同一Issueを二重実行しない。
- [ ] Executionの成功 / 失敗 / cancelを記録できる。
- [ ] Execution一覧と詳細、stdout / stderr、final resultを確認できる。
- [ ] ProjectごとのRetention Policyで履歴とlogをcleanupできる。
- [ ] Host HealthでGitHub / Codex利用可否を確認できる。
