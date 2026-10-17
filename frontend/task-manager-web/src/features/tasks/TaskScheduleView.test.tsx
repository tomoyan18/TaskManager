import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import * as f from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

const scheduleUrl = '*/api/v1/projects/SALES/tasks/schedule'
const s = f.statuses
const statusRef = (i: number) => ({ id: s[i]!.id, name: s[i]!.name, category: s[i]!.category, color: s[i]!.color })

// 2026-10-08 が今日（fixtures の TODAY）。週の表示で 2026-10-05（月）から 12 週
const scheduled = [
  f.task({ key: 'SALES-1', number: 1, title: '見積書の作成', startDate: '2026-10-01', dueDate: '2026-10-06', isOverdue: true, status: statusRef(1),
    assignee: { id: 'u-2', displayName: '山田 太郎', isActive: true } }),
  f.task({ key: 'SALES-2', number: 2, title: '会場の下見', startDate: '2026-10-12', dueDate: null, version: 7 }),
]

function captureSchedule(response = f.schedule({ items: scheduled, undatedItems: [f.task({ key: 'SALES-3', number: 3, title: '議事録の共有' })], undatedCount: 4 })) {
  const urls: URL[] = []
  server.use(http.get(scheduleUrl, ({ request }) => {
    urls.push(new URL(request.url))
    return HttpResponse.json(response)
  }))
  return urls
}

describe('タスク一覧（ガント。SCR-20）', () => {
  it('期間を指定して日程を取得し、棒・期限切れ・今日・日付未設定の枠を表示する', async () => {
    const urls = captureSchedule()
    const { container } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')

    const chart = await screen.findByRole('region', { name: 'ガントチャート' })
    expect(urls[0]?.searchParams.get('from')).toBe('2026-10-05')
    expect(urls[0]?.searchParams.get('to')).toBe('2026-12-27')
    expect(urls[0]?.searchParams.get('includeClosed')).toBeNull()
    expect(within(chart).getByRole('link', { name: '見積書の作成' }).getAttribute('href')).toBe('/projects/SALES/tasks/1')
    expect(within(chart).getByText(/10\/1（木）〜10\/6（火）/)).toBeTruthy()
    expect(within(chart).getByText(/（期限なし）/)).toBeTruthy()
    // 期限切れは色だけでなく ⚠ と「◯日超過」の文字で示す
    expect(within(chart).getAllByText(/2日超過/).length).toBeGreaterThan(0)
    expect(container.querySelector('[data-overdue]')?.getAttribute('data-color')).toBe('blue')
    expect(within(chart).getByText('今日')).toBeTruthy()
    expect(screen.getByRole('heading', { name: '日付が未設定のタスク（4 件）' })).toBeTruthy()
    expect(screen.getByText('ほか 3 件（表示はタスクの一覧で確認できます）')).toBeTruthy()
    expect(screen.queryByRole('region', { name: '絞り込み' })).toBeNull()
  })

  it('表示単位・前へ / 次へ・完了の表示で取得する期間と条件が変わる', async () => {
    const urls = captureSchedule()
    const { user } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')
    await screen.findByRole('region', { name: 'ガントチャート' })

    await user.click(screen.getByRole('button', { name: '次へ ›' }))
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('from')).toBe('2026-11-02'))

    await user.click(within(screen.getByRole('group', { name: '表示単位' })).getByRole('button', { name: '日' }))
    await waitFor(() => {
      const last = urls.at(-1)!
      const days = (Date.parse(last.searchParams.get('to')!) - Date.parse(last.searchParams.get('from')!)) / 86_400_000 + 1
      expect(days).toBe(14)
    })
    expect(within(screen.getByRole('group', { name: '表示単位' })).getByRole('button', { name: '日' }).getAttribute('aria-pressed')).toBe('true')

    await user.click(within(screen.getByRole('group', { name: '表示単位' })).getByRole('button', { name: '月' }))
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('from')?.endsWith('-01')).toBe(true))

    await user.click(screen.getByLabelText('完了・中止も表示'))
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('includeClosed')).toBe('true'))
  })

  it('担当者でまとめると担当者ごとの見出しを付ける', async () => {
    captureSchedule()
    const { user } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')
    await screen.findByRole('region', { name: 'ガントチャート' })

    await user.selectOptions(screen.getByLabelText('まとめ方'), '担当者')

    expect(await screen.findByRole('region', { name: '山田 太郎（1 件）' })).toBeTruthy()
    expect(screen.getByRole('region', { name: '担当者なし（1 件）' })).toBeTruthy()
  })

  it('表で見ると日付欄で日程を変更でき、取得時の版を付けて保存する', async () => {
    captureSchedule()
    let body: unknown = null
    server.use(http.patch('*/api/v1/projects/SALES/tasks/2', async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ ...f.task({ number: 2, version: 8 }), description: null, completedAtUtc: null, subtasks: [], createdAtUtc: f.NOW_ISO,
        createdBy: null, updatedBy: null, permissions: { canEdit: true, canDelete: true, canComment: true, canUpload: true } })
    }))
    const { user } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')
    await screen.findByRole('region', { name: 'ガントチャート' })

    await user.click(screen.getByRole('button', { name: '表で見る' }))
    const table = await screen.findByRole('table', { name: 'タスクの日程（表）' })
    const due = within(table).getByLabelText('SALES-2 の期限')
    await user.type(due, '2026-10-16')

    await waitFor(() => expect(body).toEqual({ startDate: '2026-10-12', dueDate: '2026-10-16', version: 7 }))
    expect(await screen.findByText(/SALES-2 の日程を変更しました/)).toBeTruthy()
  })

  it('表で期限を開始日より前にしようとすると送信せずに知らせる', async () => {
    captureSchedule()
    let patched = false
    server.use(http.patch('*/api/v1/projects/SALES/tasks/2', () => {
      patched = true
      return HttpResponse.json({}, { status: 500 })
    }))
    const { user } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')
    await screen.findByRole('region', { name: 'ガントチャート' })
    await user.click(screen.getByRole('button', { name: '表で見る' }))

    await user.type(within(await screen.findByRole('table', { name: 'タスクの日程（表）' })).getByLabelText('SALES-2 の期限'), '2026-10-01')

    expect(await screen.findByText('期限は開始日以降の日付にしてください。')).toBeTruthy()
    expect(patched).toBe(false)
  })

  it('閲覧者には日程を変更する部品を出さない', async () => {
    captureSchedule()
    server.use(http.get('*/api/v1/projects/SALES', () => HttpResponse.json(f.project({ myRole: 'viewer' }))))
    const { user, container } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')
    await screen.findByRole('region', { name: 'ガントチャート' })

    expect(container.querySelector('[data-editable]')).toBeNull()
    await user.click(screen.getByRole('button', { name: '表で見る' }))
    const table = await screen.findByRole('table', { name: 'タスクの日程（表）' })
    expect(within(table).queryByLabelText('SALES-2 の期限')).toBeNull()
  })

  it('期間内にも日付未設定にもタスクがなければ空の状態', async () => {
    captureSchedule(f.schedule())
    renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')

    expect(await screen.findByText('この期間に日程のあるタスクはありません')).toBeTruthy()
  })

  it('取得に失敗したら再試行できる', async () => {
    let calls = 0
    server.use(http.get(scheduleUrl, () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(f.problem(500, 'server.error', '処理中にエラーが発生しました。'), { status: 500 })
        : HttpResponse.json(f.schedule({ items: scheduled }))
    }))
    const { user } = renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')

    const alert = await screen.findByRole('alert')
    await user.click(within(alert).getByRole('button', { name: '再試行' }))
    expect(await screen.findByRole('region', { name: 'ガントチャート' })).toBeTruthy()
  })

  it('500 件を超えたことを知らせる', async () => {
    captureSchedule(f.schedule({ items: scheduled, truncated: true }))
    renderApp('/projects/SALES/tasks?view=gantt&start=2026-10-05')

    expect(await screen.findByText('表示できるのは 500 件までです。期間を短くするか、表示単位を変えてください。')).toBeTruthy()
  })
})

