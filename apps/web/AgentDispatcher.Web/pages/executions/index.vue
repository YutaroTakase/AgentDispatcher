<script setup lang="ts">
import type { ExecutionSummary, Project } from '~/types/api'

const { request } = useApi()
const executions = ref<ExecutionSummary[]>([])
const projects = ref<Project[]>([])
const error = ref('')
const filters = reactive({
  projectId: '',
  status: '',
  model: '',
  trigger: '',
  from: '',
  to: '',
})

function buildQuery() {
  const params = new URLSearchParams({ limit: '200' })
  for (const [key, value] of Object.entries(filters)) {
    if (value) params.set(key, value)
  }
  return params.toString()
}

async function load() {
  error.value = ''
  try {
    const [history, projectItems] = await Promise.all([
      request<ExecutionSummary[]>(`/api/executions?${buildQuery()}`),
      request<Project[]>('/api/projects'),
    ])
    executions.value = history
    projects.value = projectItems
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '実行履歴を取得できませんでした。'
  }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header">
      <div>
        <h1>実行履歴</h1>
        <p class="muted">プロジェクト横断で実行結果を確認します。</p>
      </div>
      <button class="button secondary" @click="load">更新</button>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>

    <div class="panel">
      <div class="form-grid">
        <div class="field">
          <label>プロジェクト</label>
          <select v-model="filters.projectId">
            <option value="">すべて</option>
            <option v-for="project in projects" :key="project.id" :value="project.id">{{ project.displayName }}</option>
          </select>
        </div>
        <div class="field">
          <label>状態</label>
          <select v-model="filters.status">
            <option value="">すべて</option>
            <option v-for="status in ['Queued','Preparing','Running','Succeeded','Failed','Canceled']" :key="status">{{ status }}</option>
          </select>
        </div>
        <div class="field"><label>推論モデル</label><input v-model="filters.model"></div>
        <div class="field">
          <label>起動方法</label>
          <select v-model="filters.trigger"><option value="">すべて</option><option value="Scheduled">自動</option><option value="Manual">手動</option></select>
        </div>
        <div class="field"><label>開始日</label><input v-model="filters.from" type="datetime-local"></div>
        <div class="field"><label>終了日</label><input v-model="filters.to" type="datetime-local"></div>
      </div>
      <div class="actions"><button class="button" @click="load">絞り込む</button></div>
    </div>

    <div class="panel" style="margin-top: 20px">
      <div class="table-wrap">
        <table>
          <thead><tr><th>プロジェクト</th><th>Issue</th><th>状態</th><th>起動</th><th>推論設定</th><th>作成日時</th></tr></thead>
          <tbody>
            <tr v-for="item in executions" :key="item.id">
              <td>{{ item.projectName ?? item.projectId }}</td>
              <td><NuxtLink :to="`/executions/${item.id}`">#{{ item.issueNumber }} {{ item.issueTitle }}</NuxtLink></td>
              <td><span class="status" :class="item.status">{{ item.status }}</span></td>
              <td>{{ item.trigger }}</td>
              <td>{{ item.modelIdentifier }} / {{ item.reasoningEffort }}</td>
              <td>{{ new Date(item.createdAt).toLocaleString() }}</td>
            </tr>
            <tr v-if="!executions.length"><td colspan="6" class="muted">該当する履歴はありません。</td></tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
