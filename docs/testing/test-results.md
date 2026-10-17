# テスト実行結果（Phase 6）

- 実行日: 2026-10-12（14:58〜17:00 JST）
- テストの構成: [test-design.md](test-design.md)、テストの一覧: [test-coverage.md](test-coverage.md)
- 本書の「成功」は、記載したコマンドを実際に実行して得た結果だけを指す。
- Phase 8（完成確認）の最終実行: Backend 994 / 994（ローカル MySQL で 2 回・MySQL 8.4 で 1 回）、Frontend 107 / 107、E2E 8 / 8（Docker 構成）。docs/completion/final-checklist.md §8
- Phase 7（セキュリティ）でテストを追加・再実行した結果（Backend 994 / 994、Frontend 107 / 107、E2E 8 / 8）は [docs/security/security-review.md](../security/security-review.md) §5 に記録した。

## 1. 最終結果

| 区分 | コマンド | 環境 | 最終結果 |
|---|---|---|---|
| Backend ビルド | `dotnet build src/TaskManager.sln` | ローカル | 成功（警告 0・エラー 0） |
| Frontend ビルド | `npm run build`（+ `npm run typecheck`、`npm run lint`） | ローカル | 成功（型エラー 0・lint 警告 0） |
| Domain | `dotnet test --project tests/TaskManager.Domain.Tests` | DB なし | **133 / 133 成功** |
| Application | `dotnet test --project tests/TaskManager.Application.Tests` | ローカル MySQL 8.0.46 | **215 / 215 成功** |
| Infrastructure（EF Core / MySQL） | `dotnet test --project tests/TaskManager.Infrastructure.Tests` | ローカル MySQL 8.0.46 | **53 / 53 成功** |
| API（認証・認可・IDOR・契約・入力検証・セキュリティ） | `dotnet test --project tests/TaskManager.Api.Tests` | ローカル MySQL 8.0.46 + TestServer | **587 / 587 成功** |
| Backend 全体（回帰） | `dotnet test --solution src/TaskManager.sln` | ローカル MySQL 8.0.46 | **988 / 988 成功** |
| Backend 全体（MySQL 8.4） | 同上（`TM_TEST_MYSQL_CONNECTION` なし = Testcontainers） | Testcontainers `mysql:8.4.11` | **988 / 988 成功** |
| Frontend（単体・部品・画面） | `npx vitest run` | jsdom + MSW | **101 / 101 成功**（最終確認で 3 回連続） |
| E2E（ローカル構成） | `npx playwright test` | Vite 開発サーバー + API（`dotnet run`）+ ローカル MySQL 8.0.46 | **8 / 8 成功** |
| E2E（Docker 構成） | `TM_E2E_BASE_URL=http://localhost:8080 npx playwright test` | Docker Compose（nginx + API + MySQL 8.4.11） | **8 / 8 成功** |
| Docker | `docker compose up -d --build` ほか（§5） | Docker Desktop（Engine 29.5.2、Compose 5.1.4） | 確認済み（§5） |
| Swagger / OpenAPI | 実行中の API に対する確認（§6） | ローカル API（Development） | 確認済み（§6） |

未解決の重大なテスト失敗はない。残る課題（重大度は低〜中）は §8。

## 2. 実行環境

| 項目 | 内容 |
|---|---|
| OS | macOS 15.8.1（24H32）、x86_64 |
| .NET | SDK 10.0.401（global.json: 10.0.400 + latestFeature）、テスト基盤 Microsoft.Testing.Platform + xUnit v3 4.0.1 |
| Node.js | 24.10.0、Vitest 5.0.3、Playwright 1.64.0（Chromium 1248） |
| MySQL（ローカル） | 8.0.46（Homebrew、`brew services start mysql@8.0`）。接続情報は環境変数 `TM_TEST_MYSQL_CONNECTION` でのみ渡した（値は記録しない） |
| MySQL（コンテナ） | `mysql:8.4.11`（Testcontainers と Docker Compose） |
| Docker | Docker Desktop（Engine 29.5.2、Compose 5.1.4）。§5.1 のとおり再起動して復旧 |
| E2E の初期管理者 | `.env` の `TM_BOOTSTRAP_ADMIN_*` を環境変数 `TM_E2E_ADMIN_*` に渡した（値は記録しない） |

