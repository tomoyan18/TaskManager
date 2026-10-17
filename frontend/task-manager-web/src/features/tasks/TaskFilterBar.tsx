import { useEffect, useState } from 'react'
import type { Schemas } from '../../api/client'
import { useMembers, useMilestones, useStatuses, useTags } from '../../api/queries'
import { Button } from '../../components/Button'
import { PRIORITY_LABELS, PRIORITY_ORDER } from '../../constants/labels'
import type { useTaskListParams } from '../../hooks/useTaskListParams'

type Params = ReturnType<typeof useTaskListParams>

const DUE_LABELS: Record<string, string> = { overdue: '期限切れ', today: '今日まで', week: '1 週間以内' }

/** 絞り込み（ui-design.md SCR-06）。適用中の条件をチップで表示し、✕ で個別に外せる */
export function TaskFilterBar({ project, params }: { project: Schemas['ProjectDetailResponse']; params: Params }) {
  const statuses = useStatuses(project.key)
  const members = useMembers(project.key)
  const tags = useTags(project.key)
  const milestones = useMilestones(project.key)
  const [q, setQ] = useState(params.q)
  // URL 側で条件が変わった（チップの ✕ など）場合は入力欄も合わせる
  const [syncedQ, setSyncedQ] = useState(params.q)
  if (params.q !== syncedQ) {
    setSyncedQ(params.q)
    setQ(params.q)
  }
  useEffect(() => {
    const timer = setTimeout(() => {
      if (q !== params.q) params.update({ q: q.trim() || null })
    }, 400)
    return () => clearTimeout(timer)
  }, [q, params])

  const statusName = (id: string) => statuses.data?.find((s) => s.id === id)?.name ?? '不明なステータス'
  const memberName = (id: string) => (id === 'me' ? '自分' : id === 'none' ? '担当者なし' : members.data?.find((m) => m.user.id === id)?.user.displayName ?? '不明な利用者')
  const chips: { label: string; remove: () => void }[] = []
  params.statusId.forEach((id) => chips.push({ label: `ステータス: ${statusName(id)}`, remove: () => params.update({ statusId: params.statusId.filter((x) => x !== id) }) }))
  if (params.assignee) chips.push({ label: `担当者: ${memberName(params.assignee)}`, remove: () => params.update({ assignee: null }) })
  params.priority.forEach((p) => chips.push({ label: `優先度: ${PRIORITY_LABELS[p]}`, remove: () => params.update({ priority: params.priority.filter((x) => x !== p) }) }))
  if (params.tagId) chips.push({ label: `タグ: ${tags.data?.find((t) => t.id === params.tagId)?.name ?? ''}`, remove: () => params.update({ tagId: null }) })
  if (params.milestoneId) chips.push({ label: `マイルストーン: ${params.milestoneId === 'none' ? 'なし' : milestones.data?.find((m) => m.id === params.milestoneId)?.name ?? ''}`, remove: () => params.update({ milestoneId: null }) })
  if (params.due) chips.push({ label: `期限: ${DUE_LABELS[params.due] ?? ''}`, remove: () => params.update({ due: null }) })
  if (params.includeClosed) chips.push({ label: '完了・中止も表示', remove: () => params.update({ includeClosed: null }) })
  if (params.q) chips.push({ label: `タイトル: ${params.q}`, remove: () => params.update({ q: null }) })

  return (
    <section aria-label="絞り込み" className="tm-card" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
      <div className="tm-row">
        <label className="tm-visually-hidden" htmlFor="filter-q">タイトルで絞り込む</label>
        <input id="filter-q" className="tm-input tm-select--inline" style={{ width: 200 }} placeholder="タイトルで絞り込む" value={q} onChange={(e) => setQ(e.target.value)} />
        {params.view === 'table' && (
          <select className="tm-select tm-select--inline" aria-label="ステータスで絞り込む" value="" onChange={(e) => e.target.value && params.update({ statusId: [...params.statusId, e.target.value] })}>
            <option value="">ステータス</option>
            {statuses.data?.filter((s) => !params.statusId.includes(s.id)).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        )}
        <select className="tm-select tm-select--inline" aria-label="担当者で絞り込む" value={params.assignee} onChange={(e) => params.update({ assignee: e.target.value || null })}>
          <option value="">担当者</option>
          <option value="me">自分</option>
          <option value="none">担当者なし</option>
          {members.data?.filter((m) => m.role !== 'viewer').map((m) => <option key={m.user.id} value={m.user.id}>{m.user.displayName}</option>)}
        </select>
        <select className="tm-select tm-select--inline" aria-label="優先度で絞り込む" value="" onChange={(e) => e.target.value && params.update({ priority: [...params.priority, e.target.value] })}>
          <option value="">優先度</option>
          {PRIORITY_ORDER.filter((p) => !params.priority.includes(p)).map((p) => <option key={p} value={p}>{PRIORITY_LABELS[p]}</option>)}
        </select>
        <select className="tm-select tm-select--inline" aria-label="期限で絞り込む" value={params.due} onChange={(e) => params.update({ due: e.target.value || null })}>
          <option value="">期限</option>
          <option value="overdue">期限切れ</option>
          <option value="today">今日まで</option>
          <option value="week">1 週間以内</option>
        </select>
        {(tags.data?.length ?? 0) > 0 && (
          <select className="tm-select tm-select--inline" aria-label="タグで絞り込む" value={params.tagId} onChange={(e) => params.update({ tagId: e.target.value || null })}>
            <option value="">タグ</option>
            {tags.data?.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
        )}
        {(milestones.data?.length ?? 0) > 0 && (
          <select className="tm-select tm-select--inline" aria-label="マイルストーンで絞り込む" value={params.milestoneId} onChange={(e) => params.update({ milestoneId: e.target.value || null })}>
            <option value="">マイルストーン</option>
            <option value="none">なし</option>
            {milestones.data?.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </select>
        )}
        {params.view === 'table' && (
          <label className="tm-checkbox tm-small">
            <input type="checkbox" checked={params.includeClosed} onChange={(e) => params.update({ includeClosed: e.target.checked })} />
            完了・中止も表示
          </label>
        )}
      </div>
      {chips.length > 0 && (
        <div className="tm-row" style={{ marginTop: 'var(--space-2)' }}>
          <span className="tm-small tm-muted">適用中:</span>
          {chips.map((c) => (
            <span key={c.label} className="tm-chip" style={{ background: '#e8eef8' }}>
              {c.label}
              <button type="button" aria-label={`${c.label} を外す`} onClick={c.remove}>✕</button>
            </span>
          ))}
          <Button size="small" variant="ghost" onClick={params.clear}>条件をクリア</Button>
        </div>
      )}
    </section>
  )
}
