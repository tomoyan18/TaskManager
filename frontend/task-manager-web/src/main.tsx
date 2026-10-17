// 最初に読み込む（各画面のスキーマが作られる前に zod の設定を行う）
import './zodConfig'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { ApiError } from './api/client'
import { router } from './app/router'
import { ToastProvider } from './components/Toast'
import { AuthProvider } from './features/auth/AuthProvider'
import './styles/global.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      // 4xx（権限なし・存在しない）は再試行しない
      retry: (failureCount, error) => !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 2,
    },
  },
})

const rootElement = document.getElementById('root')
if (rootElement === null) {
  throw new Error('root element not found')
}

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider onNavigate={(to) => void router.navigate(to)}>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
)
