import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { Loading } from '../../components/Feedback'
import { isSafeInternalPath } from '../../utils/url'
import { useAuth } from './AuthProvider'

/** ログイン必須（未ログインならログイン画面へ。元の URL を next に保持する） */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status, user } = useAuth()
  const location = useLocation()
  if (status === 'loading') return <Loading label="ログイン状態を確認しています…" />
  if (status === 'anonymous' || user === null) {
    const next = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?next=${next}`} replace />
  }
  if (user.mustChangePassword) return <Navigate to="/change-password" replace />
  return <>{children}</>
}

export function RequireSystemAdmin({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  if (user?.systemRole !== 'administrator') return <Navigate to="/" replace />
  return <>{children}</>
}

/** 「次の画面」として安全な画面内パスだけを受け付ける（オープンリダイレクト対策） */
export function safeNextPath(next: string | null): string {
  if (next === null) return '/'
  return isSafeInternalPath(next) && !next.startsWith('/login') ? next : '/'
}
