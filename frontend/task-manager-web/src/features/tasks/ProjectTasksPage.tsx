import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useOutletContext } from 'react-router'
import { apiClient, call, type Schemas } from '../../api/client'
import type { paths } from '../../api/schema'
import { queryKeys, useStatuses } from '../../api/queries'
import { Button } from '../../components/Button'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { Pagination } from '../../components/Pagination'
import { NEW_TASK_EVENT } from '../../hooks/useKeyboardShortcuts'
import { usePageTitle } from '../../hooks/usePageTitle'
import { useTaskListParams } from '../../hooks/useTaskListParams'
import { addDays, todayIn } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import { TaskFilterBar } from './TaskFilterBar'
import { ProjectStatusChartView } from './ProjectStatusChartView'
import { TaskFormDialog, type TaskFormDefaults } from './TaskFormDialog'
import { TaskScheduleView } from './TaskScheduleView'
import { TaskStatusBoard } from './TaskStatusBoard'
import { TaskTable } from './TaskTable'

type Project = Schemas['ProjectDetailResponse']
type ListQuery = NonNullable<paths['/api/v1/projects/{projectKey}/tasks']['get']['parameters']['query']>

/** 表示形式の切り替え（gantt・chart は Phase 9 で追加。ui-design.md SCR-06・07・20・21） */
const VIEWS = [
  { view: 'table', label: '表' },
  { view: 'board', label: 'ボード' },
  { view: 'gantt', label: 'ガント' },
  { view: 'chart', label: '円グラフ' },
] as const

