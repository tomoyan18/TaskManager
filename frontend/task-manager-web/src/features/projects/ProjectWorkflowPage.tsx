import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useOutletContext } from 'react-router'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys, useStatuses, useTags } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { StatusBadge, TagChip } from '../../components/Display'
import { ErrorPanel, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/Toast'
import { CATEGORY_LABELS, CATEGORY_ORDER, COLOR_LABELS, COLOR_ORDER } from '../../constants/labels'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { errorMessage } from '../tasks/taskApi'
import styles from './Projects.module.css'

type Status = Schemas['WorkflowStatusResponse']
type Tag = Schemas['TagResponse']
type Color = Schemas['StatusColor']
type Category = Schemas['StatusCategory']

/** SCR-14 プロジェクト設定: ステータスとタグ */
export function ProjectWorkflowPage() {
  const project = useOutletContext<Schemas['ProjectDetailResponse']>()
  usePageTitle('ステータスとタグ', project.name)
  const canManageStatuses = project.myRole === 'owner' && !project.isArchived
  const canManageTags = (project.myRole === 'owner' || project.myRole === 'editor') && !project.isArchived
  return (
    <div className="tm-stack">
      <StatusSection projectKey={project.key} canManage={canManageStatuses} />
      <TagSection projectKey={project.key} canManage={canManageTags} />
    </div>
  )
}

function useRefresh(projectKey: string) {
  const queryClient = useQueryClient()
  return () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.statuses(projectKey) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.tags(projectKey) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.tasks(projectKey) })
  }
}

function StatusSection({ projectKey, canManage }: { projectKey: string; canManage: boolean }) {
  const toast = useToast()
  const statuses = useStatuses(projectKey)
  const refresh = useRefresh(projectKey)
  const [editing, setEditing] = useState<Status | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Status | null>(null)
  const [moveTo, setMoveTo] = useState('')
  const onError = (fallback: string) => (e: unknown) => { const m = errorMessage(e, fallback); toast.error(m.title, m.detail); refresh() }
  const reorder = useMutation({
    mutationFn: (ids: string[]) => call(() => apiClient.PUT('/api/v1/projects/{projectKey}/statuses/order', { params: { path: { projectKey } }, body: { statusIds: ids } })),
    onSuccess: refresh,
    onError: onError('並べ替えられませんでした。'),
  })
  const markDefault = useMutation({
    mutationFn: (id: string) => call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/statuses/{statusId}', { params: { path: { projectKey, statusId: id } }, body: { isDefault: true } })),
    onSuccess: (s) => { toast.success(`「${s.name}」を既定のステータスにしました`); refresh() },
    onError: onError('既定にできませんでした。'),
  })
  const remove = useMutation({
    mutationFn: ({ id, target }: { id: string; target: string | null }) => call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/statuses/{statusId}', {
      params: { path: { projectKey, statusId: id }, query: { moveTasksToStatusId: target ?? undefined } },
    })),
    onSuccess: () => { toast.success('ステータスを削除しました'); setDeleting(null); refresh() },
    onError: onError('ステータスを削除できませんでした。'),
  })
  if (statuses.isPending) return <Loading />
  if (statuses.isError) return <ErrorPanel error={statuses.error} onRetry={() => void statuses.refetch()} />
  const list = statuses.data
  const move = (index: number, delta: number) => {
    const ids = list.map((s) => s.id)
    const [item] = ids.splice(index, 1)
    if (item !== undefined) ids.splice(index + delta, 0, item)
    reorder.mutate(ids)
  }
  return (
    <section className="tm-card">
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <h2 className="tm-section-title" style={{ margin: 0 }}>ステータス（{list.length}/20）</h2>
        <span className="tm-spacer" />
        {canManage && <Button variant="primary" onClick={() => setEditing('new')} disabled={list.length >= 20}>ステータスを追加</Button>}
      </div>
      <p className="tm-small tm-muted">区分（未着手・進行中・完了・中止）で、進み具合の集計や期限切れの判定を行います。新しいタスクには「既定」のステータスが付きます。</p>
      <ul className={styles.list}>
        {list.map((s, i) => (
          <li key={s.id} className={styles.listRow}>
            <StatusBadge status={s} />
            <span className="tm-small tm-muted">区分: {CATEGORY_LABELS[s.category]}・タスク {s.taskCount} 件</span>
            {s.isDefault && <span className="tm-chip" style={{ background: '#e6eefb' }}>既定</span>}
            <span className="tm-spacer" />
            {canManage && (
              <>
                <Button size="small" variant="ghost" iconOnly aria-label={`「${s.name}」を上へ`} disabled={i === 0 || reorder.isPending} onClick={() => move(i, -1)}><Icon name="arrowUp" size={16} /></Button>
                <Button size="small" variant="ghost" iconOnly aria-label={`「${s.name}」を下へ`} disabled={i === list.length - 1 || reorder.isPending} onClick={() => move(i, 1)}><Icon name="arrowDown" size={16} /></Button>
                {!s.isDefault && (s.category === 'notStarted' || s.category === 'inProgress') && (
                  <Button size="small" variant="ghost" onClick={() => markDefault.mutate(s.id)}>既定にする</Button>
                )}
                <Button size="small" variant="ghost" onClick={() => setEditing(s)}>編集</Button>
                <Button size="small" variant="ghost" disabled={s.isDefault} title={s.isDefault ? '既定のステータスは削除できません' : undefined}
                  onClick={() => { setMoveTo(''); setDeleting(s) }}>削除</Button>
              </>
            )}
          </li>
        ))}
      </ul>
      {editing !== null && (
        <NameColorDialog kind="status" projectKey={projectKey} target={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)} onSaved={refresh} />
      )}
      <ConfirmDialog isOpen={deleting !== null} onOpenChange={(o) => !o && setDeleting(null)} title={`ステータス「${deleting?.name ?? ''}」を削除しますか？`}
        confirmLabel={deleting && deleting.taskCount > 0 ? '削除して移動する' : '削除する'} busy={remove.isPending}
        onConfirm={() => {
          if (deleting === null) return
          if (deleting.taskCount > 0 && moveTo === '') { toast.error('移動先のステータスを選んでください。'); return }
          remove.mutate({ id: deleting.id, target: moveTo || null })
        }}>
        {deleting && deleting.taskCount > 0 ? (
          <>
            <p>このステータスのタスクが {deleting.taskCount} 件あります。移動先を選んでください。</p>
            <label className="tm-field__label" htmlFor="move-to">移動先</label>
            <select id="move-to" className="tm-select" value={moveTo} onChange={(e) => setMoveTo(e.target.value)}>
              <option value="">選んでください</option>
              {list.filter((s) => s.id !== deleting.id).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </>
        ) : <p>このステータスのタスクはありません。</p>}
      </ConfirmDialog>
    </section>
  )
}

