import { useState } from 'react'
import { Link } from 'react-router'
import { DueDateLabel, PriorityIndicator, UserLabel, colorStyle } from '../../components/Display'
import { taskPath } from '../../utils/taskKey'
import { useUpdateTask, type TaskSummary } from './taskApi'
import type { StatusOption } from './TaskFields'
import styles from './Tasks.module.css'

const PRIORITY_RANK = { urgent: 0, high: 1, normal: 2, low: 3 } as const

/**
 * SCR-07 ボード。列 = ステータス。ドラッグで列を移動するとステータスが変わる。
 * キーボード・支援技術向けに、各カードの「ステータスを変更」メニューでも同じ操作ができる。
 * 列内の並びは優先度 → 期限 → 番号で固定（OD-11）。
 */
export function TaskStatusBoard({ projectKey, statuses, tasks, today, canEdit, truncated, onAdd }: {
  projectKey: string
  statuses: StatusOption[]
  tasks: TaskSummary[]
  today: string
  canEdit: boolean
  truncated: boolean
  onAdd: (statusId: string) => void
}) {
  const update = useUpdateTask(projectKey)
  const [dragOver, setDragOver] = useState<string | null>(null)
  const move = (task: TaskSummary, statusId: string) => {
    if (task.status.id !== statusId) update.mutate({ number: task.number, body: { statusId, version: task.version } })
  }
  const sorted = [...tasks].sort((a, b) =>
    PRIORITY_RANK[a.priority] - PRIORITY_RANK[b.priority]
    || (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999')
    || a.number - b.number)

  return (
    <>
      {truncated && <p className="tm-alert tm-alert--info tm-small">表示できるのは 100 件までです。絞り込みを使ってください。</p>}
      <div className={styles.board}>
        {statuses.map((status) => {
          const columnTasks = sorted.filter((t) => t.status.id === status.id)
          return (
            // oxlint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- ドロップ先。キーボードでは各カードの「ステータスを変更」で同じ操作ができる
            <section key={status.id} className={styles.column} aria-label={`${status.name}（${columnTasks.length} 件）`}
              data-drag-over={dragOver === status.id || undefined}
              onDragOver={(e) => { if (canEdit) { e.preventDefault(); setDragOver(status.id) } }}
              onDragLeave={() => setDragOver((v) => (v === status.id ? null : v))}
              onDrop={(e) => {
                e.preventDefault()
                setDragOver(null)
                const number = Number(e.dataTransfer.getData('text/plain'))
                const task = tasks.find((t) => t.number === number)
                if (task) move(task, status.id)
              }}>
              <h2 className={styles.columnTitle}>
                <span className="tm-badge" style={colorStyle(status.color)}>{status.name}</span>
                <span className="tm-small tm-muted">{columnTasks.length}</span>
              </h2>
              <ul className={styles.columnList}>
                {columnTasks.map((t) => (
                  // oxlint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- ドラッグ元。キーボードでは「ステータスを変更」を使う
                  <li key={t.key} className={styles.card} draggable={canEdit}
                    onDragStart={(e) => { e.dataTransfer.setData('text/plain', String(t.number)); e.dataTransfer.effectAllowed = 'move' }}>
                    <div className="tm-row"><span className="tm-key">{t.key}</span><span className="tm-spacer" /><PriorityIndicator priority={t.priority} hideNormal /></div>
                    <Link to={taskPath(projectKey, t.number)} className="tm-link--plain" style={{ fontWeight: 600, display: 'block', margin: 'var(--space-1) 0' }}>{t.title}</Link>
                    <div className="tm-row tm-small"><UserLabel user={t.assignee} /><span className="tm-spacer" /><DueDateLabel dueDate={t.dueDate} today={today} isOverdue={t.isOverdue} closed={t.status.category === 'done' || t.status.category === 'canceled'} /></div>
                    {canEdit && (
                      <label className="tm-small" style={{ display: 'block', marginTop: 'var(--space-2)' }}>
                        <span className="tm-visually-hidden">{t.key} の</span>ステータスを変更{' '}
                        <select className="tm-select tm-select--inline" value={t.status.id} onChange={(e) => move(t, e.target.value)}>
                          {statuses.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                        </select>
                      </label>
                    )}
                  </li>
                ))}
              </ul>
              {canEdit && <button type="button" className="tm-btn tm-btn--ghost tm-btn--small" onClick={() => onAdd(status.id)}>＋ 追加</button>}
            </section>
          )
        })}
      </div>
    </>
  )
}
