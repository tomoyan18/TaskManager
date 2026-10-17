import { delay, http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import * as f from '../test/fixtures'
import { server } from '../test/server'
import {
  apiClient, ApiError, authEvents, authorizedFetch, call, PASSWORD_CHANGE_REQUIRED, refreshSession, SESSION_EXPIRED, tokenStore,
} from './client'

const getMe = () => call(() => apiClient.GET('/api/v1/me'))

describe('call（API 呼び出しの共通処理）', () => {
  it('成功時はデータを返し、アクセストークンを付けて送る', async () => {
    let authorization: string | null = null
    server.use(http.get('*/api/v1/me', ({ request }) => {
      authorization = request.headers.get('Authorization')
      return HttpResponse.json(f.user())
    }))
    tokenStore.set('token-1')

    const me = await getMe()

    expect(me.displayName).toBe('テスト 管理者')
    expect(authorization).toBe('Bearer token-1')
  })

  it('Problem Details を ApiError に変換する', async () => {
    server.use(http.get('*/api/v1/me', () => HttpResponse.json(
      f.problem(400, 'validation.failed', '入力内容に誤りがあります。', { detail: '赤く表示された項目を確認してください。', errors: { title: ['タイトルを入力してください。'] } }),
      { status: 400 },
    )))

    const error = await getMe().catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    const e = error as ApiError
    expect(e.status).toBe(400)
    expect(e.code).toBe('validation.failed')
    expect(e.title).toBe('入力内容に誤りがあります。')
    expect(e.detail).toBe('赤く表示された項目を確認してください。')
    expect(e.fieldErrors).toEqual({ title: ['タイトルを入力してください。'] })
    expect(e.traceId).toBe('trace-0001')
  })

  it('通信できない場合は日本語のエラー（status 0）', async () => {
    server.use(http.get('*/api/v1/me', () => HttpResponse.error()))

    const e = (await getMe().catch((x: unknown) => x)) as ApiError

    expect(e.status).toBe(0)
    expect(e.code).toBe('network.error')
    expect(e.title).toBe('通信できませんでした。ネットワークの状態を確認してください。')
  })

  it('401 ならトークンを 1 回だけ更新して再送する', async () => {
    const seen: (string | null)[] = []
    server.use(
      http.get('*/api/v1/me', ({ request }) => {
        const auth = request.headers.get('Authorization')
        seen.push(auth)
        return auth === 'Bearer token-for-01990000-0000-7000-8000-000000000001'
          ? HttpResponse.json(f.user())
          : HttpResponse.json(f.problem(401, 'auth.unauthenticated', 'ログインが必要です。'), { status: 401 })
      }),
    )
    tokenStore.set('expired-token')

    const me = await getMe()

    expect(me.id).toBe(f.user().id)
    expect(seen).toEqual(['Bearer expired-token', 'Bearer token-for-01990000-0000-7000-8000-000000000001'])
  })

  it('同時に 401 になってもトークンの更新は 1 回にまとめる', async () => {
    let refreshCount = 0
    server.use(
      http.post('*/api/v1/auth/refresh', async () => {
        refreshCount += 1
        await delay(20)
        return HttpResponse.json(f.loginResponse())
      }),
      http.get('*/api/v1/me', ({ request }) =>
        request.headers.get('Authorization') === 'Bearer stale'
          ? HttpResponse.json(f.problem(401, 'auth.unauthenticated', 'ログインが必要です。'), { status: 401 })
          : HttpResponse.json(f.user())),
    )
    tokenStore.set('stale')

    await Promise.all([getMe(), getMe(), getMe()])

    expect(refreshCount).toBe(1)
  })

  it('更新できなければトークンを消してセッション切れを知らせる', async () => {
    server.use(
      http.post('*/api/v1/auth/refresh', () => HttpResponse.json(f.problem(401, 'auth.session_expired', '期限切れ'), { status: 401 })),
      http.get('*/api/v1/me', () => HttpResponse.json(f.problem(401, 'auth.unauthenticated', 'ログインが必要です。'), { status: 401 })),
    )
    const onExpired = vi.fn()
    authEvents.addEventListener(SESSION_EXPIRED, onExpired)
    tokenStore.set('stale')

    const e = (await getMe().catch((x: unknown) => x)) as ApiError

    authEvents.removeEventListener(SESSION_EXPIRED, onExpired)
    expect(e.status).toBe(401)
    expect(tokenStore.get()).toBeNull()
    expect(onExpired).toHaveBeenCalledTimes(1)
  })

  it('一時パスワード状態のエラーを知らせる', async () => {
    server.use(http.get('*/api/v1/projects', () =>
      HttpResponse.json(f.problem(403, 'auth.password_change_required', 'パスワードの変更が必要です。'), { status: 403 })))
    const onRequired = vi.fn()
    authEvents.addEventListener(PASSWORD_CHANGE_REQUIRED, onRequired)
    tokenStore.set('token')

    await call(() => apiClient.GET('/api/v1/projects')).catch(() => undefined)

    authEvents.removeEventListener(PASSWORD_CHANGE_REQUIRED, onRequired)
    expect(onRequired).toHaveBeenCalledTimes(1)
  })
})

describe('トークンの保持', () => {
  it('アクセストークンはメモリだけに置き、ブラウザの保存領域に書かない', async () => {
    window.localStorage.clear()
    window.sessionStorage.clear()

    const result = await refreshSession()

    expect(result?.accessToken).toBeTruthy()
    expect(tokenStore.get()).toBe(result?.accessToken)
    expect(window.localStorage.length).toBe(0)
    expect(window.sessionStorage.length).toBe(0)
    expect(document.cookie).not.toContain(result?.accessToken ?? 'x')
  })
})

describe('authorizedFetch（ファイルの送受信）', () => {
  it('トークンを付け、401 なら更新して再送する', async () => {
    const seen: (string | null)[] = []
    server.use(http.get('*/api/v1/projects/SALES/tasks/1/attachments/a-1/content', ({ request }) => {
      seen.push(request.headers.get('Authorization'))
      return seen.length === 1 ? new HttpResponse(null, { status: 401 }) : new HttpResponse('中身', { status: 200 })
    }))
    tokenStore.set('old')

    const response = await authorizedFetch('/api/v1/projects/SALES/tasks/1/attachments/a-1/content')

    expect(response.status).toBe(200)
    expect(seen[0]).toBe('Bearer old')
    expect(seen[1]).toBe(`Bearer ${f.loginResponse().accessToken}`)
  })
})
