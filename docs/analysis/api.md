# API 解析（Plane `preview`）

## 目的

Plane の REST API を実コードから追跡し、TaskManager の REST API / Swagger(OpenAPI) 設計（Phase 2）に必要な情報（メソッド、ルート、入出力、ステータス、認証・認可、検証、エラー、ページネーション、フィルタ、ソート）を抽出する。

> エンドポイントの全一覧は [appendix-api-endpoints.md](appendix-api-endpoints.md)（機械抽出）。Plane のルート名・API 名は TaskManager へ流用しない。

## 解析範囲と根拠

- ルーティング: `apps/api/plane/urls.py`、`app/urls/*.py`、`api/urls/*.py`、`space/urls/*.py`、`authentication/urls.py`、`license/urls.py`
- 代表チェーン: 作業項目（`app/views/issue/base.py`、`app/serializers/issue.py`）、サイクル割当（`app/views/cycle/issue.py`）、プロジェクト作成（`app/views/project/base.py`）、ワークスペース作成（`app/views/workspace/base.py`）、ファイル（`app/views/asset/v2.py`）
- 共通: `app/views/base.py`、`api/views/base.py`、`utils/paginator.py`、`utils/issue_filters.py`、`utils/order_queryset.py`
- Frontend 呼び出し元: `apps/web/services/issue/issue.service.ts`、`apps/web/services/api.service.ts`
- OpenAPI: `settings/openapi.py`、`utils/openapi/`

## API の系統（確認済み）

| 系統 | Prefix | 利用者 | 認証 | 件数（抽出） |
|---|---|---|---|---|
| 内部 API | `/api/` | web / admin / space | セッション Cookie（CSRF チェック無効） | 233 |
| 公開 API v1 | `/api/v1/` | 外部連携 | `X-Api-Key` ヘッダー | 73 |
| 公開ボード | `/api/public/` | space（匿名可） | 任意（セッション） | 25 |
| 認証 | `/auth/` | web / space | フォーム POST + 302 リダイレクト、CSRF トークン必須 | 37 |
| インスタンス管理 | `/api/instances/` | admin | 管理者セッション（別 Cookie） | 約 14 |

- 同一機能が内部 API と公開 API に二重実装されている（例: 作業項目は `app/views/issue/base.py` と `api/views/issue.py`）。
- `issues` と `work-items`、`intake` と `inbox` のような旧名・新名のエイリアスが同じ View に向いている（後方互換）。

## URL 設計の特徴

- 階層型: `/api/workspaces/{slug}/projects/{project_id}/issues/{issue_id}/comments/{pk}/`
- ワークスペースは slug（文字列）、それ以下は UUID。
- 人が読む ID: `/api/workspaces/{slug}/work-items/{PROJECT_IDENTIFIER}-{sequence}/`
- 状態変更系はサブリソース + 動詞的 URL: `.../archive/`（POST=アーカイブ / DELETE=解除）、`.../subscribe/`、`.../read/`（POST=既読 / DELETE=未読）、`.../mark-default/`、`.../transfer-issues/`、`bulk-delete-issues/`、`bulk-archive-issues/`
- 末尾スラッシュ必須（Frontend の `normalizeAPIRequestURL` で補正）。

## 代表チェーン 1: 作業項目 CRUD（確認済み）

### 一覧 `GET /api/workspaces/{slug}/projects/{project_id}/issues/`

- 呼び出し元: `IssueService.getIssues` → `getIssuesFromServer`（`expand` に `issue_relation` があり `group_by` がない場合は `issues-detail/` を使う）← `ProjectIssues` store の `fetchIssues` ← `ProjectLayoutRoot`。
- 認可: `@allow_permission([ADMIN, MEMBER, GUEST])`（プロジェクトレベル）。さらにゲストかつ `Project.guest_view_all_features=False` の場合は自分が作成した項目のみ。
- Query: フィルタ（下記）、`order_by`（allowlist）、`group_by`、`sub_group_by`（同一値は 400）、`cursor`、`per_page`、`updated_at__gt`（差分同期）、`expand`、`fields`。
- 応答 200（ページネーション形式、下記）。グループ化時は `results` がグループ ID → `{results, total_results}` の辞書になる。

