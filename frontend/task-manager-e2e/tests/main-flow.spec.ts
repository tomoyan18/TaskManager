import { expect, test } from '@playwright/test'
import { changeTemporaryPassword, expectNoSeriousA11yViolations, login, logout, requiredEnv } from './helpers'

/**
 * 主要な利用の流れ（testing-strategy.md §9 E2E-01〜08）。前のシナリオで作った利用者・プロジェクトを次で使うため、順に実行する。
 * 利用者名・プロジェクト名はすべて架空。パスワードは実行ごとに生成し、どこにも保存しない。
 */
test.describe.configure({ mode: 'serial' })

const run = Date.now().toString().slice(-6)
// パスワードにはメールアドレス・表示名を含めない（パスワードポリシーで拒否されるため）
const admin = { email: '', password: `Sora-Kawa-${run}-Ichi` }
const editor = { email: `editor-${run}@example.test`, name: `佐藤 花子${run}`, temp: '', password: `Umi-Yama-${run}-Ni` }
const viewer = { email: `viewer-${run}@example.test`, name: `鈴木 一郎${run}`, temp: '', password: `Hoshi-Tsuki-${run}-San` }
const projectKey = `E${run}`
const projectName = `E2E 営業支援 ${run}`
let taskNumber = 0

test.beforeAll(() => {
  admin.email = requiredEnv('TM_E2E_ADMIN_EMAIL')
})

async function createUser(page: import('@playwright/test').Page, email: string, name: string): Promise<string> {
  await page.getByRole('button', { name: '利用者を追加' }).click()
  const dialog = page.getByRole('dialog', { name: '利用者を追加' })
  await dialog.getByLabel(/^メールアドレス/).fill(email)
  await dialog.getByLabel(/^表示名/).fill(name)
  await dialog.getByRole('button', { name: '追加する' }).click()
  const passwordDialog = page.getByRole('alertdialog', { name: `${name}さんの一時パスワード` })
  const temp = (await passwordDialog.locator('code').textContent())?.trim() ?? ''
  expect(temp.length).toBeGreaterThanOrEqual(12)
  await passwordDialog.getByLabel('利用者に伝えました').check()
  await passwordDialog.getByRole('button', { name: '閉じる' }).click()
  return temp
}

test('E2E-01 初期管理者のパスワード変更と利用者の発行、一時パスワードでの初回ログイン', async ({ page }) => {
  await login(page, admin.email, requiredEnv('TM_E2E_ADMIN_PASSWORD'))
  await changeTemporaryPassword(page, requiredEnv('TM_E2E_ADMIN_PASSWORD'), admin.password)

  await page.getByRole('link', { name: '利用者管理' }).click()
  editor.temp = await createUser(page, editor.email, editor.name)
  viewer.temp = await createUser(page, viewer.email, viewer.name)
  await expect(page.getByText(editor.email)).toBeVisible()
  await expectNoSeriousA11yViolations(page, '利用者管理')
  await logout(page)

  await login(page, editor.email, editor.temp)
  await changeTemporaryPassword(page, editor.temp, editor.password)
  await expect(page.getByText('担当しているタスクはありません')).toBeVisible()
})

test('E2E-02 プロジェクト作成・メンバー追加・タスク作成・担当者と期限の設定・担当者のホームに表示', async ({ page }) => {
  await login(page, admin.email, admin.password)
  await page.getByRole('link', { name: 'プロジェクト一覧' }).click()
  await page.getByRole('button', { name: 'プロジェクトを作成' }).first().click()
  await page.getByLabel(/^プロジェクト名/).fill(projectName)
  await page.getByLabel(/^プロジェクトキー/).fill(projectKey.toLowerCase())
  await page.getByRole('button', { name: '作成する' }).click()
  await page.waitForURL(`**/projects/${projectKey}/tasks`)

  for (const [member, role] of [[editor, '編集者'], [viewer, '閲覧者']] as const) {
    await page.goto(`/projects/${projectKey}/settings/members`)
    await page.getByRole('button', { name: 'メンバーを追加' }).click()
    const dialog = page.getByRole('dialog', { name: 'メンバーを追加' })
    await dialog.getByLabel('利用者（名前・メールで検索）').fill(member.name.slice(0, 4))
    await page.getByRole('option', { name: new RegExp(member.name) }).click()
    const roleSelect = dialog.getByLabel('ロール')
    const value = await roleSelect.locator('option', { hasText: role }).getAttribute('value')
    await roleSelect.selectOption(value ?? '')
    await dialog.getByRole('button', { name: '追加する' }).click()
    await expect(page.getByRole('table').getByText(member.name)).toBeVisible()
  }

  await page.goto(`/projects/${projectKey}/tasks`)
  await page.getByRole('button', { name: 'タスクを追加' }).first().click()
  await page.getByRole('dialog').getByLabel(/^タイトル/).fill('見積書の作成')
  await page.getByRole('dialog').getByRole('button', { name: '追加する' }).click()
  await expect(page.getByText(/を追加しました/)).toBeVisible()
  await page.getByRole('link', { name: '見積書の作成' }).click()
  await page.waitForURL(/\/tasks\/\d+$/)
  taskNumber = Number(page.url().split('/').pop())
  expect(taskNumber).toBeGreaterThan(0)

  // 保存の完了（「保存しました」の表示）を待ってから次の操作をする
  const savedMark = page.getByRole('status').filter({ hasText: '保存しました' })
  await page.getByLabel('担当者', { exact: true }).selectOption({ label: editor.name })
  await expect(savedMark).toBeVisible()
  await page.getByLabel('期限', { exact: true }).fill('2026-12-25')
  await expect(page.getByText('12/25（金）').or(page.getByText(/2026\/12\/25/)).first()).toBeVisible()
  await page.getByRole('tab', { name: '変更履歴' }).click()
  await expect(page.getByRole('tabpanel').getByText(editor.name).first()).toBeVisible()
  await logout(page)

  await login(page, editor.email, editor.password)
  await expect(page.getByRole('link', { name: '見積書の作成' })).toBeVisible()
})

