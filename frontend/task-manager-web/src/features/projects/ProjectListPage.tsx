import { zodResolver } from '@hookform/resolvers/zod'
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { z } from 'zod'
import { apiClient, ApiError, call } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { Field } from '../../components/Field'
import { EmptyState, ErrorPanel, FormError, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { Pagination } from '../../components/Pagination'
import { useToast } from '../../components/Toast'
import { PROJECT_ROLE_LABELS } from '../../constants/labels'
import { LIMITS, PROJECT_KEY_PATTERN } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { useCurrentUser } from '../auth/AuthProvider'

/** SCR-05 プロジェクト一覧（表。REQ-PRJ-01） */
export function ProjectListPage() {
  usePageTitle('プロジェクト')
  const user = useCurrentUser()
  const [params, setParams] = useSearchParams()
  const [createOpen, setCreateOpen] = useState(false)
  const includeArchived = params.get('archived') === 'true'
  const all = params.get('scope') === 'all' && user.systemRole === 'administrator'
  const q = params.get('q') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const set = (key: string, value: string | null) => setParams((p) => {
    const n = new URLSearchParams(p)
    if (value === null || value === '') n.delete(key); else n.set(key, value)
    if (key !== 'page') n.delete('page')
    return n
  })
  const projects = useQuery({
    queryKey: queryKeys.projects({ includeArchived, all, q, page }),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects', {
      params: { query: { includeArchived, q: q || undefined, scope: all ? 'all' : undefined, page, pageSize: 50 } },
    })),
    placeholderData: keepPreviousData,
  })
  return (
    <div className="tm-stack">
      <div className="tm-row">
        <h1 className="tm-page-title">プロジェクト</h1>
        <span className="tm-spacer" />
        <Button variant="primary" onClick={() => setCreateOpen(true)}><Icon name="plus" size={16} />プロジェクトを作成</Button>
      </div>
      <div className="tm-row">
        <label className="tm-visually-hidden" htmlFor="project-q">名前・キーで絞り込む</label>
        <input id="project-q" className="tm-input tm-select--inline" style={{ width: 240 }} placeholder="名前・キーで絞り込む" defaultValue={q}
          onKeyDown={(e) => { if (e.key === 'Enter') set('q', e.currentTarget.value.trim()) }} onBlur={(e) => set('q', e.target.value.trim())} />
        <label className="tm-checkbox tm-small"><input type="checkbox" checked={includeArchived} onChange={(e) => set('archived', e.target.checked ? 'true' : null)} />アーカイブ済みも表示</label>
        {user.systemRole === 'administrator' && (
          <label className="tm-checkbox tm-small"><input type="checkbox" checked={all} onChange={(e) => set('scope', e.target.checked ? 'all' : null)} />参加していないプロジェクトも表示（システム管理者）</label>
        )}
      </div>
      {projects.isPending && <Loading rows={5} />}
      {projects.isError && <ErrorPanel error={projects.error} onRetry={() => void projects.refetch()} />}
      {projects.isSuccess && projects.data.items.length === 0 && (
        q ? <EmptyState title="条件に合うプロジェクトはありません" text="絞り込み条件を変えてみてください。" />
          : <EmptyState title="まだプロジェクトに参加していません" text="プロジェクトを作成するか、管理者に追加を依頼してください。"
              action={<Button variant="primary" onClick={() => setCreateOpen(true)}>プロジェクトを作成</Button>} />
      )}
      {projects.isSuccess && projects.data.items.length > 0 && (
        <>
          <div className="tm-table-wrap">
            <table className="tm-table">
              <caption className="tm-visually-hidden">プロジェクトの一覧</caption>
              <thead>
                <tr><th scope="col">名前</th><th scope="col">キー</th><th scope="col">あなたのロール</th><th scope="col">未完了</th><th scope="col">期限切れ</th><th scope="col">管理者</th></tr>
              </thead>
              <tbody>
                {projects.data.items.map((p) => (
                  <tr key={p.key}>
                    <td>
                      {p.myRole ? <Link to={`/projects/${p.key}/tasks`} className="tm-link--plain" style={{ fontWeight: 600 }}>{p.name}</Link> : <span>{p.name}</span>}
                      {p.isArchived && <span className="tm-small tm-muted">（アーカイブ済み）</span>}
                    </td>
                    <td className="tm-key">{p.key}</td>
                    <td>{p.myRole ? PROJECT_ROLE_LABELS[p.myRole] : <span className="tm-muted">参加していません</span>}</td>
                    <td>{p.myRole ? p.openTaskCount : '—'}</td>
                    <td className={p.overdueTaskCount > 0 ? 'tm-due--overdue' : undefined}>{p.myRole ? p.overdueTaskCount : '—'}</td>
                    <td className="tm-small">
                      {p.ownerNames.join('、') || '—'}
                      {!p.myRole && user.systemRole === 'administrator' && <> <Link to={`/projects/${p.key}/settings/members`} className="tm-link">メンバー管理</Link></>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={projects.data.page} pageSize={projects.data.pageSize} totalCount={projects.data.totalCount}
            totalPages={projects.data.totalPages} onChange={(p) => set('page', p === 1 ? null : String(p))} />
        </>
      )}
      <CreateProjectDialog isOpen={createOpen} onOpenChange={setCreateOpen} />
    </div>
  )
}

const createSchema = z.object({
  name: z.string().trim().min(1, 'プロジェクト名を入力してください。').max(LIMITS.projectName, `プロジェクト名は${LIMITS.projectName}文字以内で入力してください。`),
  key: z.string().trim().regex(PROJECT_KEY_PATTERN, 'プロジェクトキーは英字で始まる 2〜10 文字の英字・数字で入力してください。'),
  description: z.string().max(LIMITS.projectDescription, `説明は${LIMITS.projectDescription}文字以内で入力してください。`),
})
type CreateValues = z.infer<typeof createSchema>

function CreateProjectDialog({ isOpen, onOpenChange }: { isOpen: boolean; onOpenChange: (open: boolean) => void }) {
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, formState, setError, reset } = useForm<CreateValues>({
    resolver: zodResolver(createSchema), defaultValues: { name: '', key: '', description: '' },
  })
  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const created = await call(() => apiClient.POST('/api/v1/projects', {
        body: { name: values.name, key: values.key.toUpperCase(), description: values.description.trim() || null },
      }))
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
      toast.success(`プロジェクト「${created.name}」を作成しました`)
      reset()
      onOpenChange(false)
      void navigate(`/projects/${created.key}/tasks`)
    } catch (e) {
      if (e instanceof ApiError && e.code === 'project.key_taken') setError('key', { message: e.title })
      else if (e instanceof ApiError && e.code === 'project.name_taken') setError('name', { message: e.title })
      else if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        for (const [f, m] of Object.entries(e.fieldErrors)) {
          if (f === 'name' || f === 'key' || f === 'description') setError(f, { message: m[0] })
        }
      } else setFormError(e instanceof ApiError ? e.title : 'プロジェクトを作成できませんでした。')
    }
  })
  return (
    <Dialog isOpen={isOpen} onOpenChange={onOpenChange} title="プロジェクトを作成">
      <form onSubmit={(e) => void onSubmit(e)} noValidate>
        <FormError message={formError} />
        <Field label="プロジェクト名" required error={formState.errors.name?.message}>
          {(p) => <input {...p} {...register('name')} className="tm-input" autoFocus maxLength={LIMITS.projectName} />}
        </Field>
        <Field label="プロジェクトキー" required error={formState.errors.key?.message}
          hint="タスク番号の先頭に付きます（例: SALES-12）。後から変更できません。英字で始まる 2〜10 文字の英字・数字。">
          {(p) => <input {...p} {...register('key')} className="tm-input" style={{ textTransform: 'uppercase', maxWidth: 200 }} maxLength={LIMITS.projectKeyMax} autoComplete="off" />}
        </Field>
        <Field label="説明" error={formState.errors.description?.message}>
          {(p) => <textarea {...p} {...register('description')} className="tm-textarea" maxLength={LIMITS.projectDescription} />}
        </Field>
        <div className="tm-dialog__footer">
          <Button onClick={() => onOpenChange(false)}>キャンセル</Button>
          <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="作成しています…">作成する</Button>
        </div>
      </form>
    </Dialog>
  )
}
