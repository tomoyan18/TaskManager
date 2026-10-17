// テスト用の API 応答（型は OpenAPI から生成した schema.d.ts）。値はすべて架空
import type { Schemas } from '../api/client'

export const TODAY = '2026-10-08'
export const NOW_ISO = '2026-10-08T01:00:00Z'

export function user(overrides: Partial<Schemas['CurrentUserResponse']> = {}): Schemas['CurrentUserResponse'] {
  return {
    id: '01990000-0000-7000-8000-000000000001',
    email: 'owner@example.test',
    displayName: 'テスト 管理者',
    systemRole: 'member',
    timeZoneId: 'Asia/Tokyo',
    mustChangePassword: false,
    isProjectOwnerSomewhere: true,
    ...overrides,
  }
}

export function loginResponse(u: Schemas['CurrentUserResponse'] = user()): Schemas['LoginResponse'] {
  return { accessToken: `token-for-${u.id}`, expiresIn: 900, user: u }
}

export function project(overrides: Partial<Schemas['ProjectDetailResponse']> = {}): Schemas['ProjectDetailResponse'] {
  return {
    key: 'SALES',
    name: '営業支援',
    description: null,
    isArchived: false,
    archivedAtUtc: null,
    myRole: 'owner',
    memberCount: 2,
    taskCounts: { notStarted: 1, inProgress: 0, done: 0, canceled: 0, overdue: 0 },
    createdAtUtc: NOW_ISO,
    createdBy: null,
    updatedAtUtc: NOW_ISO,
    version: 0,
    ...overrides,
  }
}

export const statuses: Schemas['WorkflowStatusResponse'][] = [
  { id: 's-1', name: '未着手', category: 'notStarted', color: 'gray', sortOrder: 1, isDefault: true, taskCount: 1 },
  { id: 's-2', name: '対応中', category: 'inProgress', color: 'blue', sortOrder: 2, isDefault: false, taskCount: 0 },
  { id: 's-3', name: '完了', category: 'done', color: 'green', sortOrder: 3, isDefault: false, taskCount: 0 },
  { id: 's-4', name: '中止', category: 'canceled', color: 'red', sortOrder: 4, isDefault: false, taskCount: 0 },
]

export function task(overrides: Partial<Schemas['TaskSummaryResponse']> = {}): Schemas['TaskSummaryResponse'] {
  const s = statuses[0]!
  return {
    key: 'SALES-1',
    number: 1,
    title: '見積書の作成',
    status: { id: s.id, name: s.name, category: s.category, color: s.color },
    priority: 'normal',
    assignee: null,
    startDate: null,
    dueDate: null,
    isOverdue: false,
    milestone: null,
    tags: [],
    parent: null,
    subtaskCount: 0,
    closedSubtaskCount: 0,
    commentCount: 0,
    attachmentCount: 0,
    updatedAtUtc: NOW_ISO,
    version: 3,
    ...overrides,
  }
}

export function paged<T>(items: T[], overrides: { page?: number; pageSize?: number; totalCount?: number } = {}) {
  const pageSize = overrides.pageSize ?? 50
  const totalCount = overrides.totalCount ?? items.length
  return { items, page: overrides.page ?? 1, pageSize, totalCount, totalPages: totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize) }
}

export function problem(status: number, code: string, title: string, extra: Partial<Schemas['ProblemDetails']> = {}): Schemas['ProblemDetails'] {
  return { type: `urn:taskmanager:problem:${code}`, title, status, detail: null, instance: null, code, traceId: 'trace-0001', errors: null, ...extra }
}

/** プロジェクトのステータス別件数（未着手 5・対応中 4・完了 3・中止 0。合計 12） */
export function statusSummary(overrides: Partial<Schemas['TaskStatusSummaryResponse']> = {}): Schemas['TaskStatusSummaryResponse'] {
  const counts = [5, 4, 3, 0]
  const percents = [41.7, 33.3, 25, 0]
  return {
    groupedBy: 'status',
    totalCount: 12,
    items: statuses.map((s, i) => ({ statusId: s.id, name: s.name, category: s.category, color: s.color, sortOrder: s.sortOrder, count: counts[i] ?? 0, percent: percents[i] ?? 0 })),
    ...overrides,
  }
}

export function schedule(overrides: Partial<Schemas['TaskScheduleResponse']> = {}): Schemas['TaskScheduleResponse'] {
  return { from: '2026-10-05', to: '2026-12-27', today: TODAY, timeZoneId: 'Asia/Tokyo', items: [], truncated: false, undatedItems: [], undatedCount: 0, ...overrides }
}

