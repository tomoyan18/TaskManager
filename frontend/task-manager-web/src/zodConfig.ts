import { z } from 'zod'

// zod の高速化（JIT）は、スキーマの作成時に eval が使えるかを new Function で試すため、
// CSP（script-src 'self'）の違反として記録される。eval を使わない方式に固定する（Phase 8）。
// 各画面のスキーマより先に評価されるよう、main.tsx の最初で読み込む
z.config({ jitless: true })
