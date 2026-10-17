import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'

type ToastKind = 'success' | 'error'
type ToastItem = { id: number; kind: ToastKind; message: string; detail?: string; link?: { to: string; label: string } }
type ToastApi = {
  success: (message: string, link?: { to: string; label: string }) => void
  error: (message: string, detail?: string) => void
}

const ToastContext = createContext<ToastApi | null>(null)

/**
 * トースト（ui-design.md §6.3）。成功は 4 秒で消え、失敗は閉じるまで表示する。
 * RouterProvider の外側に置くため Link は使えない。リンクの遷移は onNavigate（router.navigate）に任せる
 */
export function ToastProvider({ children, onNavigate }: { children: ReactNode; onNavigate: (to: string) => void }) {
  const [toasts, setToasts] = useState<ToastItem[]>([])
  const nextId = useRef(1)
  const remove = useCallback((id: number) => setToasts((list) => list.filter((t) => t.id !== id)), [])
  const push = useCallback(
    (item: Omit<ToastItem, 'id'>) => {
      const id = nextId.current++
      setToasts((list) => [...list.slice(-3), { ...item, id }])
      if (item.kind === 'success') setTimeout(() => remove(id), 4000)
    },
    [remove],
  )
  const api = useMemo<ToastApi>(
    () => ({
      success: (message, link) => push({ kind: 'success', message, link }),
      error: (message, detail) => push({ kind: 'error', message, detail }),
    }),
    [push],
  )
  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="tm-toasts" aria-live="polite" aria-relevant="additions">
        {toasts.map((t) => (
          <div key={t.id} className={t.kind === 'error' ? 'tm-toast tm-toast--error' : 'tm-toast'} role={t.kind === 'error' ? 'alert' : 'status'}>
            <div>
              <div>{t.message}</div>
              {t.detail && <div className="tm-small">{t.detail}</div>}
              {t.link && <ToastLink to={t.link.to} label={t.link.label} onNavigate={(to) => { remove(t.id); onNavigate(to) }} />}
            </div>
            <button type="button" className="tm-toast__close" aria-label="通知を閉じる" onClick={() => remove(t.id)}>✕</button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

function ToastLink({ to, label, onNavigate }: { to: string; label: string; onNavigate: (to: string) => void }) {
  return (
    <a
      href={to}
      onClick={(e) => {
        // 修飾キー付き（新しいタブ等）はブラウザに任せる
        if (e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return
        e.preventDefault()
        onNavigate(to)
      }}
    >
      {label}
    </a>
  )
}

export function useToast(): ToastApi {
  const value = useContext(ToastContext)
  if (value === null) throw new Error('ToastProvider の内側で使用してください')
  return value
}
