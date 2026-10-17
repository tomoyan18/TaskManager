import { useEffect, useState, type ReactNode } from 'react'
import { ApiError } from '../api/client'
import { Button } from './Button'

/** 空状態（ui-design.md §9）: 理由と次の行動を示す */
export function EmptyState({ title, text, action }: { title: string; text?: string; action?: ReactNode }) {
  return (
    <div className="tm-empty">
      <p className="tm-empty__title">{title}</p>
      {text && <p className="tm-empty__text">{text}</p>}
      {action}
    </div>
  )
}

/** 読み込み失敗（ui-design.md §6.3 ErrorPanel） */
export function ErrorPanel({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const traceId = error instanceof ApiError ? error.traceId : null
  return (
    <div className="tm-empty" role="alert">
      <p className="tm-empty__title">読み込めませんでした</p>
      <p className="tm-empty__text">
        {error instanceof ApiError && error.status !== 0 && error.status < 500 ? error.title : '通信状態を確認して、もう一度お試しください。'}
      </p>
      {onRetry && <Button onClick={onRetry}>再試行</Button>}
      {traceId && <p className="tm-small tm-muted">問い合わせ番号: {traceId}</p>}
    </div>
  )
}

/** 読み込み中。300ms 未満の読み込みでは表示しない（ちらつき防止） */
export function Loading({ rows = 3, label = '読み込んでいます…' }: { rows?: number; label?: string }) {
  const [visible, setVisible] = useState(false)
  useEffect(() => {
    const timer = setTimeout(() => setVisible(true), 300)
    return () => clearTimeout(timer)
  }, [])
  return (
    <div role="status" aria-live="polite" className="tm-stack" style={{ gap: 'var(--space-3)', padding: 'var(--space-4) 0' }}>
      <span className="tm-visually-hidden">{label}</span>
      {visible && Array.from({ length: rows }, (_, i) => <div key={i} className="tm-skeleton" style={{ width: `${90 - i * 12}%` }} />)}
    </div>
  )
}

export function FormError({ message }: { message: string | null }) {
  if (message === null) return null
  return <div className="tm-alert tm-alert--error" role="alert" style={{ marginBottom: 'var(--space-4)' }}>{message}</div>
}
