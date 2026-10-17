import { useEffect } from 'react'

/** 画面タイトル（ui-design.md §7.1）。「見積書の作成（SALES-12）｜営業支援｜TaskManager」の形式 */
export function usePageTitle(...parts: (string | null | undefined)[]) {
  const title = [...parts.filter((p): p is string => typeof p === 'string' && p.length > 0), 'TaskManager'].join('｜')
  useEffect(() => {
    document.title = title
  }, [title])
}
