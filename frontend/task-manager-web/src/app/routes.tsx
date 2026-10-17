import { Navigate, Outlet, type RouteObject } from 'react-router'
import { ForcedPasswordChangePage } from '../features/auth/ForcedPasswordChangePage'
import { LoginPage } from '../features/auth/LoginPage'
import { RequireAuth, RequireSystemAdmin } from '../features/auth/RouteGuards'
import { ProjectLayout } from '../features/projects/ProjectLayout'
import { AppShell } from '../features/shell/AppShell'
import { NotFoundPage } from '../features/shell/NotFoundPage'
import { RouteErrorPage } from './RouteErrorPage'

/** 画面単位で読み込む（初回表示を軽くする） */
const page = (load: () => Promise<{ default: React.ComponentType }>) => async () => ({ Component: (await load()).default })

/** ルーティング（ui-design.md §7.1）。テストではメモリ上のルーターで同じ定義を使う */
export const routes: RouteObject[] = [
  {
    errorElement: <RouteErrorPage />,
    children: [
      { path: '/login', element: <LoginPage /> },
      { path: '/change-password', element: <ForcedPasswordChangePage /> },
      {
        element: <RequireAuth><AppShell /></RequireAuth>,
        children: [
          { index: true, lazy: page(() => import('../features/my-tasks/MyTasksPage').then((m) => ({ default: m.MyTasksPage }))) },
          { path: 'projects', lazy: page(() => import('../features/projects/ProjectListPage').then((m) => ({ default: m.ProjectListPage }))) },
          {
            path: 'projects/:projectKey',
            element: <ProjectLayout />,
            children: [
              { index: true, element: <Navigate to="tasks" replace /> },
              { path: 'tasks', lazy: page(() => import('../features/tasks/ProjectTasksPage').then((m) => ({ default: m.ProjectTasksPage }))) },
              { path: 'tasks/:taskNumber', lazy: page(() => import('../features/tasks/TaskDetailPage').then((m) => ({ default: m.TaskDetailPage }))) },
              { path: 'milestones', lazy: page(() => import('../features/milestones/MilestonePages').then((m) => ({ default: m.MilestoneListPage }))) },
              { path: 'milestones/:milestoneId', lazy: page(() => import('../features/milestones/MilestonePages').then((m) => ({ default: m.MilestoneDetailPage }))) },
              {
                path: 'settings',
                lazy: page(() => import('../features/projects/ProjectSettingsLayout').then((m) => ({ default: m.ProjectSettingsLayout }))),
                children: [
                  { index: true, lazy: page(() => import('../features/projects/ProjectGeneralSettingsPage').then((m) => ({ default: m.ProjectGeneralSettingsPage }))) },
                  { path: 'members', lazy: page(() => import('../features/projects/ProjectMembersPage').then((m) => ({ default: m.ProjectMembersPage }))) },
                  { path: 'workflow', lazy: page(() => import('../features/projects/ProjectWorkflowPage').then((m) => ({ default: m.ProjectWorkflowPage }))) },
                ],
              },
            ],
          },
          { path: 'notices', lazy: page(() => import('../features/notices/NoticesPage').then((m) => ({ default: m.NoticesPage }))) },
          { path: 'settings', lazy: page(() => import('../features/settings/PersonalSettingsPage').then((m) => ({ default: m.PersonalSettingsPage }))) },
          { path: 'admin/users', element: <RequireSystemAdmin><Outlet /></RequireSystemAdmin>, children: [{ index: true, lazy: page(() => import('../features/admin-users/UserAdministrationPage').then((m) => ({ default: m.UserAdministrationPage }))) }] },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
      { path: '*', element: <Outlet /> },
    ],
  },
]
