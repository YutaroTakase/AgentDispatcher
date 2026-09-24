<script setup lang="ts">
import type { Project, ProjectRequest } from '~/types/api'

const { request } = useApi()
const projects = ref<Project[]>([])
const loading = ref(false)
const error = ref('')
const message = ref('')

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

async function load() {
  projects.value = await request<Project[]>('/api/projects')
}

async function createProject() {
  loading.value = true
  error.value = ''
  message.value = ''
  try {
    await request<Project>('/api/projects', {
      method: 'POST',
      body: form,
    })
    message.value = 'プロジェクトを登録しました。'
    form.displayName = ''
    form.repository = ''
    await load()
  }
  catch (cause) {
    error.value = cause instanceof Error ? cause.message : 'プロジェクトを登録できませんでした。'
  }
  finally {
    loading.value = false
  }
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
        <h1>プロジェクト</h1>
        <p class="muted">GitHubリポジトリと自動実行の基本設定を管理します。</p>
      </div>
    </div>

    <div v-if="error" class="notice error">{{ error }}</div>
    <div v-if="message" class="notice">{{ message }}</div>

    <div class="panel">
      <h2>新しいプロジェクト</h2>
      <form class="form-grid" @submit.prevent="createProject">
        <div class="field">
          <label>表示名</label>
          <input v-model="form.displayName" required>
        </div>
        <div class="field">
          <label>GitHubリポジトリ</label>
          <input v-model="form.repository" placeholder="owner/repository" required>
        </div>
        <div class="field">
          <label>既定ブランチ</label>
          <input v-model="form.defaultBranch" required>
        </div>
        <div class="field">
          <label>Issue確認間隔（分）</label>
          <input v-model.number="form.issueScanIntervalMinutes" type="number" min="1" max="1440">
        </div>
        <div class="field">
          <label>最大同時実行数</label>
          <input v-model.number="form.maxConcurrentExecutions" type="number" min="1" max="32">
        </div>
        <div class="field">
          <label>1回の最大候補数</label>
          <input v-model.number="form.maxCandidatesPerScan" type="number" min="1" max="100">
        </div>
        <div class="field full">
          <label>Issue検索条件</label>
          <input v-model="form.issueQueryFragment" placeholder="label:agent -label:blocked">
        </div>
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
          <select v-model="form.issueSortOrder">
            <option value="asc">昇順</option>
            <option value="desc">降順</option>
          </select>
        </div>
        <div class="field">
          <label>既定の推論モデル</label>
          <input v-model="form.defaultModelIdentifier" required>
        </div>
        <div class="field">
          <label>既定の推論レベル</label>
          <input v-model="form.defaultReasoningEffort" required>
        </div>
        <div class="field">
          <label>実行履歴の保存日数</label>
          <input v-model.number="form.executionRetentionDays" type="number" min="1">
        </div>
        <div class="field">
          <label>失敗時作業ツリーの保存日数</label>
          <input v-model.number="form.failureWorktreeRetentionDays" type="number" min="0">
        </div>
        <div class="field full">
          <label>
            <input v-model="form.enabled" type="checkbox" style="width: auto">
            登録後すぐに有効化する
          </label>
        </div>
        <div class="actions field full">
          <button class="button" type="submit" :disabled="loading">
            登録
          </button>
        </div>
      </form>
    </div>

    <div class="panel" style="margin-top: 20px">
      <h2>登録済みプロジェクト</h2>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>表示名</th>
              <th>リポジトリ</th>
              <th>状態</th>
              <th>確認間隔</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="project in projects" :key="project.id">
              <td><NuxtLink :to="`/projects/${project.id}`">{{ project.displayName }}</NuxtLink></td>
              <td>{{ project.repository }}</td>
              <td>{{ project.enabled ? '有効' : '無効' }}</td>
              <td>{{ project.issueScanIntervalMinutes }}分</td>
            </tr>
            <tr v-if="!projects.length">
              <td colspan="4" class="muted">プロジェクトはありません。</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
