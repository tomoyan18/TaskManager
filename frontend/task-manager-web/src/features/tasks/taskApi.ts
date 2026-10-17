import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient, ApiError, call, type Schemas } from '../../api/client'
import { queryKeys } from '../../api/queries'
import { useToast } from '../../components/Toast'

export type TaskSummary = Schemas['TaskSummaryResponse']
export type TaskDetail = Schemas['TaskDetailResponse']
export type UpdateTaskBody = Schemas['UpdateTaskRequest']

/** タスクの変更後に関連する一覧・詳細・自分のタスクを最新にする */
export function invalidateTaskQueries(queryClient: ReturnType<typeof useQueryClient>, projectKey: string, number?: number) {
  void queryClient.invalidateQueries({ queryKey: queryKeys.tasks(projectKey) })
  void queryClient.invalidateQueries({ queryKey: ['myTasks'] })
  void queryClient.invalidateQueries({ queryKey: queryKeys.project(projectKey) })
  void queryClient.invalidateQueries({ queryKey: queryKeys.milestones(projectKey) })
  void queryClient.invalidateQueries({ queryKey: queryKeys.statuses(projectKey) })
  if (number !== undefined) {
    void queryClient.invalidateQueries({ queryKey: queryKeys.task(projectKey, number) })
    void queryClient.invalidateQueries({ queryKey: queryKeys.history(projectKey, number) })
  }
}

/** API のエラーを利用者向けの文に変換する */
export function errorMessage(error: unknown, fallback: string): { title: string; detail?: string } {
  if (error instanceof ApiError) {
    const firstField = Object.values(error.fieldErrors)[0]?.[0]
    if (firstField) return { title: firstField }
    if (error.status === 0 || error.status >= 500) return { title: fallback, detail: '時間をおいて再度お試しください。' }
    return { title: error.title, detail: error.detail ?? undefined }
  }
  return { title: fallback }
}

/**
 * タスクの属性変更（ステータス・担当者など）。version を付けて送り、
 * 競合（409）の場合は最新を読み込み直して知らせる（ui-design.md §7.3）。
 */
export function useUpdateTask(projectKey: string) {
  const queryClient = useQueryClient()
  const toast = useToast()
  return useMutation({
    mutationFn: ({ number, body }: { number: number; body: UpdateTaskBody }) =>
      call(() => apiClient.PATCH('/api/v1/projects/{projectKey}/tasks/{taskNumber}', {
        params: { path: { projectKey, taskNumber: number } },
        body,
      })),
    onSuccess: (task) => {
      queryClient.setQueryData(queryKeys.task(projectKey, task.number), task)
      invalidateTaskQueries(queryClient, projectKey, task.number)
    },
    onError: (error, { number }) => {
      if (error instanceof ApiError && error.code === 'concurrency.conflict') {
        toast.error('他の人が先に更新しました。最新の内容を表示しています。')
      } else {
        const m = errorMessage(error, '変更を保存できませんでした。')
        toast.error(m.title, m.detail)
      }
      invalidateTaskQueries(queryClient, projectKey, number)
    },
  })
}

export function useDeleteTask(projectKey: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ number, includeSubtasks }: { number: number; includeSubtasks: boolean }) =>
      call(() => apiClient.DELETE('/api/v1/projects/{projectKey}/tasks/{taskNumber}', {
        params: { path: { projectKey, taskNumber: number }, query: { includeSubtasks } },
      })),
    onSuccess: (_, { number }) => {
      queryClient.removeQueries({ queryKey: queryKeys.task(projectKey, number) })
      invalidateTaskQueries(queryClient, projectKey)
    },
  })
}
