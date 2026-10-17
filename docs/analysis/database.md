# Database 解析（Plane `preview`）

## 目的

Plane のデータ構造（Entity、関連、制約、ライフサイクル、所有関係）を把握し、TaskManager の MySQL / EF Core 設計（Phase 2）の材料にする。

> **注意**: 以下の Table 名・Column 名は解析のための記録であり、TaskManager に流用してはならない（CLAUDE.md §10・§11）。TaskManager の名前は業務上の意味から新しく設計する。

## 解析範囲と根拠

- Model: `apps/api/plane/db/models/*.py`（`__init__.py` で export されている全モデル）
- 共通基底: `apps/api/plane/db/models/base.py`、`apps/api/plane/db/mixins.py`
- Migration: `apps/api/plane/db/migrations/`（0001〜0122、122 本。うち `RunPython` を含むデータ移行 32 本、`RunSQL` 0 本）
- API 側の利用: `app/views/issue/base.py`、`app/serializers/issue.py`、`app/views/cycle/issue.py`、`bgtasks/deletion_task.py`
- Frontend 型: `packages/types`（`TIssue` 等、未精読）

## DBMS

- PostgreSQL 15.7（`docker-compose.yml`）。`DATABASE_URL` または `POSTGRES_*` で接続（`settings/common.py`）。
- PostgreSQL 固有機能に依存している（後述）。

## 共通基底（確認済み）

| 基底 | 列 | 挙動 |
|---|---|---|
| `TimeAuditModel` | `created_at`(auto_now_add), `updated_at`(auto_now) | 自動設定 |
| `UserAuditModel` | `created_by`, `updated_by`（User への FK、`SET_NULL`、NULL 可） | `BaseModel.save()` で `crum.get_current_user()` から自動設定。作成時は `updated_by=None` |
| `SoftDeleteModel` | `deleted_at`（NULL 可） | 既定マネージャ `objects` は `deleted_at IS NULL` のみ返す。`all_objects` は全件。`delete()` は既定で論理削除し、`soft_delete_related_objects` を Celery で非同期実行 |
| `BaseModel` | `id` UUID（`uuid4`、PK） | 上記 3 つを合成 |
| `WorkspaceBaseModel` | `workspace` FK（CASCADE）、`project` FK（CASCADE、NULL 可） | ワークスペース所属 |
| `ProjectBaseModel` | `project` FK（CASCADE）、`workspace` FK（CASCADE） | プロジェクト所属。workspace を冗長に保持（テナント境界のフィルタに使用） |

- 主キーは全て UUID（`DEFAULT_AUTO_FIELD = BigAutoField` は設定されているが、BaseModel 継承クラスは UUID）。
- `ordering = ("-created_at",)` がほぼ全モデルの既定。

## 主要 Entity と関係（概要 ER）

```text
User ─1:1─ Profile
  │ 1:N Account（OAuth 連携）  1:N APIToken  1:N Session（user_id は文字列）
  │
Workspace（slug 一意, owner→User）
  ├─1:N WorkspaceMember（role 20/15/5, is_active）
  ├─1:N WorkspaceMemberInvite（email, token, role）
  ├─1:N Project（identifier: ワークスペース内一意, network 0=Secret/2=Public）
  │      ├─1:N ProjectMember（role 20/15/5, is_active）
  │      ├─1:N ProjectMemberInvite
  │      ├─1:N State（group: backlog/unstarted/started/completed/cancelled/triage）
  │      ├─1:N Label（project NULL ならワークスペース共通、parent で階層化）
  │      ├─1:N Issue（作業項目）
  │      │     ├─ parent → Issue（サブ作業項目、CASCADE）
  │      │     ├─ state → State（CASCADE）
  │      │     ├─ estimate_point → EstimatePoint（SET_NULL）
  │      │     ├─ type → IssueType（SET_NULL）
  │      │     ├─ M:N assignees（through IssueAssignee）
  │      │     ├─ M:N labels（through IssueLabel）
  │      │     ├─ 1:N IssueComment（parent でスレッド化、access INTERNAL/EXTERNAL）
  │      │     ├─ 1:N IssueActivity（履歴。issue は DO_NOTHING）
  │      │     ├─ 1:N IssueLink / IssueAttachment / FileAsset
  │      │     ├─ 1:N IssueRelation（related_issue, relation_type）
  │      │     ├─ 1:N IssueSubscriber / IssueMention / IssueReaction / IssueVote
  │      │     ├─ 1:N IssueVersion / IssueDescriptionVersion
  │      │     └─ 1:1 IssueSequence 行（採番履歴、issue 削除時 SET_NULL）
  │      ├─1:N Cycle（start_date / end_date, owned_by）─ M:N Issue（through CycleIssue）
  │      ├─1:N Module（status, lead, members）─ M:N Issue（through ModuleIssue）、1:N ModuleLink
  │      ├─1:N Estimate（type categories/points）─1:N EstimatePoint
  │      ├─1:N Intake ─1:N IntakeIssue（status -2..2, snoozed_till, duplicate_to）
  │      ├─ default_state → State, default_assignee / project_lead → User, estimate → Estimate
  │      └─ ProjectUserProperty（ユーザー毎の表示設定 JSON）
  ├─1:N IssueView（保存ビュー。project NULL ならワークスペースビュー、access 0=Private/1=Public）
  ├─1:N Page（project とは M:N through ProjectPage、parent で階層、access 0=Public/1=Private）
  ├─1:N Notification（receiver, entity_identifier, read_at, snoozed_till, archived_at）
  ├─1:N Webhook / WebhookLog, ExporterHistory, Sticky, UserFavorite, UserRecentVisit, DeployBoard
  └─1:N DraftIssue（ワークスペース単位の下書き）
```

