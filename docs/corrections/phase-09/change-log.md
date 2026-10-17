# Phase 9 変更記録（change-log）

- 作成日: 2026-10-16
- 対象: ガントチャート機能とビュー、ステータス別円グラフ機能とビューの追加
- 方針: [impact-analysis.md](impact-analysis.md)。設計書を先に更新し（§1）、その後に実装した（§2 以降）
- 変更しなかったもの: ルートの `CLAUDE.md`、`.claude/` の指示ファイル、Plane のリポジトリ、既存の Entity・表・列・索引・`InitialSchema` マイグレーション、Docker Compose・nginx（CSP）の設定、既存の 61 操作の契約

## 1. 設計書・記録

| ファイル | 種別 | 内容 | 理由 |
|---|---|---|---|
| `docs/corrections/phase-09/investigation.md` | 新規 | 確認したファイル、指示の矛盾（C-01〜C-08）、現状、変更前の基準テスト | Phase 9「作業順序 1・2」 |
| `docs/corrections/phase-09/impact-analysis.md` | 新規 | 層ごとの影響、リスクと対策、業務判断が必要な事項 | 同「作業順序 3」 |
| `docs/corrections/phase-09/change-log.md` | 新規 | 本書 | 同「修正記録」 |
| `docs/corrections/phase-09/verification-report.md` | 新規 | テスト・動作確認の結果、完了判定 | 同「修正記録」 |
| `docs/design/requirements.md` | 更新 | §1・§3・§7（対象外の見直し）・§8 UF-04・§9.11 REQ-GNT-01〜03、REQ-CHT-01〜02（ID・名称・目的・利用者・前提・入力・処理・出力・エラー・権限・関連 Entity / API / 画面・重要度）・NFR-15 | 要件の追加 |
| `docs/design/domain-model.md` | 更新 | `ScheduleWindow`（SC-01〜05）、`ShareRounding`（SR-01〜05）、Application Service 2 件 | 日程・割合の業務規則 |
| `docs/design/database-design.md` | 更新 | 索引 `ix_task_item__assignee_user_id__project_id__status_id`、主要クエリと索引（ガント・集計）、§11 Phase 9 での変更 | DB 側で集計するため |
| `docs/design/migration-strategy.md` | 更新 | §8 マイグレーションの履歴（`AddTaskStatusSummaryIndex`） | 索引の追加 |
| `docs/design/api-design.md` | 更新 | #62 `GetTaskSchedule`、#63 `GetTaskStatusSummary`、#64 `GetMyTaskStatusSummary`、`StatusCountResponse`・`StatusSummaryGrouping`、Controller の対応 | API 契約を先に定義 |
| `docs/design/authentication-authorization.md` | 更新 | §6.3 ガント・円グラフの権限、§7.2 自分の担当の集計の絞り込み | 認可・IDOR |
| `docs/design/ui-design.md` | 更新 | SCR-20（ガント）、SCR-21（円グラフ）、SCR-04 の追加、部品 `TaskScheduleView`・`StatusPieChart`、ルーティング、空状態の文言 | 画面設計 |
| `docs/design/ux-design.md` | 更新 | F-7（日程の確認と調整）、F-8（割合の確認）、操作ルール（URL での再現、ドラッグの代替）、文言 | 操作の流れ |
| `docs/design/technology.md` | 更新 | §10 グラフ・ガントのライブラリの評価（追加しない） | ライブラリの評価の記録（指示の必須事項） |
| `docs/design/naming-rules.md` | 更新 | §11 追加した名前と理由、Plane との独立性の照合 | 指示の必須事項 |
| `docs/design/security-design.md` | 更新 | S-24 の上限、S-25 ガント・円グラフ | セキュリティ対策 |
| `docs/design/testing-strategy.md` | 更新 | §12 Phase 9 で追加するテスト | テスト方針 |
| `docs/design/open-decisions.md` | 更新 | OD-12 の見直し、§6 OD-32〜OD-41 | 決定できない事項・判断の記録 |
| `docs/design/architecture.md` | 更新 | §6.1 追加した Frontend の構成と Backend の層 | 構成の記録 |
| `docs/design/README.md` | 更新 | エンドポイント数（64）、Phase 9 の記録へのリンク | 案内 |

## 2. Backend

