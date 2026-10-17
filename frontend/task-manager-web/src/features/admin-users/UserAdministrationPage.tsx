import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button as AriaButton, Menu, MenuItem, MenuTrigger, Popover } from 'react-aria-components'
import { useSearchParams } from 'react-router'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { Icon } from '../../components/Icon'
import { Pagination } from '../../components/Pagination'
import { useToast } from '../../components/Toast'
import { SYSTEM_ROLE_LABELS } from '../../constants/labels'
import { LIMITS } from '../../constants/limits'
import { usePageTitle } from '../../hooks/usePageTitle'
import { formatDateTime } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import { errorMessage } from '../tasks/taskApi'

type User = Schemas['UserAdminSummaryResponse']
type Action = { kind: 'deactivate' | 'activate' | 'unlock' | 'reset'; user: User }

/** SCR-17 利用者管理（システム管理者のみ。REQ-USR-01〜06） */
export function UserAdministrationPage() {
  usePageTitle('利用者管理')
  const me = useCurrentUser()
  const toast = useToast()
  const queryClient = useQueryClient()
  const [params, setParams] = useSearchParams()
  const q = params.get('q') ?? ''
  const status = params.get('status') ?? ''
  const page = Number(params.get('page') ?? '1') || 1
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<User | null>(null)
  const [action, setAction] = useState<Action | null>(null)
  const [tempPassword, setTempPassword] = useState<{ user: string; password: string } | null>(null)
  const set = (key: string, value: string | null) => setParams((p) => {
    const n = new URLSearchParams(p)
    if (value) n.set(key, value); else n.delete(key)
    if (key !== 'page') n.delete('page')
    return n
  })
  const users = useQuery({
    queryKey: queryKeys.users({ q, status, page }),
    queryFn: () => call(() => apiClient.GET('/api/v1/users', {
      params: { query: { q: q || undefined, status: (status || undefined) as Schemas['UserStatusFilter'] | undefined, page, pageSize: 50 } },
    })),
    placeholderData: keepPreviousData,
  })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['users'] })
  const run = useMutation({
    mutationFn: async ({ kind, user }: Action) => {
      const path = { path: { userId: user.id } }
      if (kind === 'deactivate') await call(() => apiClient.POST('/api/v1/users/{userId}/deactivate', { params: path }))
      if (kind === 'activate') await call(() => apiClient.POST('/api/v1/users/{userId}/activate', { params: path }))
      if (kind === 'unlock') await call(() => apiClient.POST('/api/v1/users/{userId}/unlock', { params: path }))
      if (kind === 'reset') {
        const r = await call(() => apiClient.POST('/api/v1/users/{userId}/password-reset', { params: path }))
        setTempPassword({ user: user.displayName, password: r.temporaryPassword })
      }
    },
    onSuccess: (_, { kind, user }) => {
      const messages = { deactivate: `${user.displayName}さんを利用停止にしました`, activate: `${user.displayName}さんの利用停止を解除しました`, unlock: `${user.displayName}さんのロックを解除しました`, reset: `${user.displayName}さんの一時パスワードを再発行しました` }
      toast.success(messages[kind])
      setAction(null)
      refresh()
    },
    onError: (e) => { const m = errorMessage(e, '操作できませんでした。'); toast.error(m.title, m.detail) },
  })

  const statusBadge = (u: User) => {
    if (!u.isActive) return <span className="tm-chip" style={{ background: '#eef0f3' }}>停止中</span>
    if (u.isLocked) return <span className="tm-chip" style={{ background: '#fbe4e2', color: '#9b1c12' }}>ロック中</span>
    if (u.mustChangePassword) return <span className="tm-chip" style={{ background: '#fbf3d6', color: '#6b5000' }}>パスワード未変更</span>
    return <span className="tm-chip" style={{ background: '#e1f4e6', color: '#1a6233' }}>利用中</span>
  }

  return (
    <div className="tm-stack">
      <div className="tm-row">
        <h1 className="tm-page-title">利用者管理</h1>
        <span className="tm-spacer" />
        <Button variant="primary" onClick={() => setCreateOpen(true)}><Icon name="plus" size={16} />利用者を追加</Button>
      </div>
      <div className="tm-row">
        <label className="tm-visually-hidden" htmlFor="user-q">表示名・メールで絞り込む</label>
        <input id="user-q" className="tm-input tm-select--inline" style={{ width: 240 }} placeholder="表示名・メールで絞り込む" defaultValue={q}
          onKeyDown={(e) => { if (e.key === 'Enter') set('q', e.currentTarget.value.trim()) }} onBlur={(e) => set('q', e.target.value.trim())} />
        <select className="tm-select tm-select--inline" aria-label="状態で絞り込む" value={status} onChange={(e) => set('status', e.target.value)}>
          <option value="">すべての状態</option>
          <option value="active">利用中</option>
          <option value="inactive">停止中</option>
          <option value="locked">ロック中</option>
        </select>
      </div>
      {users.isPending && <Loading rows={5} />}
      {users.isError && <ErrorPanel error={users.error} onRetry={() => void users.refetch()} />}
      {users.isSuccess && users.data.items.length === 0 && <EmptyState title="条件に合う利用者はいません" />}
      {users.isSuccess && users.data.items.length > 0 && (
        <>
          <div className="tm-table-wrap">
            <table className="tm-table">
              <caption className="tm-visually-hidden">利用者の一覧</caption>
              <thead><tr><th scope="col">表示名</th><th scope="col">メール</th><th scope="col">ロール</th><th scope="col">状態</th><th scope="col">最終ログイン</th><th scope="col"><span className="tm-visually-hidden">操作</span></th></tr></thead>
              <tbody>
                {users.data.items.map((u) => (
                  <tr key={u.id}>
                    <td>{u.displayName}{u.id === me.id && <span className="tm-small tm-muted">（あなた）</span>}</td>
                    <td className="tm-small">{u.email}</td>
                    <td>{SYSTEM_ROLE_LABELS[u.systemRole]}</td>
                    <td>{statusBadge(u)}</td>
                    <td className="tm-small">{u.lastLoginAtUtc ? formatDateTime(u.lastLoginAtUtc, me.timeZoneId) : 'なし'}</td>
                    <td>
                      <MenuTrigger>
                        <AriaButton className="tm-btn tm-btn--small tm-btn--icon" aria-label={`${u.displayName}さんの操作`}><Icon name="more" /></AriaButton>
                        <Popover className="tm-popover" placement="bottom end">
                          <Menu className="tm-menu" onAction={(key) => {
                            if (key === 'edit') setEditing(u)
                            else setAction({ kind: key as Action['kind'], user: u })
                          }}>
                            <MenuItem id="edit" className="tm-menu-item">編集</MenuItem>
                            {u.isLocked && <MenuItem id="unlock" className="tm-menu-item">ロックを解除</MenuItem>}
                            <MenuItem id="reset" className="tm-menu-item">パスワードを再発行</MenuItem>
                            {u.isActive && u.id !== me.id && <MenuItem id="deactivate" className="tm-menu-item tm-menu-item--danger">利用停止</MenuItem>}
                            {!u.isActive && <MenuItem id="activate" className="tm-menu-item">利用停止を解除</MenuItem>}
                          </Menu>
                        </Popover>
                      </MenuTrigger>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={users.data.page} pageSize={users.data.pageSize} totalCount={users.data.totalCount} totalPages={users.data.totalPages}
            onChange={(p) => set('page', p === 1 ? null : String(p))} />
        </>
      )}
      {createOpen && <CreateUserDialog onClose={() => setCreateOpen(false)} onCreated={(name, password) => { refresh(); setTempPassword({ user: name, password }) }} />}
      {editing && <EditUserDialog user={editing} onClose={() => setEditing(null)} onSaved={refresh} />}
      <ConfirmDialog isOpen={action !== null} onOpenChange={(o) => !o && setAction(null)} busy={run.isPending} danger={action?.kind === 'deactivate' || action?.kind === 'reset'}
        title={action ? ({ deactivate: `${action.user.displayName}さんを利用停止にしますか？`, activate: `${action.user.displayName}さんの利用停止を解除しますか？`, unlock: `${action.user.displayName}さんのロックを解除しますか？`, reset: `${action.user.displayName}さんのパスワードを再発行しますか？` })[action.kind] : ''}
        confirmLabel={action ? ({ deactivate: '利用停止にする', activate: '解除する', unlock: '解除する', reset: '再発行する' })[action.kind] : ''}
        onConfirm={() => action && run.mutate(action)}>
        {action?.kind === 'deactivate' && <p>すぐにログインできなくなります。担当中のタスクや履歴はそのまま残ります。</p>}
        {action?.kind === 'reset' && <p>新しい一時パスワードを発行します。この人のログインはすべて無効になり、次回ログイン時にパスワードの変更が必要になります。</p>}
        {action?.kind === 'activate' && <p>再びログインできるようになります。</p>}
        {action?.kind === 'unlock' && <p>すぐにログインを試せるようになります。</p>}
      </ConfirmDialog>
      {tempPassword && <TemporaryPasswordDialog userName={tempPassword.user} password={tempPassword.password} onClose={() => setTempPassword(null)} />}
    </div>
  )
}

function CreateUserDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (name: string, password: string) => void }) {
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [systemRole, setSystemRole] = useState<Schemas['SystemRole']>('member')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const submit = async () => {
    const local: Record<string, string> = {}
    if (email.trim() === '') local.email = 'メールアドレスを入力してください。'
    if (displayName.trim() === '') local.displayName = '表示名を入力してください。'
    setErrors(local)
    if (Object.keys(local).length > 0) return
    setBusy(true)
    try {
      const created = await call(() => apiClient.POST('/api/v1/users', { body: { email: email.trim(), displayName: displayName.trim(), systemRole } }))
      onClose()
      onCreated(created.user.displayName, created.temporaryPassword)
    } catch (e) {
      if (e instanceof ApiError && e.code === 'user.email_taken') setErrors({ email: e.title })
      else if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0) setErrors(Object.fromEntries(Object.entries(e.fieldErrors).map(([k, v]) => [k, v[0] ?? ''])))
      else setErrors({ form: errorMessage(e, '利用者を追加できませんでした。').title })
    } finally {
      setBusy(false)
    }
  }
  return (
    <Dialog isOpen onOpenChange={(o) => !o && onClose()} title="利用者を追加">
      <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate>
        {errors.form && <div className="tm-alert tm-alert--error" role="alert">{errors.form}</div>}
        <div className="tm-field">
          <label className="tm-field__label" htmlFor="new-email">メールアドレス<span className="tm-field__required">必須</span></label>
          <input id="new-email" type="email" className="tm-input" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus aria-invalid={!!errors.email} aria-describedby={errors.email ? 'new-email-error' : undefined} />
          {errors.email && <span id="new-email-error" className="tm-field__error">⚠ {errors.email}</span>}
        </div>
        <div className="tm-field">
          <label className="tm-field__label" htmlFor="new-name">表示名<span className="tm-field__required">必須</span></label>
          <input id="new-name" className="tm-input" value={displayName} maxLength={LIMITS.displayName} onChange={(e) => setDisplayName(e.target.value)} aria-invalid={!!errors.displayName} aria-describedby={errors.displayName ? 'new-name-error' : undefined} />
          {errors.displayName && <span id="new-name-error" className="tm-field__error">⚠ {errors.displayName}</span>}
        </div>
        <div className="tm-field">
          <label className="tm-field__label" htmlFor="new-role">システムロール</label>
          <select id="new-role" className="tm-select" value={systemRole} onChange={(e) => setSystemRole(e.target.value as Schemas['SystemRole'])}>
            <option value="member">一般利用者</option>
            <option value="administrator">システム管理者</option>
          </select>
        </div>
        <p className="tm-small tm-muted">登録すると一時パスワードが表示されます。利用者に伝えてください。</p>
        <div className="tm-dialog__footer">
          <Button onClick={onClose}>キャンセル</Button>
          <Button type="submit" variant="primary" busy={busy} busyText="追加しています…">追加する</Button>
        </div>
      </form>
    </Dialog>
  )
}