## 主要テーブル詳細

### 作業項目（`issues`, model `Issue`）（確認済み）

| 列 | 型/制約 | 備考 |
|---|---|---|
| `id` | UUID PK | |
| `project_id`, `workspace_id` | FK CASCADE | |
| `parent_id` | FK self, CASCADE, NULL | 親削除で子も削除（論理削除は非同期で波及） |
| `state_id` | FK State, CASCADE, NULL | 未指定時は `save()` で既定状態を補完 |
| `name` | varchar(255) | 必須 |
| `description_json` / `description_html` / `description_stripped` / `description_binary` | JSON / text / text / bytea | エディタ（Tiptap+Yjs）用の多重表現。`description_stripped` は HTML タグ除去後 |
| `priority` | varchar(30) | `urgent/high/medium/low/none` |
| `start_date`, `target_date` | date NULL | 検証: start ≤ target（Serializer） |
| `sequence_id` | int | プロジェクト内連番。表示 ID は `{Project.identifier}-{sequence_id}` |
| `sort_order` | float 既定 65535 | 新規は同状態内最大値 + 10000 |
| `completed_at` | datetime NULL | 状態グループが `completed` に変わると現在時刻、それ以外で NULL |
| `archived_at` | **date** NULL | アーカイブ日（Cycle/Module は datetime。型が不統一） |
| `is_draft` | bool | |
| `point` | int 0..12 NULL | 旧見積もり（`estimate_point` FK と併存） |
| `external_source`, `external_id` | | インポート元追跡 |

- 既定マネージャ以外に `issue_objects`（Triage 状態・アーカイブ済み・アーカイブ済みプロジェクト・下書きを除外）がある。一覧 API はこれを使う。
- 採番: `pg_advisory_xact_lock(project_id のハッシュ)` でプロジェクト単位に直列化し、`IssueSequence` の最大値 + 1（`db/models/issue.py:180-214`）。削除された番号は再利用されない（IssueSequence 行を残すため）。

### 状態（`states`, model `State`）

- `group` は 6 種（backlog / unstarted / started / completed / cancelled / triage）。業務上の意味（集計・自動クローズ・completed_at）は group で判断し、名前は自由。
- `default`（bool）で新規作業項目の既定状態。`sequence`（float）で並び順。
- 一意: (`name`, `project`) ※論理削除されていない行のみ。
- 既定マネージャ `objects` は triage を除外、`triage_objects` は triage のみ。
- プロジェクト作成時に `DEFAULT_STATES`（Backlog / Todo / In Progress / Done / Cancelled / Triage 相当の 6 件）を一括作成（`app/views/project/base.py`）。

### ワークスペース・メンバー

