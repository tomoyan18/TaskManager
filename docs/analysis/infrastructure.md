# Infrastructure 解析（Plane `preview`）

## 目的

Plane の Docker / Compose / 環境変数 / ネットワーク / ボリューム / 起動順序 / プロキシ / ビルド / デプロイを把握し、TaskManager の Docker 設計に **使える知識** と **使うべきでない Plane 固有構成** を分離する。

## 解析範囲と根拠

- `docker-compose.yml`、`docker-compose-test.yml`（`docker-compose-local.yml` は存在確認のみ）
- `apps/api/Dockerfile.api`、`apps/api/bin/docker-entrypoint-*.sh`
- `apps/proxy/Caddyfile.ce`
- `.env.example`（キー名のみ、値は転記しない）
- `deployments/`（ディレクトリ構成のみ）
- `.github/workflows/`（ファイル名と API 系の内容）

## サービス構成（`docker-compose.yml`、確認済み）

| サービス | イメージ / ビルド | コマンド | 依存 | ポート公開 | ボリューム |
|---|---|---|---|---|---|
| web | `apps/web/Dockerfile.web` | 既定 | api | なし | — |
| admin | `apps/admin/Dockerfile.admin` | 既定 | api, web | なし | — |
| space | `apps/space/Dockerfile.space` | 既定 | api, web | なし | — |
| api | `apps/api/Dockerfile.api` | `docker-entrypoint-api.sh` | plane-db, plane-redis | なし | — |
| worker | 同上 | `docker-entrypoint-worker.sh` | api, db, redis | なし | — |
| beat-worker | 同上 | `docker-entrypoint-beat.sh` | api, db, redis | なし | — |
| migrator | 同上 | `docker-entrypoint-migrator.sh`（`restart: no`） | db, redis | なし | — |
| live | `apps/live/Dockerfile.live` | 既定 | なし | なし | — |
| plane-db | `postgres:15.7-alpine`（`max_connections=1000`） | — | — | なし | `pgdata` |
| plane-redis | `valkey/valkey:7.2.11-alpine` | — | — | なし | `redisdata` |
| plane-mq | `rabbitmq:3.13.6-management-alpine` | — | — | なし | `rabbitmq_data` |
| plane-minio | MinIO | `server /export` | — | なし | `uploads` |
| proxy | `apps/proxy/Dockerfile.ce`（Caddy） | — | web, api, space, admin | `${LISTEN_HTTP_PORT}:80`, `${LISTEN_HTTPS_PORT}:443` | — |

- ネットワーク: 明示定義なし（Compose の既定ネットワーク）。外部公開は proxy のみ。
- **Health check: 本番 Compose には定義なし**。`depends_on` は起動順のみで、準備完了待ちはエントリポイントの `wait_for_db` / `wait_for_migrations` 管理コマンドで実現。
- テスト用 Compose には全依存サービスに healthcheck あり（`pg_isready`、`valkey-cli ping`、`rabbitmq-diagnostics ping`、`mc ready`）、データは tmpfs。

## 起動シーケンス（確認済み）

```text
plane-db / redis / mq / minio 起動
migrator: wait_for_db → migrate → 終了
api:      wait_for_db → wait_for_migrations → register_instance（マシン署名生成）
          → configure_instance → create_bucket → clear_cache → collectstatic
          → gunicorn -k uvicorn.workers.UvicornWorker（ワーカー数 GUNICORN_WORKERS、max-requests 1200 で再起動）
worker:   wait_for_db → wait_for_migrations → celery worker
beat:     wait_for_db → wait_for_migrations → celery beat
proxy:    Caddy（パスベースで各サービスへ）
```

- `register_instance` はホスト名・MAC・CPU・メモリ・ディスク情報のハッシュをインスタンス署名として登録（テレメトリ / ライセンス用、Plane 固有）。

## Dockerfile（API、確認済み）

- ベース `python:3.12.12-alpine`、ビルド依存を一時インストールして `pip install` 後に削除（マルチステージではなく単一ステージ内で削除）。
- `apk upgrade --available` をセキュリティパッチ目的で毎ビルド実行（`APK_SECURITY_PATCH` 引数でキャッシュ無効化）。
- `chmod -R 777 /code`、`USER` 指定なし（**root で実行**）。
- `EXPOSE 8000`。