## 3. 実行の記録（Backend）

| # | 時刻 | コマンド | 結果 | 失敗 | 原因 | 修正 | 再テスト |
|---|---|---|---|---|---|---|---|
| B-1 | 14:59 | `dotnet build src/TaskManager.sln` | 成功（警告 0） | — | — | — | — |
| B-2 | 15:00 | Domain テスト | 133 / 133 成功 | — | — | — | — |
| B-3 | 15:02 | Infrastructure テスト（ローカル MySQL） | 53 / 53 成功 | — | — | — | — |
| B-4 | 15:05 | Application テスト（ローカル MySQL） | 215 / 215 成功 | — | — | — | — |
| B-5 | 15:10 | API テスト | **547 / 586 成功（39 件失敗）** | 下の R-01〜R-03 | 同左 | 同左 | B-6 |
| B-6 | 15:25 | API テスト（R-01・R-02 修正後） | **584 / 586 成功（2 件失敗）** | R-03（仕様との照合後に方針を決定） | 同左 | 同左 | B-7 |
| B-7 | 15:35 | API テスト（R-03 修正後） | 586 / 586 成功 | — | — | — | — |
| B-8 | 16:15 | API テスト（R-09 を再現するテストを追加） | 新しいテストが失敗（期待 413、実際 400） | R-09 | 同左 | 同左 | B-9 |
| B-9 | 16:20 | 同上（R-09 修正後） | 1 / 1 成功 | — | — | — | B-10 |
| B-10 | 16:30 | Backend 全体の回帰（ローカル MySQL） | 988 / 988 成功 | — | — | — | — |
| B-11 | 16:45 | Backend 全体（Testcontainers、MySQL 8.4.11） | 988 / 988 成功 | — | — | — | — |

### R-01 検証エラー等の Content-Type が `application/json` になる（21 件）

- 失敗: `ValidationApiTests` の 400 応答で `application/problem+json` を期待したが `application/json`。
- 原因: 全体に付けた `[Produces("application/json")]` フィルターが、モデル検証・認可フィルターで返す `ObjectResult` の Content-Type を上書きしていた。api-design.md §1.4（エラーは `application/problem+json`）に反する実装の不具合。
- 修正: `ProblemResults.Create` を、応答を直接書き込む `IActionResult` に変更（`src/TaskManager.Api/Errors/ProblemResults.cs`）。認可マトリクスの全エラー応答で Content-Type を検査するよう、テストも強化した。
- 再テスト: B-6 で解消。

### R-02 ログイン・トークン更新の 401 が OpenAPI に定義されていない（17 件）

- 失敗: 認可マトリクスで「Login の応答 401 は OpenAPI 文書に定義されていません」。
- 原因: api-design.md #1・#2 は 401（`auth.invalid_credentials` / `auth.locked` / `auth.session_expired`）を定めているが、匿名の操作に 401 を付けていなかった（契約の記載漏れ）。
- 修正: `AuthController` の Login・Refresh に `[ProducesResponseType(401)]` を追加し、ビルド時生成の OpenAPI を確認して（差分は 401 の 2 か所のみ）スナップショット `docs/api/openapi.v1.json` と Frontend の型（`schema.d.ts`）を更新。
- 再テスト: B-6 で解消。

### R-03 一時パスワード状態のトークンを付けると、匿名の操作（ログイン・システム情報）が 403 になる（2 件）