| ファイル | 種別 | 内容 | 理由 |
|---|---|---|---|
| `src/TaskManager.Domain/Tasks/ScheduleWindow.cs` | 新規 | 表示期間（366 日以内・日付の範囲）の検証、棒の範囲、期間との重なり | SC-01〜04 |
| `src/TaskManager.Domain/Common/ShareRounding.cs` | 新規 | 割合（小数第 1 位）を最大剰余法で合計 100.0 にそろえる | SR-01〜04、OD-36 |
| `src/TaskManager.Application/Tasks/TaskInsightDtos.cs` | 新規 | `TaskScheduleQuery`、`TaskScheduleResponse`、`StatusSummaryGrouping`、`StatusCountResponse`、`TaskStatusSummaryResponse` | API #62〜64 の DTO |
| `src/TaskManager.Application/Tasks/TaskScheduleService.cs` | 新規 | 期間と重なるタスク（最大 500 件）と日付未設定（件数 + 50 件）。認可は `ProjectAccessGuard`。LINQ の `??`（SQL の COALESCE）で期間検索 | REQ-GNT-01 |
| `src/TaskManager.Application/Tasks/TaskStatusSummaryService.cs` | 新規 | ステータス別（プロジェクト・自分の担当のプロジェクト指定）、区分別（自分の担当の全プロジェクト）の件数を `GROUP BY` で集計 | REQ-CHT-01・02 |
| `src/TaskManager.Application/Tasks/TaskLabels.cs` | 更新 | 区分の日本語名 `Category()` を追加 | 区分別集計の表示名 |
| `src/TaskManager.Application/ApplicationServiceCollectionExtensions.cs` | 更新 | 2 サービスの登録 | DI |
| `src/TaskManager.Infrastructure/Persistence/Configurations/TaskConfigurations.cs` | 更新 | 索引 `ix_task_item__assignee_user_id__project_id__status_id` | 自分の担当の集計 |
| `src/TaskManager.Infrastructure/Persistence/Migrations/20261015165527_AddTaskStatusSummaryIndex.cs`（+ `.Designer.cs`）、`AppDbContextModelSnapshot.cs` | 新規 / 自動更新 | 索引の追加・削除のみ（`dotnet ef migrations add` で生成） | スキーマの変更 |
| `src/TaskManager.Api/Controllers/TasksController.cs` | 更新 | `GetTaskSchedule`（`from`・`to` は必須のクエリ）、`GetTaskStatusSummary` | API #62・#63 |
| `src/TaskManager.Api/Controllers/PersonalControllers.cs` | 更新 | `GetMyTaskStatusSummary`（`projectKey` は形式を検証） | API #64 |

## 3. OpenAPI 契約・Frontend の型

| ファイル | 内容 |
|---|---|
| `docs/api/openapi.v1.json` | ビルド時生成の文書（`artifacts/openapi/openapi.json`）と既存のスナップショットを比較し、差分が「3 操作・4 スキーマの追加のみ（既存の操作・スキーマの変更なし）」であることを確認してから更新 |
| `frontend/task-manager-web/src/api/schema.d.ts` | `npm run generate:api` で再生成 |

## 4. Frontend

| ファイル | 種別 | 内容 |
|---|---|---|
| `src/utils/schedule.ts` | 新規 | 期間（日・週・月）、前へ / 次へ / 今日、目盛り、棒の位置・切り取り、今日の線、ドラッグ後の日付、まとめ方、URL の値の検証 |
| `src/features/tasks/TaskScheduleView.tsx`、`TaskSchedule.module.css` | 新規 | SCR-20 ガント（ツールバー、時間軸、棒、期限切れ、今日の線、まとめ方、マイルストーンの ◆、ドラッグでの日程変更、表形式の代替 `TaskScheduleTable`、日付未設定の枠、空状態・読み込み・エラー・上限の案内） |
| `src/components/StatusPieChart.tsx`、`StatusPieChart.module.css` | 新規 | 円グラフ（SVG）、凡例を兼ねる表、合計、要約の文、空状態。色はクラス指定（CSP を変えない） |
| `src/features/tasks/ProjectStatusChartView.tsx` | 新規 | SCR-21 プロジェクトの円グラフ、ステータス名から絞り込み済みの表へのリンク |
| `src/features/my-tasks/MyStatusChartView.tsx` | 新規 | SCR-04 の「ステータス別の割合」（対象: すべてのプロジェクト / 参加中のプロジェクト） |
| `src/features/my-tasks/MyTasksPage.tsx` | 更新 | 表示の切り替え（期限別 / ステータス別の割合、URL `view`・`project`）。期限別の表示は従来どおり |
| `src/features/tasks/ProjectTasksPage.tsx` | 更新 | 表示形式を 4 つに（表 / ボード / ガント / 円グラフ）。ガント・円グラフでは一覧の取得と絞り込みバーを止める |
| `src/hooks/useTaskListParams.ts` | 更新 | `view` に `gantt`・`chart`、ガントの `unit`・`start`・`group` |
| `src/api/queries.ts` | 更新 | クエリキー 3 つ（既存の無効化に含まれる形） |

## 5. テスト