- `Workspace.slug`: 最大 48 文字、全体一意、予約語禁止（`RESTRICTED_WORKSPACE_SLUGS`）。`name` 最大 80。
- `WorkspaceMember`: `role` 20=Admin / 15=Member / 5=Guest（既定 5）、`is_active`。一意 (workspace, member)。
- `ProjectMember`: 同じ role 値、`is_active`、表示設定 JSON、`sort_order`。一意 (project, member)。
- `Project.identifier`: 最大 12 文字、ワークスペース内一意。`ProjectIdentifier` テーブルでも一意性を管理（論理削除後の再利用防止目的と推定 → 有力）。
- `Project.name`: ワークスペース内一意。

### サイクル / モジュール

- `Cycle`: `start_date`/`end_date` は datetime、`owned_by`、`archived_at`、`progress_snapshot`（JSON）、`timezone`、`version`。新規は既存最小 `sort_order` − 10000（先頭に追加）。
- `CycleIssue`: DB 上の一意は (cycle, issue) だが、**1 作業項目は同時に 1 サイクルのみ** というルールはアプリ側で実装（別サイクルの行があれば `cycle_id` を付け替える。`app/views/cycle/issue.py`）。DB 制約では保証されていない（確認済み）。
- 終了日を過ぎたサイクルには追加不可（400）。
- `Module`: `status`（backlog / planned / in-progress / paused / completed / cancelled）、`lead`、`members`（M:N）、名前はプロジェクト内一意。

### 受付（Intake）

- `IntakeIssue.status`: -2=Pending, -1=Rejected, 0=Snoozed, 1=Accepted, 2=Duplicate（既定 -2）。受付中の作業項目は Triage 状態に置かれ、一覧からは除外される。

### 通知

- `Notification`: `receiver`、`entity_name`/`entity_identifier`（汎用参照、FK ではない）、`read_at`、`snoozed_till`、`archived_at`。複合インデックス 9 個（受信者 × ワークスペース × 既読 × 作成日時 等）。
- `UserNotificationPreference`: property_change / state_change / comment / mention / issue_completed の bool。
- `EmailNotificationLog`: `processed_at`、`sent_at`（まとめ送信用キュー）。

### その他

- `IssueView`: `filters`、`display_filters`、`display_properties`、`rich_filters`（全て JSON）、`access`、`is_locked`、`owned_by`。
- `UserFavorite`: `entity_type`/`entity_identifier` の汎用参照、`is_folder` と `parent` でフォルダ化。
- `APIToken`: `token` 文字列を **平文で一意列に保存** し、認証時に完全一致検索（`api/middleware/api_authentication.py`、確認済み）。
- `Webhook`: `secret_key` 平文保存（HMAC 署名用）。
- `Session`: `session_key` PK、`user_id` は文字列、`device_info` JSON。

## 制約・インデックスのパターン

- **部分一意制約（Partial Unique）**: モデル内に `UniqueConstraint(..., condition=Q(deleted_at__isnull=True))` が 40 箇所。論理削除済み行を一意性から除外するため。旧来の `unique_together = [..., "deleted_at"]` も併存。
- 汎用参照（`entity_name` + `entity_identifier`）を持つテーブル（Notification、UserFavorite、UserRecentVisit、DeployBoard、FileAsset、PageLog）は FK 制約がなく、整合性はアプリ依存。
- `Label` の一意: project NULL の場合 (name)、project 非 NULL の場合 (project, name)。

## Delete behavior / Lifecycle

| 対象 | 削除方法 | 波及 |
|---|---|---|
| ほぼ全 Entity | 論理削除（`deleted_at` 設定） | `soft_delete_related_objects` が関連を非同期に論理削除 |
| 論理削除済み行 | 日次 `hard_delete`（UTC 0:00）で `HARD_DELETE_AFTER_DAYS`（既定 60 日）経過分を物理削除 | DB の CASCADE |
| 作業項目のアーカイブ | `archived_at` 設定（手動 / 一括 / 自動） | `issue_objects` から除外 |
| 自動アーカイブ | `Project.archive_in`（月数 0〜12）経過で completed/cancelled の項目をアーカイブ | 日次 |
| 自動クローズ | `Project.close_in`（月数 0〜12）経過で未完了項目を `default_state` または cancelled 状態へ | 日次 |
| ログ類 | API ログ 14 日、Webhook ログ 14 日、メール通知ログ 7 日で削除（環境変数で変更可） | 日次 |

## Ordering

