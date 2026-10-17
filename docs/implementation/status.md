# 実装状況（Phase 4）

- 更新日: 2026-10-08
- 設計の正: `docs/design/`（Phase 2）。設計から変えた点は §5 と `docs/design/open-decisions.md` に記録した。
- 規模: Backend 104 ファイル・約 8,400 行（マイグレーションを除く）、Frontend 53 ファイル・約 4,600 行（生成された `schema.d.ts` を除く）

## 1. 完了条件ごとの状況

| 完了条件 | 状況 | 根拠 |
|---|---|---|
| Domain implemented | **済** | `src/TaskManager.Domain`（Entity 14、値オブジェクト、列挙、業務ルール TK-01〜08 ほか） |
| Application implemented | **済** | `src/TaskManager.Application`（Service 19、DTO、検証、`ProjectAccessGuard`） |
| Infrastructure implemented | **済** | `src/TaskManager.Infrastructure`（EF Core / MySQL、採番、パスワードハッシュ、添付保存、種類判定） |
| EF Core Migration created | **済（ローカル MySQL 8.0.46 への適用を Phase 5 開始時に確認。§8）** | `Persistence/Migrations/*_InitialSchema.cs`、SQL は `artifacts/migrations/InitialSchema.sql`（Git 管理外。再生成手順は §4） |
| API implemented | **済** | Controller 16、エンドポイント 61（設計 60 + 環境確認用 1） |
| Swagger synchronized | **済** | ビルド時生成の OpenAPI と設計書の一覧を機械照合し、61/61 件でメソッド・ルート・operationId が一致。スナップショット `docs/api/openapi.v1.json` を更新 |
| Authentication implemented | **済（DB 込みの動作を Phase 5 開始時に確認。§8）** | JWT（15 分）+ ローテーションするリフレッシュ Cookie、ロック、一時パスワード、即時失効 |
| Authorization implemented | **済（DB 込みの動作を Phase 5 開始時に確認。§8）** | 既定でログイン必須、SA ポリシー、`ProjectAccessGuard`、子リソースは親 ID を条件に取得 |
| Frontend implemented | **済** | 画面 SCR-01〜19（SCR-03 は設計上の欠番） |
| API client implemented | **済** | openapi-fetch + 生成型、401 時の再取得と再送、Problem Details の変換 |
| UI/UX implemented | **済（実ブラウザ（Chromium）での確認を Phase 5 開始時に実施。§8）** | 一覧のその場編集、ボード、URL での状態保持、確認ダイアログ、空状態・読み込み・エラー表示、ショートカット、レスポンシブ |
| Japanese UI implemented | **済** | 画面文言・検証メッセージ・エラー・お知らせ・履歴を日本語で実装。英語のフレームワーク既定メッセージも日本語に置き換え |
| Docker works | **済（Phase 6 で確認）** | Docker Desktop の復旧後、`docker compose up -d --build` で全サービスの起動とヘルスチェック、E2E 8 シナリオの成功を確認（docs/testing/test-results.md §5）。ビルドと DB 初期化の不具合 2 件を修正（issues.md I-11・I-12） |
| Backend builds | **済** | `dotnet build src/TaskManager.sln`: 警告 0・エラー 0（警告をエラー扱い） |
| Frontend builds | **済** | `npm run typecheck` / `npm run lint`（警告 0）/ `npm run build` 成功 |
| implementation documentation updated | **済** | 本書 |

## 2. 実行した確認

