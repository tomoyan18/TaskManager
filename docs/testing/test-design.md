# テスト設計（Phase 5）

- 作成日: 2026-10-12
- 設計の元: [docs/design/testing-strategy.md](../design/testing-strategy.md)（Phase 2）。本書は、実際に作成したテストコードの構成と、設計から変えた点を記録する。
- 個々のテストの一覧（対象・目的・条件・期待結果・ファイル）と、OpenAPI のエンドポイントとの対応は [test-coverage.md](test-coverage.md)。
- テストの実行結果は [test-results.md](test-results.md)（Phase 6）。Phase 6 で行ったテストの修正・追加も同書に記録した。

## 1. 目的

業務上の振る舞い、API の契約（OpenAPI）、DB の制約と関係、認証・認可（IDOR を含む）、入力検証、画面の操作の流れを、
自動テストで確認できる状態にする。行数のカバレッジではなく「壊れると困る振る舞い」を基準に選んだ。

## 2. 全体構成

| 種類 | 場所 | 主な道具 | DB / 外部 | 件数（一覧表示で数えた値） |
|---|---|---|---|---|
| Domain | `tests/TaskManager.Domain.Tests` | xUnit v3 | なし | 72 メソッド / 133 ケース |
| Application | `tests/TaskManager.Application.Tests` | xUnit v3 + 本番と同じ DI 構成 | MySQL | 119 メソッド / 215 ケース |
| Infrastructure（EF Core / MySQL） | `tests/TaskManager.Infrastructure.Tests` | xUnit v3 | MySQL | 25 メソッド / 53 ケース |
| API | `tests/TaskManager.Api.Tests` | xUnit v3 + WebApplicationFactory（TestServer） | MySQL | 54 メソッド / 587 ケース（うち認可マトリクス 488） |
| テスト共通部品 | `tests/TaskManager.Testing` | — | MySQL | （テストなし） |
| Frontend | `frontend/task-manager-web/src/**/*.test.ts(x)` | Vitest 5 + Testing Library + MSW 2 + jsdom | なし（MSW） | 8 ファイル / 61 定義（`it.each` は 1 件と数える）/ 実行時 101 件 |
| E2E | `frontend/task-manager-e2e/tests` | Playwright 1.64 + @axe-core/playwright | 起動中の Web・API・MySQL | 8 シナリオ |

「ケース」は `[Theory]` のデータ行を展開した数（`dotnet test --list-tests` の検出数）。

## 3. Backend のテスト基盤

### 3.1 MySQL（テスト用 DB）

`tests/TaskManager.Testing/MySqlTestServer.cs`

| 項目 | 内容 |
|---|---|
| 接続先の選択 | 環境変数 `TM_TEST_MYSQL_CONNECTION` があればその MySQL（ローカル PC で起動した MySQL など）を使う。なければ Testcontainers で `mysql:8.4.11`（`TM_TEST_MYSQL_IMAGE` で変更可）を起動する |
| 接続文字列 | DB の作成・削除ができる利用者で、`Database` は指定しない（例: `Server=localhost;Port=3306;User ID=<利用者>;Password=<パスワード>;SslMode=Required`）。値はシェルの環境変数で渡し、リポジトリ・ドキュメントに書かない |
| DB の作成 | `tm_test_` + 乱数 12 桁の名前で `utf8mb4_0900_ai_ci` の DB を作り、EF Core のマイグレーション（`InitialSchema`）から生成した SQL を適用する（`__EFMigrationsHistory` も記録される）。当初は `MigrateAsync` を使ったが、MySQL のサーバー全体のロックでテストがまれに止まったため Phase 8 で変更した（security-review.md SEC-INFO-08）。マイグレーションの適用は Docker の migrate コンテナと `dotnet ef database update` で確認している |
| 後始末 | テストの終了時に `DROP DATABASE`。削除は `tm_test_` で始まる名前だけに限定し、それ以外の DB には触れない |
| 共有の単位 | テストアセンブリごとに 1 つの DB を共有する（xUnit v3 の AssemblyFixture `DatabaseFixture` / `ApiFactory`） |
| データの分離 | 各テストが一意なプロジェクトキー・メールアドレスでデータを作るため、同じ DB でも互いに干渉しない（並行実行可） |
| DB 全体に依存するテスト | 「最後の管理者」「初期管理者の作成」は、テストごとに専用の DB を作る（`CreateIsolatedDatabaseAsync`） |

