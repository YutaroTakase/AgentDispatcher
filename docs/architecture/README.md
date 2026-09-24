# Architecture Baseline

## Purpose

このDirectoryはAgentDispatcherの現在有効なArchitectureを定義する。

Product Requirementsを満たすための技術境界を扱い、変更履歴やReview経緯はGit history / GitHub Issue / Pull Requestへ残す。

## Baseline

AgentDispatcherはWSL2 Ubuntu上で動作するself-hosted local control planeとして構成する。

主要component:

- Nuxt 4 Web SPA
- ASP.NET Core Control API
- .NET Background Worker / Dispatcher
- SQLite
- File-based execution logs
- GitHub CLI / Git
- Codex CLI
- systemd
- Git worktree

## Repository Structure

FENと同じroot分離を基本とする。

```text
AgentDispatcher/
├─ .agents/
├─ .github/
├─ apps/
│  ├─ web/
│  ├─ backend/
│  ├─ worker/
│  ├─ shared/
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
```

アプリケーション本体は`apps/`、導入・補助ツールは`tools/`、self-hosted runtime定義は`infra/self-hosted/`へ配置する。

## Runtime Boundary

Control APIとDispatcher Workerは同じSQLiteへアクセスできるが、責務を分離する。

- Web / API: Configuration、Query、Cancel request、History表示
- Worker: Scan、Candidate selection、Worktree、Codex process、Cleanup
- GitHub: Issue / PR等のWork Item source of truth
- Worker user home: GitHub / Codex credential
- SQLite: AgentDispatcher固有state
- File storage: Execution full logs

## Frontend

FrontendはNuxt 4 + TypeScriptとする。

初期Productionでは管理画面をSPAとしてbuildし、ASP.NET Coreからstatic assetsを配信できる構成を優先する。

SSRをProduct要件としない。

## Backend

Backendは.NET 10 / ASP.NET Coreを使用する。

初期APIはlocal-only REST APIとする。

## Worker

Dispatcherは.NET 10 Workerとしてsystemd常駐する。

Codex executionは専用Unix worker identityで起動し、Web/API processとcredential boundaryを分離する。

## Persistence

SQLiteを初期DBとする。

Execution full logはDBと分離可能なfile storageへ保存する。

## Further Documents

- [System Overview](system-overview.md)
- [Architecture Decisions](decisions.md)
- [Product Requirements](../product/product-requirements.md)
