# 最終チェックリスト（Phase 8: 完成確認）

- 確認日: 2026-10-13
- 確認環境: macOS 15.8.1 / .NET SDK 10.0.401 / Node.js 24.10.0 / Docker Desktop（Engine 29.5.2、Compose 5.1.4）/ ローカル MySQL 8.0.46 / コンテナ MySQL 8.4.11 / Playwright 1.64（Chromium）
- 「完了」は、Command 欄のコマンドを Phase 8 で実際に実行して得た結果（Result）に基づく。過去のフェーズの記録だけに基づく項目はその旨を書いた。

## 1. Plane からの独立性

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| Plane のコードをコピーしていない | 完了 | ソース・テスト・Docker・設定に Plane 固有の名前（plane / makeplane / workspace / issue / cycle 等）がない。`Issue` はトークン発行の動詞のみ | `grep -rniI "plane\|makeplane" src frontend/... tests docker ...`、`grep -rnIw "workspace\|issue\|cycle" ...` | 該当はテストの検出語（"plane" が OpenAPI に含まれないことの検査）と指示書の README の説明のみ |
| Plane の Secret をコピーしていない | 完了 | Plane の `.env.example`（6 ファイル）の 8 文字以上の値 19 件を、TaskManager のソース・設定・テストと照合 | `find plane -name ".env*"` → 値を抽出して `grep -rqF` | 一致 0 件 |
| Plane のユーザーデータ・サンプルデータ・Branding をコピーしていない | 完了 | 初期データは初期管理者（`.env` で指定）と既定のステータスのみ。テスト・画面キャプチャのデータは架空（`@example.test`、「営業支援」など）。名称・ロゴは「TaskManager」 | 初期データの確認（§5）、`docs/images/` の目視 | Plane のデータ・名称なし |
| Plane の DB を使っていない | 完了 | DB 名 `taskmanager_db`、表名・列名は独自の設計（docs/design/database-design.md） | `SELECT TABLE_NAME FROM information_schema.TABLES` | 独自の 14 表 |
| Plane に実行時依存していない | 完了 | Plane のパス・パッケージへの参照なし。Docker イメージは TaskManager のソースだけでビルド | `grep -rnI "CSharp/plane\|\.\./plane" src frontend tests docker ...`、`package.json` / `Directory.Packages.props` の確認 | 参照 0 件 |
| Plane のリポジトリを変更していない | 完了 | `preview` ブランチのまま変更なし | `git -C ../plane status --short` | 変更 0 件 |

## 2. ドキュメント

| 項目 | Status | Evidence | Result |
|---|---|---|---|
| docs/analysis | 完了 | 15 ファイル（指定の 14 + API 付録） | Phase 1 |
| docs/design | 完了 | 要件（43 要件）・アーキテクチャ・ドメイン・DB・API（61 操作）・画面（UI/UX）・認証認可・セキュリティ・テスト戦略・命名・マイグレーション・技術・未決事項 | Phase 2。Phase 3〜8 の変更を各文書に追記 |
| docs/environment | 完了 | 環境構築・設定・Docker・ローカル開発・発生した問題（I-01〜I-12） | Phase 3、Phase 6・7 で更新 |
| docs/implementation | 完了 | 実装状況・設計からの変更（C-01〜）・要件と API / 画面の対応・Phase 6〜8 の修正 | status.md |
| docs/testing | 完了 | テスト設計・テスト一覧（対象・目的・条件・期待結果・ファイル）・実行結果 | test-design.md / test-coverage.md / test-results.md |
| docs/security | 完了 | 観点ごとの確認結果と指摘（SEC-01〜10、INFO-01〜09） | security-review.md |
| docs/completion | 完了 | 本書・既知の課題 | final-checklist.md / known-issues.md |
| docs/build_Application | 完了 | 起動手順（Docker・個別起動）。記載のコマンドで起動と接続を確認 | StartUp-Application.md（§10） |
| API 契約 | 完了 | `docs/api/openapi.v1.json` が実行中の API の文書と一致 | §7 |
| 内容と実装の一致 | 完了 | OpenAPI のスナップショット比較テスト、認可マトリクスと文書の operationId の一致テスト、要件 43 件の対応表（status.md §6）。Phase 6〜8 の変更（エラー形式・401 の記載・レート制限・DB イメージ・CSP など）を設計書・環境文書に反映 | テスト成功（§8） |

