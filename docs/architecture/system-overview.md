# System Overview

## Logical Architecture

```text
Windows Browser
      │
      │ http://localhost
      ▼
┌──────────────────────────────┐
│ WSL2 Ubuntu                  │
│                              │
│  Nuxt SPA                    │
│      │                       │
│      ▼                       │
│  ASP.NET Core Control API    │
│      │                       │
│      ├──── SQLite            │
│      ├──── Execution Logs    │
│      │                       │
│      ▼                       │
│  Dispatcher Worker           │
│      │                       │
│      ├──── GitHub CLI / Git  │
│      │                       │
│      └──── systemd-run       │
│              │               │
│              ▼               │
│        Codex Worker User     │
│              │               │
│              ├─ Codex auth   │
│              ├─ GitHub auth  │
│              └─ Worktree     │
└──────────────────────────────┘
```

## Main Flows

### Project Registration

1. User creates Project in Web UI.
2. API validates repository identity and configuration.
3. Worker verifies GitHub access and managed repository location.
4. Default Route and Retention settings are stored in SQLite.
5. Project becomes eligible for scan when Enabled.

### Scheduled Dispatch

1. Worker finds Projects whose scan interval is due.
2. Worker queries GitHub issues using the Project Issue Selector.
3. Active Execution and concurrency rules are applied.
4. Candidate Issue labels are evaluated against ordered Routing Rules.
5. Execution is persisted as Queued.
6. managed repository is synchronized.
7. Issue worktree is created.
8. Codex process is started under dedicated worker identity.
9. stdout / stderr and state transitions are persisted.
10. process completion updates Execution to Succeeded or Failed.
11. Worktree cleanup policy is applied.

### Manual Dispatch

Manual dispatch uses the same validation and state machine as scheduled dispatch.

Web UI cannot bypass concurrency, duplicate-execution, repository health, or worker health guards.

### Cancel

1. User requests cancel from Web UI.
2. API records cancel request.
3. Worker terminates the corresponding process / transient systemd unit.
4. Execution becomes Canceled after process termination is confirmed.

## Source of Truth

| Data | Source of Truth |
|---|---|
| GitHub Issue / PR / Review / CI | GitHub |
| Project configuration | AgentDispatcher SQLite |
| Issue selector | AgentDispatcher SQLite |
| Routing rules | AgentDispatcher SQLite |
| Execution state/history | AgentDispatcher SQLite |
| Full process logs | AgentDispatcher file storage |
| Git repository | Git remote + managed local clone |
| Codex authentication | Worker user Codex environment |
| GitHub authentication | Worker user GitHub environment |

## Process Isolation

1 Executionにつき1 Issue worktreeを使用する。

同一Project / IssueにActive Executionは最大1件とする。

異なるIssueはProject concurrency上限の範囲で並列実行できる。

Codex processはControl API processと異なるUnix identityで実行する。

## Data Root

実際のpathはinstaller / configurationで決定するが、論理的には次を分離する。

```text
data/
├─ agent-dispatcher.db
├─ repositories/
│  └─ {project-id}/
├─ worktrees/
│  └─ {project-id}/{issue-number}/
└─ executions/
   └─ {execution-id}/
      ├─ stdout.log
      └─ stderr.log
```

## Failure Handling

### GitHub unavailable

新しいExecutionを作成しない。

既存Codex processをGitHub一時障害だけで強制停止しない。

### Codex unavailable / unauthenticated

新しいExecutionを起動せずHost Healthへ反映する。

### Process disappears

Worker再起動時にActive Executionと実Processをreconcileし、存在しないprocessをRunningのまま残さない。

### Worktree preparation failure

Codexを起動せずExecutionをFailedにする。

### Application restart

Project設定、Execution履歴、cleanup期限はSQLiteから復元する。

## Extension Points

MVP後に次を交換・追加できる境界を維持する。

- Issue Provider
- Execution Runtime
- Remote Worker
- Model Catalog
- CI integration
- Additional routing predicates
- PostgreSQL persistence
- Multi-user authentication
