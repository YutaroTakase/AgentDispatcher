# System Overview

## Logical Architecture

```text
Windows Browser
      │
      │ localhost
      ▼
┌──────────────────────────────┐
│ WSL2 Ubuntu                  │
│                              │
│  Web UI                      │
│      │                       │
│      ▼                       │
│  Control API ─── Durable DB  │
│      │                       │
│      ▼                       │
│  Dispatcher Worker           │
│      │                       │
│      ├──── GitHub / Git      │
│      │                       │
│      └──── Worker Runtime    │
│              │               │
│              ▼               │
│           Codex              │
│              │               │
│              └─ Worktree     │
└──────────────────────────────┘
```

Componentの採用技術と配置は[Architecture Baseline](README.md)を正本とする。

## Dispatch Flow

1. Enabled Projectがscan対象になる。
2. GitHubからIssue Selectorに一致する候補を取得する。
3. Active Execution、Project concurrency、health guardを適用する。
4. ordered Routing Rulesから実行Routeを決定する。
5. ExecutionをQueuedとして永続化する。
6. managed repositoryを更新し、Issue worktreeを準備する。
7. Worker healthを再確認してCodexを起動する。
8. process stateとlogを追跡する。
9. 終了結果をSucceeded / Failed / Canceledへ確定する。
10. Retention Policyに従ってlocal execution assetsを整理する。

Manual dispatchも同じguardとstate machineを使用する。

## Execution State

```text
Queued
  ↓
Preparing
  ↓
Running ─────→ Canceled
  │
  ├──────────→ Succeeded
  └──────────→ Failed
```

Preparationに失敗した場合はCodexを起動せずFailedへ遷移する。

## Source of Truth

| Data | Source of Truth |
|---|---|
| GitHub Issue / PR / Review / CI | GitHub |
| Project / selector / routing configuration | AgentDispatcher |
| Execution state / history | AgentDispatcher |
| Full process logs | AgentDispatcher log storage |
| Git repository content | Git remote |
| Codex authentication | Worker user environment |
| GitHub authentication | Worker user environment |

## Isolation and Concurrency

- 1 Executionは1 Project + 1 Issueに対応する。
- 同一Project / IssueのActive Executionは最大1件とする。
- 異なるIssueはProject concurrency上限内で並列実行できる。
- 各Issueは専用worktreeを使用する。
- Codex processはControl APIとは異なるUnix identityで実行する。

## Data Layout

実pathはinstaller / configurationで決定する。論理配置は次とする。

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

## Recovery

- **GitHub unavailable**: 新規Executionを作成しない。
- **Codex unavailable / unauthenticated**: 新規Executionを起動せずHealthへ反映する。
- **Process disappeared**: Worker起動時にdurable stateと実processをreconcileし、stale Runningを解消する。
- **Worktree preparation failure**: Codexを起動せずFailedとする。
- **Application restart**: durable stateからProject、Execution、Retention情報を復元する。

## Extension Points

MVP後も次を交換・追加できる境界を維持する。

- Issue Provider
- Execution Runtime
- Remote Worker
- Model Catalog
- CI integration
- Routing predicates
- Persistence provider
- User authentication
