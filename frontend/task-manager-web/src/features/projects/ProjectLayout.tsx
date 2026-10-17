import { NavLink, Outlet, useParams } from 'react-router'
import { ApiError } from '../../api/client'
import { useProject } from '../../api/queries'
import { ErrorPanel, Loading } from '../../components/Feedback'
import { PROJECT_ROLE_LABELS } from '../../constants/labels'
import { NotFoundPage } from '../shell/NotFoundPage'

/** プロジェクト内の共通部分（見出し + タブ: タスク / マイルストーン / 設定。ui-design.md §4.1） */
export function ProjectLayout() {
  const projectKey = (useParams().projectKey ?? '').toUpperCase()
  const project = useProject(projectKey)
  if (project.isPending) return <Loading />
  if (project.isError) {
    if (project.error instanceof ApiError && project.error.status === 404) return <NotFoundPage />
    return <ErrorPanel error={project.error} onRetry={() => void project.refetch()} />
  }
  const p = project.data
  return (
    <div>
      <nav aria-label="パンくずリスト" className="tm-small tm-muted" style={{ marginBottom: 'var(--space-2)' }}>
        <NavLink to="/projects" className="tm-link--plain">プロジェクト</NavLink> / {p.name}（{p.key}）
      </nav>
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <h1 className="tm-page-title">{p.name}</h1>
        <span className="tm-key">{p.key}</span>
        {p.myRole && <span className="tm-small tm-muted">あなたのロール: {PROJECT_ROLE_LABELS[p.myRole]}</span>}
      </div>
      {p.isArchived && (
        <div className="tm-alert tm-alert--warning" role="status" style={{ marginBottom: 'var(--space-3)' }}>
          このプロジェクトはアーカイブされています。閲覧のみできます。
        </div>
      )}
      <nav className="tm-tabs" aria-label="プロジェクトのメニュー">
        <NavLink to={`/projects/${p.key}/tasks`} className="tm-tab">タスク</NavLink>
        <NavLink to={`/projects/${p.key}/milestones`} className="tm-tab">マイルストーン</NavLink>
        <NavLink to={`/projects/${p.key}/settings`} className="tm-tab">設定</NavLink>
      </nav>
      <Outlet context={p} />
    </div>
  )
}
