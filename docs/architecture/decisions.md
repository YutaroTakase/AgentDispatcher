# Architecture Decisions

現在有効な主要Architecture Decisionだけを記載する。

## AD-001 — WSL2 Self-hostedを初期Runtimeとする

**Status:** Accepted

初期利用者はWindows開発PC上の個人開発者とし、WSL2 Ubuntuへsystemd serviceとして導入する。

クラウドControl Planeを初期必須にしない。

## AD-002 — FrontendはNuxt 4を採用する

**Status:** Accepted

Next.js、Nuxt、SvelteKitを候補とし、AgentDispatcherではNuxt 4を採用する。

理由:

- 管理画面主体でSSRを必須としない。
- SPA / static outputを選択できる。
- ASP.NET Core APIと明確に責務分離できる。
- self-hosted環境でVercel等の特定platformを前提にしない。
- TypeScript / Vueによる管理UIを小さく開始できる。

Production MVPではNuxt serverを常駐させず、SPA static assetsをASP.NET Coreから配信する構成を優先する。

## AD-003 — Backend / Workerは.NET 10とする

**Status:** Accepted

Control APIはASP.NET Core、Dispatcher / Scheduler / Cleanupは.NET Workerを基本とする。

Web request lifecycleと長時間Worker executionを分離する。

## AD-004 — SQLiteを初期Persistenceとする

**Status:** Accepted

単一Host、単一利用者、local-onlyをMVP前提とするためSQLiteを採用する。

Project設定とExecution metadataを保存する。

Full process logはDB肥大化を避けるためfile storageへ分離可能とする。

## AD-005 — GitHubをWork ItemのSource of Truthとする

**Status:** Accepted

AgentDispatcherはGitHub Issue / PR / Review / CIの独自正本を作らない。

Issue selectionに必要な情報はGitHubから取得し、Execution開始時のsnapshotのみ履歴目的で保存する。

## AD-006 — Codex CLIのChatGPT Account認証を利用する

**Status:** Accepted

MVPではOpenAI API keyをAgentDispatcherが保持して直接Model APIを呼び出す方式を採用しない。

専用Worker userにCodex CLIを導入し、ChatGPT accountで認証済みのCodex runtimeを起動する。

## AD-007 — Worker credentialをControl APIから分離する

**Status:** Accepted

Codex / GitHub credentialは専用Worker userのenvironmentで保持する。

Web/API identityはcredential fileへ直接アクセスしない。

Dispatcherは制御されたexecution requestをWorker identityへ渡す。

## AD-008 — 1 Issue 1 WorktreeをExecution isolationの基本単位とする

**Status:** Accepted

異なるIssueのCodex executionは同一working treeを共有しない。

同一Project / IssueにActive Executionは最大1件とする。

## AD-009 — Model RoutingはProject設定とする

**Status:** Accepted

Model名をAgentDispatcher Coreへ固定しない。

MVPではordered label rules + default routeでModel identifier / Reasoning Effortを決定する。

将来Model世代が変化してもProject設定変更だけで追従できる設計とする。

## AD-010 — Repository固有開発PolicyをDispatcherへ複製しない

**Status:** Accepted

AgentDispatcherはProject固有のbootstrap、coding style、validation、review policyを持たない。

CodexがRepository内のAGENTS.md等を読み取れることを前提とし、DispatcherはIssue identityとexecution contextだけを渡す。

## AD-011 — localhost-onlyをSecurity baselineとする

**Status:** Accepted

MVPはloopback accessを前提とする。

LAN / Internet公開時に必要なAuthentication、TLS、RBACは別Decisionで追加する。
