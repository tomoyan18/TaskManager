# Frontend 解析（Plane `preview`、apps/web 中心）

## 目的

Page → Component → Hook → State → API Client → Backend API の流れを把握し、TaskManager の React / Vite / TypeScript 設計に必要な機能・操作を抽出する。デザインのコピーは目的としない。

## 解析範囲と根拠

- `apps/web/package.json`、`react-router.config.ts`、`app/routes/core.ts`
- 一覧ページ: `app/(all)/[workspaceSlug]/(projects)/projects/(detail)/[projectId]/issues/(list)/page.tsx`
- `components/issues/issue-layouts/roots/project-layout-root.tsx`
- `store/issue/project/issue.store.ts`、`store/user/base-permissions.store.ts`
- `services/api.service.ts`、`services/issue/issue.service.ts`
- `components/issues/issue-modal/*`、`components/power-k/config/*`
- `packages/i18n/src/locales/`

## 技術構成（確認済み）

| 項目 | 内容 |
|---|---|
| フレームワーク | React 19.2、React Router 8（`ssr: false`、SPA） |
| ビルド | Vite 8（React Router の Vite プラグイン）、Turborepo |
| 言語 | TypeScript 5.8（strict） |
| 状態管理 | MobX 6 + mobx-react（RootStore 配下にドメイン別ストア） |
| データ取得 | axios（サービスクラス）+ SWR（取得タイミング制御） |
| フォーム | react-hook-form |
| UI | Tailwind 4、`@makeplane/propel`（プリミティブ）、`@plane/blocks`（複合部品）、Base UI、lucide-react |
| テーブル / 仮想化 | @tanstack/react-table、@tanstack/react-virtual |
| ドラッグ&ドロップ | @atlaskit/pragmatic-drag-and-drop |
| エディタ | Tiptap 2 + Yjs（`packages/editor`） |
| グラフ | recharts |
| i18n | i18next（21 ロケール、`ja` あり） |
| 互換層 | `next/navigation` の `useParams` 等を互換シムで使用（旧 Next.js からの移行痕跡、`app/compat`） |

## Routing（確認済み）

`app/routes/core.ts` に宣言的に定義（layout + route + index）。主なもの:

| パス | 画面 |
|---|---|
| `/` | サインイン（ホーム） |
| `/sign-up`、`/accounts/{forgot,reset,set}-password` | 認証 |
| `/create-workspace`、`/onboarding`、`/invitations`、`/workspace-invitations` | 初期導線 |
| `/:ws` | ワークスペースホーム |
| `/:ws/projects` | プロジェクト一覧 |
| `/:ws/projects/:pid/issues` | 作業項目一覧（5 レイアウト） |
| `/:ws/projects/:pid/issues/:iid` | 作業項目詳細 |
| `/:ws/browse/:workItem` | 識別子（例 `ABC-12`）での作業項目表示 |
| `/:ws/projects/:pid/{cycles,modules,views,pages}`、`/:id` | 各一覧・詳細 |
| `/:ws/projects/:pid/intake`、`/archives/*` | 受付、アーカイブ |
| `/:ws/{notifications,drafts,stickies,active-cycles,analytics/:tab,workspace-views,profile/:uid}` | 横断機能 |
| `/:ws/settings/*`、`/:ws/settings/projects/:pid/*`、`/settings/profile/:tab` | 設定 |
| `/sign-in`、`/login`、`/register` 等 | 旧 URL からのリダイレクト |

## Page → API の追跡例: 作業項目一覧（確認済み）

```text
ProjectIssuesPage（page.tsx）
  └ <PageHead title="プロジェクト名 - 作業項目">
  └ ProjectLayoutRoot
      ├ useIssues(EIssuesStoreType.PROJECT) → { issues, issuesFilter }
      ├ useSWR(`PROJECT_ISSUES_${ws}_${pid}`, issuesFilter.fetchFilters)   … 表示設定（ユーザー別）を取得
      ├ ProjectLevelWorkItemFiltersHOC + WorkItemFiltersRow                 … フィルタ UI
      ├ displayFilters.layout で切替: ListLayout / KanBanLayout / CalendarLayout / BaseGanttRoot / ProjectSpreadsheetLayout
      │    └ 各レイアウトが issues.fetchIssues(ws, pid, loadType, paginationOptions)
      │         └ IssueService.getIssues → GET /api/workspaces/{ws}/projects/{pid}/issues/?...
      ├ 更新中インジケータ（getIssueLoader() === "mutation"）
      └ IssuePeekOverview                                                     … 一覧上で詳細を開くサイドパネル
```

- 作成: `components/issues/issue-modal/base.tsx` → 下書きなら `draftIssues.createIssue`、通常は `issues.createIssue` → `POST .../issues/` → 成功トースト（`issue_created_successfully` / `draft_created`）。
- 更新: `issueUpdate` → `PATCH .../issues/{id}/`（204）。ストア側を先に更新する楽観的更新（有力）。

## API Client（確認済み）