## 3. Backend

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| .NET 10 | 完了 | 全プロジェクト `net10.0`、SDK 10.0.401 | `dotnet --version`、`grep TargetFramework` | 10.0.401 / net10.0 |
| Solution / Build | 完了 | 4 層 + テスト 5 プロジェクト。警告をエラーとして扱う | `dotnet build src/TaskManager.sln` | 警告 0・エラー 0 |
| Configuration | 完了 | Secret は環境変数のみ。署名鍵・接続文字列が不正なら起動しない | `cat appsettings*.json` の確認、起動確認（§6） | appsettings に Secret なし |
| EF Core / Migration | 完了 | マイグレーション 1 件（InitialSchema）、モデルとの差分なし。Docker の migrate コンテナで適用 | `dotnet ef migrations list`、`dotnet ef migrations has-pending-model-changes` | `20261008055811_InitialSchema`、「No changes have been made」 |
| MySQL | 完了 | §5 | — | — |
| REST API | 完了 | 61 操作（GET 22・POST 21・PATCH 9・DELETE 8・PUT 1） | `GET /openapi/v1.json` | 61 操作 |
| Authentication | 完了 | JWT（HS256・15 分）+ リフレッシュ Cookie、ロック、一時パスワード、即時失効 | API テスト（§8） | 成功 |
| Authorization | 完了 | 全 61 操作 × 8 種類の呼び出し元（488 ケース）、IDOR | API テスト（§8） | 成功 |

## 4. Frontend

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| React / TypeScript / Vite | 完了 | React 19、TypeScript 5.9、Vite 8 | `package.json` の確認 | — |
| Build | 完了 | 型チェック・lint（警告 0）・本番ビルド。初回読み込みの JS 307.6KB（gzip 95KB）、画面ごとに分割 | `npm run typecheck`、`npx oxlint --deny-warnings`、`npm run build` | すべて成功 |
| Routing / API client | 完了 | React Router（遅延読み込み・ログイン必須の画面の保護）、OpenAPI から生成した型の API クライアント（401 時のトークン更新・Problem Details の変換） | Frontend テスト（§8） | 成功 |
| UI / UX | 完了 | 表・ボード・詳細・その場の編集・確認ダイアログ・ショートカット | E2E（§8）、画面キャプチャ（README §2） | 成功 |
| Loading / Empty / Error | 完了 | 読み込み中・空状態・エラー（再試行・問い合わせ番号）の表示 | Frontend テスト（ProjectTasksPage・MyTasksPage） | 成功 |
| Responsive | 完了 | スマートフォン幅（390×844）でカード表示・ドロワーのメニュー、横スクロールなし | E2E-07 | 成功 |
| Japanese UI | 完了 | 画面・検証メッセージ・エラー・お知らせ・Swagger の説明が日本語 | 画面キャプチャ（README §2）、E2E | 確認済み |
| アクセシビリティ | 完了 | axe の serious / critical の違反なし（主要 9 画面） | E2E-08 | 成功 |
| CSP 違反 | 完了（Phase 8 で修正） | 本番構成で CSP 違反 7 件を検出 → react-aria の固定スタイルをハッシュで許可、zod の eval 試行を停止 → 全画面で 0 件 | `securitypolicyviolation` の記録（全 11 画面・ダイアログ）、E2E-08 | 0 件 |

## 5. Database

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| MySQL starts | 完了 | 空のボリュームから起動し healthy（非 root、派生イメージ） | `docker compose up -d --build`、`docker compose ps` | db `Up (healthy)` |
| Migration works | 完了 | migrate コンテナ（efbundle）が適用して正常終了 | `docker compose ps`、`__EFMigrationsHistory` | `Exited (0)`、`InitialSchema` / EF Core 10.0.12 |
| Schema matches design | 完了 | 表 14、文字コード utf8mb4 / utf8mb4_0900_ai_ci | `information_schema.TABLES`、`SELECT VERSION(), @@collation_server` | 14 表、8.4.11 |
| Relations / Constraints / Indexes | 完了 | 外部キー 37、CHECK 11、一意 10、インデックス 69（database-design.md と一致。Infrastructure テストで個別に検査） | `information_schema.REFERENTIAL_CONSTRAINTS / TABLE_CONSTRAINTS / STATISTICS` | 37 / 11 / 10 / 69 |
| Seed data | 完了 | 初期システム管理者（一時パスワード状態）。プロジェクト作成時の既定ステータス 5 件 | `SELECT ... FROM user_account`（`--default-character-set=utf8mb4`） | `admin@example.test`・「システム管理者」・管理者・変更必須 |
| Connection | 完了 | アプリ用利用者（DML のみ）で TLS 1.3 接続、root はコンテナ内のみ | `/health/ready`、`mysql.user` | `Healthy`、`root@localhost` |
| Backup / Restore | 完了 | README §8 の手順で取得し、別の DB に復元して件数一致 | `mysqldump`（コンテナ内）→ `mysql restore_check < backup.sql` | 利用者 3・タスク 1 が一致 |

