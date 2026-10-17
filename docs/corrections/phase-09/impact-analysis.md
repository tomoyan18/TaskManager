# Phase 9 影響分析（impact-analysis）

- 作成日: 2026-10-16
- 入力: [investigation.md](investigation.md)
- 目的: ガントチャートとステータス別円グラフを追加したときに、どの層・ファイル・契約・テスト・運用に影響するかを、実装の前に明らかにする

---

## 1. 追加する機能の要約

| 機能 | 画面 | API | DB |
|---|---|---|---|
| ガントチャート（ビュー） | プロジェクトのタスク画面の表示形式に「ガント」を追加（`?view=gantt`） | 新規 `GET /api/v1/projects/{projectKey}/tasks/schedule`（期間と重なるタスク + 日付未設定のタスク）。日程の変更は既存の `PATCH /tasks/{taskNumber}` | 変更なし（既存の `start_date` / `due_date` を使う） |
| ステータス別円グラフ（プロジェクト） | プロジェクトのタスク画面の表示形式に「円グラフ」を追加（`?view=chart`） | 新規 `GET /api/v1/projects/{projectKey}/tasks/status-summary` | 変更なし（既存の `ix_task_item__project_id__status_id` を使う） |
| ステータス別円グラフ（自分のタスク） | 「自分のタスク」に表示の切り替え（期限別 / ステータス別の割合）を追加（`/?view=chart`） | 新規 `GET /api/v1/me/tasks/status-summary`（`projectKey` 任意） | 索引を 1 つ追加（`ix_task_item__assignee_user_id__project_id__status_id`） |

Entity・表・列の追加はない。スキーマの変更は索引 1 つだけで、EF Core マイグレーションを 1 件追加する。

## 2. 層ごとの影響

### 2.1 Domain

| 変更 | 内容 | 既存への影響 |
|---|---|---|
| 新規 `Tasks/ScheduleWindow.cs` | ガントの表示期間（開始日〜終了日）の値オブジェクト。開始 ≤ 終了、最大 366 日、2000-01-01〜2099-12-31。タスクの棒の範囲（開始日・期限の片方だけでも 1 日の棒にする）と期間との重なりを判定する | なし（新規） |
| 新規 `Common/ShareRounding.cs` | 件数から割合（%、小数第 1 位）を求める。最大剰余法で合計を 100.0 にそろえる。合計 0 件なら全て 0 | なし（新規） |
| 既存 `TaskItem` | **変更しない**。TK-02（開始日 ≤ 期限）・期限切れの判定（`IsOverdue`）をそのまま使う | — |

### 2.2 Application

| 変更 | 内容 | 既存への影響 |
|---|---|---|
| 新規 `Tasks/TaskScheduleService.cs` | 期間と重なるタスク（最大 500 件 + `truncated`）、日付未設定のタスク（件数 + 先頭 50 件）を返す。認可は `ProjectAccessGuard`（ViewProject） | なし |
| 新規 `Tasks/TaskStatusSummaryService.cs` | プロジェクト単位（ステータス別）と、自分の担当（全プロジェクトは区分別 / プロジェクト指定時はそのプロジェクトのステータス別）の件数・割合。集計は DB の `GROUP BY` | なし |
| 新規 DTO（`Tasks/TaskInsightDtos.cs`） | `TaskScheduleQuery`、`TaskScheduleResponse`、`TaskStatusSummaryResponse`、`StatusCountResponse`、`StatusSummaryGrouping` | なし |
| 既存 `TaskProjection` | **変更しない**。ガントの行は既存の `TaskSummaryResponse` で返す（N+1 を避ける既存の組み立てを再利用） | — |
| 既存 `TaskLabels` | 区分の日本語名 `Category()` を追加（区分別の集計で表示名を返すため） | 追加のみ |
| `ApplicationServiceCollectionExtensions` | 2 つのサービスを登録 | 追加のみ |

### 2.3 Infrastructure

| 変更 | 内容 | 既存への影響 |
|---|---|---|
| `TaskItemConfiguration` | 索引 `ix_task_item__assignee_user_id__project_id__status_id` を追加 | 既存の索引・制約は変えない |
| マイグレーション `AddTaskStatusSummaryIndex` | `CreateIndex` / `DropIndex` のみ | 既存の `InitialSchema` は編集しない（migration-strategy.md §4） |

### 2.4 Api

