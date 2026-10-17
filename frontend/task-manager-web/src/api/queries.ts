import { useQuery } from '@tanstack/react-query'
import { apiClient, call } from './client'

/** TanStack Query のキー（ui-design.md §7.2） */
export const queryKeys = {
  me: ['me'] as const,
  projects: (params: object) => ['projects', params] as const,
  sidebarProjects: ['projects', 'sidebar'] as const,
  project: (key: string) => ['project', key] as const,
  statuses: (key: string) => ['statuses', key] as const,
  tags: (key: string) => ['tags', key] as const,
  members: (key: string) => ['members', key] as const,
  milestones: (key: string) => ['milestones', key] as const,
  milestone: (key: string, id: string) => ['milestone', key, id] as const,
  tasks: (key: string, params?: object): readonly unknown[] => (params === undefined ? ['tasks', key] : ['tasks', key, params]),
  task: (key: string, number: number) => ['task', key, number] as const,
  comments: (key: string, number: number) => ['comments', key, number] as const,
  attachments: (key: string, number: number) => ['attachments', key, number] as const,
  history: (key: string, number: number) => ['history', key, number] as const,
  myTasks: (includeCompleted: boolean) => ['myTasks', includeCompleted] as const,
  notices: (unreadOnly: boolean, page: number) => ['notices', unreadOnly, page] as const,
  unreadCount: ['notices', 'unreadCount'] as const,
  users: (params: object) => ['users', params] as const,
  // Phase 9: タスクの変更時に既存の無効化（['tasks', key]・['myTasks']）に含まれるキーにする
  taskSchedule: (key: string, params: object) => ['tasks', key, 'schedule', params] as const,
  taskStatusSummary: (key: string) => ['tasks', key, 'status-summary'] as const,
  myTaskStatusSummary: (projectKey: string | null) => ['myTasks', 'status-summary', projectKey ?? 'all'] as const,
}

export function useProject(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.project(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}', { params: { path: { projectKey } } })),
  })
}

export function useStatuses(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.statuses(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/statuses', { params: { path: { projectKey } } })),
    staleTime: 60_000,
  })
}

export function useTags(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.tags(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/tags', { params: { path: { projectKey } } })),
    staleTime: 60_000,
  })
}

export function useMembers(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.members(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/members', { params: { path: { projectKey } } })),
    staleTime: 60_000,
  })
}

export function useMilestones(projectKey: string) {
  return useQuery({
    queryKey: queryKeys.milestones(projectKey),
    queryFn: () => call(() => apiClient.GET('/api/v1/projects/{projectKey}/milestones', { params: { path: { projectKey } } })),
    staleTime: 30_000,
  })
}

export function useSidebarProjects() {
  return useQuery({
    queryKey: queryKeys.sidebarProjects,
    queryFn: () => call(() => apiClient.GET('/api/v1/projects', { params: { query: { pageSize: 100 } } })),
    staleTime: 60_000,
  })
}

/** お知らせの未読件数（60 秒ごと + 画面復帰時。requirements.md REQ-NTC-01） */
export function useUnreadCount(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.unreadCount,
    queryFn: () => call(() => apiClient.GET('/api/v1/me/notices/unread-count')),
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
    enabled,
  })
}
