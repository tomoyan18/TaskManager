import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState, type PointerEvent as ReactPointerEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { apiClient, call, type Schemas } from '../../api/client'
import { queryKeys, useMilestones, useStatuses } from '../../api/queries'
import { Button } from '../../components/Button'
import { DueDateLabel, StatusBadge, UserLabel } from '../../components/Display'
import { EmptyState, ErrorPanel, Loading } from '../../components/Feedback'
import { useToast } from '../../components/Toast'
import type { useTaskListParams } from '../../hooks/useTaskListParams'
import { diffDays, formatDate, formatDue } from '../../utils/dates'
import {
  DAY_WIDTH, SCHEDULE_GROUPS, SCHEDULE_GROUP_LABELS, SCHEDULE_UNITS, SCHEDULE_UNIT_LABELS, barPlacement, barRange, columnsFor,
  defaultStart, formatWindow, groupScheduleRows, movedDates, shiftStart, todayOffset, windowFor, type ScheduleRowGroup,
} from '../../utils/schedule'
import { taskPath } from '../../utils/taskKey'
import { useUpdateTask, type TaskSummary } from './taskApi'
import { DateInput } from './TaskFields'
import styles from './TaskSchedule.module.css'

type Params = ReturnType<typeof useTaskListParams>
type Dates = { startDate: string | null; dueDate: string | null }
/** move = 日程の移動、resize = 期限の伸縮、open = 変更できない棒（押して離すとタスクを開く） */
type Drag = { number: number; mode: 'move' | 'resize' | 'open'; originX: number; delta: number }

const isClosed = (t: TaskSummary) => t.status.category === 'done' || t.status.category === 'canceled'

/** 日程の文（「10/1（木）〜10/9（金）」「10/9（金）（開始日なし）」） */
export function scheduleText(t: Dates, today: string): string {
  if (t.startDate && t.dueDate) return `${formatDate(t.startDate, today)}〜${formatDate(t.dueDate, today)}`
  if (t.dueDate) return `${formatDate(t.dueDate, today)}（開始日なし）`
  if (t.startDate) return `${formatDate(t.startDate, today)}（期限なし）`
  return '日付なし'
}

/**
 * SCR-20 ガントチャート（REQ-GNT-01〜03）。
 * 棒 = 開始日〜期限、色と文字 = ステータス、⚠ = 期限切れ、縦線 = 今日。日程の変更は既存のタスク更新（version 付き）で行う。
 * キーボード・支援技術では、行のタイトル（リンク）と「表で見る」（日付欄で変更）を使う。
 */
export function TaskScheduleView({ project, params, today, canEdit }: {
  project: Schemas['ProjectDetailResponse']
  params: Params
  today: string
  canEdit: boolean
}) {
  const unit = params.unit
  const start = params.start ?? defaultStart(unit, today)
  const { from, to } = windowFor(unit, start)
  const includeClosed = params.includeClosed
  const [asTable, setAsTable] = useState(false)
  const statuses = useStatuses(project.key)
  const milestones = useMilestones(project.key)
  const schedule = useQuery({
    queryKey: queryKeys.taskSchedule(project.key, { from, to, includeClosed }),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tasks/schedule', {
      params: { path: { projectKey: project.key }, query: { from, to, includeClosed: includeClosed || undefined } },
    })),
    placeholderData: keepPreviousData,
  })

  return (
    <div className="tm-stack" style={{ gap: 'var(--space-3)' }}>
      <div className={styles.toolbar}>
        <div className="tm-row" role="group" aria-label="表示単位">
          <span className="tm-small tm-muted" aria-hidden="true">表示単位</span>
          {SCHEDULE_UNITS.map((u) => (
            <Button key={u} size="small" variant={u === unit ? 'primary' : 'secondary'} aria-pressed={u === unit}
              onClick={() => params.update({ unit: u === 'week' ? null : u, start: null })}>
              {SCHEDULE_UNIT_LABELS[u]}
            </Button>
          ))}
        </div>
        <div className="tm-row" role="group" aria-label="表示期間">
          <Button size="small" onClick={() => params.update({ start: shiftStart(unit, start, -1) })}>‹ 前へ</Button>
          <Button size="small" onClick={() => params.update({ start: null })}>今日</Button>
          <Button size="small" onClick={() => params.update({ start: shiftStart(unit, start, 1) })}>次へ ›</Button>
          <span className="tm-small" aria-live="polite">{formatWindow(from, to)}</span>
        </div>
        <label className="tm-row tm-small">
          まとめ方
          <select className="tm-select tm-select--inline" value={params.group} onChange={(e) => params.update({ group: e.target.value === 'none' ? null : e.target.value })}>
            {SCHEDULE_GROUPS.map((g) => <option key={g} value={g}>{SCHEDULE_GROUP_LABELS[g]}</option>)}
          </select>
        </label>
        <label className="tm-checkbox tm-small">
          <input type="checkbox" checked={includeClosed} onChange={(e) => params.update({ includeClosed: e.target.checked })} />
          完了・中止も表示
        </label>
        <Button size="small" aria-pressed={asTable} onClick={() => setAsTable((v) => !v)}>表で見る</Button>
      </div>
      {schedule.isPending && <Loading rows={6} />}
      {schedule.isError && <ErrorPanel error={schedule.error} onRetry={() => void schedule.refetch()} />}
      {schedule.isSuccess && (
        <ScheduleContent key={`${from}-${to}`} projectKey={project.key} data={schedule.data} dataVersion={schedule.dataUpdatedAt}
          unit={unit} group={params.group} statuses={statuses.data} milestones={milestones.data} canEdit={canEdit}
          asTable={asTable} isFetching={schedule.isFetching} onToday={() => params.update({ start: null })} />
      )}
    </div>
  )
}

