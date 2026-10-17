# データベース設計（MySQL 8.4 / EF Core 10）

## 1. 共通方針

| 項目 | 方針 | 理由 |
|---|---|---|
| DB 名 | `taskmanager_db` | [naming-rules.md](naming-rules.md) §4 |
| 文字コード | `utf8mb4` / 照合順序 `utf8mb4_0900_ai_ci`（DB 既定） | 日本語・絵文字対応、大文字小文字を区別しない一意性・検索 |
| エンジン | InnoDB | トランザクション・外部キー |
| 主キー | `id CHAR(36)`（UUID v7、アプリで生成） | 推測困難な ID（IDOR の多層防御）、時系列順で挿入性能を保つ。BINARY(16) との比較は OD-05 |
| 日時 | `DATETIME(6)`、UTC、列名 `_at_utc` | タイムゾーンの取り違え防止 |
| 日付 | `DATE`（開始日・期限・目標日） | 時刻・タイムゾーンを持たない業務日付 |
| 列挙 | `TINYINT UNSIGNED` + CHECK 制約 | 容量・並べ替え・範囲保証 |
| 論理削除 | **採用しない**。プロジェクトは「アーカイブ」、利用者は「利用停止」で無効化し、それ以外は物理削除 | MySQL は条件付き一意インデックスを持たず、論理削除と一意制約の両立が複雑になる（analysis/database.md）。業務上の「取り消し」はアーカイブ・停止で足りる |
| 楽観的排他 | `row_version INT UNSIGNED NOT NULL DEFAULT 0`。EF Core の Concurrency Token とし、更新時にアプリで +1 | MySQL に rowversion 型がないため |
| 監査列 | `created_at_utc`, `created_by_user_id`, `updated_at_utc`, `updated_by_user_id`（対象表のみ） | SaveChanges インターセプタで自動設定 |
| 利用者の削除 | **行を削除しない**（停止のみ）。利用者を参照する外部キーは原則 `RESTRICT`。利用者に従属するデータ（`refresh_session`、`notice` の宛先）のみ `CASCADE` | 履歴・コメントの投稿者を失わない |
| CHECK 制約 | MySQL 8.0.16 以降で有効。範囲・日付順序を DB でも保証 | 多層防御 |

EF Core の設定は Infrastructure の `IEntityTypeConfiguration<T>` で表名・列名・型・制約名を明示する。

## 2. 表一覧

| 表 | Entity | 目的 | ライフサイクル |
|---|---|---|---|
| `user_account` | UserAccount | 利用者 | 作成 → （停止 ⇄ 再開）。削除しない |
| `refresh_session` | RefreshSession | リフレッシュトークン | ログインで作成 → ローテーション/失効 → 30 日後に削除 |
| `project` | Project | プロジェクト | 作成 → （アーカイブ ⇄ 復元）→ アーカイブ後に SA が削除可 |
| `project_membership` | ProjectMembership | 所属とロール | 追加 → ロール変更 → 削除 |
| `workflow_status` | WorkflowStatus | ステータス定義 | プロジェクトと共に作成 → 編集・並べ替え → 削除（移動先指定） |
| `tag` | Tag | タグ定義 | 作成 → 編集 → 削除 |
| `milestone` | Milestone | マイルストーン | 作成 → 終了 → 削除 |
| `task_item` | TaskItem | タスク | 作成 → 更新 → 削除 |
| `task_item_tag` | TaskItemTag | タスクとタグの対応 | タスク更新で付け外し |
| `task_comment` | TaskComment | コメント | 投稿 → 編集 → 削除 |
| `task_attachment` | TaskAttachment | 添付ファイルのメタ情報 | 登録 → 削除 |
| `task_history_entry` | TaskHistoryEntry | 変更履歴 | 追記のみ。タスクと共に削除 |
| `notice` | Notice | お知らせ | 作成 → 既読 → 90 日後に削除 |
| `security_audit_event` | SecurityAuditEvent | 監査記録 | 追記のみ → 1 年後に削除 |
| `__EFMigrationsHistory` | — | EF Core のマイグレーション履歴 | EF Core 管理 |

## 3. 表定義

凡例: PK=主キー、NN=NOT NULL、FK=外部キー（削除時の挙動）

### 3.1 `user_account`

