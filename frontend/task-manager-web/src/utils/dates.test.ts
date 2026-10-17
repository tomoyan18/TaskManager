import { afterEach, describe, expect, it, vi } from 'vitest'
import { addDays, diffDays, formatDate, formatDateTime, formatDue, nextMonday, todayIn } from './dates'

describe('formatDue（期限の表示）', () => {
  const today = '2026-10-08' // 木曜日

  it.each([
    ['2026-10-08', '今日'],
    ['2026-10-09', '明日'],
    ['2026-10-07', '1日超過'],
    ['2026-10-06', '2日超過'],
    ['2026-10-12', '10/12（月）'],
    ['2027-01-15', '2027/1/15（金）'],
  ])('%s → %s', (due, expected) => {
    expect(formatDue(due, today)).toBe(expected)
  })

  it('期限なし', () => {
    expect(formatDue(null, today)).toBe('期限なし')
    expect(formatDue(undefined, today)).toBe('期限なし')
  })

  it('完了・中止したタスクは超過と表示しない', () => {
    expect(formatDue('2026-10-06', today, true)).toBe('10/6（火）')
  })
})

describe('日付の計算', () => {
  it('年をまたいで日数を足す', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    expect(addDays('2028-02-28', 1)).toBe('2028-02-29')
  })

  it('日数の差', () => {
    expect(diffDays('2026-10-10', '2026-10-08')).toBe(2)
    expect(diffDays('2026-01-01', '2025-12-31')).toBe(1)
  })

  it.each([
    ['2026-10-08', '2026-10-12'], // 木 → 次の月曜
    ['2026-10-11', '2026-10-12'], // 日 → 翌日
    ['2026-10-12', '2026-10-19'], // 月 → 1 週間後
  ])('来週の月曜日: %s → %s', (today, expected) => {
    expect(nextMonday(today)).toBe(expected)
  })

  it('別の年は年を付けて表示する', () => {
    expect(formatDate('2027-01-15', '2026-10-08')).toBe('2027/1/15（金）')
    expect(formatDate('2026-10-09', '2026-10-08')).toBe('10/9（金）')
  })
})

describe('todayIn（タイムゾーンでの今日）', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('同じ時刻でもタイムゾーンで日付が変わる', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-07T16:30:00Z'))
    expect(todayIn('Asia/Tokyo')).toBe('2026-10-08')
    expect(todayIn('UTC')).toBe('2026-10-07')
    expect(todayIn('America/Los_Angeles')).toBe('2026-10-07')
  })
})

describe('formatDateTime（日時の相対表示）', () => {
  const now = new Date('2026-10-08T05:00:00Z') // 東京 14:00

  it.each([
    ['2026-10-08T04:59:40Z', 'たった今'],
    ['2026-10-08T04:55:00Z', '5分前'],
    ['2026-10-08T00:05:00Z', '09:05'],
    ['2026-10-07T16:00:00Z', '01:00'], // UTC では前日だが東京では同じ日
    ['2026-10-03T05:05:00Z', '10/3 14:05'],
    ['2025-10-03T05:05:00Z', '2025/10/3 14:05'],
  ])('%s → %s', (iso, expected) => {
    expect(formatDateTime(iso, 'Asia/Tokyo', now)).toBe(expected)
  })

  it('UTC の利用者には UTC の日付で表示する', () => {
    expect(formatDateTime('2026-10-07T16:00:00Z', 'UTC', now)).toBe('10/7 16:00')
  })
})
