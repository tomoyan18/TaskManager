import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { DueDateLabel, PriorityIndicator } from '../../components/Display'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { usePageTitle } from '../../hooks/usePageTitle'
import { taskPath } from '../../utils/taskKey'
import { StatusSelect } from '../tasks/TaskFields'
import { useUpdateTask } from '../tasks/taskApi'
import { MyStatusChartView } from './MyStatusChartView'
import styles from './MyTasks.module.css'

type Item = Schemas['TaskSummaryWithProjectResponse']
const BUCKETS: { key: keyof Schemas['MyTaskBuckets']; label: string; tone?: 'danger' | 'warning' }[] = [
  { key: 'overdue', label: '期限切れ', tone: 'danger' },
  { key: 'today', label: '今日まで', tone: 'warning' },
  { key: 'thisWeek', label: '今週' },
  { key: 'later', label: 'それ以降' },
  { key: 'noDueDate', label: '期限なし' },
]

/** SCR-04 ホーム（自分のタスク。REQ-MY-01）。Phase 9 で表示「ステータス別の割合」（REQ-CHT-02）を追加 */
export function MyTasksPage() {
  const [params, setParams] = useSearchParams()
  const chart = params.get('view') === 'chart'
  usePageTitle(chart ? 'ステータス別の割合' : null, '自分のタスク')
  const setView = (view: 'list' | 'chart') => setParams(view === 'chart' ? { view: 'chart' } : {})
  const setProject = (key: string | null) => setParams(key === null ? { view: 'chart' } : { view: 'chart', project: key })
  return (
    <div className="tm-stack">
      <div className="tm-row">
        <h1 className="tm-page-title">自分のタスク</h1>
        <span className="tm-spacer" />
        <div className="tm-row" role="group" aria-label="表示">
          <Button size="small" variant={chart ? 'secondary' : 'primary'} aria-pressed={!chart} onClick={() => setView('list')}>期限別</Button>
          <Button size="small" variant={chart ? 'primary' : 'secondary'} aria-pressed={chart} onClick={() => setView('chart')}>ステータス別の割合</Button>
        </div>
      </div>
      {chart ? <MyStatusChartView projectKey={params.get('project')?.toUpperCase() || null} onProjectChange={setProject} /> : <MyTaskList />}
    </div>
  )
}

function MyTaskList() {
  const [includeCompleted, setIncludeCompleted] = useState(false)
  const data = useQuery({
    queryKey: queryKeys.myTasks(includeCompleted),
    queryFn: () => call(() => apiClient.GET('/api/v1/me/tasks', { params: { query: { includeRecentlyCompleted: includeCompleted } } })),
  })
  return (
    <>
      <div className="tm-row">
        <span className="tm-spacer" />
        <label className="tm-checkbox tm-small">
          <input type="checkbox" checked={includeCompleted} onChange={(e) => setIncludeCompleted(e.target.checked)} />
          最近完了したものも表示
        </label>
      </div>
      {data.isPending && <Loading rows={6} />}
      {data.isError && <ErrorPanel error={data.error} onRetry={() => void data.refetch()} />}
      {data.isSuccess && <MyTaskBuckets data={data.data} />}
    </>
  )
}

function MyTaskBuckets({ data }: { data: Schemas['MyTasksResponse'] }) {
  const total = BUCKETS.reduce((sum, b) => sum + data.buckets[b.key].length, 0)
  if (total === 0 && data.recentlyCompleted.length === 0) {
    return <EmptyState title="担当しているタスクはありません" text="新しく割り当てられると、ここに表示されます。"
      action={<Link to="/projects" className="tm-btn">プロジェクトを見る</Link>} />
  }
  return (
    <>
      {total === 0 && <EmptyState title="今日やることはすべて完了しています" text="お疲れさまでした。" />}
      {data.truncated && <p className="tm-alert tm-alert--info tm-small">担当タスクが多いため、500 件まで表示しています。</p>}
      {BUCKETS.map((b) => (
        <Bucket key={b.key} label={b.label} tone={b.tone} items={data.buckets[b.key]} today={data.today} defaultOpen={b.key !== 'later' && b.key !== 'noDueDate'} />
      ))}
      {data.recentlyCompleted.length > 0 && <Bucket label="最近完了したタスク（7 日以内）" items={data.recentlyCompleted} today={data.today} defaultOpen />}
    </>
  )
}

function Bucket({ label, tone, items, today, defaultOpen }: { label: string; tone?: 'danger' | 'warning'; items: Item[]; today: string; defaultOpen: boolean }) {
  const [open, setOpen] = useState(defaultOpen || items.length > 0)
  const id = `bucket-${label}`
  return (
    <section className={styles.bucket} data-tone={tone}>
      <h2 className={styles.heading}>
        <button type="button" aria-expanded={open} aria-controls={id} onClick={() => setOpen((v) => !v)}>
          {open ? '▾' : '▸'} {label}（{items.length}）
        </button>
      </h2>
      {open && (
        <div id={id}>
          {items.length === 0 ? <p className="tm-small tm-muted" style={{ margin: 'var(--space-2) var(--space-4)' }}>ありません</p> : (
            <ul className={styles.list}>
              {items.map((t) => <MyTaskRow key={t.key} task={t} today={today} />)}
            </ul>
          )}
        </div>
      )}
    </section>
  )
}

function MyTaskRow({ task, today }: { task: Item; today: string }) {
  const update = useUpdateTask(task.project.key)
  const closed = task.status.category === 'done' || task.status.category === 'canceled'
  return (
    <li className={styles.row}>
      <span className={styles.due}><DueDateLabel dueDate={task.dueDate} today={today} isOverdue={task.isOverdue} closed={closed} /></span>
      <span className="tm-key">{task.key}</span>
      <Link to={taskPath(task.project.key, task.number)} className={`tm-link--plain ${styles.title}`}>{task.title}</Link>
      <span className="tm-small tm-muted">{task.project.name}</span>
      <StatusSelect projectKey={task.project.key} inline value={task.status.id} aria-label={`${task.key} のステータス`}
        onChange={(statusId) => update.mutate({ number: task.number, body: { statusId, version: task.version } })} />
      <PriorityIndicator priority={task.priority} hideNormal />
    </li>
  )
}
