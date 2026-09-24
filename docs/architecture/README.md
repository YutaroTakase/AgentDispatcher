# Architecture Baseline

このDirectoryはAgentDispatcherの現在有効な技術Architectureを定義する。

Product上の振る舞いは[Product Requirements](../product/product-requirements.md)、実行フローは[System Overview](system-overview.md)、採用理由は[Architecture Decisions](decisions.md)を正本とする。

## Technology Baseline

- Frontend: Nuxt 4 + TypeScript
- Control API: .NET 10 / ASP.NET Core
- Dispatcher: .NET 10 Worker
- Persistence: SQLite
- Execution log: file storage
- Source control / work item access: Git / GitHub CLI
- Coding worker: Codex CLI
- Service manager: systemd
- Execution isolation: Git worktree

FrontendはSPAとしてbuildし、初期ProductionではASP.NET Coreからstatic assetsを配信する。SSRを必須としない。

## Repository Structure

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

- `apps/`: application code and tests
- `docs/`: current product, architecture, development documentation
- `infra/`: self-hosted runtime definitions
- `tools/`: install / update / uninstall / health tooling
- `.github/`: repository automation
- `.agents/`: optional AI development workflow assets

## Runtime Boundaries

- **Web / API**: configuration、query、cancel request、history
- **Dispatcher Worker**: scan、candidate selection、worktree lifecycle、Codex process、cleanup、recovery
- **Worker identity**: GitHub / Codex credentials and Codex execution
- **SQLite**: AgentDispatcher-owned durable state
- **File storage**: full execution logs
- **GitHub**: Issue / PR / Review / CI source of truth

Control APIとDispatcherはdurable stateを共有できるが、Web request lifecycleと長時間execution lifecycleを分離する。

Codex executionは専用Unix identityで起動し、Web/API identityからcredentialを分離する。
