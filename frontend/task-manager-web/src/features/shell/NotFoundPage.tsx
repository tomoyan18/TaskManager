import { useEffect } from 'react'
import { Link } from 'react-router'

/** SCR-18 ページが見つかりません（存在しない・権限がないを区別しない） */
export function NotFoundPage() {
  useEffect(() => {
    document.title = 'ページが見つかりません｜TaskManager'
  }, [])
  return (
    <div className="tm-empty">
      <h1 className="tm-page-title">ページが見つかりません</h1>
      <p className="tm-empty__text">URL が間違っているか、閲覧する権限がない可能性があります。</p>
      <Link to="/" className="tm-btn">ホームへ戻る</Link>
    </div>
  )
}
