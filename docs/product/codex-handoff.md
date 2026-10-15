# GitHub Issue から Codex への Handoff

## 1. 目的

AgentDispatcher は、GitHub 上で実行可能と判断済みの Issue を Codex へ安全に引き渡す実行基盤である。

プロジェクト固有の開発フロー、Issue の設計、Codex 委譲要否の判断、成果判定は AgentDispatcher の責務に含めない。

## 2. 実行対象の選択

プロジェクトは、通常の GitHub Issue search または GitHub Projects のどちらかを Issue 取得元として使用できる。

GitHub Projects を使用する場合は以下を設定する。

- GitHub Project owner
- GitHub Project number
- Projects filter query

Projects filter query は `gh project item-list --query` が受け付ける構文をそのまま保持し、AgentDispatcher 固有の Status 名や Field 値へ変換しない。

例:

```text
status:Ready label:codex is:issue is:open
```

取得した Project Item のうち、登録プロジェクトの GitHub Repository に属する Open Issue のみを実行候補として扱う。

## 3. Issue 単位の Codex Metadata

Issue 本文の先頭に以下の形式がある場合、AgentDispatcher はその実行設定を使用する。

```markdown
## Codex Metadata

Model: `<model>`
Reasoning level: `<reasoning-level>`
```

Metadata が存在する場合は、Project の推論モデル選択規則および既定値より優先する。

Metadata が存在しない場合は、従来どおり以下の順で実行設定を決定する。

1. 最初に一致する Project routing rule
2. Project default route

Metadata の見出しが存在するが形式が不正な場合は、安全のため Codex を起動しない。Issue の選定結果に不正理由を残し、Issue 本文を AgentDispatcher が自動修正することはしない。

## 4. 実行時に固定する情報

実行待ちを作成した時点で、最終的に選択した以下を execution に固定して保存する。

- Repository
- Issue number
- Issue title
- Issue labels
- Model
- Reasoning level
- 起動方法

以後 Project 設定や Issue Metadata が変更されても、その execution の実行設定は変更しない。

## 5. AgentDispatcher の責務境界

AgentDispatcher は以下を担当する。

- Issue 候補取得
- GitHub Projects filter の適用
- Issue Metadata の解析と検証
- 実行設定の決定
- 同一 Issue の二重実行防止
- 最大同時実行数の制御
- Issue 専用 worktree の準備
- Codex の起動・取消
- 実行状態、ログ、終了結果の保存

AgentDispatcher は以下を担当しない。

- Codex を使用すべきかの判断
- Issue の仕様・設計
- GitHub Project Field の変更
- Issue / PR の Review
- 成果の Acceptance
- PR Merge
- Issue の Done / Close
- プロジェクト固有の Worker / Reviewer orchestration

GitHub 上の Workflow 状態変更が必要な場合は、対象 Repository の指示文書と Issue を正本として Codex または上位 Worker が行う。

## 6. Private consumer projects

非公開プロジェクト固有の名称、Repository、Project 名、Status 名、Worker 名、Issue Type、Review 規則は AgentDispatcher 側へ記載・ハードコードしない。

private consumer project 側で Codex 対象 Issue を設計・レビューし、実行可能になった Issue だけを Project filter により AgentDispatcher へ渡す。
