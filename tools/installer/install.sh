#!/usr/bin/env bash
set -euo pipefail

APP_USER="agent-dispatcher"
APP_GROUP="agent-dispatcher"
WORKER_USER="agent-dispatcher-worker"
APP_DIR="/opt/agent-dispatcher"
DATA_DIR="/var/lib/agent-dispatcher"
WORKER_HOME="/var/lib/agent-dispatcher-worker"
API_PORT="5088"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
BUILD_USER="${SUDO_USER:-root}"

fail() {
  echo "エラー: $*" >&2
  exit 1
}

require_root() {
  [[ "${EUID}" -eq 0 ]] || fail "sudo を付けて実行してください。"
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "$1 が見つかりません。先に導入してください。"
}

check_environment() {
  require_command systemctl
  require_command sudo
  require_command useradd
  require_command groupadd
  require_command dotnet
  require_command node
  require_command npm
  require_command git
  require_command gh
  require_command codex
  require_command visudo

  grep -qi microsoft /proc/version 2>/dev/null ||
    echo "警告: WSL2を検出できませんでした。初期サポート対象はWSL2 Ubuntuです。"

  [[ "$(dotnet --version)" == 10.* ]] ||
    fail ".NET 10 SDK が必要です。現在: $(dotnet --version)"

  local node_major
  node_major="$(node -p 'process.versions.node.split(".")[0]')"
  (( node_major >= 24 )) ||
    fail "Node.js 24以上が必要です。現在: $(node --version)"

  systemctl --version >/dev/null
}

ensure_users() {
  getent group "$APP_GROUP" >/dev/null ||
    groupadd --system "$APP_GROUP"

  if ! id "$APP_USER" >/dev/null 2>&1; then
    useradd       --system       --gid "$APP_GROUP"       --home-dir "$DATA_DIR"       --shell /usr/sbin/nologin       "$APP_USER"
  fi

  if ! id "$WORKER_USER" >/dev/null 2>&1; then
    useradd       --system       --create-home       --home-dir "$WORKER_HOME"       --shell /bin/bash       "$WORKER_USER"
  fi

  usermod -a -G "$APP_GROUP" "$WORKER_USER"

  install -d -m 2770 -o "$APP_USER" -g "$APP_GROUP" "$DATA_DIR"
  install -d -m 0750 -o "$WORKER_USER" -g "$APP_GROUP" "$WORKER_HOME"
  install -d -m 0755 -o root -g "$APP_GROUP" "$APP_DIR"
}

run_as_build_user() {
  if [[ "$BUILD_USER" == "root" ]]; then
    "$@"
  else
    sudo --non-interactive --set-home --user "$BUILD_USER" -- "$@"
  fi
}

build_application() {
  local stage
  stage="$(mktemp -d)"
  chown "$BUILD_USER":"$(id -gn "$BUILD_USER")" "$stage"
  chmod 0775 "$stage"

  echo "Nuxt管理画面を構築しています..."
  (
    cd "$REPO_ROOT/apps/web/AgentDispatcher.Web"
    run_as_build_user npm install
    run_as_build_user npm run generate
  )

  echo ".NETアプリケーションを構築しています..."
  (
    cd "$REPO_ROOT"
    run_as_build_user dotnet publish       apps/backend/AgentDispatcher.Api/AgentDispatcher.Api.csproj       --configuration Release       --output "$stage/api"
    run_as_build_user dotnet publish       apps/worker/AgentDispatcher.Worker/AgentDispatcher.Worker.csproj       --configuration Release       --output "$stage/worker"
  )

  install -d "$stage/api/wwwroot"
  cp -a "$REPO_ROOT/apps/web/AgentDispatcher.Web/.output/public/." "$stage/api/wwwroot/"

  rm -rf "$APP_DIR/api" "$APP_DIR/worker"
  install -d -m 0755 "$APP_DIR/api" "$APP_DIR/worker"
  cp -a "$stage/api/." "$APP_DIR/api/"
  cp -a "$stage/worker/." "$APP_DIR/worker/"
  chown -R root:"$APP_GROUP" "$APP_DIR"
  chmod -R o-w "$APP_DIR"
  rm -rf "$stage"
}

install_sudoers() {
  local git_path gh_path codex_path systemctl_path
  git_path="$(command -v git)"
  gh_path="$(command -v gh)"
  codex_path="$(command -v codex)"
  systemctl_path="$(command -v systemctl)"

  cat > /etc/sudoers.d/agent-dispatcher <<EOF
Cmnd_Alias AGENT_DISPATCHER_WORKER_CLI = $git_path *, $gh_path *, $codex_path *
Cmnd_Alias AGENT_DISPATCHER_CANCEL = $systemctl_path stop agent-dispatcher-codex-*
$APP_USER ALL=($WORKER_USER) NOPASSWD: AGENT_DISPATCHER_WORKER_CLI
$APP_USER ALL=(root) NOPASSWD: AGENT_DISPATCHER_CANCEL
EOF

  chmod 0440 /etc/sudoers.d/agent-dispatcher
  visudo -cf /etc/sudoers.d/agent-dispatcher >/dev/null ||
    fail "sudoers設定の検証に失敗しました。"
}

render_service() {
  local source="$1"
  local destination="$2"

  sed     -e "s|@APP_DIR@|$APP_DIR|g"     -e "s|@DATA_DIR@|$DATA_DIR|g"     -e "s|@WORKER_USER@|$WORKER_USER|g"     -e "s|@WORKER_HOME@|$WORKER_HOME|g"     "$source" > "$destination"
  chmod 0644 "$destination"
}

install_services() {
  render_service     "$REPO_ROOT/infra/self-hosted/agent-dispatcher-api.service"     /etc/systemd/system/agent-dispatcher-api.service
  render_service     "$REPO_ROOT/infra/self-hosted/agent-dispatcher-worker.service"     /etc/systemd/system/agent-dispatcher-worker.service

  systemctl daemon-reload
  systemctl enable agent-dispatcher-api.service agent-dispatcher-worker.service
  systemctl restart agent-dispatcher-api.service agent-dispatcher-worker.service
}

install_tools() {
  install -d -m 0755 "$APP_DIR/tools"
  install -m 0755 "$REPO_ROOT/tools/installer/doctor.sh" "$APP_DIR/tools/doctor.sh"
}

print_next_steps() {
  cat <<EOF

AgentDispatcherを導入しました。
Web画面: http://localhost:$API_PORT

初回のみ、Codex実行用利用者でGitHubとChatGPTへログインしてください。

  sudo -iu $WORKER_USER
  gh auth login
  codex
  # Codexの案内に従ってChatGPTでログイン後、終了してください。
  exit

その後、状態確認を実行してください。

  sudo $APP_DIR/tools/doctor.sh

認証情報は $WORKER_HOME 以下に保存され、AgentDispatcherのDBには保存しません。
EOF
}

main() {
  require_root
  check_environment
  ensure_users
  build_application
  install_tools
  install_sudoers
  install_services
  print_next_steps
}

main "$@"
