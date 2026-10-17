// 最初に読み込む共通設定。画面のコード（api/client.ts）が fetch を取り込む前に、MSW と相対 URL の補正を有効にする
// （openapi-fetch は createClient の時点で globalThis.fetch を保持するため、読み込み順が重要。Phase 6 で修正）
import { server } from './server'

// 画面のコードは相対 URL（/api/...）で通信する。Node の fetch は相対 URL を扱えないため、jsdom の location を基準に補う
function absolute(input: RequestInfo | URL): RequestInfo | URL {
  return typeof input === 'string' && input.startsWith('/') ? new URL(input, window.location.origin).toString() : input
}

server.listen({ onUnhandledRequest: 'error' })
const mswFetch = globalThis.fetch
globalThis.fetch = (input: RequestInfo | URL, init?: RequestInit) => mswFetch(absolute(input), init)
const BaseRequest = globalThis.Request
globalThis.Request = class extends BaseRequest {
  constructor(input: RequestInfo | URL, init?: RequestInit) {
    super(absolute(input), init)
  }
}

// jsdom にない API（画面部品が参照する）
window.matchMedia ??= (query: string) => ({
  matches: false, media: query, onchange: null,
  addListener: () => undefined, removeListener: () => undefined,
  addEventListener: () => undefined, removeEventListener: () => undefined, dispatchEvent: () => false,
})
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
}
Element.prototype.scrollIntoView ??= function scrollIntoView() {}
// jsdom には CSS.escape がない（react-aria のダイアログが使う。ブラウザには標準である）
globalThis.CSS ??= {} as typeof CSS
CSS.escape ??= (value: string) => value.replace(/[^a-zA-Z0-9_-]/g, (c) => `\\${c}`)
