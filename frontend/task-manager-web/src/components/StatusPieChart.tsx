import { useId, type ReactNode } from 'react'
import { Link } from 'react-router'
import type { Schemas } from '../api/client'
import styles from './StatusPieChart.module.css'

type Summary = Schemas['TaskStatusSummaryResponse']
type Item = Schemas['StatusCountResponse']

const SIZE = 200
const CENTER = SIZE / 2
const RADIUS = 92

/** 割合の表示（小数第 1 位。API が最大剰余法で合計 100.0 にそろえた値） */
export function formatPercent(percent: number): string {
  return `${percent.toFixed(1)}%`
}

/** スクリーンリーダー向けの要約（「合計 12 件。未着手 5 件（41.7%）、…」） */
export function summaryText(summary: Summary): string {
  const parts = summary.items.map((i) => `${i.name} ${i.count} 件（${formatPercent(i.percent)}）`)
  return `合計 ${summary.totalCount} 件。${parts.join('、')}`
}

/** 円の扇形（12 時の位置から時計回り） */
function slicePath(startFraction: number, endFraction: number): string {
  const point = (fraction: number) => {
    const angle = 2 * Math.PI * fraction - Math.PI / 2
    return `${(CENTER + RADIUS * Math.cos(angle)).toFixed(3)} ${(CENTER + RADIUS * Math.sin(angle)).toFixed(3)}`
  }
  const largeArc = endFraction - startFraction > 0.5 ? 1 : 0
  return `M ${CENTER} ${CENTER} L ${point(startFraction)} A ${RADIUS} ${RADIUS} 0 ${largeArc} 1 ${point(endFraction)} Z`
}

/**
 * ステータス別の円グラフ（SCR-21、SCR-04。REQ-CHT-01・02）。
 * 色だけに頼らないよう、凡例を兼ねる表（名前・件数・割合）と合計、要約の文を必ず表示する。
 * 色はステータスの色帯（--sc-{color}-bar）をクラスで指定し、インラインの <style> は使わない（CSP）。
 */
export function StatusPieChart({ title, summary, itemLink, empty, note }: {
  title: string
  summary: Summary
  /** ステータス名のリンク先（null ならリンクにしない） */
  itemLink?: (item: Item) => string | null
  /** 0 件のときの表示 */
  empty: ReactNode
  /** 表の下の補足 */
  note?: ReactNode
}) {
  const id = useId()
  const titleId = `${id}-title`
  const descId = `${id}-desc`
  if (summary.totalCount === 0) return <>{empty}</>

  const visible = summary.items.filter((i) => i.count > 0)
  const slices = visible.map((item, index) => {
    const before = visible.slice(0, index).reduce((sum, i) => sum + i.count, 0)
    return { item, start: before / summary.totalCount, end: (before + item.count) / summary.totalCount }
  })

  return (
    <section className={styles.chart} aria-labelledby={titleId}>
      <div className={styles.header}>
        <h2 id={titleId} className="tm-section-title" style={{ margin: 0 }}>{title}</h2>
        <span className="tm-spacer" />
        <span className={styles.total}>合計 <strong>{summary.totalCount}</strong> 件</span>
      </div>
      <div className={styles.body}>
        <svg className={styles.pie} viewBox={`0 0 ${SIZE} ${SIZE}`} role="img" aria-labelledby={`${titleId} ${descId}`}>
          <desc id={descId}>{summaryText(summary)}</desc>
          {slices.length === 1
            ? (
              <circle cx={CENTER} cy={CENTER} r={RADIUS} className={styles.slice} data-color={slices[0]!.item.color}>
                <title>{`${slices[0]!.item.name} ${slices[0]!.item.count} 件（${formatPercent(slices[0]!.item.percent)}）`}</title>
              </circle>
            )
            : slices.map(({ item, start, end }) => (
              <path key={item.statusId ?? item.name} d={slicePath(start, end)} className={styles.slice} data-color={item.color}>
                <title>{`${item.name} ${item.count} 件（${formatPercent(item.percent)}）`}</title>
              </path>
            ))}
        </svg>
        <div className={styles.tableWrap}>
          <table className="tm-table">
            <caption className="tm-visually-hidden">{title}（表）</caption>
            <thead>
              <tr>
                <th scope="col">{summary.groupedBy === 'category' ? '区分' : 'ステータス'}</th>
                <th scope="col" className={styles.number}>件数</th>
                <th scope="col" className={styles.number}>割合</th>
              </tr>
            </thead>
            <tbody>
              {summary.items.map((item) => {
                const to = itemLink?.(item) ?? null
                return (
                  <tr key={item.statusId ?? item.name} data-zero={item.count === 0 || undefined}>
                    <th scope="row" className={styles.nameCell}>
                      <span className={styles.swatch} data-color={item.color} aria-hidden="true" />
                      {to !== null ? <Link to={to} className="tm-link">{item.name}</Link> : item.name}
                    </th>
                    <td className={styles.number}>{item.count}</td>
                    <td className={styles.number}>{formatPercent(item.percent)}</td>
                  </tr>
                )
              })}
            </tbody>
            <tfoot>
              <tr>
                <th scope="row">合計</th>
                <td className={styles.number}>{summary.totalCount}</td>
                <td className={styles.number}>{formatPercent(100)}</td>
              </tr>
            </tfoot>
          </table>
          {note && <p className="tm-small tm-muted" style={{ margin: 'var(--space-2) 0 0' }}>{note}</p>}
        </div>
      </div>
    </section>
  )
}
