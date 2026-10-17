// MSW のサーバー。既定ではログイン中の利用者（プロジェクト SALES の管理者）として応答する
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import type { Schemas } from '../api/client'
import * as f from './fixtures'

/** ログイン状態を変える（null = 未ログイン） */
export function signedInAs(u: Schemas['CurrentUserResponse'] | null) {
  server.use(
    http.post('*/api/v1/auth/refresh', () =>
      u === null
        ? HttpResponse.json(f.problem(401, 'auth.session_expired', 'ログインの有効期限が切れました。'), { status: 401 })
        : HttpResponse.json(f.loginResponse(u))),
  )
}

export const defaultHandlers = [
  http.post('*/api/v1/auth/refresh', () => HttpResponse.json(f.loginResponse())),
  http.post('*/api/v1/auth/logout', () => new HttpResponse(null, { status: 204 })),
  http.get('*/api/v1/me/notices/unread-count', () => HttpResponse.json({ count: 0 })),
  http.get('*/api/v1/me/notices', () => HttpResponse.json(f.paged([]))),
  http.get('*/api/v1/projects', () => HttpResponse.json(f.paged([{
    key: 'SALES', name: '営業支援', myRole: 'owner', isArchived: false, openTaskCount: 1, overdueTaskCount: 0, ownerNames: ['テスト 管理者'], updatedAtUtc: f.NOW_ISO,
  }]))),
  http.get('*/api/v1/projects/:projectKey', ({ params }) =>
    params.projectKey === 'SALES'
      ? HttpResponse.json(f.project())
      : HttpResponse.json(f.problem(404, 'resource.not_found', '対象が見つかりません。'), { status: 404 })),
  http.get('*/api/v1/projects/:projectKey/statuses', () => HttpResponse.json(f.statuses)),
  http.get('*/api/v1/projects/:projectKey/tags', () => HttpResponse.json([])),
  http.get('*/api/v1/projects/:projectKey/milestones', () => HttpResponse.json([])),
  http.get('*/api/v1/projects/:projectKey/members', () => HttpResponse.json([{
    user: { id: f.user().id, displayName: 'テスト 管理者', isActive: true }, email: 'owner@example.test', role: 'owner', joinedAtUtc: f.NOW_ISO,
  }])),
  http.get('*/api/v1/projects/:projectKey/tasks', () => HttpResponse.json(f.paged([f.task()]))),
  http.get('*/api/v1/projects/:projectKey/tasks/schedule', () => HttpResponse.json(f.schedule())),
  http.get('*/api/v1/projects/:projectKey/tasks/status-summary', () => HttpResponse.json(f.statusSummary())),
  http.get('*/api/v1/me/tasks/status-summary', () => HttpResponse.json(f.statusSummary({ groupedBy: 'category', totalCount: 0,
    items: [['未着手', 'notStarted', 'gray'], ['進行中', 'inProgress', 'blue'], ['完了', 'done', 'green'], ['中止', 'canceled', 'red']].map(([name, category, color], i) => ({
      statusId: null, name: name!, category: category as Schemas['StatusCategory'], color: color as Schemas['StatusColor'], sortOrder: i + 1, count: 0, percent: 0 })) }))),
  http.get('*/api/v1/me/tasks', () => HttpResponse.json({
    today: f.TODAY, timeZoneId: 'Asia/Tokyo', truncated: false, recentlyCompleted: [],
    buckets: { overdue: [], today: [], thisWeek: [], later: [], noDueDate: [] },
  })),
]

export const server = setupServer(...defaultHandlers)
