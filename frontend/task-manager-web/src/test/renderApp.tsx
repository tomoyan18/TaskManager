// 実際のルート定義・認証・トースト・TanStack Query を組み合わせて、指定した URL の画面を描画する
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '../app/routes'
import { ToastProvider } from '../components/Toast'
import { AuthProvider } from '../features/auth/AuthProvider'

export function renderApp(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const user = userEvent.setup()
  const result = render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider onNavigate={(to) => void router.navigate(to)}>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  )
  return { ...result, router, user, queryClient }
}