| 列 | 型 | NN | 既定 | 説明 |
|---|---|---|---|---|
| `id` | CHAR(36) | ○ | | PK |
| `email` | VARCHAR(254) | ○ | | 正規化済み（小文字）。一意 |
| `display_name` | VARCHAR(50) | ○ | | |
| `system_role` | TINYINT UNSIGNED | ○ | 1 | 1=一般, 2=システム管理者 |
| `password_hash` | VARCHAR(255) | ○ | | `PasswordHasher` 出力（Base64） |
| `must_change_password` | TINYINT(1) | ○ | 1 | |
| `is_active` | TINYINT(1) | ○ | 1 | |
| `failed_login_count` | TINYINT UNSIGNED | ○ | 0 | |
| `locked_until_utc` | DATETIME(6) | | NULL | |
| `last_login_at_utc` | DATETIME(6) | | NULL | |
| `time_zone_id` | VARCHAR(64) | ○ | `Asia/Tokyo` | IANA 名 |
| `security_stamp` | CHAR(36) | ○ | | |
| `row_version` | INT UNSIGNED | ○ | 0 | |
| `created_at_utc` | DATETIME(6) | ○ | | |
| `created_by_user_id` | CHAR(36) | | NULL | FK → user_account（RESTRICT）。初期管理者は NULL |
| `updated_at_utc` | DATETIME(6) | ○ | | |
| `updated_by_user_id` | CHAR(36) | | NULL | FK → user_account（RESTRICT） |

- 一意: `ux_user_account__email (email)`
- 索引: `ix_user_account__is_active__display_name (is_active, display_name)`（利用者検索）
- CHECK: `ck_user_account__system_role (system_role IN (1,2))`

### 3.2 `refresh_session`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `user_id` | CHAR(36) | ○ | FK → user_account（CASCADE。利用者は削除しないが関係の意味として） |
| `family_id` | CHAR(36) | ○ | |
| `token_hash` | BINARY(32) | ○ | SHA-256 |
| `created_at_utc` | DATETIME(6) | ○ | |
| `expires_at_utc` | DATETIME(6) | ○ | |
| `absolute_expires_at_utc` | DATETIME(6) | ○ | |
| `rotated_at_utc` | DATETIME(6) | | |
| `revoked_at_utc` | DATETIME(6) | | |
| `user_agent` | VARCHAR(255) | | |
| `created_from_ip` | VARCHAR(45) | | |

- 一意: `ux_refresh_session__token_hash (token_hash)`
- 索引: `ix_refresh_session__user_id (user_id)`、`ix_refresh_session__family_id (family_id)`、`ix_refresh_session__expires_at_utc (expires_at_utc)`

### 3.3 `project`

| 列 | 型 | NN | 既定 | 説明 |
|---|---|---|---|---|
| `id` | CHAR(36) | ○ | | PK |
| `project_key` | VARCHAR(10) | ○ | | 英大文字・数字。一意、不変 |
| `name` | VARCHAR(80) | ○ | | 一意（照合順序で大文字小文字無視） |
| `description` | VARCHAR(2000) | | NULL | |
| `archived_at_utc` | DATETIME(6) | | NULL | |
| `last_task_number` | INT UNSIGNED | ○ | 0 | 採番カウンタ |
| `row_version` | INT UNSIGNED | ○ | 0 | |
| 監査列 4 つ | | | | `created_by_user_id` / `updated_by_user_id` は NN、FK → user_account（RESTRICT） |

- 一意: `ux_project__project_key`、`ux_project__name`
- 索引: `ix_project__archived_at_utc`
- CHECK: `ck_project__key_format (REGEXP_LIKE(project_key, '^[A-Z][A-Z0-9]{1,9}$', 'c'))`

### 3.4 `project_membership`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `project_id` | CHAR(36) | ○ | FK → project（CASCADE） |
| `user_id` | CHAR(36) | ○ | FK → user_account（RESTRICT） |
| `role` | TINYINT UNSIGNED | ○ | 1=閲覧者, 2=編集者, 3=管理者 |
| `joined_at_utc` | DATETIME(6) | ○ | |
| `created_by_user_id` | CHAR(36) | ○ | FK → user_account（RESTRICT） |
| `updated_at_utc` | DATETIME(6) | ○ | |
| `updated_by_user_id` | CHAR(36) | ○ | FK → user_account（RESTRICT） |

- 一意: `ux_project_membership__project_id__user_id`
- 索引: `ix_project_membership__user_id (user_id)`（自分のプロジェクト一覧）
- CHECK: `ck_project_membership__role (role BETWEEN 1 AND 3)`