### 3.2 テストデータ

`tests/TaskManager.Testing/TestDataBuilder.cs`

- テスト対象のサービスを経由せず、Domain の生成メソッドで DB に直接作る（利用者・プロジェクト・メンバー・タスク・コメント・添付・タグ・マイルストーン・ステータス）。
- タスク番号は本番と同じ採番処理（`MySqlTaskNumberAllocator`）で割り当てる。
- 値は架空（`@example.test`、「テスト 管理者」など）。Plane のサンプルデータは使わない。
- パスワードを指定しない利用者は共通の架空パスワードを使い、ハッシュ計算（PBKDF2）を 1 回にまとめて作成を速くする。

### 3.3 Application テスト

`tests/TaskManager.Testing/ApplicationTestHost.cs`

- 本番と同じ `AddApplication()` + `AddInfrastructure()` で MySQL に接続する（Repository のモックは使わない。EF Core の LINQ が MySQL に変換できるかも同時に確認する）。
- 差し替えるのは 3 つだけ: 現在の利用者（`TestCurrentUser`）、時刻（`FakeTimeProvider`。既定 2026-10-08 10:00 JST）、アクセストークンの発行（`FakeAccessTokenIssuer`）。
- 1 回の呼び出し = 1 スコープ（本番の 1 リクエストと同じ）。
- 添付ファイルは一時ディレクトリに保存し、テスト後に削除する。

### 3.4 API テスト

`tests/TaskManager.Api.Tests/Support/ApiFactory.cs`

| 項目 | 内容 |
|---|---|
| ホスト | 本番と同じ `Program` を TestServer で起動（環境名 `Testing`、Swagger 有効、定期メンテナンス無効） |
| JWT の署名鍵 | 起動ごとに乱数で生成（Secret をリポジトリに置かない） |
| アクセストークン | 本番と同じ `JwtAccessTokenIssuer` で発行（ログイン API の回数制限を避けるため）。ログイン自体は認証テストで API を通して確認する |
| Cookie | `https://localhost` を基準にし、`Secure` の Cookie を HttpClient が保持・送信する |
| 送信元 IP | テスト専用ヘッダー `X-Test-Remote-IP` を、テストでだけ登録するミドルウェアで接続元 IP に反映する。クライアントごとに別の IP にして、レート制限の区分がテスト間で干渉しないようにする |
| 500 の確認 | 添付の種類判定にテスト専用の差し替えを入れ、特定のファイル名（`trigger-internal-error.txt`）のときだけ例外を投げる。応答に内部情報が出ないこと・ログには追跡 ID とともに記録されることを確認する |
| ログの検査 | テスト専用のログ収集で、パスワード・トークンがログに出ないことを確認する |

### 3.5 認可マトリクス（testing-strategy.md §5）

- `tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrix.cs` に、OpenAPI の全 61 操作の要求の作り方とアクセス区分を 1 か所で定義した。
- 呼び出し元は 8 種類: 未ログイン / 一時パスワード状態 / 非メンバー / 閲覧者 / 編集者 / 管理者 / システム管理者（非メンバー）/ アーカイブ中のプロジェクトの管理者。
- 期待するステータスは、アクセス区分（例: 「編集者以上・アーカイブ中は 409」）と呼び出し元から計算する。
- ケースごとに新しいプロジェクト・利用者・タスク・コメント・添付などを作るため、削除や変更の操作も他のケースに影響しない。
- 全ケースで、応答のステータスが OpenAPI に定義されていること、本文がスキーマに一致することも検査する。
- OpenAPI の operationId とマトリクスの一致を検査するテストを置き、エンドポイントの追加時に表への追加漏れを検出する。

### 3.6 OpenAPI 契約テスト（testing-strategy.md §6）

| 検査 | 方法 |
|---|---|
| スナップショット比較 | 実行中の API が返す `/openapi/v1.json` と、ビルド時生成の `artifacts/openapi/openapi.json` を、それぞれコミット済みの `docs/api/openapi.v1.json` とキー順を正規化して比較（`servers` は環境で変わるため除外） |
| 文書の構成 | 全操作に tags・summary・operationId、匿名で呼べるのは Login と GetSystemInfo だけ、refresh / logout は Cookie、その他は bearerAuth。全操作に 500、ログイン必須の操作に 401、エラー応答は `application/problem+json` |
| 要求の制約 | 文字数の制約（例: `CreateTaskRequest.title` 1〜200）、更新要求の `version` が必須 |
| 応答の適合 | `OpenApiContract.AssertResponseAsync`（OD-26: 自作の最小検証器）で、受け取った応答のステータスと本文を検査。文書にないプロパティを返した場合も失敗にする（契約にない情報の漏れの検出） |
| 秘密情報の非掲載 | スナップショットに Secret 名・接続文字列・内部パス・例外名・Plane の名前が含まれないこと |
| Frontend | 同じスナップショットから生成した型で `tsc` を通す（既存の `npm run typecheck`） |

