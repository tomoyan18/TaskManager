import { useState } from 'react'
import { apiClient, call } from '../../api/client'
import { Button } from '../../components/Button'
import { useToast } from '../../components/Toast'
import { SYSTEM_ROLE_LABELS, TIME_ZONES } from '../../constants/labels'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { useAuth, useCurrentUser } from '../auth/AuthProvider'
import { PasswordChangeForm } from '../auth/PasswordChangeForm'
import { errorMessage } from '../tasks/taskApi'

/** SCR-16 個人設定（プロフィール・パスワード変更） */
export function PersonalSettingsPage() {
  usePageTitle('個人設定')
  const user = useCurrentUser()
  const { setUser, applySession } = useAuth()
  const toast = useToast()
  const [displayName, setDisplayName] = useState(user.displayName)
  const [timeZoneId, setTimeZoneId] = useState(user.timeZoneId)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const zones = TIME_ZONES.some((z) => z.id === user.timeZoneId) ? TIME_ZONES : [{ id: user.timeZoneId, label: user.timeZoneId }, ...TIME_ZONES]
  const save = async () => {
    if (displayName.trim() === '') { setError('表示名を入力してください。'); return }
    setBusy(true)
    setError(null)
    try {
      const updated = await call(() => apiClient.PATCH('/api/v1/me', { body: { displayName: displayName.trim(), timeZoneId } }))
      setUser(updated)
      toast.success('プロフィールを保存しました')
    } catch (e) {
      setError(errorMessage(e, '保存できませんでした。').title)
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="tm-stack" style={{ maxWidth: 640 }}>
      <h1 className="tm-page-title">個人設定</h1>
      <section className="tm-card">
        <h2 className="tm-section-title">プロフィール</h2>
        <form onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
          {error && <div className="tm-alert tm-alert--error" role="alert" style={{ marginBottom: 'var(--space-3)' }}>{error}</div>}
          <div className="tm-field">
            <span className="tm-field__label">メールアドレス</span>
            <span>{user.email}</span>
            <span className="tm-field__hint">メールアドレスの変更はシステム管理者に依頼してください。</span>
          </div>
          <div className="tm-field">
            <span className="tm-field__label">システムロール</span>
            <span>{SYSTEM_ROLE_LABELS[user.systemRole]}</span>
          </div>
          <div className="tm-field">
            <label className="tm-field__label" htmlFor="me-name">表示名<span className="tm-field__required">必須</span></label>
            <input id="me-name" className="tm-input" value={displayName} maxLength={LIMITS.displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div className="tm-field">
            <label className="tm-field__label" htmlFor="me-tz">タイムゾーン</label>
            <select id="me-tz" className="tm-select" value={timeZoneId} onChange={(e) => setTimeZoneId(e.target.value)}>
              {zones.map((z) => <option key={z.id} value={z.id}>{z.label}</option>)}
            </select>
            <span className="tm-field__hint">期限切れ・今日の判定と、日時の表示に使います。</span>
          </div>
          <Button type="submit" variant="primary" busy={busy} busyText="保存しています…">保存</Button>
        </form>
      </section>
      <section className="tm-card">
        <h2 className="tm-section-title">パスワードの変更</h2>
        <p className="tm-small tm-muted">変更すると、ほかの端末ではログアウトされます。</p>
        <PasswordChangeForm onChanged={(response) => { applySession(response); toast.success('パスワードを変更しました。') }} />
      </section>
    </div>
  )
}
