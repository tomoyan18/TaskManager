import { defineConfig, devices } from '@playwright/test'

/**
 * E2E テスト（docs/design/testing-strategy.md §9、docs/testing/test-design.md §6）。
 * 前提: 空の DB に初期システム管理者だけがいる状態で、Web（既定 http://localhost:5173）と API が起動していること。
 * 接続先と初期管理者は環境変数で渡す（値をリポジトリに書かない）:
 *   TM_E2E_BASE_URL       画面の URL（Docker 構成なら http://localhost:8080）
 *   TM_E2E_ADMIN_EMAIL    初期システム管理者のメールアドレス（TM_BOOTSTRAP_ADMIN_EMAIL と同じ）
 *   TM_E2E_ADMIN_PASSWORD 初期システム管理者の一時パスワード（TM_BOOTSTRAP_ADMIN_PASSWORD と同じ）
 */
export default defineConfig({
  testDir: './tests',
  // 主要フローは前のシナリオの結果（利用者・プロジェクト）を使うため、1 つずつ順に実行する
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.TM_E2E_BASE_URL ?? 'http://localhost:5173',
    locale: 'ja-JP',
    timezoneId: 'Asia/Tokyo',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } }],
})
