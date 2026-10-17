# 命名規則と命名理由

## 1. 基本方針

1. 名前は **TaskManager の業務上の意味と責務** から決める。Plane の名前を置換・流用しない（CLAUDE.md §10）。
2. 技術識別子は英語、利用者向け表示は自然な日本語。
3. 1 つの概念には 1 つの名前を使い、層をまたいで揃える（例: Domain の `TaskItem`、表 `task_item`、API の `tasks`、画面の「タスク」）。
4. .NET / JavaScript の標準ライブラリと衝突する名前を避ける。
5. Plane との独立性は §9 の照合表で確認する。

## 2. 用語集（ユビキタス言語）

| 日本語（画面） | 英語識別子 | 意味 | 採用理由 |
|---|---|---|---|
| 利用者 | UserAccount | ログインできる人 | `User` は ASP.NET の `ClaimsPrincipal` 周りの `User` プロパティと紛らわしいため `UserAccount` |
| システム管理者 | SystemRole.Administrator | 利用者を管理できる人 | |
| プロジェクト | Project | タスクをまとめる単位 | 業務で一般的な語。日本語でも「プロジェクト」が自然 |
| プロジェクトキー | ProjectKey | プロジェクトの短い英字識別子（例 `SALES`） | タスク番号の接頭辞。作成後に変更できない |
| メンバー | ProjectMembership | 利用者がプロジェクトに所属している関係（ロールを持つ） | Plane のモデル名と重ならず、「所属関係」という責務を表す |
| 管理者 / 編集者 / 閲覧者 | ProjectRole.Owner / Editor / Viewer | プロジェクト内の権限 | 操作内容（管理・編集・閲覧）がそのまま分かる |
| タスク | TaskItem | やるべき仕事 1 件 | C# の `System.Threading.Tasks.Task` と衝突するため `TaskItem` |
| タスク番号 | TaskNumber | プロジェクト内の連番 | |
| タスクキー | TaskKey | `プロジェクトキー-番号`（例 `SALES-12`） | 値オブジェクト |
| サブタスク | Subtask（親子関係は ParentTaskId） | タスクを分割したもの（1 段のみ） | |
| ステータス | WorkflowStatus | タスクの進み具合（プロジェクトごとに定義） | `TaskStatus` は .NET の列挙型と衝突。「ワークフロー上の段階」という責務を表す |
| ステータス区分 | StatusCategory | 未着手 / 進行中 / 完了 / 中止 | 集計・期限判定・完了日時の根拠 |
| 優先度 | TaskPriority | 低 / 中 / 高 / 至急 | |
| 担当者 | Assignee | タスクの責任者 1 名 | |
| 期限 | DueDate | いつまでに終えるか（日付） | |
| 開始日 | StartDate | いつから始めるか（日付） | |
| タグ | Tag | 自由な分類 | 画面上も「タグ」が日本語として自然 |
| マイルストーン | Milestone | 目標日のある節目。タスクを束ねる | |
| コメント | TaskComment | タスク上のやり取り | |
| 添付ファイル | TaskAttachment | タスクに添付したファイル | |
| 変更履歴 | TaskHistoryEntry | タスクの変更の記録 1 件 | 「履歴の 1 行」という単位を表す |
| お知らせ | Notice | 利用者への通知 1 件 | 画面で「お知らせ」と表示するため。`Notification` は Web API の Notification と紛らわしい |
| 自分のタスク | MyTasks | 自分が担当の未完了タスク | ホーム画面 |
| ログインセッション | RefreshSession | リフレッシュトークン 1 系列 | 何のセッションかを明示 |
| 監査記録 | SecurityAuditEvent | セキュリティ上の出来事 1 件 | |

## 3. C#（Backend）

