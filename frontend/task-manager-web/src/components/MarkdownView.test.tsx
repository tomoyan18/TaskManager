import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MarkdownView, safeUrl } from './MarkdownView'

describe('MarkdownView（XSS 対策。security-design.md S-08）', () => {
  it('Markdown の書式を表示する', () => {
    render(<MarkdownView text={'単価表を **共有** します。\n\n- 項目1\n- 項目2'} />)
    expect(screen.getByText('共有').tagName).toBe('STRONG')
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })

  it('生の HTML（script・イベント属性）は要素として描画しない', () => {
    const { container } = render(
      <MarkdownView text={'前<script>alert(1)</script>後\n\n<img src="x" onerror="alert(1)">\n\n<iframe src="https://evil.example.test"></iframe>'} />,
    )
    expect(container.querySelector('script')).toBeNull()
    expect(container.querySelector('img')).toBeNull()
    expect(container.querySelector('iframe')).toBeNull()
    expect(container.querySelector('[onerror]')).toBeNull()
  })

  it.each([
    'javascript:alert(1)',
    'JAVASCRIPT:alert(1)',
    ' javascript:alert(1)',
    'data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==',
    'vbscript:msgbox(1)',
    '//evil.example.test/',
    // ブラウザが外部サイト（//evil.example.test）として扱う形（Phase 7 で追加）
    '/\\evil.example.test/',
  ])('危険なリンク %s は、リンクにしないか画面内のパスにとどめる', (href) => {
    const { container } = render(<MarkdownView text={`[押してください](${href})`} />)
    const link = container.querySelector('a')
    // Markdown の処理で符号化される場合（/\\ → /%5C）はリンクとして残るが、行き先は同じオリジンの中である
    if (link !== null) {
      const origin = 'https://taskmanager.example.test'
      expect(new URL(link.getAttribute('href') ?? '', `${origin}/projects/`).origin).toBe(origin)
    }
    expect(screen.getByText('押してください')).toBeTruthy()
  })

  it('外部リンクは新しいタブで開き、参照元を渡さない', () => {
    render(<MarkdownView text="[資料](https://example.test/doc)" />)
    const link = screen.getByRole('link', { name: '資料' })
    expect(link.getAttribute('href')).toBe('https://example.test/doc')
    expect(link.getAttribute('target')).toBe('_blank')
    expect(link.getAttribute('rel')).toBe('noopener noreferrer')
  })

  it('画面内のリンクは同じタブで開く', () => {
    render(<MarkdownView text="[タスク](/projects/SALES/tasks/1)" />)
    const link = screen.getByRole('link', { name: 'タスク' })
    expect(link.getAttribute('target')).toBeNull()
  })

  it('画像は読み込まずリンクとして表示する', () => {
    const { container } = render(<MarkdownView text="![図](https://example.test/a.png)" />)
    expect(container.querySelector('img')).toBeNull()
    expect(screen.getByRole('link', { name: '画像: 図' }).getAttribute('href')).toBe('https://example.test/a.png')
  })
})

describe('safeUrl', () => {
  it.each([
    ['https://example.test', 'https://example.test'],
    ['http://example.test', 'http://example.test'],
    ['mailto:a@example.test', 'mailto:a@example.test'],
    ['/projects', '/projects'],
    ['javascript:alert(1)', ''],
    ['//evil.example.test', ''],
    ['/\\evil.example.test', ''],
    ['/\t/evil.example.test', ''],
    ['/\n/evil.example.test', ''],
    ['relative/path', ''],
  ])('%s → %s', (url, expected) => {
    expect(safeUrl(url)).toBe(expected)
  })
})
