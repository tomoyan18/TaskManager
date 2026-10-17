import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import type { Schemas } from '../api/client'
import { parseGroup, parseStart, parseUnit } from '../utils/schedule'

/** 表示形式。gantt（ガント）と chart（円グラフ）は Phase 9 で追加 */
export type TaskView = 'table' | 'board' | 'gantt' | 'chart'

function parseView(value: string | null): TaskView {
  return value === 'board' || value === 'gantt' || value === 'chart' ? value : 'table'
}

/** 一覧の絞り込み・並び・表示形式・ページを URL クエリで扱う（ux-design.md §3「URL で再現」） */
export function useTaskListParams() {
  const [params, setParams] = useSearchParams()
  const state = useMemo(() => ({
    view: parseView(params.get('view')),
    statusId: params.getAll('statusId'),
    assignee: params.get('assignee') ?? '',
    priority: params.getAll('priority') as Schemas['TaskPriority'][],
    tagId: params.get('tagId') ?? '',
    milestoneId: params.get('milestoneId') ?? '',
    due: params.get('due') ?? '',
    includeClosed: params.get('includeClosed') === 'true',
    showCanceled: params.get('showCanceled') === 'true',
    q: params.get('q') ?? '',
    sort: params.get('sort') ?? '-updatedAt',
    page: Math.max(1, Number(params.get('page') ?? '1') || 1),
    // ガント（SCR-20）: 表示単位・期間の開始日・まとめ方
    unit: parseUnit(params.get('unit')),
    start: parseStart(params.get('start')),
    group: parseGroup(params.get('group')),
  }), [params])

  const update = useCallback((changes: Record<string, string | string[] | boolean | number | null>, resetPage = true) => {
    setParams((current) => {
      const next = new URLSearchParams(current)
      for (const [key, value] of Object.entries(changes)) {
        next.delete(key)
        if (Array.isArray(value)) value.forEach((v) => next.append(key, v))
        else if (value !== null && value !== '' && value !== false) next.set(key, String(value))
      }
      if (resetPage && !('page' in changes)) next.delete('page')
      return next
    }, { replace: false })
  }, [setParams])

  const clear = useCallback(() => {
    setParams((current) => {
      const next = new URLSearchParams()
      const view = current.get('view')
      if (view) next.set('view', view)
      return next
    })
  }, [setParams])

  const activeCount = [state.statusId.length > 0, state.assignee !== '', state.priority.length > 0, state.tagId !== '',
    state.milestoneId !== '', state.due !== '', state.q !== '', state.includeClosed].filter(Boolean).length

  return { ...state, update, clear, activeCount }
}
