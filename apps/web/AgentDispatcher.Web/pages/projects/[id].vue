<script setup lang="ts">
import type {
  DispatchDecision,
  DispatchResult,
  ExecutionRoute,
  IssueSelector,
  Project,
  ProjectRequest,
  RoutingRule,
} from '~/types/api'

interface RuleEditor extends RoutingRule {
  requiredText: string
  excludedText: string
}

const route = useRoute()
const router = useRouter()
const { request } = useApi()
const id = computed(() => String(route.params.id))
const project = ref<Project | null>(null)
const form = reactive<ProjectRequest>({
  displayName: '',
  repository: '',
  enabled: true,
  defaultBranch: 'main',
  issueScanIntervalMinutes: 5,
  maxConcurrentExecutions: 1,
  executionRetentionDays: 30,
  failureWorktreeRetentionDays: 7,
  issueQueryFragment: '',
  issueSortField: 'updated',
  issueSortOrder: 'asc',
  maxCandidatesPerScan: 20,
  defaultModelIdentifier: '',
  defaultReasoningEffort: 'medium',
})
const rules = ref<RuleEditor[]>([])
const manualIssueNumber = ref<number | null>(null)
const message = ref('')
const error = ref('')
const busy = ref(false)

function makeRule(rule?: RoutingRule): RuleEditor {
  return {
    id: rule?.id,
    order: rule?.order ?? rules.value.length * 10,
    enabled: rule?.enabled ?? true,
    requiredLabels: rule?.requiredLabels ?? [],
    excludedLabels: rule?.excludedLabels ?? [],
    modelIdentifier: rule?.modelIdentifier ?? form.defaultModelIdentifier,
    reasoningEffort: rule?.reasoningEffort ?? form.defaultReasoningEffort,
    requiredText: (rule?.requiredLabels ?? []).join(', '),
    excludedText: (rule?.excludedLabels ?? []).join(', '),
  }
}

async function load() {
  error.value = ''
  const [projectValue, selector, defaultRoute, routingRules] = await Promise.all([
    request<Project>(`/api/projects/${id.value}`),
    request<IssueSelector>(`/api/projects/${id.value}/issue-selector`),
    request<ExecutionRoute>(`/api/projects/${id.value}/routing/default`),
    request<RoutingRule[]>(`/api/projects/${id.value}/routing/rules`),
  ])

  project.value = projectValue
  Object.assign(form, {
    displayName: projectValue.displayName,
    repository: projectValue.repository,
    enabled: projectValue.enabled,
    defaultBranch: projectValue.defaultBranch,
    issueScanIntervalMinutes: projectValue.issueScanIntervalMinutes,
    maxConcurrentExecutions: projectValue.maxConcurrentExecutions,
    executionRetentionDays: projectValue.executionRetentionDays,
    failureWorktreeRetentionDays: projectValue.failureWorktreeRetentionDays,
    issueQueryFragment: selector.queryFragment,
    issueSortField: selector.sortField,
    issueSortOrder: selector.sortOrder,
    maxCandidatesPerScan: selector.maxCandidatesPerScan,
    defaultModelIdentifier: defaultRoute.modelIdentifier,
    defaultReasoningEffort: defaultRoute.reasoningEffort,
  })
  rules.value = routingRules.map(makeRule)
}

async function saveProject() {
  busy.value = true
  error.value = ''
  message.value = ''
  try {
    project.value = await request<Project>(`/api/projects/${id.value}`, {
      method: 'PUT',
      body: form,
    })
    message.value = 'プロジェクト設定を保存しました。'
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '設定を保存できませんでした。'
  }
  finally {
    busy.value = false
  }
}

function splitLabels(value: string) {
  return value
    .split(',')
    .map(item => item.trim())
    .filter(Boolean)
}

async function saveRules() {
  busy.value = true
  error.value = ''
  message.value = ''
  try {
    const body = rules.value.map(rule => ({
      id: rule.id,
      order: rule.order,
      enabled: rule.enabled,
      requiredLabels: splitLabels(rule.requiredText),
      excludedLabels: splitLabels(rule.excludedText),
      modelIdentifier: rule.modelIdentifier,
      reasoningEffort: rule.reasoningEffort,
    }))
    const saved = await request<RoutingRule[]>(`/api/projects/${id.value}/routing/rules`, {
      method: 'PUT',
      body,
    })
    rules.value = saved.map(makeRule)
    message.value = '推論モデル選択規則を保存しました。'
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '選択規則を保存できませんでした。'
  }
  finally {
    busy.value = false
  }
}

async function scanNow() {
  busy.value = true
  error.value = ''
  try {
    const result = await request<DispatchResult>(`/api/projects/${id.value}/scan`, {
      method: 'POST',
    })
    message.value = result.decisions.map(item => item.detail).join(' / ') || '対象Issueはありませんでした。'
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'Issue確認に失敗しました。'
  }
  finally {
    busy.value = false
  }
}