## Proxy（Caddy、確認済み）

- パスベースのルーティング（`/api/*`, `/auth/*`, `/static/*` → api、`/god-mode/*` → admin、`/spaces/*` → space、`/live/*` → live、`/{bucket}/*` → MinIO、その他 → web）。
- `request_body max_size = FILE_SIZE_LIMIT`（既定 5MB）。
- 自動 TLS（ACME）、`trusted_proxies` 既定 `0.0.0.0/0`。
- 同一オリジン配下に API と SPA を置くことで、Cookie / CORS の問題を回避している。

## 環境変数（キー名のみ）

- DB: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `PGDATA`, `DATABASE_URL`
- Redis: `REDIS_HOST`, `REDIS_PORT`, `REDIS_URL`
- MQ: `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD`, `RABBITMQ_VHOST`, `AMQP_URL`
- Storage: `AWS_*`, `USE_MINIO`, `MINIO_ENDPOINT_SSL`, `FILE_SIZE_LIMIT`, `SIGNED_URL_EXPIRATION`
- Proxy: `LISTEN_HTTP_PORT`, `LISTEN_HTTPS_PORT`, `SITE_ADDRESS`, `CERT_*`, `TRUSTED_PROXIES`
- App: `SECRET_KEY`, `DEBUG`, `ALLOWED_HOSTS`, `CORS_ALLOWED_ORIGINS`, `WEB_URL`, `*_BASE_URL`, `*_BASE_PATH`, `SESSION_COOKIE_AGE`, `HARD_DELETE_AFTER_DAYS`, `*_RETENTION_DAYS`, `API_KEY_RATE_LIMIT`, `ENABLE_DRF_SPECTACULAR`, `GUNICORN_WORKERS`
- Secret はコンテナへ `env_file`（`./apps/api/.env`、ルート `.env`）で渡す。

## デプロイ

- `deployments/{aio,cli,kubernetes,swarm}/community`: オールインワン、CLI インストーラ、Kubernetes、Docker Swarm 向け（内容は未精読）。
- CI: ブランチビルド（`build-branch.yml`）、機能デプロイ、CodeQL、マイグレーションチェック等。

## TaskManager の Docker 設計に利用できる知識

- 1 つの Backend イメージを用途（API / マイグレーション）でコマンドを変えて使う方式。
- マイグレーション専用の一回実行コンテナ（`restart: no`）と、API 側の「マイグレーション完了待ち」。
- 依存サービスの healthcheck（テスト用 Compose の書き方）と tmpfs によるテスト DB の高速化。
- リバースプロキシで同一オリジン化し、Cookie / CORS を単純化する構成。
- アップロードサイズ制限をプロキシとアプリの両方で設定する二重防御。
- `.env.example` で必要な変数を列挙し、実際の値は Git に入れない運用（CLAUDE.md §17 と一致）。

## 利用すべきでない Plane 固有の構成

- 6 アプリ + RabbitMQ + Redis + MinIO の大規模構成（TaskManager の想定規模に対して過剰になりうる）
- インスタンス登録・マシン署名・テレメトリ
- root 実行・`chmod 777`（セキュリティ上不適切）
- healthcheck なしの本番 Compose
- `TRUSTED_PROXIES=0.0.0.0/0` の既定値
- 管理画面（god-mode）・公開ボード・共同編集サーバー用のコンテナ

## 不明点

- `docker-compose-local.yml` と各 `Dockerfile.dev` の詳細（Q-INF-01）
- CI の詳細（Web のテスト実行有無、デプロイ手順）（Q-INF-02）

## Phase 2 / Phase 3 で決定する事項

- コンテナ構成（api / web / mysql / （任意）proxy / （任意）mailpit / （任意）minio）
- マイグレーション適用方式（起動時自動 / 専用コンテナ / 手動）
- healthcheck と起動順序
- 非 root 実行、マルチステージビルド
- ポート公開とリバースプロキシの要否（Vite 開発サーバーのプロキシで代替するか）
- `.env.example` の項目