test('E2E-03 ボードでのステータス変更（マウス）とキーボード操作', async ({ page }) => {
  await login(page, editor.email, editor.password)
  await page.goto(`/projects/${projectKey}/tasks?view=board`)
  const notStarted = page.getByRole('region', { name: /^未着手（/ })
  await notStarted.getByRole('combobox').first().selectOption({ label: '対応中' })
  await expect(page.getByRole('region', { name: /^対応中（1 件）/ })).toBeVisible()

  // キーボード: N でタスク追加、Esc で閉じる
  await page.getByRole('button', { name: '表' }).click()
  await page.locator('body').press('n')
  await expect(page.getByRole('dialog', { name: 'タスクを追加' })).toBeVisible()
  await page.keyboard.press('Escape')
  await expect(page.getByRole('dialog')).toHaveCount(0)

  // 表のステータスはキーボードでフォーカスして変更できる
  const status = page.getByRole('combobox', { name: `${projectKey}-${taskNumber} のステータス` })
  await status.focus()
  await expect(status).toBeFocused()
  await status.selectOption({ label: '確認待ち' })
  await expect(status).toHaveValue(await status.locator('option', { hasText: '確認待ち' }).getAttribute('value') ?? '')
})

test('E2E-04 コメントの投稿で作成者にお知らせが届き、既読にできる', async ({ page }) => {
  await login(page, editor.email, editor.password)
  await page.goto(`/projects/${projectKey}/tasks/${taskNumber}`)
  await page.getByLabel('コメントを書く').fill('単価表を **共有** します。<script>window.__xss = 1</script>')
  await page.getByRole('button', { name: '投稿する' }).click()
  await expect(page.locator('.tm-markdown strong', { hasText: '共有' })).toBeVisible()
  expect(await page.evaluate(() => (window as unknown as { __xss?: number }).__xss)).toBeUndefined()
  await logout(page)

  await login(page, admin.email, admin.password)
  const noticesLink = page.getByRole('link', { name: /^お知らせ 未読 \d+ 件/ })
  await expect(noticesLink).toBeVisible()
  await noticesLink.click()
  await expect(page.getByText(/にコメントしました/)).toBeVisible()
  await page.getByRole('button', { name: 'すべて既読にする' }).click()
  await expect(page.getByRole('link', { name: /^お知らせ 未読/ })).toHaveCount(0)
  await expectNoSeriousA11yViolations(page, 'お知らせ')
})

test('E2E-05 添付ファイルの追加・ダウンロード・削除', async ({ page }) => {
  await login(page, editor.email, editor.password)
  await page.goto(`/projects/${projectKey}/tasks/${taskNumber}`)
  await page.locator('input[type="file"]').setInputFiles({ name: '単価表.csv', mimeType: 'text/csv', buffer: Buffer.from('品目,単価\n部品A,100\n') })
  await expect(page.getByText('ファイルを添付しました')).toBeVisible()
  await expect(page.getByText('添付ファイル（1）')).toBeVisible()

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '単価表.csv', exact: true }).click()
  const download = await downloadPromise
  expect(download.suggestedFilename()).toBe('単価表.csv')

  // 許可されていない形式は拒否する
  await page.locator('input[type="file"]').setInputFiles({ name: 'page.html', mimeType: 'text/html', buffer: Buffer.from('<html></html>') })
  await expect(page.getByText(/この種類のファイルは添付できません/)).toBeVisible()

  await page.getByRole('button', { name: '単価表.csv を削除' }).click()
  await page.getByRole('alertdialog').getByRole('button', { name: /削除/ }).click()
  await expect(page.getByText('添付ファイル（0）')).toBeVisible()
})

