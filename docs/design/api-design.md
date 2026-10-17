# REST API 設計（OpenAPI 3.1 契約）

本書は TaskManager API の契約である。Phase 4 の実装は本書から生成される OpenAPI 文書（`/openapi/v1.json`）と一致させる（CLAUDE.md §13）。差異が出た場合は実装ではなく **本書を先に更新** してから実装を変える。

## 1. 共通規約

| 項目 | 規約 |
|---|---|
| ベース URL | `/api/v1` |
| 形式 | JSON（`application/json; charset=utf-8`）。添付のアップロードのみ `multipart/form-data`、ダウンロードは `application/octet-stream` 等 |
| 文字 | JSON プロパティは camelCase、列挙値は camelCase 文字列 |
| 日時 | ISO 8601 UTC（`2026-10-06T01:23:45.123Z`）。日付は `YYYY-MM-DD` |
| ID | GUID 文字列（小文字ハイフン付き）。プロジェクトはキー（`SALES`）、タスクはプロジェクト内番号（`12`）で指定 |
| 認証 | `Authorization: Bearer <accessToken>`（下記の例外を除く全 API） |
| null と省略 | 応答では値がない項目も `null` で返す（プロパティを省略しない） |
| 部分更新 | `PATCH` は送ったプロパティだけを更新。値を消す場合は `null` を明示（`dueDate: null`）。各 Request に `version` 必須（対象のみ） |
| 冪等性 | `GET` / `PUT` / `DELETE` は冪等。`DELETE` で既に存在しない場合は 404 |
| 末尾スラッシュ | 付けない |
| 文字数 | すべて Unicode の文字数（UTF-16 単位ではなく書記素ではない。.NET の `string.Length` 基準）で数える |

### 1.1 ページング

クエリ: `page`（1 以上、既定 1）、`pageSize`（1〜100、既定は API ごとに記載）

```json
{
  "items": [ ... ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 123,
  "totalPages": 3
}
```
OpenAPI 上のスキーマ名は `{要素型}Page`（例 `TaskSummaryPage`）。範囲外のページは空配列を返す（エラーにしない）。

### 1.2 並べ替え

クエリ `sort` に許可リストの値を 1 つ指定。先頭 `-` で降順。許可外は 400 `request.invalid_sort`。同値の場合はタスク番号の降順など API ごとの既定順で安定させる。期限など null を含む項目は昇順・降順とも null を最後にする。

### 1.3 フィルタ

配列はキーを繰り返す（`?statusId=a&statusId=b`、OpenAPI では `style: form, explode: true`）。異なるキーは AND、同じキーの複数値は OR。

### 1.4 エラー形式（RFC 9457 Problem Details）

`Content-Type: application/problem+json`

```json
{
  "type": "urn:taskmanager:problem:validation.failed",
  "title": "入力内容に誤りがあります。",
  "status": 400,
  "detail": "赤く表示された項目を確認してください。",
  "instance": "/api/v1/projects/SALES/tasks",
  "code": "validation.failed",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "title": ["タイトルを入力してください。"],
    "dueDate": ["期限は開始日以降の日付にしてください。"]
  }
}
```

- `code` は機械判定用（§1.5）。`title` / `detail` / `errors` は利用者に見せてよい日本語。
- 500 では内部情報（例外メッセージ・スタックトレース・SQL）を一切含めない。`detail` は「時間をおいて再度お試しください。問題が続く場合は管理者に連絡してください。」
- `errors` のキーは Request の JSON プロパティ名（camelCase）。

### 1.5 エラーコード一覧

| code | HTTP | title（日本語） | 発生場面 |
|---|---|---|---|
| `validation.failed` | 400 | 入力内容に誤りがあります。 | DataAnnotations・業務検証 |
| `request.malformed` | 400 | リクエストの形式が正しくありません。 | JSON 構文エラー、型不一致 |
| `request.invalid_sort` | 400 | 指定された並べ替え項目は使用できません。 | |
| `auth.invalid_credentials` | 401 | メールアドレスまたはパスワードが正しくありません。 | ログイン失敗（存在しないメールも同じ） |
| `auth.locked` | 401 | ログインに続けて失敗したため、一時的にロックされています。 | ロック中。`detail` に「15 分ほど待ってから再度お試しください。急ぎの場合はシステム管理者に解除を依頼してください。」 |
| `auth.unauthenticated` | 401 | ログインが必要です。 | トークンなし・無効・期限切れ |
| `auth.session_expired` | 401 | ログインの有効期限が切れました。もう一度ログインしてください。 | リフレッシュ失敗 |
| `auth.password_change_required` | 403 | パスワードの変更が必要です。 | 一時パスワード状態 |
| `auth.current_password_invalid` | 400 | 現在のパスワードが正しくありません。 | |
| `auth.csrf_rejected` | 403 | 不正なリクエストのため処理できませんでした。 | Origin 不一致（refresh / logout） |
| `access.forbidden` | 403 | この操作を行う権限がありません。 | ロール不足 |
| `resource.not_found` | 404 | 対象が見つかりません。 | 存在しない、または参照権限がない |
| `project.archived` | 409 | アーカイブされたプロジェクトは変更できません。 | |
| `project.key_taken` | 409 | このプロジェクトキーは既に使われています。 | |
| `project.name_taken` | 409 | 同じ名前のプロジェクトが既にあります。 | |
| `project.not_archived` | 409 | 削除する前にプロジェクトをアーカイブしてください。 | |
| `member.already_exists` | 409 | この利用者は既にメンバーです。 | |
| `member.user_inactive` | 400 | 利用停止中の利用者は追加できません。 | |
| `member.last_owner` | 409 | 管理者が 1 人もいなくなるため、この操作はできません。 | |
| `status.name_taken` | 409 | 同じ名前のステータスが既にあります。 | |
| `status.limit_reached` | 409 | ステータスは 20 個までです。 | |
| `status.is_default` | 409 | 既定のステータスは削除できません。先に別のステータスを既定にしてください。 | |
| `status.last_one` | 409 | 最後のステータスは削除できません。 | （Phase 4 で追加） |
| `status.in_use` | 409 | このステータスのタスクがあります。移動先のステータスを選んでください。 | |
| `tag.name_taken` | 409 | 同じ名前のタグが既にあります。 | |
| `tag.limit_reached` | 409 | タグは 50 個までです。 | |
| `milestone.name_taken` | 409 | 同じ名前のマイルストーンが既にあります。 | |
| `milestone.closed` | 409 | 終了したマイルストーンにはタスクを追加できません。 | |
| `task.has_subtasks` | 409 | サブタスクがあるため削除できません。サブタスクもまとめて削除するか、先にサブタスクを移動してください。 | |
| `attachment.too_large` | 413 | ファイルサイズが上限（10MB）を超えています。 | |
| `request.too_large` | 413 | 送信できるデータの上限を超えています。 | 添付以外の要求本文が 1MB を超えた場合（サーバー全体の制限。Phase 7 で追加） |
| `attachment.type_not_allowed` | 415 | この種類のファイルは添付できません。 | |
| `attachment.limit_reached` | 409 | 1 つのタスクに添付できるファイルは 20 個までです。 | |
| `user.email_taken` | 409 | このメールアドレスは既に登録されています。 | |
| `user.last_administrator` | 409 | システム管理者が 1 人もいなくなるため、この操作はできません。 | |
| `user.cannot_deactivate_self` | 409 | 自分自身を利用停止にはできません。 | |
| `concurrency.conflict` | 409 | 他の人が先に更新しました。 | `detail`「最新の内容を読み込んでから、もう一度操作してください。」 |
| `rate_limit.exceeded` | 429 | 短時間にリクエストが集中しました。 | `Retry-After` ヘッダー付き |
| `server.error` | 500 | 処理中にエラーが発生しました。 | |