### 3.5 `workflow_status`

| 列 | 型 | NN | 既定 | 説明 |
|---|---|---|---|---|
| `id` | CHAR(36) | ○ | | PK |
| `project_id` | CHAR(36) | ○ | | FK → project（CASCADE） |
| `name` | VARCHAR(30) | ○ | | |
| `category` | TINYINT UNSIGNED | ○ | | 1〜4 |
| `color` | TINYINT UNSIGNED | ○ | | `StatusColor` の値 1〜9 |
| `sort_order` | SMALLINT UNSIGNED | ○ | | 1 から |
| `is_default` | TINYINT(1) | ○ | 0 | |
| 監査列 4 つ | | | | |

- 一意: `ux_workflow_status__project_id__name`
- 索引: `ix_workflow_status__project_id__sort_order`
- 「既定は 1 件」は条件付き一意インデックスが使えないため **Application で保証**（既定変更は同一トランザクションで旧既定を false にする）
- CHECK: `category BETWEEN 1 AND 4`、`color BETWEEN 1 AND 9`

### 3.6 `tag`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `project_id` | CHAR(36) | ○ | FK → project（CASCADE） |
| `name` | VARCHAR(30) | ○ | |
| `color` | TINYINT UNSIGNED | ○ | |
| 監査列 4 つ | | | |

- 一意: `ux_tag__project_id__name`

### 3.7 `milestone`

| 列 | 型 | NN | 既定 | 説明 |
|---|---|---|---|---|
| `id` | CHAR(36) | ○ | | PK |
| `project_id` | CHAR(36) | ○ | | FK → project（CASCADE） |
| `name` | VARCHAR(80) | ○ | | |
| `description` | VARCHAR(2000) | | NULL | |
| `target_date` | DATE | | NULL | |
| `state` | TINYINT UNSIGNED | ○ | 1 | 1=進行中, 2=終了 |
| `row_version` | INT UNSIGNED | ○ | 0 | |
| 監査列 4 つ | | | | |

- 一意: `ux_milestone__project_id__name`
- 索引: `ix_milestone__project_id__state__target_date`

### 3.8 `task_item`

| 列 | 型 | NN | 既定 | 説明 |
|---|---|---|---|---|
| `id` | CHAR(36) | ○ | | PK |
| `project_id` | CHAR(36) | ○ | | FK → project（CASCADE） |
| `task_number` | INT UNSIGNED | ○ | | プロジェクト内連番 |
| `title` | VARCHAR(200) | ○ | | |
| `description` | MEDIUMTEXT | | NULL | Markdown（最大 20,000 文字をアプリで制限） |
| `status_id` | CHAR(36) | ○ | | FK → workflow_status（RESTRICT） |
| `priority` | TINYINT UNSIGNED | ○ | 2 | 1〜4 |
| `assignee_user_id` | CHAR(36) | | NULL | FK → user_account（RESTRICT） |
| `start_date` | DATE | | NULL | |
| `due_date` | DATE | | NULL | |
| `milestone_id` | CHAR(36) | | NULL | FK → milestone（SET NULL） |
| `parent_task_id` | CHAR(36) | | NULL | FK → task_item（CASCADE） |
| `completed_at_utc` | DATETIME(6) | | NULL | |
| `row_version` | INT UNSIGNED | ○ | 0 | |
| 監査列 4 つ | | | | |

- 一意: `ux_task_item__project_id__task_number`
- 索引:
  - `ix_task_item__project_id__status_id`（ボード・ステータス絞り込み）
  - `ix_task_item__project_id__assignee_user_id`（担当者絞り込み）
  - `ix_task_item__assignee_user_id__due_date`（自分のタスク）
  - `ix_task_item__project_id__due_date`（期限並べ替え・期限切れ）
  - `ix_task_item__project_id__updated_at_utc`（更新順）
  - `ix_task_item__parent_task_id`、`ix_task_item__milestone_id`
  - `ix_task_item__assignee_user_id__project_id__status_id`（自分の担当のステータス別集計。Phase 9 で追加）
- CHECK: `ck_task_item__date_range (start_date IS NULL OR due_date IS NULL OR start_date <= due_date)`、`ck_task_item__priority (priority BETWEEN 1 AND 4)`、`ck_task_item__number (task_number >= 1)`、`ck_task_item__not_self_parent (parent_task_id IS NULL OR parent_task_id <> id)`
- 「親・ステータス・マイルストーン・担当者が同一プロジェクト」「親子は 1 段」は複数表にまたがるため **Application で保証**

