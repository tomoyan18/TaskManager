// ガントチャート（SCR-20）の期間・目盛り・棒の位置・まとめ方の計算。日付は YYYY-MM-DD（タイムゾーンなし）で扱う
import type { Schemas } from '../api/client'
import { addDays, diffDays } from './dates'

export type ScheduleUnit = 'day' | 'week' | 'month'
export type ScheduleGroup = 'none' | 'assignee' | 'status' | 'milestone'

export const SCHEDULE_UNITS: ScheduleUnit[] = ['day', 'week', 'month']
export const SCHEDULE_UNIT_LABELS: Record<ScheduleUnit, string> = { day: '日', week: '週', month: '月' }
export const SCHEDULE_GROUPS: ScheduleGroup[] = ['none', 'assignee', 'status', 'milestone']
export const SCHEDULE_GROUP_LABELS: Record<ScheduleGroup, string> = { none: 'なし', assignee: '担当者', status: 'ステータス', milestone: 'マイルストーン' }

/** 1 日あたりの幅（px）。日 14 列・週 12 列・月 12 列が画面に収まる程度にする（OD-40） */
export const DAY_WIDTH: Record<ScheduleUnit, number> = { day: 48, week: 16, month: 4 }

/** API の上限と同じ（ScheduleWindow.MaxDays） */
export const MAX_WINDOW_DAYS = 366

const WEEKDAYS = ['日', '月', '火', '水', '木', '金', '土']

function toDate(date: string): Date {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(Date.UTC(y ?? 1970, (m ?? 1) - 1, d ?? 1))
}

function toIso(date: Date): string {
  return date.toISOString().slice(0, 10)
}

/** 月曜日（週の始まり） */
export function mondayOf(date: string): string {
  const day = toDate(date).getUTCDay()
  return addDays(date, -((day + 6) % 7))
}

/** その月の 1 日 */
export function firstOfMonth(date: string): string {
  return `${date.slice(0, 7)}-01`
}

/** 月を足す（1 日に揃える） */
export function addMonths(date: string, months: number): string {
  const d = toDate(firstOfMonth(date))
  d.setUTCMonth(d.getUTCMonth() + months)
  return toIso(d)
}

/** 表示期間（両端を含む）。日 = 14 日、週 = 12 週（月曜始まり）、月 = 12 か月 */
export function windowFor(unit: ScheduleUnit, start: string): { from: string; to: string } {
  if (unit === 'day') return { from: start, to: addDays(start, 13) }
  if (unit === 'week') {
    const from = mondayOf(start)
    return { from, to: addDays(from, 12 * 7 - 1) }
  }
  const from = firstOfMonth(start)
  return { from, to: addDays(addMonths(from, 12), -1) }
}

/** 「今日」を押したときの期間の開始日（今日の少し前から表示する） */
export function defaultStart(unit: ScheduleUnit, today: string): string {
  if (unit === 'day') return addDays(today, -3)
  if (unit === 'week') return mondayOf(addDays(today, -7))
  return addMonths(today, -1)
}

/** 前へ / 次へ（日 7 日、週 4 週、月 3 か月ずつ） */
export function shiftStart(unit: ScheduleUnit, start: string, direction: -1 | 1): string {
  if (unit === 'day') return addDays(start, 7 * direction)
  if (unit === 'week') return addDays(mondayOf(start), 28 * direction)
  return addMonths(start, 3 * direction)
}

/** URL の値を検証する（不正な値は既定に戻す） */
export function parseUnit(value: string | null): ScheduleUnit {
  return value === 'day' || value === 'month' ? value : 'week'
}

export function parseGroup(value: string | null): ScheduleGroup {
  return value === 'assignee' || value === 'status' || value === 'milestone' ? value : 'none'
}