### 作成 `POST .../issues/`

- 認可: ADMIN / MEMBER（ゲスト不可）。
- Request（主な項目）: `name`(必須, 255), `description_html`, `state_id`, `parent_id`, `priority`, `start_date`, `target_date`, `assignee_ids[]`, `label_ids[]`, `estimate_point`, `type_id`, `cycle_id`/`module_ids`（後者は別 API で関連付け）。
- 検証（`IssueCreateSerializer.validate`）:
  - `start_date` ≤ `target_date`（違反は 400 "Start date cannot exceed target date"）
  - `description_html` の内容検証（不正は 400）、`description_binary` 検証
  - `assignee_ids` は同プロジェクトの role ≥ 15 かつ有効なメンバーのみに **黙って絞り込む**（エラーにしない）
  - `label_ids` は同プロジェクトのラベルのみに絞り込む
  - `state_id` は同プロジェクトの非 Triage 状態であること（不正は 400）
  - `parent_id` は同ワークスペースの作業項目であること（不正は 400）
  - `estimate_point` は同プロジェクトのもの
- 副作用: 採番、既定状態、既定担当者（担当者未指定時）、履歴・通知・Webhook・説明文バージョンを非同期発行。
- 応答: **201** + 一覧行と同じ軽量 JSON（`id, name, state_id, sort_order, ..., label_ids, assignee_ids, module_ids, cycle_id, sub_issues_count, attachment_count, link_count`）。検証エラーは 400 + DRF 形式のエラー辞書。

### 取得 `GET .../issues/{pk}/`

- 認可: ADMIN / MEMBER / GUEST、または作成者（`creator=True`）。ゲスト制限は同上（403 "You are not allowed to view this issue"）。存在しない場合 404。
- 副作用: 最近見た項目を記録（`recent_visited_task`）。
- 応答 200: `IssueDetailSerializer`（`?expand=` で関連展開）。

### 更新 `PATCH .../issues/{pk}/`

- 認可: ADMIN / MEMBER、または作成者。
- `skip_activity` フラグ（説明文移行時に履歴を残さない）。
- 応答: **204 No Content**（本文なし）。Frontend は楽観的更新を前提にしている（有力）。
- 副作用: 更新前スナップショットを JSON 化して履歴タスクへ渡し、差分を履歴化。

### 削除 `DELETE .../issues/{pk}/`

- 認可: ADMIN、または作成者。論理削除 + 最近見た項目から物理削除 + 履歴。応答 204。

### その他の作業項目系

- サブ作業項目、リンク、添付、履歴（`history/`）、コメント、購読、リアクション、関係（blocked_by / relates_to / duplicate / start_before / finish_before / implemented_by。逆方向名は blocking / start_after / finish_after / implements）、アーカイブ、削除済み一覧、一括日付更新（`issue-dates/`）、一括削除（ADMIN のみ）、一括アーカイブ、バージョン、説明文バージョン。

## 代表チェーン 2: サイクルへの追加 `POST .../cycles/{cycle_id}/cycle-issues/`（確認済み）

- Request: `{"issues": [uuid, ...]}`。空なら 400。
- 終了日が過去のサイクルは 400。
- 他サイクルに属していた作業項目は付け替え（移動）、それ以外は新規作成。ワークスペース + プロジェクトで絞り込み（過去の IDOR 修正: GHSA-4w5x-wc9w-f47x のコメントあり）。

## 代表チェーン 3: プロジェクト作成 `POST /api/workspaces/{slug}/projects/`

- 認可: ワークスペースの ADMIN / MEMBER。
- 副作用: 作成者を ADMIN としてプロジェクトメンバー登録、プロジェクトリードが別人なら追加登録、既定状態 6 件作成、Webhook。応答 201。
- `identifier` 重複は 409 系（`project-identifiers/` で事前確認 API あり）。