test('E2E-06 閲覧者には編集の部品を表示せず、所属していないプロジェクトは見つからない', async ({ page }) => {
  await login(page, viewer.email, viewer.temp)
  await changeTemporaryPassword(page, viewer.temp, viewer.password)
  await page.goto(`/projects/${projectKey}/tasks`)
  await expect(page.getByRole('link', { name: '見積書の作成' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'タスクを追加' })).toHaveCount(0)
  await expect(page.getByRole('combobox', { name: `${projectKey}-${taskNumber} のステータス` })).toHaveCount(0)

  await page.goto(`/projects/${projectKey}/tasks/${taskNumber}`)
  await expect(page.getByLabel('コメントを書く')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'ファイルを添付' })).toHaveCount(0)

  await page.goto('/projects/ZZNOTEXIST/tasks')
  await expect(page.getByRole('heading', { name: 'ページが見つかりません' })).toBeVisible()
  await page.goto('/admin/users')
  await expect(page.getByRole('heading', { name: '自分のタスク' })).toBeVisible()
})

test('E2E-07 スマートフォン幅（390×844）での一覧・メニュー・詳細', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await login(page, editor.email, editor.password)
  await page.goto(`/projects/${projectKey}/tasks`)
  await expect(page.getByRole('list', { name: 'タスクの一覧' }).getByRole('link', { name: '見積書の作成' })).toBeVisible()
  const hasHorizontalScroll = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)
  expect(hasHorizontalScroll).toBe(false)

  await page.getByRole('button', { name: 'メニューを開く' }).click()
  await expect(page.getByRole('navigation', { name: 'メインメニュー' }).getByRole('link', { name: '自分のタスク' })).toBeVisible()
  await page.getByRole('button', { name: 'メニューを閉じる' }).click()

  await page.goto(`/projects/${projectKey}/tasks/${taskNumber}`)
  await expect(page.getByRole('heading', { name: '見積書の作成' })).toBeVisible()
  await expectNoSeriousA11yViolations(page, 'スマートフォン幅のタスク詳細')
})

test('E2E-08 主要画面のアクセシビリティ自動検査と CSP 違反がないことの確認', async ({ page }) => {
  // CSP（Docker 構成の nginx が付ける）の違反を記録する。開発サーバーには CSP がないため常に 0 件になる
  await page.addInitScript(() => {
    const w = window as unknown as { __csp: string[] }
    w.__csp = []
    document.addEventListener('securitypolicyviolation', (e) => w.__csp.push(`${e.violatedDirective} ${e.sourceFile}`))
  })
  const cspViolations: string[] = []
  const collectCsp = async () => {
    cspViolations.push(...await page.evaluate(() => (window as unknown as { __csp: string[] }).__csp.splice(0)))
  }
  await page.goto('/login')
  await expectNoSeriousA11yViolations(page, 'ログイン')
  await collectCsp()
  await login(page, editor.email, editor.password)
  await expect(page.getByRole('heading', { name: '自分のタスク' })).toBeVisible()
  await expectNoSeriousA11yViolations(page, '自分のタスク')
  await collectCsp()
  for (const [path, label] of [
    ['/projects', 'プロジェクト一覧'],
    [`/projects/${projectKey}/tasks`, 'タスク一覧（表）'],
    [`/projects/${projectKey}/tasks?view=board`, 'タスク一覧（ボード）'],
    [`/projects/${projectKey}/tasks/${taskNumber}`, 'タスク詳細'],
    [`/projects/${projectKey}/milestones`, 'マイルストーン'],
    ['/settings', '個人設定'],
  ] as const) {
    await page.goto(path)
    await page.waitForLoadState('networkidle')
    await expectNoSeriousA11yViolations(page, label)
    await collectCsp()
  }
  expect(cspViolations, 'CSP 違反').toEqual([])
})