| 確認 | コマンド・方法 | 結果 |
|---|---|---|
| Backend ビルド | `dotnet build src/TaskManager.sln` | 成功（警告 0） |
| Frontend 型チェック | `npm run typecheck` | 成功 |
| Frontend lint | `npm run lint`（`--deny-warnings`） | 成功（警告 0） |
| Frontend ビルド | `npm run build` | 成功。初回読み込みの JS 307KB（gzip 95KB）、画面ごとに分割 |
| npm 依存の脆弱性 | `npm audit` | 0 件 |
| マイグレーション生成 | `dotnet ef migrations add InitialSchema` / `migrations script --idempotent` | 成功。表 14・CHECK 制約・外部キー（削除時の挙動）・インデックス名が database-design.md と一致することを SQL で確認 |
| OpenAPI と設計の一致 | ビルド時生成の `openapi.json` を api-design.md §3 の表と機械照合 | 61/61 一致。匿名で呼べるのは Login と GetSystemInfo のみ |
| OpenAPI の Secret 非掲載 | 文字列検査（plane / secret / signingkey / connectionstring / パス） | 該当なし（password を含むのはプロパティ名とエラーコードのみ） |
| API の動作（DB なし） | `dotnet run` で起動し curl | 401（未認証）・400（検証、日本語）・403（CSRF の Origin 不一致）・401（Cookie なしの refresh）・500（内部情報なし）・改ざん JWT の拒否・Swagger UI 200・`Server` ヘッダーなし |

## 3. 未確認（Docker Desktop の不具合による）

| 項目 | 必要な確認 |
|---|---|
| MySQL へのマイグレーション適用 | `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build db migrate` |
| API と MySQL を使う一連の操作 | ログイン → プロジェクト作成 → タスク作成（採番）→ 更新（楽観的排他）→ コメント → 添付 → お知らせ |
| MySql.EntityFrameworkCore での LINQ の変換 | `Contains`（部分一致）、相関サブクエリでの並べ替え、`ExecuteDelete` / `ExecuteUpdate`、`DateOnly` |
| コンテナでの起動 | `docker compose up -d --build`（api の読み取り専用ルートファイルシステム: OD-29 を含む） |
| 実ブラウザでの画面操作 | Vite 開発サーバーまたは web コンテナ経由 |

これらは Phase 5（テストコード）・Phase 6（テスト実行）でも確認するが、Phase 4 の完了条件「Docker works」は満たしていない。

**2026-10-08 の判断**: Docker Desktop が応答せず、代替として検討したローカルの MySQL 8.0（Homebrew）も本体が起動できない状態（依存ライブラリ abseil の版ずれ）だった。利用者の判断により、今回は DB を使う確認とコンテナ起動の確認を見送った。Docker Desktop の復旧後、上表の手順で確認する。

**2026-10-11 の追記**: Phase 5 の指示により、Docker の代わりにローカルの MySQL 8.0（Homebrew、`brew services start mysql@8.0` で起動できるようになっていた）を使って、コンテナ起動以外の 4 項目を確認した。結果は §8。

## 4. 主なコマンド

```bash
# Backend
dotnet build src/TaskManager.sln
dotnet tool restore
dotnet ef migrations script --idempotent --project src/TaskManager.Infrastructure --startup-project src/TaskManager.Infrastructure --output artifacts/migrations/InitialSchema.sql

# OpenAPI（ビルドで artifacts/openapi/openapi.json が生成される）→ 差分確認のうえスナップショット更新
cp artifacts/openapi/openapi.json docs/api/openapi.v1.json

# Frontend
cd frontend/task-manager-web
npm run generate:api && npm run typecheck && npm run lint && npm run build
```

## 5. 設計からの変更（理由）

