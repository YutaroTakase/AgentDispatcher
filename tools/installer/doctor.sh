#!/usr/bin/env bash
set -u

WORKER_USER="agent-dispatcher-worker"
DATA_DIR="/var/lib/agent-dispatcher"
FAILED=0

check() {
  local name="$1"
  shift
  if "$@" >/dev/null 2>&1; then
    printf "OK   %s\n" "$name"
  else
    printf "NG   %s\n" "$name"
    FAILED=1
  fi
}

check "systemd" systemctl --version
check "APIサービス" systemctl is-active --quiet agent-dispatcher-api.service
check "常駐サービス" systemctl is-active --quiet agent-dispatcher-worker.service
check "Git" sudo --non-interactive --set-home --user "$WORKER_USER" -- git --version
check "GitHub認証" sudo --non-interactive --set-home --user "$WORKER_USER" -- gh auth status
check "Codex" sudo --non-interactive --set-home --user "$WORKER_USER" -- codex --version
check "Codex認証" sudo --non-interactive --set-home --user "$WORKER_USER" -- codex login status
check "データ領域" test -w "$DATA_DIR"

if command -v curl >/dev/null 2>&1; then
  check "Web API" curl --fail --silent http://127.0.0.1:5088/health
else
  echo "SKIP Web API (curlがありません)"
fi

exit "$FAILED"