### 3.9 `task_item_tag`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `task_id` | CHAR(36) | ○ | PK(1)、FK → task_item（CASCADE） |
| `tag_id` | CHAR(36) | ○ | PK(2)、FK → tag（CASCADE） |

- 主キー: `pk_task_item_tag (task_id, tag_id)`
- 索引: `ix_task_item_tag__tag_id`

### 3.10 `task_comment`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `task_id` | CHAR(36) | ○ | FK → task_item（CASCADE） |
| `author_user_id` | CHAR(36) | ○ | FK → user_account（RESTRICT） |
| `body` | TEXT | ○ | Markdown（最大 10,000 文字） |
| `created_at_utc` | DATETIME(6) | ○ | |
| `edited_at_utc` | DATETIME(6) | | |

- 索引: `ix_task_comment__task_id__created_at_utc`

### 3.11 `task_attachment`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `task_id` | CHAR(36) | ○ | FK → task_item（CASCADE） |
| `original_file_name` | VARCHAR(255) | ○ | 表示用（正規化済み） |
| `content_type` | VARCHAR(100) | ○ | |
| `size_bytes` | BIGINT UNSIGNED | ○ | |
| `storage_key` | VARCHAR(64) | ○ | `yyyy/MM/{guid}`。一意 |
| `sha256` | BINARY(32) | ○ | |
| `uploaded_by_user_id` | CHAR(36) | ○ | FK → user_account（RESTRICT） |
| `uploaded_at_utc` | DATETIME(6) | ○ | |

- 一意: `ux_task_attachment__storage_key`
- 索引: `ix_task_attachment__task_id`
- CHECK: `size_bytes BETWEEN 1 AND 10485760`

### 3.12 `task_history_entry`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `task_id` | CHAR(36) | ○ | FK → task_item（CASCADE） |
| `kind` | TINYINT UNSIGNED | ○ | `TaskHistoryKind` |
| `actor_user_id` | CHAR(36) | | FK → user_account（RESTRICT）。NULL はシステム |
| `old_value` | VARCHAR(500) | | 表示用値 |
| `new_value` | VARCHAR(500) | | 表示用値 |
| `occurred_at_utc` | DATETIME(6) | ○ | |

- 索引: `ix_task_history_entry__task_id__occurred_at_utc`

### 3.13 `notice`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `recipient_user_id` | CHAR(36) | ○ | FK → user_account（CASCADE） |
| `kind` | TINYINT UNSIGNED | ○ | `NoticeKind` |
| `actor_user_id` | CHAR(36) | | FK → user_account（RESTRICT） |
| `project_id` | CHAR(36) | | FK → project（SET NULL） |
| `task_id` | CHAR(36) | | FK → task_item（SET NULL） |
| `message` | VARCHAR(300) | ○ | 日本語の本文 |
| `link_path` | VARCHAR(200) | | 画面内パス（`/` で始まる相対パスのみ） |
| `read_at_utc` | DATETIME(6) | | |
| `created_at_utc` | DATETIME(6) | ○ | |

- 索引: `ix_notice__recipient_user_id__read_at_utc__created_at_utc`（未読件数・一覧）、`ix_notice__created_at_utc`（削除処理）、`ix_notice__task_id`

### 3.14 `security_audit_event`

| 列 | 型 | NN | 説明 |
|---|---|---|---|
| `id` | CHAR(36) | ○ | PK |
| `kind` | TINYINT UNSIGNED | ○ | `SecurityEventKind` |
| `actor_user_id` | CHAR(36) | | 外部キーなし（記録を独立させる） |
| `subject_user_id` | CHAR(36) | | 外部キーなし |
| `project_id` | CHAR(36) | | 外部キーなし（プロジェクト削除後も残す） |
| `ip_address` | VARCHAR(45) | | |
| `user_agent` | VARCHAR(255) | | |
| `detail_json` | JSON | | 最大 2,000 文字。秘密情報を含めない |
| `occurred_at_utc` | DATETIME(6) | ○ | |

- 索引: `ix_security_audit_event__occurred_at_utc`、`ix_security_audit_event__subject_user_id__occurred_at_utc`

## 4. 削除時の挙動と業務削除の手順

DB の `CASCADE` は「安全網」とし、業務上の削除は Application が順序を明示して行う（InnoDB の連鎖削除の順序に依存しないため）。

