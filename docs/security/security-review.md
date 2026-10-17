# セキュリティレビュー（Phase 7）

- 実施日: 2026-10-12〜13
- 基準: [docs/design/security-design.md](../design/security-design.md)（S-01〜S-24）、CLAUDE.md §19
- 方法: コード・設定の確認、依存関係とコンテナイメージの監査、実行中の API と Docker 構成（nginx → API → MySQL 8.4）への実際の要求による境界の確認、自動テスト（Phase 6 のテストに Phase 7 で追加したテストを含む）。
- 「確認済み」は、本書に記載した方法で実際に確認したことを表す。

## 1. 結論

| 重大度 | 件数 | 状態 |
|---|---|---|
| Critical | 0 | — |
| High | 1 | 修正済み（SEC-01: DB コンテナイメージの既知の脆弱性） |
| Medium | 3 | 修正済み 2（SEC-02、SEC-03）、利用者の対応待ち 1（SEC-04） |
| Low | 6 | 修正済み 5、受け入れ 1 |
| Informational | 9 | 記録のみ（受け入れ・Phase 8 で文書化） |

**未対応の Critical / High はない。** 修正後の回帰確認は §5。

## 2. 指摘事項

### SEC-01 DB コンテナイメージ（公式 mysql:8.4.11）に既知の重大な脆弱性 — High — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Dependency / Docker / MySQL |
| Evidence | Trivy 0.69.1（`aquasec/trivy` コンテナ）で `mysql:8.4.11` を走査: CRITICAL 1・HIGH 34。内訳: `gosu` に含まれる Go 標準ライブラリ 1.24.6（CVE-2025-68121 ほか）、MySQL Shell 同梱の Python ライブラリ（cryptography 46.0.5・pyOpenSSL 25.3.0・urllib3）、OS の openssl 3.5.8-1（CVE-2026-84782）。`mysql:8.4`（最新）も同じ 8.4.11 で、修正版の公式イメージはない |
| Impact | DB は内部ネットワークのみで公開していないため、直ちに悪用される可能性は低い。ただし openssl は mysqld の TLS が使う部品で、内部ネットワークから到達できる |
| Recommendation | 使わない部品を除き、修正版のある OS パッケージを更新した派生イメージを使う |
| Fix | `docker/mysql/Dockerfile` を追加（openssl / openssl-libs を 3.5.8-2 に更新、MySQL Shell と gosu を削除、`USER mysql` で非 root 実行）。`docker-compose.yml` の db をこのイメージ（`taskmanager-db:8.4.11`）に変更 |
| Verification | 派生イメージを Trivy で走査: **HIGH / CRITICAL 0 件**。DB を作り直して起動し healthy、E2E 8 / 8 成功（§5） |
| Status | 修正済み。公式イメージの更新時は派生イメージを作り直し、Trivy で再確認する |

### SEC-02 X-Forwarded-For をどの接続元からも信頼していた（レート制限の回避） — Medium — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Rate limiting / Authentication |
| Evidence | `Program.cs` の `ForwardedHeadersOptions` で `KnownIPNetworks` と `KnownProxies` を空にしていた。API に直接接続して `X-Forwarded-For` を毎回変えると、ログインのレート制限（IP あたり 10 回/分）の区分が変わる。修正前の設定で追加したテストを実行し、11 回目も 401（制限されない）であることを確認 |
| Impact | API を直接公開した場合、パスワードの総当たりの抑止（レート制限）を回避できる。Docker 構成では API のポートを公開していないため影響しない |
| Recommendation | X-Forwarded-For を信頼するのは中継（nginx）のネットワークからの接続だけにする |
| Fix | 既定で信頼するネットワークをプライベート IP（10/8、172.16/12、192.168/16、fc00::/7）と loopback に限定。設定 `ForwardedHeaders:KnownNetworks` で変更可能 |
| Verification | API テスト「信頼しない接続元からの X-Forwarded-For ではレート制限を回避できない」（修正前の設定で失敗・修正後に成功を確認）、「信頼するネットワークの中継からは X-Forwarded-For の利用者 IP で区別する」。Docker 構成でも nginx 経由で X-Forwarded-For を偽って 11 回ログイン → 429（回避できない） |
| Status | 修正済み |

### SEC-03 MySQL の root がネットワーク経由でログインできた — Medium — 修正済み

