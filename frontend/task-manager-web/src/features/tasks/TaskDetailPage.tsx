import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Button as AriaButton, Menu, MenuItem, MenuTrigger, Popover, Tab, TabList, TabPanel, Tabs } from 'react-aria-components'
import { Link, useBlocker, useNavigate, useOutletContext, useParams } from 'react-router'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { DueDateLabel, PriorityIndicator, StatusBadge, TagChip, UserLabel } from '../../components/Display'
import { ErrorPanel, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { MarkdownView } from '../../components/MarkdownView'
import { useToast } from '../../components/Toast'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { formatDateTime, formatFullDateTime, todayIn } from '../../utils/dates'
import { taskPath } from '../../utils/taskKey'
import { useCurrentUser } from '../auth/AuthProvider'
import { NotFoundPage } from '../shell/NotFoundPage'
import { TaskAttachments } from './TaskAttachments'
import { TaskComments } from './TaskComments'
import { TaskHistory } from './TaskHistory'
import { errorMessage, useDeleteTask, useUpdateTask, type TaskDetail, type UpdateTaskBody } from './taskApi'
import { AssigneeSelect, DateInput, MilestoneSelect, PrioritySelect, StatusSelect, TagPicker } from './TaskFields'
import { TaskFormDialog } from './TaskFormDialog'
import styles from './Tasks.module.css'

/** SCR-08 タスク詳細 */
export function TaskDetailPage() {
  const project = useOutletContext<Schemas['ProjectDetailResponse']>()
  const number = Number(useParams().taskNumber)
  const task = useQuery({
    queryKey: queryKeys.task(project.key, number),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/{taskNumber}', {
      params: { path: { projectKey: project.key, taskNumber: number } },
    })),
    staleTime: 10_000,
    enabled: Number.isInteger(number) && number > 0,
  })
  usePageTitle(task.data ? `${task.data.title}（${task.data.key}）` : null, project.name)
  if (!Number.isInteger(number) || number < 1) return <NotFoundPage />
  if (task.isPending) return <Loading rows={8} />
  if (task.isError) {
    if (task.error instanceof ApiError && task.error.status === 404) return <NotFoundPage />
    return <ErrorPanel error={task.error} onRetry={() => void task.refetch()} />
  }
  return <TaskDetailView project={project} task={task.data} />
}

