import { useQuery } from '@tanstack/react-query'
import { apiClient, call } from '../../api/client'
import { queryKeys, useSidebarProjects } from '../../api/queries'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { StatusPieChart } from '../../components/StatusPieChart'
import { statusListLink } from '../tasks/ProjectStatusChartView'

/**
 * SCR-04「ステータス別の割合」（REQ-CHT-02）。
 * すべてのプロジェクトは区分別、プロジェクトを選ぶとそのプロジェクトのステータス別に表示する（OD-34）。
 */
export function MyStatusChartView({ projectKey, onProjectChange }: { projectKey: string | null; onProjectChange: (key: string | null) => void }) {
  const projects = useSidebarProjects()
  const summary = useQuery({
    queryKey: queryKeys.myTaskStatusSummary(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/me/tasks/status-summary', { params: { query: { projectKey: projectKey ?? undefined } } })),
  })
  const active = (projects.data?.items ?? []).filter((p) => !p.isArchived)
  return (
    <div className="tm-stack" style={{ gap: 'var(--space-3)' }}>
      <label className="tm-row tm-small">
        対象
        <select className="tm-select tm-select--inline" value={projectKey ?? ''} onChange={(e) => onProjectChange(e.target.value || null)}>
          <option value="">すべてのプロジェクト（区分別）</option>
          {projectKey !== null && !active.some((p) => p.key === projectKey) && <option value={projectKey}>{projectKey}</option>}
          {active.map((p) => <option key={p.key} value={p.key}>{p.name}（{p.key}）</option>)}
        </select>
      </label>
      {summary.isPending && <Loading rows={4} />}
      {summary.isError && <ErrorPanel error={summary.error} onRetry={() => void summary.refetch()} />}
      {summary.isSuccess && (
        <StatusPieChart
          title={projectKey === null ? '自分のタスクの割合（区分別）' : `自分のタスクの割合（${active.find((p) => p.key === projectKey)?.name ?? projectKey}）`}
          summary={summary.data}
          itemLink={projectKey === null ? undefined : (item) => statusListLink(projectKey, item, true)}
          note={projectKey === null
            ? '参加中（アーカイブ以外）のすべてのプロジェクトで、自分が担当のタスクを集計しています。プロジェクトごとのステータスで見るには、対象を選んでください。'
            : 'ステータス名を選ぶと、自分が担当のそのステータスのタスクを表で表示します。'}
          empty={<EmptyState title="担当しているタスクはありません" text="新しく割り当てられると、ここに表示されます。" />}
        />
      )}
    </div>
  )
}
