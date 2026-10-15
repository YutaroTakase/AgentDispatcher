# Codex Handoff Architecture

## 1. 論理フロー

```text
GitHub Issue / Project
        │
        │ candidate query
        ▼
GitHubCliClient
        │
        ├─ Repository issue search
        └─ GitHub Project item-list + Projects filter
        │
        ▼
IssueCandidate
  - number
  - title
  - labels
  - body
  - url
        │
        ▼
CodexMetadataParser
        │
        ├─ Valid     → Issue-level ExecutionRoute
        ├─ NotPresent→ Project routing
        └─ Invalid   → dispatch rejected
        │
        ▼
DispatchCoordinator
        │
        ▼
ExecutionRepository
  model / reasoning を execution 作成時に固定
        │
        ▼
Git worktree
        │
        ▼
CodexCliRunner
```

## 2. Issue 取得境界

`IGitHubIssueSource` は、実行候補に必要な GitHub 情報を Domain へ渡す境界とする。

GitHub Project が設定されていない場合は従来どおり repository issue search を使用する。

GitHub Project が設定されている場合は `gh project item-list` に Projects filter query を渡し、返された Project Item から対象 repository の Issue number を抽出する。その後 `gh issue view` で最新の本文、Label、Open 状態を取得する。

これにより GitHub Project の Field 定義を AgentDispatcher Domain へ複製せず、Project filter syntax を GitHub 側の正本として扱う。

## 3. Routing 優先順位

ExecutionRoute は以下の優先順位で確定する。

```text
Issue Codex Metadata
        ↓ ない場合
Project routing rule
        ↓ 一致しない場合
Project default route
```

Issue Metadata は Issue 設計時に上位 Worker が確定した実行指示であるため最優先とする。

Metadata の見出しが存在するにもかかわらず Model / Reasoning level が欠落または不正な場合、fallback して実行しない。設定ミスを隠さず dispatch を拒否する。

## 4. 正本

| 情報 | 正本 |
| --- | --- |
| Issue / Label / Project Field / Workflow 状態 | GitHub |
| Project selector / fallback routing | AgentDispatcher |
| Issue-level Model / Reasoning level | GitHub Issue Codex Metadata |
| execution に採用済みの Model / Reasoning level | AgentDispatcher execution record |
| Repository 内容 | Git remote |
| Codex 実行状態・ログ | AgentDispatcher |

## 5. 状態変更

AgentDispatcher の dispatch 処理は GitHub Issue / Project の Workflow 状態を変更しない。

Codex は対象 Repository 内の指示文書と Issue を読み、必要な GitHub 操作をその Repository のルールに従って行う。

この分離により、AgentDispatcher は特定 Repository の `Ready`、`In Progress`、`Review` 等の意味を理解せずに再利用できる。

## 6. 障害時

- GitHub Project の取得に失敗した場合: 新規 execution を作成しない。
- Project Item から対象 Issue の最新情報を取得できない場合: scan を失敗として扱う。
- Codex Metadata が不正な場合: 該当 Issue の dispatch だけを拒否し、他候補の処理は継続する。
- 同一 Issue が既に active execution を持つ場合: 既存の重複実行防止を適用する。
- Project の最大同時実行数に達した場合: 既存の concurrency guard を適用する。
