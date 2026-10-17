# アーキテクチャ解析（Plane `preview`）

## 目的

Plane の Backend / Frontend / 非同期処理 / ストレージ / デプロイ構造を把握し、TaskManager の設計判断（Phase 2）に使う。採用を決定するものではない。

## 解析範囲

`apps/api/plane/settings/common.py`、`apps/api/plane/urls.py`、`apps/api/plane/app/views/base.py`、`apps/api/plane/celery.py`、`apps/api/plane/db/mixins.py`、`apps/web/services/api.service.ts`、`apps/web/store/`、`docker-compose.yml`、`apps/proxy/Caddyfile.ce`、`apps/live/src/`。

## 全体構成

```text
Browser
  │
  ▼
Caddy proxy（80/443、TLS、request_body max_size = FILE_SIZE_LIMIT）
  ├─ /*            → web   (React SPA, 静的配信)
  ├─ /god-mode/*   → admin (React SPA)
  ├─ /spaces/*     → space (React SPA)
  ├─ /live/*       → live  (Node + Hocuspocus WebSocket)
  ├─ /api/*, /auth/*, /static/* → api (Django, gunicorn + uvicorn worker, ASGI)
  └─ /uploads/*    → MinIO
api ─┬─ PostgreSQL（主DB、任意で read replica）
     ├─ Redis/Valkey（キャッシュ、Magic code、ロック、webhook origin 等）
     ├─ RabbitMQ（Celery broker）
     └─ MinIO / S3（presigned URL で直接アップロード）
worker（celery worker） / beat-worker（celery beat, DB スケジューラ）
migrator（起動時に migrate を 1 回実行して終了）
```

根拠: `docker-compose.yml`、`apps/proxy/Caddyfile.ce`、`apps/api/bin/docker-entrypoint-*.sh`（確認済み）。

## Backend architecture

### レイヤ構造（確認済み）

Django の標準構成で、明確な Application / Domain 層の分離はない。

```text
urls.py → View（APIView / ModelViewSet）
          ├─ 認可デコレータ @allow_permission / permission_classes
          ├─ Serializer（入力検証 + 永続化ロジックの一部）
          ├─ Model.save()（採番・既定値・completed_at 同期などのドメインルール）
          ├─ QuerySet（アノテーション、フィルタ、並び替え、グルーピング）
          └─ Celery タスク .delay()（アクティビティ記録、通知、Webhook、検索履歴など）
```

- ドメインルールが View・Serializer・Model.save()・Celery タスクに分散している（例: 作業項目の採番は `Issue.save()`、担当者がプロジェクトメンバーかの検証は `IssueCreateSerializer.validate()`、ゲストの閲覧制限は View 内）。
- URL は 3 系統に分離: 内部 API `/api/`（セッション）、公開 API `/api/v1/`（API Key）、公開ボード `/api/public/`。同じ機能が `app/views` と `api/views` に二重実装されている。

### 共通基底クラス（`app/views/base.py`、確認済み）

- `BaseViewSet` / `BaseAPIView`: タイムゾーン適用（ユーザーの `user_timezone` を `timezone.activate`）、Read replica 制御、`BasePaginator` を mixin。
- 例外を共通ハンドリング: `IntegrityError`→400 `{"error":"The payload is not valid"}`、`ValidationError`→400、`ObjectDoesNotExist`→404、`KeyError`→400、その他→500 `{"error":"Something went wrong please try again later"}`。
- `?fields=` と `?expand=` で応答フィールドの選択・展開に対応。

### 認証（概要）

- 内部 API: Django セッション Cookie（DB 保存セッション）。REST API では CSRF チェックを無効化（`BaseSessionAuthentication.enforce_csrf` が何もしない）。
- 公開 API: `X-Api-Key` ヘッダー。
- 詳細は [authentication-authorization.md](authentication-authorization.md)。

### データアクセス

- Django ORM。全 Entity が `BaseModel`（UUID PK、`created_at/updated_at`、`created_by/updated_by`、`deleted_at` による論理削除）を継承。
- `created_by/updated_by` は `crum.get_current_user()`（スレッドローカルの現在ユーザー）で `save()` 時に自動設定。
- 論理削除時は Celery タスク `soft_delete_related_objects` で関連オブジェクトも非同期に論理削除。
- Read replica: `ENABLE_READ_REPLICA=1` でルーター + Middleware が有効。
- 詳細は [database.md](database.md)。

### Cache

- `django_redis` を `CACHES.default` に設定（`REDIS_URL`）。
- `utils/cache.py` に API 応答キャッシュ用デコレータあり（使用範囲の網羅は未確認）。

## Frontend architecture（apps/web、確認済み）

```text
React Router（app/routes/core.ts、SSR なし）
  → Page（app/(all)/[workspaceSlug]/...page.tsx）
    → Component（components/...）
      → Hook（hooks/store/use-*.ts）
        → MobX Store（store/*.store.ts, RootStore）
          → Service（services/*.service.ts, APIService を継承）
            → axios（withCredentials: true、401 で `/?next_path=` にリダイレクト）
              → Backend /api/...
```