| # | 設計 | 実装 | 理由 | 記録先 |
|---|---|---|---|---|
| C-01 | アクセストークン発行は Infrastructure（`JwtTokenIssuer`） | Api 層（`JwtAccessTokenIssuer`） | 検証と同じ `JwtOptions` を使い、Infrastructure に JWT 関連パッケージを追加しないため | architecture.md §2 |
| C-02 | `ck_task_item__not_self_parent`（CHECK 制約） | 作らない | `parent_task_id` は ON DELETE CASCADE のため MySQL では CHECK で参照できない（エラー 3823）。ドメイン規則 TK-03 で保証 | database-design.md §3.8 |
| C-03 | 監査列の作成者・更新者はプロジェクト等で NOT NULL | すべて NULL 可 | システムによる操作（メンバー削除時の担当解除など）でも同じ規則で保存できるようにするため。アプリ経由の操作では常に設定される | database-design.md §1 |
| C-04 | 列の DB 既定値（`system_role` 既定 1 など） | 付けない | EF Core の既定値機能は「CLR の既定値と同じ値を送らない」ため、`false` を保存したいのに DB 既定の `true` になる不具合を招く。値は常にアプリが設定する | database-design.md §1 |
| C-05 | 外部キー用インデックスは設計の一覧のみ | `ix_task_item__status_id` 等、外部キー列に必要な索引を追加 | MySQL は外部キー列を先頭に持つ索引を要求するため（EF Core が自動作成。名前は規則に揃えた） | database-design.md §3 |
| C-06 | 文言は `src/i18n/ja.ts` に集約 | 列挙の表示名は `constants/labels.ts` に集約し、画面固有の文言は各コンポーネントに記述 | 日本語のみのシステムで、文言と画面を近くに置く方が保守しやすいと判断。翻訳の予定がないため | open-decisions.md OD-31 |
| C-07 | 選択部品・日付選択は React Aria Components | ネイティブの `select` / `input type="date"` / `checkbox`（ダイアログ・メニュー・タブ・検索のコンボボックスは React Aria） | ネイティブ部品はキーボード・スクリーンリーダー・スマートフォンでの操作が標準で使え、実装量が少ない | open-decisions.md OD-31 |
| C-08 | ボードのドラッグは React Aria DnD | HTML 標準のドラッグ&ドロップ + 各カードの「ステータスを変更」選択（キーボード代替） | 依存を増やさず、キーボード操作は代替手段で満たす | ui-design.md SCR-07 |
| C-09 | アイコンは lucide-react、日付は date-fns | インラインの SVG と `Intl.DateTimeFormat` | 必要なアイコン・日付処理が少なく、依存を増やさないため | technology.md §9 |
| C-10 | CSS Modules | 共通部品は `styles/components.css`（`tm-` 接頭辞）、画面固有は CSS Modules | 共通部品のスタイルを 1 か所で管理するため | ui-design.md §3 |
| C-11 | エラーコード一覧 | `status.last_one`（最後のステータスは削除できない）を追加 | 設計 REQ-STS-02 の「最後の 1 件」に対応するコードが一覧になかったため | api-design.md §1.5 |
| C-12 | PATCH の `version` | `[JsonRequired]` の整数（省略すると 400） | OpenAPI で null 不可の必須項目として表現するため | api-design.md |
| C-13 | lint（警告ゼロ） | `jsx-a11y/no-autofocus`（ダイアログの初期フォーカスは設計どおり）、`jsx-a11y/prefer-tag-over-role`（正しい ARIA role を許容）、`react/only-export-components`（開発時のホットリロード専用）を無効化。ドラッグ&ドロップの受け皿 3 か所は理由付きで個別に許可 | 設計上の意図と矛盾する規則のため | `frontend/task-manager-web/.oxlintrc.json` |
| C-14 | C# のコード分析 | `.editorconfig` で一部の規則を調整（理由をコメントに記載）。自動生成のマイグレーションは対象外 | 設計の命名を優先する規則などがあるため | `.editorconfig` |
| C-15 | Swagger UI の位置 | 認証ミドルウェアより前に配置 | 既定の認可ポリシー（ログイン必須）が静的ファイルにも適用されるため（環境構築時の I-07） | security-design.md S-20 |

## 6. 要件の実装状況