- 手動並び替えは `sort_order`（float、既定 65535、間に挿入できるよう 10000 刻み）を全面採用（Issue、State は `sequence`、Cycle、Module、View、Favorite、ProjectMember 等）。
- 並び替え API の `order_by` は allowlist で制限（`utils/order_queryset.py`。例: 作業項目は created_at / updated_at / sequence_id / sort_order / target_date / start_date / completed_at / archived_at / priority / state__name / state__group / assignees__first_name / labels__name / issue_module__module__name）。
- 優先度・状態は意味順（urgent→none、backlog→cancelled）での並び替えに対応。

## Concurrency

- 採番は PostgreSQL advisory lock（2 箇所で使用）。
- 楽観的排他（バージョン列 / ETag）は確認できない。更新は後勝ち（有力。全モデルを網羅確認してはいない）。
- 共同編集（説明文・ページ）は Yjs の CRDT（`description_binary`）で競合解決。

## Data validation（DB 外）

- 日付順序、HTML 内容検証、バイナリ検証、担当者は役割 15 以上のアクティブなプロジェクトメンバーのみ、ラベル・状態・親・見積もりは同プロジェクト（親は同ワークスペース）であること（`IssueCreateSerializer.validate`）。
- ワークスペース名は URL を含めない、長さ制限（View）。

## PostgreSQL 固有機能の使用状況（MySQL 移行時の論点）

| 機能 | 使用量 | MySQL での扱い（Phase 2 で検討） |
|---|---|---|
| 部分一意制約（WHERE 付き UNIQUE INDEX） | モデル内 40 箇所 | MySQL は非対応。生成列 + 一意インデックス、論理削除の方式変更、またはアプリ側検証で代替 |
| `ArrayAgg` / `ArrayField` | 16 / 21 ファイル | JSON 集約（`JSON_ARRAYAGG`）、別クエリ、またはアプリ側集約 |
| `pg_advisory_xact_lock` | 2 ファイル | `SELECT ... FOR UPDATE` の採番テーブル、または `GET_LOCK()` |
| `JSONField` | モデル内 95 箇所 | MySQL `JSON` 型で可能。ただし表示設定以外は正規化を検討 |
| `BinaryField`（Yjs） | 8 箇所 | `LONGBLOB`。共同編集を採用しないなら不要 |
| `icontains` 検索 | 8 ファイル | 照合順序（`utf8mb4_0900_ai_ci` 等）次第。全文検索は FULLTEXT（ngram）を検討 |
| UUID PK | 全モデル | MySQL では `CHAR(36)` または `BINARY(16)`。クラスタ化インデックスの断片化に注意 |

## データの関係上の注意点（発見事項）

- `Issue.archived_at` は date、`Cycle/Module.archived_at` は datetime と型が不統一（確認済み）。
- `close_old_issues` で `project.default_state` が NULL の場合、`State.objects.filter(group="cancelled").first()` を **プロジェクトで絞り込まずに** 取得している（`bgtasks/issue_automation_task.py:119-120`）。他プロジェクトの状態が設定される可能性がある（有力。実行による確認はしていない）。
- `Issue.point`（int）と `estimate_point`（FK）が併存しており、移行途中の痕跡と思われる（有力）。

## 不明点

- Frontend 型（`packages/types`）と API 応答の一致状況は一部のみ確認（Q-DB-01）。
- 論理削除の関連波及（`soft_delete_related_objects`）がどの関連を対象とするかの詳細（Q-DB-02）。

## TaskManager への影響

- 論理削除 + 部分一意制約の組み合わせは MySQL ではそのまま再現できない。論理削除を採用するかどうか自体を Phase 2 で決める必要がある。
- 汎用参照（entity_name + id）は FK 制約が効かないため、TaskManager では必要最小限にとどめるのが望ましい（設計候補）。
- 採番（プロジェクト内連番）は重要な業務要件。MySQL での排他方式を設計する。
- 表示設定を JSON 列に持つ方式は柔軟だが検証しにくい。

## Phase 2 で決定する事項

- 論理削除の採否と、一意制約との両立方法
- 主キー方式（UUID / 連番 + 公開 ID）
- 作業項目の番号採番方式と排他制御
- 監査列（作成者・更新者・日時）の自動設定方式
- 楽観的排他（RowVersion 等）の採否
- 表示設定・フィルタの保存形式（JSON / 正規化）
- 履歴（アクティビティ）テーブルの粒度
- Delete behavior（CASCADE / RESTRICT / SET NULL）の方針