- 失敗: テストは「誰でも呼べる操作は 200 / 401」を期待、実装は 403 `auth.password_change_required`。
- 仕様との照合: authentication-authorization.md §3.3 は「pwchg=1 のトークンは /auth/password・/auth/logout・/auth/refresh・GET /me 以外を 403」と書いており、文字どおりなら実装が正しい。いったんテストの期待値を仕様に合わせたところ（B-6）、今度は 403 が OpenAPI の契約（ログイン・システム情報は `security: []` で 403 なし）にないため失敗した。
- 判断: 匿名で呼べる操作はトークンなしで誰でも呼べるため、トークンの有無で応答を変えても安全上の効果がなく、契約とも矛盾する。§3.3 は「ログインが必要な API」を対象にした記述と解釈し、**匿名で呼べる操作は対象外**と明確化した（設計書を更新）。
- 修正: `PasswordChangeRequiredFilter` が `[AllowAnonymous]` の操作を対象外にする。authentication-authorization.md §3.3 を更新。テストの期待値は元の「誰でも呼べる操作は呼び出し元によらず同じ」に戻した。
- 再テスト: B-7 で解消。

### R-09 11MB を超える添付が 413 ではなく 400 になる

- 発見: §6 の実サーバーでの確認（12MB のファイルで 400 `request.malformed`）。
- 再現テスト: `SecurityApiTests.要求の上限を超えるアップロードは413_attachment_too_large` を追加し、失敗を確認（B-8）。
- 原因: フォームの読み込みで上限を超えたことが「モデル検証のエラー（メッセージのみ）」として扱われ、形式エラーに変換されていた。api-design.md #50 は 413 `attachment.too_large`。
- 修正: `ValidationProblemFactory` で上限超過を検出して 413 `attachment.too_large` を返す。
- 再テスト: B-9・B-10・B-11 で成功。実サーバーでも 413 を確認（§6）。

## 4. 実行の記録（Frontend・E2E）

| # | 時刻 | コマンド | 結果 | 失敗 | 原因 | 修正 | 再テスト |
|---|---|---|---|---|---|---|---|
| F-1 | 15:00 | `npm run build` | 成功 | — | — | — | — |
| F-2 | 15:40 | `npx vitest run` | **71 / 100 成功（29 件失敗）** | R-04 | 同左 | 同左 | F-3 |
| F-3 | 15:45 | 同上 | **90 / 100 成功（10 件失敗）** | R-05・R-06・R-07 | 同左 | 同左 | F-4 |
| F-4 | 15:50 | 同上 | 100 / 100 成功 | — | — | — | — |
| E-1 | 15:55 | E2E（ローカル構成） | 0 / 8（E2E-01 で失敗、以降未実行） | R-08 | 同左 | 同左 | E-2 |
| E-2〜E-10 | 16:00〜16:12 | 同上（修正のたびに空の DB から再実行） | E2E-01 → 02 → 03 → 04 → 05 の順に通過 | R-10〜R-15 | 同左 | 同左 | E-11 |
| F-5 | 16:08 | `npx vitest run`（R-13 の回帰テスト追加後） | 101 / 101 成功 | — | — | — | — |
| E-11 | 16:13 | E2E（ローカル構成） | **8 / 8 成功** | — | — | — | — |
| E-12 | 16:42 | E2E（Docker 構成、`http://localhost:8080`） | **8 / 8 成功** | — | — | — | — |
| F-6 | 16:55 | `npx vitest run`（最終確認） | 100 / 101（1 件失敗） | R-16 | 同左 | 同左 | F-7 |
| F-7 | 16:58 | 同上を 3 回 | 3 回とも 101 / 101 成功 | — | — | — | — |

### R-04 API の呼び出しがすべて通信エラーになる（29 件。テスト基盤の不具合）

- 原因: 共通設定（`setup.ts`）が `api/client.ts` を先に読み込んだため、openapi-fetch が MSW を有効にする前の `fetch` を保持し、相対 URL の補正も効かなかった。
- 修正: MSW の開始と `fetch` の補正を、最初に読み込む `src/test/environment.ts` に分けた（`vite.config.ts` の `setupFiles` の順序）。

