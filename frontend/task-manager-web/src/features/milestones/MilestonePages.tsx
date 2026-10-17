import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useOutletContext, useParams } from 'react-router'
import { z } from 'zod'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { queryKeys, useMilestones } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { DueDateLabel, ProgressBar } from '../../components/Display'
import { Dialog } from '../../components/Dialog'
import { Field } from '../../components/Field'
import { EmptyState, ErrorPanel, FormError, Loading } from '../../components/Feedback'
import { MarkdownView } from '../../components/MarkdownView'
import { useToast } from '../../components/Toast'
import { MILESTONE_STATE_LABELS } from '../../constants/labels'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { todayIn } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import { NotFoundPage } from '../shell/NotFoundPage'
import { TaskFormDialog } from '../tasks/TaskFormDialog'
import { TaskTable } from '../tasks/TaskTable'
import { errorMessage } from '../tasks/taskApi'

type Project = Schemas['ProjectDetailResponse']
const canManage = (p: Project) => !p.isArchived && (p.myRole === 'editor' || p.myRole === 'owner')

/** SCR-10 マイルストーン一覧 */
export function MilestoneListPage() {
  const project = useOutletContext<Project>()
  const user = useCurrentUser()
  usePageTitle('マイルストーン', project.name)
  const milestones = useMilestones(project.key)
  const [createOpen, setCreateOpen] = useState(false)
  const today = todayIn(user.timeZoneId)
  return (
    <div className="tm-stack">
      <div className="tm-row">
        <h2 className="tm-section-title" style={{ margin: 0 }}>マイルストーン</h2>
        <span className="tm-spacer" />
        {canManage(project) && <Button variant="primary" onClick={() => setCreateOpen(true)}>マイルストーンを作成</Button>}
      </div>
      {milestones.isPending && <Loading />}
      {milestones.isError && <ErrorPanel error={milestones.error} onRetry={() => void milestones.refetch()} />}
      {milestones.isSuccess && milestones.data.length === 0 && (
        <EmptyState title="マイルストーンはまだありません" text="リリースや締め切りなどの節目を作り、タスクをまとめて進み具合を確認できます。" />
      )}
      {milestones.isSuccess && milestones.data.length > 0 && (
        <div className="tm-table-wrap">
          <table className="tm-table">
            <caption className="tm-visually-hidden">マイルストーンの一覧</caption>
            <thead><tr><th scope="col">名前</th><th scope="col">目標日</th><th scope="col" style={{ minWidth: 200 }}>進捗</th><th scope="col">期限切れ</th><th scope="col">状態</th></tr></thead>
            <tbody>
              {milestones.data.map((m) => (
                <tr key={m.id}>
                  <td><Link to={m.id} className="tm-link--plain" style={{ fontWeight: 600 }}>{m.name}</Link></td>
                  <td><DueDateLabel dueDate={m.targetDate} today={today} isOverdue={m.state === 'open' && m.targetDate !== null && m.targetDate !== undefined && m.targetDate < today} closed={m.state === 'closed'} /></td>
                  <td><ProgressBar done={m.closedTaskCount} total={m.totalTaskCount} /></td>
                  <td className={m.overdueTaskCount > 0 ? 'tm-due--overdue' : undefined}>{m.overdueTaskCount}</td>
                  <td>{MILESTONE_STATE_LABELS[m.state]}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {createOpen && <MilestoneFormDialog projectKey={project.key} onClose={() => setCreateOpen(false)} />}
    </div>
  )
}

/** SCR-11 マイルストーン詳細 */
export function MilestoneDetailPage() {
  const project = useOutletContext<Project>()
  const user = useCurrentUser()
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const milestoneId = useParams().milestoneId ?? ''
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [createTaskOpen, setCreateTaskOpen] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const milestone = useQuery({
    queryKey: queryKeys.milestone(project.key, milestoneId),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/milestones/{milestoneId}', { params: { path: { projectKey: project.key, milestoneId } } })),
  })
  const tasks = useQuery({
    queryKey: queryKeys.tasks(project.key, { milestoneId, includeClosed: true }),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks', {
      params: { path: { projectKey: project.key }, query: { milestoneId, includeClosed: true, sort: 'status', pageSize: 100 } },
    })),
  })
  usePageTitle(milestone.data?.name, project.name)
  if (milestone.isPending) return <Loading />
  if (milestone.isError) {
    if (milestone.error instanceof ApiError && milestone.error.status === 404) return <NotFoundPage />
    return <ErrorPanel error={milestone.error} onRetry={() => void milestone.refetch()} />
  }
  const m = milestone.data
  const today = todayIn(user.timeZoneId)
  const doDelete = async () => {
    setDeleting(true)
    try {
      await call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/milestones/{milestoneId}', { params: { path: { projectKey: project.key, milestoneId } } }))
      void queryClient.invalidateQueries({ queryKey: queryKeys.milestones(project.key) })
      void queryClient.invalidateQueries({ queryKey: queryKeys.tasks(project.key) })
      toast.success(`マイルストーン「${m.name}」を削除しました`)
      void navigate(`/projects/${project.key}/milestones`)
    } catch (e) {
      const msg = errorMessage(e, 'マイルストーンを削除できませんでした。')
      toast.error(msg.title, msg.detail)
    } finally {
      setDeleting(false)
    }
  }
  return (
    <div className="tm-stack">
      <nav className="tm-small"><Link to={`/projects/${project.key}/milestones`} className="tm-link">← マイルストーン一覧</Link></nav>
      <section className="tm-card tm-stack">
        <div className="tm-row">
          <h2 className="tm-page-title">{m.name}</h2>
          <span className="tm-small tm-muted">{MILESTONE_STATE_LABELS[m.state]}</span>
          <span className="tm-spacer" />
          {canManage(project) && <><Button size="small" onClick={() => setEditOpen(true)}>編集</Button><Button size="small" variant="ghost" onClick={() => setDeleteOpen(true)}>削除</Button></>}
        </div>
        <div className="tm-row tm-small">目標日: <DueDateLabel dueDate={m.targetDate} today={today} isOverdue={m.state === 'open' && !!m.targetDate && m.targetDate < today} closed={m.state === 'closed'} /></div>
        <ProgressBar done={m.closedTaskCount} total={m.totalTaskCount} />
        {m.overdueTaskCount > 0 && <p className="tm-due--overdue tm-small" style={{ margin: 0 }}>⚠ 期限切れのタスクが {m.overdueTaskCount} 件あります</p>}
        {m.description && <MarkdownView text={m.description} />}
      </section>
      <div className="tm-row">
        <h3 className="tm-section-title" style={{ margin: 0 }}>このマイルストーンのタスク</h3>
        <span className="tm-spacer" />
        {canManage(project) && m.state === 'open' && <Button onClick={() => setCreateTaskOpen(true)}>タスクを追加</Button>}
      </div>
      {tasks.isPending && <Loading />}
      {tasks.isError && <ErrorPanel error={tasks.error} onRetry={() => void tasks.refetch()} />}
      {tasks.isSuccess && (tasks.data.items.length === 0
        ? <EmptyState title="このマイルストーンのタスクはまだありません" text="タスクの詳細画面でマイルストーンを設定すると、ここに表示されます。" />
        : <TaskTable projectKey={project.key} tasks={tasks.data.items} today={today} canEdit={canManage(project)} sort="status" onSort={() => undefined} isFetching={tasks.isFetching} />)}
      {editOpen && <MilestoneFormDialog projectKey={project.key} milestone={m} onClose={() => setEditOpen(false)} />}
      <ConfirmDialog isOpen={deleteOpen} onOpenChange={setDeleteOpen} title={`マイルストーン「${m.name}」を削除しますか？`}
        confirmLabel="削除する" busy={deleting} onConfirm={() => void doDelete()}>
        <p>タスクは削除されません。{m.totalTaskCount > 0 && `${m.totalTaskCount} 件のタスクから、このマイルストーンの設定が外れます。`}</p>
      </ConfirmDialog>
      {createTaskOpen && <TaskFormDialog projectKey={project.key} isOpen onOpenChange={setCreateTaskOpen} defaults={{ milestoneId: m.id }} />}
    </div>
  )
}

const schema = z.object({
  name: z.string().trim().min(1, 'マイルストーン名を入力してください。').max(LIMITS.milestoneName, `マイルストーン名は${LIMITS.milestoneName}文字以内で入力してください。`),
  description: z.string().max(LIMITS.milestoneDescription, `説明は${LIMITS.milestoneDescription}文字以内で入力してください。`),
  targetDate: z.string(),
  state: z.enum(['open', 'closed']),
})
type Values = z.infer<typeof schema>

function MilestoneFormDialog({ projectKey, milestone, onClose }: { projectKey: string; milestone?: Schemas['MilestoneDetailResponse']; onClose: () => void }) {
  const queryClient = useQueryClient()
  const toast = useToast()
  const navigate = useNavigate()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, formState, setError } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { name: milestone?.name ?? '', description: milestone?.description ?? '', targetDate: milestone?.targetDate ?? '', state: milestone?.state ?? 'open' },
  })
  const onSubmit = handleSubmit(async (v) => {
    setFormError(null)
    try {
      const body = { name: v.name, description: v.description.trim() || null, targetDate: v.targetDate || null }
      if (milestone) {
        await call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/milestones/{milestoneId}', {
          params: { path: { projectKey, milestoneId: milestone.id } }, body: { ...body, state: v.state, version: milestone.version },
        }))
        toast.success('マイルストーンを保存しました')
      } else {
        const created = await call(() => apiClient.POST('/api/v1/projects/{projectKey}/milestones', { params: { path: { projectKey } }, body }))
        toast.success(`マイルストーン「${created.name}」を作成しました`)
        void navigate(`/projects/${projectKey}/milestones/${created.id}`)
      }
      void queryClient.invalidateQueries({ queryKey: queryKeys.milestones(projectKey) })
      void queryClient.invalidateQueries({ queryKey: ['milestone', projectKey] })
      onClose()
    } catch (e) {
      if (e instanceof ApiError && e.code === 'milestone.name_taken') setError('name', { message: e.title })
      else if (e instanceof ApiError && e.code === 'concurrency.conflict') setFormError('他の人が先に更新しました。画面を閉じて、最新の内容を確認してください。')
      else setFormError(errorMessage(e, '保存できませんでした。').title)
    }
  })
  return (
    <Dialog isOpen onOpenChange={(open) => !open && onClose()} title={milestone ? 'マイルストーンを編集' : 'マイルストーンを作成'}>
      <form onSubmit={(e) => void onSubmit(e)} noValidate>
        <FormError message={formError} />
        <Field label="マイルストーン名" required error={formState.errors.name?.message}>
          {(p) => <input {...p} {...register('name')} className="tm-input" autoFocus maxLength={LIMITS.milestoneName} />}
        </Field>
        <Field label="目標日" error={formState.errors.targetDate?.message}>
          {(p) => <input {...p} {...register('targetDate')} type="date" className="tm-input" style={{ maxWidth: 200 }} min="2000-01-01" max="2099-12-31" />}
        </Field>
        {milestone && (
          <Field label="状態" hint="終了したマイルストーンには、新しくタスクを追加できません。">
            {(p) => <select {...p} {...register('state')} className="tm-select" style={{ maxWidth: 200 }}><option value="open">進行中</option><option value="closed">終了</option></select>}
          </Field>
        )}
        <Field label="説明" hint="Markdown で書けます。" error={formState.errors.description?.message}>
          {(p) => <textarea {...p} {...register('description')} className="tm-textarea" maxLength={LIMITS.milestoneDescription} />}
        </Field>
        <div className="tm-dialog__footer">
          <Button onClick={onClose}>キャンセル</Button>
          <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="保存しています…">{milestone ? '保存' : '作成する'}</Button>
        </div>
      </form>
    </Dialog>
  )
}