| 変更 | 内容 | 既存への影響 |
|---|---|---|
| `TasksController` | `GetTaskSchedule`、`GetTaskStatusSummary` を追加（ルートは既存の `{taskNumber:int:min(1)}` と衝突しない文字列のサブパス） | 既存の操作は変えない |
| `MyTasksController`（`PersonalControllers.cs`） | `GetMyTaskStatusSummary` を追加 | 同上 |
| `OpenApiSetup` | 変更なし（タグは既存の `Tasks` / `MyTasks` を使う。共通エラー・セキュリティ要件は自動付与） | — |

### 2.5 OpenAPI 契約

- 操作 61 → **64**。スキーマ追加: `TaskScheduleResponse`、`TaskStatusSummaryResponse`、`StatusCountResponse`、`StatusSummaryGrouping`。
- 更新: `docs/api/openapi.v1.json`（ビルド時生成を確認して更新）、`frontend/task-manager-web/src/api/schema.d.ts`（`npm run generate:api`）。
- 既存の操作の Request / Response は変えない（後方互換）。

### 2.6 Frontend

| 変更 | 内容 | 既存への影響 |
|---|---|---|
| `hooks/useTaskListParams.ts` | `view` に `gantt` / `chart` を追加。ガント用の `unit`・`start`・`group` を URL クエリで保持 | 既存の `table` / `board` の動作は変えない |
| `features/tasks/ProjectTasksPage.tsx` | 表示形式のボタンを 4 つに（表 / ボード / ガント / 円グラフ）。ガント・円グラフでは絞り込みバーを出さない | 表・ボードの取得条件は変えない |
| 新規 `features/tasks/TaskScheduleView.tsx`（+ CSS Module） | ガント本体、表形式の代替、日付未設定の枠、ドラッグでの日程変更 | なし |
| 新規 `utils/schedule.ts` | 期間・目盛り・棒の位置・グループ分けの計算（純粋関数） | なし |
| 新規 `components/StatusPieChart.tsx` | 円グラフ（SVG）+ 表（凡例を兼ねる）+ 合計 + 空状態 | なし |
| 新規 `features/tasks/ProjectStatusChartView.tsx` | プロジェクトの円グラフ | なし |
| `features/my-tasks/MyTasksPage.tsx` | 表示の切り替え（期限別 / ステータス別の割合）とプロジェクトの選択 | 既存の期限別の表示は既定のまま |
| `api/queries.ts` | クエリキー 3 つ | 追加のみ |
| `styles/components.css` | 円グラフの塗り（9 色のクラス） | 追加のみ |
| `api/schema.d.ts` | 生成し直し | 生成物 |

既存の `invalidateTaskQueries`（`['tasks', key]`・`['myTasks']` を無効化）にキーを合わせることで、タスクの変更後にガント・円グラフも最新になる（新しい無効化処理は不要）。

### 2.7 テスト

| 種類 | 追加・変更 |
|---|---|
| Domain | `ScheduleWindowTests`、`ShareRoundingTests`（新規） |
| Application（MySQL） | `TaskScheduleServiceTests`、`TaskStatusSummaryServiceTests`（新規。期間検索・集計クエリを MySQL で実行） |
| Infrastructure | `SchemaTests` に新しい索引の確認を追加 |
| API | 認可マトリクスに 3 操作を追加（既存の検査「全 operationId がマトリクスにある」が要求）。`OpenApiDocumentTests` の操作数 61 → 64（契約の変更に合わせた更新。検査は弱めない）。新規 `TaskInsightApiTests`（入力検証・IDOR・契約） |
| Frontend | `schedule.test.ts`、`StatusPieChart.test.tsx`、`TaskScheduleView.test.tsx`（画面）、`MyTasksPage.test.tsx` に円グラフの表示を追加 |
| E2E | E2E-09（ガントの表示と表示単位の切り替え、日程の変更、円グラフの表示、アクセシビリティ・CSP） |
| テスト補助 | `TestDataBuilder.CreateTaskAsync` に開始日の任意引数を追加（既存の呼び出しはそのまま動く） |

既存テストの削除・無効化はしない。

### 2.8 Docker・設定

- 構成の変更なし。マイグレーションは migrate コンテナが自動適用する（索引の追加のみ）。
- CSP の変更なし（インラインスタイルを挿入しない実装にする。§3）。
- 環境変数の追加なし。

## 3. 技術的なリスクと対策