OD-26 の決定: JSON Schema 検証ライブラリは追加せず、この OpenAPI 文書で使っている構文（type・null を含む型の配列・required・properties・additionalProperties・items・enum・oneOf・$ref・format の uuid / date / date-time・minLength / maxLength）だけに対応した検証器を自作した。検証器自体の誤りを防ぐため、型違い・必須項目の欠落・文書にない項目を検出できることを確認するテストを置いた。

## 4. 主な観点と担当するテスト

| 区分（testing-strategy.md §4） | 主なテスト |
|---|---|
| 採番（20 並列、プロジェクトごとに独立、取り消し時に戻る） | Infrastructure `PersistenceBehaviorTests`、Application `TaskItemServiceTests` |
| 楽観的排他 | Infrastructure `PersistenceBehaviorTests`、Application（タスク・プロジェクト・マイルストーン・利用者）、API `ValidationApiTests` |
| 期限区分（タイムゾーン、日曜・月曜、年またぎ） | Domain `DueBucketClassifierTests`、Application `MyTasksServiceTests`、Frontend `dates.test.ts` |
| 完了日時 | Domain `TaskItemTests`、Application `TaskItemServiceTests` |
| 親子（2 段目・自己参照・サブタスクありの削除） | Domain `TaskItemTests`、Application `TaskItemServiceTests`、Infrastructure（外部キーの連鎖削除） |
| ステータス削除（既定・使用中・移動後の履歴・別プロジェクトの移動先） | Application `WorkflowStatusServiceTests`、API `IdorApiTests` |
| メンバー削除（担当解除と履歴、最後の管理者） | Application `ProjectMembershipServiceTests` |
| お知らせ（本人に作らない、5 分以内をまとめる、他人宛ては 404） | Application `TaskCommentServiceTests` `TaskItemServiceTests` `NoticeServiceTests`、API `IdorApiTests` |
| 認証（ロック、停止、ローテーション、再利用検知、パスワード変更で他セッション失効、即時失効、一時パスワード） | Application `AuthServiceTests`、API `AuthApiTests` |
| トークン（改ざん・署名なし・期限切れ・別 audience / issuer・別の鍵・形式不正） | API `AuthApiTests` |
| CSRF（別 Origin、Origin なし + cross-site、Origin なし） | API `AuthApiTests` |
| 入力検証（境界値、列挙、日付、数値の文字列、未知の項目、深い入れ子、壊れた JSON） | Domain、API `ValidationApiTests` |
| SQL インジェクション・LIKE の記号 | Application `TaskItemServiceTests`、API `ValidationApiTests` |
| 添付（10MB ちょうど / 超過、種類の偽装、許可外、ファイル名の `../`、件数上限、ダウンロードのヘッダー、保存先の外を指すキー） | Domain、Application `TaskAttachmentServiceTests`、Infrastructure `AttachmentStorageTests`、API `SecurityApiTests` |
| XSS（Markdown の script・イベント属性・javascript: リンク・画像） | Frontend `MarkdownView.test.tsx` `ProjectTasksPage.test.tsx`、E2E-04 |
| オープンリダイレクト（ログイン後の移動先） | Frontend `taskKey.test.ts`（safeNextPath）`auth.test.tsx` |
| エラー情報の漏えい・セキュリティヘッダー・ログ | API `SecurityApiTests` `AuthApiTests` |
| レート制限（ログイン 11 回目で 429 と Retry-After） | API `AuthApiTests` |
| トークンの保持（localStorage 等に書かない） | Frontend `client.test.ts` |
| アクセシビリティ | E2E-01・04・07・08（axe の serious / critical） |

## 5. Frontend のテスト

