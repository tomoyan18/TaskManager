// 日付・日時の表示（docs/design/ux-design.md §6）
const WEEKDAYS = ['日', '月', '火', '水', '木', '金', '土']

/** タイムゾーンでの今日（YYYY-MM-DD） */
export function todayIn(timeZone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
}

function parseDate(date: string): Date {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(Date.UTC(y ?? 1970, (m ?? 1) - 1, d ?? 1))
}

/** 日数の差（a - b） */
export function diffDays(a: string, b: string): number {
  return Math.round((parseDate(a).getTime() - parseDate(b).getTime()) / 86_400_000)
}

/** 10/9（木）、別の年は 2027/1/15（金） */
export function formatDate(date: string, today?: string): string {
  const d = parseDate(date)
  const sameYear = today === undefined || today.slice(0, 4) === date.slice(0, 4)
  const md = `${d.getUTCMonth() + 1}/${d.getUTCDate()}（${WEEKDAYS[d.getUTCDay()]}）`
  return sameYear ? md : `${d.getUTCFullYear()}/${md}`
}

/** 期限の表示: 今日 / 明日 / 昨日 / ◯日超過 / 10/9（木） */
export function formatDue(date: string | null | undefined, today: string, closed = false): string {
  if (date === null || date === undefined) return '期限なし'
  const diff = diffDays(date, today)
  if (diff === 0) return '今日'
  if (diff === 1) return '明日'
  if (diff < 0 && !closed) return `${-diff}日超過`
  return formatDate(date, today)
}

/** 日時の表示: ◯分前 / 14:05 / 10/3 14:05 / 2025/10/3 14:05 */
export function formatDateTime(iso: string, timeZone: string, now: Date = new Date()): string {
  const date = new Date(iso)
  const minutes = Math.floor((now.getTime() - date.getTime()) / 60_000)
  if (minutes >= 0 && minutes < 1) return 'たった今'
  if (minutes >= 0 && minutes < 60) return `${minutes}分前`
  const parts = (d: Date) => {
    const f = new Intl.DateTimeFormat('ja-JP', {
      timeZone, year: 'numeric', month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    }).formatToParts(d)
    const get = (type: string) => f.find((p) => p.type === type)?.value ?? ''
    return { y: get('year'), m: get('month'), d: get('day'), time: `${get('hour')}:${get('minute')}` }
  }
  const p = parts(date)
  const n = parts(now)
  if (p.y === n.y && p.m === n.m && p.d === n.d) return p.time
  if (p.y === n.y) return `${p.m}/${p.d} ${p.time}`
  return `${p.y}/${p.m}/${p.d} ${p.time}`
}

/** 完全な日時（ツールチップ・スクリーンリーダー用） */
export function formatFullDateTime(iso: string, timeZone: string): string {
  return new Intl.DateTimeFormat('ja-JP', { timeZone, dateStyle: 'long', timeStyle: 'short' }).format(new Date(iso))
}

/** YYYY-MM-DD に日数を足す */
export function addDays(date: string, days: number): string {
  const d = parseDate(date)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** 来週の月曜日 */
export function nextMonday(today: string): string {
  const day = parseDate(today).getUTCDay()
  const delta = ((8 - day) % 7) || 7
  return addDays(today, delta)
}
