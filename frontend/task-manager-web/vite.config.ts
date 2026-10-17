import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// 開発時は API（dotnet run, :5080）へ同一オリジンとしてプロキシする（docs/design/architecture.md §6）
// Host を書き換えない（changeOrigin: false）。API は refresh / logout で Origin と Host を比較するため
const apiTarget = { target: process.env.TM_API_ORIGIN ?? 'http://localhost:5080', changeOrigin: false }

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiTarget,
      '/openapi': apiTarget,
      '/swagger': apiTarget,
      '/health': apiTarget,
    },
  },
  build: {
    sourcemap: false,
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['./src/test/environment.ts', './src/test/setup.ts'],
    // 画面の日付表示を固定するため、テストは東京時間で動かす
    env: { TZ: 'Asia/Tokyo' },
  },
})
