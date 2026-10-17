# アーキテクチャ設計

## 1. 全体構成

```text
ブラウザ（React SPA）
   │ HTTPS（本番は前段の TLS 終端。開発は HTTP）
   ▼
web コンテナ（nginx-unprivileged :8080）
   ├─ /            → SPA の静的ファイル（index.html へのフォールバック）
   ├─ /api/        → api:8080 へリバースプロキシ
   └─ /swagger/, /openapi/ → api:8080（TM_ENABLE_SWAGGER=true の場合のみ）
   ▼
api コンテナ（ASP.NET Core .NET 10 :8080、非 root）
   ├─ MySQL 8.4（db コンテナ、内部ネットワークのみ）
   └─ 添付ファイル保存領域（Docker ボリューム /var/lib/taskmanager/attachments）
migrate コンテナ（EF Core マイグレーションバンドルを 1 回実行して終了）
```

設計判断:

- **同一オリジン構成**（web が /api を中継）: CORS を不要にし、リフレッシュ Cookie を `SameSite=Strict` で運用できる。Phase 1 で見た「CORS 全許可 + 資格情報許可」の設定ミスの余地をなくす（analysis/authentication-authorization.md）。
- **コンテナは 4 つ**（web / api / db / migrate）。Plane の 13 コンテナ構成（キュー・Redis・オブジェクトストレージ・ワーカー）は採用しない。MVP の非同期処理は API プロセス内の `BackgroundService` で足りる（§5）。

## 2. Backend のプロジェクト構成と責務

```text
src/
├── TaskManager.sln
├── TaskManager.Domain/            … 業務ルールの中心。外部依存なし
├── TaskManager.Application/       … ユースケース、DTO、検証、認可判定、インターフェース
├── TaskManager.Infrastructure/    … EF Core(MySQL)、パスワードハッシュ、JWT、ファイル保存、時刻
└── TaskManager.Api/               … HTTP、認証設定、Controller、ミドルウェア、OpenAPI、起動
tests/
├── TaskManager.Domain.Tests/
├── TaskManager.Application.Tests/
├── TaskManager.Infrastructure.Tests/   … MySQL（Testcontainers）を使う統合テスト
└── TaskManager.Api.Tests/              … WebApplicationFactory + MySQL による API テスト・OpenAPI 契約テスト
```

| プロジェクト | 責務 | 置くもの | 置かないもの |
|---|---|---|---|
| Domain | 業務ルール・不変条件 | Entity、Value Object、列挙、ドメイン例外、ドメインサービス（採番規則、ステータス遷移時の完了日時など） | EF Core、HTTP、DTO |
| Application | ユースケースの手順と認可 | `{機能}Service`、Request/Response DTO、入力検証、`ProjectAccessGuard`（所属・ロール判定）、`IAppDbContext` 等のインターフェース、エラーコード定義 | MySQL 固有処理、HTTP の型 |
| Infrastructure | 外部技術の実装 | `AppDbContext`（EF Core / MySQL）、Entity 設定、マイグレーション、`PasswordHasherAdapter`、`JwtTokenIssuer`、`LocalAttachmentStorage`、監査列・`row_version` の自動設定（SaveChanges インターセプタ） | ユースケース |
| Api | HTTP の入口 | Controller、認証・認可ポリシー、例外 → Problem Details 変換、レート制限、セキュリティヘッダー、OpenAPI 生成、Swagger UI、ヘルスチェック、起動時シード、`HttpCurrentUser`、アクセストークン発行（`JwtAccessTokenIssuer`。Phase 4 で Infrastructure から移動: 検証と同じ設定を使うため） | 業務ルール |

### 依存方向（採用）

```text
Api ──────────────┐
 │                ▼
 │          Infrastructure
 │                │
 ▼                ▼
Application ◄─────┘
 │
 ▼
Domain
```

- Api → Application, Infrastructure（DI 登録のためだけに参照。Controller は Application のみを使う）
- Infrastructure → Application, Domain（Application のインターフェースを実装）
- Application → Domain
- Domain → なし
- テストで依存規則を検査する（Api.Tests に NetArchTest 相当の「Domain は他層を参照しない」等のテストを置く。ライブラリ採用は Phase 3 で確認、未採用ならリフレクションで自作）

### Repository を作らない理由

Application は `IAppDbContext`（`DbSet<T>` と `SaveChangesAsync` を公開するインターフェース）を直接使う。

