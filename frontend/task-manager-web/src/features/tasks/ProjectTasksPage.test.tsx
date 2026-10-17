import { screen, waitFor, within } from '@testing-library/react'
import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import * as f from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'

// 表とスマートフォン用のカード一覧は CSS で切り替える。jsdom は CSS を適用しないため、表の中に限定して探す
const inTable = async () => within(await screen.findByRole('table', { name: 'タスクの一覧' }))
import { server } from '../../test/server'

const tasksUrl = '*/api/v1/projects/SALES/tasks'

describe('タスク一覧（SCR-06）', () => {
  it('タスクを表で表示し、タイトルから詳細へ移動できる', async () => {
    server.use(http.get(tasksUrl, () => HttpResponse.json(f.paged([
      f.task(),
      f.task({ key: 'SALES-2', number: 2, title: '会場の候補を3つ挙げる', dueDate: '2026-10-06', isOverdue: true, priority: 'urgent' }),
    ]))))
    renderApp('/projects/SALES/tasks')

    const table = await screen.findByRole('table', { name: 'タスクの一覧' })
    expect(within(table).getByRole('link', { name: '見積書の作成' }).getAttribute('href')).toBe('/projects/SALES/tasks/1')
    expect(within(table).getByText('SALES-2')).toBeTruthy()
    expect(within(table).getAllByText(/超過/).length).toBeGreaterThan(0)
  })

  it('読み込み中は読み込み中であることを伝える', async () => {
    server.use(http.get(tasksUrl, async () => {
      await delay('infinite')
      return HttpResponse.json(f.paged([]))
    }))
    renderApp('/projects/SALES/tasks')

    await screen.findByRole('heading', { name: '営業支援' })
    expect(screen.getAllByRole('status').some((s) => s.textContent?.includes('読み込んでいます'))).toBe(true)
  })

  it('タスクがなければ空の状態と追加の操作を示す', async () => {
    server.use(http.get(tasksUrl, () => HttpResponse.json(f.paged([]))))
    renderApp('/projects/SALES/tasks')

    expect(await screen.findByText('まだタスクがありません')).toBeTruthy()
    expect(screen.getAllByRole('button', { name: /タスクを追加/ }).length).toBeGreaterThanOrEqual(1)
  })

  it('読み込みに失敗したら再試行できる', async () => {
    let calls = 0
    server.use(http.get(tasksUrl, () => {
      calls += 1
      return calls === 1
        ? HttpResponse.json(f.problem(500, 'server.error', '処理中にエラーが発生しました。'), { status: 500 })
        : HttpResponse.json(f.paged([f.task()]))
    }))
    const { user } = renderApp('/projects/SALES/tasks')

    const alert = await screen.findByRole('alert')
    expect(alert.textContent).toContain('読み込めませんでした')
    expect(alert.textContent).toContain('問い合わせ番号: trace-0001')
    await user.click(within(alert).getByRole('button', { name: '再試行' }))

    expect((await inTable()).getByRole('link', { name: '見積書の作成' })).toBeTruthy()
  })

  it('閲覧者には編集の部品を表示しない', async () => {
    server.use(http.get('*/api/v1/projects/SALES', () => HttpResponse.json(f.project({ myRole: 'viewer' }))))
    renderApp('/projects/SALES/tasks')

    ;(await inTable()).getByRole('link', { name: '見積書の作成' })
    expect(screen.queryByRole('button', { name: /タスクを追加/ })).toBeNull()
    expect(screen.queryByRole('combobox', { name: 'SALES-1 のステータス' })).toBeNull()
    expect(screen.getAllByText('未着手').length).toBeGreaterThan(0)
  })

  it('アーカイブ中は管理者でも編集できないことを示す', async () => {
    server.use(http.get('*/api/v1/projects/SALES', () => HttpResponse.json(f.project({ isArchived: true, archivedAtUtc: f.NOW_ISO }))))
    renderApp('/projects/SALES/tasks')

    expect(await screen.findByText('このプロジェクトはアーカイブされています。閲覧のみできます。')).toBeTruthy()
    ;(await inTable()).getByRole('link', { name: '見積書の作成' })
    expect(screen.queryByRole('button', { name: /タスクを追加/ })).toBeNull()
    expect(screen.queryByRole('combobox', { name: 'SALES-1 のステータス' })).toBeNull()
  })

  it('表でステータスを変えると取得時の版を付けて保存する', async () => {
    let body: unknown = null
    server.use(http.patch(`${tasksUrl}/1`, async ({ request }) => {
      body = await request.json()
      return HttpResponse.json({ ...f.task({ version: 4 }), description: null, completedAtUtc: null, subtasks: [], createdAtUtc: f.NOW_ISO,
        createdBy: null, updatedBy: null, permissions: { canEdit: true, canDelete: true, canComment: true, canUpload: true } })
    }))
    const { user } = renderApp('/projects/SALES/tasks')

    const select = await screen.findByRole('combobox', { name: 'SALES-1 のステータス' })
    await waitFor(() => expect((select as HTMLSelectElement).disabled).toBe(false))
    await user.selectOptions(select, '対応中')

    await waitFor(() => expect(body).toEqual({ statusId: 's-2', version: 3 }))
  })

  it('他の人が先に更新していたら知らせる', async () => {
    server.use(http.patch(`${tasksUrl}/1`, () =>
      HttpResponse.json(f.problem(409, 'concurrency.conflict', '他の人が先に更新しました。'), { status: 409 })))
    const { user } = renderApp('/projects/SALES/tasks')

    const select = await screen.findByRole('combobox', { name: 'SALES-1 のステータス' })
    await waitFor(() => expect((select as HTMLSelectElement).disabled).toBe(false))
    await user.selectOptions(select, '完了')

    expect(await screen.findByText('他の人が先に更新しました。最新の内容を表示しています。')).toBeTruthy()
  })

  it('タイトルに HTML を含めても文字として表示する', async () => {
    server.use(http.get(tasksUrl, () => HttpResponse.json(f.paged([f.task({ title: '<img src=x onerror=alert(1)>見積' })]))))
    const { container } = renderApp('/projects/SALES/tasks')

    expect((await inTable()).getByRole('link', { name: '<img src=x onerror=alert(1)>見積' })).toBeTruthy()
    expect(container.querySelector('img')).toBeNull()
  })
})