function ScheduleContent({ projectKey, data, dataVersion, unit, group, statuses, milestones, canEdit, asTable, isFetching, onToday }: {
  projectKey: string
  data: Schemas['TaskScheduleResponse']
  dataVersion: number
  unit: Params['unit']
  group: Params['group']
  statuses: Schemas['WorkflowStatusResponse'][] | undefined
  milestones: Schemas['MilestoneSummaryResponse'][] | undefined
  canEdit: boolean
  asTable: boolean
  isFetching: boolean
  onToday: () => void
}) {
  const update = useUpdateTask(projectKey)
  const toast = useToast()
  // 保存中の日程（取得し直すまで、ドラッグ後の位置に表示しておく）
  const [pending, setPending] = useState<{ version: number; dates: Record<number, Dates> }>({ version: dataVersion, dates: {} })
  const pendingDates = pending.version === dataVersion ? pending.dates : {}
  const withPending = (t: TaskSummary): TaskSummary => {
    const p = pendingDates[t.number]
    return p ? { ...t, startDate: p.startDate, dueDate: p.dueDate } : t
  }
  const items = data.items.map(withPending)
  const groups = groupScheduleRows(items, group, statuses, milestones)

  const save = (task: TaskSummary, dates: Dates) => {
    if (dates.startDate && dates.dueDate && dates.startDate > dates.dueDate) {
      toast.error('期限は開始日以降の日付にしてください。')
      return
    }
    if (dates.startDate === (task.startDate ?? null) && dates.dueDate === (task.dueDate ?? null)) return
    setPending({ version: dataVersion, dates: { ...pendingDates, [task.number]: dates } })
    update.mutate({ number: task.number, body: { startDate: dates.startDate, dueDate: dates.dueDate, version: task.version } }, {
      onSuccess: () => toast.success(`${task.key} の日程を変更しました（${scheduleText(dates, data.today)}）`),
      onError: () => setPending((current) => ({ version: current.version, dates: Object.fromEntries(Object.entries(current.dates).filter(([n]) => Number(n) !== task.number)) })),
    })
  }

  if (data.items.length === 0 && data.undatedCount === 0) {
    return (
      <EmptyState title="この期間に日程のあるタスクはありません" text="開始日や期限を設定すると、ここに表示されます。期間を移動することもできます。"
        action={<Button onClick={onToday}>今日</Button>} />
    )
  }

  return (
    <div className="tm-stack" style={{ gap: 'var(--space-3)' }} aria-busy={isFetching || undefined}>
      {data.truncated && (
        <p className="tm-alert tm-alert--info tm-small" style={{ margin: 0 }}>表示できるのは 500 件までです。期間を短くするか、表示単位を変えてください。</p>
      )}
      {data.items.length === 0
        ? <p className="tm-small tm-muted" style={{ margin: 0 }}>この期間に日程のあるタスクはありません。</p>
        : asTable
          ? <TaskScheduleTable projectKey={projectKey} groups={groups} group={group} today={data.today} canEdit={canEdit} onSave={save} />
          : <GanttChart projectKey={projectKey} groups={groups} group={group} from={data.from} to={data.to} today={data.today} unit={unit} canEdit={canEdit} onSave={save} />}
      {!asTable && data.items.length > 0 && (
        <p className="tm-small tm-muted" style={{ margin: 0 }}>
          棒 = 開始日〜期限。色と文字 = ステータス。⚠ = 期限切れ。縦線 = 今日。◆ = マイルストーンの目標日。
          {canEdit && ' 棒をドラッグすると日程を、右端をドラッグすると期限を変更できます（キーボードでは「表で見る」の日付欄で変更できます）。'}
        </p>
      )}
      {data.undatedCount > 0 && <UndatedTasks projectKey={projectKey} items={data.undatedItems} count={data.undatedCount} />}
    </div>
  )
}