| 項目 | 内容 |
|---|---|
| Area | MySQL / Docker |
| Evidence | 公式イメージの既定（`MYSQL_ROOT_HOST` 未指定）では `root@'%'` が作られる。内部ネットワーク上の別コンテナ（api が乗っ取られた場合など）から root で接続を試みられる |
| Impact | api コンテナなど内部ネットワークの侵害時に、root の総当たり・最大権限の取得につながる |
| Recommendation | root はコンテナ内（ソケット）からのみ使う |
| Fix | `MYSQL_ROOT_HOST: localhost` を設定し、DB を作り直した |
| Verification | `mysql.user` は `root@localhost`・`taskmanager_app@%`・`taskmanager_migrator@%` のみ。内部ネットワークから root で接続 → `ERROR 1045 Access denied`。アプリ用利用者で `DROP TABLE` → `ERROR 1142 DROP command denied`（DML のみ） |
| Status | 修正済み。**既存の環境では DB の作り直し（`docker compose down -v`、データが消える）か、`DROP USER 'root'@'%'` の手動実行が必要**（Phase 8 で手順を README に記載） |

### SEC-04 プロジェクトの指示ファイルに DB のパスワードが平文で書かれている — Medium — 利用者の対応待ち

| 項目 | 内容 |
|---|---|
| Area | Secrets |
| Evidence | ルートの `CLAUDE.md` §11 と `.claude/skills/phase-05-test-code/CLAUDE.md` に、ローカル PC の MySQL の利用者名とパスワードが記載されている（いずれも Git 未追跡。リポジトリのコード・設定・ドキュメント・イメージには含まれないことを確認） |
| Impact | このまま Git にコミットすると、ローカル MySQL の資格情報が履歴に残る。同じパスワードを他で使っている場合は影響が広がる |
| Recommendation | ファイルから削除して環境変数・パスワードマネージャーで渡す。コミット済みにしてしまった場合はパスワードを変更する |
| Fix | 利用者が管理するファイルのため変更していない |
| Verification | `git ls-files` で未追跡を確認。リポジトリ内（`.claude` を除く）を走査し、他に含まれないことを確認 |
| Status | **利用者の対応待ち**（コミット前に削除すること） |

### SEC-05 画面内リンクの判定が「/\」「/<タブ>/」で始まるパスを許していた — Low — 修正済み