- `APIService`（抽象クラス）: axios インスタンス（`withCredentials: true`）、リクエスト時に末尾スラッシュ正規化、401 応答で `/?next_path=<現在パス>` へ遷移。
- ドメイン毎にサービスクラス（`IssueService`、`CycleService` 等）。`serviceType`（`issues` / `work-items` 等）で URL を切替える汎用化。
- 型は `@plane/types`（`TIssue`、`TIssuesResponse` 等）。

## State Management（確認済み）

- `RootStore`（`store/root.store.ts`）配下に、user / workspace / project / issue / cycle / module / label / state / member / notifications / pages / favorite / router などのストア。
- 作業項目ストアはコンテキスト別（project / cycle / module / project-views / workspace / profile / archived / workspace-draft）に分かれ、`helpers/base-issues.store.ts` を継承。
- ルーターストアで現在の workspaceSlug / projectId を保持し、権限判定などに使用。

## Forms / Validation

- react-hook-form。例: タイトル入力 `required: t("title_is_required")`、`maxLength`（255 と推定 → 有力）。
- サーバー検証エラーはトーストで表示（詳細なフィールド単位表示の網羅は未確認）。

## Loading / Error / Empty State

- ローディング: ストアのローダー状態（`init-loader`、`mutation`、ページング）、スピナー、スケルトン（`components/ui` 等、未精読）。
- エラー: トースト（`@plane/blocks/toast`）、ルートのエラー境界（`app/error`、`not-found.tsx`）。
- 空状態: `components/empty-state/`（専用コンポーネント群、`ja/empty-state.json` に 270 行の文言）。

## Modal / Dialog / Notification

- 作成モーダル、削除確認モーダル、ピーク表示（サイドパネル）。
- トーストで成功・失敗を通知。

## Table / Search / Filter / Pagination

- スプレッドシートレイアウト（@tanstack/react-table）。
- フィルタ行（`components/work-item-filters/`、`components/rich-filters/`）: 状態、優先度、担当者、ラベル、日付、作成者等。
- 表示設定: グループ化、サブグループ化、並び順、表示列、サブ作業項目表示、空グループ表示。ユーザー × プロジェクト単位でサーバーに保存。
- ページネーション: カーソル方式で無限スクロール（グループ毎の追加読み込み）。

## Permission-based rendering（確認済み）

- `allowPermissions([ADMIN, MEMBER], EUserPermissionsLevel.PROJECT)` で表示制御（96 コンポーネントで使用）。

## キーボード操作（確認済み）

`components/power-k/config/` にコマンドパレットとショートカット定義:

| 種別 | 例 |
|---|---|
| 移動（g + キー） | gh ホーム、gx 受信箱、gy 自分の作業、gp プロジェクト一覧、gi 作業項目、gc サイクル、gm モジュール、gv ビュー、gd ページ、ga 分析、gs 設定 |
| 作成（n + キー） | ni 作業項目、nd ページ、nv ビュー、nc サイクル、nm モジュール、np プロジェクト |
| 開く（o + キー） | ow ワークスペース、op プロジェクト、oc / om / ov / os |
| 単一キー | c（作成）、項目上で a（担当者）、l（ラベル）、p（優先度）、s（状態）、m、i |

## i18n

- 21 言語。`ja` は 28 ファイル（common 878 行、auth 368 行など）。文言キー方式（`t("issue.label", { count: 2 })`）で複数形にも対応。

## Frontend テスト

- `apps/web` に単体テストファイルは見つからない（`*.test.ts(x)` 検索で 0 件）。`packages/blocks` に vitest + Testing Library のテストあり。

## 発見事項

- Backend（CE）に存在しない API（`bulk-operation-issues/`）をストアから呼んでいる（[api.md](api.md) 参照）。
- `next/navigation` 互換シムの使用（移行途中の構造）。

## 不明点

- 楽観的更新と失敗時のロールバックの網羅的な挙動（Q-FE-01）
- サーバー検証エラーのフィールド単位表示の有無（Q-FE-02）

## TaskManager への影響

- 状態管理は MobX + SWR の 2 系統が混在しており複雑。TaskManager では TanStack Query 等サーバー状態に特化したライブラリ 1 本に寄せる案がある（設計候補、Phase 2 で決定）。
- 5 レイアウトは高コスト。リスト + カンバンから始め、他は要件次第（設計候補）。
- 表示設定のサーバー保存、URL での状態表現（ピーク表示）、キーボードショートカットは操作性向上の参考になる。
- 型定義を Backend の OpenAPI から生成すれば、Frontend との不一致（今回見つかった未実装 API 呼び出し）を防げる（設計候補）。

## Phase 2 で決定する事項

- ルーティング（React Router）と画面一覧
- サーバー状態管理・クライアント状態管理のライブラリ
- フォーム / バリデーションライブラリ（react-hook-form + zod 等）
- UI コンポーネント基盤（自前 / ヘッドレス UI）
- API クライアントの生成方式（OpenAPI からの型生成の採否）
- レイアウト（リスト / カンバン / カレンダー / ガント）の範囲
- i18n の要否（日本語のみか）