| 要件 | API | 画面 |
|---|---|---|
| REQ-AUTH-01〜06 | #1〜4、ロック・一時パスワード制限・即時失効 | SCR-01, 02, 16 |
| REQ-USR-01〜09 | #5〜15、起動時の初期管理者作成 | SCR-16, 17 |
| REQ-PRJ-01〜05 | #16〜22 | SCR-05, 12 |
| REQ-MEM-01〜04 | #23〜26（担当解除と履歴、最後の管理者保護） | SCR-13 |
| REQ-STS-01〜02 / REQ-TAG-01 | #27〜35 | SCR-14 |
| REQ-MLS-01 | #36〜40 | SCR-10, 11 |
| REQ-TSK-01〜09 | #41〜46（採番、楽観的排他、サブタスク、履歴） | SCR-06〜09 |
| REQ-CMT-01 / REQ-ATT-01 | #47〜54 | SCR-08 |
| REQ-MY-01 / REQ-NTC-01 / REQ-SRC-01 | #55〜60 | SCR-04, 15、ヘッダー |
| REQ-AUD-01 | `security_audit_event` への記録 + 構造化ログ | （閲覧画面は OD-13 で対象外） |
| 定期メンテナンス | `MaintenanceWorker`（1 時間ごと） | — |

## 7. Local と Docker の差異

| 項目 | ローカル開発（`dotnet run` + `npm run dev`） | Docker（`docker compose up`） |
|---|---|---|
| 入口 | http://localhost:5173（Vite が /api を :5080 にプロキシ） | http://localhost:8080（nginx が /api を api:8080 に中継） |
| 環境 | `ASPNETCORE_ENVIRONMENT=Development`（Swagger 有効、ログは通常形式） | `Production`（Swagger 無効、ログは JSON） |
| DB | `docker-compose.dev.yml` で公開した 127.0.0.1:3307 | 内部ネットワークの `db:3306` |
| マイグレーション | `docker compose ... run --rm migrate` または `dotnet ef database update` | `migrate` コンテナが自動で適用 |
| 添付の保存先 | `Attachments__RootPath`（例: `./artifacts/attachments`） | ボリューム `tm-attachments` |
| Cookie | `AuthCookie__Secure=true`（localhost は HTTP でも有効） | 同左（HTTPS 前提。LAN の HTTP 検証時のみ `TM_COOKIE_SECURE=false`） |
| 起動時処理 | 未適用のマイグレーションがあると起動しない（`Database__RunStartupTasks=false` で省略可） | 同左 |

## 8. Phase 5 開始時に行った未確認事項の確認（2026-10-11）

Phase 5 の指示（`.claude/skills/phase-05-test-code/CLAUDE.md`）に従い、テストコードの作成前に §3 の未確認事項を確認した。Docker Desktop は引き続き応答しないため（`docker info` が応答なし）、MySQL だけはローカル PC で起動した。

### 8.1 環境

| 項目 | 内容 |
|---|---|
| MySQL | ローカルの MySQL 8.0.46（Homebrew `mysql@8.0`）。設計の 8.4 系とは版が異なる（差異は §8.4） |
| DB・ユーザー | `taskmanager_db`（utf8mb4 / utf8mb4_0900_ai_ci）、`taskmanager_app`（DML のみ）、`taskmanager_migrator`（DB 単位の全権）を `docker/mysql/init/01-create-users.sh` と同じ権限で作成。パスワードは `.env` の値（どこにも記録していない） |
| 既存 DB | 同じサーバー上の他の DB には触れていない |
| API | `dotnet run --launch-profile http`（Development、`SslMode=Required` でローカル MySQL に接続） |
| Frontend | `npm run dev`（Vite、http://localhost:5173） |
| ブラウザ | Playwright 1.64 の Chromium（ヘッドレス、デスクトップ 1280×800 / スマートフォン 390×844、`ja-JP`、`Asia/Tokyo`） |

### 8.2 確認結果

