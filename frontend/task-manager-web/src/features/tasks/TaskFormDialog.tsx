import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { useStatuses } from '../../api/queries'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { Field } from '../../components/Field'
import { FormError } from '../../components/Feedback'
import { useToast } from '../../components/Toast'
import { LIMITS } from '../../constants/limits'
import { todayIn } from '../../utils/dates'
import { taskPath } from '../../utils/taskKey'
import { useCurrentUser } from '../auth/AuthProvider'
import { invalidateTaskQueries } from './taskApi'
import { AssigneeSelect, DateInput, MilestoneSelect, PrioritySelect, StatusSelect, TagPicker } from './TaskFields'

const schema = z
  .object({
    title: z.string().trim().min(1, 'タイトルを入力してください。').max(LIMITS.title, `タイトルは${LIMITS.title}文字以内で入力してください。`)
      .refine((v) => !/[\r\n]/.test(v), 'タイトルに改行は使えません。'),
    description: z.string().max(LIMITS.description, `説明は${LIMITS.description}文字以内で入力してください。`),
    statusId: z.string(),
    priority: z.enum(['low', 'normal', 'high', 'urgent']),
    assigneeUserId: z.string().nullable(),
    startDate: z.string().nullable(),
    dueDate: z.string().nullable(),
    milestoneId: z.string().nullable(),
    tagIds: z.array(z.string()).max(LIMITS.tagsPerTask),
  })
  .refine((v) => v.startDate === null || v.dueDate === null || v.startDate <= v.dueDate, {
    path: ['dueDate'], message: '期限は開始日以降の日付にしてください。',
  })
type FormValues = z.infer<typeof schema>

export type TaskFormDefaults = {
  statusId?: string
  milestoneId?: string | null
  parent?: { number: number; title: string }
}

