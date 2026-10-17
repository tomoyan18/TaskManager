import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import * as f from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

const withProject = (t: ReturnType<typeof f.task>) => ({ ...t, project: { key: 'SALES', name: '営業支援' } })

describe('自分のタスク（SCR-04）', () => {
  it('担当タスクがなければ空の状態を示す', async () => {
    renderApp('/')

    expect(await screen.findByRole('heading', { name: '自分のタスク' })).toBeTruthy()
    expect(await screen.findByText('担当しているタスクはありません')).toBeTruthy()
  })

  it('期限の区分ごとに見出しを付けて表示する', async () => {
    server.use(http.get('*/api/v1/me/tasks', () => HttpResponse.json({
      today: f.TODAY, timeZoneId: 'Asia/Tokyo', truncated: false, recentlyCompleted: [],
      buckets: {
        overdue: [withProject(f.task({ key: 'SALES-1', title: '昨日までの作業', dueDate: '2026-10-07', isOverdue: true }))],
        today: [withProject(f.task({ key: 'SALES-2', number: 2, title: '今日までの作業', dueDate: '2026-10-08' }))],
        thisWeek: [],
        later: [],
        noDueDate: [withProject(f.task({ key: 'SALES-3', number: 3, title: '期限のない作業' }))],
      },
    })))
    renderApp('/')

    expect(await screen.findByRole('heading', { name: /期限切れ/ })).toBeTruthy()
    expect(screen.getByRole('heading', { name: /今日まで/ })).toBeTruthy()
    expect(screen.getByText('昨日までの作業')).toBeTruthy()
    expect(screen.getByText('今日までの作業')).toBeTruthy()
  })

  it('読み込みに失敗したらエラーを表示する', async () => {
    server.use(http.get('*/api/v1/me/tasks', () => HttpResponse.error()))
    renderApp('/')

    expect((await screen.findByRole('alert')).textContent).toContain('通信状態を確認して、もう一度お試しください。')
  })
})

describe('自分のタスク: ステータス別の割合（REQ-CHT-02）', () => {
  const summaryUrl = '*/api/v1/me/tasks/status-summary'

  it('表示を切り替えると、全プロジェクトの区分別の割合を表示する', async () => {
    server.use(http.get(summaryUrl, () => HttpResponse.json(f.statusSummary({
      groupedBy: 'category', totalCount: 4,
      items: [['未着手', 'notStarted', 'gray', 1, 25], ['進行中', 'inProgress', 'blue', 2, 50], ['完了', 'done', 'green', 1, 25], ['中止', 'canceled', 'red', 0, 0]]
        .map(([name, category, color, count, percent], i) => ({ statusId: null, name: name as string, category: category as 'done', color: color as 'gray', sortOrder: i + 1, count: count as number, percent: percent as number })),
    }))))
    const { user, router } = renderApp('/')

    await user.click(await screen.findByRole('button', { name: 'ステータス別の割合' }))

    const table = await screen.findByRole('table', { name: '自分のタスクの割合（区分別）（表）' })
    expect(within(table).getAllByRole('row').slice(1, -1).map((r) => r.textContent)).toEqual(['未着手125.0%', '進行中250.0%', '完了125.0%', '中止00.0%'])
    // 区分別の集計はプロジェクトのステータスと対応しないため、一覧へのリンクにしない
    expect(within(table).queryAllByRole('link')).toHaveLength(0)
    expect(router.state.location.search).toBe('?view=chart')
  })

  it('プロジェクトを選ぶとそのプロジェクトのステータス別で、ステータス名から自分の担当の表へ移動できる', async () => {
    const keys: (string | null)[] = []
    server.use(http.get(summaryUrl, ({ request }) => {
      keys.push(new URL(request.url).searchParams.get('projectKey'))
      return HttpResponse.json(f.statusSummary())
    }))
    const { user, router } = renderApp('/?view=chart')

    // 選べるのは参加中のプロジェクト（サイドナビと同じ一覧）。読み込みを待ってから選ぶ
    await screen.findByRole('option', { name: '営業支援（SALES）' })
    await user.selectOptions(screen.getByLabelText('対象'), '営業支援（SALES）')

    await waitFor(() => expect(keys.at(-1)).toBe('SALES'))
    const table = await screen.findByRole('table', { name: '自分のタスクの割合（営業支援）（表）' })
    expect(within(table).getByRole('link', { name: '対応中' }).getAttribute('href')).toBe('/projects/SALES/tasks?statusId=s-2&assignee=me')
    expect(router.state.location.search).toBe('?view=chart&project=SALES')
  })

  it('担当がなければ空の状態', async () => {
    renderApp('/?view=chart')

    expect(await screen.findByText('担当しているタスクはありません')).toBeTruthy()
  })

  it('取得に失敗したらエラーを表示する', async () => {
    server.use(http.get(summaryUrl, () => HttpResponse.json(f.problem(404, 'resource.not_found', '対象が見つかりません。'), { status: 404 })))
    renderApp('/?view=chart&project=ZZNOTEXIST')

    expect((await screen.findByRole('alert')).textContent).toContain('対象が見つかりません。')
  })
})