- データ取得は MobX Store のアクション + SWR（キャッシュキーで再取得制御）。
- 詳細は [frontend.md](frontend.md)。

## アプリケーション間通信

| 経路 | 方式 | 認証 |
|---|---|---|
| web/admin/space → api | HTTPS REST（JSON）、認証系のみフォーム POST + リダイレクト | セッション Cookie |
| web → live | WebSocket（Hocuspocus/Yjs） | トークン（ユーザー ID と Cookie を JSON で渡す）、または要求ヘッダーの Cookie |
| live → api | HTTP | ブラウザから受け取った Cookie を転送 |
| api → worker | RabbitMQ（Celery） | 内部 |
| api → S3 | boto3 | アクセスキー |
| ブラウザ → S3 | presigned POST / presigned GET | 署名 URL |

根拠: `apps/live/src/lib/auth.ts`、`apps/live/src/extensions/database.ts`、`apps/api/plane/settings/storage.py`。

## 非同期処理 / Background Job / Message Queue

- Celery（ブローカー RabbitMQ、`CELERY_BROKER_URL`）、Beat は `django_celery_beat.schedulers.DatabaseScheduler`。
- 書き込み系 API のほぼ全てが `issue_activity.delay(...)`、`model_activity.delay(...)`（Webhook）を発行。
- 詳細は [background-processing.md](background-processing.md)。

## Storage

- `STORAGES.default = plane.settings.storage.S3Storage`。MinIO（セルフホスト既定）または S3。
- アップロードは「API が FileAsset 行を作成し presigned POST を返す → ブラウザが直接 S3 に PUT → API に完了 PATCH（`is_uploaded=True`）」の 3 段階。
- ダウンロードは presigned GET（既定 3600 秒、`SIGNED_URL_EXPIRATION`）。

## Notification

- アプリ内通知（`Notification` テーブル）、メール通知（`EmailNotificationLog` を 5 分毎にまとめ送信）、Webhook（HMAC-SHA256 署名）。

## Configuration

- 環境変数（`settings/common.py`）+ DB 保存のインスタンス設定（`license` アプリ、Secret は暗号化保存フラグ `is_encrypted`）。
- 管理画面（admin）から SMTP / OAuth / AI 等を設定する構造。

## Deployment / Docker 構造

- 1 つの API イメージを `api` / `worker` / `beat-worker` / `migrator` の 4 コンテナでコマンドを変えて起動。
- 詳細は [infrastructure.md](infrastructure.md)。

## 処理の流れ例: 作業項目の作成（確認済み）

```text
[web] 作成モーダル (components/issues/issue-modal/base.tsx)
  → store createIssue (store/issue/project/issue.store.ts)
  → IssueService.createIssue → POST /api/workspaces/{slug}/projects/{project_id}/issues/
[api] IssueViewSet.create (app/views/issue/base.py:405)
  → @allow_permission([ADMIN, MEMBER])               … プロジェクトロール確認
  → IssueCreateSerializer.validate                    … 日付順序、HTML検証、担当者/ラベル/状態/親の所属検証
  → serializer.save → Issue.save()                    … 既定状態の補完、PostgreSQL advisory lock で sequence_id 採番、sort_order 計算、IssueSequence 作成
  → IssueAssignee / IssueLabel bulk_create（担当者未指定ならプロジェクト既定担当者）
  → issue_activity.delay("issue.activity.created")    … 履歴作成 → notifications.delay
  → model_activity.delay(...)                         … Webhook
  → issue_description_version_task.delay(...)         … 説明文バージョン
  → 201 + 一覧用の軽量 JSON
```

## 不明点

- `utils/cache.py` のキャッシュがどの API に適用されているか未網羅（Q-ARC-01）。
- `channels`（Django Channels）は依存にあるが、リアルタイム配信に使っているかは未確認（Q-ARC-02）。

## TaskManager への影響

- Plane はドメインルールが View / Serializer / Model / Task に分散しており、テストや再利用がしにくい。TaskManager は CLAUDE.md §16 の 4 層（Api / Application / Domain / Infrastructure）にルールを集約する方が保守しやすい。
- 書き込み API が非同期タスクに強く依存（履歴・通知・Webhook）。TaskManager で同期処理にするか、キュー（または .NET の BackgroundService）を使うかは Phase 2 で決定。
- `crum` によるスレッドローカルの監査列自動設定は、.NET では EF Core の `SaveChanges` インターセプタ + 現在ユーザーサービスで置き換えられる（設計候補）。

## Phase 2 で決定する事項

- 同期 / 非同期処理の境界（履歴・通知・メール・Webhook）
- Redis・メッセージキューを導入するか
- ファイル保存先（ローカルボリューム / S3 互換）
- 公開 API（API Key）と内部 API を分けるか
- リアルタイム更新（WebSocket / SignalR）の要否