function TaskDetailView({ project, task }: { project: Schemas['ProjectDetailResponse']; task: TaskDetail }) {
  const user = useCurrentUser()
  const toast = useToast()
  const navigate = useNavigate()
  const today = todayIn(user.timeZoneId)
  const update = useUpdateTask(project.key)
  const remove = useDeleteTask(project.key)
  const [editingTitle, setEditingTitle] = useState(false)
  const [title, setTitle] = useState(task.title)
  const [editingDescription, setEditingDescription] = useState(false)
  const [description, setDescription] = useState(task.description ?? '')
  const [previewDescription, setPreviewDescription] = useState(false)
  const [savedField, setSavedField] = useState<string | null>(null)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [addSubtaskOpen, setAddSubtaskOpen] = useState(false)
  const canEdit = task.permissions.canEdit
  const closed = task.status.category === 'done' || task.status.category === 'canceled'
  const unsaved = (editingTitle && title !== task.title) || (editingDescription && description !== (task.description ?? ''))
  const blocker = useBlocker(unsaved)

  useEffect(() => {
    if (blocker.state === 'blocked') {
      if (window.confirm('保存していない変更があります。破棄して移動しますか？')) blocker.proceed()
      else blocker.reset()
    }
  }, [blocker])

  useEffect(() => {
    if (!unsaved) return
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [unsaved])

  // 続けて別の項目を変えても自分の変更同士で競合（409）しないよう、保存は 1 件ずつ順に行い、
  // 直前の保存で返った版を次の保存に使う（Phase 6 の E2E で検出）
  const latestVersion = useRef(task.version)
  const saveQueue = useRef<Promise<unknown>>(Promise.resolve())
  useEffect(() => {
    latestVersion.current = Math.max(latestVersion.current, task.version)
  }, [task.version])

  const save = (field: string, body: Omit<UpdateTaskBody, 'version'>, onDone?: () => void) => {
    saveQueue.current = saveQueue.current.then(async () => {
      try {
        const updated = await update.mutateAsync({ number: task.number, body: { ...body, version: latestVersion.current } })
        latestVersion.current = updated.version
        setSavedField(field)
        setTimeout(() => setSavedField((f) => (f === field ? null : f)), 2000)
        onDone?.()
      } catch {
        // エラーの通知と最新の再読み込みは useUpdateTask で行う
      }
    })
  }

  const saveTitle = () => {
    const value = title.trim()
    if (value === '') {
      toast.error('タイトルを入力してください。')
      return
    }
    if (value === task.title) {
      setEditingTitle(false)
      return
    }
    save('title', { title: value }, () => setEditingTitle(false))
  }

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(new URL(taskPath(project.key, task.number), window.location.origin).toString())
      toast.success('リンクをコピーしました')
    } catch {
      toast.error('リンクをコピーできませんでした。')
    }
  }

  const doDelete = () => {
    remove.mutate({ number: task.number, includeSubtasks: task.subtaskCount > 0 }, {
      onSuccess: () => {
        toast.success(`${task.key} を削除しました`)
        setDeleteOpen(false)
        void navigate(`/projects/${project.key}/tasks`)
      },
      onError: (e) => {
        const m = errorMessage(e, 'タスクを削除できませんでした。')
        toast.error(m.title, m.detail)
      },
    })
  }

  const saved = (field: string) =>
    savedField === field ? <span className={styles.saved} role="status">保存しました</span> : null

  return (
    <article>
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <span className="tm-key">{task.key}</span>
        {task.parent && (
          <span className="tm-small tm-muted">親タスク: <Link to={taskPath(project.key, task.parent.number)} className="tm-link">{task.parent.key} {task.parent.title}</Link></span>
        )}
        <span className="tm-spacer" />
        <Button size="small" onClick={() => void copyLink()}>リンクをコピー</Button>
        {task.permissions.canDelete && (
          <MenuTrigger>
            <AriaButton className="tm-btn tm-btn--small tm-btn--icon" aria-label="その他の操作"><Icon name="more" /></AriaButton>
            <Popover className="tm-popover" placement="bottom end">
              <Menu className="tm-menu" onAction={(key) => key === 'delete' && setDeleteOpen(true)}>
                <MenuItem id="delete" className="tm-menu-item tm-menu-item--danger">タスクを削除</MenuItem>
              </Menu>
            </Popover>
          </MenuTrigger>
        )}
      </div>

      <div className={styles.layout}>
        <div className="tm-stack">
          <section className="tm-card">
            {editingTitle ? (
              <div className="tm-stack" style={{ gap: 'var(--space-2)' }}>
                <label htmlFor="task-title" className="tm-field__label">タイトル</label>
                <input id="task-title" className="tm-input" value={title} maxLength={LIMITS.title} autoFocus
                  onChange={(e) => setTitle(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && !e.nativeEvent.isComposing) saveTitle()
                    if (e.key === 'Escape') { setTitle(task.title); setEditingTitle(false) }
                  }} />
                <div className="tm-row">
                  <Button variant="primary" size="small" onClick={saveTitle} busy={update.isPending} busyText="保存しています…">保存</Button>
                  <Button size="small" onClick={() => { setTitle(task.title); setEditingTitle(false) }}>キャンセル</Button>
                </div>
              </div>
            ) : (
              <div className="tm-row" style={{ alignItems: 'flex-start' }}>
                <h1 className="tm-page-title" style={{ flex: 1 }}>{task.title}</h1>
                {canEdit && <Button size="small" variant="ghost" onClick={() => { setTitle(task.title); setEditingTitle(true) }}>編集</Button>}
                {saved('title')}
              </div>
            )}

            <div style={{ marginTop: 'var(--space-4)' }}>
              <div className="tm-row">
                <h2 className="tm-section-title" style={{ margin: 0 }}>説明</h2>
                <span className="tm-spacer" />
                {canEdit && !editingDescription && <Button size="small" variant="ghost" onClick={() => { setDescription(task.description ?? ''); setEditingDescription(true) }}>編集</Button>}
                {saved('description')}
              </div>
              {editingDescription ? (
                <div className="tm-stack" style={{ gap: 'var(--space-2)', marginTop: 'var(--space-2)' }}>
                  <div className="tm-row" role="group" aria-label="表示の切り替え">
                    <Button size="small" variant={previewDescription ? 'secondary' : 'primary'} aria-pressed={!previewDescription} onClick={() => setPreviewDescription(false)}>書く</Button>
                    <Button size="small" variant={previewDescription ? 'primary' : 'secondary'} aria-pressed={previewDescription} onClick={() => setPreviewDescription(true)}>プレビュー</Button>
                  </div>
                  {previewDescription
                    ? <div className="tm-card" style={{ minHeight: 120 }}><MarkdownView text={description || '（説明はありません）'} /></div>
                    : <textarea aria-label="説明（Markdown）" className="tm-textarea" style={{ minHeight: 200 }} value={description}
                        maxLength={LIMITS.description} onChange={(e) => setDescription(e.target.value)} autoFocus />}
                  <div className="tm-row">
                    <Button variant="primary" size="small" busy={update.isPending} busyText="保存しています…"
                      onClick={() => save('description', { description: description.trim() === '' ? null : description }, () => setEditingDescription(false))}>保存</Button>
                    <Button size="small" onClick={() => setEditingDescription(false)}>キャンセル</Button>
                    <span className="tm-small tm-muted">{description.length.toLocaleString()} / {LIMITS.description.toLocaleString()} 文字</span>
                  </div>
                </div>
              ) : task.description
                ? <div style={{ marginTop: 'var(--space-2)' }}><MarkdownView text={task.description} /></div>
                : <p className="tm-muted">説明はありません。</p>}
            </div>
          </section>

          <section className="tm-card" aria-labelledby="subtasks-title">
            <div className="tm-row">
              <h2 id="subtasks-title" className="tm-section-title" style={{ margin: 0 }}>
                サブタスク{task.subtaskCount > 0 && `（${task.closedSubtaskCount}/${task.subtaskCount} 完了）`}
              </h2>
              <span className="tm-spacer" />
              {canEdit && task.parent === null && <Button size="small" onClick={() => setAddSubtaskOpen(true)}><Icon name="plus" size={14} />サブタスクを追加</Button>}
            </div>
            {task.subtasks.length === 0
              ? <p className="tm-muted tm-small">{task.parent === null ? 'サブタスクはありません。' : 'サブタスクの下にサブタスクは作れません。'}</p>
              : (
                <ul className={styles.subtasks}>
                  {task.subtasks.map((s) => (
                    <li key={s.key}>
                      <span aria-hidden="true">{s.status.category === 'done' ? '☑' : '☐'}</span>
                      <span className="tm-key">{s.key}</span>
                      <Link to={taskPath(project.key, s.number)} className="tm-link--plain">{s.title}</Link>
                      <StatusBadge status={s.status} />
                      <UserLabel user={s.assignee} emptyText="" />
                    </li>
                  ))}
                </ul>
              )}
          </section>

          <TaskAttachments projectKey={project.key} task={task} />

          <section className="tm-card">
            <Tabs>
              <TabList aria-label="コメントと変更履歴" className="tm-tabs">
                <Tab id="comments" className="tm-tab">コメント（{task.commentCount}）</Tab>
                <Tab id="history" className="tm-tab">変更履歴</Tab>
              </TabList>
              <TabPanel id="comments"><TaskComments projectKey={project.key} task={task} /></TabPanel>
              <TabPanel id="history"><TaskHistory projectKey={project.key} taskNumber={task.number} /></TabPanel>
            </Tabs>
          </section>
        </div>

        <aside className={`tm-card ${styles.properties}`} aria-label="タスクの属性">
          <Property label="ステータス" saved={saved('status')}>
            {canEdit
              ? <StatusSelect projectKey={project.key} value={task.status.id} aria-label="ステータス" onChange={(statusId) => save('status', { statusId })} />
              : <StatusBadge status={task.status} />}
          </Property>
          <Property label="担当者" saved={saved('assignee')}>
            {canEdit
              ? <AssigneeSelect projectKey={project.key} currentUserId={user.id} current={task.assignee} value={task.assignee?.id ?? null}
                  aria-label="担当者" onChange={(assigneeUserId) => save('assignee', { assigneeUserId })} />
              : <UserLabel user={task.assignee} />}
          </Property>
          <Property label="優先度" saved={saved('priority')}>
            {canEdit ? <PrioritySelect value={task.priority} aria-label="優先度" onChange={(priority) => save('priority', { priority })} /> : <PriorityIndicator priority={task.priority} />}
          </Property>
          <Property label="開始日" saved={saved('startDate')}>
            {canEdit
              ? <DateInput today={today} showShortcuts={false} value={task.startDate ?? null} aria-label="開始日" onChange={(startDate) => save('startDate', { startDate })} />
              : <span>{task.startDate ?? <span className="tm-muted">なし</span>}</span>}
          </Property>
          <Property label="期限" saved={saved('dueDate')}>
            {canEdit && <DateInput today={today} value={task.dueDate ?? null} aria-label="期限" onChange={(dueDate) => save('dueDate', { dueDate })} />}
            <DueDateLabel dueDate={task.dueDate} today={today} isOverdue={task.isOverdue} closed={closed} />
          </Property>
          <Property label="マイルストーン" saved={saved('milestone')}>
            {canEdit
              ? <MilestoneSelect projectKey={project.key} value={task.milestone?.id ?? null} aria-label="マイルストーン" onChange={(milestoneId) => save('milestone', { milestoneId })} />
              : <span>{task.milestone?.name ?? <span className="tm-muted">なし</span>}</span>}
          </Property>
          <Property label="タグ" saved={saved('tags')}>
            {canEdit
              ? <TagPicker projectKey={project.key} value={task.tags.map((t) => t.id)} onChange={(tagIds) => save('tags', { tagIds })} />
              : <div className="tm-row">{task.tags.length === 0 ? <span className="tm-muted">なし</span> : task.tags.map((t) => <TagChip key={t.id} tag={t} />)}</div>}
          </Property>
          <hr style={{ border: 'none', borderTop: '1px solid var(--color-border)' }} />
          <p className="tm-small tm-muted" style={{ margin: 0 }}>
            作成 {task.createdBy?.displayName ?? '—'}{' '}
            <time dateTime={task.createdAtUtc} title={formatFullDateTime(task.createdAtUtc, user.timeZoneId)}>{formatDateTime(task.createdAtUtc, user.timeZoneId)}</time>
            <br />
            更新 {task.updatedBy?.displayName ?? '—'}{' '}
            <time dateTime={task.updatedAtUtc} title={formatFullDateTime(task.updatedAtUtc, user.timeZoneId)}>{formatDateTime(task.updatedAtUtc, user.timeZoneId)}</time>
            {task.completedAtUtc && <><br />完了 {formatDateTime(task.completedAtUtc, user.timeZoneId)}</>}
          </p>
        </aside>
      </div>

      <ConfirmDialog isOpen={deleteOpen} onOpenChange={setDeleteOpen} title={`タスク ${task.key} を削除しますか？`}
        confirmLabel={task.subtaskCount > 0 ? 'サブタスクも削除する' : '削除する'} onConfirm={doDelete} busy={remove.isPending}>
        {task.subtaskCount > 0 && <p><strong>このタスクにはサブタスクが {task.subtaskCount} 件あります。サブタスクもまとめて削除されます。</strong></p>}
        <p>コメント・添付ファイル・変更履歴も削除され、元に戻せません。</p>
      </ConfirmDialog>
      {addSubtaskOpen && (
        <TaskFormDialog projectKey={project.key} isOpen onOpenChange={setAddSubtaskOpen}
          defaults={{ parent: { number: task.number, title: task.title }, milestoneId: task.milestone?.id ?? null }} />
      )}
    </article>
  )
}

function Property({ label, children, saved }: { label: string; children: React.ReactNode; saved?: React.ReactNode }) {
  return (
    <div className={styles.propertyRow}>
      <div className={styles.propertyLabel}>{label}</div>
      <div className="tm-stack" style={{ gap: 'var(--space-1)' }}>{children}{saved}</div>
    </div>
  )
}
