import { NavLink, Outlet, useOutletContext } from 'react-router'
import type { Schemas } from '../../api/client'
import styles from './Projects.module.css'

/** SCR-12〜14 プロジェクト設定（左に設定メニュー、右に内容） */
export function ProjectSettingsLayout() {
  const project = useOutletContext<Schemas['ProjectDetailResponse']>()
  const isOwner = project.myRole === 'owner'
  return (
    <div className={styles.settings}>
      <nav aria-label="設定メニュー" className={styles.settingsNav}>
        <NavLink to="." end className={styles.settingsLink}>基本情報</NavLink>
        <NavLink to="members" className={styles.settingsLink}>メンバー</NavLink>
        <NavLink to="workflow" className={styles.settingsLink}>ステータスとタグ</NavLink>
      </nav>
      <div>
        {!isOwner && project.myRole !== null && (
          <p className="tm-alert tm-alert--info tm-small" role="note">設定の変更はプロジェクトの管理者のみ行えます。{project.myRole === 'editor' && 'タグは編集者も追加・変更できます。'}</p>
        )}
        <Outlet context={project} />
      </div>
    </div>
  )
}