| 対象 | 規則 | 例 |
|---|---|---|
| ソリューション / プロジェクト | `TaskManager.{層}` | `TaskManager.Domain` |
| 名前空間 | フォルダと一致、機能単位 | `TaskManager.Application.Tasks` |
| Entity | 単数形 PascalCase の業務名 | `TaskItem`, `WorkflowStatus` |
| Value Object | 単数形、`record struct` または `record` | `ProjectKey`, `TaskKey`, `EmailAddress`, `HexColor` |
| 列挙 | 単数形 | `StatusCategory`, `ProjectRole` |
| Application Service | `{機能}Service`（ユースケースをメソッドとして持つ） | `TaskItemService.CreateAsync` |
| Interface | `I` + 役割 | `IAppDbContext`, `ICurrentUser`, `IAttachmentStorage` |
| Request DTO（入力） | `{動詞}{対象}Request` | `CreateTaskRequest`, `UpdateTaskRequest` |
| Response DTO（出力） | `{対象}{粒度}Response` | `TaskSummaryResponse`, `TaskDetailResponse` |
| ページ応答 | `PagedResponse<T>`（OpenAPI 上は `{T}Page`） | `TaskSummaryPage` |
| Controller | `{リソース複数形}Controller` | `TasksController`, `ProjectMembersController` |
| アクション | 動詞 + 対象 | `GetTask`, `CreateTask`（OpenAPI の operationId と同一） |
| 例外 | `{意味}Exception` | `ResourceNotFoundException`, `ConcurrencyConflictException` |
| 非同期メソッド | 末尾 `Async` | `FindByKeyAsync` |
| テスト | `{対象}Tests`、メソッドは `{メソッド}_{条件}_{期待}` | `CreateAsync_WhenAssigneeIsViewer_ThrowsValidation` |

DTO は API の外形（OpenAPI スキーマ）なので Application 層に置き、Domain Entity をそのまま返さない。

## 4. データベース（MySQL）

| 対象 | 規則 | 例 | 理由 |
|---|---|---|---|
| DB 名 | `taskmanager_db` | — | 用途が分かり、他システムと衝突しない |
| 表 | **単数形** snake_case の業務名 | `task_item`, `workflow_status` | 1 行が 1 つの実体を表すことを強調。Entity 名と 1 対 1 |
| 列 | snake_case | `due_date` | MySQL の大文字小文字の扱い（OS 依存）を避ける |
| 主キー | `id` | — | |
| 外部キー列 | `{参照先の役割}_id` | `assignee_user_id`, `parent_task_id` | 参照先だけでなく役割を表す（同じ表を複数回参照するため） |
| 日時列 | `{意味}_at_utc`（`DATETIME(6)`、UTC） | `created_at_utc` | UTC であることを列名で明示 |
| 日付列 | `{意味}_date`（`DATE`） | `due_date` | タイムゾーンを持たないことを型で表す |
| 真偽列 | `is_` / `has_` / `must_` | `is_default`, `must_change_password` | |
| 監査列 | `created_at_utc`, `created_by_user_id`, `updated_at_utc`, `updated_by_user_id` | — | |
| 楽観的排他列 | `row_version`（`INT UNSIGNED`） | — | 役割が明確 |
| 主キー制約 | `pk_{表}` | `pk_task_item` | |
| 外部キー制約 | `fk_{表}__{参照表}__{列}` | `fk_task_item__user_account__assignee_user_id` | 64 文字を超える場合は列名を省略形にする（実装時に確認） |
| 一意インデックス | `ux_{表}__{列...}` | `ux_task_item__project_id__task_number` | |
| 通常インデックス | `ix_{表}__{列...}` | `ix_task_item__assignee_user_id__due_date` | |
| CHECK 制約 | `ck_{表}__{意味}` | `ck_task_item__date_range` | |

EF Core では `UseSnakeCaseNamingConvention` 系の拡張は使わず、`IEntityTypeConfiguration<T>` で表名・列名・制約名を明示する（MySQL 公式プロバイダとの互換性確認を不要にするため）。

## 5. REST API

