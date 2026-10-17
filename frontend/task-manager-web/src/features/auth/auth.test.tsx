import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import * as f from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server, signedInAs } from '../../test/server'

describe('ログイン画面（SCR-01）', () => {
  it('未ログインでログイン必須の画面を開くと、元の URL を保持してログイン画面へ移動する', async () => {
    signedInAs(null)
    const { router } = renderApp('/projects?view=board')

    expect(await screen.findByRole('heading', { name: 'ログイン' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/login')
    expect(new URLSearchParams(router.state.location.search).get('next')).toBe('/projects?view=board')
  })

  it('未入力で送信すると項目ごとのメッセージを出し、API は呼ばない', async () => {
    signedInAs(null)
    let called = false
    server.use(http.post('*/api/v1/auth/login', () => {
      called = true
      return HttpResponse.json(f.loginResponse())
    }))
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: 'ログイン' }))

    expect(await screen.findByText(/メールアドレスを入力してください。/)).toBeTruthy()
    expect(screen.getByText(/パスワードを入力してください。/)).toBeTruthy()
    expect(screen.getByLabelText('メールアドレス').getAttribute('aria-invalid')).toBe('true')
    expect(called).toBe(false)
  })

  it('認証に失敗するとエラーを表示しパスワード欄を空にする', async () => {
    signedInAs(null)
    server.use(http.post('*/api/v1/auth/login', () => HttpResponse.json(
      f.problem(401, 'auth.invalid_credentials', 'メールアドレスまたはパスワードが正しくありません。'), { status: 401 })))
    const { user } = renderApp('/login')

    await user.type(await screen.findByLabelText('メールアドレス'), 'owner@example.test')
    await user.type(screen.getByLabelText('パスワード'), 'wrong-password')
    await user.click(screen.getByRole('button', { name: 'ログイン' }))

    expect((await screen.findByRole('alert')).textContent).toBe('メールアドレスまたはパスワードが正しくありません。')
    expect((screen.getByLabelText('パスワード') as HTMLInputElement).value).toBe('')
  })

  it('ロック中は理由と対処を表示する', async () => {
    signedInAs(null)
    server.use(http.post('*/api/v1/auth/login', () => HttpResponse.json(
      f.problem(401, 'auth.locked', 'ログインに続けて失敗したため、一時的にロックされています。', { detail: '15 分ほど待ってから再度お試しください。' }),
      { status: 401 })))
    const { user } = renderApp('/login')

    await user.type(await screen.findByLabelText('メールアドレス'), 'owner@example.test')
    await user.type(screen.getByLabelText('パスワード'), 'x')
    await user.click(screen.getByRole('button', { name: 'ログイン' }))

    expect((await screen.findByRole('alert')).textContent).toContain('15 分ほど待ってから再度お試しください。')
  })

  it('短時間に何度も試すと待つよう案内する', async () => {
    signedInAs(null)
    server.use(http.post('*/api/v1/auth/login', () => HttpResponse.json(
      f.problem(429, 'rate_limit.exceeded', '短時間にリクエストが集中しました。'), { status: 429 })))
    const { user } = renderApp('/login')

    await user.type(await screen.findByLabelText('メールアドレス'), 'owner@example.test')
    await user.type(screen.getByLabelText('パスワード'), 'x')
    await user.click(screen.getByRole('button', { name: 'ログイン' }))

    expect((await screen.findByRole('alert')).textContent).toBe('しばらく時間をおいてから再度お試しください。')
  })

  it('ログインに成功すると元の画面へ戻る', async () => {
    signedInAs(null)
    server.use(http.post('*/api/v1/auth/login', () => HttpResponse.json(f.loginResponse())))
    const { user, router } = renderApp('/login?next=%2Fprojects')

    await user.type(await screen.findByLabelText('メールアドレス'), 'owner@example.test')
    await user.type(screen.getByLabelText('パスワード'), 'Correct-Password-2026')
    await user.click(screen.getByRole('button', { name: 'ログイン' }))

    expect(await screen.findByRole('heading', { name: 'プロジェクト' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/projects')
  })

  it('外部サイトへの next は無視してホームへ移動する', async () => {
    signedInAs(null)
    server.use(http.post('*/api/v1/auth/login', () => HttpResponse.json(f.loginResponse())))
    const { user, router } = renderApp('/login?next=%2F%2Fevil.example.test')

    await user.type(await screen.findByLabelText('メールアドレス'), 'owner@example.test')
    await user.type(screen.getByLabelText('パスワード'), 'Correct-Password-2026')
    await user.click(screen.getByRole('button', { name: 'ログイン' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
  })
})

describe('パスワード変更（SCR-02）', () => {
  const tempUser = f.user({ mustChangePassword: true })

  it('一時パスワード状態ではどの画面を開いても変更画面へ移動する', async () => {
    signedInAs(tempUser)
    const { router } = renderApp('/projects/SALES/tasks')

    await waitFor(() => expect(router.state.location.pathname).toBe('/change-password'))
  })

  it('確認用が一致しない・短すぎる場合はその場で知らせる', async () => {
    signedInAs(tempUser)
    const { user } = renderApp('/change-password')

    await user.type(await screen.findByLabelText(/^現在のパスワード/), 'Temporary-Pass-01')
    await user.type(screen.getByLabelText(/^新しいパスワード\s*必須$/), 'short')
    await user.type(screen.getByLabelText(/^新しいパスワード（確認）/), 'different')
    await user.click(screen.getByRole('button', { name: 'パスワードを変更する' }))

    expect(await screen.findByText(/新しいパスワードは12文字以上で設定してください。/)).toBeTruthy()
    expect(screen.getByText(/新しいパスワードが一致しません。/)).toBeTruthy()
  })

  it('API の項目エラー（現在のパスワード違い）を該当欄に表示する', async () => {
    signedInAs(tempUser)
    server.use(http.post('*/api/v1/auth/password', () => HttpResponse.json(
      f.problem(400, 'auth.current_password_invalid', '現在のパスワードが正しくありません。', { errors: { currentPassword: ['現在のパスワードが正しくありません。'] } }),
      { status: 400 })))
    const { user } = renderApp('/change-password')

    await user.type(await screen.findByLabelText(/^現在のパスワード/), 'Wrong-Temporary-01')
    await user.type(screen.getByLabelText(/^新しいパスワード\s*必須$/), 'Kanagawa-Minato-2026')
    await user.type(screen.getByLabelText(/^新しいパスワード（確認）/), 'Kanagawa-Minato-2026')
    await user.click(screen.getByRole('button', { name: 'パスワードを変更する' }))

    expect(await screen.findByText(/現在のパスワードが正しくありません。/)).toBeTruthy()
    expect(screen.getByLabelText(/^現在のパスワード/).getAttribute('aria-invalid')).toBe('true')
  })

  it('変更に成功するとホームへ移動する', async () => {
    signedInAs(tempUser)
    server.use(http.post('*/api/v1/auth/password', () => HttpResponse.json(f.loginResponse(f.user()))))
    const { user, router } = renderApp('/change-password')

    await user.type(await screen.findByLabelText(/^現在のパスワード/), 'Temporary-Pass-01')
    await user.type(screen.getByLabelText(/^新しいパスワード\s*必須$/), 'Kanagawa-Minato-2026')
    await user.type(screen.getByLabelText(/^新しいパスワード（確認）/), 'Kanagawa-Minato-2026')
    await user.click(screen.getByRole('button', { name: 'パスワードを変更する' }))

    expect(await screen.findByRole('heading', { name: '自分のタスク' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/')
  })
})

describe('権限による画面の制御', () => {
  it('一般利用者が利用者管理を開くとホームへ戻す', async () => {
    signedInAs(f.user({ systemRole: 'member' }))
    const { router } = renderApp('/admin/users')

    await waitFor(() => expect(router.state.location.pathname).toBe('/'))
    expect(screen.queryByRole('link', { name: '利用者管理' })).toBeNull()
  })

  it('システム管理者にはメニューに利用者管理を表示する', async () => {
    signedInAs(f.user({ systemRole: 'administrator' }))
    renderApp('/')

    expect(await screen.findByRole('link', { name: '利用者管理' })).toBeTruthy()
  })

  it('存在しない・所属していないプロジェクトは「見つかりません」と表示する', async () => {
    renderApp('/projects/OTHER/tasks')

    expect(await screen.findByRole('heading', { name: 'ページが見つかりません' })).toBeTruthy()
  })
})
