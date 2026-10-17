# 設定（Configuration）

## 1. 方針

- Secret（パスワード、JWT 署名鍵）は **環境変数だけ** で渡す。`appsettings*.json`、ソースコード、Dockerfile、Compose ファイルに値を書かない。
- `.env`（Git 管理外）に値を置き、Compose が各コンテナの環境変数に展開する。
- ローカルで `dotnet run` する場合も環境変数（またはユーザーシークレット）で渡す。
- Plane の設定値・Secret は一切使わない。

## 2. 環境変数（`.env`）

| 変数 | 必須 | 既定 | 用途 | 使用先 |
|---|---|---|---|---|
| `TM_WEB_PORT` | | 8080 | 公開ポート | web |
| `MYSQL_ROOT_PASSWORD` | ○ | — | MySQL root（初期化のみ） | db |
| `TM_DB_NAME` | | `taskmanager_db` | DB 名 | db, migrate, api |
| `TM_DB_APP_USER` | | `taskmanager_app` | アプリ用 DB ユーザー（DML のみ） | db(init), api |
| `TM_DB_APP_PASSWORD` | ○ | — | 同パスワード | db(init), api |
| `TM_DB_MIGRATOR_USER` | | `taskmanager_migrator` | マイグレーション用 DB ユーザー | db(init), migrate |
| `TM_DB_MIGRATOR_PASSWORD` | ○ | — | 同パスワード | db(init), migrate |
| `TM_ASPNETCORE_ENVIRONMENT` | | `Production` | ASP.NET Core の環境名 | api |
| `TM_JWT_SIGNING_KEY` | ○ | — | JWT 署名鍵（Base64、32 バイト以上） | api |
| `TM_JWT_ISSUER` | | `taskmanager` | JWT の発行者 | api |
| `TM_JWT_AUDIENCE` | | `taskmanager-web` | JWT の対象 | api |
| `TM_ENABLE_SWAGGER` | | `false` | Swagger UI と `/openapi/v1.json` の公開 | api |
| `TM_DB_PORT` | | 3307 | 開発用: DB を公開する localhost のポート | db（dev） |
| `TM_API_PORT` | | 5080 | 開発用: API を公開する localhost のポート | api（dev） |

初期システム管理者（`TM_BOOTSTRAP_ADMIN_*`）、`TM_COOKIE_SECURE` は、使用する機能を実装する Phase 4 で追加する。

## 3. Secret の生成

```bash
cp .env.example .env
chmod 600 .env
# 下の値を生成して .env の空欄に貼り付ける（パスワードは ; ' " を含まない hex を使う）
openssl rand -hex 24      # MYSQL_ROOT_PASSWORD / TM_DB_APP_PASSWORD / TM_DB_MIGRATOR_PASSWORD（それぞれ別の値）
openssl rand -base64 48   # TM_JWT_SIGNING_KEY
```

- パスワードは接続文字列（`Password=...;`）に埋め込まれるため、`;` を含めてはいけない。
- Phase 3 の検証では、上記と同じ方法で生成した値を `.env` に設定した（値はどこにも記録していない）。

## 4. .NET の設定キー

| 設定キー | 環境変数名 | 置き場所 | 内容 |
|---|---|---|---|
| `ConnectionStrings:TaskManager` | `ConnectionStrings__TaskManager` | 環境変数のみ | MySQL 接続文字列（`Server=...;Port=3306;Database=...;User ID=...;Password=...;SslMode=Required`） |
| `Jwt:SigningKey` | `Jwt__SigningKey` | 環境変数のみ | 起動時に Base64・32 バイト以上を検証し、満たさなければ起動失敗 |
| `Jwt:Issuer` / `Jwt:Audience` | `Jwt__Issuer` / `Jwt__Audience` | `appsettings.json` に既定値 | Secret ではない |
| `ForwardedHeaders:KnownNetworks` | `ForwardedHeaders__KnownNetworks__0` など | 任意 | X-Forwarded-For を信頼する接続元のネットワーク（CIDR の配列）。既定はプライベート IP（10/8、172.16/12、192.168/16、fc00::/7）と loopback（Phase 7） |
| `Swagger:Enabled` | `Swagger__Enabled` | `appsettings.json`（false）、`appsettings.Development.json`（true） | Swagger UI と OpenAPI 文書の公開 |
| `ASPNETCORE_ENVIRONMENT` | 同名 | 起動設定 / Compose | `Development` ではログを通常形式、それ以外は JSON 形式で出力 |

設定の読み込み順（後勝ち）: `appsettings.json` → `appsettings.{環境}.json` → ユーザーシークレット（Development のみ）→ 環境変数。

## 5. ローカル実行時の渡し方（例）

```bash
cd /Volumes/OutSSD/CSharp/TaskManager
set -a; . ./.env; set +a     # .env を読み込む（画面に表示しない）
export Jwt__SigningKey="$TM_JWT_SIGNING_KEY"
export ConnectionStrings__TaskManager="Server=127.0.0.1;Port=${TM_DB_PORT:-3307};Database=${TM_DB_NAME};User ID=${TM_DB_APP_USER};Password=${TM_DB_APP_PASSWORD};SslMode=Required"
dotnet run --project src/TaskManager.Api --launch-profile http
```

ユーザーシークレットを使う場合（Development のみ、リポジトリ外に保存される）:

```bash
dotnet user-secrets --project src/TaskManager.Api set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets --project src/TaskManager.Api set "ConnectionStrings:TaskManager" "Server=127.0.0.1;Port=3307;..."
```

## 6. Secret 保護の確認結果

| 確認 | 結果 |
|---|---|
| `git check-ignore -v .env .env.local` | 除外される（`.gitignore` の `.env` / `.env.*`） |
| `git check-ignore .env.example` | 除外されない（雛形はコミット対象） |
| `.env.example` の Secret 欄 | すべて空欄 |
| `docker compose --env-file .env.example config` | `MYSQL_ROOT_PASSWORD を .env に設定してください` で停止（未設定のまま起動しない） |
| `.dockerignore` | `.env` / `.env.*` をビルドコンテキストから除外 |
| ソースコード・設定ファイル | Secret の値なし。設計時の接続文字列（`DesignTimeAppDbContextFactory`）はパスワードを含まない |
