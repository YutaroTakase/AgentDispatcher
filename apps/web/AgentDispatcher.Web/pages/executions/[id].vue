<script setup lang="ts">
import type { ExecutionDetail, ExecutionLogs } from '~/types/api'

const route = useRoute()
const { request } = useApi()
const id = computed(() => String(route.params.id))
const detail = ref<ExecutionDetail | null>(null)
const logs = ref<ExecutionLogs | null>(null)
const error = ref('')
const busy = ref(false)

const canCancel = computed(() =>
  detail.value && ['Queued', 'Preparing', 'Running'].includes(detail.value.execution.status))

async function load() {
  error.value = ''
  try {
    detail.value = await request<ExecutionDetail>(`/api/executions/${id.value}`)
    logs.value = await request<ExecutionLogs>(`/api/executions/${id.value}/logs`)
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '実行詳細を取得できませんでした。'
  }
}

async function cancel() {
  busy.value = true
  try {
    await request(`/api/executions/${id.value}/cancel`, { method: 'POST' })
    await load()
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '実行を取り消せませんでした。'
  }
  finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <section>
    <div class="page-header">
      <div>
        <h1>実行詳細</h1>
        <p v-if="detail" class="muted">#{{ detail.execution.issueNumber }} {{ detail.execution.issueTitle }}</p>
      </div>
      <button v-if="canCancel" class="button danger" :disabled="busy" @click="cancel">実行を取り消す</button>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>

    <template v-if="detail">
      <div class="grid cards">
        <div class="card"><div class="muted">状態</div><div><span class="status" :class="detail.execution.status">{{ detail.execution.status }}</span></div></div>
        <div class="card"><div class="muted">推論設定</div><div>{{ detail.execution.modelIdentifier }} / {{ detail.execution.reasoningEffort }}</div></div>
        <div class="card"><div class="muted">所要時間</div><div>{{ detail.durationSeconds == null ? '-' : Math.round(detail.durationSeconds) + '秒' }}</div></div>
      </div>

      <div class="panel" style="margin-top: 20px">
        <h2>結果</h2>
        <p v-if="detail.failureSummary" class="notice error">{{ detail.failureSummary }}</p>
        <pre>{{ detail.finalResult || '最終結果はまだありません。' }}</pre>
      </div>

      <div class="panel" style="margin-top: 20px">
        <h2>状態遷移</h2>
        <div class="table-wrap">
          <table>
            <thead><tr><th>日時</th><th>状態</th><th>詳細</th></tr></thead>
            <tbody>
              <tr v-for="event in detail.events" :key="event.id">
                <td>{{ new Date(event.occurredAt).toLocaleString() }}</td>
                <td>{{ event.status }}</td>
                <td>{{ event.detail }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div class="panel" style="margin-top: 20px">
        <h2>標準出力</h2>
        <pre>{{ logs?.standardOutput || 'ログはありません。' }}</pre>
        <h2>標準エラー出力</h2>
        <pre>{{ logs?.standardError || 'ログはありません。' }}</pre>
      </div>
    </template>
  </section>
</template>