function TagSection({ projectKey, canManage }: { projectKey: string; canManage: boolean }) {
  const toast = useToast()
  const tags = useTags(projectKey)
  const refresh = useRefresh(projectKey)
  const [editing, setEditing] = useState<Tag | 'new' | null>(null)
  const [deleting, setDeleting] = useState<Tag | null>(null)
  const remove = useMutation({
    mutationFn: (id: string) => call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/tags/{tagId}', { params: { path: { projectKey, tagId: id } } })),
    onSuccess: () => { toast.success('タグを削除しました'); setDeleting(null); refresh() },
    onError: (e) => { const m = errorMessage(e, 'タグを削除できませんでした。'); toast.error(m.title, m.detail) },
  })
  if (tags.isPending) return <Loading />
  if (tags.isError) return <ErrorPanel error={tags.error} onRetry={() => void tags.refetch()} />
  return (
    <section className="tm-card">
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <h2 className="tm-section-title" style={{ margin: 0 }}>タグ（{tags.data.length}/50）</h2>
        <span className="tm-spacer" />
        {canManage && <Button variant="primary" onClick={() => setEditing('new')} disabled={tags.data.length >= 50}>タグを追加</Button>}
      </div>
      {tags.data.length === 0 && <p className="tm-muted tm-small">タグはまだありません。「見積」「急ぎ」などの分類に使えます。</p>}
      <ul className={styles.list}>
        {tags.data.map((t) => (
          <li key={t.id} className={styles.listRow}>
            <TagChip tag={t} />
            <span className="tm-small tm-muted">タスク {t.taskCount} 件</span>
            <span className="tm-spacer" />
            {canManage && <><Button size="small" variant="ghost" onClick={() => setEditing(t)}>編集</Button><Button size="small" variant="ghost" onClick={() => setDeleting(t)}>削除</Button></>}
          </li>
        ))}
      </ul>
      {editing !== null && <NameColorDialog kind="tag" projectKey={projectKey} target={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={refresh} />}
      <ConfirmDialog isOpen={deleting !== null} onOpenChange={(o) => !o && setDeleting(null)} title={`タグ「${deleting?.name ?? ''}」を削除しますか？`}
        confirmLabel="削除する" busy={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting.id)}>
        <p>{deleting && deleting.taskCount > 0 ? `${deleting.taskCount} 件のタスクからこのタグが外れます。` : ''}タスク自体は削除されません。</p>
      </ConfirmDialog>
    </section>
  )
}

