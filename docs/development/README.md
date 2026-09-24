# Development Documentation

このDirectoryはAgentDispatcherの開発・運用ルールの入口とする。

Product requirementは`docs/product/`、Architectureは`docs/architecture/`を正本とする。

## Planned Repository Layout

```text
AgentDispatcher/
├─ .agents/                         # AI agent skills / optional local workflow
├─ .github/                         # GitHub Actions / repository automation
├─ apps/
│  ├─ AgentDispatcher.slnx          # .NET workspace
│  ├─ Directory.Build.props
│  ├─ web/
│  │  └─ AgentDispatcher.Web/       # Nuxt 4 + TypeScript
│  ├─ backend/
│  │  └─ AgentDispatcher.Api/       # ASP.NET Core Control API
│  ├─ worker/
│  │  └─ AgentDispatcher.Worker/    # scan / dispatch / cleanup
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
│  └─ self-hosted/                  # WSL / systemd runtime
└─ tools/
   └─ installer/                    # install / update / uninstall / health
```

## Initial Development Order

1. .NET workspace / Nuxt workspace skeleton
2. Project domain + SQLite persistence
3. GitHub repository / issue query integration
4. Routing Rule domain
5. Execution state machine
6. Worktree manager
7. Codex process adapter
8. Background scan / dispatch
9. Execution history API
10. Nuxt management UI
11. Retention cleanup
12. WSL installer / systemd
13. Host health / recovery validation

## Development Principles

- Product固有要件をCoreへ混ぜない。
- Model名をCoreへ固定しない。
- CLI command文字列をDomainへ漏らさずadapterへ閉じ込める。
- GitHub、Codex、Persistence、Process Runtimeをinterface境界で分離する。
- Execution state transitionはDBで追跡可能にする。
- External commandのstdout / stderrを調査可能な形で保持する。
- Web UIから任意shell commandを実行可能にしない。
- Migration可能なDB schemaを使用する。
- Source of Truthの重複を避ける。

## Validation Direction

MVPでは少なくとも次を自動化対象とする。

- .NET build / unit tests
- Nuxt typecheck / lint / build
- Persistence migration tests
- Routing Rule deterministic tests
- duplicate dispatch concurrency tests
- Execution state transition tests
- Worktree lifecycle integration tests
- Process cancellation tests
- Retention cleanup tests
- API smoke tests

Codex CLI実機認証を必要とするE2Eは、通常unit testと分離する。
