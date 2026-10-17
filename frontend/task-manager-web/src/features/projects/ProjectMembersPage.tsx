import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { ComboBox, Input, Label, ListBox, ListBoxItem, Popover } from 'react-aria-components'
import { useNavigate, useOutletContext } from 'react-router'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys, useMembers } from '../../api/queries'
import { Button } from '../../components/Button'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Dialog } from '../../components/Dialog'
import { UserLabel } from '../../components/Display'
import { ErrorPanel, Loading } from '../../components/Feedback'
import { useToast } from '../../components/Toast'
import { PROJECT_ROLE_LABELS } from '../../constants/labels'
import { usePageTitle } from '../../hooks/usePageTitle'
import { formatDate } from '../../utils/dates'
import { useCurrentUser } from '../auth/AuthProvider'
import { errorMessage } from '../tasks/taskApi'

type Role = Schemas['ProjectRole']
const ROLES: Role[] = ['owner', 'editor', 'viewer']
const ROLE_HELP: Record<Role, string> = {
  owner: '設定・メンバー管理を含むすべての操作',
  editor: 'タスクの追加・編集・コメント・添付',
  viewer: '閲覧のみ',
}

/** SCR-13 プロジェクト設定: メンバー */
export function ProjectMembersPage() {
  const project = useOutletContext<Schemas['ProjectDetailResponse']>()
  const user = useCurrentUser()
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  usePageTitle('メンバー', project.name)
  const members = useMembers(project.key)
  const canManage = (project.myRole === 'owner' && !project.isArchived) || user.systemRole === 'administrator'
  const [addOpen, setAddOpen] = useState(false)
  const [removing, setRemoving] = useState<Schemas['ProjectMemberResponse'] | null>(null)
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.members(project.key) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.project(project.key) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.tasks(project.key) })
  }
  const changeRole = useMutation({
    mutationFn: ({ userId, role }: { userId: string; role: Role }) => call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/members/{userId}', {
      params: { path: { projectKey: project.key, userId } }, body: { role },
    })),
    onSuccess: (m) => { toast.success(`${m.user.displayName}さんのロールを「${PROJECT_ROLE_LABELS[m.role]}」にしました`); refresh() },
    onError: (e) => { const m = errorMessage(e, 'ロールを変更できませんでした。'); toast.error(m.title, m.detail); refresh() },
  })
  const remove = useMutation({
    mutationFn: (userId: string) => call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/members/{userId}', { params: { path: { projectKey: project.key, userId } } })),
    onSuccess: (_, userId) => {
      setRemoving(null)
      if (userId === user.id) {
        toast.success(`プロジェクト「${project.name}」から脱退しました`)
        void queryClient.invalidateQueries({ queryKey: ['projects'] })
        void navigate('/projects')
      } else {
        toast.success('メンバーから外しました')
        refresh()
      }
    },
    onError: (e) => { const m = errorMessage(e, 'メンバーから外せませんでした。'); toast.error(m.title, m.detail) },
  })

  if (members.isPending) return <Loading />
  if (members.isError) return <ErrorPanel error={members.error} onRetry={() => void members.refetch()} />
  const isMember = members.data.some((m) => m.user.id === user.id)
  return (
    <section className="tm-card">
      <div className="tm-row" style={{ marginBottom: 'var(--space-3)' }}>
        <h2 className="tm-section-title" style={{ margin: 0 }}>メンバー（{members.data.length}）</h2>
        <span className="tm-spacer" />
        {canManage && <Button variant="primary" onClick={() => setAddOpen(true)}>メンバーを追加</Button>}
      </div>
      <div className="tm-table-wrap">
        <table className="tm-table">
          <caption className="tm-visually-hidden">メンバーの一覧</caption>
          <thead><tr><th scope="col">名前</th><th scope="col">メール</th><th scope="col">ロール</th><th scope="col">参加日</th><th scope="col"><span className="tm-visually-hidden">操作</span></th></tr></thead>
          <tbody>
            {members.data.map((m) => (
              <tr key={m.user.id}>
                <td><UserLabel user={m.user} />{m.user.id === user.id && <span className="tm-small tm-muted">（あなた）</span>}</td>
                <td className="tm-small">{m.email}</td>
                <td>
                  {canManage
                    ? <select className="tm-select tm-select--inline" value={m.role} aria-label={`${m.user.displayName}さんのロール`}
                        onChange={(e) => changeRole.mutate({ userId: m.user.id, role: e.target.value as Role })}>
                        {ROLES.map((r) => <option key={r} value={r}>{PROJECT_ROLE_LABELS[r]}</option>)}
                      </select>
                    : PROJECT_ROLE_LABELS[m.role]}
                </td>
                <td className="tm-small">{formatDate(m.joinedAtUtc.slice(0, 10))}</td>
                <td>
                  {(canManage || m.user.id === user.id) && !project.isArchived && (
                    <Button size="small" variant="ghost" onClick={() => setRemoving(m)}>{m.user.id === user.id ? '脱退する' : '外す'}</Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <dl className="tm-small tm-muted" style={{ marginTop: 'var(--space-3)' }}>
        {ROLES.map((r) => <div key={r}><dt style={{ display: 'inline', fontWeight: 700 }}>{PROJECT_ROLE_LABELS[r]}:</dt> <dd style={{ display: 'inline', margin: 0 }}>{ROLE_HELP[r]}</dd></div>)}
      </dl>
      {!isMember && user.systemRole === 'administrator' && <p className="tm-small tm-muted">システム管理者として、このプロジェクトのメンバーを管理しています。</p>}
      {addOpen && <AddMemberDialog projectKey={project.key} onClose={() => setAddOpen(false)} onAdded={refresh} />}
      <ConfirmDialog isOpen={removing !== null} onOpenChange={(o) => !o && setRemoving(null)}
        title={removing?.user.id === user.id ? `プロジェクト「${project.name}」から脱退しますか？` : `${removing?.user.displayName ?? ''}さんをこのプロジェクトから外しますか？`}
        confirmLabel={removing?.user.id === user.id ? '脱退する' : '外す'} busy={remove.isPending} onConfirm={() => removing && remove.mutate(removing.user.id)}>
        <p>{removing?.user.id === user.id ? 'あなたが担当しているタスク' : 'この人が担当しているタスク'}は「担当者なし」になります。</p>
      </ConfirmDialog>
    </section>
  )
}

function AddMemberDialog({ projectKey, onClose, onAdded }: { projectKey: string; onClose: () => void; onAdded: () => void }) {
  const toast = useToast()
  const [input, setInput] = useState('')
  const [term, setTerm] = useState('')
  const [selected, setSelected] = useState<Schemas['UserDirectoryEntry'] | null>(null)
  const [role, setRole] = useState<Role>('editor')
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    const t = setTimeout(() => setTerm(input.trim()), 250)
    return () => clearTimeout(t)
  }, [input])
  const directory = useQuery({
    queryKey: ['userDirectory', projectKey, term],
    queryFn: () => call(() => apiClient.GET('/api/v1/user-directory', { params: { query: { q: term, excludeProjectKey: projectKey } } })),
    enabled: term.length > 0,
  })
  const add = useMutation({
    mutationFn: () => call(() => apiClient.POST('/api/v1/projects/{projectKey}/members', {
      params: { path: { projectKey } }, body: { userId: selected?.id ?? null, role },
    })),
    onSuccess: (m) => { toast.success(`${m.user.displayName}さんを追加しました`); onAdded(); onClose() },
    onError: (e) => setError(errorMessage(e, 'メンバーを追加できませんでした。').title),
  })
  const results = term.length > 0 ? (directory.data ?? []) : []
  // 選択した人は、検索語が変わって一覧から外れても選択肢に残す（選択が外れて追加できなくなるのを防ぐ。Phase 6 で修正）
  const items = selected !== null && !results.some((u) => u.id === selected.id) ? [selected, ...results] : results
  return (
    <Dialog isOpen onOpenChange={(o) => !o && onClose()} title="メンバーを追加">
      {error && <div className="tm-alert tm-alert--error" role="alert" style={{ marginBottom: 'var(--space-3)' }}>{error}</div>}
      <ComboBox inputValue={input} items={items} selectedKey={selected?.id ?? null}
        onInputChange={(v) => { setInput(v); setSelected((s) => (s !== null && s.displayName === v ? s : null)) }}
        menuTrigger="input" allowsEmptyCollection
        onSelectionChange={(key) => { const u = items.find((i) => i.id === key); if (u) { setSelected(u); setInput(u.displayName) } }}>
        {/* ComboBox が入力欄の id を管理するため、react-aria の Label で関連付ける（Phase 6 で修正） */}
        <Label className="tm-field__label">利用者（名前・メールで検索）</Label>
        <Input className="tm-input" autoFocus />
        <Popover className="tm-popover" placement="bottom start" style={{ width: 'var(--trigger-width)' }}>
          <ListBox className="tm-menu" renderEmptyState={() => <div className="tm-small tm-muted" style={{ padding: 'var(--space-2)' }}>{directory.isFetching ? '検索しています…' : '見つかりませんでした'}</div>}>
            {(u: (typeof items)[number]) => (
              <ListBoxItem id={u.id} textValue={u.displayName} className="tm-menu-item">{u.displayName}<div className="tm-small tm-muted">{u.email}</div></ListBoxItem>
            )}
          </ListBox>
        </Popover>
      </ComboBox>
      <div className="tm-field" style={{ marginTop: 'var(--space-4)' }}>
        <label className="tm-field__label" htmlFor="member-role">ロール</label>
        <select id="member-role" className="tm-select" value={role} onChange={(e) => setRole(e.target.value as Role)}>
          {ROLES.map((r) => <option key={r} value={r}>{PROJECT_ROLE_LABELS[r]}（{ROLE_HELP[r]}）</option>)}
        </select>
      </div>
      <div className="tm-dialog__footer">
        <Button onClick={onClose}>キャンセル</Button>
        <Button variant="primary" disabled={selected === null} busy={add.isPending} busyText="追加しています…" onClick={() => add.mutate()}>追加する</Button>
      </div>
    </Dialog>
  )
}
