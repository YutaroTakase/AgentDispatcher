<script setup lang="ts">
import type { ExecutionSummary, Project } from '~/types/api'

const { request } = useApi()
const projects = ref<Project[]>([])
const executions = ref<ExecutionSummary[]>([])
const loading = ref(true)
const error = ref('')

const runningCount = computed(() =>
  executions.value.filter(item => ['Queued', 'Preparing', 'Running'].includes(item.status)).length)
const failedCount = computed(() =>
  executions.value.filter(item => item.status === 'Failed').length)

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [projectItems, executionItems] = await Promise.all([
      request<Project[]>('/api/projects'),
      request<ExecutionSummary[]>('/api/executions?limit=10'),
    ])
    projects.value = projectItems
    executions.value = executionItems
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '概要を取得できませんでした。'
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
        <h1>概要</h1>
        <p class="muted">プロジェクトと直近の実行状況を確認します。</p>
      </div>
      <button class="button secondary" :disabled="loading" @click="load">
        更新
      </button>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>

    <div class="grid cards">
      <div class="card">
        <div class="muted">プロジェクト</div>
        <div class="metric">{{ projects.length }}</div>
      </div>
      <div class="card">
        <div class="muted">実行中・待機中</div>
        <div class="metric">{{ runningCount }}</div>
      </div>
      <div class="card">
        <div class="muted">直近の失敗</div>
        <div class="metric">{{ failedCount }}</div>
      </div>
    </div>

    <div class="panel" style="margin-top: 20px">
      <h2>直近の実行</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>プロジェクト</th>
              <th>Issue</th>
              <th>状態</th>
              <th>推論モデル</th>
              <th>作成日時</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in executions" :key="item.id">
              <td>{{ item.projectName ?? item.projectId }}</td>
              <td>
                <NuxtLink :to="`/executions/${item.id}`">
                  #{{ item.issueNumber }} {{ item.issueTitle }}
                </NuxtLink>
              </td>
              <td><span class="status" :class="item.status">{{ item.status }}</span></td>
              <td>{{ item.modelIdentifier }} / {{ item.reasoningEffort }}</td>
              <td>{{ new Date(item.createdAt).toLocaleString() }}</td>
            </tr>
            <tr v-if="!executions.length">
              <td colspan="5" class="muted">実行履歴はありません。</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
