import { useQuery } from '@tanstack/react-query'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { StatusPieChart } from '../../components/StatusPieChart'

type Item = Schemas['StatusCountResponse']

/** 円グラフの凡例から、そのステータスで絞り込んだ表へ移動する（OD-41）。完了・中止の区分は「完了・中止も表示」を付ける */
export function statusListLink(projectKey: string, item: Item, assignedToMe = false): string | null {
  if (item.statusId === null || item.statusId === undefined) return null
  const params = new URLSearchParams({ statusId: item.statusId })
  if (assignedToMe) params.set('assignee', 'me')
  if (item.category === 'done' || item.category === 'canceled') params.set('includeClosed', 'true')
  return `/projects/${projectKey}/tasks?${params.toString()}`
}

/** SCR-21 プロジェクトのステータス別円グラフ（REQ-CHT-01） */
export function ProjectStatusChartView({ projectKey, canEdit, onAdd }: { projectKey: string; canEdit: boolean; onAdd: () => void }) {
  const summary = useQuery({
    queryKey: queryKeys.taskStatusSummary(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/status-summary', { params: { path: { projectKey } } })),
  })
  if (summary.isPending) return <Loading rows={4} />
  if (summary.isError) return <ErrorPanel error={summary.error} onRetry={() => void summary.refetch()} />
  return (
    <StatusPieChart
      title="ステータス別の割合"
      summary={summary.data}
      itemLink={(item) => statusListLink(projectKey, item)}
      note="ステータス名を選ぶと、そのステータスのタスクを表で表示します。"
      empty={(
        <EmptyState title="まだタスクがありません" text="最初のタスクを追加してみましょう。"
          action={canEdit ? <Button variant="primary" onClick={onAdd}>タスクを追加</Button> : undefined} />
      )}
    />
  )
}
