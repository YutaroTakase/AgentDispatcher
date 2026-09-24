# Architecture Decisions

この文書は主要な技術判断と採用理由だけを記録する。

現在の構成そのものは[Architecture Baseline](README.md)を正本とする。

## AD-001 — WSL2 Self-hostedを初期Runtimeとする

**Status:** Accepted

初期利用者をWindows開発PC上の個人開発者とし、クラウドControl Planeを必須にせず低コストで導入できることを優先する。

## AD-002 — FrontendはNuxt 4を採用する

**Status:** Accepted

Next.js、Nuxt、SvelteKitを比較し、管理画面主体でSSRを必須とせず、SPA / static outputを選択でき、ASP.NET Core APIと責務分離しやすいNuxt 4を採用する。

特定クラウドFrontend platformへの依存を初期要件にしない。

## AD-003 — Backend / Workerは.NET 10とする

**Status:** Accepted

Control APIと長時間Dispatcher処理を同一言語・runtimeで実装しつつ、Web request lifecycleとWorker execution lifecycleはprocess責務として分離する。

## AD-004 — SQLiteを初期Persistenceとする

**Status:** Accepted

単一Host、単一利用者、local-onlyをMVP前提とするため、外部DB serviceを必須にしない。

将来のmulti-host化を妨げないようPersistence境界は分離する。

## AD-005 — GitHubをWork ItemのSource of Truthとする

**Status:** Accepted

Issue / PR / Review / CIをAgentDispatcherへ複製して独自正本を作ると同期問題が生じるため、GitHubを正本としExecutionに必要なsnapshotだけを履歴として保持する。

## AD-006 — ChatGPT Account認証済みCodex CLIを初期Execution Runtimeとする

**Status:** Accepted

MVPではAgentDispatcher自身がOpenAI API keyを保持してModel APIを直接呼び出す方式を採用せず、既存のCodex CLI認証環境を利用する。

Execution Runtimeは将来交換可能な境界にする。

## AD-007 — Worker credentialをControl Planeから分離する

**Status:** Accepted

Codex / GitHub credentialへのWeb processからの直接アクセスを避けるため、専用Worker identityで保持・実行する。

## AD-008 — 1 Issue 1 WorktreeをExecution isolationの基本単位とする

**Status:** Accepted

異なるIssueの同時実行によるworking tree競合を避けつつ、Git repository objectを共有して軽量に並列化するためGit worktreeを採用する。

## AD-009 — Model RoutingをProject設定とする

**Status:** Accepted

Model世代やProject特性の変更へCore code変更なしで追従できるよう、Model identifierとReasoning EffortをProject設定として扱う。

## AD-010 — Repository固有PolicyをDispatcherへ複製しない

**Status:** Accepted

Projectごとのcoding rule、validation、bootstrapをDispatcherが保持すると二重管理になるため、対象Repository内のinstruction sourceを正本とする。

## AD-011 — localhost-onlyを初期Security baselineとする

**Status:** Accepted

MVPで不要なAuthentication / TLS / RBACの複雑性を持ち込まず、外部公開は別Architecture Decisionとして扱う。