/** SCR-06 / SCR-07 / SCR-20 / SCR-21 タスク一覧（表・ボード・ガント・円グラフ） */
export function ProjectTasksPage() {
  const project = useOutletContext<Project>()
  const user = useCurrentUser()
  const params = useTaskListParams()
  const today = todayIn(user.timeZoneId)
  const statuses = useStatuses(project.key)
  const canEdit = !project.isArchived && (project.myRole === 'editor' || project.myRole === 'owner')
  const [createDefaults, setCreateDefaults] = useState<TaskFormDefaults | null>(null)
  // ボードの「完了」列は直近 14 日に完了したものだけを表示する（画面を開いた時点を基準にする）
  const [boardCompletedSince] = useState(() => new Date(Date.now() - 14 * 86_400_000).toISOString())
  usePageTitle(params.view === 'gantt' ? 'ガント' : params.view === 'chart' ? 'ステータス別の割合' : 'タスク', project.name)
  const isList = params.view === 'table' || params.view === 'board'

  useEffect(() => {
    const onNewTask = () => {
      if (canEdit) setCreateDefaults({})
    }
    window.addEventListener(NEW_TASK_EVENT, onNewTask)
    return () => window.removeEventListener(NEW_TASK_EVENT, onNewTask)
  }, [canEdit])

  const query: ListQuery = {
    statusId: params.statusId.length > 0 ? params.statusId : undefined,
    priority: params.priority.length > 0 ? params.priority : undefined,
    tagId: params.tagId ? [params.tagId] : undefined,
    q: params.q || undefined,
    sort: params.view === 'board' ? '-priority' : params.sort,
    page: params.view === 'board' ? 1 : params.page,
    pageSize: params.view === 'board' ? 100 : 50,
    includeClosed: params.view === 'board' || params.includeClosed || undefined,
    category: params.view === 'board'
      ? (params.showCanceled ? ['notStarted', 'inProgress', 'done', 'canceled'] : ['notStarted', 'inProgress', 'done'])
      : undefined,
    completedSince: params.view === 'board' ? boardCompletedSince : undefined,
  }
  if (params.assignee === 'me') query.assignedToMe = true
  else if (params.assignee === 'none') query.unassigned = true
  else if (params.assignee !== '') query.assigneeUserId = [params.assignee]
  if (params.milestoneId === 'none') query.noMilestone = true
  else if (params.milestoneId !== '') query.milestoneId = params.milestoneId
  if (params.due === 'overdue') query.overdue = true
  if (params.due === 'today') { query.dueFrom = today; query.dueTo = today }
  if (params.due === 'week') { query.dueFrom = today; query.dueTo = addDays(today, 7) }

  const tasks = useQuery({
    queryKey: queryKeys.tasks(project.key, { ...query, completedSince: params.view === 'board' ? 'recent' : undefined }),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks', { params: { path: { projectKey: project.key }, query } })),
    placeholderData: keepPreviousData,
    // ガント・円グラフでは一覧を取得しない（それぞれ専用の API を使う）
    enabled: isList,
  })

  const items = tasks.data?.items ?? []
  const filtered = params.activeCount > 0
  return (
    <div>
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <div className="tm-row" role="group" aria-label="表示形式">
          {VIEWS.map(({ view, label }) => (
            <Button key={view} size="small" variant={params.view === view ? 'primary' : 'secondary'} aria-pressed={params.view === view}
              onClick={() => params.update({ view: view === 'table' ? null : view })}>{label}</Button>
          ))}
        </div>
        <span className="tm-spacer" />
        {canEdit && (
          <Button variant="primary" onClick={() => setCreateDefaults({})} aria-keyshortcuts="N">
            <Icon name="plus" size={16} />タスクを追加
          </Button>
        )}
      </div>
      {params.view === 'gantt' && <TaskScheduleView project={project} params={params} today={today} canEdit={canEdit} />}
      {params.view === 'chart' && <ProjectStatusChartView projectKey={project.key} canEdit={canEdit} onAdd={() => setCreateDefaults({})} />}
      {isList && <TaskFilterBar project={project} params={params} />}
      {isList && tasks.isPending && <Loading rows={6} />}
      {isList && tasks.isError && <ErrorPanel error={tasks.error} onRetry={() => void tasks.refetch()} />}
      {tasks.isSuccess && items.length === 0 && params.view === 'table' && (
        filtered || tasks.data.totalCount === 0 && params.page > 1
          ? <EmptyState title="条件に合うタスクはありません" text="絞り込み条件を変えてみてください。" action={<Button onClick={params.clear}>条件をクリア</Button>} />
          : <EmptyState title="まだタスクがありません" text="最初のタスクを追加してみましょう。"
              action={canEdit ? <Button variant="primary" onClick={() => setCreateDefaults({})}>タスクを追加</Button> : undefined} />
      )}
      {tasks.isSuccess && items.length > 0 && params.view === 'table' && (
        <>
          <TaskTable projectKey={project.key} tasks={items} today={today} canEdit={canEdit} sort={params.sort}
            onSort={(sort) => params.update({ sort: sort === '-updatedAt' ? null : sort })} isFetching={tasks.isFetching} />
          <Pagination page={tasks.data.page} pageSize={tasks.data.pageSize} totalCount={tasks.data.totalCount}
            totalPages={tasks.data.totalPages} onChange={(page) => params.update({ page: page === 1 ? null : page }, false)} />
        </>
      )}
      {tasks.isSuccess && params.view === 'board' && statuses.data && (
        <TaskStatusBoard projectKey={project.key} statuses={statuses.data.filter((s) => s.category !== 'canceled' || params.showCanceled)}
          tasks={items} today={today} canEdit={canEdit} truncated={tasks.data.totalCount > items.length}
          onAdd={(statusId) => setCreateDefaults({ statusId })} />
      )}
      {params.view === 'board' && (
        <p className="tm-small tm-muted">
          完了の列は直近 14 日に完了したタスクを表示しています。{' '}
          <Link to="?view=board&showCanceled=true" className="tm-link" replace>中止も表示</Link>
        </p>
      )}
      {createDefaults !== null && (
        <TaskFormDialog projectKey={project.key} isOpen onOpenChange={(open) => !open && setCreateDefaults(null)} defaults={createDefaults} />
      )}
    </div>
  )
}