| §3 の項目 | 確認方法 | 結果 |
|---|---|---|
| MySQL へのマイグレーション適用 | 空の DB に `dotnet ef database update`（マイグレーション用ユーザー） | 適用できた。表 14・CHECK 制約 11・外部キー 37 を `information_schema` で確認。`has-pending-model-changes` は差分なし |
| API と MySQL を使う一連の操作 | 検証用スクリプト（リポジトリ外）で API を 66 か所確認 | 初期管理者のログイン → 一時パスワードの変更 → 利用者作成 → プロジェクト作成 → メンバー追加 → タスク作成（並行作成で採番が重複しないこと）→ 更新（古い `version` で 409）→ コメント → 添付（種類の偽装を拒否）→ お知らせ → 検索（`%` を文字として扱う）→ 他プロジェクトのタスク ID 指定（404）→ プロジェクト削除。最終実行で 66 件すべて期待どおり、API ログに警告・エラーなし |
| MySql.EntityFrameworkCore での LINQ の変換 | 上記の一連の操作で該当クエリを実行 | `Contains`、相関サブクエリでの並べ替え、`ExecuteDelete` / `ExecuteUpdate` は変換できた。**`DateOnly` の読み込みで不具合を検出し修正**（§8.3 F-01） |
| 実ブラウザでの画面操作 | Playwright で画面を操作し、各段階の画面を撮影して目視確認 | ログイン失敗の表示 → 一時パスワードの変更 → 自分のタスク（空）→ プロジェクト作成 → タスク追加（詳細設定あり / Enter で連続追加）→ トーストの「開く」→ 一覧でのステータス変更 → ボード → 詳細（コメント投稿、変更履歴）→ 検索欄でのキー指定移動 → スマートフォン幅の一覧・メニュー・詳細。**不具合 3 件を検出し修正**（§8.3 F-02〜F-04）。最終実行でブラウザのエラーは想定どおりの 401（未ログイン時の refresh、誤ったパスワード）のみ。コメントの `<script>` は要素として描画されない（0 件） |
| コンテナでの起動 | — | **未確認**（Docker Desktop が応答しない） |

### 8.3 確認で見つかった不具合と修正

| ID | 不具合 | 原因 | 修正 |
|---|---|---|---|
| F-01 | 期限付きのタスク作成・取得が 500（`InvalidCastException`） | MySql.EntityFrameworkCore 10.0.9 が DATE 列を `DateTime` として読み込み、`DateOnly` に変換できない | `AppDbContext.ConfigureConventions` で `DateOnly` / `DateOnly?` に値変換（`DateOnlyConverter`）を設定。列の型（`date`）とマイグレーションは変わらない |
| F-02 | 画面を再読み込みするとログイン画面に戻る（refresh が 403 `auth.csrf_rejected`） | Vite のプロキシ（文字列指定）は Host を API 側（:5080）に書き換えるため、Origin（:5173）と一致しない。nginx も `Host $host`（ポートなし）で、`http://localhost:8080` で開くと同じ問題が起きる | `vite.config.ts` を `changeOrigin: false` に、`docker/web/nginx.conf` を `Host $http_host` / `X-Forwarded-Host $http_host` に変更。nginx 側は Docker 不調のため実機では未確認 |
| F-03 | タスク追加後に画面が真っ白になる（`Cannot destructure property 'basename'`） | `ToastProvider` が `RouterProvider` の外側にあり、トースト内の `Link`（「開く」）がルーターの文脈なしで描画された | トーストのリンクを通常の `<a href>` にし、遷移は `router.navigate` に任せる（`onNavigate`）。修飾キー付きのクリックはブラウザに任せる |
| F-04 | デスクトップ・スマートフォンともヘッダーにメニューのボタンが 2 つ表示される | CSS Modules の `display: none` が共通部品 `.tm-btn` の `display` と同じ詳細度で、読み込み順により上書きされた | `.header .menuButton` のように詳細度を上げた |
| F-05 | 画面遷移で中断されたリクエストについて、API ログに未処理例外が記録される（利用者への影響なし） | 認証時の security_stamp 照合にリクエストの中断トークンを渡しており、MySql.Data が SSL 接続確立中の取り消しでコールバックから例外を投げる | 照合はキャッシュで共有するため、個々のリクエストの中断では取り消さないよう変更（主キー検索 1 件） |