| 対象 | 規則 | 例 |
|---|---|---|
| ベースパス | `/api/v1` | 一般的なバージョン付き REST の慣例 |
| リソース | 複数形 kebab-case | `/projects`, `/user-directory` |
| 識別子 | プロジェクトは `projectKey`、タスクは `taskNumber`、その他は GUID | `/projects/SALES/tasks/12` |
| 状態遷移 | リソース + 動詞のサブパス（POST） | `/projects/SALES/archive` |
| クエリ | camelCase、配列は同名キーの繰り返し | `?statusId=a&statusId=b&sort=-dueDate` |
| JSON プロパティ | camelCase | `dueDate`, `assigneeUserId` |
| 列挙値（JSON） | camelCase 文字列 | `"inProgress"`, `"urgent"` |
| エラーコード | `{領域}.{理由}` snake_case | `task.has_subtasks` |
| OpenAPI タグ | PascalCase 英語 + 日本語説明 | `Tasks`（タスク） |
| operationId | Controller アクション名 | `ListTasks` |

## 6. Frontend（React / TypeScript）

| 対象 | 規則 | 例 |
|---|---|---|
| ディレクトリ | 機能単位 kebab-case | `src/features/tasks/` |
| コンポーネント | PascalCase、`{対象}{形}` | `TaskTable`, `TaskStatusBoard`, `TaskFormDialog` |
| ページ | `{対象}Page` | `ProjectTasksPage`, `TaskDetailPage` |
| フック | `use{目的}` | `useTaskList`, `useCurrentUser` |
| API 型 | OpenAPI から生成した型を `components["schemas"]["TaskDetailResponse"]` 経由で別名定義 | `type TaskDetail = Schemas["TaskDetailResponse"]` |
| クエリキー | 配列、先頭はリソース | `["tasks", projectKey, filters]` |
| CSS Modules | `{Component}.module.css`、クラスは camelCase | `.cardHeader` |
| 定数 | UPPER_SNAKE_CASE | `MAX_TITLE_LENGTH` |
| 画面文言 | `src/i18n/ja.ts` に集約（キーは英語、値は日本語） | `task.create.success` |

## 7. 環境変数

- プレフィックス `TM_`（Compose / `.env`）。コンテナ内では .NET の階層キー（`Jwt__SigningKey`）へ対応付ける。
- 例: `TM_JWT_SIGNING_KEY`, `TM_DB_APP_PASSWORD`

## 8. 主要な命名判断の記録

| 判断 | 採用 | 却下した案 | 理由 |
|---|---|---|---|
| タスクの Entity 名 | `TaskItem` | `Task`, `WorkItem`, `Ticket` | `Task` は .NET と衝突。`WorkItem` は Plane の用語と重なる。`Ticket` は問い合わせ管理の印象が強い |
| ステータスの Entity 名 | `WorkflowStatus` | `TaskStatus`, `State`, `Stage` | `TaskStatus` は .NET と衝突。`State` は Plane と同一。「ワークフロー上の状態」という責務が明確 |
| 表名の単数形 | `task_item` | `task_items`, `TaskItems` | Entity と 1 対 1 で読みやすい。Plane の複数形表名と区別できる |
| 日時列の接尾辞 `_at_utc` | 採用 | `_at` | UTC 保存の取り違えを防ぐ |
| プロジェクトをキーで指す API | `/projects/{projectKey}` | GUID | 画面 URL と一致し人が読める。キーは不変なので安定 |
| タスクを番号で指す API | `/projects/{projectKey}/tasks/{taskNumber}` | `/tasks/{guid}` | ルート自体がプロジェクトの範囲を限定し、他プロジェクトのタスクを指せない（IDOR 対策、[security-design.md](security-design.md)） |
| 通知の名前 | `Notice` / お知らせ | `Notification` | 画面文言と一致、ブラウザ API との混同回避 |
| 変更履歴 | `TaskHistoryEntry` | `Activity` | 「誰が何を変えたか」という履歴の意味を直接表す |

## 9. Plane との独立性の照合（表名）

Phase 1 で記録した Plane の表名（`docs/analysis/database.md`）と TaskManager の表名を照合した。

