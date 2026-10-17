import { describe, expect, it } from 'vitest'
import * as f from '../test/fixtures'
import {
  addMonths, barPlacement, barRange, columnsFor, defaultStart, formatWindow, groupScheduleRows, mondayOf, movedDates,
  parseGroup, parseStart, parseUnit, shiftStart, todayOffset, windowFor,
} from './schedule'

describe('ガントの期間（OD-40）', () => {
  it('日は 14 日、週は月曜始まりの 12 週、月は 12 か月', () => {
    expect(windowFor('day', '2026-10-05')).toEqual({ from: '2026-10-05', to: '2026-10-18' })
    // 2026-10-08 は木曜日 → 週の始まりは 10/5（月）
    expect(windowFor('week', '2026-10-08')).toEqual({ from: '2026-10-05', to: '2026-12-27' })
    expect(windowFor('month', '2026-10-08')).toEqual({ from: '2026-10-01', to: '2027-09-30' })
  })

  it('月の期間は閏年でも API の上限（366 日）以内', () => {
    const { from, to } = windowFor('month', '2027-03-15')
    expect(from).toBe('2027-03-01')
    expect(to).toBe('2028-02-29')
    const days = (Date.parse(to) - Date.parse(from)) / 86_400_000 + 1
    expect(days).toBeLessThanOrEqual(366)
  })

  it('「今日」は今日の少し前から、前へ・次へは日 7 日・週 4 週・月 3 か月ずつ', () => {
    expect(defaultStart('day', '2026-10-08')).toBe('2026-10-05')
    expect(defaultStart('week', '2026-10-08')).toBe('2026-09-28')
    expect(defaultStart('month', '2026-10-08')).toBe('2026-09-01')
    expect(shiftStart('day', '2026-10-05', 1)).toBe('2026-10-12')
    expect(shiftStart('week', '2026-10-05', -1)).toBe('2026-09-07')
    expect(shiftStart('month', '2026-11-01', 1)).toBe('2027-02-01')
  })

  it('月曜日・月の加算は年をまたいでも正しい', () => {
    expect(mondayOf('2027-01-03')).toBe('2026-12-28') // 日曜日
    expect(mondayOf('2026-10-05')).toBe('2026-10-05')
    expect(addMonths('2026-11-30', 3)).toBe('2027-02-01')
  })

  it('URL の不正な値は既定に戻す', () => {
    expect(parseUnit('year')).toBe('week')
    expect(parseUnit('day')).toBe('day')
    expect(parseGroup('priority')).toBe('none')
    expect(parseGroup('milestone')).toBe('milestone')
    expect(parseStart('2026-02-30')).toBeNull()
    expect(parseStart('1999-12-31')).toBeNull()
    expect(parseStart('<script>')).toBeNull()
    expect(parseStart('2026-10-05')).toBe('2026-10-05')
  })

  it('期間の表示', () => {
    expect(formatWindow('2026-10-05', '2026-12-27')).toBe('2026/10/5〜2026/12/27')
  })
})

describe('ガントの目盛り', () => {
  it('日は 1 日 1 列で土日を区別する', () => {
    const columns = columnsFor('day', '2026-10-09', '2026-10-12')
    expect(columns.map((c) => `${c.label}${c.detail}`)).toEqual(['10/9金', '10/10土', '10/11日', '10/12月'])
    expect(columns.map((c) => c.weekend)).toEqual([false, true, true, false])
  })

  it('週は 7 日、月はその月の日数', () => {
    expect(columnsFor('week', '2026-10-05', '2026-10-18').map((c) => [c.label, c.days])).toEqual([['10/5', 7], ['10/12', 7]])
    const months = columnsFor('month', '2026-12-01', '2027-02-28')
    expect(months.map((c) => [c.label, c.detail, c.days])).toEqual([['12月', '2026年', 31], ['1月', '2027年', 31], ['2月', '', 28]])
  })
})