| 項目 | 内容 |
|---|---|
| 構成 | Vitest（jsdom）+ Testing Library + MSW。共通設定は `src/test/environment.ts`（最初に読み込む: MSW の開始・fetch の補正・jsdom にない API の補完）と `src/test/setup.ts`（後片付け・待ち時間 5 秒） |
| API | MSW で応答を返す（`src/test/server.ts`）。未定義の通信はエラーにする（`onUnhandledRequest: 'error'`） |
| 相対 URL | 画面のコードは `/api/...` の相対 URL で通信する。Node の fetch は相対 URL を扱えないため、テストでだけ `fetch` / `Request` を包んで jsdom の location を基準に補う |
| 画面の描画 | `src/test/renderApp.tsx` が、実際のルート定義（`app/routes.tsx`）・`AuthProvider`・`ToastProvider`・TanStack Query を組み合わせ、メモリ上のルーターで指定 URL の画面を表示する。ログイン状態は refresh API の応答で決める（`signedInAs`） |
| タイムゾーン | テストは `Asia/Tokyo` で実行する（`vite.config.ts` の `test.env.TZ`） |
| 観点 | 表示、操作、フォーム、検証メッセージ、API の成功・失敗、読み込み中、空状態、エラー（再試行・問い合わせ番号）、権限（閲覧者・アーカイブ中・システム管理者メニュー）、XSS、トークンの保持 |

## 6. E2E テスト

| 項目 | 内容 |
|---|---|
| 構成 | `frontend/task-manager-e2e`（Playwright、Chromium、`ja-JP`、`Asia/Tokyo`） |
| 前提 | 空の DB に初期システム管理者だけがいる状態で、Web と API が起動していること（シナリオは順に実行し、前のシナリオで作った利用者・プロジェクトを使う） |
| 接続先 | `TM_E2E_BASE_URL`（既定 `http://localhost:5173` = Vite 開発サーバー経由）。Docker 構成では `http://localhost:8080` |
| 初期管理者 | `TM_E2E_ADMIN_EMAIL` / `TM_E2E_ADMIN_PASSWORD`（`.env` の `TM_BOOTSTRAP_ADMIN_*` と同じ値を環境変数で渡す）。途中で設定する新しいパスワードは実行ごとに生成し、保存しない |
| シナリオ | E2E-01〜08（testing-strategy.md §9 に対応。test-coverage.md §5） |
| アクセシビリティ | `@axe-core/playwright` で WCAG 2.1 A / AA の serious・critical の違反がないこと |
| 記録 | 失敗時のトレース・スクリーンショット（`test-results/`、`playwright-report/`。Git 管理外） |

## 7. セキュリティテストの範囲

- 攻撃を模した入力は、文字列の検査（`<script>`、`' OR 1=1 --`、`../`、改ざんした JWT など）に限った。外部への送信や、検証に必要な範囲を超える攻撃コードは作っていない。
- 依存関係の脆弱性（`dotnet list package --vulnerable`、`npm audit`）、コンテナ設定（非 root、ポート非公開）は Phase 7 で確認する（testing-strategy.md §7 のとおり）。

## 8. 実行方法（Phase 6 で使う）

```bash
cd /Volumes/OutSSD/CSharp/TaskManager

# Backend（Domain は DB 不要。その他はローカルの MySQL を使う）
export TM_TEST_MYSQL_CONNECTION='Server=localhost;Port=3306;User ID=<DB を作成できる利用者>;Password=<パスワード>;SslMode=Required'
dotnet test --solution src/TaskManager.sln
# 一部だけ: dotnet test --project tests/TaskManager.Api.Tests

# Frontend
cd frontend/task-manager-web && npm run test

# E2E（DB を空にして初期管理者だけの状態にし、API と Web を起動してから）
cd frontend/task-manager-e2e
export TM_E2E_ADMIN_EMAIL=... TM_E2E_ADMIN_PASSWORD=...   # .env の TM_BOOTSTRAP_ADMIN_* と同じ値
npx playwright test
```

- `TM_TEST_MYSQL_CONNECTION` を設定しない場合は Testcontainers（Docker）を使う。2026-10-12 時点では Docker Desktop が応答しないため（docs/environment/issues.md I-01）、ローカルの MySQL を使う。
- テストの途中で強制終了した場合、`tm_test_` で始まる DB が残ることがある。`SHOW DATABASES LIKE 'tm\_test\_%'` で確認し、手動で削除する。

## 9. Phase 5 で確認したこと（テストの実行ではない）