- 理由: 汎用 Repository は EF Core の機能（`Include`、射影、`AsNoTracking`）を隠すだけで重複になる。CLAUDE.md「不要な Repository 等を追加しない」に従う。
- 代償: Application が `Microsoft.EntityFrameworkCore`（プロバイダなし）に依存する。MySQL 固有処理（採番の行ロック等）は Infrastructure の専用インターフェース（`ITaskNumberAllocator`）に閉じ込める。
- テスト: Application の単体テストは MySQL コンテナに対して行う統合寄りのテストと、Domain の純粋な単体テストに分ける（[testing-strategy.md](testing-strategy.md)）。EF Core InMemory は MySQL と挙動が違うため使わない。

## 3. リクエスト処理の流れ（例: タスク更新）

```text
PATCH /api/v1/projects/SALES/tasks/12  (Authorization: Bearer ...)
 Api:
  1. 認証ミドルウェア（JWT 検証）→ 失敗 401
  2. パスワード変更強制チェック（MustChangePasswordFilter）→ 403
  3. レート制限 → 429
  4. TasksController.UpdateTask(projectKey, taskNumber, UpdateTaskRequest)
     - [ApiController] のモデル検証（DataAnnotations）→ 400 Problem Details
 Application:
  5. TaskItemService.UpdateAsync(projectKey, taskNumber, request)
     a. ProjectAccessGuard.RequireAsync(projectKey, ProjectPermission.EditTasks)
        → プロジェクトなし / 非メンバー: 404、権限不足: 403、アーカイブ中: 409
     b. TaskItem を (project_id, task_number) で取得 → なし: 404
     c. 参照の検証（ステータス・担当者・タグ・マイルストーン・親が同一プロジェクトか）→ 400
     d. Domain の TaskItem メソッドで変更（不変条件の検証、変更点の列挙）
     e. 変更点ごとに TaskHistoryEntry、担当者変更なら Notice を追加
     f. SaveChangesAsync（row_version 不一致 → ConcurrencyConflictException）
 Infrastructure:
  6. SaveChanges インターセプタが updated_at_utc / updated_by_user_id / row_version を設定
 Api:
  7. 200 + TaskDetailResponse、例外は ProblemDetailsMapper で 4xx/5xx に変換
```

## 4. 横断的な仕組み

| 仕組み | 方式 |
|---|---|
| 現在の利用者 | `ICurrentUser`（Api が JWT のクレームから実装）。Application はこれだけを参照 |
| 時刻 | .NET 標準の `TimeProvider` を DI。テストで固定可能 |
| トランザクション | 1 ユースケース = 1 `SaveChangesAsync`。採番のみ明示トランザクション（[database-design.md](database-design.md) §5） |
| 例外 → HTTP | Application の例外（NotFound / Forbidden / Validation / Conflict / ConcurrencyConflict）を Api の例外ハンドラ（`IExceptionHandler`）で Problem Details に変換 |
| ログ | `Microsoft.Extensions.Logging` + JSON コンソール出力。相関 ID（`traceId`）を Problem Details とログに含める |
| 設定 | `appsettings.json` + 環境変数。Secret は環境変数のみ（[security-design.md](security-design.md)） |
| ヘルスチェック | `/health/live`（プロセス）、`/health/ready`（DB 接続）。OpenAPI には含めない |

## 5. バックグラウンド処理

API プロセス内の `BackgroundService` を 1 つ（`MaintenanceWorker`）置き、1 時間ごとに実行する。

| 処理 | 内容 |
|---|---|
| 期限切れリフレッシュセッション削除 | 失効・期限切れから 30 日経過したもの |
| 古いお知らせ削除 | 作成から 90 日経過 |
| 古い監査記録削除 | 1 年経過 |
| 孤立した添付ファイル削除 | DB に行がないファイル（タスク削除後の削除失敗分） |

複数インスタンスで動かす想定はない（NFR-04）。将来スケールアウトする場合は MySQL の `GET_LOCK` で排他する（OD-14）。
履歴とお知らせは業務データと **同じトランザクションで同期的に** 作成する（Plane の非同期作成で起きうる「更新は成功・履歴は欠落」を避けるため。analysis/background-processing.md）。

## 6. Frontend アーキテクチャ