| ファイル | 種別 | 内容 |
|---|---|---|
| `tests/TaskManager.Domain.Tests/Tasks/ScheduleWindowTests.cs` | 新規 | 期間の検証・棒の範囲・重なりの境界 |
| `tests/TaskManager.Domain.Tests/Common/ShareRoundingTests.cs` | 新規 | 割合の合計 100.0、端数の配り方、0 件、合計 0 件 |
| `tests/TaskManager.Application.Tests/Tasks/TaskInsightServiceTests.cs` | 新規 | `TaskScheduleServiceTests`・`TaskStatusSummaryServiceTests`（MySQL での期間検索・集計、認可、タイムゾーン、検証） |
| `tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs` | 追加 | 集計・期間検索に使う索引の列、マイグレーションの適用順 |
| `tests/TaskManager.Api.Tests/Flows/TaskInsightApiTests.cs` | 新規 | 契約・入力検証・日程変更（既存の更新 API・競合 409・TK-02）・IDOR・`projectKey` の形式 |
| `tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrix.cs` | 追加 | 3 操作 × 8 種類の呼び出し元（既存の「全 operationId の登録」検査が要求） |
| `tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs` | 更新 | 操作数 61 → 64（契約の変更に合わせた更新。検査の内容は変えていない） |
| `tests/TaskManager.Testing/TestDataBuilder.cs` | 更新 | `CreateTaskAsync` に任意の `startDate` 引数（末尾に追加。既存の呼び出しはそのまま） |
| `frontend/task-manager-web/src/utils/schedule.test.ts` | 新規 | 期間・目盛り・棒・ドラッグ・まとめ方 |
| `frontend/task-manager-web/src/components/StatusPieChart.test.tsx` | 新規 | 0 件の凡例、合計、要約、1 項目、空状態、リンク |
| `frontend/task-manager-web/src/features/tasks/TaskScheduleView.test.tsx` | 新規 | ガント・円グラフの画面（取得条件、表示単位、まとめ方、表での日程変更、閲覧者、空・エラー・上限） |
| `frontend/task-manager-web/src/features/my-tasks/MyTasksPage.test.tsx` | 追加 | 自分の円グラフ（区分別・プロジェクト指定・空・エラー） |
| `frontend/task-manager-web/src/test/fixtures.ts`、`server.ts` | 追加 | 新しい API のテスト用応答（MSW の既定） |
| `frontend/task-manager-e2e/tests/main-flow.spec.ts` | 追加 | E2E-09（ガントの表示と表示単位の切り替え、表とドラッグでの日程変更、円グラフ、自分の円グラフ、axe、CSP） |

既存のテストは削除・無効化していない。既存テストの期待値を変えたのは操作数（61 → 64）の 1 か所のみで、API の追加という契約の変更に合わせたもの。

## 6. E2E の補助・README・起動手順・既存の記録

| ファイル | 種別 | 内容 | 理由 |
|---|---|---|---|
| `frontend/task-manager-e2e/tests/helpers.ts` | 更新 | `login` がログインの回数制限（429）の表示を検出したら 61 秒待って 1 回だけやり直す | E2E が同じ送信元から 10 回/分を超えてログインし、区切りの位置によって既存の E2E-08 が失敗したため（verification-report.md §2.4 E-2）。制限そのものは変えていない |
| `src/features/tasks/TaskScheduleView.tsx`、`src/components/StatusPieChart.module.css` | 更新 | 行の見出しにステータス名を文字で表示、表の数値を右寄せ | 画面のキャプチャの目視確認で気付いた点（verification-report.md §3） |
| `README.md` | 更新 | 機能（ガント・ステータス別の割合）、画面キャプチャ（09〜12 を追加、01〜08 を撮り直し）、マイグレーション、API 64 操作、テスト件数（1,079・144・9 シナリオ）、E2E の回数制限の待機、Phase 9 の記録へのリンク | README に実画面のキャプチャを付ける指示 |
| `docs/images/01〜08-*.png` | 撮り直し | 表示形式のボタン（表 / ボード / ガント / 円グラフ）、Swagger UI の 64 操作を反映 | 画面の変化に合わせる |
| `docs/images/09-task-gantt.png`、`10-task-gantt-month.png`、`11-project-status-chart.png`、`12-my-status-chart.png` | 新規 | Docker 構成で起動した実画面（架空のデータ） | README の指示 |
| `docs/build_Application/StartUp-Application.md` | 追記 | §5 に Phase 9 の起動確認（マイグレーション 2 件の適用、E2E 9 / 9） | 起動手順の確認結果 |
| `docs/implementation/status.md` | 追記 | §11 Phase 9 の要件・API・画面の対応 | 実装状況の記録 |
| `docs/testing/test-results.md`、`docs/testing/test-coverage.md` | 追記 | Phase 9 の結果と追加したテストの一覧 | テスト記録 |
| `docs/completion/known-issues.md` | 追記 | KI-20〜KI-23（割合の丸め、E2E の回数制限の待機、ドラッグの代替、承認待ちの判断） | 既知の課題 |