/** ステータス・タグの追加と編集（名前・色、ステータスは区分も） */
function NameColorDialog({ kind, projectKey, target, onClose, onSaved }: {
  kind: 'status' | 'tag'
  projectKey: string
  target: Status | Tag | null
  onClose: () => void
  onSaved: () => void
}) {
  const toast = useToast()
  const [name, setName] = useState(target?.name ?? '')
  const [color, setColor] = useState<Color>(target?.color ?? (kind === 'status' ? 'blue' : 'gray'))
  const [category, setCategory] = useState<Category>(target && 'category' in target ? target.category : 'inProgress')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const label = kind === 'status' ? 'ステータス' : 'タグ'
  const max = kind === 'status' ? LIMITS.statusName : LIMITS.tagName
  const isDefault = target !== null && 'isDefault' in target && target.isDefault
  const submit = async () => {
    if (name.trim() === '') { setError(`${label}名を入力してください。`); return }
    setBusy(true)
    setError(null)
    try {
      const path = { projectKey }
      if (kind === 'status') {
        if (target) await call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/statuses/{statusId}', { params: { path: { ...path, statusId: target.id } }, body: { name, color, category } }))
        else await call(() => apiClient.POST('/api/v1/projects/{projectKey}/statuses', { params: { path }, body: { name, color, category } }))
      } else if (target) {
        await call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/tags/{tagId}', { params: { path: { ...path, tagId: target.id } }, body: { name, color } }))
      } else {
        await call(() => apiClient.POST('/api/v1/projects/{projectKey}/tags', { params: { path }, body: { name, color } }))
      }
      toast.success(`${label}「${name.trim()}」を保存しました`)
      onSaved()
      onClose()
    } catch (e) {
      setError(errorMessage(e, '保存できませんでした。').title)
    } finally {
      setBusy(false)
    }
  }
  return (
    <Dialog isOpen onOpenChange={(o) => !o && onClose()} title={target ? `${label}を編集` : `${label}を追加`}>
      <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate>
        {error && <div className="tm-alert tm-alert--error" role="alert" style={{ marginBottom: 'var(--space-3)' }}>{error}</div>}
        <div className="tm-field">
          <label className="tm-field__label" htmlFor="nc-name">{label}名<span className="tm-field__required">必須</span></label>
          <input id="nc-name" className="tm-input" value={name} maxLength={max} autoFocus onChange={(e) => setName(e.target.value)} />
        </div>
        {kind === 'status' && (
          <div className="tm-field">
            <label className="tm-field__label" htmlFor="nc-category">区分</label>
            <select id="nc-category" className="tm-select" value={category} onChange={(e) => setCategory(e.target.value as Category)}>
              {CATEGORY_ORDER.filter((c) => !isDefault || c === 'notStarted' || c === 'inProgress').map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
            </select>
            {isDefault && <span className="tm-field__hint">既定のステータスの区分は「未着手」か「進行中」にしてください。</span>}
          </div>
        )}
        <fieldset className="tm-field" style={{ border: 'none', padding: 0, margin: '0 0 var(--space-4)' }}>
          <legend className="tm-field__label">色</legend>
          <div className="tm-row">
            {COLOR_ORDER.map((c) => (
              <label key={c} className="tm-checkbox tm-small">
                <input type="radio" name="color" value={c} checked={color === c} onChange={() => setColor(c)} />
                <span className="tm-badge" style={{ background: `var(--sc-${c}-bg)`, color: `var(--sc-${c}-fg)`, borderLeftColor: `var(--sc-${c}-bar)` }}>{COLOR_LABELS[c]}</span>
              </label>
            ))}
          </div>
        </fieldset>
        <div className="tm-dialog__footer">
          <Button onClick={onClose}>キャンセル</Button>
          <Button type="submit" variant="primary" busy={busy} busyText="保存しています…">保存</Button>
        </div>
      </form>
    </Dialog>
  )
}
