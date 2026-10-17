import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate, useOutletContext } from 'react-router'
import { z } from 'zod'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Field } from '../../components/Field'
import { FormError } from '../../components/Feedback'
import { useToast } from '../../components/Toast'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { useCurrentUser } from '../auth/AuthProvider'
import { errorMessage } from '../tasks/taskApi'
import styles from './Projects.module.css'

const schema = z.object({
  name: z.string().trim().min(1, 'プロジェクト名を入力してください。').max(LIMITS.projectName, `プロジェクト名は${LIMITS.projectName}文字以内で入力してください。`),
  description: z.string().max(LIMITS.projectDescription, `説明は${LIMITS.projectDescription}文字以内で入力してください。`),
})
type Values = z.infer<typeof schema>

/** SCR-12 プロジェクト設定: 基本情報（アーカイブは危険操作として下部に区切って配置） */
export function ProjectGeneralSettingsPage() {
  const project = useOutletContext<Schemas['ProjectDetailResponse']>()
  const user = useCurrentUser()
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  usePageTitle('設定', project.name)
  const isOwner = project.myRole === 'owner'
  const editable = isOwner && !project.isArchived
  const [formError, setFormError] = useState<string | null>(null)
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)
  const [confirmKey, setConfirmKey] = useState('')
  const [busy, setBusy] = useState(false)
  const { register, handleSubmit, formState, setError, reset } = useForm<Values>({
    resolver: zodResolver(schema), values: { name: project.name, description: project.description ?? '' },
  })
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.project(project.key) })
    void queryClient.invalidateQueries({ queryKey: ['projects'] })
  }
  const onSubmit = handleSubmit(async (v) => {
    setFormError(null)
    try {
      await call(() => apiClient.PATCH('/api/v1/projects/{projectKey}', {
        params: { path: { projectKey: project.key } },
        body: { name: v.name, description: v.description.trim() || null, version: project.version },
      }))
      toast.success('プロジェクトの情報を保存しました')
      refresh()
    } catch (e) {
      if (e instanceof ApiError && e.code === 'project.name_taken') setError('name', { message: e.title })
      else if (e instanceof ApiError && e.code === 'concurrency.conflict') { setFormError('他の人が先に更新しました。最新の内容を表示しています。'); refresh(); reset() }
      else setFormError(errorMessage(e, '保存できませんでした。').title)
    }
  })
  const archive = async (value: boolean) => {
    setBusy(true)
    try {
      await call(() => value
        ? apiClient.POST('/api/v1/projects/{projectKey}/archive', { params: { path: { projectKey: project.key } } })
        : apiClient.POST('/api/v1/projects/{projectKey}/unarchive', { params: { path: { projectKey: project.key } } }))
      toast.success(value ? `プロジェクト「${project.name}」をアーカイブしました` : 'アーカイブを解除しました')
      setArchiveOpen(false)
      refresh()
    } catch (e) {
      toast.error(errorMessage(e, '操作できませんでした。').title)
    } finally {
      setBusy(false)
    }
  }
  const remove = async () => {
    setBusy(true)
    try {
      await call(() => apiClient.DELETE('/api/v1/projects/{projectKey}', { params: { path: { projectKey: project.key }, query: { confirmKey } } }))
      toast.success(`プロジェクト「${project.name}」を削除しました`)
      queryClient.removeQueries({ queryKey: queryKeys.project(project.key) })
      void queryClient.invalidateQueries({ queryKey: ['projects'] })
      void navigate('/projects')
    } catch (e) {
      toast.error(errorMessage(e, 'プロジェクトを削除できませんでした。').title)
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="tm-stack">
      <section className="tm-card">
        <h2 className="tm-section-title">基本情報</h2>
        <form onSubmit={(e) => void onSubmit(e)} noValidate>
          <FormError message={formError} />
          <Field label="プロジェクト名" required error={formState.errors.name?.message}>
            {(p) => <input {...p} {...register('name')} className="tm-input" disabled={!editable} maxLength={LIMITS.projectName} />}
          </Field>
          <Field label="プロジェクトキー" hint="プロジェクトキーは変更できません（タスク番号 SALES-12 などがそのまま使えるようにするため）。">
            {(p) => <input {...p} value={project.key} readOnly className="tm-input" style={{ maxWidth: 200 }} />}
          </Field>
          <Field label="説明" error={formState.errors.description?.message}>
            {(p) => <textarea {...p} {...register('description')} className="tm-textarea" disabled={!editable} maxLength={LIMITS.projectDescription} />}
          </Field>
          {editable && <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="保存しています…">保存</Button>}
        </form>
      </section>
      {isOwner && (
        <section className={`tm-card ${styles.danger}`}>
          <h2 className="tm-section-title">アーカイブ</h2>
          {project.isArchived ? (
            <>
              <p>このプロジェクトはアーカイブされています。解除すると、メンバーが再び編集できるようになります。</p>
              <Button onClick={() => void archive(false)} busy={busy} busyText="処理しています…">アーカイブを解除する</Button>
            </>
          ) : (
            <>
              <p>終わったプロジェクトをアーカイブすると、すべてのメンバーが閲覧のみできる状態になります。あとから元に戻せます。</p>
              <Button variant="danger" onClick={() => setArchiveOpen(true)}>アーカイブする</Button>
            </>
          )}
        </section>
      )}
      {user.systemRole === 'administrator' && project.isArchived && (
        <section className={`tm-card ${styles.danger}`}>
          <h2 className="tm-section-title">プロジェクトの削除（システム管理者）</h2>
          <p>プロジェクトとすべてのタスク・コメント・添付ファイルを削除します。元に戻せません。</p>
          <Button variant="danger" onClick={() => { setConfirmKey(''); setDeleteOpen(true) }}>プロジェクトを削除する</Button>
        </section>
      )}
      <ConfirmDialog isOpen={archiveOpen} onOpenChange={setArchiveOpen} title={`プロジェクト「${project.name}」をアーカイブしますか？`}
        confirmLabel="アーカイブする" busy={busy} onConfirm={() => void archive(true)}>
        <p>アーカイブすると、すべてのメンバーが閲覧のみできる状態になります。あとから元に戻せます。</p>
      </ConfirmDialog>
      <ConfirmDialog isOpen={deleteOpen} onOpenChange={setDeleteOpen} title={`プロジェクト「${project.name}」を削除しますか？`}
        confirmLabel="削除する" busy={busy} onConfirm={() => { if (confirmKey.trim().toUpperCase() === project.key) void remove() }}>
        <p>すべてのタスク・コメント・添付ファイル・変更履歴が削除され、元に戻せません。</p>
        <label className="tm-field__label" htmlFor="confirm-key">確認のため、プロジェクトキー「{project.key}」を入力してください</label>
        <input id="confirm-key" className="tm-input" value={confirmKey} onChange={(e) => setConfirmKey(e.target.value)} autoComplete="off" />
        {confirmKey !== '' && confirmKey.trim().toUpperCase() !== project.key && <p className="tm-field__error">キーが一致しません。</p>}
      </ConfirmDialog>
    </div>
  )
}
