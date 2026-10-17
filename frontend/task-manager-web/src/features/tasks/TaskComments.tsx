import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { UserLabel } from '../../components/Display'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { MarkdownView } from '../../components/MarkdownView'
import { useToast } from '../../components/Toast'
import { LIMITS } from '../../constants/limits'
import { formatDateTime, formatFullDateTime } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import { errorMessage, invalidateTaskQueries, type TaskDetail } from './taskApi'

/** コメント（REQ-CMT-01）。古い順に表示し、下に入力欄を置く */
export function TaskComments({ projectKey, task }: { projectKey: string; task: TaskDetail }) {
  const user = useCurrentUser()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [body, setBody] = useState('')
  const [editing, setEditing] = useState<{ id: string; body: string } | null>(null)
  const [deleting, setDeleting] = useState<Schemas['TaskCommentResponse'] | null>(null)
  const path = { projectKey, taskNumber: task.number }
  const comments = useQuery({
    queryKey: queryKeys.comments(projectKey, task.number),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments', { params: { path, query: { pageSize: 100 } } })),
  })
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.comments(projectKey, task.number) })
    invalidateTaskQueries(queryClient, projectKey, task.number)
  }
  const onError = (fallback: string) => (e: unknown) => {
    const m = errorMessage(e, fallback)
    toast.error(m.title, m.detail)
  }
  const create = useMutation({
    mutationFn: (text: string) => call(() => apiClient.POST('/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments', { params: { path }, body: { body: text } })),
    onSuccess: () => { setBody(''); refresh() },
    onError: onError('コメントを投稿できませんでした。'),
  })
  const edit = useMutation({
    mutationFn: (v: { id: string; body: string }) => call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}', {
      params: { path: { ...path, commentId: v.id } }, body: { body: v.body },
    })),
    onSuccess: () => { setEditing(null); refresh() },
    onError: onError('コメントを保存できませんでした。'),
  })
  const remove = useMutation({
    mutationFn: (id: string) => call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}', { params: { path: { ...path, commentId: id } } })),
    onSuccess: () => { setDeleting(null); toast.success('コメントを削除しました'); refresh() },
    onError: onError('コメントを削除できませんでした。'),
  })

  if (comments.isPending) return <Loading />
  if (comments.isError) return <ErrorPanel error={comments.error} onRetry={() => void comments.refetch()} />
  const items = comments.data.items
  return (
    <div className="tm-stack">
      {items.length === 0 && <EmptyState title="まだコメントはありません" text={task.permissions.canComment ? '進み具合や質問を書き込めます。' : undefined} />}
      <ol style={{ listStyle: 'none', padding: 0, margin: 0 }} className="tm-stack">
        {items.map((c) => (
          <li key={c.id} className="tm-card" style={{ padding: 'var(--space-3)' }}>
            <div className="tm-row tm-small">
              <UserLabel user={c.author} />
              <time dateTime={c.createdAtUtc} title={formatFullDateTime(c.createdAtUtc, user.timeZoneId)} className="tm-muted">
                {formatDateTime(c.createdAtUtc, user.timeZoneId)}
              </time>
              {c.editedAtUtc && <span className="tm-muted">（編集済み）</span>}
              <span className="tm-spacer" />
              {c.canEdit && editing?.id !== c.id && <Button size="small" variant="ghost" onClick={() => setEditing({ id: c.id, body: c.body })}>編集</Button>}
              {c.canDelete && <Button size="small" variant="ghost" onClick={() => setDeleting(c)}>削除</Button>}
            </div>
            {editing?.id === c.id ? (
              <div className="tm-stack" style={{ gap: 'var(--space-2)', marginTop: 'var(--space-2)' }}>
                <textarea aria-label="コメントを編集" className="tm-textarea" value={editing.body} maxLength={LIMITS.comment}
                  onChange={(e) => setEditing({ id: c.id, body: e.target.value })} />
                <div className="tm-row">
                  <Button size="small" variant="primary" disabled={editing.body.trim() === ''} busy={edit.isPending} busyText="保存しています…"
                    onClick={() => edit.mutate(editing)}>保存</Button>
                  <Button size="small" onClick={() => setEditing(null)}>キャンセル</Button>
                </div>
              </div>
            ) : <div style={{ marginTop: 'var(--space-2)' }}><MarkdownView text={c.body} /></div>}
          </li>
        ))}
      </ol>
      {task.permissions.canComment && (
        <form onSubmit={(e) => { e.preventDefault(); if (body.trim() !== '') create.mutate(body) }} className="tm-stack" style={{ gap: 'var(--space-2)' }}>
          <label htmlFor="new-comment" className="tm-field__label">コメントを書く</label>
          <textarea id="new-comment" className="tm-textarea" placeholder="進み具合や質問を書き込めます（Markdown 対応）" value={body}
            maxLength={LIMITS.comment} onChange={(e) => setBody(e.target.value)} />
          <div className="tm-row">
            <Button type="submit" variant="primary" disabled={body.trim() === ''} busy={create.isPending} busyText="投稿しています…">投稿する</Button>
          </div>
        </form>
      )}
      <ConfirmDialog isOpen={deleting !== null} onOpenChange={(open) => !open && setDeleting(null)} title="コメントを削除しますか？"
        confirmLabel="削除する" busy={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting.id)}>
        <p>削除したコメントは元に戻せません。</p>
      </ConfirmDialog>
    </div>
  )
}
