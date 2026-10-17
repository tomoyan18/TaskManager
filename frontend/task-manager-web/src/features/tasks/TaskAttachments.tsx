import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { apiClient, authorizedFetch, call, toApiError, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { ErrorPanel, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/Toast'
import { ATTACHMENT_ACCEPT, LIMITS } from '../../constants/limits'
import { formatDateTime } from '../../utils/dates'
import { formatBytes } from '../../utils/taskKey'
import { useCurrentUser } from '../auth/AuthProvider'
import { errorMessage, invalidateTaskQueries, type TaskDetail } from './taskApi'

/** 添付ファイル（REQ-ATT-01）。ボタンでの選択とドラッグの両方に対応し、許可形式と上限を常に表示する */
export function TaskAttachments({ projectKey, task }: { projectKey: string; task: TaskDetail }) {
  const user = useCurrentUser()
  const toast = useToast()
  const queryClient = useQueryClient()
  const inputRef = useRef<HTMLInputElement>(null)
  const [dragging, setDragging] = useState(false)
  const [deleting, setDeleting] = useState<Schemas['TaskAttachmentResponse'] | null>(null)
  const basePath = `/api/v1/projects/${projectKey}/tasks/${task.number}/attachments`
  const attachments = useQuery({
    queryKey: queryKeys.attachments(projectKey, task.number),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments', {
      params: { path: { projectKey, taskNumber: task.number } },
    })),
  })
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.attachments(projectKey, task.number) })
    invalidateTaskQueries(queryClient, projectKey, task.number)
  }
  const upload = useMutation({
    mutationFn: async (file: File) => {
      if (file.size > LIMITS.attachmentBytes) throw new Error('ファイルサイズが上限（10MB）を超えています。')
      const form = new FormData()
      form.append('file', file)
      const response = await authorizedFetch(basePath, { method: 'POST', body: form })
      if (!response.ok) throw await toApiError(response)
    },
    onSuccess: () => { toast.success('ファイルを添付しました'); refresh() },
    onError: (e) => {
      const m = e instanceof Error && !(e as { status?: number }).status ? { title: e.message } : errorMessage(e, 'ファイルを添付できませんでした。')
      toast.error(m.title, 'detail' in m ? m.detail : undefined)
    },
  })
  const remove = useMutation({
    mutationFn: (id: string) => call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments/{attachmentId}', {
      params: { path: { projectKey, taskNumber: task.number, attachmentId: id } },
    })),
    onSuccess: () => { setDeleting(null); toast.success('添付ファイルを削除しました'); refresh() },
    onError: (e) => { const m = errorMessage(e, '添付ファイルを削除できませんでした。'); toast.error(m.title, m.detail) },
  })

  const download = async (a: Schemas['TaskAttachmentResponse']) => {
    const response = await authorizedFetch(`${basePath}/${a.id}/content`)
    if (!response.ok) {
      toast.error((await toApiError(response)).title)
      return
    }
    const url = URL.createObjectURL(await response.blob())
    const link = document.createElement('a')
    link.href = url
    link.download = a.fileName
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  }

  const files = attachments.data ?? []
  const full = files.length >= LIMITS.attachmentsPerTask
  return (
    // oxlint-disable-next-line jsx-a11y/no-noninteractive-element-interactions -- ファイルのドロップ先。「ファイルを添付」ボタンでも同じ操作ができる
    <section className="tm-card" aria-labelledby="attachments-title"
      onDragOver={(e) => { if (task.permissions.canUpload) { e.preventDefault(); setDragging(true) } }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => {
        e.preventDefault()
        setDragging(false)
        const file = e.dataTransfer.files[0]
        if (file && task.permissions.canUpload && !full) upload.mutate(file)
      }}
      style={dragging ? { outline: '3px dashed var(--color-primary)' } : undefined}>
      <div className="tm-row">
        <h2 id="attachments-title" className="tm-section-title" style={{ margin: 0 }}>添付ファイル（{files.length}）</h2>
        <span className="tm-spacer" />
        {task.permissions.canUpload && (
          <>
            <input ref={inputRef} type="file" accept={ATTACHMENT_ACCEPT} hidden
              onChange={(e) => { const f = e.target.files?.[0]; if (f) upload.mutate(f); e.target.value = '' }} />
            <Button size="small" disabled={full} busy={upload.isPending} busyText="アップロードしています…" onClick={() => inputRef.current?.click()}>
              <Icon name="paperclip" size={14} />ファイルを添付
            </Button>
          </>
        )}
      </div>
      {task.permissions.canUpload && (
        <p className="tm-small tm-muted">
          {full ? '添付できるのは 20 個までです。' : '10MB まで。PDF・画像（PNG・JPEG・GIF・WebP）・テキスト（TXT・CSV・MD）・Word・Excel・PowerPoint・ZIP を添付できます。ここにドラッグしても追加できます。'}
        </p>
      )}
      {attachments.isPending && <Loading rows={2} />}
      {attachments.isError && <ErrorPanel error={attachments.error} onRetry={() => void attachments.refetch()} />}
      {attachments.isSuccess && files.length === 0 && <p className="tm-muted tm-small">添付ファイルはありません。</p>}
      <ul style={{ listStyle: 'none', padding: 0, margin: 0 }}>
        {files.map((a) => (
          <li key={a.id} className="tm-row" style={{ padding: 'var(--space-1) 0' }}>
            <button type="button" className="tm-link" style={{ border: 'none', background: 'none', cursor: 'pointer', padding: 0, font: 'inherit' }} onClick={() => void download(a)}>
              {a.fileName}
            </button>
            <span className="tm-small tm-muted">{formatBytes(a.sizeBytes)}・{a.uploadedBy.displayName}・{formatDateTime(a.uploadedAtUtc, user.timeZoneId)}</span>
            {a.canDelete && <Button size="small" variant="ghost" onClick={() => setDeleting(a)} aria-label={`${a.fileName} を削除`}>削除</Button>}
          </li>
        ))}
      </ul>
      <ConfirmDialog isOpen={deleting !== null} onOpenChange={(o) => !o && setDeleting(null)} title={`「${deleting?.fileName ?? ''}」を削除しますか？`}
        confirmLabel="削除する" busy={remove.isPending} onConfirm={() => deleting && remove.mutate(deleting.id)}>
        <p>削除したファイルは元に戻せません。</p>
      </ConfirmDialog>
    </section>
  )
}