describe('タスク一覧（円グラフ。SCR-21）', () => {
  it('プロジェクトのステータス別の割合を表示し、ステータス名から絞り込んだ表へ移動できる', async () => {
    const { user, router } = renderApp('/projects/SALES/tasks?view=chart')

    const table = await screen.findByRole('table', { name: 'ステータス別の割合（表）' })
    expect(within(table).getByRole('link', { name: '完了' }).getAttribute('href')).toBe('/projects/SALES/tasks?statusId=s-3&includeClosed=true')
    expect(screen.getByRole('img', { name: /ステータス別の割合/ })).toBeTruthy()

    await user.click(within(table).getByRole('link', { name: '未着手' }))
    await waitFor(() => expect(router.state.location.search).toBe('?statusId=s-1'))
    expect(await screen.findByRole('table', { name: 'タスクの一覧' })).toBeTruthy()
  })

  it('タスクがなければ空の状態と追加の操作を示す', async () => {
    server.use(http.get('*/api/v1/projects/SALES/tasks/status-summary', () => HttpResponse.json(
      f.statusSummary({ totalCount: 0, items: f.statusSummary().items.map((i) => ({ ...i, count: 0, percent: 0 })) }))))
    renderApp('/projects/SALES/tasks?view=chart')

    expect(await screen.findByText('まだタスクがありません')).toBeTruthy()
    expect(screen.getAllByRole('button', { name: 'タスクを追加' }).length).toBeGreaterThanOrEqual(1)
  })
})
