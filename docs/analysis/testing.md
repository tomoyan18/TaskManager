# テスト解析（Plane `preview`）

## 目的

Plane のテスト構造（種類・配置・フィクスチャ・テスト DB・認証/認可テスト）と、重要機能がどうテストされているかを把握し、TaskManager のテスト設計（Phase 5〜6）の材料にする。

## 解析範囲と根拠

- `apps/api/pytest.ini`、`apps/api/plane/settings/test.py`
- `apps/api/plane/tests/`（conftest.py、factories.py、contract/、unit/、smoke/）
- `apps/api/plane/tests/TESTING_GUIDE.md`、`apps/api/tests/RUNNING_TESTS.md`（存在確認）
- `docker-compose-test.yml`、`AGENTS.md`
- `.github/workflows/*.yml`
- Frontend: `*.test.ts(x)` / `*.spec.ts` の検索、`vitest.config.ts`

## Backend テスト（確認済み）

### 構成

| 種類 | マーカー | 配置 | ファイル数 |
|---|---|---|---|
| Unit | `unit` | `tests/unit/{bg_tasks,middleware,models,serializers,settings,utils,views}` | 約 37 |
| Contract（API） | `contract` | `tests/contract/api`（公開 API v1）、`tests/contract/app`（内部 API） | 18 + 22 |
| Smoke | `smoke` | `tests/smoke` | 2 |
| （遅いテスト） | `slow` | — | — |

- pytest + pytest-django。`--reuse-db --nomigrations`（マイグレーションを適用せずモデルから直接テーブル作成）。
- 設定 `plane.settings.test`: `DEBUG=True`、メールは `locmem`（送信しない）。

### Fixture / Factory

- `conftest.py`: `api_client`、`create_user`、`api_token`、`api_key_client`（`X-Api-Key` 付き）、`session_client`（セッションログイン済み）、`create_bot_user`、`workspace`、`plane_server`（`live_server`）など。
- `factories.py`（factory_boy）: User、Workspace、WorkspaceMember、Project、ProjectMember。
- Mock: Celery タスク呼び出しや S3 はテスト毎にモック（`test_copy_s3_objects.py` 等、詳細未網羅）。

### Test DB / 実行環境

- `docker-compose-test.yml`: PostgreSQL / Valkey / RabbitMQ / MinIO を tmpfs で起動し、ヘルスチェック後に `api-tests` コンテナで pytest。外部 SMTP は `test-smtp.invalid` で無効化。
- 実行: `docker compose -f docker-compose-test.yml up --build --abort-on-container-exit --exit-code-from api-tests`

### CI での実行状況（確認済み）

- `.github/workflows/` の API 用ワークフローは `ruff check`（lint）と `manage.py check` / `makemigrations --check --dry-run`（マイグレーション漏れ検出）のみ。**pytest を実行するワークフローは見つからない**。
- Web 用ワークフローは build / lint / 型チェック。CodeQL、i18n 同期チェック、著作権表記チェックもある。

## 重要機能のテスト状況

| 機能 | テスト | 観点 |
|---|---|---|
| 認証 | `contract/app/test_authentication.py`、`contract/api/test_authentication.py`、`unit/middleware/test_api_authentication.py` | セッション / API Key の成功・失敗 |
| API Token | `contract/app/test_api_token.py` | 作成・取得 |
| 作業項目 | `contract/api/test_issues.py`、`test_issue_assignee_label_validation.py`、`test_issue_attachment_size.py`、`test_issue_notifications.py`、`contract/app/test_issue_history_app.py`、`unit/serializers/test_issue_serializer_api.py` | CRUD、担当者・ラベル検証、添付サイズ、通知、履歴 |
| 認可（IDOR） | `*_scope_app.py` 系 9 本、`test_project_member_is_active_authz.py`、`test_project_join_cross_project_activation.py`、`test_project_members_roster_scope.py` | 他プロジェクト・他テナントのオブジェクトにアクセスできないこと（脆弱性の回帰テスト） |
| サイクル / モジュール | `test_cycles.py`、`test_cycle_issue_app.py`、`test_module_issue_destroy_app.py` | |
| ワークスペース / プロジェクト | `test_workspace_app.py`、`test_project_app.py`、`unit/models/test_workspace_model.py` | |
| フィルタ・並び替え・ページング | `unit/utils/test_issue_filters.py`、`test_issue_datetime_filters.py`、`test_order_by_sanitize.py`、`test_order_queryset.py`、`test_paginator.py` | allowlist、日付フィルタ、オフセット境界 |
| セキュリティ（入力・出力） | `test_csv_export_sanitization.py`、`test_xlsx_export_sanitization.py`、`test_path_validator.py`、`test_url.py`、`bg_tasks/test_ssrf_advisories.py`、`test_url_security.py`、`test_non_object_json_body.py` | CSV/数式インジェクション、オープンリダイレクト、SSRF、不正 JSON |
| 設定 | `unit/settings/test_retention.py`、`test_storage.py` | 保持期間、ストレージ |
| ページ | `test_page_order_by_allowlist_app.py`、`test_page_version_project_scope_app.py` | |

## Frontend テスト（確認済み）

- `apps/web`、`apps/admin`、`apps/space`: テストファイルなし。
- `packages/blocks`: vitest + @testing-library/react で UI 部品（toast、dialog、select、context-menu 等）をテスト。
- `packages/services`、`packages/codemods`、`apps/live`（PDF エクスポート）にテストあり。

## E2E テスト

- 確認した結果、該当なし（Playwright / Cypress の設定ファイルなし）。

## 発見事項

- 認可テストは「脆弱性が見つかった後に追加された回帰テスト」が中心で、全エンドポイントを網羅する系統的な認可テスト（ロール × 操作のマトリクス）は見当たらない（有力）。
- `--nomigrations` のため、マイグレーション自体の検証はテストではなく CI の `makemigrations --check` に依存。

## TaskManager への影響

- CLAUDE.md §18 の各テスト種別のうち、Plane で参考になるのは Contract（API）テストと認可の回帰テストの観点。
- TaskManager では最初から「ロール × 操作 × 所属（自分/他プロジェクト/他テナント）」の認可マトリクステストを設計するのが望ましい（設計候補）。
- テスト DB は MySQL コンテナ（Testcontainers 等）で、マイグレーション込みで検証する案がある（設計候補）。
- Frontend の単体・E2E テストは Plane に参考実装がほぼないため、独自に設計する。

## 不明点

- `TESTING_GUIDE.md` の詳細規約（未精読、Q-TEST-01）
- テストが実際に全件成功する状態か（Plane 側では実行していない。Phase 1 では実行しない）（Q-TEST-02）

## Phase 2 で決定する事項

- テスト種別と対象範囲（Unit / Application / Integration / API / Frontend / E2E）
- テスト DB 方式（MySQL コンテナ / InMemory の扱い）
- 認可マトリクステストの設計
- OpenAPI 契約をテストに使う方法
- CI でのテスト実行の有無
