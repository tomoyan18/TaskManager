# Docker 構成

設計: `docs/design/architecture.md` §7。Linux コンテナのみを使う。

## 1. サービス

| サービス | イメージ / ビルド | 役割 | ポート（本番構成） | 依存 | ヘルスチェック |
|---|---|---|---|---|---|
| `db` | `docker/mysql/Dockerfile`（`mysql:8.4.11` を元に openssl を更新し、MySQL Shell・gosu を削除。mysql 利用者で実行。Phase 7） | DB（`utf8mb4` / `utf8mb4_0900_ai_ci`） | 公開なし | — | `mysqladmin ping -h 127.0.0.1 --silent`（10 秒間隔、開始猶予 30 秒） |
| `migrate` | `docker/api/Dockerfile` の `migrate` ステージ | EF Core マイグレーションバンドル（`efbundle`）を実行して終了 | なし | db: healthy | なし（`restart: "no"`） |
| `api` | `docker/api/Dockerfile` の `runtime` ステージ（`aspnet:10.0.12-noble`、非 root） | API | 公開なし | migrate: 正常終了 | `curl -fsS http://localhost:8080/health/ready`（DB 接続を含む） |
| `web` | `docker/web/Dockerfile`（`node:24-alpine3.24` でビルド → `nginx-unprivileged:1.30.5-alpine`） | SPA 配信 + `/api` `/openapi` `/swagger` を api に中継 | `${TM_WEB_PORT:-8080}:8080` | api: healthy | `wget -qO- http://127.0.0.1:8080/healthz` |

起動順序: `db（healthy）→ migrate（正常終了）→ api（healthy）→ web`

## 2. ネットワーク・ボリューム

| 名前 | 種類 | 所属 / マウント | 目的 |
|---|---|---|---|
| `tm-internal` | ネットワーク（`internal: true`） | db, migrate, api | DB を外部から到達不能にする |
| `tm-edge` | ネットワーク | api, web | web → api の中継 |
| `tm-devhost` | ネットワーク（開発用のみ） | db | `internal: true` のネットワークだけではポートを公開できないため、開発時のみ追加 |
| `tm-mysql-data` | ボリューム | db: `/var/lib/mysql` | DB データ |
| `tm-attachments` | ボリューム | api: `/var/lib/taskmanager/attachments` | 添付ファイル（Phase 4 以降で使用） |
| `./docker/mysql/init` | バインド（読み取り専用） | db: `/docker-entrypoint-initdb.d` | 初回起動時に DB ユーザーを作成 |

## 3. セキュリティ上の設定

| 設定 | 内容 |
|---|---|
| 非 root | api: イメージ既定の `app` ユーザー（`$APP_UID`）、web: nginx-unprivileged、db: mysql（Phase 7） |
| ケーパビリティ | 全サービス `cap_drop: ALL`（Phase 7） |
| MySQL の root | `MYSQL_ROOT_HOST=localhost`（コンテナ内のソケットからのみ。Phase 7。既存のボリュームには反映されないため作り直すか `root@'%'` を削除する） |
| 読み取り専用ルート | api・web は `read_only: true` + `/tmp` を tmpfs（OD-29。api の起動確認は I-01 解消後） |
| 権限昇格の禁止 | 全サービス `no-new-privileges:true` |
| DB ユーザーの分離 | `01-create-users.sh` でアプリ用（SELECT/INSERT/UPDATE/DELETE のみ）とマイグレーション用（DB 単位の全権）を作成。root はアプリから使わない |
| Secret | すべて `.env` から環境変数で渡す。`${VAR:?}` で未設定時は起動前にエラー。イメージ・Dockerfile・Compose に値を書かない |
| ビルドコンテキスト | `.dockerignore` で `.env`、`.git`、`bin/obj`、`node_modules`、`docs`、`tests` を除外 |
| DB 接続 | `SslMode=Required`（内部ネットワークでも暗号化） |
| Swagger | `TM_ENABLE_SWAGGER`（既定 false）。web は常に中継するが、無効時は api が 404 を返す |

## 4. Dockerfile の構成

### docker/api/Dockerfile

| ステージ | ベース | 内容 |
|---|---|---|
| `build` | `sdk:10.0.400-noble` | csproj だけを先にコピーして `restore`（キャッシュ効率）→ `dotnet tool restore` → `publish`（ビルド時 OpenAPI 生成は無効化） |
| `migrations-build` | build | `dotnet ef migrations bundle`（Infrastructure をスタートアッププロジェクトとして使用） |
| `migrate` | `aspnet:10.0.12-noble` | `efbundle --connection "$MIGRATOR_CONNECTION"` |
| `runtime` | `aspnet:10.0.12-noble` | curl のみ追加、添付ディレクトリ作成、非 root で `dotnet TaskManager.Api.dll` |

### docker/web/Dockerfile

| ステージ | ベース | 内容 |
|---|---|---|
| `build` | `node:24-alpine3.24` | `npm ci` → `npm run build` |
| `runtime` | `nginx-unprivileged:1.30.5-alpine` | `docker/web/nginx.conf` と `dist/` を配置 |

nginx の設定: SPA のフォールバック（`try_files $uri /index.html`）、`/assets/` の長期キャッシュ、`index.html` は `no-cache`、`client_max_body_size 11m`、CSP 等のセキュリティヘッダー、`server_tokens off`。

## 5. コマンド

```bash
# 設定の検証（値を表示しない）
docker compose config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml config --quiet

# 全体を起動（本番相当）
docker compose up -d --build
docker compose ps

# 開発: DB だけ起動してマイグレーションを適用（API と Frontend はローカルで実行）
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db migrate

# ログ
docker compose logs -f api

# 停止（データは残る）
docker compose down
# データも削除（DB・添付が消える。注意）
docker compose down -v
```

## 6. 確認状況

- Compose 設定の検証: **成功**（本番構成・開発構成とも）
- イメージのビルドとコンテナ起動: **確認済み（Phase 6、2026-10-12）**。db healthy → migrate 正常終了 → api healthy → web healthy、非 root・読み取り専用ルート・公開ポート（web の 8080 のみ）も確認。途中で見つかった問題は [issues.md](issues.md) I-11・I-12、結果は [docs/testing/test-results.md](../testing/test-results.md) §5
- Swagger 無効時（本番構成）の `/swagger`・`/openapi` は 404 ではなく 401 が返る（公開はされない。test-results.md §8 O-02）