function GanttChart({ projectKey, groups, group, from, to, today, unit, canEdit, onSave }: {
  projectKey: string
  groups: ScheduleRowGroup[]
  group: Params['group']
  from: string
  to: string
  today: string
  unit: Params['unit']
  canEdit: boolean
  onSave: (task: TaskSummary, dates: Dates) => void
}) {
  const navigate = useNavigate()
  const [drag, setDrag] = useState<Drag | null>(null)
  const dayWidth = DAY_WIDTH[unit]
  const width = (diffDays(to, from) + 1) * dayWidth
  const columns = columnsFor(unit, from, to)
  const todayX = todayOffset(today, from, to, dayWidth)

  const begin = (e: ReactPointerEvent<HTMLElement>, task: TaskSummary, mode: Drag['mode']) => {
    if (e.button !== 0 || (mode !== 'open' && !canEdit)) return
    e.preventDefault()
    e.stopPropagation()
    e.currentTarget.setPointerCapture?.(e.pointerId)
    setDrag({ number: task.number, mode, originX: e.clientX, delta: 0 })
  }
  const moveTo = (e: ReactPointerEvent<HTMLElement>) => {
    if (drag === null) return
    const delta = Math.round((e.clientX - drag.originX) / dayWidth)
    if (delta !== drag.delta) setDrag({ ...drag, delta })
  }
  const end = (e: ReactPointerEvent<HTMLElement>, task: TaskSummary) => {
    if (drag === null || drag.number !== task.number) return
    const moved = Math.abs(e.clientX - drag.originX) > 4
    setDrag(null)
    if (!moved) {
      void navigate(taskPath(projectKey, task.number))
      return
    }
    if (drag.mode !== 'open' && drag.delta !== 0) onSave(task, movedDates(task, drag.mode, drag.delta))
  }

  return (
    <section className={styles.gantt} aria-labelledby="schedule-gantt-title">
      <h2 id="schedule-gantt-title" className="tm-visually-hidden">ガントチャート</h2>
      <div className={styles.scroller}>
        <div className={styles.canvas} style={{ width: `calc(var(--gantt-label) + ${width}px)` }}>
          <div className={styles.overlay} style={{ width }} aria-hidden="true">
            {columns.map((c) => (
              <div key={c.start} className={styles.columnBand} data-weekend={c.weekend || undefined}
                style={{ left: diffDays(c.start, from) * dayWidth, width: c.days * dayWidth }} />
            ))}
            {todayX !== null && <div className={styles.todayLine} style={{ left: todayX }} />}
          </div>
          <div className={styles.headerRow} aria-hidden="true">
            <div className={styles.labelCell}>タスク</div>
            <div className={styles.track} style={{ width }}>
              {columns.map((c) => (
                <div key={c.start} className={styles.columnLabel} style={{ left: diffDays(c.start, from) * dayWidth, width: c.days * dayWidth }}>
                  <span>{c.label}</span>
                  {c.detail && <span className="tm-muted">{c.detail}</span>}
                </div>
              ))}
              {todayX !== null && <span className={styles.todayLabel} style={{ left: todayX }}>今日</span>}
            </div>
          </div>
          {groups.map((g) => (
            <section key={g.key} aria-label={group === 'none' ? 'タスク' : `${g.label}（${g.tasks.length} 件）`}>
              {group !== 'none' && (
                <div className={styles.groupRow}>
                  <div className={styles.labelCell}>
                    <h3 className={styles.groupTitle}>
                      {g.status ? <StatusBadge status={g.status} /> : g.kind === 'assignee' && g.assignee ? <UserLabel user={g.assignee} /> : g.label}
                      <span className="tm-small tm-muted">（{g.tasks.length}）</span>
                    </h3>
                    {g.milestoneTargetDate && <span className="tm-small tm-muted">目標日 {formatDate(g.milestoneTargetDate, today)}</span>}
                  </div>
                  <div className={styles.track} style={{ width }} aria-hidden="true">
                    {g.milestoneTargetDate && g.milestoneTargetDate >= from && g.milestoneTargetDate <= to && (
                      <span className={styles.milestoneMark} style={{ left: (diffDays(g.milestoneTargetDate, from) + 0.5) * dayWidth }}>◆</span>
                    )}
                  </div>
                </div>
              )}
              <ul className={styles.rows}>
                {g.tasks.map((t) => {
                  const dragging = drag !== null && drag.number === t.number && drag.mode !== 'open'
                  const dates = dragging && drag.mode !== 'open' ? movedDates(t, drag.mode, drag.delta) : { startDate: t.startDate ?? null, dueDate: t.dueDate ?? null }
                  const placement = barPlacement(barRange(dates), from, to, dayWidth)
                  const overdue = t.isOverdue && !dragging
                  const editable = canEdit && barRange(t) !== null
                  return (
                    <li key={t.key} className={styles.row}>
                      <div className={styles.labelCell}>
                        <span className="tm-key">{t.key}</span>
                        <Link to={taskPath(projectKey, t.number)} className={styles.rowTitle}>{t.title}</Link>
                        <span className={styles.rowMeta}>
                          {scheduleText(dates, today)}
                          {t.isOverdue && <span className="tm-due--overdue">（⚠ {formatDue(t.dueDate, today)}）</span>}
                          {/* 棒が短いとステータス名が見えないため、文字でも示す（色だけに頼らない） */}
                          <span>・{t.status.name}</span>
                          <span className="tm-visually-hidden">{t.assignee ? `、担当 ${t.assignee.displayName}` : '、担当者なし'}</span>
                        </span>
                      </div>
                      <div className={styles.track} style={{ width }} onPointerMove={moveTo} onPointerUp={(e) => end(e, t)}
                        onPointerCancel={() => setDrag(null)} aria-hidden="true">
                        {placement && (
                          // oxlint-disable-next-line jsx-a11y/no-static-element-interactions -- 棒はマウス・タッチでの操作。キーボードでは行のタイトルのリンクと「表で見る」の日付欄を使う
                          <div className={styles.bar} data-color={t.status.color} data-overdue={overdue || undefined}
                            data-closed={isClosed(t) || undefined} data-editable={editable || undefined} data-dragging={dragging || undefined}
                            style={{ left: placement.left, width: Math.max(placement.width, 6) }}
                            title={`${t.key} ${t.title}（${t.status.name}、${scheduleText(dates, today)}）`}
                            onPointerDown={(e) => begin(e, t, editable ? 'move' : 'open')}>
                            {placement.clippedStart && <span className={styles.clip}>‹</span>}
                            <span className={styles.barText}>{t.status.name}</span>
                            {placement.clippedEnd && <span className={styles.clip}>›</span>}
                            {editable && !placement.clippedEnd && (
                              <span className={styles.handle} onPointerDown={(e) => begin(e, t, 'resize')} />
                            )}
                          </div>
                        )}
                        {placement && overdue && (
                          <span className={styles.overdueText} style={{ left: placement.left + Math.max(placement.width, 6) + 4 }}>
                            ⚠ {formatDue(t.dueDate, today)}
                          </span>
                        )}
                        {placement && dragging && drag?.delta !== 0 && (
                          <span className={styles.dragText} style={{ left: placement.left + Math.max(placement.width, 6) + 4 }}>
                            {scheduleText(dates, today)}
                          </span>
                        )}
                      </div>
                    </li>
                  )
                })}
              </ul>
            </section>
          ))}
        </div>
      </div>
    </section>
  )
}