## 6. Docker

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| Build | 完了 | api・migrate・web・db（派生）の 4 イメージ | `docker compose down -v && docker compose up -d --build` | 成功（約 80 秒） |
| Startup | 完了 | db healthy → migrate 正常終了 → api healthy → web healthy | `docker compose ps -a` | 4 サービスとも期待どおり |
| Network | 完了 | `tm-internal`（internal: true。db・migrate・api）、`tm-edge`（api・web）。公開は web の 8080 のみ | `docker network ls`、`docker compose ps` | 期待どおり |
| Volume | 完了 | `tm-mysql-data`、`tm-attachments` | `docker volume ls` | 2 ボリューム |
| Health | 完了 | 各ヘルスチェック、API の `/health/ready` は DB 接続を含む | `docker compose exec api curl /health/ready`、`curl /healthz` | `Healthy`、`ok` |
| Backend / Frontend / MySQL | 完了 | nginx 経由で API に接続、画面 200、E2E 8 / 8 | `curl http://localhost:8080/api/v1/system/info`、E2E | 200、成功 |
| Linux コンテナ・非 root | 完了 | 全コンテナ非 root・ケーパビリティなし・no-new-privileges、api・web は読み取り専用ルート | Phase 7 の確認（security-review.md SEC-09） | — |
| ログ | 完了 | API のログの Warning は DataProtection の 3 件のみ（影響なし: known-issues.md KI-10） | `docker compose logs api` | Error 0 件 |

## 7. Swagger / OpenAPI

開発用の上書き設定（`docker-compose.dev.yml`）で起動し、ブラウザで Swagger UI を開いて確認した。

| 項目 | Status | Evidence | Command | Result |
|---|---|---|---|---|
| Swagger UI | 完了 | タイトル「TaskManager API」、操作 61、タグ 17（日本語の説明） | Playwright で `http://127.0.0.1:5080/swagger/index.html` を表示（README の画面 08） | 表示された |
| OpenAPI document | 完了 | 実行中の API の文書とスナップショット `docs/api/openapi.v1.json` が一致（servers を除く） | `curl /openapi/v1.json` と比較 | 一致 |
| Endpoint / HTTP Method | 完了 | 61 操作のメソッド・パスで、認可マトリクスの全ケースが文書の状態コードと一致 | API テスト | 成功 |
| Request / Response / Status Code | 完了 | 全応答を OpenAPI のスキーマで検証（未定義の項目も検出） | API テスト | 成功 |
| Authentication | 完了 | Swagger UI で Authorize の前後に `GET /api/v1/me` を実行 | Playwright | 401 → 200 |
| Authorization | 完了 | Swagger UI でシステム管理者専用の `GET /api/v1/users` を実行 | Playwright | 200（管理者）。一般利用者 403 は API テストで確認 |
| Error schema | 完了 | Swagger UI で存在しないプロジェクト・不正な入力を実行 | Playwright | 404 / 400 の Problem Details（`code`・`traceId`・`errors`） |
| No secret leakage | 完了 | 文書に Secret・接続文字列・内部パス・例外名なし。本番構成では公開しない | 契約テスト、`curl http://localhost:8080/swagger/index.html` | 含まれない、本番 401 |

## 8. テスト

| 項目 | Status | Command | Result |
|---|---|---|---|
| Backend（Domain・Application・Infrastructure・API。認証・認可・IDOR・入力・セキュリティ関連を含む） | 完了 | `TM_TEST_MYSQL_CONNECTION=... dotnet test --solution src/TaskManager.sln`（ローカル MySQL 8.0.46） | **994 / 994 成功を 2 回連続**（23:50〜23:59） |
| Backend（MySQL 8.4） | 完了 | 同上（`TM_TEST_MYSQL_CONNECTION` なし = Testcontainers `mysql:8.4.11`） | **994 / 994 成功** |
| Frontend（部品・画面・API クライアント・XSS・権限表示） | 完了 | `npx vitest run` | **107 / 107 成功** |
| E2E（主要フロー・権限・スマートフォン幅・アクセシビリティ・CSP） | 完了 | 空の DB で `docker compose up -d --build` → `TM_E2E_BASE_URL=http://localhost:8080 npx playwright test` | **8 / 8 成功** |
| これまでの実行結果の確認 | 完了 | test-results.md（Phase 6）・security-review.md §5（Phase 7） | 失敗の原因と修正の記録あり。未実行のものを成功扱いしていない |

### 8.1 Phase 8 で起きたテストの停止と対応

Backend の全体実行が、テスト用 DB のマイグレーションのロック待ちで止まった（Phase 7 と同じ現象。残ったプロセスはなし）。テスト用 DB には EF Core のマイグレーションから生成した SQL を直接適用する方式に変更し、2 回連続で全件成功・停止なしを確認した（security-review.md SEC-INFO-08）。アプリ本体の変更はない。

変更後は、ローカル MySQL 8.0.46 で 2 回、Testcontainers（MySQL 8.4.11）で 1 回、いずれも 994 / 994 成功。

## 9. セキュリティ