async function dispatchIssue() {
  if (!manualIssueNumber.value) return
  busy.value = true
  error.value = ''
  try {
    const result = await request<DispatchDecision>(
      `/api/projects/${id.value}/issues/${manualIssueNumber.value}/dispatch`,
      { method: 'POST' },
    )
    message.value = result.detail
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : '実行を開始できませんでした。'
  }
  finally {
    busy.value = false
  }
}

async function removeProject() {
  if (!window.confirm('このプロジェクトをAgentDispatcherから削除しますか？')) return
  await request(`/api/projects/${id.value}`, { method: 'DELETE' })
  await router.push('/projects')
}

onMounted(() => {
  load().catch((cause: unknown) => {
    error.value = cause instanceof Error ? cause.message : 'プロジェクトを取得できませんでした。'
  })
})
</script>

<template>
  <section>
    <div class="page-header">
      <div>
        <h1>{{ project?.displayName ?? 'プロジェクト' }}</h1>
        <p class="muted">{{ project?.repository }}</p>
      </div>
      <div class="actions">
        <button class="button secondary" :disabled="busy" @click="scanNow">今すぐIssue確認</button>
        <button class="button danger" :disabled="busy" @click="removeProject">削除</button>
      </div>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>
    <div v-if="message" class="notice">{{ message }}</div>

    <div class="panel">
      <h2>基本設定</h2>
      <form class="form-grid" @submit.prevent="saveProject">
        <div class="field"><label>表示名</label><input v-model="form.displayName" required></div>
        <div class="field"><label>GitHubリポジトリ</label><input v-model="form.repository" required></div>
        <div class="field"><label>既定ブランチ</label><input v-model="form.defaultBranch" required></div>
        <div class="field"><label>Issue確認間隔（分）</label><input v-model.number="form.issueScanIntervalMinutes" type="number" min="1"></div>
        <div class="field"><label>最大同時実行数</label><input v-model.number="form.maxConcurrentExecutions" type="number" min="1"></div>
        <div class="field"><label>最大候補数</label><input v-model.number="form.maxCandidatesPerScan" type="number" min="1" max="100"></div>
        <div class="field full"><label>Issue検索条件</label><input v-model="form.issueQueryFragment"></div>
        <div class="field">
          <label>並び替え</label>
          <select v-model="form.issueSortField">
            <option value="updated">更新日時</option>
            <option value="created">作成日時</option>
            <option value="comments">コメント数</option>
            <option value="interactions">反応数</option>
            <option value="reactions">リアクション数</option>
          </select>
        </div>
        <div class="field">
          <label>並び順</label>
          <select v-model="form.issueSortOrder"><option value="asc">昇順</option><option value="desc">降順</option></select>
        </div>
        <div class="field"><label>既定の推論モデル</label><input v-model="form.defaultModelIdentifier" required></div>
        <div class="field"><label>既定の推論レベル</label><input v-model="form.defaultReasoningEffort" required></div>
        <div class="field"><label>実行履歴保存日数</label><input v-model.number="form.executionRetentionDays" type="number" min="1"></div>
        <div class="field"><label>失敗時作業ツリー保存日数</label><input v-model.number="form.failureWorktreeRetentionDays" type="number" min="0"></div>
        <div class="field full">
          <label><input v-model="form.enabled" type="checkbox" style="width: auto"> 自動実行を有効にする</label>
        </div>
        <div class="actions field full"><button class="button" :disabled="busy">保存</button></div>
      </form>
    </div>

    <div class="panel" style="margin-top: 20px">
      <div class="page-header">
        <div>
          <h2>推論モデル選択規則</h2>
          <p class="muted">上から評価し、最初に一致した規則を使用します。</p>
        </div>
        <button class="button secondary" @click="rules.push(makeRule())">規則を追加</button>
      </div>

      <div v-for="(rule, index) in rules" :key="rule.id ?? index" class="rule">
        <div class="form-grid">
          <div class="field"><label>評価順</label><input v-model.number="rule.order" type="number" min="0"></div>
          <div class="field"><label>推論モデル</label><input v-model="rule.modelIdentifier"></div>
          <div class="field"><label>推論レベル</label><input v-model="rule.reasoningEffort"></div>
          <div class="field"><label>必須ラベル（カンマ区切り）</label><input v-model="rule.requiredText"></div>
          <div class="field"><label>除外ラベル（カンマ区切り）</label><input v-model="rule.excludedText"></div>
          <div class="field">
            <label><input v-model="rule.enabled" type="checkbox" style="width: auto"> 有効</label>
          </div>
        </div>
        <div class="actions">
          <button class="button danger" type="button" @click="rules.splice(index, 1)">削除</button>
        </div>
      </div>
      <div class="actions">
        <button class="button" :disabled="busy" @click="saveRules">選択規則を保存</button>
      </div>
    </div>

    <div class="panel" style="margin-top: 20px">
      <h2>手動実行</h2>
      <div class="form-grid">
        <div class="field">
          <label>Issue番号</label>
          <input v-model.number="manualIssueNumber" type="number" min="1">
        </div>
      </div>
      <div class="actions">
        <button class="button" :disabled="busy || !manualIssueNumber" @click="dispatchIssue">
          実行待ちへ追加
        </button>
      </div>
    </div>
  </section>
</template>