### 1.6 存在しない vs 権限なし

- プロジェクトの **メンバーでない** 利用者がプロジェクト配下にアクセス → **404**（存在の有無を漏らさない）
- メンバーだがロールが不足 → **403**
- システム管理者専用 API に一般利用者 → 403

## 2. OpenAPI 構成要素

### 2.1 info / servers

```yaml
openapi: 3.1.0
info:
  title: TaskManager API
  version: v1
  description: チームのタスクを管理するための REST API。
servers:
  - url: /
```
（Plane の連絡先・ライセンス・サーバー URL 等は含めない）

### 2.2 セキュリティスキーム

```yaml
components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: /api/v1/auth/login で取得したアクセストークン（有効期限 15 分）。
    refreshCookie:
      type: apiKey
      in: cookie
      name: tm_refresh
      description: HttpOnly のリフレッシュトークン Cookie。/api/v1/auth/refresh と /api/v1/auth/logout のみで使用。
security:
  - bearerAuth: []
```
- `login` は `security: []`、`refresh` / `logout` は `security: [{ refreshCookie: [] }]`。
- 各 operation の説明（`description`）末尾に必要な権限を「必要な権限: プロジェクトの編集者以上」の形式で記載する。OpenAPI 拡張 `x-required-permission`（例 `EditTasks`）も付ける。

### 2.3 タグ

| タグ | 説明（OpenAPI の description） |
|---|---|
| `Auth` | ログイン・ログアウト・トークン更新・パスワード変更 |
| `Me` | ログイン中の利用者自身の情報 |
| `Users` | 利用者管理（システム管理者のみ） |
| `UserDirectory` | メンバー追加用の利用者検索 |
| `Projects` | プロジェクト |
| `ProjectMembers` | プロジェクトのメンバーとロール |
| `WorkflowStatuses` | プロジェクトのステータス定義 |
| `Tags` | プロジェクトのタグ定義 |
| `Milestones` | マイルストーン |
| `Tasks` | タスク |
| `TaskComments` | タスクのコメント |
| `TaskAttachments` | タスクの添付ファイル |
| `TaskHistory` | タスクの変更履歴 |
| `MyTasks` | 自分のタスク |
| `Notices` | お知らせ |
| `Search` | 検索 |
| `System` | システム情報（環境確認用。Phase 3 で追加） |

### 2.4 共通パラメータ

| 名前 | 位置 | スキーマ |
|---|---|---|
| `projectKey` | path | string, pattern `^[A-Za-z][A-Za-z0-9]{1,9}$`（大文字小文字を問わず受け付け、大文字化して扱う） |
| `taskNumber` | path | integer, minimum 1, maximum 999999999 |
| `userId`, `statusId`, `tagId`, `milestoneId`, `commentId`, `attachmentId`, `noticeId` | path | string, format uuid |
| `page` | query | integer, minimum 1, default 1 |
| `pageSize` | query | integer, minimum 1, maximum 100 |

### 2.5 共通スキーマ

| スキーマ | プロパティ |
|---|---|
| `ProblemDetails` | `type` string, `title` string, `status` integer, `detail` string?, `instance` string?, `code` string, `traceId` string, `errors` object<string, string[]>? |
| `UserRef` | `id` uuid, `displayName` string, `isActive` boolean |
| `StatusRef` | `id` uuid, `name` string, `category` StatusCategory, `color` StatusColor |
| `TagRef` | `id` uuid, `name` string, `color` StatusColor |
| `MilestoneRef` | `id` uuid, `name` string, `state` MilestoneState |
| `TaskRef` | `key` string（`SALES-12`）, `number` integer, `title` string, `statusCategory` StatusCategory |
| `StatusCountResponse`（Phase 9） | `statusId` uuid?（区分別の集計では null）, `name` string, `category` StatusCategory, `color` StatusColor, `sortOrder` integer, `count` integer, `percent` number（小数第 1 位） |

列挙スキーマ（文字列）:
- `SystemRole`: `member`, `administrator`
- `ProjectRole`: `viewer`, `editor`, `owner`
- `StatusCategory`: `notStarted`, `inProgress`, `done`, `canceled`
- `StatusColor`: `gray`, `blue`, `teal`, `green`, `yellow`, `orange`, `red`, `purple`, `pink`
- `TaskPriority`: `low`, `normal`, `high`, `urgent`
- `MilestoneState`: `open`, `closed`
- `TaskHistoryKind`: `created`, `titleChanged`, `descriptionChanged`, `statusChanged`, `priorityChanged`, `assigneeChanged`, `startDateChanged`, `dueDateChanged`, `milestoneChanged`, `parentChanged`, `tagsChanged`, `attachmentAdded`, `attachmentRemoved`
- `NoticeKind`: `taskAssigned`, `taskCommented`, `taskCompleted`, `addedToProject`
- `UserStatusFilter`: `active`, `inactive`, `locked`
- `MyTaskBucket`: `overdue`, `today`, `thisWeek`, `later`, `noDueDate`
- `StatusSummaryGrouping`（Phase 9）: `status`（ステータス別）, `category`（ステータスの区分別）