| 項目 | Status | Evidence | Result |
|---|---|---|---|
| Critical / High | 完了 | High 1 件（DB イメージの脆弱性）は修正済み。Critical 0 件 | 未対応の Critical / High なし |
| 受け入れたリスク | 明記 | ログアウト後 15 分のトークン有効、ロック時の利用者の存在、ロックの嫌がらせ、社内名簿、DB 接続の証明書未検証 | known-issues.md §2 |
| 利用者の対応が必要な事項 | 明記 | 指示書（CLAUDE.md 2 ファイル）のローカル MySQL のパスワード（Git 未追跡） | known-issues.md KI-01 |
| 依存関係 | 完了 | `dotnet list package --vulnerable --include-transitive`・`npm audit`: 0 件、アプリのイメージ（Trivy）: HIGH / CRITICAL 0 件 | Phase 7 |

## 10. README・起動手順

| 項目 | Status | Evidence |
|---|---|---|
| README | 完了 | 概要・機能・画面キャプチャ 8 枚・技術・ディレクトリ・必要なもの・セットアップ・環境変数・Docker・DB・マイグレーション・バックアップ・Backend / Frontend の起動・Swagger・テスト・セキュリティ・開発の進め方・ドキュメント一覧 |
| 起動手順（StartUp-Application.md） | 完了 | 方法 A（Docker）: 空のボリュームから起動して画面・API の接続、E2E 8 / 8 を確認。方法 B（個別起動）: 記載のコマンドをそのまま実行し、API `Healthy`・画面 200・画面経由の API 200、ブラウザでログインしてタスク一覧の表示と再読み込み後のログイン維持を確認 |

## 11. Git / Secret

| 項目 | Status | Command | Result |
|---|---|---|---|
| `.env` が除外されている | 完了 | `git check-ignore -v .env` | `.gitignore:2:.env` |
| 生成物・依存・テスト結果が除外されている | 完了 | `git check-ignore -v node_modules artifacts dist bin obj test-results playwright-report` | すべて除外 |
| 実際の Secret の値がコミット対象にない | 完了 | `.env` の 5 つの Secret の値を、追跡済み + 未追跡（除外以外）の全ファイルと照合 | 0 件 |
| API キー・秘密鍵・トークンの形式がない | 完了 | `BEGIN ... PRIVATE KEY`、`AKIA...`、`ghp_...`、`sk-...`、`xox[bp]-` を検索 | 0 件 |
| OAuth / SMTP の Secret | 該当なし | 外部認証・メール送信の機能はない（docs/analysis/external-services.md、設計で対象外） | — |
| `.env.example` | 完了 | Secret の欄はすべて空欄 | 確認済み |
| Git の状態・履歴 | 確認 | `git status`、`git log --all` | 履歴は初期コミット（README のみ）。成果物は未コミット（コミットは利用者の判断） |
| 指示書のパスワード | **利用者の対応待ち** | 上の照合でローカル MySQL のパスワードを検索 | `CLAUDE.md`・`.claude/skills/phase-05-test-code/CLAUDE.md` に存在（KI-01）。コミット前に削除が必要 |

## 12. 完成判定

| 項目 | 判定 | 根拠 |
|---|---|---|
| [x] Requirements | 完了 | docs/design/requirements.md（43 要件）、status.md §6 で全要件を API・画面に対応付け |
| [x] Architecture | 完了 | docs/design/architecture.md、実装は 4 層（Domain / Application / Infrastructure / Api） |
| [x] Database design | 完了 | §5（実 DB で表・制約・インデックスを確認） |
| [x] API design | 完了 | api-design.md（61 操作）と OpenAPI・実装が一致 |
| [x] Swagger/OpenAPI | 完了 | §7 |
| [x] Authentication | 完了 | §3・§8 |
| [x] Authorization | 完了 | §3・§8（488 ケース + IDOR） |
| [x] UI/UX | 完了 | §4、README の画面、E2E・アクセシビリティ |
| [x] Backend implementation | 完了 | §3 |
| [x] Frontend implementation | 完了 | §4 |
| [x] EF Core migrations | 完了 | §3・§5 |
| [x] MySQL | 完了 | §5 |
| [x] Docker Compose | 完了 | §6 |
| [x] Backend build | 完了 | §3 |
| [x] Frontend build | 完了 | §4 |
| [x] Test code | 完了 | docs/testing/test-coverage.md |
| [x] Test execution | 完了 | §8 |
| [x] Security review | 完了 | §9、security-review.md |
| [x] Secret review | 完了（利用者の対応 1 件） | §11。アプリのコード・設定・イメージ・文書に Secret なし。利用者が作成した指示書のパスワード（KI-01）はコミット前に削除が必要 |
| [x] README | 完了 | §10 |
| [x] Final documentation | 完了 | 本書、known-issues.md、StartUp-Application.md |