describe('ガントの棒', () => {
  it('開始日・期限の片方だけなら 1 日の棒、どちらもなければなし', () => {
    expect(barRange({ startDate: '2026-10-01', dueDate: '2026-10-09' })).toEqual({ start: '2026-10-01', end: '2026-10-09' })
    expect(barRange({ startDate: null, dueDate: '2026-10-09' })).toEqual({ start: '2026-10-09', end: '2026-10-09' })
    expect(barRange({ startDate: '2026-10-01', dueDate: null })).toEqual({ start: '2026-10-01', end: '2026-10-01' })
    expect(barRange({ startDate: null, dueDate: null })).toBeNull()
  })

  it('位置と幅は日数 × 1 日の幅。期間からはみ出す部分は切り取る', () => {
    expect(barPlacement({ start: '2026-10-06', end: '2026-10-08' }, '2026-10-05', '2026-10-18', 10))
      .toEqual({ left: 10, width: 30, clippedStart: false, clippedEnd: false, start: '2026-10-06', end: '2026-10-08' })
    expect(barPlacement({ start: '2026-09-01', end: '2026-10-06' }, '2026-10-05', '2026-10-18', 10))
      .toMatchObject({ left: 0, width: 20, clippedStart: true, clippedEnd: false })
    expect(barPlacement({ start: '2026-10-17', end: '2026-11-30' }, '2026-10-05', '2026-10-18', 10))
      .toMatchObject({ left: 120, width: 20, clippedStart: false, clippedEnd: true })
    expect(barPlacement({ start: '2026-10-19', end: '2026-10-20' }, '2026-10-05', '2026-10-18', 10)).toBeNull()
  })

  it('今日の線は期間内のときだけ、その日の中央', () => {
    expect(todayOffset('2026-10-08', '2026-10-05', '2026-10-18', 10)).toBe(35)
    expect(todayOffset('2026-10-19', '2026-10-05', '2026-10-18', 10)).toBeNull()
  })

  it('ドラッグの移動は開始日と期限を同じ日数ずらし、伸縮は期限だけを変えて開始日より前にしない', () => {
    expect(movedDates({ startDate: '2026-10-01', dueDate: '2026-10-09' }, 'move', 3)).toEqual({ startDate: '2026-10-04', dueDate: '2026-10-12' })
    expect(movedDates({ startDate: null, dueDate: '2026-10-09' }, 'move', -2)).toEqual({ startDate: null, dueDate: '2026-10-07' })
    expect(movedDates({ startDate: '2026-10-01', dueDate: '2026-10-09' }, 'resize', 2)).toEqual({ startDate: '2026-10-01', dueDate: '2026-10-11' })
    expect(movedDates({ startDate: '2026-10-01', dueDate: '2026-10-09' }, 'resize', -20)).toEqual({ startDate: '2026-10-01', dueDate: '2026-10-01' })
    // 開始日だけのタスクを伸ばすと期限が付く
    expect(movedDates({ startDate: '2026-10-01', dueDate: null }, 'resize', 4)).toEqual({ startDate: '2026-10-01', dueDate: '2026-10-05' })
  })
})

describe('ガントのまとめ方', () => {
  const s = f.statuses
  const ref = (i: number) => ({ id: s[i]!.id, name: s[i]!.name, category: s[i]!.category, color: s[i]!.color })
  const tasks = [
    f.task({ key: 'SALES-1', number: 1, status: ref(1), assignee: { id: 'u-2', displayName: '山田 太郎', isActive: true }, milestone: { id: 'm-2', name: '11月', state: 'open' } }),
    f.task({ key: 'SALES-2', number: 2, status: ref(0), assignee: null, milestone: null }),
    f.task({ key: 'SALES-3', number: 3, status: ref(0), assignee: { id: 'u-1', displayName: '佐藤 花子', isActive: true }, milestone: { id: 'm-1', name: '10月', state: 'open' } }),
  ]

  it('なしは 1 つのグループ', () => {
    expect(groupScheduleRows(tasks, 'none')).toHaveLength(1)
  })

  it('担当者は表示名順で担当者なしを最後にする', () => {
    expect(groupScheduleRows(tasks, 'assignee').map((g) => g.label)).toEqual(['佐藤 花子', '山田 太郎', '担当者なし'])
  })

  it('ステータスはステータスの並び順', () => {
    expect(groupScheduleRows(tasks, 'status', s).map((g) => [g.label, g.tasks.length])).toEqual([['未着手', 2], ['対応中', 1]])
  })

  it('マイルストーンは目標日の近い順でなしを最後にし、目標日を持つ', () => {
    const milestones = [{ id: 'm-1', name: '10月', targetDate: '2026-10-31' }, { id: 'm-2', name: '11月', targetDate: '2026-11-30' }]
    const groups = groupScheduleRows(tasks, 'milestone', s, milestones)
    expect(groups.map((g) => [g.label, g.milestoneTargetDate])).toEqual([['10月', '2026-10-31'], ['11月', '2026-11-30'], ['マイルストーンなし', null]])
  })
})