describe('タスクの追加（SCR-09）', () => {
  it('タイトルが空なら送信せずに知らせる', async () => {
    let posted = false
    server.use(http.post(tasksUrl, () => {
      posted = true
      return HttpResponse.json({}, { status: 201 })
    }))
    const { user } = renderApp('/projects/SALES/tasks')

    await user.click((await screen.findAllByRole('button', { name: /タスクを追加/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: '追加する' }))

    expect(await within(dialog).findByText(/タイトルを入力してください。/)).toBeTruthy()
    expect(posted).toBe(false)
  })

  it('タイトルだけで追加でき、追加したことと開くリンクを知らせる', async () => {
    let body: Record<string, unknown> | null = null
    server.use(http.post(tasksUrl, async ({ request }) => {
      body = (await request.json()) as Record<string, unknown>
      return HttpResponse.json({ ...f.task({ key: 'SALES-2', number: 2, title: '議事録の共有' }), description: null, completedAtUtc: null,
        subtasks: [], createdAtUtc: f.NOW_ISO, createdBy: null, updatedBy: null,
        permissions: { canEdit: true, canDelete: true, canComment: true, canUpload: true } }, { status: 201 })
    }))
    const { user } = renderApp('/projects/SALES/tasks')

    await user.click((await screen.findAllByRole('button', { name: /タスクを追加/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/^タイトル/), '  議事録の共有  ')
    await user.click(within(dialog).getByRole('button', { name: '追加する' }))

    expect(await screen.findByText('SALES-2 を追加しました')).toBeTruthy()
    expect(screen.getByRole('link', { name: '開く' }).getAttribute('href')).toBe('/projects/SALES/tasks/2')
    expect(body).toMatchObject({ title: '議事録の共有', priority: 'normal', parentTaskNumber: null })
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('API の項目エラーを該当欄に表示する', async () => {
    server.use(http.post(tasksUrl, () => HttpResponse.json(
      f.problem(400, 'validation.failed', '入力内容に誤りがあります。', { errors: { title: ['タイトルに改行や制御文字は使えません。'] } }),
      { status: 400 })))
    const { user } = renderApp('/projects/SALES/tasks')

    await user.click((await screen.findAllByRole('button', { name: /タスクを追加/ }))[0]!)
    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/^タイトル/), '見積')
    await user.click(within(dialog).getByRole('button', { name: '追加する' }))

    expect(await within(dialog).findByText(/タイトルに改行や制御文字は使えません。/)).toBeTruthy()
  })
})
