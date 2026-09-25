# 導入試験手順

## 1. 目的

AgentDispatcherを対応するWSL2 Ubuntuへ新規導入し、Web画面、管理API、常駐処理、GitHub認証、ChatGPT認証済みCodex CLI、systemd、保存領域が実環境で利用できることを確認する。

この試験では既存の開発用環境に依存しない状態から導入することを推奨する。

## 2. 試験結果に記録する情報

試験開始前に次を記録する。

```text
実施日:
実施者:
AgentDispatcher commit:
Windows version:
WSL version:
Ubuntu version:
.NET SDK:
Node.js:
npm:
Git:
GitHub CLI:
Codex CLI:
```

対象コミットは次で確認する。

```bash
git rev-parse HEAD
```

## 3. 前提条件

必須条件は次のとおり。

- Windows 11上のWSL2 Ubuntu
- WSL内でsystemdが有効
- インターネットへ接続可能
- .NET 10 SDK
- Node.js 24以上とnpm
- Git
- GitHub CLI
- Codex CLI
- sudoとvisudo
- 試験に使用できるGitHubアカウント
- Codexを利用できるChatGPTアカウント

Codex CLIはChatGPTアカウントで利用できる。初回起動時の案内に従ってChatGPTへサインインする。認証状態は `codex login status` で確認する。

## 4. 環境確認

WSL上で次を実行し、前提条件を記録する。

```bash
cat /etc/os-release
uname -a
systemctl --version
dotnet --version
node --version
npm --version
git --version
gh --version
codex --version
```

### 合格条件

- Ubuntuとして起動している。
- `systemctl --version` が成功する。
- .NETの主版が10である。
- Node.jsの主版が24以上である。
- Git、GitHub CLI、Codex CLIを起動できる。

## 5. リポジトリ取得

試験用の任意ディレクトリで実施する。

```bash
git clone https://github.com/YutaroTakase/AgentDispatcher.git
cd AgentDispatcher
git checkout main
git pull --ff-only
git rev-parse HEAD
```

試験対象コミットが意図したコミットと一致することを確認する。

## 6. 新規導入

```bash
sudo ./tools/installer/install.sh
```

導入スクリプトはアプリケーション用利用者 `agent-dispatcher`、Codex実行用利用者 `agent-dispatcher-worker`、アプリケーション、データ領域、systemdサービスを構成する。

### 合格条件

- スクリプトが終了コード0で終了する。
- `/opt/agent-dispatcher` が作成される。
- `/var/lib/agent-dispatcher` が作成される。
- `agent-dispatcher-api.service` が有効化される。
- `agent-dispatcher-worker.service` が有効化される。
- WebプロセスとCodex実行利用者が分離されている。

確認例:

```bash
id agent-dispatcher
id agent-dispatcher-worker
systemctl is-enabled agent-dispatcher-api.service
systemctl is-enabled agent-dispatcher-worker.service
systemctl is-active agent-dispatcher-api.service
systemctl is-active agent-dispatcher-worker.service
```

## 7. 専用実行利用者の認証

Codex実行用利用者へ切り替える。

```bash
sudo -iu agent-dispatcher-worker
```

GitHubへログインする。

```bash
gh auth login
gh auth status
```

Codexを起動し、表示される案内に従ってChatGPTアカウントでサインインする。

```bash
codex
```

ログイン完了後にCodexを終了し、認証状態を確認する。

```bash
codex login status
exit
```

### 合格条件

- `gh auth status` が成功する。
- `codex login status` がログイン済みとして成功する。
- AgentDispatcherのSQLiteへGitHubやChatGPTの認証情報を保存していない。

## 8. 導入後の状態確認

```bash
sudo /opt/agent-dispatcher/tools/doctor.sh
```

### 合格条件

次がすべて `OK` になる。

- systemd
- APIサービス
- 常駐サービス
- Git
- GitHub認証
- Codex
- Codex認証
- データ領域
- curlが存在する場合はWeb API

失敗した場合は次も保存する。

```bash
systemctl status agent-dispatcher-api.service --no-pager
systemctl status agent-dispatcher-worker.service --no-pager
journalctl -u agent-dispatcher-api.service -n 200 --no-pager
journalctl -u agent-dispatcher-worker.service -n 200 --no-pager
```

## 9. Web画面確認

Windows側のブラウザで次を開く。

```text
http://localhost:5088
```

### 合格条件

- AgentDispatcherの管理画面が表示される。
- 「概要」「プロジェクト」「実行履歴」「実行環境」へ移動できる。
- 「実行環境」でGit、GitHub、Codex、systemd、保存領域の状態を確認できる。
- Web画面は意図せずLAN向けに公開されていない。

## 10. サービス再起動確認

```bash
sudo systemctl restart agent-dispatcher-api.service
sudo systemctl restart agent-dispatcher-worker.service
sudo /opt/agent-dispatcher/tools/doctor.sh
```

### 合格条件

- 再起動後も両サービスがactiveになる。
- Web画面へ再度アクセスできる。
- 既存の設定と実行履歴が保持される。

## 11. 更新試験

リポジトリを最新の試験対象へ更新した後、次を実行する。

```bash
git pull --ff-only
sudo ./tools/installer/update.sh
sudo /opt/agent-dispatcher/tools/doctor.sh
```

### 合格条件

- 更新処理が終了コード0で終了する。
- サービスが正常に再起動する。
- 既存データと専用実行利用者の認証情報が保持される。

## 12. 削除試験

削除試験はE2E完了後に実施する。

データを保持して本体だけ削除する場合:

```bash
sudo ./tools/installer/uninstall.sh
```

### 合格条件

- systemdサービスが停止・無効化される。
- `/opt/agent-dispatcher` が削除される。
- `/var/lib/agent-dispatcher` と専用実行利用者のホームは保持される。

完全削除を確認する場合:

```bash
sudo ./tools/installer/uninstall.sh --purge
```

### 合格条件

- アプリケーション、データ、専用利用者が削除される。
- 再度新規導入できる。

## 13. 導入試験の最終判定

次をすべて満たした場合に導入試験を合格とする。

- 新規導入が成功した。
- GitHubとCodexの認証が専用実行利用者で成功した。
- `doctor.sh` が成功した。
- Windowsブラウザからlocalhostの管理画面へアクセスできた。
- systemd再起動後も正常に復旧した。
- 更新処理でデータと認証情報が保持された。
