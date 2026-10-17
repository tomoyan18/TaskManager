import { render, screen, within } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import * as f from '../test/fixtures'
import { EmptyState } from './Feedback'
import { formatPercent, StatusPieChart, summaryText } from './StatusPieChart'

function renderChart(summary = f.statusSummary(), itemLink?: (item: { statusId?: string | null }) => string | null) {
  const router = createMemoryRouter([{
    path: '*',
    element: <StatusPieChart title="ステータス別の割合" summary={summary} itemLink={itemLink}
      empty={<EmptyState title="まだタスクがありません" />} note="ステータス名を選ぶと、そのステータスのタスクを表で表示します。" />,
  }])
  return render(<RouterProvider router={router} />)
}

describe('ステータス別の円グラフ（StatusPieChart）', () => {
  it('0 件のステータスも表（凡例）に出し、件数・割合・合計を数値で示す', () => {
    renderChart()

    const table = screen.getByRole('table', { name: 'ステータス別の割合（表）' })
    const rows = within(table).getAllByRole('row').slice(1, -1)
    expect(rows.map((r) => r.textContent)).toEqual(['未着手541.7%', '対応中433.3%', '完了325.0%', '中止00.0%'])
    expect(within(table).getByRole('row', { name: /合計/ }).textContent).toBe('合計12100.0%')
    expect(screen.getByText(/合計/, { selector: 'span' }).textContent).toBe('合計 12 件')
  })

  it('円は件数のあるステータスだけを描き、要約の文をスクリーンリーダーに伝える', () => {
    const { container } = renderChart()

    const img = screen.getByRole('img')
    expect(img.getAttribute('aria-labelledby')).toContain(' ')
    expect(container.querySelector('desc')?.textContent).toBe('合計 12 件。未着手 5 件（41.7%）、対応中 4 件（33.3%）、完了 3 件（25.0%）、中止 0 件（0.0%）')
    expect(container.querySelectorAll('path')).toHaveLength(3)
    expect(Array.from(container.querySelectorAll('path'), (p) => p.getAttribute('data-color'))).toEqual(['gray', 'blue', 'green'])
  })

  it('1 つのステータスだけなら円全体を描く', () => {
    const summary = f.statusSummary({ totalCount: 2, items: f.statusSummary().items.map((i, n) => ({ ...i, count: n === 1 ? 2 : 0, percent: n === 1 ? 100 : 0 })) })
    const { container } = renderChart(summary)

    expect(container.querySelectorAll('circle')).toHaveLength(1)
    expect(container.querySelectorAll('path')).toHaveLength(0)
  })

  it('合計 0 件なら円を描かず空状態を表示する', () => {
    const { container } = renderChart(f.statusSummary({ totalCount: 0, items: f.statusSummary().items.map((i) => ({ ...i, count: 0, percent: 0 })) }))

    expect(screen.getByText('まだタスクがありません')).toBeTruthy()
    expect(container.querySelector('svg')).toBeNull()
  })

  it('ステータス名をリンクにできる', () => {
    renderChart(undefined, (item) => (item.statusId ? `/projects/SALES/tasks?statusId=${item.statusId}` : null))

    expect(screen.getByRole('link', { name: '対応中' }).getAttribute('href')).toBe('/projects/SALES/tasks?statusId=s-2')
  })

  it('割合の表示と要約の文', () => {
    expect(formatPercent(33.3)).toBe('33.3%')
    expect(formatPercent(100)).toBe('100.0%')
    expect(summaryText(f.statusSummary({ totalCount: 1, items: [] }))).toBe('合計 1 件。')
  })
})