```text
frontend/task-manager-web/   … Phase 3 で frontend/ 直下から変更（下記）
├── index.html
├── vite.config.ts            … 開発時 /api を API にプロキシ
├── openapi/                  … openapi-typescript で生成した型（コミットする）
└── src/
    ├── main.tsx              … ルート、QueryClient、Router
    ├── app/                  … ルーティング定義、レイアウト（AppShell）、エラー境界
    ├── api/                  … openapi-fetch クライアント、認証トークン管理、エラー変換
    ├── features/
    │   ├── auth/             … ログイン、パスワード変更
    │   ├── my-tasks/         … ホーム（自分のタスク）
    │   ├── projects/         … 一覧、作成、設定（基本・メンバー・ステータス・タグ）
    │   ├── tasks/            … 表、ボード、詳細、作成・編集、コメント、添付、履歴
    │   ├── milestones/
    │   ├── notices/
    │   ├── search/
    │   └── admin-users/
    ├── components/           … 汎用 UI 部品（Button、Dialog、Menu、Toast、DatePicker、Badge 等）
    ├── hooks/                … 汎用フック（useDebounce、useKeyboardShortcut 等）
    ├── i18n/ja.ts            … 画面文言
    ├── styles/               … デザイントークン（CSS 変数）、リセット
    └── utils/                … 日付表示、タスクキー解析など
```

**配置の変更（Phase 3）**: Phase 2 では `frontend/` 直下としていたが、Phase 3 の指示（`frontend/task-manager-web/`）に合わせて 1 階層下げた。`frontend/` 配下に将来 E2E テスト等の別パッケージを並べられる利点がある。OpenAPI の型生成元は `../../docs/api/openapi.v1.json`。

| 関心 | 方式 | 理由 |
|---|---|---|
| ルーティング | React Router（宣言的ルート、SPA） | 標準的。URL で画面状態（フィルタ・表示切替）を表現 |
| サーバー状態 | TanStack Query | 取得・キャッシュ・再取得・楽観的更新を 1 つの仕組みで扱う。Plane の MobX + SWR 二重構成を避ける |
| クライアント状態 | React の state / context のみ（認証状態、トースト） | グローバルストアを持たない |
| API クライアント | openapi-fetch + openapi-typescript の生成型 | OpenAPI と型を一致させ、存在しない API を呼ぶ事故（Plane で確認）をコンパイル時に防ぐ |
| フォーム | react-hook-form + zod | 入力検証を宣言的に記述。制約値は OpenAPI と同じ値を定数化 |
| UI 部品 | React Aria Components + CSS Modules | アクセシビリティ（キーボード・スクリーンリーダー）を部品側で担保 |
| Markdown 表示 | react-markdown（生 HTML 無効）+ 許可リスト | XSS を防ぐ（[security-design.md](security-design.md)） |
| 認証トークン | アクセストークンはメモリのみ。期限切れ時・401 時に `/auth/refresh` を 1 回試行し再送 | localStorage に置かず XSS での窃取を難しくする |

詳細は [ui-design.md](ui-design.md)、[ux-design.md](ux-design.md)。

### 6.1 Phase 9 で追加した Frontend の構成

| 置き場所 | 内容 |
|---|---|
| `features/tasks/TaskScheduleView.tsx`（+ `TaskSchedule.module.css`） | ガント（SCR-20）と表形式の代替 |
| `features/tasks/ProjectStatusChartView.tsx` | プロジェクトの円グラフ（SCR-21） |
| `features/my-tasks/MyStatusChartView.tsx` | 自分のタスクの円グラフ（SCR-04） |
| `components/StatusPieChart.tsx`（+ CSS Module） | 円グラフの共通部品 |
| `utils/schedule.ts` | 期間・目盛り・棒の位置・まとめ方の計算（純粋関数） |

Backend は既存の層構成のまま、Domain（`ScheduleWindow`・`ShareRounding`）→ Application（`TaskScheduleService`・`TaskStatusSummaryService`）→ Api（既存の `TasksController`・`MyTasksController` に操作を追加）。Infrastructure は索引の追加とマイグレーションのみ。

## 7. Docker Compose 構成

### 7.1 サービス

