# Development Documentation

このDirectoryはAgentDispatcherの実装・検証ルールを扱う。

- Product requirement: [docs/product/product-requirements.md](../product/product-requirements.md)
- Architecture: [docs/architecture/README.md](../architecture/README.md)
- Runtime flow: [docs/architecture/system-overview.md](../architecture/system-overview.md)

Repository配置や採用技術はここへ重複記載しない。

## Initial Development Order

1. Application workspace skeleton
2. Project domain + persistence
3. GitHub repository / issue query integration
4. Routing Rule domain
5. Execution state machine
6. Worktree manager
7. Codex execution adapter
8. Background scan / dispatch
9. Execution history API
10. Management UI
11. Retention cleanup
12. WSL installer / systemd integration
13. Host health / restart recovery

## Implementation Rules

- Product固有要件をCoreへ混ぜない。
- External CLI command構築をDomainへ漏らさない。
- GitHub、Codex、Persistence、Process Runtimeをadapter境界で分離する。
- Execution state transitionをdurableに追跡する。
- stdout / stderrをFailure調査可能な形で保持する。
- DB schema変更はmigration可能にする。
- Web UIから任意shell commandを実行する機能を追加しない。

## Validation

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

Codex CLIの実認証を必要とするE2Eは通常のunit / integration testから分離する。