### R-05 トークン更新の結果が後の呼び出しに使い回される（3 件。アプリの不具合）

- 原因: `refreshSession` が完了した Promise を `setTimeout(0)` まで保持しており、その間の呼び出しに古い結果を返していた（トークンは設定されない）。ログアウト直後などに起こり得る。
- 修正: 完了時に直ちに外す（`src/api/client.ts`）。

### R-06 表とスマートフォン用カードの両方に同じリンクがある（4 件。テストの不具合）

- 原因: 表示の切り替えは CSS で行う。jsdom は CSS を適用しないため両方が見つかった。
- 修正: テストで表の中に限定して探す（期待する内容は変えていない）。

### R-07 タスク追加ダイアログを開くと画面全体がエラーになる（3 件。テスト環境の不足）

- 原因: jsdom に `CSS.escape` がない（react-aria のダイアログが使う。ブラウザには標準である）。
- 修正: テスト環境に `CSS.escape` を補う（`src/test/environment.ts`）。

### R-08 E2E の初期管理者のパスワード変更が拒否される（テストデータの不具合）

- 原因: テストが生成したパスワードにメールアドレスのローカル部（`admin`、`editor-…`）が含まれ、パスワードポリシー（正しい動作）で拒否された。
- 修正: メールアドレス・表示名を含まないパスワードを生成する。

### R-10 メンバー追加の利用者検索欄にラベルが関連付いていない（アプリの不具合・アクセシビリティ）

- 原因: react-aria の ComboBox が入力欄の id を管理するため、`<label htmlFor="member-search">` が入力欄を指していなかった（スクリーンリーダーで欄の名前が「利用者（名前・メールで検索）」にならない）。
- 修正: react-aria の `Label` で関連付けた（`ProjectMembersPage.tsx`）。

### R-11 メンバー追加で候補を選んでも「追加する」が押せないことがある（アプリの不具合）

- 原因: 候補を選ぶと入力欄の文字が変わり、その変更通知で選択が解除されていた。また検索語が変わると候補一覧が一時的に空になり、選択が外れていた。
- 修正: 選んだ人の表示名と同じ入力では選択を保つ。選んだ人は一覧から外れても候補に残す（`ProjectMembersPage.tsx`）。

### R-12 E2E の待ち合わせ不足（テストの不具合、複数）

- タスクの画面への移動完了を待たずに URL を読んだ、ログインの完了を待たずに別の URL を開いた（セッションがない状態で読み込まれた）、保存の完了を待たずにログアウトした。
- 修正: 画面の移動・ログイン完了・「保存しました」の表示を待つ。

### R-13 詳細画面で続けて 2 つの項目を変えると、先の変更が失われる（アプリの不具合）

- 発見: E2E-02 で担当者が保存されなかった。DB を確認すると期限だけが保存され、担当者の履歴がなかった。
- 原因: 詳細画面の保存は、画面に表示中の版（version）を使っていた。1 件目の保存の応答前に 2 件目を変更すると、同じ版で 2 回送り、自分の変更同士で 409（競合）になって一方が失われた。
- 修正: 保存を 1 件ずつ順に行い、直前の保存で返った版を次に使う（`TaskDetailPage.tsx`）。
- 回帰テスト: `TaskDetailPage.test.tsx` を追加。**修正前のコードで失敗し、修正後に成功する**ことを確認した（素早い連続操作で送る版が 3 → 4 になること）。

### R-14・R-15 E2E のあいまいな指定（テストの不具合）

- 「未読」のバッジがベルとメニューの 2 か所にある、添付のファイル名が「◯◯ を削除」ボタンの名前にも含まれる。
- 修正: 対象を特定する指定にした（期待する内容は変えていない）。

### R-16 最初の画面テストが待ち時間内に表示されないことがある（テスト環境、不安定）

