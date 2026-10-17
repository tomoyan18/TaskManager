import type { CSSProperties } from 'react'
import type { Schemas } from '../api/client'
import { CATEGORY_LABELS, PRIORITY_LABELS } from '../constants/labels'
import { diffDays, formatDate, formatDue } from '../utils/dates'
import { Icon } from './Icon'

export function colorStyle(color: Schemas['StatusColor']): CSSProperties {
  return {
    background: `var(--sc-${color}-bg)`,
    color: `var(--sc-${color}-fg)`,
    borderLeftColor: `var(--sc-${color}-bar)`,
  }
}

/** ステータス（色帯 + 名前。区分は読み上げ用に補足） */
export function StatusBadge({ status }: { status: Schemas['StatusRef'] }) {
  return (
    <span className="tm-badge" style={colorStyle(status.color)} title={`区分: ${CATEGORY_LABELS[status.category]}`}>
      {status.name}
    </span>
  )
}

export function TagChip({ tag, onRemove }: { tag: Schemas['TagRef']; onRemove?: () => void }) {
  return (
    <span className="tm-chip" style={{ background: `var(--sc-${tag.color}-bg)`, color: `var(--sc-${tag.color}-fg)` }}>
      {tag.name}
      {onRemove && <button type="button" aria-label={`タグ「${tag.name}」を外す`} onClick={onRemove}>✕</button>}
    </span>
  )
}

/** 優先度（記号 + 文字。色だけに頼らない） */
export function PriorityIndicator({ priority, hideNormal = false }: { priority: Schemas['TaskPriority']; hideNormal?: boolean }) {
  if (hideNormal && (priority === 'normal' || priority === 'low')) return null
  const mark = priority === 'urgent' ? '‼ ' : priority === 'high' ? '↑ ' : priority === 'low' ? '↓ ' : ''
  return <span className={`tm-priority tm-priority--${priority}`}>{mark}{PRIORITY_LABELS[priority]}</span>
}

/** 期限（期限切れは ⚠ + 「◯日超過」+ 警告色） */
export function DueDateLabel({ dueDate, today, isOverdue, closed = false }: { dueDate: string | null | undefined; today: string; isOverdue: boolean; closed?: boolean }) {
  if (dueDate === null || dueDate === undefined) return <span className="tm-muted tm-small">期限なし</span>
  const label = formatDue(dueDate, today, closed)
  const isToday = diffDays(dueDate, today) === 0 && !closed
  const className = isOverdue ? 'tm-due--overdue' : isToday ? 'tm-due--today' : undefined
  return (
    <span className={className} title={formatDate(dueDate)}>
      {isOverdue && <><Icon name="warning" size={14} /> </>}
      {label}
    </span>
  )
}

export function UserLabel({ user, emptyText = '未設定' }: { user: Schemas['UserRef'] | null | undefined; emptyText?: string }) {
  if (user === null || user === undefined) return <span className="tm-muted">{emptyText}</span>
  return (
    <span className="tm-user">
      <span className="tm-avatar" aria-hidden="true">{user.displayName.slice(0, 1)}</span>
      {user.displayName}
      {!user.isActive && <span className="tm-muted tm-small">（停止中）</span>}
    </span>
  )
}

export function ProgressBar({ done, total }: { done: number; total: number }) {
  const percent = total === 0 ? 0 : Math.round((done / total) * 100)
  return (
    <div>
      <div className="tm-progress" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={percent} aria-label={`進捗 ${percent}%`}>
        <div className="tm-progress__bar" style={{ width: `${percent}%` }} />
      </div>
      <div className="tm-small tm-muted">{done} / {total} 件完了（{percent}%）</div>
    </div>
  )
}
