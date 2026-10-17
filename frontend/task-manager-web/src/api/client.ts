import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

/**
 * TaskManager API のクライアント（docs/design/ui-design.md §7.4）。
 * 型は OpenAPI（docs/api/openapi.v1.json）から生成した schema.d.ts を使う。
 * アクセストークンはメモリにのみ保持し、localStorage には保存しない（security-design.md S-08）。
 */
export type Schemas = components['schemas']

let accessToken: string | null = null
let refreshPromise: Promise<Schemas['LoginResponse'] | null> | null = null

export const tokenStore = {
  get: () => accessToken,
  set: (token: string | null) => {
    accessToken = token
  },
}

/** 認証状態の変化を AuthProvider に伝えるイベント */
export const authEvents = new EventTarget()
export const SESSION_EXPIRED = 'session-expired'
export const PASSWORD_CHANGE_REQUIRED = 'password-change-required'
export const SESSION_REFRESHED = 'session-refreshed'

export const apiClient = createClient<paths>({ baseUrl: '' })

apiClient.use({
  onRequest({ request }) {
    if (accessToken !== null) {
      request.headers.set('Authorization', `Bearer ${accessToken}`)
    }
    return request
  },
})

/** API のエラー（Problem Details を変換したもの） */
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly title: string
  readonly detail: string | null
  readonly fieldErrors: Record<string, string[]>
  readonly traceId: string | null

  constructor(status: number, problem: Partial<Schemas['ProblemDetails']> | undefined) {
    const title = problem?.title ?? defaultTitle(status)
    super(title)
    this.status = status
    this.code = problem?.code ?? (status === 0 ? 'network.error' : 'server.error')
    this.title = title
    this.detail = problem?.detail ?? null
    this.fieldErrors = (problem?.errors as Record<string, string[]> | null | undefined) ?? {}
    this.traceId = problem?.traceId ?? null
  }
}

function defaultTitle(status: number): string {
  if (status === 0) return '通信できませんでした。ネットワークの状態を確認してください。'
  if (status === 404) return '対象が見つかりません。'
  if (status === 429) return '短時間にリクエストが集中しました。'
  return '処理中にエラーが発生しました。'
}

/** リフレッシュ Cookie でアクセストークンを取り直す（同時に呼ばれても 1 回にまとめる） */
export function refreshSession(): Promise<Schemas['LoginResponse'] | null> {
  refreshPromise ??= (async () => {
    try {
      const response = await fetch('/api/v1/auth/refresh', { method: 'POST', credentials: 'same-origin' })
      if (!response.ok) {
        tokenStore.set(null)
        return null
      }
      const body = (await response.json()) as Schemas['LoginResponse']
      tokenStore.set(body.accessToken)
      authEvents.dispatchEvent(new CustomEvent(SESSION_REFRESHED, { detail: body }))
      return body
    } catch {
      return null
    } finally {
      // 完了したら直ちに外す。完了済みの結果を後の呼び出しに返さない（Phase 6 で修正）
      refreshPromise = null
    }
  })()
  return refreshPromise
}

type FetchResult<T> = { data?: T; error?: unknown; response: Response }

/**
 * openapi-fetch の呼び出しを実行し、成功ならデータを返す。
 * 401 のときは 1 回だけトークンを更新して再実行する。失敗は ApiError を投げる。
 */
export async function call<T>(request: () => Promise<FetchResult<T>>): Promise<T> {
  let result: FetchResult<T>
  try {
    result = await request()
    if (result.response.status === 401 && tokenStore.get() !== null) {
      const refreshed = await refreshSession()
      if (refreshed !== null) {
        result = await request()
      }
    }
  } catch (e) {
    if (e instanceof ApiError) throw e
    throw new ApiError(0, undefined)
  }
  if (!result.response.ok) {
    const problem = result.error as Partial<Schemas['ProblemDetails']> | undefined
    const error = new ApiError(result.response.status, problem)
    if (error.status === 401 && !result.response.url.includes('/api/v1/auth/')) {
      tokenStore.set(null)
      authEvents.dispatchEvent(new Event(SESSION_EXPIRED))
    }
    if (error.code === 'auth.password_change_required') {
      authEvents.dispatchEvent(new Event(PASSWORD_CHANGE_REQUIRED))
    }
    throw error
  }
  return result.data as T
}

/** 認証付きの生の fetch（ファイルのアップロード・ダウンロード用）。401 なら 1 回だけ更新して再送する */
export async function authorizedFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const send = () => {
    const headers = new Headers(init.headers)
    const token = tokenStore.get()
    if (token !== null) headers.set('Authorization', `Bearer ${token}`)
    return fetch(input, { ...init, headers, credentials: 'same-origin' })
  }
  let response = await send()
  if (response.status === 401 && (await refreshSession()) !== null) {
    response = await send()
  }
  return response
}

export async function toApiError(response: Response): Promise<ApiError> {
  let problem: Partial<Schemas['ProblemDetails']> | undefined
  try {
    problem = (await response.json()) as Partial<Schemas['ProblemDetails']>
  } catch {
    problem = undefined
  }
  return new ApiError(response.status, problem)
}
