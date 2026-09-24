# WSLへの導入

## 対応環境

初期サポート対象はWindows 11上のWSL2 Ubuntuで、systemdが有効な環境とする。

導入前に次を利用可能にする。

- .NET 10 SDK
- Node.js 24以上
- npm
- Git
- GitHub CLI
- Codex CLI
- sudo
- systemd

GitHub CLIとCodex CLIの認証は導入スクリプトでは自動化しない。

## 導入

リポジトリのルートで次を実行する。

```bash
sudo ./tools/installer/install.sh
```

導入処理は次を行う。

- `agent-dispatcher` 管理用利用者を作成する。
- `agent-dispatcher-worker` Codex実行用利用者を作成する。
- Nuxt管理画面と.NETアプリケーションを構築する。
- `/opt/agent-dispatcher` へアプリケーションを配置する。
- `/var/lib/agent-dispatcher` へ永続データ領域を作成する。
- systemdサービスを登録・起動する。
- 管理用利用者から実行用利用者へ必要なCLIだけを起動できるsudo規則を登録する。

Web画面は初期状態で `http://localhost:5088` からのみ利用できる。

## 初回認証

Codex実行用利用者へ切り替える。

```bash
sudo -iu agent-dispatcher-worker
```

GitHubへログインする。

```bash
gh auth login
```

Codexを起動し、表示される案内に従ってChatGPTでログインする。

```bash
codex
```

ログイン後はCodexを終了し、シェルから抜ける。

認証情報は実行用利用者のHOMEに保持し、AgentDispatcherのSQLiteへ保存しない。

## 状態確認

```bash
sudo /opt/agent-dispatcher/tools/doctor.sh
```

## 更新

最新コードを取得した後に次を実行する。

```bash
sudo ./tools/installer/update.sh
```

## 削除

アプリケーションだけを削除し、データと認証情報を残す場合:

```bash
sudo ./tools/installer/uninstall.sh
```

データ、認証情報、専用利用者も削除する場合:

```bash
sudo ./tools/installer/uninstall.sh --purge
```