- 原因: 画面は遅延読み込みのため、初回の表示に Testing Library の既定の待ち時間（1 秒）を超えることがある。
- 修正: 待ち時間を 5 秒にした（`configure({ asyncUtilTimeout: 5000 })`）。確認内容は変えていない。修正後 3 回連続で成功。

## 5. Docker

### 5.1 Docker Desktop の復旧

- 状況: 10/8 から Docker Desktop の内部 API が応答しない状態（`docker info` 無応答、`/_ping` がタイムアウト、仮想マシンのプロセスなし）。docs/environment/issues.md I-01。
- 対応: 利用者の了承を得て、Docker Desktop のプロセスを強制終了し再起動した（root 権限の補助プロセス `com.docker.vmnetd` は終了していない）。40 秒で応答が戻り、`docker pull mysql:8.4.11` も完了した。
- その後、1 回だけ API が一時的に 500 を返したが、再試行で回復した（原因不明。§8 O-06）。

### 5.2 実行の記録

| # | 時刻 | コマンド | 結果 | 原因 | 修正 |
|---|---|---|---|---|---|
| D-1 | 16:20 | `docker compose up -d --build` | **失敗**: api イメージのビルドで CA1711・CA1000（コード分析）がエラー | R-17: Dockerfile が `.editorconfig` をコピーしておらず、ローカルと異なる分析設定でビルドしていた | `docker/api/Dockerfile` で `.editorconfig` もコピーする |
| D-2 | 16:33 | 同上 | **失敗**: db が unhealthy。ログに `01-create-users.sh: Permission denied`。DB 利用者が作られていない | R-18: 公式イメージのエントリポイントは「実行可能」と判定した .sh を実行するが、Docker Desktop のバインドマウントでは実行権限のない 644 のファイルも実行可能と判定され、実行に失敗した（スクリプトは source される前提だった） | スクリプトに実行権限を付け、source でも別プロセスでの実行でも動くようにした（実行時は MYSQL_PWD 環境変数で root 接続。パスワードはコマンドラインに出さない）。`docker compose down -v` で DB を作り直した |
| D-3 | 16:38 | 同上 | 成功（db healthy → migrate 正常終了 → api healthy → web healthy） | — | — |

### 5.3 確認結果（D-3 の環境）

| 確認 | 方法 | 結果 |
|---|---|---|
| コンテナの起動 | `docker compose ps -a` | db: healthy、migrate: Exited (0)、api: healthy、web: healthy |
| MySQL の状態 | ヘルスチェック、初期化ログ | healthy。初期化スクリプトが実行され、エラーなし |
| DB 利用者と権限 | `mysql.user`、`SHOW GRANTS` | `taskmanager_app`（taskmanager_db への SELECT / INSERT / UPDATE / DELETE のみ）、`taskmanager_migrator` を作成 |
| マイグレーション | migrate のログ、`__EFMigrationsHistory` | `InitialSchema` を適用。表 14 + 履歴表 |
| Backend のヘルス | コンテナ内 `curl /health/ready` | `Healthy`（DB 接続を含む） |
| Frontend | `http://localhost:8080/`、`/healthz` | 200、`ok` |
| API との接続（nginx 経由） | `GET /api/v1/system/info`、`GET /api/v1/me` | 200（名前と版）、401（未ログイン） |
| 公開ポート | `docker compose ps` | web の 8080 のみ。db・api は公開なし |
| 非 root | `id` | api: uid 1654（app）、web: uid 101（nginx） |
| 読み取り専用ルート（OD-29） | api で `/app` に書き込み | `Read-only file system`。`/tmp`（tmpfs）は書き込み可 |
| Swagger（本番構成） | `/swagger/index.html`、`/openapi/v1.json` | 公開されない（401。§8 O-02） |
| 画面の一連の操作 | E2E 8 シナリオ（E-12） | 8 / 8 成功。画面の再読み込みでセッションが保たれる（nginx が Host をポート付きで渡す Phase 5 の修正 F-02 を実機で確認） |

