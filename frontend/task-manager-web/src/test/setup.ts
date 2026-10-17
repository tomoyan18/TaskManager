// テストごとの後片付け（docs/testing/test-design.md §5）。MSW の開始は environment.ts で行う
import { cleanup, configure } from '@testing-library/react'
import { afterAll, afterEach } from 'vitest'
import { tokenStore } from '../api/client'
import { server } from './server'

// 画面は遅延読み込み（lazy）のため、初回の表示に 1 秒（既定）を超えることがある。待ち時間だけを延ばす（Phase 6 で調整）
configure({ asyncUtilTimeout: 5000 })

afterEach(() => {
  cleanup()
  server.resetHandlers()
  tokenStore.set(null)
  window.history.replaceState(null, '', '/')
})

afterAll(() => server.close())
