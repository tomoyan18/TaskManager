import { fireEvent, screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { Schemas } from '../../api/client'
import * as f from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

const base = '*/api/v1/projects/SALES/tasks/1'

function detail(overrides: Partial<Schemas['TaskDetailResponse']> = {}): Schemas['TaskDetailResponse'] {
  return {
    ...f.task(), description: null, completedAtUtc: null, subtasks: [], createdAtUtc: f.NOW_ISO, createdBy: null, updatedBy: null,
    permissions: { canEdit: true, canDelete: true, canComment: true, canUpload: true }, ...overrides,
  }
}

describe('タスク詳細（SCR-08）', () => {
  it('続けて 2 つの項目を変えても、直前の保存の版を使って順に保存する（自分の変更同士で競合しない）', async () => {
    let current = detail({ version: 3 })
    const sentVersions: number[] = []
    server.use(
      http.get(base, () => HttpResponse.json(current)),
      http.get(`${base}/comments`, () => HttpResponse.json(f.paged([]))),
      http.get(`${base}/history`, () => HttpResponse.json(f.paged([]))),
      http.get(`${base}/attachments`, () => HttpResponse.json([])),
      http.patch(base, async ({ request }) => {
        const body = (await request.json()) as { version: number; priority?: Schemas['TaskPriority']; dueDate?: string }
        sentVersions.push(body.version)
        if (body.version !== current.version) {
          return HttpResponse.json(f.problem(409, 'concurrency.conflict', '他の人が先に更新しました。'), { status: 409 })
        }
        current = { ...current, ...('priority' in body ? { priority: body.priority! } : {}), ...('dueDate' in body ? { dueDate: body.dueDate ?? null } : {}),
          version: current.version + 1 }
        return HttpResponse.json(current)
      }),
    )
    renderApp('/projects/SALES/tasks/1')

    // 1 件目の保存の応答を待たずに 2 件目を変更する（素早い連続操作）
    const priority = await screen.findByLabelText('優先度')
    fireEvent.change(priority, { target: { value: 'high' } })
    fireEvent.change(priority, { target: { value: 'urgent' } })

    await waitFor(() => expect(sentVersions).toEqual([3, 4]))
    expect(current.priority).toBe('urgent')
    expect(screen.queryByText('他の人が先に更新しました。最新の内容を表示しています。')).toBeNull()
  })
})