/** REQ-GNT-03 表形式の代替表示（同じ行・同じまとめ方。編集者以上は日付欄で日程を変更できる） */
export function TaskScheduleTable({ projectKey, groups, group, today, canEdit, onSave }: {
  projectKey: string
  groups: ScheduleRowGroup[]
  group: Params['group']
  today: string
  canEdit: boolean
  onSave: (task: TaskSummary, dates: Dates) => void
}) {
  return (
    <div className="tm-table-wrap">
      <table className="tm-table">
        <caption className="tm-visually-hidden">タスクの日程（表）</caption>
        <thead>
          <tr>
            <th scope="col">キー</th>
            <th scope="col">タイトル</th>
            <th scope="col">ステータス</th>
            <th scope="col">担当者</th>
            <th scope="col">開始日</th>
            <th scope="col">期限</th>
            <th scope="col">日数</th>
            <th scope="col">期限の状態</th>
          </tr>
        </thead>
        {groups.map((g) => (
          <tbody key={g.key}>
            {group !== 'none' && (
              <tr className={styles.tableGroup}>
                <th scope="colgroup" colSpan={8}>{g.label}（{g.tasks.length} 件）</th>
              </tr>
            )}
            {g.tasks.map((t) => {
              const range = barRange(t)
              return (
                <tr key={t.key}>
                  <td className="tm-key">{t.key}</td>
                  <td><Link to={taskPath(projectKey, t.number)} className="tm-link--plain" style={{ fontWeight: 600 }}>{t.title}</Link></td>
                  <td><StatusBadge status={t.status} /></td>
                  <td><UserLabel user={t.assignee} /></td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {canEdit
                      ? <DateInput inline today={today} showShortcuts={false} value={t.startDate ?? null} aria-label={`${t.key} の開始日`}
                          onChange={(startDate) => onSave(t, { startDate, dueDate: t.dueDate ?? null })} />
                      : t.startDate ? formatDate(t.startDate, today) : <span className="tm-muted">なし</span>}
                  </td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    {canEdit
                      ? <DateInput inline today={today} showShortcuts={false} value={t.dueDate ?? null} aria-label={`${t.key} の期限`}
                          onChange={(dueDate) => onSave(t, { startDate: t.startDate ?? null, dueDate })} />
                      : t.dueDate ? formatDate(t.dueDate, today) : <span className="tm-muted">なし</span>}
                  </td>
                  <td>{range ? `${diffDays(range.end, range.start) + 1} 日` : '—'}</td>
                  <td><DueDateLabel dueDate={t.dueDate} today={today} isOverdue={t.isOverdue} closed={isClosed(t)} /></td>
                </tr>
              )
            })}
          </tbody>
        ))}
      </table>
    </div>
  )
}

function UndatedTasks({ projectKey, items, count }: { projectKey: string; items: TaskSummary[]; count: number }) {
  return (
    <section className="tm-card" aria-labelledby="schedule-undated">
      <h2 id="schedule-undated" className="tm-section-title">日付が未設定のタスク（{count} 件）</h2>
      <p className="tm-small tm-muted" style={{ marginTop: 0 }}>開始日も期限もないタスクです。</p>
      <ul className={styles.undatedList}>
        {items.map((t) => (
          <li key={t.key}>
            <span className="tm-key">{t.key}</span>
            <Link to={taskPath(projectKey, t.number)} className="tm-link--plain" style={{ fontWeight: 600 }}>{t.title}</Link>
            <StatusBadge status={t.status} />
          </li>
        ))}
      </ul>
      {count > items.length && <p className="tm-small tm-muted" style={{ marginBottom: 0 }}>ほか {count - items.length} 件（表示はタスクの一覧で確認できます）</p>}
    </section>
  )
}