## 3. エンドポイント一覧

| # | Method | Route | operationId | タグ | 権限 |
|---|---|---|---|---|---|
| 1 | POST | `/api/v1/auth/login` | `Login` | Auth | なし |
| 2 | POST | `/api/v1/auth/refresh` | `RefreshAccessToken` | Auth | refresh Cookie |
| 3 | POST | `/api/v1/auth/logout` | `Logout` | Auth | refresh Cookie |
| 4 | POST | `/api/v1/auth/password` | `ChangePassword` | Auth | 本人 |
| 5 | GET | `/api/v1/me` | `GetMe` | Me | 本人 |
| 6 | PATCH | `/api/v1/me` | `UpdateMe` | Me | 本人 |
| 7 | GET | `/api/v1/users` | `ListUsers` | Users | SA |
| 8 | POST | `/api/v1/users` | `CreateUser` | Users | SA |
| 9 | GET | `/api/v1/users/{userId}` | `GetUser` | Users | SA |
| 10 | PATCH | `/api/v1/users/{userId}` | `UpdateUser` | Users | SA |
| 11 | POST | `/api/v1/users/{userId}/deactivate` | `DeactivateUser` | Users | SA |
| 12 | POST | `/api/v1/users/{userId}/activate` | `ActivateUser` | Users | SA |
| 13 | POST | `/api/v1/users/{userId}/unlock` | `UnlockUser` | Users | SA |
| 14 | POST | `/api/v1/users/{userId}/password-reset` | `ResetUserPassword` | Users | SA |
| 15 | GET | `/api/v1/user-directory` | `SearchUserDirectory` | UserDirectory | いずれかの OW、または SA |
| 16 | GET | `/api/v1/projects` | `ListProjects` | Projects | 認証済み |
| 17 | POST | `/api/v1/projects` | `CreateProject` | Projects | 認証済み |
| 18 | GET | `/api/v1/projects/{projectKey}` | `GetProject` | Projects | VW 以上 |
| 19 | PATCH | `/api/v1/projects/{projectKey}` | `UpdateProject` | Projects | OW |
| 20 | POST | `/api/v1/projects/{projectKey}/archive` | `ArchiveProject` | Projects | OW |
| 21 | POST | `/api/v1/projects/{projectKey}/unarchive` | `UnarchiveProject` | Projects | OW |
| 22 | DELETE | `/api/v1/projects/{projectKey}` | `DeleteProject` | Projects | SA |
| 23 | GET | `/api/v1/projects/{projectKey}/members` | `ListProjectMembers` | ProjectMembers | VW 以上 |
| 24 | POST | `/api/v1/projects/{projectKey}/members` | `AddProjectMember` | ProjectMembers | OW / SA |
| 25 | PATCH | `/api/v1/projects/{projectKey}/members/{userId}` | `ChangeProjectMemberRole` | ProjectMembers | OW / SA |
| 26 | DELETE | `/api/v1/projects/{projectKey}/members/{userId}` | `RemoveProjectMember` | ProjectMembers | OW / SA / 本人 |
| 27 | GET | `/api/v1/projects/{projectKey}/statuses` | `ListWorkflowStatuses` | WorkflowStatuses | VW 以上 |
| 28 | POST | `/api/v1/projects/{projectKey}/statuses` | `CreateWorkflowStatus` | WorkflowStatuses | OW |
| 29 | PATCH | `/api/v1/projects/{projectKey}/statuses/{statusId}` | `UpdateWorkflowStatus` | WorkflowStatuses | OW |
| 30 | PUT | `/api/v1/projects/{projectKey}/statuses/order` | `ReorderWorkflowStatuses` | WorkflowStatuses | OW |
| 31 | DELETE | `/api/v1/projects/{projectKey}/statuses/{statusId}` | `DeleteWorkflowStatus` | WorkflowStatuses | OW |
| 32 | GET | `/api/v1/projects/{projectKey}/tags` | `ListTags` | Tags | VW 以上 |
| 33 | POST | `/api/v1/projects/{projectKey}/tags` | `CreateTag` | Tags | ED 以上 |
| 34 | PATCH | `/api/v1/projects/{projectKey}/tags/{tagId}` | `UpdateTag` | Tags | ED 以上 |
| 35 | DELETE | `/api/v1/projects/{projectKey}/tags/{tagId}` | `DeleteTag` | Tags | ED 以上 |
| 36 | GET | `/api/v1/projects/{projectKey}/milestones` | `ListMilestones` | Milestones | VW 以上 |
| 37 | POST | `/api/v1/projects/{projectKey}/milestones` | `CreateMilestone` | Milestones | ED 以上 |
| 38 | GET | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | `GetMilestone` | Milestones | VW 以上 |
| 39 | PATCH | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | `UpdateMilestone` | Milestones | ED 以上 |
| 40 | DELETE | `/api/v1/projects/{projectKey}/milestones/{milestoneId}` | `DeleteMilestone` | Milestones | ED 以上 |
| 41 | GET | `/api/v1/projects/{projectKey}/tasks` | `ListTasks` | Tasks | VW 以上 |
| 42 | POST | `/api/v1/projects/{projectKey}/tasks` | `CreateTask` | Tasks | ED 以上 |
| 43 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | `GetTask` | Tasks | VW 以上 |
| 44 | PATCH | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | `UpdateTask` | Tasks | ED 以上 |
| 45 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}` | `DeleteTask` | Tasks | OW、または作成者の ED |
| 46 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/history` | `ListTaskHistory` | TaskHistory | VW 以上 |
| 47 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments` | `ListTaskComments` | TaskComments | VW 以上 |
| 48 | POST | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments` | `CreateTaskComment` | TaskComments | ED 以上 |
| 49 | PATCH | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}` | `UpdateTaskComment` | TaskComments | 投稿者本人（ED 以上） |
| 50 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/comments/{commentId}` | `DeleteTaskComment` | TaskComments | 投稿者本人（ED 以上）または OW |
| 51 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments` | `ListTaskAttachments` | TaskAttachments | VW 以上 |
| 52 | POST | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments` | `UploadTaskAttachment` | TaskAttachments | ED 以上 |
| 53 | GET | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments/{attachmentId}/content` | `DownloadTaskAttachment` | TaskAttachments | VW 以上 |
| 54 | DELETE | `/api/v1/projects/{projectKey}/tasks/{taskNumber}/attachments/{attachmentId}` | `DeleteTaskAttachment` | TaskAttachments | 登録者本人（ED 以上）または OW |
| 55 | GET | `/api/v1/me/tasks` | `GetMyTasks` | MyTasks | 本人 |
| 56 | GET | `/api/v1/me/notices` | `ListMyNotices` | Notices | 本人 |
| 57 | GET | `/api/v1/me/notices/unread-count` | `GetMyUnreadNoticeCount` | Notices | 本人 |
| 58 | POST | `/api/v1/me/notices/{noticeId}/read` | `MarkNoticeRead` | Notices | 本人 |
| 59 | POST | `/api/v1/me/notices/read-all` | `MarkAllNoticesRead` | Notices | 本人 |
| 60 | GET | `/api/v1/search/tasks` | `SearchTasks` | Search | 認証済み |
| 61 | GET | `/api/v1/system/info` | `GetSystemInfo` | System | なし（匿名） |
| 62 | GET | `/api/v1/projects/{projectKey}/tasks/schedule` | `GetTaskSchedule` | Tasks | VW 以上（Phase 9） |
| 63 | GET | `/api/v1/projects/{projectKey}/tasks/status-summary` | `GetTaskStatusSummary` | Tasks | VW 以上（Phase 9） |
| 64 | GET | `/api/v1/me/tasks/status-summary` | `GetMyTaskStatusSummary` | MyTasks | 本人（Phase 9） |

OpenAPI 対象外: `/health/live`, `/health/ready`（`ExcludeFromDescription`）。

全エンドポイント共通の追加エラー: 401 `auth.unauthenticated`（認証が必要なもの）、403 `auth.password_change_required`（#4, #5, #3 以外）、429 `rate_limit.exceeded`、500 `server.error`。以下の各定義では省略する。
プロジェクト配下（#18〜#54、#62、#63）共通: 404 `resource.not_found`（プロジェクトなし・非メンバー）、403 `access.forbidden`（ロール不足）、更新系は 409 `project.archived`。

---

## 4. エンドポイント詳細

### 4.1 Auth

#### #1 POST /api/v1/auth/login — ログイン
- 認証: 不要。レート制限: IP あたり 10 回/分（`auth` ポリシー）
- Request `LoginRequest`

| プロパティ | 型 | 必須 | 検証 |
|---|---|---|---|
| `email` | string | ○ | 1〜254 文字 |
| `password` | string | ○ | 1〜128 文字 |

- 処理: [authentication-authorization.md](authentication-authorization.md) §3.1
- Response 200 `LoginResponse`: `accessToken` string, `expiresIn` integer（秒, 900）, `user` `CurrentUserResponse`
- Set-Cookie: `tm_refresh=...; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth; Max-Age=1209600`
- エラー: 400 `validation.failed`、401 `auth.invalid_credentials` / `auth.locked`、429
- DB: `user_account`（失敗回数・ロック・最終ログイン）更新、`refresh_session` 追加、`security_audit_event` 追加

#### #2 POST /api/v1/auth/refresh — アクセストークン更新
- 認証: `tm_refresh` Cookie。追加検証: `Origin` ヘッダーが許可オリジン（同一オリジン）と一致すること（なければ `Sec-Fetch-Site: same-origin` を要求）
- Request: なし
- Response 200 `LoginResponse`（新トークン）+ 新 Cookie
- エラー: 401 `auth.session_expired`（Cookie なし・無効・期限切れ・再利用検知）、403 `auth.csrf_rejected`
- DB: 旧 `refresh_session.rotated_at_utc` 設定、新行追加。再利用検知時は同系列を全失効 + 監査記録

#### #3 POST /api/v1/auth/logout — ログアウト
- 認証: `tm_refresh` Cookie（無効でも 204 を返し Cookie を削除）。Origin 検証は #2 と同じ
- Response 204、`Set-Cookie: tm_refresh=; Max-Age=0; ...`
- DB: 現在のセッションを失効、監査記録

#### #4 POST /api/v1/auth/password — パスワード変更
- 認証: Bearer（パスワード変更強制中でも可）
- Request `ChangePasswordRequest`: `currentPassword` string 必須 1〜128、`newPassword` string 必須 12〜128
- 検証: パスワードポリシー（[authentication-authorization.md](authentication-authorization.md) §5）、新旧が同一でないこと
- Response 200 `LoginResponse`（新しいアクセストークン。security stamp 更新のため再発行）+ 新 Cookie
- エラー: 400 `auth.current_password_invalid` / `validation.failed`
- DB: `user_account` 更新、他の全 `refresh_session` 失効、新セッション作成、監査記録

### 4.2 Me

#### #5 GET /api/v1/me
- Response 200 `CurrentUserResponse`: `id`, `email`, `displayName`, `systemRole` SystemRole, `timeZoneId`, `mustChangePassword` boolean, `isProjectOwnerSomewhere` boolean（利用者検索の可否判定用）

#### #6 PATCH /api/v1/me
- Request `UpdateMeRequest`: `displayName` string? 1〜50（制御文字不可）、`timeZoneId` string? 許可リスト（IANA 名、.NET の `TimeZoneInfo.TryFindSystemTimeZoneById` で検証）
- Response 200 `CurrentUserResponse`
- DB: `user_account` 更新

### 4.3 Users（システム管理者）

#### #7 GET /api/v1/users
- Query: `q` string? 0〜100（表示名・メールの部分一致）、`status` UserStatusFilter?、`page`、`pageSize`（既定 50）、`sort`: `displayName`（既定）、`-lastLoginAt`、`email`、`createdAt`
- Response 200 `UserAdminSummaryPage`。要素 `UserAdminSummaryResponse`: `id`, `email`, `displayName`, `systemRole`, `isActive`, `isLocked`, `mustChangePassword`, `lastLoginAtUtc`?, `createdAtUtc`

#### #8 POST /api/v1/users
- Request `CreateUserRequest`: `email` 必須 3〜254（メール形式）、`displayName` 必須 1〜50、`systemRole` 必須
- Response 201 `CreatedUserResponse`: `user` UserAdminSummaryResponse, `temporaryPassword` string（**この応答でのみ返す**）。`Location: /api/v1/users/{id}`、`Cache-Control: no-store`
- エラー: 409 `user.email_taken`
- DB: `user_account` 追加、監査記録

#### #9 GET /api/v1/users/{userId}
- Response 200 `UserAdminDetailResponse`: Summary の項目 + `lockedUntilUtc`?, `timeZoneId`, `projectCount` integer, `version` integer

#### #10 PATCH /api/v1/users/{userId}
- Request `UpdateUserRequest`: `displayName`?, `systemRole`?, `version` 必須
- Response 200 `UserAdminDetailResponse`
- エラー: 409 `user.last_administrator` / `concurrency.conflict`
- DB: 更新、監査記録（ロール変更）

#### #11 / #12 POST .../deactivate, .../activate
- Response 204
- エラー: 409 `user.cannot_deactivate_self` / `user.last_administrator`
- DB: `is_active` 更新、停止時は `security_stamp` 更新・全セッション失効、監査記録

#### #13 POST .../unlock
- Response 204。DB: `failed_login_count=0`, `locked_until_utc=NULL`、監査記録

#### #14 POST .../password-reset
- Response 200 `TemporaryPasswordResponse`: `temporaryPassword` string（`Cache-Control: no-store`）
- DB: ハッシュ更新、`must_change_password=1`、`security_stamp` 更新、全セッション失効、ロック解除、監査記録

### 4.4 UserDirectory

#### #15 GET /api/v1/user-directory
- 権限: いずれかのプロジェクトの OW、または SA（それ以外 403）
- Query: `q` string 必須 1〜100、`excludeProjectKey` string?（そのプロジェクトのメンバーを除外）
- Response 200 `UserDirectoryEntry[]`（最大 20 件、有効な利用者のみ）: `id`, `displayName`, `email`
- ソート: 表示名

### 4.5 Projects

#### #16 GET /api/v1/projects
- Query: `includeArchived` boolean 既定 false、`q` string? 0〜80（名前・キー部分一致）、`page`、`pageSize`（既定 50）、`sort`: `name`（既定）、`key`、`-updatedAt`
- 対象: 自分がメンバーのプロジェクト。SA は `scope=all` を指定すると全プロジェクト（内容は見られないため `myRole` は null）
- Response 200 `ProjectSummaryPage`。要素 `ProjectSummaryResponse`: `key`, `name`, `myRole` ProjectRole?, `isArchived`, `openTaskCount` integer, `overdueTaskCount` integer, `ownerNames` string[]（最大 3）, `updatedAtUtc`

#### #17 POST /api/v1/projects
- Request `CreateProjectRequest`

| プロパティ | 型 | 必須 | 検証 |
|---|---|---|---|
| `key` | string | ○ | 2〜10、`^[A-Za-z][A-Za-z0-9]{1,9}$`（大文字化して保存） |
| `name` | string | ○ | 1〜80（前後空白除去） |
| `description` | string? | | 0〜2,000 |

- Response 201 `ProjectDetailResponse`、`Location: /api/v1/projects/{key}`
- エラー: 409 `project.key_taken` / `project.name_taken`
- DB: `project`、`project_membership`（作成者を Owner）、`workflow_status` 5 件

#### #18 GET /api/v1/projects/{projectKey}
- Response 200 `ProjectDetailResponse`: `key`, `name`, `description`?, `isArchived`, `archivedAtUtc`?, `myRole`, `memberCount`, `taskCounts`（`notStarted`, `inProgress`, `done`, `canceled`, `overdue` の各 integer）, `createdAtUtc`, `createdBy` UserRef, `updatedAtUtc`, `version`

#### #19 PATCH /api/v1/projects/{projectKey}
- Request `UpdateProjectRequest`: `name`?, `description`?（null で消去）, `version` 必須
- Response 200 `ProjectDetailResponse`
- エラー: 409 `project.name_taken` / `concurrency.conflict`

#### #20 / #21 POST .../archive, .../unarchive
- Response 200 `ProjectDetailResponse`。アーカイブ済みに #20 → 200（変化なし）

#### #22 DELETE /api/v1/projects/{projectKey}
- 権限: SA
- Query: `confirmKey` string 必須（パスのキーと一致しなければ 400）
- Response 204
- エラー: 409 `project.not_archived`
- DB: [database-design.md](database-design.md) §4 の順序で削除、添付ファイル削除、監査記録

### 4.6 ProjectMembers

#### #23 GET .../members
- Query: `role` ProjectRole?（`assignable=true` で OW と ED のみ）
- Response 200 `ProjectMemberResponse[]`（全件、表示名順）: `user` UserRef, `email`, `role`, `joinedAtUtc`

#### #24 POST .../members
- Request `AddProjectMemberRequest`: `userId` uuid 必須、`role` ProjectRole 必須
- Response 201 `ProjectMemberResponse`
- エラー: 400 `member.user_inactive` / `validation.failed`（利用者なし）、409 `member.already_exists`
- DB: `project_membership` 追加、`notice`（AddedToProject）、監査記録

#### #25 PATCH .../members/{userId}
- Request `ChangeProjectMemberRoleRequest`: `role` 必須
- Response 200 `ProjectMemberResponse`
- エラー: 409 `member.last_owner`
- DB: 更新。ED/OW → VW に変更した場合、その利用者が担当のタスクは担当者なしに戻す（履歴あり）。監査記録

#### #26 DELETE .../members/{userId}
- Response 204
- エラー: 409 `member.last_owner`
- DB: 担当解除（履歴）、所属削除、監査記録

### 4.7 WorkflowStatuses

#### #27 GET .../statuses
- Response 200 `WorkflowStatusResponse[]`（並び順）: `id`, `name`, `category`, `color`, `sortOrder`, `isDefault`, `taskCount`

#### #28 POST .../statuses
- Request `CreateWorkflowStatusRequest`: `name` 1〜30 必須、`category` 必須、`color` 必須
- 末尾に追加。Response 201 `WorkflowStatusResponse`
- エラー: 409 `status.name_taken` / `status.limit_reached`

#### #29 PATCH .../statuses/{statusId}
- Request `UpdateWorkflowStatusRequest`: `name`?, `category`?, `color`?, `isDefault`?（true のみ受け付け。他の既定を外す）
- 検証: 既定にする/既定のまま区分変更する場合、区分は notStarted / inProgress
- Response 200 `WorkflowStatusResponse`

#### #30 PUT .../statuses/order
- Request `ReorderWorkflowStatusesRequest`: `statusIds` uuid[] 必須（プロジェクトの全ステータスをちょうど 1 回ずつ）
- Response 200 `WorkflowStatusResponse[]`
- エラー: 400 `validation.failed`（過不足・重複）

#### #31 DELETE .../statuses/{statusId}
- Query: `moveTasksToStatusId` uuid?（タスクが存在する場合は必須）
- Response 204
- エラー: 409 `status.is_default` / `status.in_use`、400（移動先が自分自身・別プロジェクト）、409（最後の 1 件）

### 4.8 Tags

#### #32 GET .../tags → 200 `TagResponse[]`: `id`, `name`, `color`, `taskCount`（名前順）
#### #33 POST .../tags
- Request `CreateTagRequest`: `name` 1〜30、`color`。Response 201 `TagResponse`。エラー: 409 `tag.name_taken` / `tag.limit_reached`
#### #34 PATCH .../tags/{tagId}
- Request `UpdateTagRequest`: `name`?, `color`?。Response 200
#### #35 DELETE .../tags/{tagId}
- Response 204。DB: `task_item_tag` から削除（CASCADE）、`tag` 削除

### 4.9 Milestones

#### #36 GET .../milestones
- Query: `state` MilestoneState?（既定は全件）
- Response 200 `MilestoneSummaryResponse[]`（進行中 → 目標日の近い順 → 名前、終了は後ろ）: `id`, `name`, `targetDate`?, `state`, `totalTaskCount`, `closedTaskCount`（完了 + 中止）, `overdueTaskCount`
#### #37 POST .../milestones
- Request `CreateMilestoneRequest`: `name` 1〜80、`description`? 0〜2,000、`targetDate`? date
- Response 201 `MilestoneDetailResponse`（Summary + `description`, `version`, `createdAtUtc`, `updatedAtUtc`）
- エラー: 409 `milestone.name_taken`
#### #38 GET .../milestones/{milestoneId} → 200 `MilestoneDetailResponse`
#### #39 PATCH .../milestones/{milestoneId}
- Request `UpdateMilestoneRequest`: `name`?, `description`?, `targetDate`?, `state`?, `version` 必須
#### #40 DELETE .../milestones/{milestoneId} → 204。DB: タスクの関連解除（履歴）、削除

### 4.10 Tasks

#### #41 GET /api/v1/projects/{projectKey}/tasks — タスク一覧

| Query | 型 | 説明 |
|---|---|---|
| `statusId` | uuid[] | ステータス |
| `category` | StatusCategory[] | 区分。**既定は `notStarted` と `inProgress`（未完了のみ）**。全件は `includeClosed=true` |
| `includeClosed` | boolean | 完了・中止も含める |
| `completedSince` | date-time | 完了区分のタスクは `completedAtUtc` がこの日時以降のものだけを返す（他の区分には影響しない。ボードの「完了」列で使用） |
| `assigneeUserId` | uuid[] | 担当者 |
| `unassigned` | boolean | 担当者なし（`assigneeUserId` と OR） |
| `assignedToMe` | boolean | 自分の担当 |
| `priority` | TaskPriority[] | |
| `tagId` | uuid[] | いずれかのタグを持つ |
| `milestoneId` | uuid | |
| `noMilestone` | boolean | |
| `dueFrom` / `dueTo` | date | 期限の範囲（両端含む） |
| `overdue` | boolean | 期限切れのみ（利用者のタイムゾーン基準） |
| `parentTaskNumber` | integer | その親のサブタスクのみ |
| `topLevelOnly` | boolean | サブタスクを除く |
| `q` | string 0〜100 | タイトルの部分一致（`%` `_` はエスケープ） |
| `sort` | string | `-updatedAt`（既定）, `updatedAt`, `number`, `-number`, `dueDate`, `-dueDate`, `priority`, `-priority`, `status`（ステータスの並び順）, `title`, `assignee`, `-createdAt` |
| `page` / `pageSize` | | 既定 50、最大 100 |

- Response 200 `TaskSummaryPage`。要素 `TaskSummaryResponse`:

| プロパティ | 型 |
|---|---|
| `key` | string |
| `number` | integer |
| `title` | string |
| `status` | StatusRef |
| `priority` | TaskPriority |
| `assignee` | UserRef? |
| `startDate` / `dueDate` | date? |
| `isOverdue` | boolean |
| `milestone` | MilestoneRef? |
| `tags` | TagRef[] |
| `parent` | TaskRef? |
| `subtaskCount` / `closedSubtaskCount` | integer |
| `commentCount` / `attachmentCount` | integer |
| `updatedAtUtc` | date-time |
| `version` | integer（ボードでのステータス変更に使用） |

- ボード表示は `pageSize=100` と `includeClosed`（ボード設定）で取得し、100 件を超える列は「さらに表示」で次ページを取得する（[ui-design.md](ui-design.md) SCR-07）。

#### #42 POST /api/v1/projects/{projectKey}/tasks — タスク作成
- Request `CreateTaskRequest`

| プロパティ | 型 | 必須 | 検証 |
|---|---|---|---|
| `title` | string | ○ | 1〜200、改行不可、前後空白除去後 1 文字以上 |
| `description` | string? | | 0〜20,000 |
| `statusId` | uuid? | | 同一プロジェクト。省略時は既定ステータス |
| `priority` | TaskPriority? | | 省略時 `normal` |
| `assigneeUserId` | uuid? | | 同一プロジェクトの OW / ED かつ有効 |
| `startDate` / `dueDate` | date? | | 開始日 ≤ 期限、2000-01-01〜2099-12-31 |
| `tagIds` | uuid[]? | | 最大 10、重複不可、同一プロジェクト |
| `milestoneId` | uuid? | | 同一プロジェクト、進行中 |
| `parentTaskNumber` | integer? | | 同一プロジェクト、親自身がサブタスクでない |

- Response 201 `TaskDetailResponse`、`Location: /api/v1/projects/{key}/tasks/{number}`
- エラー: 400 `validation.failed`（各項目）、409 `milestone.closed`
- DB: 採番（`project.last_task_number`）、`task_item`、`task_item_tag`、`task_history_entry`（Created）、`notice`（担当者が自分以外）

#### #43 GET .../tasks/{taskNumber} — タスク詳細
- Response 200 `TaskDetailResponse`: Summary の全項目 + `description`?, `completedAtUtc`?, `subtasks` TaskSummaryResponse[]（番号順）, `createdAtUtc`, `createdBy` UserRef, `updatedBy` UserRef, `permissions`（`canEdit`, `canDelete`, `canComment`, `canUpload` の boolean。画面の表示制御用）

#### #44 PATCH .../tasks/{taskNumber} — タスク更新
- Request `UpdateTaskRequest`: #42 の全項目を任意（`title` は null 不可）+ `version` integer 必須。`tagIds` は送った配列で **置き換え**。`parentTaskNumber: null` で親子解除
- 追加検証: サブタスクを持つタスクに親を設定不可、自分自身を親に不可
- Response 200 `TaskDetailResponse`
- エラー: 400、409 `concurrency.conflict` / `milestone.closed`
- DB: `task_item` 更新、`task_item_tag` 差し替え、変更項目ごとに `task_history_entry`、`notice`（担当者変更・完了）

#### #45 DELETE .../tasks/{taskNumber}
- Query: `includeSubtasks` boolean 既定 false
- Response 204
- エラー: 409 `task.has_subtasks`、403（ED が他人の作成したタスク）
- DB: [database-design.md](database-design.md) §4

#### #46 GET .../tasks/{taskNumber}/history
- Query: `page`、`pageSize`（既定 50）。並びは新しい順で固定
- Response 200 `TaskHistoryEntryPage`。要素: `id`, `kind` TaskHistoryKind, `actor` UserRef?, `oldValue`?, `newValue`?, `occurredAtUtc`

#### #62 GET /api/v1/projects/{projectKey}/tasks/schedule — ガントチャートの日程（Phase 9）

- 目的: 表示期間と重なるタスクを棒として描くためのデータ（REQ-GNT-01）。日程の変更は #44（既存）を使う
- 権限: VW 以上（`ViewProject`）。アーカイブ中も参照可。非メンバー 404
- Query

| Query | 型 | 必須 | 説明 |
|---|---|---|---|
| `from` | date | ○ | 表示期間の開始日（含む） |
| `to` | date | ○ | 表示期間の終了日（含む）。`from` 以上、`from` から 366 日以内（両端を含めて最大 366 日）、2000-01-01〜2099-12-31 |
| `includeClosed` | boolean | | 完了・中止の区分も含める（既定 false = 未着手・進行中のみ） |

- 処理: domain-model.md §5.1（SC-01〜SC-03）。期間と重なるタスクを「棒の始点 → 番号」の順に最大 500 件。開始日・期限がどちらもないタスクは別に件数と先頭 50 件（優先度の高い順 → 番号）
- Response 200 `TaskScheduleResponse`

| プロパティ | 型 | 説明 |
|---|---|---|
| `from` / `to` | date | 受け付けた表示期間 |
| `today` | date | 利用者のタイムゾーンでの今日（今日の線・期限切れの判定に使う） |
| `timeZoneId` | string | 利用者のタイムゾーン（既定 `Asia/Tokyo`） |
| `items` | TaskSummaryResponse[] | 期間と重なるタスク（#41 と同じ形。`startDate`・`dueDate`・`isOverdue`・`status`・`assignee`・`milestone`・`version` を含む） |
| `truncated` | boolean | 500 件を超えたため一部のみ返した |
| `undatedItems` | TaskSummaryResponse[] | 開始日・期限がどちらもないタスク（最大 50 件） |
| `undatedCount` | integer | 開始日・期限がどちらもないタスクの件数（`undatedItems` の件数を超えることがある） |

- エラー: 400 `validation.failed`（`from` / `to` の欠落・形式不正、`to` < `from`、367 日以上、範囲外。`errors` のキーは `from` / `to`）、404、401、403（一時パスワード）、429、500
- DB: 参照のみ。database-design.md §7（ガント）

#### #63 GET /api/v1/projects/{projectKey}/tasks/status-summary — プロジェクトのステータス別件数・割合（Phase 9）

- 目的: 円グラフ（REQ-CHT-01）
- 権限: VW 以上（`ViewProject`）。アーカイブ中も参照可。非メンバー 404
- Query: なし（プロジェクトの全タスク。全区分・サブタスクを含む）
- Response 200 `TaskStatusSummaryResponse`

| プロパティ | 型 | 説明 |
|---|---|---|
| `groupedBy` | StatusSummaryGrouping | 常に `status` |
| `totalCount` | integer | 合計件数 |
| `items` | StatusCountResponse[] | プロジェクトの全ステータス（0 件を含む）。ステータスの並び順（`sortOrder`） |

- `percent`: 小数第 1 位。最大剰余法で合計 100.0（`totalCount` = 0 なら全て 0）。domain-model.md §5.2
- エラー: 404、401、403（一時パスワード）、429、500
- DB: `task_item` を `status_id` で `GROUP BY`（`ix_task_item__project_id__status_id`）



#### #47 GET .../comments
- Query: `page`、`pageSize`（既定 50）。並びは古い順（会話として読むため）
- Response 200 `TaskCommentPage`。要素 `TaskCommentResponse`: `id`, `author` UserRef, `body`, `createdAtUtc`, `editedAtUtc`?, `canEdit`, `canDelete`
#### #48 POST .../comments
- Request `CreateTaskCommentRequest`: `body` 1〜10,000（前後空白除去後 1 文字以上）
- Response 201 `TaskCommentResponse`
- DB: `task_comment`、`notice`（担当者・作成者、投稿者を除く）
#### #49 PATCH .../comments/{commentId}
- Request `UpdateTaskCommentRequest`: `body` 必須。Response 200。権限: 投稿者本人のみ（他人は 403）
#### #50 DELETE .../comments/{commentId} → 204

### 4.12 TaskAttachments

#### #51 GET .../attachments
- Response 200 `TaskAttachmentResponse[]`（新しい順）: `id`, `fileName`, `contentType`, `sizeBytes`, `uploadedBy` UserRef, `uploadedAtUtc`, `canDelete`

#### #52 POST .../attachments
- Content-Type: `multipart/form-data`。パート `file`（1 件）
- 制限: リクエスト本文 11MB（Kestrel / nginx）、ファイル 10MB、レート 20 回/分/利用者
- 検証: 拡張子許可リスト、先頭バイト（マジックナンバー）と拡張子の整合、ファイル名正規化
- Response 201 `TaskAttachmentResponse`
- エラー: 413 `attachment.too_large`、415 `attachment.type_not_allowed`、409 `attachment.limit_reached`
- DB: ファイル保存 → `task_attachment`、`task_history_entry`（AttachmentAdded）。DB 失敗時は保存したファイルを削除

#### #53 GET .../attachments/{attachmentId}/content
- Response 200 バイナリ。ヘッダー: `Content-Type`（保存時に検証した値。テキスト系は `text/plain; charset=utf-8` に固定）、`Content-Disposition: attachment; filename="..."; filename*=UTF-8''...`、`X-Content-Type-Options: nosniff`、`Cache-Control: private, no-store`
- OpenAPI: `content: { application/octet-stream: { schema: { type: string, format: binary } } }`

#### #54 DELETE .../attachments/{attachmentId} → 204
- DB: `task_attachment` 削除、履歴（AttachmentRemoved）、コミット後にファイル削除

### 4.13 MyTasks

#### #55 GET /api/v1/me/tasks
- Query: `includeRecentlyCompleted` boolean 既定 false（直近 7 日に完了したものを `recentlyCompleted` に入れる）
- 対象: 参加中かつアーカイブされていないプロジェクトで、自分が担当の未完了（notStarted / inProgress）タスク。最大 500 件（超えた場合 `truncated: true`）
- Response 200 `MyTasksResponse`:
```json
{
  "today": "2026-10-06",
  "timeZoneId": "Asia/Tokyo",
  "buckets": {
    "overdue":   [ TaskSummaryWithProject ],
    "today":     [ ... ],
    "thisWeek":  [ ... ],
    "later":     [ ... ],
    "noDueDate": [ ... ]
  },
  "recentlyCompleted": [ ... ],
  "truncated": false
}
```
- `TaskSummaryWithProject` = `TaskSummaryResponse` + `project`（`key`, `name`）
- 区分: overdue = 期限 < 今日、today = 期限 = 今日、thisWeek = 明日〜今週の日曜日（週は月曜始まり。今日が日曜日の場合は該当なし）、later = それ以降、noDueDate = 期限なし。各区分内は優先度の高い順 → 期限 → 番号

#### #64 GET /api/v1/me/tasks/status-summary — 自分の担当のステータス別件数・割合（Phase 9）

- 目的: 「自分のタスク」の円グラフ（REQ-CHT-02）
- 権限: 本人（ログイン済み）。`projectKey` 指定時は、そのプロジェクトの閲覧者以上（非メンバー・存在しない場合は 404）
- Query: `projectKey` string?（pattern `^[A-Za-z][A-Za-z0-9]{1,9}$`）
- 対象: 自分が担当者のタスク（全区分）
  - `projectKey` なし: 参加中かつアーカイブされていない全プロジェクト。**区分別**（`groupedBy = category`）に 4 項目（未着手・進行中・完了・中止。0 件を含む）。`statusId` は null、`name` は区分の日本語名、`color` は区分の既定色（gray / blue / green / red）、`sortOrder` は 1〜4
  - `projectKey` あり: そのプロジェクトの**ステータス別**（`groupedBy = status`）。#63 と同じ形
- Response 200 `TaskStatusSummaryResponse`（#63 と同じスキーマ）
- エラー: 400 `validation.failed`（`projectKey` の形式不正）、404（プロジェクト指定時の非メンバー・存在しない）、401、403（一時パスワード）、429、500
- DB: `assignee_user_id = 自分` で絞り `GROUP BY`（`ix_task_item__assignee_user_id__project_id__status_id`）

### 4.14 Notices

#### #56 GET /api/v1/me/notices
- Query: `unreadOnly` boolean、`page`、`pageSize`（既定 30）。新しい順
- Response 200 `NoticePage`。要素 `NoticeResponse`: `id`, `kind`, `message`, `linkPath`?, `actor` UserRef?, `isRead`, `createdAtUtc`
#### #57 GET /api/v1/me/notices/unread-count → 200 `{ "count": 3 }`（`UnreadNoticeCountResponse`、上限表示は画面側で 99+）
#### #58 POST /api/v1/me/notices/{noticeId}/read → 204（他人のお知らせは 404）
#### #59 POST /api/v1/me/notices/read-all → 204

### 4.16 System（Phase 3 で追加）

#### #61 GET /api/v1/system/info — API の名前と版
- 目的: Swagger UI・OpenAPI 文書・Frontend の型生成と疎通の確認（業務機能ではない）
- 認証: 不要（`[AllowAnonymous]`）。秘密情報・環境名・ビルドのコミットハッシュは返さない
- Response 200 `SystemInfoResponse`: `name` string（`TaskManager`）、`version` string（例 `1.0.0`）

### 4.15 Search

#### #60 GET /api/v1/search/tasks
- Query: `q` string 必須 1〜100
- 処理: `q` が `TaskKey` 形式（大文字小文字問わず）なら該当タスクを先頭に。続いてタイトル部分一致（参加中プロジェクト、アーカイブ除外、更新の新しい順）
- Response 200 `TaskSearchResultResponse[]`（最大 20）: `key`, `title`, `project`（`key`, `name`）, `status` StatusRef, `assignee` UserRef?
- レート制限: 60 回/分/利用者

## 5. Controller とエンドポイントの対応

| Controller | エンドポイント # |
|---|---|
| `AuthController` | 1〜4 |
| `MeController` | 5, 6 |
| `UsersController` | 7〜14 |
| `UserDirectoryController` | 15 |
| `ProjectsController` | 16〜22 |
| `ProjectMembersController` | 23〜26 |
| `WorkflowStatusesController` | 27〜31 |
| `TagsController` | 32〜35 |
| `MilestonesController` | 36〜40 |
| `TasksController` | 41〜45、62、63（Phase 9） |
| `TaskHistoryController` | 46 |
| `TaskCommentsController` | 47〜50 |
| `TaskAttachmentsController` | 51〜54 |
| `MyTasksController` | 55、64（Phase 9） |
| `NoticesController` | 56〜59 |
| `SearchController` | 60 |
| `SystemController` | 61 |

## 6. OpenAPI を実装と一致させる仕組み

1. Controller アクションに `[EndpointName("ListTasks")]`（= operationId）、`[Tags("Tasks")]`、`[EndpointSummary("タスク一覧を取得する")]`、`[ProducesResponseType<T>(200)]`、`[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]` 等を付ける。
2. Request DTO の DataAnnotations（`[Required]`, `[StringLength]`, `[Range]`, `[RegularExpression]`）がスキーマの制約になる。
3. 列挙は `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` で文字列化し、スキーマにも反映させる。
4. 共通エラー応答・セキュリティ要件・`x-required-permission` はドキュメント変換（`IOpenApiOperationTransformer`）で付与する。
5. ビルド時に `openapi/v1.json` を生成し、`docs/api/openapi.v1.json`（コミット済み）と差分があればテストを失敗させる（[testing-strategy.md](testing-strategy.md) §6）。差分が意図したものなら本書とスナップショットを同時に更新する。
6. Frontend は同じスナップショットから型を生成する。
