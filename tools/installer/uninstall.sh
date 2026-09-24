#!/usr/bin/env bash
set -euo pipefail

APP_USER="agent-dispatcher"
APP_GROUP="agent-dispatcher"
WORKER_USER="agent-dispatcher-worker"
APP_DIR="/opt/agent-dispatcher"
DATA_DIR="/var/lib/agent-dispatcher"
WORKER_HOME="/var/lib/agent-dispatcher-worker"
PURGE=false

if [[ "${1:-}" == "--purge" ]]; then
  PURGE=true
fi

[[ "${EUID}" -eq 0 ]] || {
  echo "エラー: sudo を付けて実行してください。" >&2
  exit 1
}

systemctl disable --now agent-dispatcher-worker.service agent-dispatcher-api.service 2>/dev/null || true
rm -f   /etc/systemd/system/agent-dispatcher-worker.service   /etc/systemd/system/agent-dispatcher-api.service   /etc/sudoers.d/agent-dispatcher
systemctl daemon-reload

rm -rf "$APP_DIR"

if [[ "$PURGE" == true ]]; then
  rm -rf "$DATA_DIR" "$WORKER_HOME"
  userdel "$WORKER_USER" 2>/dev/null || true
  userdel "$APP_USER" 2>/dev/null || true
  groupdel "$APP_GROUP" 2>/dev/null || true
  echo "AgentDispatcher本体、データ、専用利用者を削除しました。"
else
  echo "AgentDispatcher本体を削除しました。データと認証情報は保持しています。"
  echo "完全削除する場合: sudo $0 --purge"
fi
