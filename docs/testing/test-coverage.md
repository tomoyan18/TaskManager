# テストカバレッジ（Phase 5）

- 作成日: 2026-10-12
- テストの構成と方針: [test-design.md](test-design.md)
- 本書の「カバー」は、テストコードがあることを表す。実行結果は [test-results.md](test-results.md)（Phase 6: 全件成功。実行した環境とコマンドは同書）。
- 行数のカバレッジは未測定（test-design.md §11 T-07）。

## 1. 概要

| 種類 | テストメソッド / 定義 | 検出されたケース数 | 実行 |
|---|---|---|---|
| Domain | 72 | 133 | Phase 6 で実行 |
| Application（MySQL） | 119 | 215 | Phase 6 で実行 |
| Infrastructure（EF Core / MySQL） | 25 | 53 | Phase 6 で実行 |
| API（TestServer + MySQL） | 54 | 587 | Phase 6 で実行 |
| Frontend（Vitest） | 61 | 101 | Phase 6 で実行 |
| E2E（Playwright） | 8 | 8 | Phase 6 で実行（ローカル構成・Docker 構成） |

## 2. OpenAPI の全エンドポイントとの対応

OpenAPI 文書（[docs/api/openapi.v1.json](../api/openapi.v1.json)）の **61 操作すべて** が認可マトリクス（8 種類の呼び出し元 × 61 = 488 ケース）に含まれる。
マトリクスの各ケースは、ステータスの期待値に加えて、応答のステータスが文書に定義されていること・本文がスキーマに合うことを検査する。
マトリクスと文書の operationId が一致することもテストしている（`AuthorizationMatrixTests`）。

「API の個別テスト」は、マトリクス以外で応答の契約や挙動を確認するテスト。「Application の挙動テスト」は、同じ処理をサービス単位（MySQL 込み）で確認するテスト。

