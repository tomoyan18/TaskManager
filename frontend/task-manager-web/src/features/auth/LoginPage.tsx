import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useNavigate, useSearchParams } from 'react-router'
import { z } from 'zod'
import { ApiError } from '../../api/client'
import { Button } from '../../components/Button'
import { Field } from '../../components/Field'
import { FormError } from '../../components/Feedback'
import { useAuth } from './AuthProvider'
import { safeNextPath } from './RouteGuards'
import styles from './Auth.module.css'

const schema = z.object({
  email: z.string().trim().min(1, 'メールアドレスを入力してください。').max(254, 'メールアドレスが長すぎます。'),
  password: z.string().min(1, 'パスワードを入力してください。').max(128, 'パスワードが長すぎます。'),
})
type FormValues = z.infer<typeof schema>

/** SCR-01 ログイン */
export function LoginPage() {
  const { status, user, login, sessionExpired } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)
  const [showPassword, setShowPassword] = useState(false)
  const { register, handleSubmit, formState, resetField } = useForm<FormValues>({ resolver: zodResolver(schema) })
  const next = safeNextPath(params.get('next'))

  if (status === 'authenticated' && user !== null) {
    return <Navigate to={user.mustChangePassword ? '/change-password' : next} replace />
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const loggedIn = await login(values.email, values.password)
      void navigate(loggedIn.mustChangePassword ? '/change-password' : next, { replace: true })
    } catch (e) {
      resetField('password')
      if (e instanceof ApiError && e.code === 'auth.locked') {
        setFormError(`${e.title}${e.detail ? ` ${e.detail}` : ''}`)
      } else if (e instanceof ApiError && e.status === 429) {
        setFormError('しばらく時間をおいてから再度お試しください。')
      } else if (e instanceof ApiError && e.status !== 0 && e.status < 500) {
        setFormError(e.title)
      } else {
        setFormError('ログインできませんでした。時間をおいて再度お試しください。')
      }
    }
  })

  return (
    <main className={styles.page}>
      <div className={styles.box}>
        <p className={styles.brand}>TaskManager</p>
        <h1 className="tm-page-title" style={{ marginBottom: 'var(--space-4)' }}>ログイン</h1>
        {sessionExpired && formError === null && (
          <div className="tm-alert tm-alert--info" role="status" style={{ marginBottom: 'var(--space-4)' }}>
            ログインの有効期限が切れました。もう一度ログインしてください。
          </div>
        )}
        <FormError message={formError} />
        <form onSubmit={(e) => void onSubmit(e)} noValidate>
          <Field label="メールアドレス" error={formState.errors.email?.message}>
            {(p) => <input {...p} {...register('email')} type="email" autoComplete="username" className="tm-input" autoFocus />}
          </Field>
          <Field label="パスワード" error={formState.errors.password?.message}>
            {(p) => (
              <div className="tm-row" style={{ flexWrap: 'nowrap' }}>
                <input {...p} {...register('password')} type={showPassword ? 'text' : 'password'} autoComplete="current-password" className="tm-input" />
                <Button variant="ghost" size="small" onClick={() => setShowPassword((v) => !v)} aria-pressed={showPassword}>
                  {showPassword ? '隠す' : '表示'}
                </Button>
              </div>
            )}
          </Field>
          <Button type="submit" variant="primary" busy={formState.isSubmitting} busyText="ログインしています…" style={{ width: '100%' }}>
            ログイン
          </Button>
        </form>
        <p className="tm-small tm-muted" style={{ marginTop: 'var(--space-4)' }}>
          パスワードを忘れた場合は、システム管理者に再発行を依頼してください。
        </p>
      </div>
    </main>
  )
}