## 代表チェーン 4: ワークスペース作成 `POST /api/workspaces/`

- 検証: name と slug 必須（400）、name ≤ 80 / slug ≤ 48（400）、name に URL 禁止（400）、slug 重複は 409 `{"slug": "..."}`。
- 副作用: 作成者を Admin(20) として登録、デモデータ投入タスク `workspace_seed.delay`。応答 201。

## 代表チェーン 5: ファイルアップロード（確認済み）

```text
1. POST /api/assets/v2/workspaces/{slug}/  {name, type, size, entity_type, entity_identifier}
   → MIME 許可リスト確認、size を FILE_SIZE_LIMIT で上限、FileAsset 作成(is_uploaded=False)
   → 200 {upload_data(presigned POST), asset_id, asset_url}
2. Browser → S3 へ直接 POST
3. PATCH /api/assets/v2/workspaces/{slug}/{asset_id}/  → is_uploaded=True, 204
4. GET  .../{asset_id}/ → presigned GET へ 302（未アップロードは 404）
```
- スクリプト実行可能な MIME（SVG、JS、HTML、XML 等）は `Content-Disposition: attachment` で配信。
- 未完了アップロードは日次で削除。

## ページネーション（確認済み）

- カーソル形式 `cursor=<per_page>:<page>:<is_prev>`（実体はオフセット）。`per_page` 既定・上限 1000（呼び出し側で変更）。負や巨大なオフセットは `BadPaginationError`。
- 応答:

```json
{
  "grouped_by": null, "sub_grouped_by": null,
  "total_count": 0, "next_cursor": "100:1:0", "prev_cursor": "100:-1:1",
  "next_page_results": false, "prev_page_results": false,
  "count": 0, "total_pages": 0, "total_results": 0,
  "extra_stats": null, "results": []
}
```
- ページネーションしない一覧 API も多い（配列をそのまま返す）。

## フィルタ（`utils/issue_filters.py`、確認済み）

クエリキー: `state`, `state_group`, `estimate_point`, `priority`, `parent`, `labels`, `assignees`, `mentions`, `created_by`, `logged_by`, `name`, `created_at`, `updated_at`, `start_date`, `target_date`, `completed_at`, `type`, `project`, `cycle`, `module`, `intake_status`, `inbox_status`, `sub_issue`, `subscriber`, `start_target_date`。複数値はカンマ区切り、UUID は妥当性チェックで不正値を除外。日付は範囲・相対指定に対応。

加えて保存ビュー用の `rich_filters`（JSON の式）があるが、詳細は未精読（Q-API-02）。

## ソート

- `order_by` パラメータ、先頭 `-` で降順。モデル毎の allowlist で制限（SQL インジェクション・情報漏えい防止）。不正値は既定（`-created_at`）にフォールバック（有力）。

## エラー形式（確認済み、不統一）

| 発生源 | 形式 | ステータス |
|---|---|---|
| View の明示エラー | `{"error": "メッセージ"}` | 400 / 403 / 404 / 409 |
| DRF Serializer | `{"field": ["msg"]}` または `{"non_field_errors": [...]}` | 400 |
| ワークスペース作成 | `["msg", ...]`（配列） | 400 |
| 共通例外ハンドラ | `{"error": "The payload is not valid"}` 等 | 400 / 404 / 500 |
| 認証（/auth/） | エラーコードをクエリ文字列に付けてリダイレクト | 302 |
| 権限不足 | `{"error": "You don't have the required permissions."}` | 403 |
| 未認証 | DRF 既定 | 401（Frontend は `/` へリダイレクト） |

## ステータスコードの傾向

- 作成 201、更新は 200（本文あり）と 204（本文なし）が混在、削除 204、重複 409、検証 400、未認証 401、権限 403、未存在 404、レート制限 429（DRF Throttle）。

## Swagger / OpenAPI（確認済み）