| TaskManager | 同一名の Plane 表 | 判定 |
|---|---|---|
| `user_account` | なし（Plane は `users`, `accounts`） | 独立 |
| `refresh_session` | なし（Plane は `sessions`） | 独立 |
| `project` | なし（Plane は `projects`） | 独立 |
| `project_membership` | なし（Plane は `project_members`） | 独立 |
| `workflow_status` | なし（Plane は `states`） | 独立 |
| `tag` | なし（Plane は `labels`） | 独立 |
| `task_item_tag` | なし（Plane は `issue_labels`） | 独立 |
| `milestone` | なし（Plane は `cycles`, `modules`） | 独立 |
| `task_item` | なし（Plane は `issues`） | 独立 |
| `task_comment` | なし（Plane は `issue_comments`） | 独立 |
| `task_attachment` | なし（Plane は `issue_attachments`, `file_assets`） | 独立 |
| `task_history_entry` | なし（Plane は `issue_activities`） | 独立 |
| `notice` | なし（Plane は `notifications`） | 独立 |
| `security_audit_event` | なし | 独立 |

列名も監査列（`created_at_utc` 等）・外部キー列（役割付き）の規則により Plane と異なる。Phase 8 で実装後の DB スキーマに対して再照合する。

## 10. Plane との独立性の照合（Entity / クラス名）

Plane の Django モデル名（`docs/analysis/database.md`、`apps/api/plane/db/models/__init__.py` の export 一覧）と照合した。

| TaskManager | 同一名の Plane クラス | 判定・理由 |
|---|---|---|
| `UserAccount` | なし（Plane は `User`, `Account`） | 独立 |
| `RefreshSession` | なし（Plane は `Session`） | 独立 |
| `Project` | **あり**（Plane `Project`） | 例外として採用（下記） |
| `ProjectMembership` | なし（Plane は `ProjectMember`） | 独立 |
| `WorkflowStatus` | なし（Plane は `State`） | 独立 |
| `Tag` / `TaskItemTag` | なし（Plane は `Label`, `IssueLabel`） | 独立 |
| `Milestone` | なし | 独立 |
| `TaskItem` | なし（Plane は `Issue`） | 独立 |
| `TaskComment` | なし（Plane は `IssueComment`） | 独立 |
| `TaskAttachment` | なし（Plane は `IssueAttachment`, `FileAsset`） | 独立 |
| `TaskHistoryEntry` | なし（Plane は `IssueActivity`） | 独立 |
| `Notice` | なし（Plane は `Notification`） | 独立 |
| `SecurityAuditEvent` | なし | 独立 |

**`Project` を例外とする理由**: 「プロジェクト」はこの業務領域で利用者が実際に使う一般名詞であり、業務意味から名前を決めると `Project` になる。別名（`WorkSpace`、`Board` 等）にすると意味がずれ、かえって分かりにくくなる。クラスの責務・属性（不変のキー、採番カウンタ、アーカイブ、ワークスペース所属なし）は Plane の `Project` とは独立に設計している。この例外を認めるかは [open-decisions.md](open-decisions.md) OD-15 で確認する。

DTO・Controller・Application Service・React コンポーネントの名前は、Plane の `IssueSerializer`、`IssueViewSet`、`ProjectViewSet`、`IssuePeekOverview`、`KanBanLayout` 等と重複しない（Phase 1 で確認した Plane の名前と照合済み）。

## 11. Phase 9 で追加した名前と理由（2026-10-16）

### 11.1 用語（画面）

| 日本語（画面） | 英語識別子 | 意味 | 理由 |
|---|---|---|---|
| ガント | `gantt`（URL の `view`）、`TaskSchedule*`（コード） | タスクの日程を横棒で表した表示 | 画面の利用者にはなじみのある「ガント」を表示名に使う。コードでは「タスクの日程」という責務を表す `Schedule` を使い、表示方法（ガント）と分ける |
| 表示単位（日 / 週 / 月） | `unit`（`day` / `week` / `month`） | 1 列の幅 | 一般的な語 |
| まとめ方 | `group`（`none` / `assignee` / `status` / `milestone`） | 行のグループ分け | 一般的な語 |
| 日付が未設定のタスク | `undated` | 開始日・期限がどちらもないタスク | 状態をそのまま表す |
| ステータス別の割合 / 円グラフ | `chart`（URL の `view`）、`StatusSummary`・`StatusCount`（コード） | ステータスごとの件数と割合 | 集計の軸（ステータス）と内容（件数）を名前に入れる |