| 操作 | 手順（1 トランザクション） |
|---|---|
| タスク削除 | (サブタスク含む場合) 子の添付メタ・履歴・コメント・タグ付与 → 子タスク → 親の同上 → 親タスク。コミット後に添付ファイル実体を削除 |
| ステータス削除 | 対象ステータスのタスクを移動先へ更新（履歴追加）→ ステータス削除 → 残りの `sort_order` 振り直し |
| マイルストーン削除 | 該当タスクの `milestone_id` を NULL に更新（履歴追加）→ 削除 |
| メンバー削除 | 該当利用者が担当のタスクを担当者なしに更新（履歴追加、actor=NULL）→ 所属削除 |
| プロジェクト削除 | `ExecuteDelete` で task_item_tag → task_history_entry → task_comment → task_attachment → task_item（子 → 親）→ tag → milestone → workflow_status → project_membership → project。notice は SET NULL。コミット後に添付ファイル実体を削除 |

## 5. 採番（タスク番号）

```sql
-- 1 トランザクション内（READ COMMITTED 以上）
UPDATE project SET last_task_number = last_task_number + 1 WHERE id = @projectId AND archived_at_utc IS NULL;
-- 影響行数 0 → プロジェクトなし/アーカイブ中としてエラー
SELECT last_task_number FROM project WHERE id = @projectId;
INSERT INTO task_item (..., task_number, ...) VALUES (..., @number, ...);
COMMIT;
```

- `UPDATE` が行ロックを取るため、同じプロジェクトへの同時作成は直列化される。別プロジェクトは並行可能。
- `ux_task_item__project_id__task_number` が最終防衛線。
- `ITaskNumberAllocator`（Infrastructure）に実装を閉じ込め、EF Core の `ExecuteSqlInterpolatedAsync` で実行する。
- `row_version` は更新しない（プロジェクト設定の編集と競合させないため）。

## 6. 楽観的排他

| 表 | 対象 | 競合時 |
|---|---|---|
| `project` | 名前・説明の変更 | 409 `concurrency.conflict` |
| `milestone` | 変更 | 同上 |
| `task_item` | 変更（ボードでのステータス変更を含む） | 同上 |
| `user_account` | 管理者による変更 | 同上（ログイン時の失敗回数更新は `ExecuteUpdate` で `row_version` を使わない） |

クライアントは取得時の `version`（= `row_version`）を更新リクエストに含める。

## 7. 主要クエリと索引の対応

| 画面・処理 | 条件 | 使用索引 |
|---|---|---|
| タスク一覧（既定: 未完了、更新の新しい順） | `project_id = ? ORDER BY updated_at_utc DESC LIMIT 50` | `ix_task_item__project_id__updated_at_utc` |
| ボード | `project_id = ? AND status_id IN (...)` | `ix_task_item__project_id__status_id` |
| 自分のタスク | `assignee_user_id = ? AND (終了区分以外) ORDER BY due_date` + 参加・非アーカイブのプロジェクトで結合 | `ix_task_item__assignee_user_id__due_date` |
| 期限切れ | `project_id = ? AND due_date < ?` | `ix_task_item__project_id__due_date` |
| 未読お知らせ件数 | `recipient_user_id = ? AND read_at_utc IS NULL` | `ix_notice__recipient_user_id__read_at_utc__created_at_utc` |
| タイトル検索 | `title LIKE CONCAT('%', ?, '%')`（エスケープ済み）を参加プロジェクトに限定 | プロジェクト単位の範囲走査（10 万件規模で許容。不足時は FULLTEXT ngram を追加: OD-17） |
| ガント（Phase 9） | `project_id = ? AND COALESCE(start_date, due_date) <= ? AND COALESCE(due_date, start_date) >= ?`（+ 区分の条件）`ORDER BY COALESCE(start_date, due_date), task_number LIMIT 501` | `ix_task_item__project_id__due_date`（プロジェクトで絞った範囲を走査。1 プロジェクト最大 10,000 件の想定で許容） |
| ガントの日付未設定（Phase 9） | `project_id = ? AND start_date IS NULL AND due_date IS NULL` の件数と先頭 50 件 | 同上 |
| プロジェクトのステータス別件数（Phase 9） | `project_id = ? GROUP BY status_id` | `ix_task_item__project_id__status_id`（索引だけで集計できる） |
| 自分の担当のステータス別件数（Phase 9） | `assignee_user_id = ? AND project_id IN (参加中・非アーカイブ) GROUP BY status_id`（区分別は `workflow_status` と結合して `GROUP BY category`） | `ix_task_item__assignee_user_id__project_id__status_id`（Phase 9 で追加。索引だけで絞り込み・集計できる） |