## 6. Swagger / OpenAPI

ローカルの API（`dotnet run`、Development、Swagger 有効）に対して確認した。

| 確認 | 方法 | 結果 |
|---|---|---|
| Swagger UI | ブラウザ（Playwright）で `/swagger/index.html` | 表示される。タイトル「TaskManager API」、操作 61 件、タグ 17 件（説明は日本語） |
| OpenAPI 文書 | `GET /openapi/v1.json` | 実行中の API の文書とコミット済みのスナップショットが一致（`servers` を除く） |
| エンドポイント・HTTP メソッド | 認可マトリクス（全 61 操作 × 8 呼び出し元）、文書の構成テスト | 全操作のメソッド・パスで、定義した状態コードが返り、本文がスキーマに一致（API テスト 587 件で成功） |
| Request スキーマ | 文字数・必須（`version`）・列挙・日付の検査（`ValidationApiTests`、`OpenApiDocumentTests`） | 成功 |
| Response / Error スキーマ | 全応答を `OpenApiContract.AssertResponseAsync` で検査。エラーは `application/problem+json`（R-01 の修正後） | 成功 |
| 認証 | 文書: `bearerAuth`（HTTP Bearer）と `refreshCookie`。匿名は Login と GetSystemInfo のみ。Swagger UI の Authorize にトークンを入れて `GET /api/v1/me` を実行 | 200 が返ることを確認（未認証では 401） |
| 認可 | 認可マトリクス、IDOR テスト | 成功 |
| 秘密情報 | 文書の文字列検査（署名鍵・接続文字列・パスワード・MySQL・内部パス） | 含まれない |
| 本文サイズの上限（Phase 5 の T-06） | 実サーバーに 1MB を超える JSON、10MB + 1 バイト、12MB の添付を送信 | JSON: 413。10MB + 1: 413 `attachment.too_large`。12MB: **400（R-09）→ 修正後 413** |

## 7. Phase 5 の制約事項の扱い

| Phase 5 の記録 | Phase 6 の結果 |
|---|---|
| Docker が使えない（T-01・T-03） | 復旧し、Docker Compose の全体構成・Testcontainers（MySQL 8.4）の両方で確認した |
| 本文サイズの上限は TestServer では確認できない（T-06） | 実サーバーで確認した（§6）。添付の上限超過はフォームの制限で TestServer でも再現できたため、API テストを追加した |
| MySQL 8.4 での動作 | Testcontainers（8.4.11）で Backend 全体 988 件、Docker 構成で E2E 8 件が成功 |
| 行カバレッジ（T-07） | 未測定（§8 O-03） |

## 8. 未解決の課題

