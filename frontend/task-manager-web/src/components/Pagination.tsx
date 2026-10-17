import { Button } from './Button'

type PaginationProps = { page: number; pageSize: number; totalCount: number; totalPages: number; onChange: (page: number) => void }

/** ページ送り。「1〜50 件 / 全 123 件」を必ず表示する（ui-design.md §6.1） */
export function Pagination({ page, pageSize, totalCount, totalPages, onChange }: PaginationProps) {
  if (totalCount === 0) return null
  const from = (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, totalCount)
  return (
    <nav className="tm-row" aria-label="ページ送り" style={{ justifyContent: 'flex-end', marginTop: 'var(--space-3)' }}>
      <span className="tm-small tm-muted">{from}〜{to} 件 / 全 {totalCount} 件</span>
      <Button size="small" onClick={() => onChange(page - 1)} disabled={page <= 1}>前へ</Button>
      <span className="tm-small" aria-current="page">{page} / {Math.max(totalPages, 1)} ページ</span>
      <Button size="small" onClick={() => onChange(page + 1)} disabled={page >= totalPages}>次へ</Button>
    </nav>
  )
}