## 8. 初期データ

- 業務の初期データ（既定ステータス）は **Application がプロジェクト作成時に生成**（マイグレーションの `HasData` は使わない）。
- 初期システム管理者は起動時シーダーで作成（[migration-strategy.md](migration-strategy.md) §6）。
- デモデータは本番に入れない。開発用シーダーは Phase 5 で別途用意（OD-19）。

## 9. Phase 3 で確認する技術事項

- MySql.EntityFrameworkCore 10 での `Guid` の既定マッピング（CHAR(36) になるか、`ascii` 文字セットを列単位で指定できるか）
- `DateOnly` → `DATE` の対応
- `REGEXP_LIKE` を含む CHECK 制約をマイグレーションで生成できるか（不可なら `migrationBuilder.Sql` で追加）
- 外部キー名・索引名が 64 文字以内か（超えるものは省略形に変更し、本書を更新）

## 10. Phase 4 での確定・変更（2026-10-08）

マイグレーション `InitialSchema` の生成結果（`dotnet ef migrations script`）で確認した内容と、設計からの変更。詳細な理由は docs/implementation/status.md §5。

| 項目 | 結果 |
|---|---|
| Guid の型（§9） | `char(36)`（MySql.EntityFrameworkCore 10.0.9 の既定）。OD-05 は CHAR(36) で確定 |
| `DateOnly` | `date`。MySql.EntityFrameworkCore 10.0.9 は DATE 列を `DateTime` として読み込むため、`DateOnly` ⇔ `DateTime` の値変換を全列に設定（Phase 5 開始時にローカル MySQL で検出。列の型は変わらない） |
| CHECK 制約 | `REGEXP_LIKE` を含め生成できた。ただし **`ck_task_item__not_self_parent` は作らない**（`parent_task_id` は ON DELETE CASCADE のため、MySQL は CHECK で参照できない: エラー 3823） |
| 名前の長さ | 外部キー名の最長は 55 文字、インデックス名の最長は 57 文字（上限 64 以内） |
| 監査列 | `created_by_user_id` / `updated_by_user_id` はすべての表で NULL 可（§3.3 の NN から変更） |
| DB の既定値 | 付けない（値は常にアプリが設定する。EF Core の既定値機能による保存値の取り違えを避けるため） |
| 追加の索引 | MySQL が外部キー列に要求する索引（例: `ix_task_item__status_id`、`ix_task_comment__author_user_id` 等）を EF Core が作成し、名前を §4 の規則に揃えた |
| `DateTime` の読み込み | `DATETIME(6)` は時差を持たないため、読み込み時に UTC として扱う変換を全列に適用 |

## 11. Phase 9 での変更（2026-10-16）

ガントチャートとステータス別円グラフの追加（docs/corrections/phase-09/）。

| 項目 | 内容 | 理由 |
|---|---|---|
| 表・列 | **変更なし**。開始日（`start_date`）・期限（`due_date`）・ステータス（`status_id`）・楽観的排他（`row_version`）は既存の列を使う | 必要な項目は Phase 2 の設計で既にある |
| 日時・日付 | 既存方針のまま（日付は `DATE`、日時は UTC の `DATETIME(6)`）。新しい日時の列はない | NFR-10 |
| 索引の追加 | `ix_task_item__assignee_user_id__project_id__status_id`（`task_item`: `assignee_user_id`, `project_id`, `status_id`） | 自分の担当のステータス別集計を、表の行を読まずに索引だけで行うため。既存の `ix_task_item__assignee_user_id__due_date` は集計に使う `status_id` を含まない。名前は 53 文字（上限 64 以内）、命名規則 `ix_{表}__{列...}` に従う |
| 集計 | `GROUP BY`（EF Core の `GroupBy` + `Count` を SQL に変換）。アプリでの全件読み込みはしない | SKILL.md「集計は DB 側で行う」 |
| SQL | 生 SQL・文字列連結は使わない（LINQ のみ） | security-design.md S-09 |
| マイグレーション | `AddTaskStatusSummaryIndex`（索引の追加・削除のみ） | migration-strategy.md §8 |