| 項目 | 内容 |
|---|---|
| Area | XSS / Open redirect |
| Evidence | Markdown のリンク（`safeUrl`）、ログイン後の移動先（`safeNextPath`）、お知らせのリンク（`Notice.Create`）は「`/` で始まり `//` で始まらない」だけを確認していた。ブラウザは `/\evil.example` と `/<タブ>/evil.example` を `//evil.example`（外部サイト）として解釈する（Chromium で確認） |
| Impact | 実際の経路では大きな影響はない: Markdown は react-markdown がバックスラッシュを `%5C` に符号化するため同じオリジンに留まる、お知らせのリンクはサーバーが生成する値のみ、ログイン後の移動はクライアント内の遷移。ただし判定関数自体は不十分だった |
| Fix | 2 文字目の `/` `\`、空白・制御文字を含むパスを拒否する判定（`isSafeInternalPath`）に統一。Domain の `Notice.Create` も同様に拒否 |
| Verification | Frontend の回帰テスト（修正前の判定で 6 件失敗・修正後に成功を確認）、Domain テスト（`/\…`、`/<タブ>/…`、空白を含むパスを拒否） |
| Status | 修正済み |

### SEC-06 パスワード変更にレート制限がなかった — Low — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Authentication / Rate limiting |
| Evidence | `POST /api/v1/auth/password` は全体の制限（300 回/分）のみ。現在のパスワードの誤りはロックの対象外 |
| Impact | アクセストークンを盗まれた場合、15 分の有効期間内に現在のパスワードを高速に総当たりされうる（12 文字以上・弱いパスワード禁止のため成功の可能性は低い） |
| Fix | 利用者ごとに 5 回/分の制限（`password-change` ポリシー）。当初はログインと同じ IP 単位の制限にしたが、E2E で同じ送信元からのログインと合わせて上限に達することを確認したため（同じ NAT 配下の利用者のログインにも影響する）、利用者単位に変更した |
| Verification | API テスト「パスワード変更は利用者ごとに 1 分 5 回までで 6 回目は 429」（送信元 IP を毎回変えても制限される） |
| Status | 修正済み |

### SEC-07 本文サイズ超過（JSON）のエラーコードが添付の上限と同じだった — Low — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Error handling |
| Evidence | 1MB を超える JSON に 413 `attachment.too_large`（Phase 6 の O-01） |
| Fix | 添付以外は `request.too_large`（api-design.md §1.5 に追加） |
| Verification | Docker 構成で 1MB を超える JSON をログイン API に送信 → 413 `request.too_large`。添付の上限超過は 413 `attachment.too_large`（API テスト） |
| Status | 修正済み |

### SEC-08 利用者管理の検索語に長さの上限がなかった — Low — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Input validation |
| Evidence | `GET /api/v1/users?q=` に上限がない（他の検索語は 80〜100 文字） |
| Impact | 非常に長い LIKE 検索で DB に負荷をかけられる（システム管理者のみ呼べるため影響は小さい） |
| Fix | 100 文字の上限を追加（OpenAPI・Frontend の型を更新） |
| Verification | OpenAPI のスナップショット比較テスト、API テスト全件 |
| Status | 修正済み |

### SEC-09 全コンテナで Linux ケーパビリティを外していなかった — Low — 修正済み

| 項目 | 内容 |
|---|---|
| Area | Docker |
| Evidence | `cap_drop` の指定なし（Docker の既定のケーパビリティが付与されたまま）。db は root で起動して gosu で切り替えていた |
| Fix | 全サービスに `cap_drop: [ALL]`。db も mysql 利用者で起動（SEC-01） |
| Verification | 各コンテナの `/proc/1/status` の `CapEff: 0000000000000000`、`docker inspect`: privileged=false、`no-new-privileges`、api・web は読み取り専用ルート、公開ポートは web の 8080 のみ。db 999・api 1654・web 101 の非 root |
| Status | 修正済み |

### SEC-10 アクセス制御まわりの設計上の受け入れ事項 — Low — 受け入れ

| 事項 | 内容と判断 |
|---|---|
| ログアウト後もアクセストークンは最長 15 分有効 | JWT は保持しない設計（メモリのみ）。パスワード変更・停止は security_stamp で即時失効する。社内利用の前提で受け入れ（authentication-authorization.md §2.1） |
| ロック中の応答で利用者の存在がわかる | 5 回失敗した場合のみ `auth.locked` が返る。ログインは IP あたり 10 回/分に制限。案内の分かりやすさを優先して受け入れ |
| 既知の利用者をロックできる（嫌がらせ） | 15 分で自動解除、管理者が解除可能。受け入れ |
| プロジェクトの管理者は全利用者を名前・メールで検索できる | 誰でもプロジェクトを作れるため、実質的に全利用者が社内名簿を見られる。社内ツールの名簿として受け入れ（検索は最大 20 件、停止中の利用者は出さない） |

## 3. 観点ごとの確認結果

| 観点 | 確認内容（根拠） | 結果 |
|---|---|---|
| Authentication | パスワードは ASP.NET Core Identity の `PasswordHasher`（PBKDF2-HMAC-SHA512、反復 100,000、ソルト付き）でハッシュのみ保存。12〜128 文字・弱いパスワード・メールや表示名を含むものを拒否。存在しない利用者でもダミー検証で時間差をなくす。5 回失敗で 15 分ロック。一時パスワードは作成・再発行の応答で 1 回だけ返す | 問題なし（テスト: AuthServiceTests、AuthApiTests、PasswordPolicyTests、SecurityPrimitiveTests） |
| Token / JWT | HS256 のみ許可（`ValidAlgorithms`）、署名鍵は Base64 で 32 バイト以上（不足・未設定なら起動しない）、`iss`・`aud`・`exp` 検証、許容 30 秒、有効期限 15 分、security_stamp の照合で即時失効。メールアドレスは含めない | 問題なし（改ざん・署名なし・期限切れ・別 audience / issuer・別の鍵・形式不正 → 401 のテスト） |
| Refresh / Logout | リフレッシュトークンは 32 バイトの乱数、SHA-256 のハッシュのみ保存、ローテーション、使用済みの再提示で系列ごと失効、絶対期限 30 日。Cookie は `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth`。ログアウトで系列を失効し Cookie を削除 | 問題なし（SEC-10 の受け入れを除く） |
| Credential handling | アクセストークンはメモリのみ（localStorage はサイドバーの開閉だけに使用）。ログイン失敗の応答は存在の有無で同じ | 問題なし（Frontend テスト: トークンをブラウザの保存領域に書かない） |
| Authorization | 既定でログイン必須（FallbackPolicy）、利用者管理はシステム管理者ポリシー、プロジェクト配下は `ProjectAccessGuard`（非メンバー 404・ロール不足 403・アーカイブ中 409）。一時パスワード状態は 403 | 全 61 操作 × 8 種類の呼び出し元（488 ケース）で確認（Phase 6・7 で成功） |
| IDOR / Object-level | 子リソースは URL のプロジェクト・親 ID を条件に取得。本文の参照 ID（ステータス・タグ・マイルストーン・担当者・親タスク）も同じプロジェクトか確認。他人宛てのお知らせは 404。ID は UUID v7 | 問題なし（IdorApiTests: B のデータが変化しないことも確認） |
| Ownership | コメントの編集は投稿者のみ、削除は投稿者か管理者。タスクの削除は作成者か管理者。添付の削除は投稿者か管理者 | 問題なし（認可マトリクス・Application テスト） |
| Input validation | DataAnnotations（OpenAPI と同じ制約）+ Domain の検証。数値の文字列表現を拒否（Strict）、列挙は文字列のみ、JSON の深さ 32、未知のプロパティは無視、一覧の `pageSize` 最大 100、並べ替えは許可リスト | 問題なし（ValidationApiTests、SEC-08 を修正） |
| Oversized request | Kestrel 1MB（添付は 11MB）、nginx `client_max_body_size 11m` | Docker 構成で 12MB → 413（nginx）、1MB 超の JSON → 413（API）。SEC-07 を修正 |
| SQL Injection | 生 SQL は採番の 1 箇所のみで `ExecuteSqlInterpolatedAsync`（パラメータ化）。その他は LINQ。並べ替えは許可リストから式を選ぶ。検索の `%` `_` `\` `'` は文字として扱う | 問題なし（コード走査、`' OR 1=1 --` などのテスト） |
| XSS | React の自動エスケープ、`dangerouslySetInnerHTML` なし（lint で禁止）、Markdown は生 HTML を無効化・リンクは http / https / mailto / 画面内パスのみ・画像は読み込まない。CSP: `script-src 'self'`・`object-src 'none'`・`frame-ancestors 'none'`（画面）、`default-src 'none'`（API） | SEC-05 を修正。E2E でコメントの `<script>` が実行されないことを確認 |
| CSRF | 通常の API は `Authorization` ヘッダーが必須（Cookie だけでは実行できない）。Cookie を使う refresh / logout は SameSite=Strict + Origin / Sec-Fetch-Site の検証 | 問題なし（別オリジン・Origin なし・cross-site → 403 のテスト） |
| CORS | `AddCors` を使っていない（同一オリジン構成）。nginx も CORS ヘッダーを付けない | 問題なし（コード走査、応答ヘッダーに `Access-Control-*` なし） |
| File upload | 拡張子の許可リスト + 先頭バイト検査、SVG・HTML・実行ファイルは不可、10MB・1 タスク 20 件・利用者ごと 20 回/分、保存名はサーバー生成（`yyyy/MM/{GUID}`）、保存先は公開されない領域、ダウンロードは常に `attachment` + `nosniff` + 検証済みの Content-Type、閲覧権限を確認 | 問題なし（Application・API・E2E のテスト、12MB → 413） |
| Path traversal | 元のファイル名は表示と Content-Disposition のみに使用（`../` やパス区切りは除去）。保存先は `Path.GetFullPath` 後にルート配下か確認し、`..`・絶対パスを拒否。nginx は `--path-as-is` の `/assets/../../../etc/passwd` に 400 | 問題なし（AttachmentStorageTests、SecurityApiTests、Docker 構成での確認） |
| Rate limiting | ログイン 10 回/分/IP、refresh・logout 30 回/分/IP、パスワード変更 5 回/分/利用者、アップロード 20 回/分/利用者、検索 60 回/分/利用者、全 API 300 回/分/利用者（未ログインは IP）。超過は 429 + Retry-After | SEC-02・SEC-06 を修正。ログイン・パスワード変更・X-Forwarded-For のテスト |
| Error leakage | 例外は Problem Details（`code`・`traceId`）に変換し、メッセージ・スタックトレース・SQL・内部パスを返さない。404 と権限なしを区別しない。`Server` は `nginx`（版は非表示）、`X-Powered-By` なし。DeveloperExceptionPage は使っていない | 問題なし（意図的な例外 → 500 のテスト、応答が OpenAPI のスキーマに一致し未定義の項目がないこと） |
| Logging | セキュリティイベントは種類・利用者 ID・プロジェクト ID・IP のみ（メール・パスワード・トークンなし）。EF Core の SQL ログは Warning 以上、パラメータの値は出さない（`EnableSensitiveDataLogging` なし）。要求の本文・ヘッダーを記録しない | 問題なし（ログにパスワード・アクセストークン・リフレッシュトークンが出ないテスト） |
| Secrets | `.env` は `.gitignore` 対象・`.dockerignore` 対象、`.env.example` は空欄、appsettings に Secret なし、Compose は `${VAR:?}` で未設定なら起動しない、JWT 鍵・DB パスワードの既定値なし、Frontend に環境変数（`import.meta.env`）の使用なし。Git の履歴は初期コミット（README のみ）。イメージの環境変数・履歴・`/app` に Secret の値なし。Plane の値・名前なし | SEC-04（利用者の対応待ち）を除き問題なし |
| Docker | 公開ポートは web の 8080 のみ（db・api は内部ネットワーク、開発用の上書きは 127.0.0.1 のみ）。privileged なし、全サービス非 root・`cap_drop: ALL`・`no-new-privileges`、api・web は読み取り専用ルート。イメージは公式・版を固定（ダイジェスト固定はしていない: SEC-INFO-04）。アプリのイメージ（api・web・migrate）は Trivy で HIGH / CRITICAL 0 件 | SEC-01・SEC-09 を修正 |
| MySQL | アプリ用（DML のみ）・マイグレーション用（DB 単位の全権）・root（ローカルのみ）を分離。接続は TLS 1.3（`SslMode=Required`）。ポートは公開しない。マイグレーションは専用コンテナが専用利用者で実行 | SEC-03 を修正。バックアップ手順は SEC-INFO-07 |
| Dependency | `dotnet list package --vulnerable --include-transitive`: 全プロジェクトで 0 件。`npm audit`（Web・E2E）: 0 件。コンテナ: §2 SEC-01 | 問題なし（修正後） |
| Swagger / OpenAPI | 本番構成（`TM_ENABLE_SWAGGER` 未指定）では `/swagger`・`/openapi` は公開されない（401）。開発時のみ有効で、開発用の上書きは 127.0.0.1 にのみ公開。文書に Secret・接続文字列・内部パス・例外名・実データなし。認証方式は bearerAuth と refreshCookie、匿名は Login と GetSystemInfo のみ。Swagger UI から API を実行するにはトークンが必要 | 問題なし（契約テスト、Docker 構成での確認） |
| API Access Control | 新しい操作が認可マトリクスに未登録ならテストが失敗する（operationId の一致検査） | 問題なし |

## 4. Informational（記録のみ）

| ID | 内容 | 対応 |
|---|---|---|
| SEC-INFO-01 | DB 接続は TLS 必須だが、サーバー証明書は検証していない（MySQL の自己署名証明書） | 内部ネットワークのみのため受け入れ。外部の DB を使う場合は `SslMode=VerifyCA` と CA 証明書を設定する |
| SEC-INFO-02 | HSTS・TLS は前段（社内 LB / リバースプロキシ）で設定する前提（OD-18）。web コンテナは HTTP のみ | Phase 8 の README に記載する |
| SEC-INFO-03 | API の応答に `X-Content-Type-Options`・`X-Frame-Options` が API と nginx の両方から付き、同じ値が 2 回出る | 値は同じで動作に影響しない。受け入れ |
| SEC-INFO-04 | ベースイメージはタグで版を固定しているが、ダイジェスト（`@sha256:`）では固定していない | 再現性を高める場合はダイジェストで固定する。受け入れ |
| SEC-INFO-05 | Swagger 無効時の `/swagger` は 404 ではなく 401（Phase 6 の O-02） | 公開はされない。docs/environment/docker.md の記載を修正済み |
| SEC-INFO-06 | `AllowedHosts: *`。CSRF の検証は Origin と Host を比較するが、ブラウザの要求では Host を偽れないため影響なし | 受け入れ |
| SEC-INFO-07 | DB のバックアップ・復元の手順が利用者向け文書にない（S-18） | Phase 8 で README に記載する（`mysqldump` を db コンテナ内でソケット接続の root で実行し、ボリュームの外へ保存） |
| SEC-INFO-08 | EF Core のマイグレーションの排他は MySQL のサーバー全体の名前付きロック（`GET_LOCK('__EFMigrationsLock', -1)`、待ち時間なし）を使う。Phase 7 の回帰テスト中にテストが止まり、当初は強制終了したテストプロセスの残りが原因と判断したが、Phase 8 で残りのプロセスがない状態でも再発した（ロックを取った接続がアイドルのまま、テストプロセスが CPU 0% で停止。MySQL プロバイダのマイグレーションのロック処理と、テストの並行実行の組み合わせで起きると判断。根本原因は特定できていない） | Phase 8 でテスト基盤を修正: 最初にプロセス内で 1 件ずつ行う対策を入れたが、ロックを取った接続がアイドルのまま .NET 側で止まる形で再発したため、テスト用 DB には `MigrateAsync` を使わず、同じマイグレーションから生成した SQL を直接適用する方式に変えた（`tests/TaskManager.Testing/MySqlTestServer.cs`）。修正後、Backend 全体を 2 回続けて実行して止まらないことを確認（final-checklist.md）。本番では migrate コンテナ（efbundle）が 1 回だけ実行するため、複数の同時実行は起きない |
| SEC-INFO-09 | 行カバレッジは未測定（Phase 6 の O-03） | Phase 8 で判断する |

## 5. 修正後の回帰確認

| 確認 | コマンド・環境 | 結果 |
|---|---|---|
| Backend ビルド | `dotnet build src/TaskManager.sln` | 警告 0・エラー 0 |
| Backend テスト全体 | `dotnet test --solution src/TaskManager.sln`（ローカル MySQL 8.0.46） | **994 / 994 成功**（Phase 6 の 988 件 + Phase 7 で追加した 6 件） |
| Frontend | `npm run typecheck`・`npx oxlint --deny-warnings`・`npm run build`・`npx vitest run` | 型エラー 0・lint 警告 0・ビルド成功・**107 / 107 成功** |
| E2E（Docker 構成） | DB を作り直して `docker compose up -d --build`（派生 DB イメージ・cap_drop・root はローカルのみ）→ `TM_E2E_BASE_URL=http://localhost:8080 npx playwright test` | **8 / 8 成功** |
| OpenAPI | スナップショット更新（`GET /api/v1/users` の `q` の長さのみの差分を確認） | 契約テストで一致 |
| 境界の確認（Docker 構成） | §2・§3 に記載した要求 | 記載のとおり |