| # | Method | Route | operationId | 認可マトリクス | API の個別テスト（契約検査・挙動） | Application の挙動テスト |
|---|---|---|---|---|---|---|
| 1 | POST | `/api/v1/auth/login` | Login | ○ | AuthApiTests, MainFlowApiTests, OpenApiDocumentTests | AuthServiceTests |
| 2 | POST | `/api/v1/auth/refresh` | RefreshAccessToken | ○ | AuthApiTests, OpenApiDocumentTests | AuthServiceTests |
| 3 | POST | `/api/v1/auth/logout` | Logout | ○ | OpenApiDocumentTests | AuthServiceTests |
| 4 | POST | `/api/v1/auth/password` | ChangePassword | ○ | AuthApiTests, MainFlowApiTests | AuthServiceTests |
| 5 | GET | `/api/v1/me` | GetMe | ○ | AuthApiTests, MainFlowApiTests | — |
| 6 | PATCH | `/api/v1/me` | UpdateMe | ○ | — | MyAccountServiceTests |
| 7 | GET | `/api/v1/me/tasks` | GetMyTasks | ○ | MainFlowApiTests | MyTasksServiceTests |
| 8 | GET | `/api/v1/me/notices` | ListMyNotices | ○ | MainFlowApiTests | NoticeServiceTests |
| 9 | GET | `/api/v1/me/notices/unread-count` | GetMyUnreadNoticeCount | ○ | MainFlowApiTests | NoticeServiceTests |
| 10 | POST | `/api/v1/me/notices/{noticeId}/read` | MarkNoticeRead | ○ | IdorApiTests | NoticeServiceTests |
| 11 | POST | `/api/v1/me/notices/read-all` | MarkAllNoticesRead | ○ | MainFlowApiTests | NoticeServiceTests |
| 12 | GET | `/api/v1/search/tasks` | SearchTasks | ○ | MainFlowApiTests | TaskSearchServiceTests |
| 13 | GET | `/api/v1/projects` | ListProjects | ○ | — | ProjectServiceTests |
| 14 | POST | `/api/v1/projects` | CreateProject | ○ | MainFlowApiTests | ProjectServiceTests |
| 15 | GET | `/api/v1/projects/{projectKey}` | GetProject | ○ | MainFlowApiTests | ProjectServiceTests |
| 16 | PATCH | `/api/v1/projects/{projectKey}` | UpdateProject | ○ | — | ProjectServiceTests |
| 17 | DELETE | `/api/v1/projects/{projectKey}` | DeleteProject | ○ | AuthorizationMatrixTests（削除の成功） | ProjectServiceTests |
| 18 | POST | `/api/v1/projects/{projectKey}/archive` | ArchiveProject | ○ | — | ProjectServiceTests |
| 19 | POST | `/api/v1/projects/{projectKey}/unarchive` | UnarchiveProject | ○ | — | ProjectServiceTests |
| 20 | GET | `/api/v1/projects/{projectKey}/members` | ListProjectMembers | ○ | — | ProjectMembershipServiceTests |
| 21 | POST | `/api/v1/projects/{projectKey}/members` | AddProjectMember | ○ | MainFlowApiTests | ProjectMembershipServiceTests |
| 22 | PATCH | `/api/v1/projects/{projectKey}/members/{userId}` | ChangeProjectMemberRole | ○ | IdorApiTests | ProjectMembershipServiceTests |
| 23 | DELETE | `/api/v1/projects/{projectKey}/members/{userId}` | RemoveProjectMember | ○ | AuthorizationMatrixTests（自分の脱退）, IdorApiTests | ProjectMembershipServiceTests |
| 24 | GET | `/api/v1/system/info` | GetSystemInfo | ○ | OpenApiDocumentTests, SecurityApiTests | — |
| 25 | GET | `/api/v1/projects/{projectKey}/tasks` | ListTasks | ○ | MainFlowApiTests, ValidationApiTests | TaskItemServiceTests |
| 26 | POST | `/api/v1/projects/{projectKey}/tasks` | CreateTask | ○ | MainFlowApiTests, ValidationApiTests | TaskItemServiceTests |
| 27 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | GetTask | ○ | MainFlowApiTests, ValidationApiTests | TaskItemServiceTests |
| 28 | PATCH | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | UpdateTask | ○ | MainFlowApiTests, ValidationApiTests | TaskItemServiceTests |
| 29 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | DeleteTask | ○ | AuthorizationMatrixTests（作成者の削除） | TaskItemServiceTests |
| 30 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/history` | ListTaskHistory | ○ | MainFlowApiTests | TaskItemServiceTests |
| 31 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments` | ListTaskComments | ○ | — | TaskCommentServiceTests |
| 32 | POST | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments` | CreateTaskComment | ○ | MainFlowApiTests | TaskCommentServiceTests |
| 33 | PATCH | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}` | UpdateTaskComment | ○ | AuthorizationMatrixTests（投稿者の編集）, IdorApiTests | TaskCommentServiceTests |
| 34 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}` | DeleteTaskComment | ○ | AuthorizationMatrixTests（投稿者の削除）, IdorApiTests | TaskCommentServiceTests |
| 35 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments` | ListTaskAttachments | ○ | — | — |
| 36 | POST | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments` | UploadTaskAttachment | ○ | MainFlowApiTests, SecurityApiTests | TaskAttachmentServiceTests |
| 37 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments/{attachmentId}/content` | DownloadTaskAttachment | ○ | SecurityApiTests, MainFlowApiTests, IdorApiTests | TaskAttachmentServiceTests |
| 38 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments/{attachmentId}` | DeleteTaskAttachment | ○ | AuthorizationMatrixTests（投稿者の削除）, IdorApiTests | TaskAttachmentServiceTests |
| 39 | GET | `/api/v1/users` | ListUsers | ○ | — | UserAdministrationServiceTests |
| 40 | POST | `/api/v1/users` | CreateUser | ○ | MainFlowApiTests | UserAdministrationServiceTests |
| 41 | GET | `/api/v1/users/{userId}` | GetUser | ○ | IdorApiTests | UserAdministrationServiceTests |
| 42 | PATCH | `/api/v1/users/{userId}` | UpdateUser | ○ | — | UserAdministrationServiceTests |
| 43 | POST | `/api/v1/users/{userId}/deactivate` | DeactivateUser | ○ | AuthApiTests（停止で即時失効） | UserAdministrationServiceTests, LastAdministratorTests |
| 44 | POST | `/api/v1/users/{userId}/activate` | ActivateUser | ○ | — | — |
| 45 | POST | `/api/v1/users/{userId}/unlock` | UnlockUser | ○ | — | UserAdministrationServiceTests |
| 46 | POST | `/api/v1/users/{userId}/password-reset` | ResetUserPassword | ○ | — | UserAdministrationServiceTests |
| 47 | GET | `/api/v1/user-directory` | SearchUserDirectory | ○ | MainFlowApiTests | — |
| 48 | GET | `/api/v1/projects/{projectKey}/statuses` | ListWorkflowStatuses | ○ | MainFlowApiTests | WorkflowStatusServiceTests |
| 49 | POST | `/api/v1/projects/{projectKey}/statuses` | CreateWorkflowStatus | ○ | — | WorkflowStatusServiceTests |
| 50 | PATCH | `/api/v1/projects/{projectKey}/statuses/{statusId}` | UpdateWorkflowStatus | ○ | IdorApiTests | WorkflowStatusServiceTests |
| 51 | DELETE | `/api/v1/projects/{projectKey}/statuses/{statusId}` | DeleteWorkflowStatus | ○ | IdorApiTests | WorkflowStatusServiceTests |
| 52 | PUT | `/api/v1/projects/{projectKey}/statuses/order` | ReorderWorkflowStatuses | ○ | IdorApiTests | WorkflowStatusServiceTests |
| 53 | GET | `/api/v1/projects/{projectKey}/tags` | ListTags | ○ | — | — |
| 54 | POST | `/api/v1/projects/{projectKey}/tags` | CreateTag | ○ | — | TagServiceTests |
| 55 | PATCH | `/api/v1/projects/{projectKey}/tags/{tagId}` | UpdateTag | ○ | IdorApiTests | — |
| 56 | DELETE | `/api/v1/projects/{projectKey}/tags/{tagId}` | DeleteTag | ○ | IdorApiTests | TagServiceTests |
| 57 | GET | `/api/v1/projects/{projectKey}/milestones` | ListMilestones | ○ | — | — |
| 58 | POST | `/api/v1/projects/{projectKey}/milestones` | CreateMilestone | ○ | — | MilestoneServiceTests |
| 59 | GET | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | GetMilestone | ○ | IdorApiTests | — |
| 60 | PATCH | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | UpdateMilestone | ○ | IdorApiTests | MilestoneServiceTests |
| 61 | DELETE | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | DeleteMilestone | ○ | IdorApiTests | MilestoneServiceTests |

### 2.1 挙動を個別に確認していないエンドポイント（認可と応答の契約のみ）

| operationId | 確認済みの範囲 | 未確認の挙動 | 重要度 |
|---|---|---|---|
| ListTaskAttachments | 認可・応答スキーマ | 一覧の並び・`canDelete` の値 | 低（詳細画面の表示に使う。Frontend の E2E-05 で間接的に確認） |
| ActivateUser | 認可・応答（204） | 停止中の利用者を有効にしてログインできること | 中 |
| ListTags | 認可・応答スキーマ | `taskCount` の集計 | 低 |
| ListMilestones | 認可・応答スキーマ | 状態での絞り込み（`state`）、件数の集計（完了・期限切れ） | 中 |
| UpdateTag | 認可・IDOR | 名前の重複（409）・色の変更 | 低 |
| GetMilestone | 認可・IDOR | 件数の集計 | 低 |
| SearchUserDirectory | 認可（管理者 / いずれかのプロジェクトの管理者 / それ以外 403）・E2E-02 | `excludeProjectKey` で既存メンバーを除くこと（MainFlowApiTests で一部確認）、停止中の利用者を返さないこと | 中 |

## 3. 未テストの重要項目（エンドポイント以外）

| 項目 | 理由・予定 |
|---|---|
| 1MB を超える JSON → 413（サーバーの本文サイズ上限） | TestServer では Kestrel の上限が働かないため自動テストなし。Phase 6 で実サーバーに対して確認済み。11MB を超える添付は API テストを追加（Phase 6） |
| 全 API 共通・アップロード・検索のレート制限 | ログインの制限のみテストした |
| 定期メンテナンス（`MaintenanceService`） | テスト未作成 |
| Docker 構成（nginx の中継、Host の受け渡し、読み取り専用ルート、非 root） | 専用の自動テストはない。Phase 6 で E2E（Docker 構成）と手動確認を実施（test-results.md §5） |
| MySQL 8.4 での動作 | Phase 6 で Testcontainers（8.4.11）により Backend 全体を実行済み |
| 行カバレッジ（Domain + Application 80%） | 測定の拡張が未導入（T-07、test-results.md §8 O-03） |
| 画面: マイルストーン・プロジェクト設定（メンバー・ワークフロー）・利用者管理・個人設定・お知らせ一覧の個別の画面テスト | Vitest では未作成。E2E-01・02・04・08 で主な操作と表示を確認する |

## 4. テストの一覧

「目的・期待結果」はテストメソッド名（日本語）から、「条件」は `[InlineData]` などのデータ行から作成した。
「対象」は `テストクラス.メソッド` の形（Domain・Application はテスト対象のクラス名と操作名）。

### Domain（DB: なし）

| # | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| 1 | UserAccount.Create | 一時パスワード状態の有効な利用者を作る | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 2 | UserAccount.Create | 表示名の検証 | 2 通り（""; "改行\nあり"） | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 3 | UserAccount.RegisterFailedLogin | 4回まではロックせず5回目でロックする | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 4 | UserAccount.IsLockedAt | ロック期限ちょうどで解除される | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 5 | UserAccount.RegisterSuccessfulLogin | 失敗回数とロックを消す | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 6 | UserAccount.ChangePassword | 一時パスワード状態を解除しスタンプを変える | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 7 | UserAccount.ResetPassword | 変更を強制しロックを解除しスタンプを変える | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 8 | UserAccount.Deactivate | スタンプを変えて既存のトークンを無効にする | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 9 | RefreshSession.StartFamily | 有効期限は14日で絶対期限は30日 | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 10 | RefreshSession.Rotate | 同じ系列で次を作り元は使えなくなる | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 11 | RefreshSession.Rotate | 絶対期限を超えて延長しない | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 12 | RefreshSession.Revoke | 使えなくなり最初の失効日時を保つ | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 13 | RefreshSession.StartFamily | 長い利用者エージェントは切り詰める | — | [UserAccountTests.cs](../../tests/TaskManager.Domain.Tests/Accounts/UserAccountTests.cs) |
| 14 | EmailAddress.Create | 有効な形式なら前後空白を除き小文字に正規化する | 2 通り（"  Owner@Example.TEST ", "owner@example.test"; "a@b.jp", "a@b.jp"） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 15 | EmailAddress.Create | 不正な形式はDomainException | 11 通り（null; ""; "no-at-mark.example.test"; "@example.test" …） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 16 | EmailAddress.Create | 254文字は許可し255文字は拒否する | — | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 17 | ProjectKey.Create | 英字で始まる2から10文字を大文字に正規化する | 3 通り（"sales", "SALES"; " Ab ", "AB"; "A123456789", "A123456789"） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 18 | ProjectKey.Create | 規則外はDomainException | 7 通り（"A"; "A1234567890"; "1ABC"; "AB-C" …） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 19 | TaskKey.TryParse | キーを解析する | 3 通り（"SALES-12", "SALES", 12; " sales-1 ", "SALES", 1; "A1-999999999", "A1", 999999999） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 20 | TaskKey.TryParse | 不正な形式はfalse | 8 通り（"SALES-0"; "SALES-01"; "SALES-1234567890"; "SALES12" …） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 21 | TextRules.RequireSingleLine | 前後空白を除く | — | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 22 | TextRules.RequireSingleLine | 空は入力を求める | 3 通り（""; "   "; null） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 23 | TextRules.RequireSingleLine | 最大長ちょうどは許可し超過は拒否する | — | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 24 | TextRules.RequireSingleLine | 改行や制御文字を拒否する | 2 通り（"一行目\n二行目"; "制御\u0007文字"） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 25 | TextRules.OptionalMultiline | 空はnull | 3 通り（null; ""; "  \n "） | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 26 | TextRules.OptionalMultiline | 改行とタブは許可しその他の制御文字は拒否する | — | [ValueObjectTests.cs](../../tests/TaskManager.Domain.Tests/Common/ValueObjectTests.cs) |
| 27 | Project.CreateDefaultStatuses | 既定のステータス5件を作る | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 28 | Project.Archive | 最初のアーカイブ日時を保ち解除できる | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 29 | Project.Rename | 80文字まで | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 30 | WorkflowStatus.CanBeDefault | 未着手と進行中だけ | 4 通り（StatusCategory.NotStarted, true; StatusCategory.InProgress, true; StatusCategory.Done, false; StatusCategory.Canceled, false） | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 31 | WorkflowStatus.Create | 完了区分を既定にすると拒否する | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 32 | WorkflowStatus.ChangeCategory | 既定のステータスを終了区分にできない | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 33 | WorkflowStatus.ChangeCategory | 既定でなければ終了区分にできる | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 34 | WorkflowStatus.Create | 名前は30文字まで | — | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 35 | WorkflowStatus.CanBeAssigned | 編集者以上 | 3 通り（ProjectRole.Viewer, false; ProjectRole.Editor, true; ProjectRole.Owner, true） | [ProjectTests.cs](../../tests/TaskManager.Domain.Tests/Projects/ProjectTests.cs) |
| 36 | DueBucketClassifier.Classify | 週は月曜始まりで今週の日曜日までを今週とする | 5 通り（"2026-10-06", DueBucket.Overdue; "2026-10-07", DueBucket.Today; "2026-10-08", DueBucket.ThisWeek; "2026-10-11", DueBucket.ThisWeek …） | [DueBucketClassifierTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/DueBucketClassifierTests.cs) |
| 37 | DueBucketClassifier.Classify | 期限なし | — | [DueBucketClassifierTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/DueBucketClassifierTests.cs) |
| 38 | DueBucketClassifier.Classify | 今日が日曜日なら今週は該当なしで翌日は以降 | — | [DueBucketClassifierTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/DueBucketClassifierTests.cs) |
| 39 | DueBucketClassifier.Classify | 今日が月曜日なら日曜日まで今週 | — | [DueBucketClassifierTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/DueBucketClassifierTests.cs) |
| 40 | DueBucketClassifier.EndOfWeek | 年またぎ | — | [DueBucketClassifierTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/DueBucketClassifierTests.cs) |
| 41 | TaskItem.Create | タイトルを正規化し未完了で作る | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 42 | TaskItem.Create | 番号は1以上 | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 43 | TaskItem.Create | タイトル200文字は許可し201文字は拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 44 | TaskItem.ChangeStatus | 完了区分へ変えると完了日時を設定する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 45 | TaskItem.ChangeStatus | 完了から完了への変更では完了日時を保つ | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 46 | TaskItem.ChangeStatus | 完了以外へ戻すと完了日時を消す | 3 通り（StatusCategory.InProgress; StatusCategory.Canceled; StatusCategory.NotStarted） | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 47 | TaskItem.ChangeStatus | 別プロジェクトのステータスは拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 48 | TaskItem.ChangeDates | 開始日と期限が同じ日は許可する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 49 | TaskItem.ChangeDates | 期限が開始日より前なら拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 50 | TaskItem.ChangeDates | 範囲外の開始日を拒否する | 2 通り（1999, 12, 31, "startDate"; 2100, 1, 1, "startDate"） | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 51 | TaskItem.ChangeDates | 範囲の両端は許可する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 52 | TaskItem.ChangeParent | 通常のタスクを親にできる | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 53 | TaskItem.ChangeParent | 自分自身は拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 54 | TaskItem.ChangeParent | サブタスクの下にはサブタスクを作れない | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 55 | TaskItem.ChangeParent | サブタスクを持つタスクは別タスクの子にできない | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 56 | TaskItem.ChangeParent | 別プロジェクトのタスクは拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 57 | TaskItem.ReplaceTags | 置き換えと変更有無を返す | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 58 | TaskItem.ReplaceTags | 10個まで許可し11個は拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 59 | TaskItem.ReplaceTags | 別プロジェクトのタグは拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 60 | TaskItem.ChangeMilestone | 別プロジェクトのマイルストーンは拒否する | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 61 | TaskItem.IsOverdue | 終了区分でなく期限が今日より前の場合だけtrue | 5 通り（"2026-10-08", StatusCategory.InProgress, true; "2026-10-09", StatusCategory.InProgress, false; "2026-10-08", StatusCategory.Done, false; "2026-10-08", StatusCategory.Canceled, false …） | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 62 | TaskItem.ChangeDescription | 空白だけならnull | — | [TaskItemTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskItemTests.cs) |
| 63 | TaskAttachmentFileName.NormalizeFileName | パス区切りと使えない文字を除く | 10 通り（"見積書.pdf", "見積書.pdf"; "../../etc/passwd", "passwd"; "..\\..\\windows\\win.ini", "win.ini"; "C:\\Users\\a\\report.xlsx", "report.xlsx" …） | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 64 | TaskAttachmentFileName.NormalizeFileName | 長い名前は拡張子を残して255文字に収める | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 65 | TaskComment.Create | 空のコメントは拒否する | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 66 | TaskComment.Edit | 本文と編集日時を更新する | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 67 | TaskComment.Create | 10000文字まで | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 68 | Notice.Create | リンクは画面内の相対パスだけ | 3 通り（"https://evil.example.test/"; "//evil.example.test/"; "javascript:alert(1)"） | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 69 | Notice.Create | 長い本文は300文字に切り詰める | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 70 | Notice.MarkRead | 最初の既読日時を保つ | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 71 | Notice.Refresh | 本文と日時を更新する | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |
| 72 | TaskHistoryEntry.Create | 長い値は500文字に切り詰める | — | [TaskRelatedEntityTests.cs](../../tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs) |

### Application（DB: MySQL）

| # | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| 1 | AuthService.Login | 正しいパスワードでトークンとリフレッシュトークンを返す | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 2 | AuthService.Login | 誤ったパスワードと存在しない利用者は同じエラー | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 3 | AuthService.Login | 5回失敗するとロックされ正しいパスワードでもログインできない | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 4 | AuthService.Login | 利用停止中はログインできない | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 5 | AuthService.Login | 一時パスワード状態は変更が必要なことを返す | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 6 | AuthService.Refresh | ローテーションし古いトークンの再利用で系列ごと失効する | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 7 | AuthService.Refresh | トークンがないか不明なら期限切れ | 3 通り（null; ""; "not-a-real-token"） | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 8 | AuthService.Refresh | 14日を過ぎると期限切れ | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 9 | AuthService.Refresh | 利用停止された利用者は期限切れ | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 10 | AuthService.Logout | 同じ系列のトークンを失効する | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 11 | AuthService.Logout | トークンなしや不明なトークンでもエラーにしない | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 12 | AuthService.ChangePassword | 成功すると一時パスワード状態を解除し他のセッションを失効する | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 13 | AuthService.ChangePassword | 現在のパスワードが違うと項目エラー | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 14 | AuthService.ChangePassword | ポリシー違反は項目エラー | 3 通り（"short-pw"; "password1234"; "aaaa-bbbb-cccc"） | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 15 | AuthService.ChangePassword | 現在と同じパスワードは使えない | — | [AuthServiceTests.cs](../../tests/TaskManager.Application.Tests/Auth/AuthServiceTests.cs) |
| 16 | ProjectMembershipService.Add | メンバーを追加し本人にお知らせする | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 17 | ProjectMembershipService.Add | 既存メンバー・停止中・存在しない利用者を拒否する | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 18 | ProjectMembershipService.Add | 編集者はメンバーを追加できない | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 19 | ProjectMembershipService.ChangeRole | 閲覧者にすると担当を外し履歴を残す | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 20 | ProjectMembershipService | 最後の管理者は降格も脱退もできない | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 21 | ProjectMembershipService.Remove | 閲覧者は自分で脱退できるが他人は外せない | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 22 | ProjectMembershipService.Remove | 外すと担当を外す | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 23 | ProjectMembershipService | システム管理者は管理者不在のプロジェクトにメンバーを追加できる | — | [ProjectMembershipServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectMembershipServiceTests.cs) |
| 24 | ProjectService.Create | 作成者が管理者になり既定のステータス5件を作る | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 25 | ProjectService.Create | キーと名前の重複は409 | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 26 | ProjectService.Create | キーの形式が不正なら項目エラー | 2 通り（"1ABC"; "A"） | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 27 | ProjectService.Update | 古い版での更新は409で内容は変わらない | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 28 | ProjectService.Update | 送らなかった項目は変更しない | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 29 | ProjectService.Update | 編集者は設定を変更できない | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 30 | ProjectService.List | 自分が所属するプロジェクトだけを返す | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 31 | ProjectService.List | システム管理者は全件指定で所属外も参照できる | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 32 | ProjectService.Archive | アーカイブと解除 | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 33 | ProjectService.Delete | アーカイブ済みでキー確認が一致すれば関連データとファイルを削除する | — | [ProjectServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/ProjectServiceTests.cs) |
| 34 | TagService.Create | 大文字小文字を区別せず同名は409 | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 35 | TagService.Delete | タスクからも外す | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 36 | TagService.Delete | 別プロジェクトのタグIDは404で削除されない | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 37 | TagService | 閲覧者はタグを作れない | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 38 | MilestoneService.Create | 同名は409 | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 39 | MilestoneService.Update | 古い版は409 | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 40 | MilestoneService.Delete | タスクとの関連を外して履歴を残す | — | [TagAndMilestoneServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/TagAndMilestoneServiceTests.cs) |
| 41 | WorkflowStatusService.Create | 末尾に追加し同名は409 | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 42 | WorkflowStatusService.Create | 20件を超えると409 | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 43 | WorkflowStatusService.Delete | 既定のステータスは削除できない | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 44 | WorkflowStatusService.Delete | 使用中は移動先が必要で移動後に履歴を残す | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 45 | WorkflowStatusService.Delete | 移動先に別プロジェクトのステータスは指定できない | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 46 | WorkflowStatusService.Delete | 別プロジェクトのステータスIDは404 | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 47 | WorkflowStatusService.Update | 既定を切り替えると以前の既定は外れる | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 48 | WorkflowStatusService.Reorder | 全件の並びを指定する | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 49 | WorkflowStatusService | 編集者はステータスを管理できない | — | [WorkflowStatusServiceTests.cs](../../tests/TaskManager.Application.Tests/Projects/WorkflowStatusServiceTests.cs) |
| 50 | PasswordPolicy.Validate | 条件を満たすパスワードは許可する | 2 通り（"Kanagawa-Minato-2026"; "12文字の日本語パスワードです"） | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 51 | PasswordPolicy.Validate | 12文字ちょうどは許可し11文字は拒否する | — | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 52 | PasswordPolicy.Validate | 128文字まで | — | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 53 | PasswordPolicy.Validate | 規則違反を拒否する | 7 通り（" leading-space-1"; "trailing-space1 "; "PASSWORD1234"; "TaskManager2026" …） | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 54 | PasswordPolicy.Validate | 短いローカル部や表示名は含んでも拒否しない | — | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 55 | ProjectPermissionRules.IsGranted | ロールと権限の対応表どおり | データ Matrix | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 56 | ProjectPermissionRules.IsAllowedWhileArchived | 参照とアーカイブ解除と削除だけ | 7 通り（ProjectPermission.ViewProject, true; ProjectPermission.ArchiveProject, true; ProjectPermission.DeleteProject, true; ProjectPermission.EditTasks, false …） | [PasswordPolicyTests.cs](../../tests/TaskManager.Application.Tests/Security/PasswordPolicyTests.cs) |
| 57 | ProjectAccessGuard | 非メンバーには存在を明かさず404 | — | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 58 | ProjectAccessGuard | 存在しないキーや不正なキーは404 | 3 通り（"NOSUCHKEY"; "invalid-key"; "../etc"） | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 59 | ProjectAccessGuard | メンバーはロールに応じて許可または403 | 8 通り（ProjectRole.Viewer, ProjectPermission.ViewProject, true; ProjectRole.Viewer, ProjectPermission.EditTasks, false; ProjectRole.Viewer, ProjectPermission.Comment, false; ProjectRole.Editor, ProjectPermission.EditTasks, true …） | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 60 | ProjectAccessGuard | アーカイブ中は参照できるが変更は409 | — | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 61 | ProjectAccessGuard | システム管理者は非メンバーでもメンバー管理と削除だけ可能 | — | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 62 | ProjectAccessGuard | システム管理者はアーカイブ中でもメンバー管理できる | — | [ProjectAccessGuardTests.cs](../../tests/TaskManager.Application.Tests/Security/ProjectAccessGuardTests.cs) |
| 63 | MyTasksService.Get | 期限で区分し他人のタスクとアーカイブ中のプロジェクトは含めない | — | [PersonalViewTests.cs](../../tests/TaskManager.Application.Tests/Tasks/PersonalViewTests.cs) |
| 64 | MyTasksService.Get | 今日は利用者のタイムゾーンで決める | — | [PersonalViewTests.cs](../../tests/TaskManager.Application.Tests/Tasks/PersonalViewTests.cs) |
| 65 | NoticeService | 自分のお知らせだけを参照し既読にできる | — | [PersonalViewTests.cs](../../tests/TaskManager.Application.Tests/Tasks/PersonalViewTests.cs) |
| 66 | TaskSearchService | 所属するプロジェクトだけを検索しキーで完全一致を先頭にする | — | [PersonalViewTests.cs](../../tests/TaskManager.Application.Tests/Tasks/PersonalViewTests.cs) |
| 67 | TaskCommentService.Create | 担当者と作成者にお知らせし投稿者本人には送らない | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 68 | TaskCommentService.Create | 5分以内の未読のお知らせはまとめる | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 69 | TaskCommentService.Create | 閲覧者はコメントできない | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 70 | TaskCommentService.Create | 空白だけのコメントは項目エラー | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 71 | TaskCommentService.Update | 投稿者以外は編集できず管理者は削除できる | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 72 | TaskCommentService.Delete | 別タスクのコメントIDは404 | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 73 | TaskCommentService.List | 本文はMarkdownのまま保存し返す | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 74 | TaskAttachmentService.Upload | 中身を確認して保存しダウンロードできる | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 75 | TaskAttachmentService.Upload | 拡張子と中身が一致しない・許可外の形式は415 | 5 通り（"report.pdf", "PNGではない"; "image.png", "%PDF-1.7"; "script.exe", "MZ"; "page.html", "<html>" …） | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 76 | TaskAttachmentService.Upload | テキストにNULが含まれると拒否する | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 77 | TaskAttachmentService.Upload | 10MBちょうどは許可し超過は413 | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 78 | TaskAttachmentService.Upload | 空のファイルは項目エラー | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 79 | TaskAttachmentService.Upload | 1タスク20件まで | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 80 | TaskAttachmentService.Delete | 他の編集者のファイルは削除できず投稿者は削除できる | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 81 | TaskAttachmentService.OpenRead | 別プロジェクトの添付IDは404 | — | [TaskCommentServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskCommentServiceTests.cs) |
| 82 | TaskItemService.Create | タイトルだけで既定値のタスクを作り番号を順に振る | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 83 | TaskItemService.Create | 担当者を指定すると本人にお知らせする | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 84 | TaskItemService.Create | 自分を担当者にしてもお知らせは作らない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 85 | TaskItemService.Create | 閲覧者・非メンバー・停止中の利用者は担当者にできない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 86 | TaskItemService.Create | 別プロジェクトのステータス・タグ・マイルストーン・親は指定できない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 87 | TaskItemService.Create | 重複したタグと終了したマイルストーンを拒否する | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 88 | TaskItemService.Create | サブタスクの下にはサブタスクを作れない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 89 | TaskItemService.Create | 期限が開始日より前なら項目エラー | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 90 | TaskItemService.Create | 閲覧者はタスクを作れない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 91 | TaskItemService.Create | 20件を並行して作成しても番号は重複も欠番もない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 92 | TaskItemService.Update | 古い版は409で他の人の変更を上書きしない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 93 | TaskItemService.Update | 完了にすると完了日時を設定し作成者にお知らせし未着手に戻すと消す | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 94 | TaskItemService.Update | 変更した項目ごとに履歴を残し変わらない項目は残さない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 95 | TaskItemService.Update | nullを送ると値を消し送らない項目は変えない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 96 | TaskItemService.Update | ステータスにnullは指定できない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 97 | TaskItemService.Update | サブタスクを持つタスクは子にできず自分自身も親にできない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 98 | TaskItemService.Delete | 編集者は自分が作ったタスクだけ削除でき管理者は誰のものでも削除できる | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 99 | TaskItemService.Delete | サブタスクがあれば409でまとめて削除を指定すれば消す | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 100 | TaskItemService.Get | 他プロジェクトのタスク番号はURLのプロジェクトでは見つからない | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 101 | TaskItemService.List | 既定は未完了だけで完了も含める指定ができる | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 102 | TaskItemService.List | タイトル検索の記号は文字として扱う | 4 通り（"%", 1; "_", 1; "' OR 1=1 --", 0; "100%", 1） | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 103 | TaskItemService.List | 期限切れの絞り込みは利用者の今日を基準にする | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 104 | TaskItemService.List | 許可された並べ替えはすべてSQLに変換できる | 11 通り（"-updatedAt"; "number"; "-number"; "dueDate" …） | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 105 | TaskItemService.List | 許可されていない並べ替えは400 | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 106 | TaskItemService.List | 担当者と担当者なしの絞り込み | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 107 | TaskItemService.List | ページングは100件まで | — | [TaskItemServiceTests.cs](../../tests/TaskManager.Application.Tests/Tasks/TaskItemServiceTests.cs) |
| 108 | UserAdministrationService.Create | 一時パスワードを発行し初回ログインで変更を求める | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 109 | UserAdministrationService.Create | 同じメールアドレスは409 | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 110 | UserAdministrationService.Deactivate | 自分自身は停止できない | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 111 | UserAdministrationService.ResetPassword | セッションを失効し一時パスワード状態にする | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 112 | UserAdministrationService.Unlock | ロックを解除する | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 113 | UserAdministrationService.Update | 古い版は409 | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 114 | UserAdministrationService.List | 許可されていない並べ替えは400 | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 115 | LastAdministrator | 最後の有効な管理者は停止も降格もできない | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 116 | LastAdministrator | 初期管理者は管理者がいない場合だけ作成する | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 117 | LastAdministrator | 初期管理者の設定が不正なら作成しない | 3 通り（"admin@example.test", "short"; "not-an-email", "Bootstrap-Admin-2026"; null, null） | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 118 | MyAccountService.UpdateProfile | 不正なタイムゾーンは項目エラー | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |
| 119 | MyAccountService.UpdateProfile | 表示名を変える | — | [UserServiceTests.cs](../../tests/TaskManager.Application.Tests/Users/UserServiceTests.cs) |

### Infrastructure（DB: MySQL）

| # | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| 1 | PersistenceBehavior | 採番・20並列でも重複と欠番がなくプロジェクトごとに独立する | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 2 | PersistenceBehavior | 採番・アーカイブ中はnullでトランザクション外は例外 | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 3 | PersistenceBehavior | 採番・トランザクションを取り消すと番号も戻る | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 4 | PersistenceBehavior | 楽観的排他・同じ版からの2回目の保存は失敗する | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 5 | PersistenceBehavior | 監査列・作成者と更新者と日時を設定する | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 6 | PersistenceBehavior | 型変換・日付と日時と絵文字を往復できる | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 7 | PersistenceBehavior | 一意制約・同じプロジェクトキーは保存できない | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 8 | PersistenceBehavior | CHECK制約・不正な値はDBでも拒否する | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 9 | PersistenceBehavior | 外部キー・タスクが使っているステータスは直接削除できない | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 10 | PersistenceBehavior | 外部キー・親タスクを削除するとサブタスクも削除される | — | [PersistenceBehaviorTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/PersistenceBehaviorTests.cs) |
| 11 | Schema | マイグレーションがすべて適用されモデルとの差分がない | — | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 12 | Schema | 設計どおりの14表がある | — | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 13 | Schema | 文字コードはutf8mb4 | — | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 14 | Schema | CHECK制約が11件ある | — | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 15 | Schema | 一意制約がある | 5 通り（"user_account", "email", "ux_user_account__email"; "project", "project_key", "ux_project__project_key"; "task_item", "project_id,task_number", "ux_task_item__project_id__task_number"; "project_membership", "project_id,user_id", "ux_project_membership__project_id__user_id" …） | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 16 | Schema | 外部キーの削除時の挙動 | 5 通り（"task_item", "workflow_status", "RESTRICT"; "task_item", "milestone", "SET NULL"; "task_item_tag", "task_item", "CASCADE"; "task_item", "task_item", "CASCADE" …） | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 17 | Schema | 日時列はDATETIME6で日付列はDATE | — | [SchemaTests.cs](../../tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs) |
| 18 | LocalAttachmentStorage.Save | 年月とIDのキーで保存し読み出し削除できる | — | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 19 | LocalAttachmentStorage.Save | 同じIDで上書きしない | — | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 20 | LocalAttachmentStorage | ルートの外を指すキーは拒否する | 4 通り（"../outside"; "2026/../../outside"; "/etc/passwd"; ""） | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 21 | AttachmentContentInspector.Inspect | 許可された形式で中身が一致する | データ Allowed | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 22 | AttachmentContentInspector.Inspect | 許可外の形式や中身の偽装は拒否する | データ Rejected | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 23 | SecurityPrimitive.PasswordHasher | ハッシュは平文を含まず検証できる | — | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 24 | SecurityPrimitive.RefreshToken | 十分な長さの乱数でハッシュは決定的 | — | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |
| 25 | SecurityPrimitive.TemporaryPassword | パスワードポリシーを満たし紛らわしい文字を含まない | — | [AttachmentStorageTests.cs](../../tests/TaskManager.Infrastructure.Tests/Storage/AttachmentStorageTests.cs) |

### API（DB: MySQL + TestServer）

| # | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| 1 | AuthApi | ログインに成功するとトークンを返しリフレッシュCookieは安全な属性で設定する | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 2 | AuthApi | ログイン失敗は存在の有無にかかわらず同じ401 | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 3 | AuthApi | リフレッシュCookieでトークンを更新し使用済みCookieの再利用は失効させる | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 4 | AuthApi | 別オリジンからのリフレッシュとログアウトは403 | 4 通り（"https://evil.example.test", null; "null", null; null, "cross-site"; null, null） | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 5 | AuthApi | Originがなく同一オリジンのSecFetchSiteなら許可する | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 6 | AuthApi | ログアウトするとCookieを削除しリフレッシュできなくなる | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 7 | AuthApi | トークンなしは401で日本語のProblemDetailsを返す | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 8 | AuthApi | 不正なトークンは401 | データ TamperedTokens | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 9 | AuthApi | 正しく署名された有効なトークンは受け付ける | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 10 | AuthApi | パスワードを変更すると変更前のアクセストークンは使えなくなる | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 11 | AuthApi | 利用停止されるとアクセストークンは即座に使えなくなる | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 12 | AuthApi | 一時パスワード状態では変更以外の操作は403 | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 13 | AuthApi | ログインは同じIPから1分に10回までで11回目は429とRetryAfter | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 14 | AuthApi | ログインに5回失敗するとロックされ正しいパスワードでも401・locked | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 15 | AuthApi | ログイン中のログにパスワードとトークンを出さない | — | [AuthApiTests.cs](../../tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs) |
| 16 | AuthorizationMatrix | OpenAPI文書の全operationIdがマトリクスにある | — | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 17 | AuthorizationMatrix | 許可された場合のステータスは文書に定義されている | — | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 18 | AuthorizationMatrix | 呼び出し元ごとに期待どおりのステータスを返す | データ Cases | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 19 | AuthorizationMatrix | システム管理者はアーカイブ済みのプロジェクトを削除できる | — | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 20 | AuthorizationMatrix | 編集者は自分が作ったタスクとコメントと添付を削除できる | — | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 21 | AuthorizationMatrix | メンバーは自分でプロジェクトから脱退できる | — | [AuthorizationMatrixTests.cs](../../tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrixTests.cs) |
| 22 | IdorApi | 子リソースのパスに他プロジェクトのIDを指定すると404 | — | [IdorApiTests.cs](../../tests/TaskManager.Api.Tests/Authorization/IdorApiTests.cs) |
| 23 | IdorApi | 本文に他プロジェクトのIDを指定すると400 | — | [IdorApiTests.cs](../../tests/TaskManager.Api.Tests/Authorization/IdorApiTests.cs) |
| 24 | IdorApi | 他人宛てのお知らせは既読にできない | — | [IdorApiTests.cs](../../tests/TaskManager.Api.Tests/Authorization/IdorApiTests.cs) |
| 25 | IdorApi | 一般利用者は利用者管理APIで他人の情報を見られない | — | [IdorApiTests.cs](../../tests/TaskManager.Api.Tests/Authorization/IdorApiTests.cs) |
| 26 | MainFlowApi | 利用者の発行からタスクの完了までの流れ | — | [MainFlowApiTests.cs](../../tests/TaskManager.Api.Tests/Flows/MainFlowApiTests.cs) |
| 27 | OpenApiDocument | 実行中のAPIが公開するOpenAPI文書はコミット済みのスナップショットと一致する | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 28 | OpenApiDocument | ビルド時に生成したOpenAPI文書もスナップショットと一致する | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 29 | OpenApiDocument | 全操作にタグ・要約・operationIdがあり匿名で呼べるのはログインとシステム情報だけ | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 30 | OpenApiDocument | 全操作が500とエラー応答をProblemDetailsで定義する | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 31 | OpenApiDocument | 要求のスキーマに文字数の制約がある | 6 通り（"CreateTaskRequest", "title", 1, 200; "CreateProjectRequest", "key", 2, 10; "CreateProjectRequest", "name", 1, 80; "CreateTaskCommentRequest", "body", 1, 10000 …） | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 32 | OpenApiDocument | 更新要求の版は必須 | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 33 | OpenApiDocument | 文書に秘密情報や内部情報を含まない | 9 通り（"plane"; "signingkey"; "connectionstring"; "password=" …） | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 34 | OpenApiDocument | SwaggerUIとOpenAPI文書は有効時にログインなしで表示できる | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 35 | OpenApiDocument | 検証器は型の違いと必須項目の欠落と文書にない項目を検出する | — | [OpenApiDocumentTests.cs](../../tests/TaskManager.Api.Tests/OpenApi/OpenApiDocumentTests.cs) |
| 36 | SecurityApi | API応答にセキュリティヘッダーを付ける | 2 通り（"/api/v1/system/info"; "/api/v1/me"） | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 37 | SecurityApi | 想定外の例外は500で内部情報を返さず追跡IDを返す | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 38 | SecurityApi | 添付のダウンロードは保存を強制し中身の種類を推測させない | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 39 | SecurityApi | アップロードのファイル名からパスを取り除く | 3 通り（"../../etc/passwd.txt", "passwd.txt"; "..\\..\\windows\\boot.txt", "boot.txt"; "/var/lib/taskmanager/attachments/x.txt", "x.txt"） | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 40 | SecurityApi | 許可されていない形式の添付は415 | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 41 | SecurityApi | 要求の上限を超えるアップロードは413・attachment・too・large | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 42 | SecurityApi | ファイルなしのアップロードは400 | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 43 | SecurityApi | ヘルスチェックはログインなしで応答し内部情報を含まない | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 44 | SecurityApi | システム情報は名前と版だけを返す | — | [SecurityApiTests.cs](../../tests/TaskManager.Api.Tests/Security/SecurityApiTests.cs) |
| 45 | ValidationApi | タスク作成の不正な入力は項目ごとの400 | 12 通り（"""{"title":""}""", "title"; """{"title":"   "}""", "title"; """{"priority":"normal"}""", "title"; """{"title":"a","priority":"highest"}""", "priority" …） | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 46 | ValidationApi | タイトルは200文字まで受け付け201文字は400 | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 47 | ValidationApi | JSONとして読めない本文は400 | 3 通り（"{"; "[1,2,3]"; "not json"） | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 48 | ValidationApi | 深すぎる入れ子のJSONは400 | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 49 | ValidationApi | 未知のプロパティは無視する | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 50 | ValidationApi | 更新の版は数値で必須 | 3 通り（"""{"title":"版なし"}"""; """{"title":"文字列の版","version":"0"}"""; """{"title":"負の版","version":-1}"""） | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 51 | ValidationApi | 一覧の不正なクエリは400 | 7 通り（"pageSize=0"; "pageSize=101"; "page=0"; "priority=highest" …） | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 52 | ValidationApi | 存在しないタスク番号は404でProblemDetails | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 53 | ValidationApi | 楽観的排他の競合は409 | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |
| 54 | ValidationApi | 検索語の記号やSQL断片はそのまま文字として扱う | — | [ValidationApiTests.cs](../../tests/TaskManager.Api.Tests/Validation/ValidationApiTests.cs) |

### Frontend（Vitest + Testing Library + MSW）

| # | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| 1 | call（API 呼び出しの共通処理） | 成功時はデータを返し、アクセストークンを付けて送る | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 2 | call（API 呼び出しの共通処理） | Problem Details を ApiError に変換する | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 3 | call（API 呼び出しの共通処理） | 通信できない場合は日本語のエラー（status 0） | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 4 | call（API 呼び出しの共通処理） | 401 ならトークンを 1 回だけ更新して再送する | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 5 | call（API 呼び出しの共通処理） | 同時に 401 になってもトークンの更新は 1 回にまとめる | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 6 | call（API 呼び出しの共通処理） | 更新できなければトークンを消してセッション切れを知らせる | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 7 | call（API 呼び出しの共通処理） | 一時パスワード状態のエラーを知らせる | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 8 | トークンの保持 | アクセストークンはメモリだけに置き、ブラウザの保存領域に書かない | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 9 | authorizedFetch（ファイルの送受信） | トークンを付け、401 なら更新して再送する | — | [client.test.ts](../../frontend/task-manager-web/src/api/client.test.ts) |
| 10 | MarkdownView（XSS 対策。security-design.md S-08） | Markdown の書式を表示する | — | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 11 | MarkdownView（XSS 対策。security-design.md S-08） | 生の HTML（script・イベント属性）は要素として描画しない | — | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 12 | MarkdownView（XSS 対策。security-design.md S-08） | 入力ごとの期待値（危険なリンク ◯ はリンクにしない） | パラメータ化（it.each） | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 13 | MarkdownView（XSS 対策。security-design.md S-08） | 外部リンクは新しいタブで開き、参照元を渡さない | — | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 14 | MarkdownView（XSS 対策。security-design.md S-08） | 画面内のリンクは同じタブで開く | — | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 15 | MarkdownView（XSS 対策。security-design.md S-08） | 画像は読み込まずリンクとして表示する | — | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 16 | safeUrl | 入力ごとの期待値（◯ → ◯） | パラメータ化（it.each） | [MarkdownView.test.tsx](../../frontend/task-manager-web/src/components/MarkdownView.test.tsx) |
| 17 | ログイン画面（SCR-01） | 未ログインでログイン必須の画面を開くと、元の URL を保持してログイン画面へ移動する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 18 | ログイン画面（SCR-01） | 未入力で送信すると項目ごとのメッセージを出し、API は呼ばない | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 19 | ログイン画面（SCR-01） | 認証に失敗するとエラーを表示しパスワード欄を空にする | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 20 | ログイン画面（SCR-01） | ロック中は理由と対処を表示する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 21 | ログイン画面（SCR-01） | 短時間に何度も試すと待つよう案内する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 22 | ログイン画面（SCR-01） | ログインに成功すると元の画面へ戻る | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 23 | ログイン画面（SCR-01） | 外部サイトへの next は無視してホームへ移動する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 24 | パスワード変更（SCR-02） | 一時パスワード状態ではどの画面を開いても変更画面へ移動する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 25 | パスワード変更（SCR-02） | 確認用が一致しない・短すぎる場合はその場で知らせる | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 26 | パスワード変更（SCR-02） | API の項目エラー（現在のパスワード違い）を該当欄に表示する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 27 | パスワード変更（SCR-02） | 変更に成功するとホームへ移動する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 28 | 権限による画面の制御 | 一般利用者が利用者管理を開くとホームへ戻す | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 29 | 権限による画面の制御 | システム管理者にはメニューに利用者管理を表示する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 30 | 権限による画面の制御 | 存在しない・所属していないプロジェクトは「見つかりません」と表示する | — | [auth.test.tsx](../../frontend/task-manager-web/src/features/auth/auth.test.tsx) |
| 31 | 自分のタスク（SCR-04） | 担当タスクがなければ空の状態を示す | — | [MyTasksPage.test.tsx](../../frontend/task-manager-web/src/features/my-tasks/MyTasksPage.test.tsx) |
| 32 | 自分のタスク（SCR-04） | 期限の区分ごとに見出しを付けて表示する | — | [MyTasksPage.test.tsx](../../frontend/task-manager-web/src/features/my-tasks/MyTasksPage.test.tsx) |
| 33 | 自分のタスク（SCR-04） | 読み込みに失敗したらエラーを表示する | — | [MyTasksPage.test.tsx](../../frontend/task-manager-web/src/features/my-tasks/MyTasksPage.test.tsx) |
| 34 | タスク一覧（SCR-06） | タスクを表で表示し、タイトルから詳細へ移動できる | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 35 | タスク一覧（SCR-06） | 読み込み中は読み込み中であることを伝える | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 36 | タスク一覧（SCR-06） | タスクがなければ空の状態と追加の操作を示す | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 37 | タスク一覧（SCR-06） | 読み込みに失敗したら再試行できる | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 38 | タスク一覧（SCR-06） | 閲覧者には編集の部品を表示しない | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 39 | タスク一覧（SCR-06） | アーカイブ中は管理者でも編集できないことを示す | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 40 | タスク一覧（SCR-06） | 表でステータスを変えると取得時の版を付けて保存する | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 41 | タスク一覧（SCR-06） | 他の人が先に更新していたら知らせる | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 42 | タスク一覧（SCR-06） | タイトルに HTML を含めても文字として表示する | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 43 | タスクの追加（SCR-09） | タイトルが空なら送信せずに知らせる | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 44 | タスクの追加（SCR-09） | タイトルだけで追加でき、追加したことと開くリンクを知らせる | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 45 | タスクの追加（SCR-09） | API の項目エラーを該当欄に表示する | — | [ProjectTasksPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/ProjectTasksPage.test.tsx) |
| 46 | タスク詳細（SCR-08） | 続けて 2 つの項目を変えても、直前の保存の版を使って順に保存する（自分の変更同士で競合しない） | — | [TaskDetailPage.test.tsx](../../frontend/task-manager-web/src/features/tasks/TaskDetailPage.test.tsx) |
| 47 | formatDue（期限の表示） | 入力ごとの期待値（◯ → ◯） | パラメータ化（it.each） | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 48 | formatDue（期限の表示） | 期限なし | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 49 | formatDue（期限の表示） | 完了・中止したタスクは超過と表示しない | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 50 | 日付の計算 | 年をまたいで日数を足す | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 51 | 日付の計算 | 日数の差 | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 52 | 日付の計算 | 入力ごとの期待値（来週の月曜日: ◯ → ◯） | パラメータ化（it.each） | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 53 | 日付の計算 | 別の年は年を付けて表示する | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 54 | todayIn（タイムゾーンでの今日） | 同じ時刻でもタイムゾーンで日付が変わる | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 55 | formatDateTime（日時の相対表示） | 入力ごとの期待値（◯ → ◯） | パラメータ化（it.each） | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 56 | formatDateTime（日時の相対表示） | UTC の利用者には UTC の日付で表示する | — | [dates.test.ts](../../frontend/task-manager-web/src/utils/dates.test.ts) |
| 57 | parseTaskKey（タスクキーの解析） | 入力ごとの期待値（◯） | パラメータ化（it.each） | [taskKey.test.ts](../../frontend/task-manager-web/src/utils/taskKey.test.ts) |
| 58 | parseTaskKey（タスクキーの解析） | 入力ごとの期待値（不正な形式 ◯ は null） | パラメータ化（it.each） | [taskKey.test.ts](../../frontend/task-manager-web/src/utils/taskKey.test.ts) |
| 59 | parseTaskKey（タスクキーの解析） | 画面のパス | — | [taskKey.test.ts](../../frontend/task-manager-web/src/utils/taskKey.test.ts) |
| 60 | formatBytes | 入力ごとの期待値（◯ → ◯） | パラメータ化（it.each） | [taskKey.test.ts](../../frontend/task-manager-web/src/utils/taskKey.test.ts) |
| 61 | safeNextPath（ログイン後の移動先。オープンリダイレクト対策） | 入力ごとの期待値（◯ → ◯） | パラメータ化（it.each） | [taskKey.test.ts](../../frontend/task-manager-web/src/utils/taskKey.test.ts) |


## 5. E2E のシナリオ

| ID | 対象 | 目的・期待結果 | 条件 | テストファイル |
|---|---|---|---|---|
| E2E-01 | 初期管理者・利用者管理・初回ログイン | 初期管理者が一時パスワードを変更し、利用者 2 人を発行する。発行した利用者が一時パスワードでログインし、パスワードを変更してホームが表示される | 空の DB + 初期管理者。利用者管理画面の axe 検査 | [main-flow.spec.ts](../../frontend/task-manager-e2e/tests/main-flow.spec.ts) |
| E2E-02 | プロジェクト・メンバー・タスク | プロジェクト作成 → 編集者・閲覧者の追加 → タイトルだけでタスク作成 → 詳細で担当者と期限を設定 → 担当者のホーム（自分のタスク）に表示される | E2E-01 の利用者 | 同上 |
| E2E-03 | ボード・キーボード操作 | ボードでステータスを変えると列が移る。N キーでタスク追加ダイアログが開き Esc で閉じる。表のステータスはキーボードでフォーカスして変更できる | 編集者 | 同上 |
| E2E-04 | コメント・お知らせ | 編集者のコメントで作成者に未読のお知らせが届き、すべて既読にできる。コメント内の `<script>` は実行されない | お知らせ画面の axe 検査 | 同上 |
| E2E-05 | 添付ファイル | CSV の添付 → ダウンロード（ファイル名が一致）→ HTML は拒否 → 削除 | 編集者 | 同上 |
| E2E-06 | 閲覧者の権限・存在しないプロジェクト | 閲覧者には追加・編集・コメント・添付の部品を表示しない。所属していないプロジェクトの URL は「ページが見つかりません」。利用者管理の URL はホームに戻る | 閲覧者（初回のパスワード変更を含む） | 同上 |
| E2E-07 | スマートフォン幅 | 390×844 で一覧がカード表示になり横スクロールが出ない。メニュー（ドロワー）の開閉、詳細の表示 | axe 検査 | 同上 |
| E2E-08 | アクセシビリティ | ログイン・自分のタスク・プロジェクト一覧・タスク一覧（表・ボード）・タスク詳細・マイルストーン・個人設定で、axe の serious / critical の違反がない | WCAG 2.1 A / AA | 同上 |

## 6. Phase 9 で追加したテスト（2026-10-16）

ガントチャート・ステータス別円グラフ（REQ-GNT-01〜03、REQ-CHT-01〜02、API #62〜#64）。方針は docs/design/testing-strategy.md §12。

| 種類 | ファイル | 件数 | 対象・観点 |
|---|---|---|---|
| Domain | `tests/TaskManager.Domain.Tests/Tasks/ScheduleWindowTests.cs` | 16 | 期間の検証（366 日・367 日・逆転・範囲外）、棒の範囲、期間との重なりの境界 |
| Domain | `tests/TaskManager.Domain.Tests/Common/ShareRoundingTests.cs` | 10 | 割合の合計 100.0、最大剰余法、0 件、合計 0 件、負の件数 |
| Application（MySQL） | `tests/TaskManager.Application.Tests/Tasks/TaskInsightServiceTests.cs` `TaskScheduleServiceTests` | 9 | 期間検索（COALESCE 条件）、未完了 / 全区分、日付未設定、タイムゾーンでの期限切れ、他プロジェクトを含めない、非メンバー 404・閲覧者・アーカイブ中、期間の検証 |
| Application（MySQL） | 同 `TaskStatusSummaryServiceTests` | 8 | ステータス別件数（0 件・並び順・追加したステータス）、割合、空、非メンバー 404、自分の担当の区分別（他人・アーカイブ中・非参加を含めない）、プロジェクト指定、存在しないプロジェクト 404 |
| Infrastructure（MySQL） | `tests/TaskManager.Infrastructure.Tests/Persistence/SchemaTests.cs` | 4 | 集計・期間検索に使う索引の列、マイグレーションの適用順 |
| API | `tests/TaskManager.Api.Tests/Flows/TaskInsightApiTests.cs` | 14 | 契約（OpenAPI の応答スキーマ）、`from` / `to` の検証 6 パターン、日程変更（既存 API・409・TK-02）、円グラフ、自分の円グラフ、IDOR（他プロジェクトのキー → 404）、`projectKey` の形式 |
| API | `tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrix.cs` | 24 | 3 操作 × 8 種類の呼び出し元 |
| Frontend | `src/utils/schedule.test.ts` | 16 | 期間・目盛り・棒・今日の線・ドラッグ後の日付・まとめ方・URL の値 |
| Frontend | `src/components/StatusPieChart.test.tsx` | 6 | 0 件の凡例、合計、要約、1 項目、空状態、リンク |
| Frontend | `src/features/tasks/TaskScheduleView.test.tsx` | 11 | ガント（取得条件・表示単位・前へ / 次へ・完了の表示・まとめ方・表での日程変更・開始日より前の期限・閲覧者・空・エラー・上限）、プロジェクトの円グラフ（リンク・空） |
| Frontend | `src/features/my-tasks/MyTasksPage.test.tsx` | 4 | 自分の円グラフ（区分別・プロジェクト指定・空・エラー） |
| E2E | `frontend/task-manager-e2e/tests/main-flow.spec.ts` E2E-09 | 1 | ガントの表示・表示単位（日・週・月）・表とドラッグでの日程変更・円グラフ・自分の円グラフ・axe・CSP |
