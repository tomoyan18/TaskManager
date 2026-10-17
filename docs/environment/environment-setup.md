# 環境構築手順と確認結果（Phase 3）

- 実施日: 2026-10-08
- 対象: TaskManager（`/Volumes/OutSSD/CSharp/TaskManager`）
- 設計: `docs/design/architecture.md`、`docs/design/technology.md`（§9 に Phase 3 の確定事項）

## 1. 前提ツールの確認結果（実測）

| ツール | 確認コマンド | 結果 | 判定 |
|---|---|---|---|
| macOS | `sw_vers` | macOS 15.8.1（24H32）、Intel（x86_64） | OK |
| VS Code | （Claude Code の VS Code 拡張から作業） | 利用中。ただし `code` コマンドは PATH にない | OK（任意で「Shell Command: Install 'code' command in PATH」） |
| .NET SDK | `dotnet --version` | 10.0.401（8.0.417 も併存） | OK |
| Node.js | `node --version` | v24.10.0 | OK |
| npm | `npm --version` | 11.6.0 | OK |
| Docker | `docker --version` | Docker version 29.5.2, build 79eb04c（Docker Desktop） | OK（ただし後述の不具合あり） |
| Docker Compose | `docker compose version` | v5.1.4 | OK |
| Git | `git --version` | 2.31.1 | OK |

Windows 専用 API・Windows サービス・Windows 固有の構成は使っていない（Linux コンテナ、`mcr.microsoft.com/dotnet/*-noble` 等の Linux イメージのみ）。

## 2. 作成したもの

```text
TaskManager/
├── global.json                    … .NET SDK 10.0.400 以上（latestFeature）、テスト実行基盤 Microsoft.Testing.Platform
├── Directory.Build.props          … 全 .NET プロジェクト共通（net10.0、Nullable、警告をエラー扱い 等）
├── Directory.Packages.props       … NuGet の版の中央管理
├── dotnet-tools.json              … dotnet-ef 10.0.12（ローカルツール）
├── src/
│   ├── TaskManager.sln            … .sln 形式（.NET 10 既定の .slnx ではなく明示指定）
│   ├── TaskManager.Domain/        … 外部依存なし（中身は Phase 4）
│   ├── TaskManager.Application/   … IAppDbContext、AddApplication()
│   ├── TaskManager.Infrastructure/… AppDbContext（MySQL）、設計時ファクトリ、AddInfrastructure()
│   └── TaskManager.Api/           … Program.cs、JWT 認証基盤、OpenAPI、Swagger UI、ヘルスチェック、セキュリティヘッダー、環境確認用 API
├── tests/                         … 4 つのテストプロジェクト（xUnit v3。テストコードは Phase 5）
├── frontend/task-manager-web/     … React + TypeScript + Vite（環境確認画面のみ）
├── docker/
│   ├── api/Dockerfile             … build / migrations-build / migrate / runtime の多段ビルド
│   ├── web/Dockerfile, nginx.conf … SPA 配信 + /api 中継（非 root）
│   └── mysql/init/01-create-users.sh … アプリ用・マイグレーション用 DB ユーザー作成
├── docker-compose.yml             … db / migrate / api / web
├── docker-compose.dev.yml         … 開発用（DB・API のポートを localhost に公開、Swagger 有効）
├── docs/api/openapi.v1.json       … OpenAPI 契約のスナップショット（Frontend の型生成元）
├── .env.example                   … 環境変数の雛形（Secret は空欄）
├── .gitignore / .dockerignore
└── docs/environment/              … 本書ほか
```

業務機能（Entity、業務 API、業務画面）は実装していない。Plane のコード・DB・Secret は使っていない。

## 3. 実行したコマンド（主なもの）

### 3.1 Backend

```bash
cd /Volumes/OutSSD/CSharp/TaskManager
dotnet new globaljson --sdk-version 10.0.401 --roll-forward latestFeature   # 後で 10.0.400 に変更（issues.md I-03）
mkdir -p src && cd src
dotnet new sln -n TaskManager --format sln
dotnet new classlib -n TaskManager.Domain -f net10.0 -o TaskManager.Domain
dotnet new classlib -n TaskManager.Application -f net10.0 -o TaskManager.Application
dotnet new classlib -n TaskManager.Infrastructure -f net10.0 -o TaskManager.Infrastructure
dotnet new webapi -n TaskManager.Api -f net10.0 --use-controllers --no-https -o TaskManager.Api
# テンプレートのサンプル（WeatherForecast、Class1.cs、.http）は削除
dotnet sln TaskManager.sln add TaskManager.Domain TaskManager.Application TaskManager.Infrastructure TaskManager.Api
dotnet add TaskManager.Application reference TaskManager.Domain
dotnet add TaskManager.Infrastructure reference TaskManager.Application TaskManager.Domain
dotnet add TaskManager.Api reference TaskManager.Application TaskManager.Infrastructure
dotnet sln TaskManager.sln add --solution-folder tests ../tests/*/*.csproj
cd ..
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.12
```

### 3.2 Frontend