### 5.1 Backend テストの記録

| # | 内容 | 結果 |
|---|---|---|
| 1 | 修正途中の回帰（パスワード変更を IP 単位の制限にした状態）で E2E を実行 | E2E-06 でログインが 429（同じ送信元のログインとパスワード変更が合わせて上限に達した）→ SEC-06 を利用者単位の制限に変更 |
| 2 | Backend 全体の実行が終わらない | 前の実行を止めた際に残ったテストプロセスがマイグレーションのロックを持ったままだった（SEC-INFO-08）。プロセスを終了し、テスト用 DB（`tm_test_` で始まるもののみ）を削除して再実行 |
| 3 | Backend 全体（最終） | 994 / 994 成功（Domain 136、Application 215、Infrastructure 53、API 590） |
| 4 | Frontend（最終） | 107 / 107 成功、型チェック・lint・ビルド成功 |
| 5 | E2E（Docker 構成、最終） | 8 / 8 成功 |

## 6. Phase 7 で変更したファイル

| ファイル | 指摘 |
|---|---|
| `docker/mysql/Dockerfile`（新規）、`docker-compose.yml` | SEC-01・SEC-03・SEC-09 |
| `src/TaskManager.Api/Program.cs` | SEC-02 |
| `src/TaskManager.Api/Security/RateLimitPolicies.cs`、`Controllers/AuthController.cs` | SEC-06 |
| `src/TaskManager.Api/Errors/TaskManagerExceptionHandler.cs`、`src/TaskManager.Application/Common/ErrorCodes.cs` | SEC-07 |
| `src/TaskManager.Api/Controllers/UsersController.cs`、`docs/api/openapi.v1.json`、`frontend/task-manager-web/src/api/schema.d.ts` | SEC-08 |
| `src/TaskManager.Domain/Notices/Notice.cs`、`frontend/task-manager-web/src/components/MarkdownView.tsx`、`src/features/auth/RouteGuards.tsx` | SEC-05 |
| テスト: `tests/TaskManager.Api.Tests/Auth/AuthApiTests.cs`、`tests/TaskManager.Domain.Tests/Tasks/TaskRelatedEntityTests.cs`、`frontend/task-manager-web/src/components/MarkdownView.test.tsx`、`src/utils/taskKey.test.ts` | 追加・修正 |
| 設計・環境: `docs/design/security-design.md`、`docs/design/api-design.md`、`docs/environment/configuration.md`、`docs/environment/docker.md` | 記載の更新 |