test('E2E-09 ガントチャート（表示単位の切り替え・日程の変更）とステータス別の円グラフ', async ({ page }) => {
  // CSP 違反を記録する（Docker 構成の nginx が付ける CSP。E2E-08 と同じ方法）
  await page.addInitScript(() => {
    const w = window as unknown as { __csp: string[] }
    w.__csp = []
    document.addEventListener('securitypolicyviolation', (e) => w.__csp.push(`${e.violatedDirective} ${e.sourceFile}`))
  })
  const cspViolations: string[] = []
  const collectCsp = async () => {
    cspViolations.push(...await page.evaluate(() => (window as unknown as { __csp: string[] }).__csp.splice(0)))
  }
  const taskKey = `${projectKey}-${taskNumber}`
  await login(page, editor.email, editor.password)

  // ガント: 期限 12/25（E2E-02 で設定）のタスクを 12 月を含む週の表示で確認する
  await page.goto(`/projects/${projectKey}/tasks?view=gantt&start=2026-11-30`)
  const gantt = page.getByRole('region', { name: 'ガントチャート' })
  await expect(gantt.getByRole('link', { name: '見積書の作成' })).toBeVisible()
  await expect(gantt.getByText(/12\/25（金）（開始日なし）/)).toBeVisible()
  await expect(page.getByRole('group', { name: '表示単位' }).getByRole('button', { name: '週' })).toHaveAttribute('aria-pressed', 'true')
  await expectNoSeriousA11yViolations(page, 'ガント（週）')
  await collectCsp()

  // 表示単位の切り替え: 日（14 日）・月（12 か月）
  await page.getByRole('group', { name: '表示単位' }).getByRole('button', { name: '日' }).click()
  await expect(page).toHaveURL(/unit=day/)
  await expect(page.getByText(/^\d{4}\/\d{1,2}\/\d{1,2}〜\d{4}\/\d{1,2}\/\d{1,2}$/)).toBeVisible()
  await page.getByRole('group', { name: '表示単位' }).getByRole('button', { name: '月' }).click()
  await expect(page).toHaveURL(/unit=month/)
  await expect(gantt.getByRole('link', { name: '見積書の作成' })).toBeVisible()
  await expect(gantt.getByText('今日')).toBeVisible()

  // キーボードでの代替: 表で見る → 開始日を入力
  await page.goto(`/projects/${projectKey}/tasks?view=gantt&start=2026-11-30`)
  await page.getByRole('button', { name: '表で見る' }).click()
  const scheduleTable = page.getByRole('table', { name: 'タスクの日程（表）' })
  await scheduleTable.getByLabel(`${taskKey} の開始日`).fill('2026-12-21')
  await expect(page.getByText(`${taskKey} の日程を変更しました`)).toBeVisible()
  await expectNoSeriousA11yViolations(page, 'ガント（表）')

  // マウスでのドラッグ: 日の表示で棒を 2 日分右へ移動する
  await page.goto(`/projects/${projectKey}/tasks?view=gantt&unit=day&start=2026-12-18`)
  const bar = page.locator('[data-editable]').first()
  await expect(bar).toBeVisible()
  const box = await bar.boundingBox()
  expect(box).not.toBeNull()
  await page.mouse.move(box!.x + 10, box!.y + box!.height / 2)
  await page.mouse.down()
  await page.mouse.move(box!.x + 10 + 48 * 2, box!.y + box!.height / 2, { steps: 8 })
  await page.mouse.up()
  await expect(page.getByText(`${taskKey} の日程を変更しました（12/23（水）〜12/27（日））`)).toBeVisible()
  await expect(page.getByRole('region', { name: 'ガントチャート' }).getByText('12/23（水）〜12/27（日）')).toBeVisible()
  await collectCsp()

  // プロジェクトの円グラフ（E2E-03 でステータスを「確認待ち」にしたタスク 1 件）
  await page.goto(`/projects/${projectKey}/tasks?view=chart`)
  const chartTable = page.getByRole('table', { name: 'ステータス別の割合（表）' })
  await expect(chartTable.getByRole('row', { name: /確認待ち/ })).toHaveText(/確認待ち\s*1\s*100\.0%/)
  await expect(chartTable.getByRole('row', { name: /未着手/ })).toHaveText(/未着手\s*0\s*0\.0%/)
  await expect(page.getByRole('img', { name: /ステータス別の割合/ })).toBeVisible()
  await expectNoSeriousA11yViolations(page, 'プロジェクトの円グラフ')
  await collectCsp()
  await chartTable.getByRole('link', { name: '確認待ち' }).click()
  await expect(page).toHaveURL(/statusId=/)
  await expect(page.getByRole('table', { name: 'タスクの一覧' }).getByRole('link', { name: '見積書の作成' })).toBeVisible()

  // 自分のタスクの円グラフ（担当 1 件。確認待ちの区分は「進行中」）
  await page.goto('/')
  await page.getByRole('button', { name: 'ステータス別の割合' }).click()
  const myTable = page.getByRole('table', { name: '自分のタスクの割合（区分別）（表）' })
  await expect(myTable.getByRole('row', { name: /進行中/ })).toHaveText(/進行中\s*1\s*100\.0%/)
  await expectNoSeriousA11yViolations(page, '自分のタスクの円グラフ')
  await collectCsp()

  expect(cspViolations, 'CSP 違反').toEqual([])
})
