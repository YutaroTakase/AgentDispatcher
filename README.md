# AgentDispatcher

GitHub Issueを起点に、セルフホスト環境のAI coding workerへ作業をdispatchするためのローカルWebアプリケーションです。

初期MVPではWSL2 Ubuntu上で動作し、複数GitHub Projectについて次を管理します。

- Issue selection
- Model / Reasoning routing
- ChatGPTアカウント認証済みCodex CLI execution
- IssueごとのGit worktree
- Execution status / history / logs
- Retention / cleanup
- Host health

## Documentation

- [Product Brief](docs/product/product-brief.md)
- [Product Requirements](docs/product/product-requirements.md)
- [Architecture Baseline](docs/architecture/README.md)
- [System Overview](docs/architecture/system-overview.md)
- [Architecture Decisions](docs/architecture/decisions.md)
- [Development Documentation](docs/development/README.md)

## Initial Technology Baseline

- Windows 11 + WSL2 Ubuntu
- Nuxt 4 + TypeScript
- .NET 10 / ASP.NET Core
- .NET Worker
- SQLite
- systemd
- GitHub CLI / Git
- Codex CLI
- Git worktree

## Repository Structure

FrontierEarthNeoと同様に、application / documentation / infrastructure / toolingをrootで分離します。

```text
AgentDispatcher/
├─ .agents/
├─ .github/
├─ apps/
├─ docs/
├─ infra/
└─ tools/
```

AgentDispatcher固有の設定・Execution履歴は保持しますが、GitHub Issue / PR / Review / CIの独自正本は作りません。