function EditUserDialog({ user, onClose, onSaved }: { user: User; onClose: () => void; onSaved: () => void }) {
  const toast = useToast()
  const detail = useQuery({
    queryKey: ['user', user.id],
    queryFn: () => call(() => apiClient.GET('/api/v1/users/{userId}', { params: { path: { userId: user.id } } })),
  })
  const [displayName, setDisplayName] = useState(user.displayName)
  const [systemRole, setSystemRole] = useState(user.systemRole)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const save = async () => {
    if (!detail.data) return
    setBusy(true)
    setError(null)
    try {
      await call(() => apiClient.PATCH('/api/v1/users/{userId}', { params: { path: { userId: user.id } }, body: { displayName: displayName.trim(), systemRole, version: detail.data.version } }))
      toast.success('利用者の情報を保存しました')
      onSaved()
      onClose()
    } catch (e) {
      setError(errorMessage(e, '保存できませんでした。').title)
    } finally {
      setBusy(false)
    }
  }
  return (
    <Dialog isOpen onOpenChange={(o) => !o && onClose()} title={`${user.displayName}さんの情報を編集`}>
      {detail.isPending && <Loading />}
      {detail.isError && <ErrorPanel error={detail.error} />}
      {detail.isSuccess && (
        <form onSubmit={(e) => { e.preventDefault(); void save() }} noValidate>
          {error && <div className="tm-alert tm-alert--error" role="alert">{error}</div>}
          <p className="tm-small tm-muted">メール: {detail.data.email}・参加プロジェクト {detail.data.projectCount} 件</p>
          <div className="tm-field">
            <label className="tm-field__label" htmlFor="edit-name">表示名</label>
            <input id="edit-name" className="tm-input" value={displayName} maxLength={LIMITS.displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div className="tm-field">
            <label className="tm-field__label" htmlFor="edit-role">システムロール</label>
            <select id="edit-role" className="tm-select" value={systemRole} onChange={(e) => setSystemRole(e.target.value as Schemas['SystemRole'])}>
              <option value="member">一般利用者</option>
              <option value="administrator">システム管理者</option>
            </select>
          </div>
          <div className="tm-dialog__footer">
            <Button onClick={onClose}>キャンセル</Button>
            <Button type="submit" variant="primary" busy={busy} busyText="保存しています…">保存</Button>
          </div>
        </form>
      )}
    </Dialog>
  )
}

/** 一時パスワードの表示（この画面を閉じると再表示できない） */
function TemporaryPasswordDialog({ userName, password, onClose }: { userName: string; password: string; onClose: () => void }) {
  const toast = useToast()
  const [confirmed, setConfirmed] = useState(false)
  return (
    <Dialog isOpen onOpenChange={(o) => !o && confirmed && onClose()} title={`${userName}さんの一時パスワード`} role="alertdialog">
      <p className="tm-alert tm-alert--warning">この画面を閉じると、一時パスワードは再表示できません。</p>
      <div className="tm-row" style={{ margin: 'var(--space-4) 0' }}>
        <code style={{ fontSize: 20, padding: 'var(--space-2) var(--space-3)', background: '#f3f5f8', borderRadius: 6, userSelect: 'all' }}>{password}</code>
        <Button size="small" onClick={() => void navigator.clipboard.writeText(password).then(() => toast.success('コピーしました'), () => toast.error('コピーできませんでした。'))}>コピー</Button>
      </div>
      <p className="tm-small">利用者は、このパスワードでログインした後、自分で新しいパスワードを設定します。</p>
      <label className="tm-checkbox"><input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />利用者に伝えました</label>
      <div className="tm-dialog__footer"><Button variant="primary" disabled={!confirmed} onClick={onClose}>閉じる</Button></div>
    </Dialog>
  )
}
