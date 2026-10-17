import { Navigate, useNavigate } from 'react-router'
import { Button } from '../../components/Button'
import { useToast } from '../../components/Toast'
import { useAuth } from './AuthProvider'
import { PasswordChangeForm } from './PasswordChangeForm'
import styles from './Auth.module.css'

/** SCR-02 パスワード変更（一時パスワードからの切替。他の画面には移動できない） */
export function ForcedPasswordChangePage() {
  const { status, user, applySession, logout } = useAuth()
  const navigate = useNavigate()
  const toast = useToast()
  if (status === 'anonymous') return <Navigate to="/login" replace />
  if (user !== null && !user.mustChangePassword) return <Navigate to="/" replace />
  return (
    <main className={styles.page}>
      <div className={styles.box}>
        <p className={styles.brand}>TaskManager</p>
        <h1 className="tm-page-title">パスワードの設定</h1>
        <p>最初に、ご自身で新しいパスワードを設定してください。</p>
        <PasswordChangeForm
          currentLabel="現在のパスワード（一時パスワード）"
          onChanged={(response) => {
            applySession(response)
            toast.success('パスワードを変更しました。')
            void navigate('/', { replace: true })
          }}
        />
        <div style={{ marginTop: 'var(--space-6)' }}>
          <Button variant="ghost" size="small" onClick={() => void logout()}>ログアウト</Button>
        </div>
      </div>
    </main>
  )
}