| 確認 | 結果 |
|---|---|
| `dotnet build src/TaskManager.sln`（テストプロジェクトを含む、警告をエラー扱い） | エラー 0・警告 0 |
| `dotnet test --list-tests`（テストの検出のみ。実行しない） | Domain 133、Application 215、Infrastructure 53、API 586 件を検出 |
| Frontend `npm run typecheck` / `npm run lint`（テストファイルを含む） | エラー 0・警告 0 |
| Frontend `npx vitest list`（検出のみ） | 7 ファイル、60 定義 |
| E2E `npx tsc --noEmit` / `npx playwright test --list` | 型エラー 0、8 シナリオを検出 |

テストの成否は未確認（Phase 6 で実行し、結果を記録する）。

## 10. テストのために行った実装の変更

| 変更 | 理由 |
|---|---|
| ルート定義を `app/routes.tsx` に分け、`app/router.tsx` はそれを使ってブラウザ用のルーターを作るだけにした | 画面のテストで、同じルート定義をメモリ上のルーターで使うため（動作は変わらない） |
| `TestDataBuilder` の利用者作成で、共通パスワードのハッシュを使い回す | テストデータ作成の高速化（テストコードのみ） |

Phase 5 の開始時に見つけて直した不具合（実ブラウザでの確認による）は [docs/implementation/status.md](../implementation/status.md) §8.3 に記録した。

## 11. 設計（testing-strategy.md）からの変更

| ID | 設計 | 変更後 | 理由 |
|---|---|---|---|
| T-01 | DB を伴うテストは Testcontainers の MySQL 8.4 | 環境変数 `TM_TEST_MYSQL_CONNECTION` があれば既存の MySQL（今回はローカルの MySQL 8.0.46）を使い、なければ Testcontainers | Docker Desktop が応答しないため。Phase 5 の指示でローカル PC の MySQL を使う。8.0 と 8.4 の差は status.md §8.4 |
| T-02 | テストごとに DB を作り直すか TRUNCATE | アセンブリで DB を共有し、データを一意にして分離。DB 全体に依存するテストだけ専用 DB | 速度。並行実行しても干渉しない |
| T-03 | E2E は Docker Compose の全体構成で実行 | 接続先を環境変数で切り替え、既定は Vite 開発サーバー + ローカルの API + ローカルの MySQL | Docker が使えないため。Docker 復旧後は `TM_E2E_BASE_URL=http://localhost:8080` で同じテストを実行できる |
| T-04 | OD-26（OpenAPI 応答検証のライブラリ）は Phase 3 で選定 | 自作の最小検証器 | 依存を増やさないため。対応する構文は §3.6 |
| T-05 | 全エンドポイント × 呼び出し元の期待値表 | 期待値は「アクセス区分 × 呼び出し元」の規則から計算し、操作ごとの要求の作り方と区分だけを表に書く | 488 ケースの期待値を手書きすると誤りやすいため。規則は authentication-authorization.md §6 に対応 |
| T-06 | 1MB を超える JSON → 413 | 自動テストは未作成。Phase 6 で実サーバーに対して確認済み（test-results.md §6）。添付の上限超過はフォームの制限で TestServer でも再現できるため API テストを追加した | TestServer は Kestrel の本文サイズ上限を適用しないため |
| T-07 | 行カバレッジの測定（Domain + Application 80%） | 未設定 | カバレッジ測定の拡張（Microsoft.Testing.Extensions.CodeCoverage 等）は未導入（test-results.md §8 O-03） |

## 12. 既知の制約・未対応

- 全 API 共通のレート制限（300 回/分）、アップロード（20 回/分）・検索（60 回/分）の個別制限は、テストを作っていない（ログインの制限のみ）。
- 定期メンテナンス（期限切れセッション・古いお知らせ・孤立ファイルの削除）は、テストを作っていない。
- Docker 構成（nginx の中継・Host の受け渡し・読み取り専用ルート）は専用の自動テストはない。Phase 6 で E2E を `http://localhost:8080` に向けて実行し、構成を手動で確認した（test-results.md §5）。
- E2E は前のシナリオの結果を使うため、途中のシナリオだけを単独で実行できない。
- （Phase 7 で確認する事項）API は `X-Forwarded-For` をどの送信元からも受け付ける設定（`KnownProxies` / `KnownIPNetworks` を空にしている）。nginx 経由（API のポートを公開しない構成）では最後の値が nginx の付けた実際の送信元になるが、API を直接公開すると送信元 IP を偽ってレート制限を回避できる。テストではこの設定に依存しないよう、専用ヘッダーで送信元 IP を指定した（§3.4）。