export function parseStart(value: string | null): string | null {
  if (value === null || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null
  const d = toDate(value)
  return toIso(d) === value && value >= '2000-01-01' && value <= '2099-12-31' ? value : null
}

export type TimelineColumn = { start: string; days: number; label: string; detail: string; weekend: boolean }

/** 目盛り（1 列 = 日・週・月） */
export function columnsFor(unit: ScheduleUnit, from: string, to: string): TimelineColumn[] {
  const columns: TimelineColumn[] = []
  let cursor = from
  while (cursor <= to) {
    const d = toDate(cursor)
    if (unit === 'day') {
      const dow = d.getUTCDay()
      columns.push({ start: cursor, days: 1, label: `${d.getUTCMonth() + 1}/${d.getUTCDate()}`, detail: WEEKDAYS[dow] ?? '', weekend: dow === 0 || dow === 6 })
      cursor = addDays(cursor, 1)
    } else if (unit === 'week') {
      const end = addDays(cursor, 6)
      columns.push({ start: cursor, days: 7, label: `${d.getUTCMonth() + 1}/${d.getUTCDate()}`, detail: '週', weekend: false })
      cursor = addDays(end, 1)
    } else {
      const next = addMonths(cursor, 1)
      const days = diffDays(next, cursor)
      const showYear = columns.length === 0 || d.getUTCMonth() === 0
      columns.push({ start: cursor, days, label: `${d.getUTCMonth() + 1}月`, detail: showYear ? `${d.getUTCFullYear()}年` : '', weekend: false })
      cursor = next
    }
  }
  return columns
}

/** 棒の範囲（開始日・期限の片方だけならその日の 1 日。どちらもなければ null） */
export function barRange(task: { startDate?: string | null; dueDate?: string | null }): { start: string; end: string } | null {
  const start = task.startDate ?? task.dueDate ?? null
  const end = task.dueDate ?? task.startDate ?? null
  return start !== null && end !== null ? { start, end } : null
}

export type BarPlacement = { left: number; width: number; clippedStart: boolean; clippedEnd: boolean; start: string; end: string }

/** 期間内の棒の位置（px）。期間からはみ出す部分は切り取り、切り取ったことを返す */
export function barPlacement(range: { start: string; end: string } | null, from: string, to: string, dayWidth: number): BarPlacement | null {
  if (range === null || range.end < from || range.start > to) return null
  const visibleStart = range.start < from ? from : range.start
  const visibleEnd = range.end > to ? to : range.end
  return {
    left: diffDays(visibleStart, from) * dayWidth,
    width: (diffDays(visibleEnd, visibleStart) + 1) * dayWidth,
    clippedStart: range.start < from,
    clippedEnd: range.end > to,
    start: range.start,
    end: range.end,
  }
}

/** 今日の線の位置（期間外なら null） */
export function todayOffset(today: string, from: string, to: string, dayWidth: number): number | null {
  if (today < from || today > to) return null
  return (diffDays(today, from) + 0.5) * dayWidth
}

/** ドラッグでの日程変更: 移動（開始日・期限を同じ日数ずらす）と伸縮（期限のみ。開始日より前にしない） */
export function movedDates(task: { startDate?: string | null; dueDate?: string | null }, mode: 'move' | 'resize', deltaDays: number) {
  const startDate = task.startDate ?? null
  const dueDate = task.dueDate ?? null
  if (mode === 'move') {
    return { startDate: startDate === null ? null : addDays(startDate, deltaDays), dueDate: dueDate === null ? null : addDays(dueDate, deltaDays) }
  }
  const range = barRange(task)
  if (range === null) return { startDate, dueDate }
  const proposed = addDays(range.end, deltaDays)
  return { startDate, dueDate: proposed < range.start ? range.start : proposed }
}

type Task = Schemas['TaskSummaryResponse']
type Status = Pick<Schemas['WorkflowStatusResponse'], 'id' | 'name' | 'sortOrder'>
type Milestone = Pick<Schemas['MilestoneSummaryResponse'], 'id' | 'name' | 'targetDate'>

export type ScheduleRowGroup = {
  key: string
  label: string
  kind: ScheduleGroup
  status?: Schemas['StatusRef']
  assignee?: Schemas['UserRef'] | null
  milestoneTargetDate?: string | null
  tasks: Task[]
}

/** まとめ方（担当者: 表示名順・担当者なしは最後、ステータス: 並び順、マイルストーン: 目標日の近い順・なしは最後） */
export function groupScheduleRows(tasks: Task[], group: ScheduleGroup, statuses: Status[] = [], milestones: Milestone[] = []): ScheduleRowGroup[] {
  if (group === 'none') return [{ key: 'all', label: 'すべてのタスク', kind: 'none', tasks }]
  const map = new Map<string, ScheduleRowGroup>()
  for (const t of tasks) {
    let key: string
    let entry: Omit<ScheduleRowGroup, 'tasks'>
    if (group === 'assignee') {
      key = t.assignee?.id ?? '__none'
      entry = { key, label: t.assignee?.displayName ?? '担当者なし', kind: group, assignee: t.assignee ?? null }
    } else if (group === 'status') {
      key = t.status.id
      entry = { key, label: t.status.name, kind: group, status: t.status }
    } else {
      key = t.milestone?.id ?? '__none'
      const target = milestones.find((m) => m.id === t.milestone?.id)?.targetDate ?? null
      entry = { key, label: t.milestone?.name ?? 'マイルストーンなし', kind: group, milestoneTargetDate: target }
    }
    const existing = map.get(key)
    if (existing) existing.tasks.push(t)
    else map.set(key, { ...entry, tasks: [t] })
  }
  const groups = [...map.values()]
  const none = (g: ScheduleRowGroup) => (g.key === '__none' ? 1 : 0)
  if (group === 'assignee') return groups.sort((a, b) => none(a) - none(b) || a.label.localeCompare(b.label, 'ja'))
  if (group === 'status') {
    const order = (g: ScheduleRowGroup) => statuses.find((s) => s.id === g.key)?.sortOrder ?? Number.MAX_SAFE_INTEGER
    return groups.sort((a, b) => order(a) - order(b))
  }
  return groups.sort((a, b) => none(a) - none(b)
    || (a.milestoneTargetDate ?? '9999-12-31').localeCompare(b.milestoneTargetDate ?? '9999-12-31')
    || a.label.localeCompare(b.label, 'ja'))
}

/** 期間の表示（2026/10/5〜2026/12/27） */
export function formatWindow(from: string, to: string): string {
  const f = (date: string) => {
    const d = toDate(date)
    return `${d.getUTCFullYear()}/${d.getUTCMonth() + 1}/${d.getUTCDate()}`
  }
  return `${f(from)}〜${f(to)}`
}
