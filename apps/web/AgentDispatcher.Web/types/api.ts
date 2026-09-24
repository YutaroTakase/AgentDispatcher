export interface Project {
  id: string
  displayName: string
  repository: string
  enabled: boolean
  defaultBranch: string
  issueScanIntervalMinutes: number
  maxConcurrentExecutions: number
  executionRetentionDays: number
  failureWorktreeRetentionDays: number
}

export interface ProjectRequest {
  displayName: string
  repository: string
  enabled: boolean
  defaultBranch: string
  issueScanIntervalMinutes: number
  maxConcurrentExecutions: number
  executionRetentionDays: number
  failureWorktreeRetentionDays: number
  issueQueryFragment: string
  issueSortField: string
  issueSortOrder: string
  maxCandidatesPerScan: number
  defaultModelIdentifier: string
  defaultReasoningEffort: string
}

export interface IssueSelector {
  projectId: string
  queryFragment: string
  sortField: string
  sortOrder: string
  maxCandidatesPerScan: number
}

export interface ExecutionRoute {
  modelIdentifier: string
  reasoningEffort: string
}

export interface RoutingRule {
  id?: string
  order: number
  enabled: boolean
  requiredLabels: string[]
  excludedLabels: string[]
  modelIdentifier: string
  reasoningEffort: string
}

export interface ExecutionSummary {
  id: string
  projectId: string
  projectName?: string
  issueNumber: number
  issueTitle: string
  issueUrl?: string
  issueLabels: string[]
  trigger: string
  modelIdentifier: string
  reasoningEffort: string
  matchedRuleId?: string
  status: string
  createdAt: string
  startedAt?: string
  finishedAt?: string
  durationSeconds?: number
  exitCode?: number
  failureSummary?: string
}

export interface ExecutionEvent {
  id: number
  executionId: string
  status: string
  occurredAt: string
  detail?: string
}

export interface ExecutionDetail {
  execution: ExecutionSummary
  events: ExecutionEvent[]
  baseRevision?: string
  worktreePath?: string
  processIdentifier?: string
  exitCode?: number
  finalResult?: string
  failureSummary?: string
  durationSeconds?: number
  hasStandardOutput: boolean
  hasStandardError: boolean
}

export interface ExecutionLogs {
  standardOutput?: string
  standardError?: string
}

export interface DispatchDecision {
  issueNumber?: number
  kind: string
  detail: string
  executionId?: string
}

export interface DispatchResult {
  queuedExecutions: ExecutionSummary[]
  decisions: DispatchDecision[]
  queuedCount: number
}