| サービス | イメージ / ビルド | 役割 | ポート | 依存 | ヘルスチェック | 再起動 |
|---|---|---|---|---|---|---|
| `db` | `mysql:8.4`（パッチ版は Phase 3 で固定） | DB | 公開なし（開発用 override で `127.0.0.1:3307:3306`） | — | `mysqladmin ping -h 127.0.0.1 --silent` 10 秒間隔 | unless-stopped |
| `migrate` | `docker/api/Dockerfile` の `migrate` ステージ | `efbundle` でマイグレーション適用後に終了 | なし | db: `service_healthy` | なし | `no` |
| `api` | `docker/api/Dockerfile` の `runtime` ステージ | API | 公開なし（開発用 override で `127.0.0.1:5080:8080`） | migrate: `service_completed_successfully` | `curl -fsS http://localhost:8080/health/ready` | unless-stopped |
| `web` | `docker/web/Dockerfile`（Node でビルド → nginx-unprivileged） | SPA 配信 + /api 中継 | `${TM_WEB_PORT:-8080}:8080` | api: `service_healthy` | `wget -qO- http://localhost:8080/healthz` | unless-stopped |

### 7.2 ネットワーク

| ネットワーク | 所属 | 目的 |
|---|---|---|
| `tm-internal`（`internal: true`） | db, migrate, api | DB を外部から到達不能にする |
| `tm-edge` | api, web | web から api への中継 |

### 7.3 ボリューム

| ボリューム | マウント先 | 内容 |
|---|---|---|
| `tm-mysql-data` | db: `/var/lib/mysql` | DB データ |
| `tm-attachments` | api: `/var/lib/taskmanager/attachments` | 添付ファイル |
| （バインド）`./docker/mysql/init` | db: `/docker-entrypoint-initdb.d`（読み取り専用） | 初回のみ: アプリ用 DB ユーザー・マイグレーション用 DB ユーザーの作成 |

### 7.4 起動順序

```text
db（healthy）→ migrate（正常終了）→ api（healthy）→ web
```

### 7.5 DB ユーザーの分離

| DB ユーザー | 権限 | 使用者 |
|---|---|---|
| `root` | 全権 | 初期化スクリプトのみ（アプリは使わない） |
| `TM_DB_MIGRATOR_USER` | `taskmanager_db` への DDL + DML | migrate |
| `TM_DB_APP_USER` | `taskmanager_db` への SELECT / INSERT / UPDATE / DELETE のみ | api |

### 7.6 環境変数（`.env.example` に記載。値は例のみで実 Secret は含めない）

| 変数 | 用途 | 使用サービス |
|---|---|---|
| `TM_WEB_PORT` | 公開ポート（既定 8080） | web |
| `MYSQL_ROOT_PASSWORD` | MySQL root | db |
| `TM_DB_NAME` | 既定 `taskmanager_db` | db, migrate, api |
| `TM_DB_APP_USER` / `TM_DB_APP_PASSWORD` | アプリ用 | db(init), api |
| `TM_DB_MIGRATOR_USER` / `TM_DB_MIGRATOR_PASSWORD` | マイグレーション用 | db(init), migrate |
| `TM_JWT_SIGNING_KEY` | JWT 署名鍵（32 バイト以上のランダム値、Base64） | api |
| `TM_JWT_ISSUER` / `TM_JWT_AUDIENCE` | JWT の発行者・対象 | api |
| `TM_BOOTSTRAP_ADMIN_EMAIL` / `TM_BOOTSTRAP_ADMIN_PASSWORD` | 初期システム管理者（SA が 0 人の時だけ使用） | api |
| `TM_ENABLE_SWAGGER` | Swagger UI と OpenAPI 文書の公開（本番既定 false） | api, web |
| `TM_COOKIE_SECURE` | リフレッシュ Cookie の Secure 属性（既定 true。HTTP の LAN 検証時のみ false） | api |
| `ASPNETCORE_ENVIRONMENT` | `Production` / `Development` | api |

### 7.7 Compose ファイル

- `docker-compose.yml`: 本番相当の全体構成
- `docker-compose.dev.yml`: 開発用の上書き（DB と API のポートを localhost に公開、Swagger 有効、`ASPNETCORE_ENVIRONMENT=Development`）
- 開発時の通常の流れ: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up db migrate` で DB を起動し、API は `dotnet run`、Frontend は `npm run dev`（Vite のプロキシで /api を転送）

## 8. 将来の拡張点（MVP では実装しない）

- メール送信: Application に `IMailSender` を追加し、Infrastructure で SMTP 実装（OD-08）
- リアルタイム更新: SignalR でお知らせ件数とタスク変更を配信（OD-10）
- ファイル保存先の S3 互換化: `IAttachmentStorage` の実装差し替え
