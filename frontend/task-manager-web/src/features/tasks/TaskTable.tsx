import { Link } from 'react-router'
import { DueDateLabel, PriorityIndicator, StatusBadge, TagChip, UserLabel } from '../../components/Display'
import { Icon } from '../../components/Icon'
import { useCurrentUser } from '../auth/AuthProvider'
import { taskPath } from '../../utils/taskKey'
import { useUpdateTask, type TaskSummary, type UpdateTaskBody } from './taskApi'
import { AssigneeSelect, DateInput, PrioritySelect, StatusSelect } from './TaskFields'
import styles from './Tasks.module.css'

type Column = { key: string; label: string; sort?: string }
const COLUMNS: Column[] = [
  { key: 'key', label: 'キー', sort: 'number' },
  { key: 'title', label: 'タイトル', sort: 'title' },
  { key: 'status', label: 'ステータス', sort: 'status' },
  { key: 'priority', label: '優先度', sort: '-priority' },
  { key: 'assignee', label: '担当者', sort: 'assignee' },
  { key: 'due', label: '期限', sort: 'dueDate' },
  { key: 'milestone', label: 'マイルストーン' },
  { key: 'updated', label: '更新', sort: '-updatedAt' },
]

/** SCR-06 タスクの表（幅 768px 未満ではカード表示に切り替える） */
export function TaskTable({ projectKey, tasks, today, canEdit, sort, onSort, isFetching }: {
  projectKey: string
  tasks: TaskSummary[]
  today: string
  canEdit: boolean
  sort: string
  onSort: (sort: string) => void
  isFetching: boolean
}) {
  const user = useCurrentUser()
  const update = useUpdateTask(projectKey)
  const save = (task: TaskSummary, body: Omit<UpdateTaskBody, 'version'>) =>
    update.mutate({ number: task.number, body: { ...body, version: task.version } })

  return (
    <div aria-busy={isFetching || undefined}>
      <div className={`tm-table-wrap ${styles.tableOnly}`}>
        <table className="tm-table">
          <caption className="tm-visually-hidden">タスクの一覧</caption>
          <thead>
            <tr>
              {COLUMNS.map((c) => {
                const active = c.sort !== undefined && (sort === c.sort || sort === `-${c.sort}` || sort === c.sort.replace(/^-/, ''))
                const ariaSort = !active ? 'none' : sort.startsWith('-') ? 'descending' : 'ascending'
                return (
                  <th key={c.key} scope="col" aria-sort={c.sort ? ariaSort : undefined} className={c.key === 'milestone' ? styles.hideTablet : undefined}>
                    {c.sort ? (
                      <button type="button" onClick={() => onSort(active ? toggle(sort) : c.sort!)}>
                        {c.label}{active ? (sort.startsWith('-') ? ' ↓' : ' ↑') : ''}
                      </button>
                    ) : c.label}
                  </th>
                )
              })}
            </tr>
          </thead>
          <tbody>
            {tasks.map((t) => {
              const closed = t.status.category === 'done' || t.status.category === 'canceled'
              return (
                <tr key={t.key}>
                  <td className="tm-key">{t.key}</td>
                  <td className={styles.titleCell}>
                    {t.parent && <span className="tm-small tm-muted" aria-label={`親タスク ${t.parent.key}`}>└ </span>}
                    <Link to={taskPath(projectKey, t.number)} className="tm-link--plain" style={{ fontWeight: 600 }}>{t.title}</Link>
                    <span className="tm-row" style={{ gap: 'var(--space-2)', display: 'inline-flex', marginLeft: 'var(--space-2)' }}>
                      {t.subtaskCount > 0 && <span className="tm-small tm-muted">サブ {t.closedSubtaskCount}/{t.subtaskCount}</span>}
                      {t.commentCount > 0 && <span className="tm-small tm-muted" aria-label={`コメント ${t.commentCount} 件`}><Icon name="comment" size={13} />{t.commentCount}</span>}
                      {t.attachmentCount > 0 && <span className="tm-small tm-muted" aria-label={`添付 ${t.attachmentCount} 件`}><Icon name="paperclip" size={13} />{t.attachmentCount}</span>}
                      {t.tags.map((tag) => <TagChip key={tag.id} tag={tag} />)}
                    </span>
                  </td>
                  <td>
                    {canEdit
                      ? <StatusSelect projectKey={projectKey} inline value={t.status.id} aria-label={`${t.key} のステータス`} onChange={(statusId) => save(t, { statusId })} />
                      : <StatusBadge status={t.status} />}
                  </td>
                  <td>
                    {canEdit
                      ? <PrioritySelect inline value={t.priority} aria-label={`${t.key} の優先度`} onChange={(priority) => save(t, { priority })} />
                      : <PriorityIndicator priority={t.priority} />}
                  </td>
                  <td>
                    {canEdit
                      ? <AssigneeSelect projectKey={projectKey} currentUserId={user.id} current={t.assignee} inline value={t.assignee?.id ?? null}
                          aria-label={`${t.key} の担当者`} onChange={(assigneeUserId) => save(t, { assigneeUserId })} />
                      : <UserLabel user={t.assignee} />}
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {canEdit ? (
                      <div>
                        <DateInput inline today={today} showShortcuts={false} value={t.dueDate ?? null} aria-label={`${t.key} の期限`}
                          onChange={(dueDate) => save(t, { dueDate })} />
                        {t.dueDate && <DueDateLabel dueDate={t.dueDate} today={today} isOverdue={t.isOverdue} closed={closed} />}
                      </div>
                    ) : <DueDateLabel dueDate={t.dueDate} today={today} isOverdue={t.isOverdue} closed={closed} />}
                  </td>
                  <td className={styles.hideTablet}>{t.milestone?.name ?? <span className="tm-muted">—</span>}</td>
                  <td className="tm-small tm-muted" style={{ whiteSpace: 'nowrap' }}>{t.updatedAtUtc.slice(5, 10).replace('-', '/')}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      <ul className={styles.cardList} aria-label="タスクの一覧">
        {tasks.map((t) => (
          <li key={t.key} className={styles.card}>
            <div className="tm-row"><span className="tm-key">{t.key}</span><StatusBadge status={t.status} /><PriorityIndicator priority={t.priority} hideNormal /></div>
            <Link to={taskPath(projectKey, t.number)} className="tm-link--plain" style={{ fontWeight: 600, display: 'block', margin: 'var(--space-1) 0' }}>{t.title}</Link>
            <div className="tm-row tm-small"><UserLabel user={t.assignee} />・<DueDateLabel dueDate={t.dueDate} today={today} isOverdue={t.isOverdue} /></div>
          </li>
        ))}
      </ul>
    </div>
  )
}

function toggle(sort: string): string {
  return sort.startsWith('-') ? sort.slice(1) : `-${sort}`
}
