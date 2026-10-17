import ReactMarkdown, { type Components } from 'react-markdown'
import remarkGfm from 'remark-gfm'
import { isSafeInternalPath } from '../utils/url'

/** 許可するリンク先: http / https / mailto / 画面内の相対パス（security-design.md S-08） */
export function safeUrl(url: string): string {
  const trimmed = url.trim()
  if (/^(https?:|mailto:)/i.test(trimmed)) return trimmed
  if (isSafeInternalPath(trimmed)) return trimmed
  return ''
}

const components: Components = {
  a: ({ href, children }) => {
    const url = href ? safeUrl(href) : ''
    if (url === '') return <span>{children}</span>
    const external = !url.startsWith('/')
    return (
      <a href={url} className="tm-link" {...(external ? { target: '_blank', rel: 'noopener noreferrer' } : {})}>
        {children}
      </a>
    )
  },
  // OD-28: Markdown 内の画像は読み込まず、リンクとして表示する
  img: ({ src, alt }) => {
    const url = typeof src === 'string' ? safeUrl(src) : ''
    return url ? <a href={url} className="tm-link" target="_blank" rel="noopener noreferrer">画像: {alt || url}</a> : <span>{alt}</span>
  },
}

/** Markdown の表示。生の HTML は無効化する（XSS 対策） */
export function MarkdownView({ text }: { text: string }) {
  return (
    <div className="tm-markdown">
      <ReactMarkdown remarkPlugins={[remarkGfm]} skipHtml urlTransform={(url) => safeUrl(url)} components={components}>
        {text}
      </ReactMarkdown>
    </div>
  )
}
