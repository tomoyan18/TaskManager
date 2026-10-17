import { useEffect, useRef } from 'react'

/** 入力欄にフォーカスがある間は無効（ux-design.md §4） */
function isTyping(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false
  const tag = target.tagName
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable
}

/** 単キー（"/", "n", "?"）と 2 ストローク（"g h"）のショートカット */
export function useKeyboardShortcuts(handlers: Record<string, () => void>) {
  const pending = useRef<string | null>(null)
  const handlersRef = useRef(handlers)
  useEffect(() => {
    handlersRef.current = handlers
  })
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) return
      if (document.querySelector('[role="dialog"], [role="alertdialog"]') !== null) return
      const key = event.key.toLowerCase()
      const sequence = pending.current !== null ? `${pending.current} ${key}` : key
      const handler = handlersRef.current[sequence] ?? handlersRef.current[event.key]
      if (handler !== undefined) {
        event.preventDefault()
        pending.current = null
        handler()
        return
      }
      pending.current = key === 'g' ? 'g' : null
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])
}

/** 「タスクを追加」ショートカット（N）を、表示中の画面に伝えるイベント */
export const NEW_TASK_EVENT = 'tm:new-task'
