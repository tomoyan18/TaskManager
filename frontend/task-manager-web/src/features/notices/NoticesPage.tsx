import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { apiClient, call } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { Pagination } from '../../components/Pagination'
import { useToast } from '../../components/Toast'
import { usePageTitle } from '../../hooks/usePageTitle'
import { formatDateTime, formatFullDateTime } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'

/** SCR-15 お知らせ */
export function NoticesPage() {
  usePageTitle('お知らせ')
  const user = useCurrentUser()
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [unreadOnly, setUnreadOnly] = useState(false)
  const [page, setPage] = useState(1)
  const notices = useQuery({
    queryKey: queryKeys.notices(unreadOnly, page),
    queryFn: () => call(() => apiClient.GET('/api/v1/me/notices', { params: { query: { unreadOnly, page, pageSize: 30 } } })),
    placeholderData: keepPreviousData,
  })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['notices'] })
  const readAll = useMutation({
    mutationFn: () => call(() => apiClient.POST('/api/v1/me/notices/read-all')),
    onSuccess: () => { toast.success('すべて既読にしました'); refresh() },
  })
  const open = async (id: string, isRead: boolean, link: string | null | undefined) => {
    if (!isRead) {
      await call(() => apiClient.POST('/api/v1/me/notices/{noticeId}/read', { params: { path: { noticeId: id } } })).catch(() => undefined)
      refresh()
    }
    if (link) void navigate(link)
  }
  return (
    <div className="tm-stack">
      <div className="tm-row">
        <h1 className="tm-page-title">お知らせ</h1>
        <span className="tm-spacer" />
        <label className="tm-checkbox tm-small"><input type="checkbox" checked={unreadOnly} onChange={(e) => { setUnreadOnly(e.target.checked); setPage(1) }} />未読のみ</label>
        <Button size="small" onClick={() => readAll.mutate()} busy={readAll.isPending} busyText="処理しています…">すべて既読にする</Button>
      </div>
      {notices.isPending && <Loading rows={5} />}
      {notices.isError && <ErrorPanel error={notices.error} onRetry={() => void notices.refetch()} />}
      {notices.isSuccess && notices.data.items.length === 0 && <EmptyState title={unreadOnly ? '未読のお知らせはありません' : 'お知らせはありません'} />}
      {notices.isSuccess && notices.data.items.length > 0 && (
        <>
          <ul className="tm-card" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {notices.data.items.map((n) => (
              <li key={n.id} style={{ borderBottom: '1px solid var(--color-border)' }}>
                <button type="button" onClick={() => void open(n.id, n.isRead, n.linkPath)}
                  style={{ display: 'flex', gap: 'var(--space-3)', width: '100%', textAlign: 'left', padding: 'var(--space-3) var(--space-4)', border: 'none', background: n.isRead ? 'transparent' : '#f1f6fe', font: 'inherit', cursor: 'pointer', color: 'inherit' }}>
                  <span aria-hidden="true" style={{ color: 'var(--color-primary)', width: '1em' }}>{n.isRead ? '' : '●'}</span>
                  <span style={{ flex: 1 }}>
                    {!n.isRead && <span className="tm-visually-hidden">未読: </span>}
                    {n.message}
                    <time className="tm-small tm-muted" dateTime={n.createdAtUtc} title={formatFullDateTime(n.createdAtUtc, user.timeZoneId)} style={{ display: 'block' }}>
                      {formatDateTime(n.createdAtUtc, user.timeZoneId)}
                    </time>
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <Pagination page={notices.data.page} pageSize={notices.data.pageSize} totalCount={notices.data.totalCount} totalPages={notices.data.totalPages} onChange={setPage} />
        </>
      )}
    </div>
  )
}
