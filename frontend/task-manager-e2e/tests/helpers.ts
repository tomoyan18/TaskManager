import AxeBuilder from '@axe-core/playwright'
import { expect, test, type Page } from '@playwright/test'

export function requiredEnv(name: string): string {
  const value = process.env[name]
  if (value === undefined || value === '') {
    throw new Error(`環境変数 ${name} を設定してください（playwright.config.ts の説明を参照）。`)
  }
  return value
}

export async function login(page: Page, email: string, password: string) {
  for (let attempt = 0; attempt < 2; attempt++) {
    await page.goto('/login')
    await page.getByLabel('メールアドレス').fill(email)
    await page.getByLabel('パスワード', { exact: true }).fill(password)
    await page.getByRole('button', { name: 'ログイン' }).click()
    // ログインの完了（ログイン画面から移動したこと）を待つ。待たずに URL を開くとセッションがない状態で読み込まれる
    const rateLimited = page.getByRole('alert').filter({ hasText: 'しばらく時間をおいてから再度お試しください。' })
    const result = await Promise.race([
      page.waitForURL((url) => !url.pathname.startsWith('/login')).then(() => 'signed-in', () => 'timeout'),
      rateLimited.waitFor().then(() => 'rate-limited', () => 'timeout'),
    ])
    if (result === 'signed-in') return
    if (result !== 'rate-limited') break
    // シナリオ全体で同じ送信元から 10 回を超えてログインするため、ログインの回数制限（IP あたり 10 回/分。security-design.md S-13）に
    // 達することがある。制限は緩めず、1 分の区切りが過ぎるまで待ってから 1 回だけやり直す（Phase 9 で追加）
    test.info().setTimeout(test.info().timeout + 75_000)
    await page.waitForTimeout(61_000)
  }
  await expect(page).not.toHaveURL(/\/login/)
}

/** 一時パスワードでログインした直後のパスワード変更（SCR-02） */
export async function changeTemporaryPassword(page: Page, current: string, next: string) {
  await page.waitForURL('**/change-password')
  await page.getByLabel(/^現在のパスワード/).fill(current)
  await page.getByLabel(/^新しいパスワード\s*必須$/).fill(next)
  await page.getByLabel(/^新しいパスワード（確認）/).fill(next)
  await page.getByRole('button', { name: 'パスワードを変更する' }).click()
  await expect(page.getByRole('heading', { name: '自分のタスク' })).toBeVisible()
}

export async function logout(page: Page) {
  await page.getByRole('button', { name: /^利用者メニュー/ }).click()
  await page.getByRole('menuitem', { name: 'ログアウト' }).click()
  await expect(page.getByRole('heading', { name: 'ログイン' })).toBeVisible()
}

/** アクセシビリティの自動検査（重大・深刻な違反がないこと。testing-strategy.md E2E-08） */
export async function expectNoSeriousA11yViolations(page: Page, context: string) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze()
  const serious = results.violations.filter((v) => v.impact === 'serious' || v.impact === 'critical')
  expect(serious.map((v) => `${v.id}: ${v.help}（${v.nodes.length} 件）`), context).toEqual([])
}