```bash
mkdir -p frontend && cd frontend
npm create vite@latest task-manager-web -- --template react-ts --no-interactive
cd task-manager-web
npm install
npm install react-router @tanstack/react-query openapi-fetch react-hook-form zod @hookform/resolvers react-aria-components
npm install -D typescript@~5.9.3                  # issues.md I-05
npm install -D openapi-typescript vitest jsdom @testing-library/react @testing-library/user-event msw@^2.15.0   # I-06
npm run generate:api                               # docs/api/openapi.v1.json → src/api/schema.d.ts
```

### 3.3 Docker

```bash
cp .env.example .env    # 値は openssl で生成（configuration.md §3）。.env は Git 管理外
docker compose config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db migrate   # → issues.md I-01 で失敗
```

## 4. 確認結果（Verification）

| # | 確認項目 | 方法 | 結果 |
|---|---|---|---|
| V-01 | Solution が存在する | `dotnet sln src/TaskManager.sln list` | **OK**（src 4 + tests 4 プロジェクト） |
| V-02 | Backend のプロジェクトが存在する | 同上 | **OK** |
| V-03 | .NET 10 のビルド成功 | `dotnet build src/TaskManager.sln` | **OK**（警告 0・エラー 0。警告はエラー扱い設定） |
| V-04 | テスト実行基盤 | `dotnet test --solution src/TaskManager.sln` | **OK**（4 プロジェクトが起動し、テスト 0 件で終了コード 8＝「テストなし」。テストは Phase 5） |
| V-05 | Frontend が存在する | `frontend/task-manager-web` | **OK** |
| V-06 | Frontend の型チェック・lint・テスト・ビルド | `npm run typecheck` / `npm run lint` / `npm run test` / `npm run build` | **OK**（ビルド成果物 JS 353KB / gzip 111KB。テストは 0 件） |
| V-07 | 依存関係の脆弱性 | `npm audit` | **OK**（0 件） |
| V-08 | OpenAPI 文書の生成（ビルド時） | ビルドで `artifacts/openapi/openapi.json` を生成 | **OK**（OpenAPI 3.1.1、`bearerAuth` スキーム、`GET /api/v1/system/info`） |
| V-09 | Backend の起動 | `dotnet run --project src/TaskManager.Api --launch-profile http`（環境変数で JWT 鍵と接続文字列を指定） | **OK**（`http://localhost:5080` で待受） |
| V-10 | Swagger UI の表示 | `curl /swagger/index.html` | **OK**（200、タイトル「TaskManager API」。初回は 401 だったため修正: issues.md I-07） |
| V-11 | OpenAPI 文書の取得 | `curl /openapi/v1.json` | **OK**（200） |
| V-12 | エンドポイントが確認できる | `curl /api/v1/system/info` | **OK**（`{"name":"TaskManager","version":"1.0.0"}`） |
| V-13 | 認証スキームの設定が反映できる | OpenAPI の `components.securitySchemes.bearerAuth`、既定ポリシーで未ログインのリクエストが 401（Problem Details） | **OK** |
| V-14 | セキュリティヘッダー | `curl -I /api/v1/system/info` | **OK**（`nosniff`、`DENY`、CSP、`no-store`、`Server` ヘッダーなし） |
| V-15 | Frontend → API の疎通（開発時） | Vite 開発サーバー（:5173）経由で `/api/v1/system/info` と `/swagger/index.html` | **OK**（200） |
| V-16 | Docker Compose 設定の妥当性 | `docker compose config --quiet`（本番構成・開発構成） | **OK**。`.env.example` のまま（Secret 空欄）だと分かりやすいエラーで停止することも確認 |
| V-17 | MySQL の起動 | `docker compose ... up -d db` | **未完了**（Docker Desktop がイメージを取得できない: issues.md I-01） |
| V-18 | Backend から MySQL への接続 | `/health/ready`（`Database.CanConnectAsync`） | **未完了**（V-17 に依存。DB なしでは 503 を返すことは確認済み） |
| V-19 | マイグレーション実行（migrate コンテナ） | `docker compose ... up migrate` | **未完了**（V-17 に依存） |
| V-20 | コンテナでの API / Web 起動 | `docker compose up -d --build` | **未完了**（V-17 に依存） |
| V-21 | `.env.example` が存在する | ファイル確認 | **OK** |
| V-22 | `.gitignore` が Secret を保護する | `git check-ignore -v .env .env.local` | **OK**（`.env`、`.env.*` は除外、`.env.example` は管理対象） |
| V-23 | 環境ドキュメント | `docs/environment/` | **OK**（本書を含む 5 ファイル） |

## 5. 未完了事項と再開手順

Docker Desktop の不具合（issues.md I-01）が解消した後、次の順で確認する。

```bash
cd /Volumes/OutSSD/CSharp/TaskManager
docker pull hello-world                                   # Docker がイメージを取得できるか
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db migrate
docker compose ps                                         # db: healthy、migrate: exited (0)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build api web
curl -s http://localhost:5080/health/ready                # Healthy（Backend → MySQL 接続）
curl -s http://localhost:8080/api/v1/system/info          # web → api 中継
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8080/swagger/index.html   # 開発構成なら 200
```
