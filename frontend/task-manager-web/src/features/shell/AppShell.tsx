import { useState } from 'react'
import { Button as AriaButton, Menu, MenuItem, MenuTrigger, Popover } from 'react-aria-components'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router'
import { useSidebarProjects, useUnreadCount } from '../../api/queries'
import { Button } from '../../components/Button'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { NEW_TASK_EVENT, useKeyboardShortcuts } from '../../hooks/useKeyboardShortcuts'
import { useAuth } from '../auth/AuthProvider'
import { NoticeBell } from './NoticeBell'
import { TaskSearchBox } from './TaskSearchBox'
import styles from './AppShell.module.css'

const SIDEBAR_KEY = 'tm.sidebarCollapsed'

function readCollapsed(): boolean {
  try {
    return localStorage.getItem(SIDEBAR_KEY) === '1'
  } catch {
    return false
  }
}

/** 画面の外枠（ui-design.md §4）: ヘッダー + サイドナビ + 本体 */
export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  // ドロワーは開いた画面でのみ開いた状態にする（画面を移動したら自動で閉じる）
  const [drawerPath, setDrawerPath] = useState<string | null>(null)
  const drawerOpen = drawerPath === location.pathname
  const setDrawerOpen = (open: boolean) => setDrawerPath(open ? location.pathname : null)
  const [collapsed, setCollapsed] = useState(readCollapsed)
  const [shortcutsOpen, setShortcutsOpen] = useState(false)
  const unread = useUnreadCount(user !== null)

  const toggleCollapsed = () => {
    setCollapsed((v) => {
      try {
        localStorage.setItem(SIDEBAR_KEY, v ? '0' : '1')
      } catch {
        // 保存できなくても表示は切り替える
      }
      return !v
    })
  }

  useKeyboardShortcuts({
    '/': () => document.getElementById('tm-task-search')?.focus(),
    n: () => window.dispatchEvent(new Event(NEW_TASK_EVENT)),
    'g h': () => void navigate('/'),
    '?': () => setShortcutsOpen(true),
  })

  const unreadCount = unread.data?.count ?? 0
  return (
    <div className={styles.shell} data-collapsed={collapsed || undefined}>
      <a href="#main" className={styles.skip}>本文へ移動</a>
      <header className={styles.header}>
        <Button variant="ghost" iconOnly aria-label="メニューを開く" className={styles.menuButton} onClick={() => setDrawerOpen(true)}>
          <Icon name="menu" />
        </Button>
        <Button variant="ghost" iconOnly aria-label={collapsed ? 'サイドナビを広げる' : 'サイドナビをたたむ'} aria-pressed={collapsed}
          className={styles.collapseButton} onClick={toggleCollapsed}>
          <Icon name="menu" />
        </Button>
        <NavLink to="/" className={styles.brand}>TaskManager</NavLink>
        <div className={styles.search}><TaskSearchBox /></div>
        <NoticeBell count={unreadCount} />
        <MenuTrigger>
          <AriaButton className="tm-btn tm-btn--ghost" aria-label={`利用者メニュー（${user?.displayName ?? ''}）`}>
            <span className="tm-avatar" aria-hidden="true">{user?.displayName.slice(0, 1)}</span>
            <span className={styles.userName}>{user?.displayName}</span>
          </AriaButton>
          <Popover className="tm-popover" placement="bottom end">
            <Menu className="tm-menu" onAction={(key) => {
              if (key === 'settings') void navigate('/settings')
              if (key === 'shortcuts') setShortcutsOpen(true)
              if (key === 'logout') void logout().then(() => navigate('/login'))
            }}>
              <MenuItem id="settings" className="tm-menu-item">個人設定</MenuItem>
              <MenuItem id="shortcuts" className="tm-menu-item">キーボードショートカット</MenuItem>
              <MenuItem id="logout" className="tm-menu-item">ログアウト</MenuItem>
            </Menu>
          </Popover>
        </MenuTrigger>
      </header>
      {drawerOpen && <div className={styles.backdrop} onClick={() => setDrawerOpen(false)} aria-hidden="true" />}
      <nav className={styles.sidebar} data-open={drawerOpen || undefined} aria-label="メインメニュー">
        <div className={styles.drawerHeader}>
          <span>メニュー</span>
          <Button variant="ghost" iconOnly aria-label="メニューを閉じる" onClick={() => setDrawerOpen(false)}><Icon name="close" /></Button>
        </div>
        <SidebarLinks unreadCount={unreadCount} isAdmin={user?.systemRole === 'administrator'} collapsed={collapsed} />
      </nav>
      <main id="main" className={styles.main} tabIndex={-1}>
        <Outlet />
      </main>
      <Dialog isOpen={shortcutsOpen} onOpenChange={setShortcutsOpen} title="キーボードショートカット">
        <table className="tm-table">
          <tbody>
            <tr><th scope="row"><kbd>/</kbd></th><td>タスクの検索欄に移動</td></tr>
            <tr><th scope="row"><kbd>N</kbd></th><td>タスクを追加（プロジェクトのタスク一覧で）</td></tr>
            <tr><th scope="row"><kbd>G</kbd> → <kbd>H</kbd></th><td>ホーム（自分のタスク）へ移動</td></tr>
            <tr><th scope="row"><kbd>Esc</kbd></th><td>ダイアログ・メニューを閉じる</td></tr>
            <tr><th scope="row"><kbd>?</kbd></th><td>この一覧を表示</td></tr>
          </tbody>
        </table>
        <p className="tm-small tm-muted">入力欄に文字を入力している間は使えません。どの操作も画面のボタンから同じことができます。</p>
        <div className="tm-dialog__footer"><Button onClick={() => setShortcutsOpen(false)}>閉じる</Button></div>
      </Dialog>
    </div>
  )
}

