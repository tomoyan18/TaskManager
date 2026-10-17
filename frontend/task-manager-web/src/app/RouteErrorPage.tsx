import { useRouteError } from 'react-router'
import { ApiError } from '../api/client'
import { Button } from '../components/Button'

/** SCR-19 エラー（画面の描画中の予期しないエラー） */
export function RouteErrorPage() {
  const error = useRouteError()
  const traceId = error instanceof ApiError ? error.traceId : null
  return (
    <main className="tm-empty" role="alert">
      <h1 className="tm-page-title">問題が発生しました</h1>
      <p className="tm-empty__text">画面を表示できませんでした。再読み込みしても解決しない場合は、管理者に連絡してください。</p>
      <Button variant="primary" onClick={() => window.location.reload()}>再読み込み</Button>
      {traceId && <p className="tm-small tm-muted">問い合わせ番号: {traceId}</p>}
    </main>
  )
}
