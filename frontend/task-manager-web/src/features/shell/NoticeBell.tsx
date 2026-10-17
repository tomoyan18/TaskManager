import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button as AriaButton, Dialog, DialogTrigger, Popover } from 'react-aria-components'
import { Link, useNavigate } from 'react-router'
import { apiClient, call } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Icon } from '../../components/Icon'
import { formatDateTime } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'

/** ヘッダーのお知らせ（最新 10 件のパネル。ui-design.md SCR-15） */
export function NoticeBell({ count }: { count: number }) {
  const user = useCurrentUser()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const label = count > 0 ? `お知らせ（未読 ${count} 件）` : 'お知らせ'
  return (
    <DialogTrigger>
      <AriaButton className="tm-btn tm-btn--ghost tm-btn--icon" aria-label={label} style={{ position: 'relative' }}>
        <Icon name="bell" />
        {count > 0 && (
          <span aria-hidden="true" style={{ position: 'absolute', top: 2, right: 2, background: 'var(--color-danger)', color: '#fff', borderRadius: 999, fontSize: 11, padding: '0 5px', fontWeight: 700 }}>
            {count > 99 ? '99+' : count}
          </span>
        )}
      </AriaButton>
      <Popover className="tm-popover" placement="bottom end" style={{ width: 360 }}>
        <Dialog aria-label="お知らせ" style={{ outline: 'none', padding: 'var(--space-3)' }}>
          {({ close }) => (
            <NoticePanel
              timeZone={user.timeZoneId}
              onOpen={async (id, link) => {
                close()
                await call(() => apiClient.POST('/api/v1/me/notices/{noticeId}/read', { params: { path: { noticeId: id } } })).catch(() => undefined)
                void queryClient.invalidateQueries({ queryKey: ['notices'] })
                if (link) void navigate(link)
              }}
              onShowAll={close}
            />
          )}
        </Dialog>
      </Popover>
    </DialogTrigger>
  )
}

function NoticePanel({ timeZone, onOpen, onShowAll }: { timeZone: string; onOpen: (id: string, link: string | null | undefined) => Promise<void>; onShowAll: () => void }) {
  const notices = useQuery({
    queryKey: queryKeys.notices(false, 1),
    queryFn: () => call(() => apiClient.GET('/api/v1/me/notices', { params: { query: { pageSize: 10 } } })),
  })
  const items = notices.data?.items ?? []
  return (
    <div>
      <div className="tm-row" style={{ marginBottom: 'var(--space-2)' }}>
        <strong>お知らせ</strong>
        <span className="tm-spacer" />
        <Link to="/notices" className="tm-small" onClick={onShowAll}>すべて見る</Link>
      </div>
      {notices.isPending && <p className="tm-small tm-muted">読み込んでいます…</p>}
      {notices.isError && <p className="tm-small" role="alert">読み込めませんでした。</p>}
      {notices.isSuccess && items.length === 0 && <p className="tm-small tm-muted">お知らせはありません</p>}
      <ul style={{ listStyle: 'none', margin: 0, padding: 0, maxHeight: 360, overflowY: 'auto' }}>
        {items.map((n) => (
          <li key={n.id}>
            <button type="button" onClick={() => void onOpen(n.id, n.linkPath)}
              style={{ display: 'block', width: '100%', textAlign: 'left', border: 'none', background: n.isRead ? 'transparent' : '#f1f6fe', padding: 'var(--space-2)', borderRadius: 6, cursor: 'pointer', font: 'inherit' }}>
              {!n.isRead && <span style={{ color: 'var(--color-primary)' }} aria-hidden="true">● </span>}
              {!n.isRead && <span className="tm-visually-hidden">未読: </span>}
              <span className="tm-small">{n.message}</span>
              <div className="tm-small tm-muted">{formatDateTime(n.createdAtUtc, timeZone)}</div>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