### 8.4 ローカル MySQL 8.0 と設計（8.4）の差異

- 使用した機能（CHECK 制約、`utf8mb4_0900_ai_ci`、`DATETIME(6)`、外部キーの削除時の挙動）は 8.0 と 8.4 で同じ。
- 認証プラグインの既定（8.4 は `mysql_native_password` が既定で無効）の違いは、`caching_sha2_password` を使う限り影響しない。
- 8.4 での確認は Docker の復旧後に行う（Phase 6 でも可能なら実施する）。

## 9. Phase 6（テスト実行）で行った実装の修正（2026-10-12）

テストの実行で見つかった不具合を修正した。詳細は [docs/testing/test-results.md](../testing/test-results.md)。

| ID | 修正 |
|---|---|
| R-01 | 検証エラー・認可フィルターの応答が `application/json` になっていた → `application/problem+json` で返す |
| R-02 | Login・Refresh の 401 を OpenAPI に記載（スナップショット・Frontend の型を更新） |
| R-03 | 一時パスワード状態の 403 を、匿名で呼べる API には適用しない（authentication-authorization.md §3.3 を明確化） |
| R-05 | トークン更新の完了結果を後の呼び出しに使い回していた |
| R-09 | 11MB を超える添付が 400 になっていた → 413 `attachment.too_large` |
| R-10・R-11 | メンバー追加の検索欄のラベルの関連付け、候補の選択が外れる問題 |
| R-13 | 詳細画面で続けて項目を変えると先の変更が失われる（自分の変更同士の 409） |
| R-17・R-18 | Docker: api イメージのビルド（`.editorconfig`）、MySQL 初期化スクリプトの実行 |

## 10. Phase 8（完成確認）で行った修正（2026-10-13）

| 修正 | 内容 |
|---|---|
| 初回読み込みの JS の増加 | Phase 7 でログイン後の移動先の判定が Markdown の部品を読み込み、初回の JS が 307KB → 464KB に増えていた。判定を `src/utils/url.ts` に分け、307KB に戻した |
| CSP 違反（本番構成） | react-aria が挿入する固定の `<style>` を CSP のハッシュで許可（`docker/web/nginx.conf`）。zod が eval の可否を試す処理を止めるため `jitless` を最初に設定（`src/zodConfig.ts`）。全画面で違反 0 件、E2E-08 で継続して確認 |
| テスト基盤の停止 | テスト用 DB の作成で `MigrateAsync` のロック待ちのままテストがまれに止まる問題。テスト用 DB には生成した SQL を直接適用する方式に変更（security-review.md SEC-INFO-08。アプリ本体の変更なし） |
| README・起動手順 | README を TaskManager の説明に書き直し、`docs/build_Application/StartUp-Application.md` を追加 |

## 11. Phase 9（修正）: ガントチャート・ステータス別円グラフの追加（2026-10-16）

詳細は [docs/corrections/phase-09/](../corrections/phase-09/)（investigation / impact-analysis / change-log / verification-report）。

| 要件 | API | 画面 |
|---|---|---|
| REQ-GNT-01〜03 ガントチャート | #62 `GetTaskSchedule`（新規）、#44 `UpdateTask`（既存。日程の変更） | SCR-20（`/projects/:key/tasks?view=gantt`） |
| REQ-CHT-01 プロジェクトのステータス別円グラフ | #63 `GetTaskStatusSummary`（新規） | SCR-21（`?view=chart`） |
| REQ-CHT-02 自分のタスクのステータス別円グラフ | #64 `GetMyTaskStatusSummary`（新規） | SCR-04（`/?view=chart`） |

- 規模: API 61 → 64 操作。マイグレーション 1 件追加（`AddTaskStatusSummaryIndex`、索引のみ）。Entity・表・列の追加なし。
- ライブラリの追加なし（グラフ・ガントは SVG + CSS Modules。docs/design/technology.md §10）。初回読み込みの JS は 307.75KB（変更前 307.6KB）。
