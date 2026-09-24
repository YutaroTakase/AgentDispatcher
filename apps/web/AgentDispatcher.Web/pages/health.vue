<script setup lang="ts">
import type {
  CleanupRun,
  HostHealthReport,
  ProjectHealthReport,
} from '~/types/api'

const { request } = useApi()
const host = ref<HostHealthReport | null>(null)
const projects = ref<ProjectHealthReport[]>([])
const cleanupRuns = ref<CleanupRun[]>([])
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [hostReport, projectReports, cleanup] = await Promise.all([
      request<HostHealthReport>('/api/health/host', {
        ignoreResponseError: true,
      }),
      request<ProjectHealthReport[]>('/api/health/projects'),
      request<CleanupRun[]>('/api/maintenance/cleanup-runs?limit=10'),
    ])
    host.value = hostReport
    projects.value = projectReports
    cleanupRuns.value = cleanup
  }
  catch (cause) {
    error.value = cause instanceof Error
      ? cause.message
      : '実行環境の状態を取得できませんでした。'
  }
  finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header">
      <div>
        <h1>実行環境</h1>
        <p class="muted">GitHub、Codex、systemd、保存領域と各プロジェクトの状態を確認します。</p>
      </div>
      <button class="button secondary" :disabled="loading" @click="load">
        再確認
      </button>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>

    <div class="panel">
      <h2>ホスト</h2>
      <div v-if="host" class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>項目</th>
              <th>状態</th>
              <th>詳細</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="check in host.checks" :key="check.key">
              <td>{{ check.name }}</td>
              <td>
                <span
                  class="status"
                  :class="check.state === 'Healthy' ? 'Succeeded' : check.state === 'Unhealthy' ? 'Failed' : 'Preparing'"
                >
                  {{ check.state }}
                </span>
              </td>
              <td>{{ check.detail }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-else class="muted">確認中です。</p>
    </div>

    <div class="panel" style="margin-top: 20px">
      <h2>プロジェクト</h2>
      <div v-for="project in projects" :key="project.projectId" class="rule">
        <h3>{{ project.projectName }}</h3>
        <p class="muted">{{ project.repository }} / 作業ツリー {{ project.worktreeCount }}件</p>
        <div class="table-wrap">
          <table>
            <tbody>
              <tr v-for="check in project.checks" :key="check.key">
                <td>{{ check.name }}</td>
                <td>
                  <span
                    class="status"
                    :class="check.state === 'Healthy' ? 'Succeeded' : check.state === 'Unhealthy' ? 'Failed' : 'Preparing'"
                  >
                    {{ check.state }}
                  </span>
                </td>
                <td>{{ check.detail }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <p v-if="!projects.length" class="muted">登録済みプロジェクトはありません。</p>
    </div>

    <div class="panel" style="margin-top: 20px">
      <h2>直近の整理処理</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>終了日時</th>
              <th>作業ツリー</th>
              <th>実行履歴</th>
              <th>エラー</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="run in cleanupRuns" :key="run.id">
              <td>{{ new Date(run.finishedAt).toLocaleString() }}</td>
              <td>{{ run.worktreesRemoved }}</td>
              <td>{{ run.executionsDeleted }}</td>
              <td>{{ run.errorCount }}</td>
            </tr>
            <tr v-if="!cleanupRuns.length">
              <td colspan="4" class="muted">整理履歴はありません。</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