| ID | 課題 | 影響 | 再現 | 調査 | 現状 | 推奨する対応 |
|---|---|---|---|---|---|---|
| O-01 | 1MB を超える JSON の 413 のエラーコードが `attachment.too_large`（添付の上限と同じ） | 低。状態コードと文言（「送信できるデータの上限を超えています。」）は正しいが、コードが内容と合わない。OpenAPI の各操作にも 413 の記載がない | 1MB を超える JSON を任意の API に送る | `TaskManagerExceptionHandler` が本文サイズ超過を一律に `attachment.too_large` にしている | 未修正 | `request.too_large` を追加して使い分ける。サーバー全体の制限として api-design.md §1.5 に記載する（Phase 7 / 8） |
| O-02 | Swagger を無効にした本番構成で `/swagger`・`/openapi` が 404 ではなく 401 | 低。公開されないことは満たす。docs/environment/docker.md は「404」と記載 | 本番構成で `/swagger/index.html` を開く | ルートがないため既定の認可（ログイン必須）が先に 401 を返す | 未修正 | 文書の記載を 401 に直すか、無効時は 404 を返す（Phase 7 で判断） |
| O-03 | 行カバレッジ（Domain + Application 80%）を測定していない | 中。振る舞いのテストは揃っているが、NFR-13 の数値は未確認 | — | 測定の拡張（Microsoft.Testing.Extensions.CodeCoverage 等）が未導入 | 未対応 | 拡張を導入して測定する（Phase 7 または 8） |
| O-04 | ログイン以外のレート制限、定期メンテナンスのテストがない | 低〜中 | — | Phase 5 の test-coverage.md §3 | 未対応 | Phase 7 のセキュリティ確認で手動確認し、必要ならテストを追加する |
| O-05 | `X-Forwarded-For` をどの送信元からも受け付ける | 中（API を直接公開した場合のみ。Docker 構成では API のポートを公開しておらず、§5.3 で確認） | — | test-design.md §12 | 未対応 | Phase 7 で確認する |
| O-06 | Docker Desktop が不安定（長時間の無応答、再起動後に 1 回だけ 500） | 中（開発環境のみ。アプリの不具合ではない） | 不定 | I-01 | 再起動後は D-3・E-12・B-11 まで安定して動作 | 発生時は Docker Desktop を再起動する（docs/environment/issues.md I-01） |
| O-07 | E2E は前のシナリオの結果を使うため、空の DB から通しで実行する必要がある | 低（運用上の注意） | — | test-design.md §12 | 仕様どおり | 実行手順に従う（test-design.md §8） |

## 9. この Phase で変更したファイル

| 種類 | ファイル | 内容 |
|---|---|---|
| API | `src/TaskManager.Api/Errors/ProblemResults.cs` | R-01 |
| API | `src/TaskManager.Api/Controllers/AuthController.cs` | R-02 |
| API | `src/TaskManager.Api/Security/PasswordChangeRequiredFilter.cs` | R-03 |
| API | `src/TaskManager.Api/Errors/ValidationProblemFactory.cs` | R-09 |
| OpenAPI | `docs/api/openapi.v1.json`、`frontend/task-manager-web/src/api/schema.d.ts` | R-02 |
| Frontend | `src/api/client.ts`、`src/features/projects/ProjectMembersPage.tsx`、`src/features/tasks/TaskDetailPage.tsx` | R-05・R-10・R-11・R-13 |
| Docker | `docker/api/Dockerfile`、`docker/mysql/init/01-create-users.sh` | R-17・R-18 |
| テスト | `tests/TaskManager.Api.Tests/Authorization/*`、`Security/SecurityApiTests.cs` | R-01（検査の強化）・R-03・R-09 |
| テスト | `frontend/task-manager-web/src/test/*`、`vite.config.ts`、`*.test.tsx` | R-04・R-06・R-07・R-13（回帰テスト）・R-16 |
| テスト | `frontend/task-manager-e2e/tests/*` | R-08・R-12・R-14・R-15 |
| 設計 | `docs/design/authentication-authorization.md` §3.3 | R-03 |

テストを削除・無効化して成功させたものはない。期待値を変えたのは R-03（仕様と照合し、仕様の記述を明確化）のみで、それ以外はテストの待ち合わせ・要素の指定・テスト環境の修正である。

## 9. Phase 9（ガントチャート・ステータス別円グラフの追加）の実行結果（2026-10-16）

詳細（実行の記録・失敗と対応）は [docs/corrections/phase-09/verification-report.md](../corrections/phase-09/verification-report.md)。

| 区分 | 環境 | 結果 |
|---|---|---|
| Backend 全体（Domain 162・Application 232・Infrastructure 57・API 628） | ローカル MySQL 8.0.46 | 1,079 / 1,079 成功 |
| Backend 全体 | Testcontainers MySQL 8.4.11 | 1,079 / 1,079 成功 |
| Frontend | jsdom + MSW | 144 / 144 成功 |
| E2E（Docker 構成、9 シナリオ） | nginx + API + MySQL 8.4.11 | 9 / 9 成功 |