| リスク | 影響 | 対策 |
|---|---|---|
| グラフ・ガントのライブラリが CSP（`style-src 'self'`）に違反する（インライン `<style>` の挿入など） | 本番構成で表示が崩れる・E2E-08 が失敗 | ライブラリを追加せず、SVG と CSS Modules で実装する（technology.md に評価を記録）。色はクラス（`fill: var(--sc-*-bar)`）で指定 |
| 期間検索の条件（開始日・期限のどちらかが NULL）の SQL 変換 | 期待と異なる行が返る | `COALESCE` に相当する LINQ（`??`）で条件を書き、MySQL 上の Application テストで境界（期間の前後・片方だけの日付・両方なし）を確認 |
| 大量のタスク（NFR-03: 1 プロジェクト 10,000 件） | 応答が大きくなる | 期間の上限 366 日、行の上限 500 件 + `truncated`、日付未設定は件数 + 先頭 50 件 |
| 割合の丸めで合計が 100% にならない | 利用者の混乱 | 最大剰余法で小数第 1 位の合計を 100.0 にそろえる（Domain で計算しテスト） |
| ドラッグでの日程変更の競合 | 他の人の変更を上書き | 既存の `version`（楽観的排他）を送る。409 は既存の「他の人が先に更新しました」で知らせて再取得 |
| ドラッグで開始日 > 期限 | 400 | 移動は両方を同じ日数ずらす、伸縮は期限のみで開始日より前にならないよう画面で制限。最終的な検証はサーバー（TK-02） |
| 自分の担当の集計で、複数プロジェクトのステータス名が重なる | 凡例が分かりにくい | 全プロジェクトは区分（未着手・進行中・完了・中止）で集計し、プロジェクトを選んだときだけそのプロジェクトのステータスで集計（OD-34） |
| 新しい操作の認可漏れ | IDOR | `ProjectAccessGuard` を必ず通す。自分の担当の集計は `assignee_user_id = 自分` と参加中プロジェクトで絞る。認可マトリクス（8 種類の呼び出し元）と IDOR テストで確認 |

## 4. 業務上の判断が必要な事項（open-decisions.md に記録）

| ID | 事項 | 推奨（設計に採用） |
|---|---|---|
| OD-32 | タスクの依存関係（先行・後続）をガントで扱うか | 扱わない（Entity・表の追加が必要で、Phase 9 の対象外の変更が大きい） |
| OD-33 | マイルストーンをガントで扱うか | 扱う（グループ分けと目標日の目印のみ。既存データで実現でき、追加の保存はない） |
| OD-34 | 「自分のタスク」の円グラフの集計単位 | 全プロジェクトは区分別、プロジェクトを選ぶとそのプロジェクトのステータス別 |
| OD-35 | 棒のドラッグによる日程変更 | 採用（既存の更新 API と権限・検証・楽観的排他を使う。キーボードでは表形式の代替で変更） |
| OD-36 | 割合の計算を Backend / Frontend のどちらで行うか | Backend（Domain）で計算し API で返す |
| OD-37 | アーカイブ・削除されたタスクを円グラフに含めるか | タスクの削除は物理削除のため存在しない。アーカイブはプロジェクト単位で、プロジェクトの円グラフは閲覧可能な限り表示（含める）、自分のタスクの全体集計はアーカイブ中のプロジェクトを除く（既存の「自分のタスク」と同じ） |
| OD-38 | グラフ・ガントのライブラリ | 追加しない（SVG + CSS で実装） |
| OD-39 | 円グラフの置き場所 | プロジェクト: タスク画面の表示形式「円グラフ」。自分のタスク: 表示の切り替え「ステータス別の割合」 |
| OD-40 | ガントの表示期間と上限 | 日: 14 日、週: 12 週、月: 12 か月。API の上限 366 日・500 件 |
| OD-41 | 円グラフの凡例から一覧への移動 | 行うプロジェクトの円グラフと、プロジェクトを選んだ自分のタスクの円グラフでは、ステータス名から絞り込み済みの表へ移動。区分別の集計では移動しない |

## 5. 変更しないもの（明示）

- 既存の 61 操作の Request / Response、エラーコード、認可。
- Entity・表・列、既存の索引・制約、`InitialSchema` マイグレーション。
- 表・ボード・タスク詳細・マイルストーンの画面の動作。
- Docker Compose・nginx・CSP の設定。
- ルートの `CLAUDE.md`、Plane のリポジトリ。

## 6. 作業の順序（Phase 9 の CLAUDE.md「作業順序」）

1. 設計書の更新（requirements → domain-model → database-design → migration-strategy → api-design → authentication-authorization → ui-design → ux-design → technology → naming-rules → security-design → testing-strategy → open-decisions → architecture）
2. Backend（Domain → Application → Infrastructure → マイグレーション → Api）
3. OpenAPI スナップショットの更新と確認、Frontend の型生成
4. Frontend（型 → API → フック → 部品 → 画面 → 切り替えの統合）
5. テスト追加・実行（Backend・Frontend・E2E）
6. Docker Compose での起動確認、README の画面キャプチャ
7. change-log.md・verification-report.md・完了判定