function SidebarLinks({ unreadCount, isAdmin, collapsed }: { unreadCount: number; isAdmin: boolean; collapsed: boolean }) {
  const projects = useSidebarProjects()
  const items = projects.data?.items ?? []
  const visible = items.slice(0, 10)
  const linkClass = ({ isActive }: { isActive: boolean }) => (isActive ? `${styles.navLink} ${styles.active}` : styles.navLink)
  return (
    <>
      <ul className={styles.navList}>
        <li><NavLink to="/" end className={linkClass} title="自分のタスク"><Icon name="home" /><span className={styles.label}>自分のタスク</span></NavLink></li>
        <li>
          <NavLink to="/notices" className={linkClass} title="お知らせ">
            <Icon name="bell" /><span className={styles.label}>お知らせ</span>
            {unreadCount > 0 && <span className={styles.count} aria-label={`未読 ${unreadCount} 件`}>{unreadCount > 99 ? '99+' : unreadCount}</span>}
          </NavLink>
        </li>
      </ul>
      {!collapsed && <p className={styles.navHeading}>プロジェクト</p>}
      <ul className={styles.navList}>
        {visible.map((p) => (
          <li key={p.key}>
            <NavLink to={`/projects/${p.key}/tasks`} className={linkClass} title={p.name}>
              <span className={styles.projectKey} aria-hidden={collapsed || undefined}>{collapsed ? p.key.slice(0, 2) : ''}</span>
              <span className={styles.label}>{p.name}</span>
            </NavLink>
          </li>
        ))}
        <li><NavLink to="/projects" end className={linkClass} title="すべてのプロジェクト"><Icon name="folder" /><span className={styles.label}>{items.length > 10 ? 'すべて表示' : 'プロジェクト一覧'}</span></NavLink></li>
      </ul>
      {isAdmin && (
        <ul className={styles.navList}>
          <li><NavLink to="/admin/users" className={linkClass} title="利用者管理"><Icon name="users" /><span className={styles.label}>利用者管理</span></NavLink></li>
        </ul>
      )}
    </>
  )
}
