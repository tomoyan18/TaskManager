import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { Pagination } from '../../components/Pagination'
import { formatDate, formatDateTime, formatFullDateTime } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import styles from './Tasks.module.css'

type Entry = Schemas['TaskHistoryEntryResponse']

const FIELD_LABELS: Partial<Record<Entry['kind'], string>> = {
  titleChanged: 'タイトル',
  statusChanged: 'ステータス',
  priorityChanged: '優先度',
  assigneeChanged: '担当者',
  startDateChanged: '開始日',
  dueDateChanged: '期限',
  milestoneChanged: 'マイルストーン',
  parentChanged: '親タスク',
  tagsChanged: 'タグ',
}

const show = (kind: Entry['kind'], value: string | null | undefined): string | null => {
  if (value === null || value === undefined) return null
  return (kind === 'dueDateChanged' || kind === 'startDateChanged') && /^\d{4}-\d{2}-\d{2}$/.test(value) ? formatDate(value) : value
}

/** 変更履歴を自然な日本語の文にする（ux-design.md §5 主要な文言） */
export function describeHistory(entry: Entry): string {
  const actor = entry.actor ? `${entry.actor.displayName}さんが` : ''
  const oldValue = show(entry.kind, entry.oldValue)
  const newValue = show(entry.kind, entry.newValue)
  switch (entry.kind) {
    case 'created': return `${actor}タスクを作成しました`
    case 'descriptionChanged': return `${actor}説明を変更しました`
    case 'attachmentAdded': return `${actor}ファイル「${newValue ?? ''}」を添付しました`
    case 'attachmentRemoved': return `${actor}添付ファイル「${oldValue ?? ''}」を削除しました`
    case 'assigneeChanged':
      if (entry.actor === null && newValue === null) return `担当者が外れました（${oldValue ?? ''}さんがメンバーから外れたため）`
      break
    default:
      break
  }
  const label = FIELD_LABELS[entry.kind] ?? '項目'
  if (oldValue === null && newValue !== null) return `${actor}${label}を「${newValue}」に設定しました`
  if (oldValue !== null && newValue === null) return `${actor}${label}「${oldValue}」を外しました`
  return `${actor}${label}を「${oldValue ?? ''}」から「${newValue ?? ''}」に変更しました`
}

export function TaskHistory({ projectKey, taskNumber }: { projectKey: string; taskNumber: number }) {
  const user = useCurrentUser()
  const [page, setPage] = useState(1)
  const history = useQuery({
    queryKey: [...queryKeys.history(projectKey, taskNumber), page],
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/{taskNumber}/history', {
      params: { path: { projectKey, taskNumber }, query: { page, pageSize: 50 } },
    })),
  })
  if (history.isPending) return <Loading />
  if (history.isError) return <ErrorPanel error={history.error} onRetry={() => void history.refetch()} />
  if (history.data.items.length === 0) return <EmptyState title="変更履歴はありません" />
  return (
    <>
      <ol className={styles.timeline}>
        {history.data.items.map((h) => (
          <li key={h.id}>
            <div>{describeHistory(h)}</div>
            <time className="tm-small tm-muted" dateTime={h.occurredAtUtc} title={formatFullDateTime(h.occurredAtUtc, user.timeZoneId)}>
              {formatDateTime(h.occurredAtUtc, user.timeZoneId)}
            </time>
          </li>
        ))}
      </ol>
      <Pagination page={history.data.page} pageSize={history.data.pageSize} totalCount={history.data.totalCount} totalPages={history.data.totalPages} onChange={setPage} />
    </>
  )
}
