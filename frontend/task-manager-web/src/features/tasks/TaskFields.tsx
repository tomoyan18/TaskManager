import type { Schemas } from '../../api/client'
import { useMembers, useMilestones, useStatuses, useTags } from '../../api/queries'
import { TagChip } from '../../components/Display'
import { CATEGORY_LABELS, CATEGORY_ORDER, PRIORITY_LABELS, PRIORITY_ORDER } from '../../constants/labels'
import { LIMITS } from '../../constants/limits'
import { addDays, nextMonday } from '../../utils/dates'

type SelectProps<T> = {
  id?: string
  value: T
  onChange: (value: T) => void
  disabled?: boolean
  inline?: boolean
  'aria-label'?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

const cls = (inline?: boolean) => (inline ? 'tm-select tm-select--inline' : 'tm-select')

/** ステータス（区分ごとに区切って表示。ui-design.md §6.2） */
export function StatusSelect({ projectKey, ...p }: SelectProps<string> & { projectKey: string }) {
  const statuses = useStatuses(projectKey)
  const list = statuses.data ?? []
  return (
    <select id={p.id} className={cls(p.inline)} value={p.value} disabled={p.disabled || statuses.isPending}
      onChange={(e) => p.onChange(e.target.value)} aria-label={p['aria-label']} aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']}>
      {CATEGORY_ORDER.map((category) => {
        const options = list.filter((s) => s.category === category)
        return options.length === 0 ? null : (
          <optgroup key={category} label={CATEGORY_LABELS[category]}>
            {options.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </optgroup>
        )
      })}
    </select>
  )
}

export function PrioritySelect(p: SelectProps<Schemas['TaskPriority']>) {
  return (
    <select id={p.id} className={cls(p.inline)} value={p.value} disabled={p.disabled}
      onChange={(e) => p.onChange(e.target.value as Schemas['TaskPriority'])} aria-label={p['aria-label']}>
      {PRIORITY_ORDER.map((v) => <option key={v} value={v}>{PRIORITY_LABELS[v]}</option>)}
    </select>
  )
}

/** 担当者（担当者になれる人 = 編集者以上・利用中。先頭に「自分」「担当者なし」） */
export function AssigneeSelect({ projectKey, currentUserId, current, ...p }: SelectProps<string | null> & {
  projectKey: string
  currentUserId: string
  /** 現在の担当者（停止中・閲覧者になった人でも表示を崩さないため） */
  current?: Schemas['UserRef'] | null
}) {
  const members = useMembers(projectKey)
  const assignable = (members.data ?? []).filter((m) => m.role !== 'viewer' && m.user.isActive)
  const includesMe = assignable.some((m) => m.user.id === currentUserId)
  const others = assignable.filter((m) => m.user.id !== currentUserId)
  const currentMissing = current && !assignable.some((m) => m.user.id === current.id)
  return (
    <select id={p.id} className={cls(p.inline)} value={p.value ?? ''} disabled={p.disabled || members.isPending}
      onChange={(e) => p.onChange(e.target.value === '' ? null : e.target.value)} aria-label={p['aria-label']}
      aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']}>
      <option value="">担当者なし</option>
      {includesMe && <option value={currentUserId}>自分</option>}
      {currentMissing && <option value={current.id}>{current.displayName}（{current.isActive ? '担当者にできません' : '停止中'}）</option>}
      {others.map((m) => <option key={m.user.id} value={m.user.id}>{m.user.displayName}</option>)}
    </select>
  )
}

export function MilestoneSelect({ projectKey, ...p }: SelectProps<string | null> & { projectKey: string }) {
  const milestones = useMilestones(projectKey)
  const list = milestones.data ?? []
  return (
    <select id={p.id} className={cls(p.inline)} value={p.value ?? ''} disabled={p.disabled || milestones.isPending}
      onChange={(e) => p.onChange(e.target.value === '' ? null : e.target.value)} aria-label={p['aria-label']}
      aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']}>
      <option value="">なし</option>
      {list.map((m) => (
        <option key={m.id} value={m.id} disabled={m.state === 'closed' && m.id !== p.value}>
          {m.name}{m.state === 'closed' ? '（終了）' : ''}
        </option>
      ))}
    </select>
  )
}

/** 期限・開始日（ブラウザ標準の日付入力 + よく使う日付のショートカット） */
export function DateInput({ today, showShortcuts = true, ...p }: SelectProps<string | null> & { today: string; showShortcuts?: boolean }) {
  return (
    <div className="tm-stack" style={{ gap: 'var(--space-1)' }}>
      <input id={p.id} type="date" className={p.inline ? 'tm-input tm-select--inline' : 'tm-input'} value={p.value ?? ''}
        min="2000-01-01" max="2099-12-31" disabled={p.disabled} aria-label={p['aria-label']}
        aria-invalid={p['aria-invalid']} aria-describedby={p['aria-describedby']}
        onChange={(e) => p.onChange(e.target.value === '' ? null : e.target.value)} />
      {showShortcuts && !p.disabled && (
        <div className="tm-row" style={{ gap: 'var(--space-1)' }}>
          <button type="button" className="tm-btn tm-btn--small tm-btn--ghost" onClick={() => p.onChange(today)}>今日</button>
          <button type="button" className="tm-btn tm-btn--small tm-btn--ghost" onClick={() => p.onChange(addDays(today, 1))}>明日</button>
          <button type="button" className="tm-btn tm-btn--small tm-btn--ghost" onClick={() => p.onChange(nextMonday(today))}>来週の月曜</button>
          {p.value !== null && <button type="button" className="tm-btn tm-btn--small tm-btn--ghost" onClick={() => p.onChange(null)}>なし</button>}
        </div>
      )}
    </div>
  )
}

/** タグ（複数選択。10 件を超えると選べない理由を表示） */
export function TagPicker({ projectKey, value, onChange, disabled, id }: {
  projectKey: string
  value: string[]
  onChange: (ids: string[]) => void
  disabled?: boolean
  id?: string
}) {
  const tags = useTags(projectKey)
  const all = tags.data ?? []
  const selected = all.filter((t) => value.includes(t.id))
  const remaining = all.filter((t) => !value.includes(t.id))
  const full = value.length >= LIMITS.tagsPerTask
  return (
    <div className="tm-stack" style={{ gap: 'var(--space-2)' }}>
      <div className="tm-row">
        {selected.length === 0 && <span className="tm-muted tm-small">なし</span>}
        {selected.map((t) => (
          <TagChip key={t.id} tag={t} onRemove={disabled ? undefined : () => onChange(value.filter((v) => v !== t.id))} />
        ))}
      </div>
      {!disabled && remaining.length > 0 && (
        <select id={id} className="tm-select tm-select--inline" value="" disabled={full}
          aria-label="タグを追加" onChange={(e) => e.target.value && onChange([...value, e.target.value])}>
          <option value="">{full ? `タグは${LIMITS.tagsPerTask}個までです` : '＋ タグを追加'}</option>
          {remaining.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
        </select>
      )}
      {!disabled && all.length === 0 && <span className="tm-small tm-muted">このプロジェクトにはタグがありません（設定で追加できます）。</span>}
    </div>
  )
}

export type StatusOption = Schemas['WorkflowStatusResponse']