/** SCR-09 タスク作成ダイアログ（ux-design.md F-1） */
export function TaskFormDialog({ projectKey, isOpen, onOpenChange, defaults }: {
  projectKey: string
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  defaults?: TaskFormDefaults
}) {
  const user = useCurrentUser()
  const toast = useToast()
  const queryClient = useQueryClient()
  const statuses = useStatuses(projectKey)
  const today = todayIn(user.timeZoneId)
  // このダイアログは開くときにマウントされる（呼び出し側で条件付き描画）
  const [showDetails, setShowDetails] = useState(defaults?.milestoneId != null)
  const [continueAdding, setContinueAdding] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const defaultStatusId = defaults?.statusId ?? statuses.data?.find((s) => s.isDefault)?.id ?? ''
  const emptyValues: FormValues = {
    title: '', description: '', statusId: defaultStatusId, priority: 'normal', assigneeUserId: null,
    startDate: null, dueDate: null, milestoneId: defaults?.milestoneId ?? null, tagIds: [],
  }
  const { register, handleSubmit, control, formState, reset, setError, setFocus, getValues } = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: emptyValues,
    resetOptions: { keepDirtyValues: true },
  })

  const isDirty = () => {
    const v = getValues()
    return v.title.trim() !== '' || v.description.trim() !== ''
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const body: Schemas['CreateTaskRequest'] = {
        title: values.title.trim(),
        description: values.description.trim() === '' ? null : values.description,
        statusId: values.statusId || null,
        priority: values.priority,
        assigneeUserId: values.assigneeUserId,
        startDate: values.startDate,
        dueDate: values.dueDate,
        milestoneId: values.milestoneId,
        tagIds: values.tagIds,
        parentTaskNumber: defaults?.parent?.number ?? null,
      }
      const created = await call(() => apiClient.POST('/api/v1/projects/{projectKey}/tasks', { params: { path: { projectKey } }, body }))
      invalidateTaskQueries(queryClient, projectKey, defaults?.parent?.number)
      toast.success(`${created.key} を追加しました`, { to: taskPath(projectKey, created.number), label: '開く' })
      if (continueAdding) {
        reset({ ...values, title: '', description: '' })
        setFocus('title')
      } else {
        reset(emptyValues)
        onOpenChange(false)
      }
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(e.fieldErrors)) {
          if (field in emptyValues) setError(field as keyof FormValues, { message: messages[0] })
          else setFormError(messages[0] ?? e.title)
        }
        setShowDetails(true)
      } else {
        setFormError(e instanceof ApiError ? `${e.title}${e.detail ? ` ${e.detail}` : ''}` : 'タスクを追加できませんでした。')
      }
    }
  })

  return (
    <Dialog isOpen={isOpen} onOpenChange={onOpenChange} title={defaults?.parent ? 'サブタスクを追加' : 'タスクを追加'} wide
      canClose={() => !isDirty() || window.confirm('入力内容を破棄しますか？')}>
      <form onSubmit={(e) => void onSubmit(e)} noValidate>
        {defaults?.parent && <p className="tm-small tm-muted">親タスク: {projectKey}-{defaults.parent.number}「{defaults.parent.title}」</p>}
        <FormError message={formError} />
        <Field label="タイトル" required error={formState.errors.title?.message}>
          {(p) => (
            <input {...p} {...register('title')} className="tm-input" autoFocus maxLength={LIMITS.title}
              onKeyDown={(e) => { if (e.key === 'Enter' && !e.nativeEvent.isComposing) { e.preventDefault(); void onSubmit() } }} />
          )}
        </Field>
        <button type="button" className="tm-btn tm-btn--ghost tm-btn--small" aria-expanded={showDetails} onClick={() => setShowDetails((v) => !v)}>
          {showDetails ? '▾ 詳細を閉じる' : '▸ 詳細を設定（担当者・期限・優先度など）'}
        </button>
        {showDetails && (
          <div style={{ marginTop: 'var(--space-4)', display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', columnGap: 'var(--space-4)' }}>
            <Field label="ステータス" error={formState.errors.statusId?.message}>
              {(p) => <Controller control={control} name="statusId" render={({ field }) => <StatusSelect {...p} projectKey={projectKey} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <Field label="優先度">
              {(p) => <Controller control={control} name="priority" render={({ field }) => <PrioritySelect id={p.id} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <Field label="担当者" error={formState.errors.assigneeUserId?.message}>
              {(p) => <Controller control={control} name="assigneeUserId" render={({ field }) => <AssigneeSelect {...p} projectKey={projectKey} currentUserId={user.id} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <Field label="マイルストーン" error={formState.errors.milestoneId?.message}>
              {(p) => <Controller control={control} name="milestoneId" render={({ field }) => <MilestoneSelect {...p} projectKey={projectKey} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <Field label="開始日" error={formState.errors.startDate?.message}>
              {(p) => <Controller control={control} name="startDate" render={({ field }) => <DateInput {...p} today={today} showShortcuts={false} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <Field label="期限" error={formState.errors.dueDate?.message}>
              {(p) => <Controller control={control} name="dueDate" render={({ field }) => <DateInput {...p} today={today} value={field.value} onChange={field.onChange} />} />}
            </Field>
            <div style={{ gridColumn: '1 / -1' }}>
              <Field label="タグ" error={formState.errors.tagIds?.message}>
                {(p) => <Controller control={control} name="tagIds" render={({ field }) => <TagPicker id={p.id} projectKey={projectKey} value={field.value} onChange={field.onChange} />} />}
              </Field>
              <Field label="説明" hint="Markdown で書けます。" error={formState.errors.description?.message}>
                {(p) => <textarea {...p} {...register('description')} className="tm-textarea" maxLength={LIMITS.description} />}
              </Field>
            </div>
          </div>
        )}
        <div className="tm-dialog__footer" style={{ alignItems: 'center' }}>
          <label className="tm-checkbox" style={{ marginRight: 'auto' }}>
            <input type="checkbox" checked={continueAdding} onChange={(e) => setContinueAdding(e.target.checked)} />
            続けて追加
          </label>
          <Button onClick={() => onOpenChange(false)}>キャンセル</Button>
          <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="追加しています…">追加する</Button>
        </div>
      </form>
    </Dialog>
  )
}
