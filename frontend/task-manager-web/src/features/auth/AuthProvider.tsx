import { useQueryClient } from '@tanstack/react-query'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  apiClient, authEvents, call, PASSWORD_CHANGE_REQUIRED, refreshSession, SESSION_EXPIRED, SESSION_REFRESHED, tokenStore,
  type Schemas,
} from '../../api/client'

type CurrentUser = Schemas['CurrentUserResponse']
type AuthStatus = 'loading' | 'anonymous' | 'authenticated'

type AuthContextValue = {
  status: AuthStatus
  user: CurrentUser | null
  /** セッション切れでログイン画面に戻った場合 true（案内文を出す） */
  sessionExpired: boolean
  login: (email: string, password: string) => Promise<CurrentUser>
  logout: () => Promise<void>
  /** パスワード変更などで新しいトークンを受け取ったとき */
  applySession: (response: Schemas['LoginResponse']) => void
  setUser: (user: CurrentUser) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * 認証状態（ui-design.md §7.2）。アクセストークンはメモリのみに保持し、画面の再読み込み時は
 * リフレッシュ Cookie でトークンを取り直す（authentication-authorization.md §3.2）。
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUserState] = useState<CurrentUser | null>(null)
  const [sessionExpired, setSessionExpired] = useState(false)

  const applySession = useCallback((response: Schemas['LoginResponse']) => {
    tokenStore.set(response.accessToken)
    setUserState(response.user)
    setStatus('authenticated')
    setSessionExpired(false)
  }, [])

  useEffect(() => {
    let cancelled = false
    void refreshSession().then((response) => {
      if (cancelled) return
      if (response === null) {
        setStatus('anonymous')
      } else {
        applySession(response)
      }
    })
    return () => {
      cancelled = true
    }
  }, [applySession])

  useEffect(() => {
    const onExpired = () => {
      setUserState(null)
      setStatus('anonymous')
      setSessionExpired(true)
      queryClient.clear()
    }
    const onPasswordChange = () => setUserState((u) => (u === null ? u : { ...u, mustChangePassword: true }))
    const onRefreshed = (event: Event) => {
      const detail = (event as CustomEvent<Schemas['LoginResponse']>).detail
      setUserState(detail.user)
    }
    authEvents.addEventListener(SESSION_EXPIRED, onExpired)
    authEvents.addEventListener(PASSWORD_CHANGE_REQUIRED, onPasswordChange)
    authEvents.addEventListener(SESSION_REFRESHED, onRefreshed)
    return () => {
      authEvents.removeEventListener(SESSION_EXPIRED, onExpired)
      authEvents.removeEventListener(PASSWORD_CHANGE_REQUIRED, onPasswordChange)
      authEvents.removeEventListener(SESSION_REFRESHED, onRefreshed)
    }
  }, [queryClient])

  const login = useCallback(
    async (email: string, password: string) => {
      const response = await call(() => apiClient.POST('/api/v1/auth/login', { body: { email, password } }))
      applySession(response)
      return response.user
    },
    [applySession],
  )

  const logout = useCallback(async () => {
    try {
      await fetch('/api/v1/auth/logout', { method: 'POST', credentials: 'same-origin' })
    } finally {
      tokenStore.set(null)
      setUserState(null)
      setStatus('anonymous')
      setSessionExpired(false)
      queryClient.clear()
    }
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, sessionExpired, login, logout, applySession, setUser: setUserState }),
    [status, user, sessionExpired, login, logout, applySession],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (value === null) throw new Error('AuthProvider の内側で使用してください')
  return value
}

/** ログイン中の利用者（ログイン必須の画面でのみ使う） */
export function useCurrentUser(): CurrentUser {
  const { user } = useAuth()
  if (user === null) throw new Error('ログインしていません')
  return user
}
