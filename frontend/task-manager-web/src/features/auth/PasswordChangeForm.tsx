import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { FormError } from '../../components/Feedback'
import { LIMITS } from '../../constants/limits'
import styles from './Auth.module.css'

const schema = z
  .object({
    currentPassword: z.string().min(1, '現在のパスワードを入力してください。'),
    newPassword: z.string()
      .min(LIMITS.passwordMin, `新しいパスワードは${LIMITS.passwordMin}文字以上で設定してください。`)
      .max(LIMITS.passwordMax, `新しいパスワードは${LIMITS.passwordMax}文字以内で設定してください。`),
    confirmPassword: z.string().min(1, '確認のため、もう一度入力してください。'),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: '新しいパスワードが一致しません。' })
type FormValues = z.infer<typeof schema>

/** パスワード変更フォーム（SCR-02 と SCR-16 で共用） */
export function PasswordChangeForm({ onChanged, currentLabel = '現在のパスワード' }: {
  onChanged: (response: Schemas['LoginResponse']) => void
  currentLabel?: string
}) {
  const [formError, setFormError] = useState<string | null>(null)
  const { register, handleSubmit, formState, setError, control, reset } = useForm<FormValues>({ resolver: zodResolver(schema) })
  const newPassword = useWatch({ control, name: 'newPassword' }) ?? ''

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const response = await call(() => apiClient.POST('/api/v1/auth/password', {
        body: { currentPassword: values.currentPassword, newPassword: values.newPassword },
      }))
      reset()
      onChanged(response)
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) {
        for (const [field, messages] of Object.entries(e.fieldErrors)) {
          if (field === 'currentPassword' || field === 'newPassword') setError(field, { message: messages[0] })
        }
      } else {
        setFormError(e instanceof ApiError ? e.title : 'パスワードを変更できませんでした。')
      }
    }
  })

  const lengthOk = newPassword.length >= LIMITS.passwordMin
  return (
    <form onSubmit={(e) => void onSubmit(e)} noValidate>
      <FormError message={formError} />
      <Field label={currentLabel} required error={formState.errors.currentPassword?.message}>
        {(p) => <input {...p} {...register('currentPassword')} type="password" autoComplete="current-password" className="tm-input" />}
      </Field>
      <Field label="新しいパスワード" required error={formState.errors.newPassword?.message}
        hint="12 文字以上で設定してください。長いほど安全です。メールアドレスや名前を含むもの、よく使われるものは使えません。">
        {(p) => <input {...p} {...register('newPassword')} type="password" autoComplete="new-password" className="tm-input" />}
      </Field>
      <ul className={styles.rules} aria-live="polite">
        <li className={lengthOk ? styles.ok : styles.ng}>{lengthOk ? '✓' : '・'} 12 文字以上（現在 {newPassword.length} 文字）</li>
      </ul>
      <Field label="新しいパスワード（確認）" required error={formState.errors.confirmPassword?.message}>
        {(p) => <input {...p} {...register('confirmPassword')} type="password" autoComplete="new-password" className="tm-input" />}
      </Field>
      <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="変更しています…">パスワードを変更する</Button>
    </form>
  )
}
