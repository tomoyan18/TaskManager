# ローカル開発の手順

macOS + VS Code を前提とする（Linux でも同じ手順で動く。Windows 固有の手順は使わない）。

## 1. 初回準備

```bash
# 必要なもの: .NET SDK 10.0.400 以上、Node.js 24、Docker Desktop（Compose v2）、Git
dotnet --version && node --version && npm --version && docker --version && docker compose version

cd /Volumes/OutSSD/CSharp/TaskManager
cp .env.example .env && chmod 600 .env     # 値の生成は configuration.md §3
dotnet tool restore                        # dotnet-ef 10.0.12
dotnet restore src/TaskManager.sln
(cd frontend/task-manager-web && npm ci)
```

VS Code の推奨拡張: C# Dev Kit、Docker、ESLint の代わりに oxlint 用拡張（任意）。

## 2. 毎日の開発（推奨構成）

DB だけ Docker、API と Frontend はローカルで実行する（ホットリロードが速い）。

```bash
# 1) DB を起動してマイグレーションを適用（127.0.0.1:3307 に公開）
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db migrate

# 2) API（http://localhost:5080、Swagger UI: http://localhost:5080/swagger）
set -a; . ./.env; set +a
export Jwt__SigningKey="$TM_JWT_SIGNING_KEY"
export ConnectionStrings__TaskManager="Server=127.0.0.1;Port=${TM_DB_PORT:-3307};Database=${TM_DB_NAME};User ID=${TM_DB_APP_USER};Password=${TM_DB_APP_PASSWORD};SslMode=Required"
dotnet watch --project src/TaskManager.Api --launch-profile http

# 3) Frontend（http://localhost:5173。/api /openapi /swagger /health は API にプロキシ）
cd frontend/task-manager-web && npm run dev
```

## 3. よく使うコマンド

| 目的 | コマンド |
|---|---|
| Backend ビルド | `dotnet build src/TaskManager.sln` |
| Backend テスト | `TM_TEST_MYSQL_CONNECTION='Server=localhost;Port=3306;User ID=...;Password=...;SslMode=Required' dotnet test --solution src/TaskManager.sln`（未設定なら Testcontainers。docs/testing/test-design.md §8） |
| OpenAPI の再生成 | `dotnet build src/TaskManager.Api` → `artifacts/openapi/openapi.json` が更新される |
| OpenAPI スナップショットの更新 | 差分を確認して `cp artifacts/openapi/openapi.json docs/api/openapi.v1.json`（api-design.md を先に更新すること） |
| Frontend の API 型の再生成 | `cd frontend/task-manager-web && npm run generate:api` |
| Frontend の型チェック / lint / テスト / ビルド | `npm run typecheck` / `npm run lint` / `npm run test` / `npm run build` |
| E2E テスト | `cd frontend/task-manager-e2e && TM_E2E_ADMIN_EMAIL=... TM_E2E_ADMIN_PASSWORD=... npx playwright test`（空の DB + 起動中の Web・API が前提） |
| マイグレーション追加（Phase 4 以降） | `dotnet ef migrations add <名前> --project src/TaskManager.Infrastructure --startup-project src/TaskManager.Infrastructure` |
| ローカル DB へ適用 | `docker compose -f docker-compose.yml -f docker-compose.dev.yml run --rm migrate` |
| MySQL に接続（開発用） | `docker compose exec db mysql -u"$TM_DB_APP_USER" -p taskmanager_db`（パスワードは対話入力） |
| 全体を Docker で起動 | `docker compose up -d --build` → http://localhost:8080 |

## 4. 動作確認の URL

| URL | 期待 |
|---|---|
| http://localhost:5080/swagger | Swagger UI（Development のみ） |
| http://localhost:5080/openapi/v1.json | OpenAPI 文書 |
| http://localhost:5080/api/v1/system/info | `{"name":"TaskManager","version":"..."}` |
| http://localhost:5080/health/live | `Healthy` |
| http://localhost:5080/health/ready | `Healthy`（DB に接続できる場合）、DB がなければ 503 |
| http://localhost:5173/ | 環境確認画面（「API に接続できました」） |

## 5. OpenAPI 契約の流れ

```text
docs/design/api-design.md を更新
  → Controller / DTO を実装
  → dotnet build（artifacts/openapi/openapi.json を生成）
  → 差分を確認して docs/api/openapi.v1.json を更新（コミット）
  → npm run generate:api（frontend/task-manager-web/src/api/schema.d.ts）
  → npm run typecheck（Frontend の呼び出しが契約と一致するか）
```
スナップショット比較の自動テストは Phase 5 で追加する（testing-strategy.md §6）。

## 6. 注意

- `.env` はコミットしない。値を画面共有・チャットに貼らない。
- 外付け SSD（`/Volumes/OutSSD`）で作業しているため、SSD を取り外す前に `docker compose down` で停止する（ボリュームは Docker Desktop の VM 内にあり、SSD 上ではない）。
- macOS には `timeout` コマンドがない（issues.md I-09）。