- drf-spectacular 0.29。`ENABLE_DRF_SPECTACULAR=1` のときのみ `/api/schema/`、`/api/schema/swagger-ui/`、`/api/schema/redoc/` を公開（既定 OFF）。
- 対象は `/api/v1/` のみ（`SCHEMA_PATH_PREFIX`、前処理フックで v1 以外を除外）。内部 API はスキーマ化されていない。
- `@extend_schema` は v1 の一部 View（invite、issue、member 等）で使用。`utils/openapi/` に共通パラメータ・応答・例・認証定義がある。
- タグ単位の説明（Assets、Cycles、Intake、…）を設定で記述。

### TaskManager の OpenAPI 設計に必要な情報（抽出）

- リソース毎の: パスパラメータ（ワークスペース / プロジェクト / 対象 ID）、Query（フィルタ・ソート・ページ）、Request DTO（必須 / 任意 / 長さ / 列挙値）、Response DTO（一覧用の軽量表現と詳細表現の 2 種）、エラー応答（統一形式）、認証スキーム（Cookie または Bearer、API Key）
- 列挙値: 優先度 5 値、状態グループ 6 値、ロール 3 値、関係種別 6 値（+逆方向）、受付ステータス 5 値、モジュール状態 6 値、見積もり種別 2 値、ビュー公開範囲 2 値
- Plane では内部 API がスキーマ化されていないため、TaskManager では全 API を OpenAPI 契約として扱う必要がある（CLAUDE.md §13）。

## DB への影響（作業項目更新時）

`issues` 更新 → 中間テーブル（担当者 / ラベル）差し替え → 非同期で `issue_activities`、`notifications`、`issue_mentions`、`issue_subscribers`、`issue_description_versions`、`webhook_logs`、`email_notification_logs` に書き込み。

## Frontend 呼び出しとの不一致（発見事項）

- `IssueService.bulkOperations` → `POST .../bulk-operation-issues/` を `store/issue/helpers/base-issues.store.ts` から呼ぶが、Backend（CE）に該当ルートがない（確認済み）。CE の一括操作 UI（`components/issues/bulk-operations/root.tsx`）は操作ボタンではなく有償版案内のバナー（`MARKETING_PLANE_ONE_PAGE_LINK`）を表示するため、有償版専用機能の呼び出しが CE に残っているものと判断（有力。2 つの根拠あり）。
- `IssueService.getIssueDisplayProperties` / `updateIssueDisplayProperties` → `.../issue-display-properties/` も Backend に存在しない（実ルートは `.../user-properties/`）。ただし Frontend 内で呼び出し元はなく未使用（確認済み）。

## 不明点

- v1 API のシリアライザとの差分の網羅（Q-API-01）
- `rich_filters` の式の仕様（Q-API-02）
- 更新 API が 204 を返すものと 200 を返すものの使い分け基準（Q-API-03）

## TaskManager への影響

- エラー形式が不統一なので、TaskManager は RFC 9457（Problem Details）等で統一するのが望ましい（設計候補）。
- 「担当者の不正 ID を黙って捨てる」挙動は利用者に分かりにくい。TaskManager では明示的な検証エラーにするか Phase 2 で決める。
- 一覧と詳細でレスポンス形を分ける設計（軽量一覧 + 詳細）は性能面で参考になる。
- グループ化ページネーションは複雑。TaskManager の UI 要件（カンバン等）次第で採否を決める。

## Phase 2 で決定する事項

- URL 体系（階層の深さ、ID 方式、人が読む番号でのアクセス可否）
- 統一エラー形式とエラーコード
- ページネーション方式（offset / cursor）と既定・上限件数
- フィルタ・ソートの仕様と allowlist
- 更新 API の応答（200 + 本文 / 204）
- 一括操作 API の範囲
- 公開 API（API Key）の要否
- OpenAPI の生成方式（Swashbuckle / Microsoft.AspNetCore.OpenApi 等）とセキュリティスキーム