### 11.2 コード・API・DB

| 種類 | 名前 | 理由 |
|---|---|---|
| Value Object（Domain） | `ScheduleWindow` | 「日程を表示する期間」。`DateRange`（タスクの開始日・期限）と役割を分けるため別名 |
| ドメインサービス（Domain） | `ShareRounding` | 「割合（share）の丸め」。丸め規則（SR-01〜05）を 1 か所にまとめる |
| Application Service | `TaskScheduleService`、`TaskStatusSummaryService` | 命名規則 `{機能}Service` |
| Query DTO | `TaskScheduleQuery` | `TaskListQuery` と同じ形の名前 |
| Response DTO | `TaskScheduleResponse`、`TaskStatusSummaryResponse`、`StatusCountResponse` | 命名規則 `{対象}{粒度}Response` |
| 列挙 | `StatusSummaryGrouping`（`Status` / `Category`） | 何でまとめたかを表す |
| operationId | `GetTaskSchedule`、`GetTaskStatusSummary`、`GetMyTaskStatusSummary` | 動詞 + 対象（既存の `GetMyTasks` と同じ形） |
| ルート | `/projects/{projectKey}/tasks/schedule`、`/projects/{projectKey}/tasks/status-summary`、`/me/tasks/status-summary` | タスクの下位の「見え方」として `tasks` の下に置く。単数形・kebab-case は既存の `/user-directory`・`/notices/unread-count`（集合ではない計算結果）と同じ扱い。`{taskNumber:int}` と衝突しない |
| Controller | 既存の `TasksController`・`MyTasksController` に追加 | 新しいリソースではなく、タスクの参照の一種のため |
| 索引 | `ix_task_item__assignee_user_id__project_id__status_id` | 命名規則 `ix_{表}__{列...}`（53 文字） |
| マイグレーション | `AddTaskStatusSummaryIndex` | 変更内容を表す PascalCase |
| React コンポーネント | `TaskScheduleView`、`TaskScheduleTable`、`StatusPieChart`、`ProjectStatusChartView`、`MyStatusChartView` | `{対象}{形}`。`StatusPieChart` はプロジェクト・自分のタスクで共用する部品 |
| Frontend の型 | `TaskSchedule`（= `Schemas['TaskScheduleResponse']`）、`StatusSummary`（= `Schemas['TaskStatusSummaryResponse']`） | 生成型の別名（§6） |
| ユーティリティ | `utils/schedule.ts`（`buildTimeline`、`barPlacement`、`groupScheduleRows` など） | 日程の計算 |
| クエリキー | `['tasks', key, 'schedule', params]`、`['tasks', key, 'status-summary']`、`['myTasks', 'status-summary', projectKey]` | 既存の無効化（`['tasks', key]`・`['myTasks']`）に含まれるようにする |

### 11.3 Plane との独立性の照合（Phase 9）

Phase 1 の解析資料（analysis/frontend.md・feature-map.md・appendix-api-endpoints.md）に記録された Plane のガント・分析の名前と照合した。

| Plane の名前（解析資料） | TaskManager | 判定 |
|---|---|---|
| `BaseGanttRoot`、`gantt` レイアウト（ユーザー × プロジェクトでサーバー保存） | `TaskScheduleView`、URL クエリ `view=gantt`（サーバー保存なし） | 独立（名前・構造とも異なる。`gantt` は一般語として URL の値にのみ使用） |
| `issue-dates`（一括日付更新 API） | 既存の `PATCH /tasks/{taskNumber}` を使用（一括 API は作らない） | 独立 |
| `analytics`、`advance-analytics-charts`、`project-stats`、`AnalyticsEndpoint` | `tasks/status-summary`、`TaskStatusSummaryService`、`GetTaskStatusSummary` | 独立 |
| recharts（グラフライブラリ） | 使用しない（technology.md §10） | 独立 |

