# セキュリティ設計

CLAUDE.md §19 の各項目について、TaskManager の対策を定義する。Phase 7 ではこの表を検査項目として使う。Phase 1 で確認した Plane の弱点（analysis/open-questions.md の F-06〜F-11）を繰り返さないことを明示的な目標にする。

## 1. 脅威と前提

- 利用者は社内の人間だが、アカウントの乗っ取り・権限外の閲覧（他プロジェクト）・誤操作を想定する。
- 公開範囲: 社内ネットワークまたは TLS 終端の背後（インターネット公開は OD-18 で判断）。
- 守る資産: タスク・コメント・添付ファイルの内容、アカウント、監査記録。

## 2. 対策一覧

| # | 項目 | 対策 | 検証 |
|---|---|---|---|
| S-01 | Authentication | メール + パスワード、JWT（15 分）+ ローテーションするリフレッシュトークン（HttpOnly Cookie）。詳細 [authentication-authorization.md](authentication-authorization.md) | 認証テスト |
| S-02 | Authorization | 既定でログイン必須（FallbackPolicy）、SA 専用ポリシー、プロジェクト操作は `ProjectAccessGuard` で所属・ロール・アーカイブを判定 | 認可マトリクス |
| S-03 | IDOR | (1) プロジェクト配下は URL のプロジェクトから辿る、(2) 子リソースは親 ID を条件に含めて取得、(3) Request 内の参照 ID も同一プロジェクトか検証、(4) 非メンバーには 404、(5) 推測困難な UUID v7、(6) 権限に関わる項目を汎用更新 DTO に含めない | IDOR 専用テスト |
| S-04 | JWT | HS256 固定（`alg` 検証）、署名鍵 32 バイト以上（不足なら起動失敗）、`iss`・`aud`・`exp` 検証、時計の許容 30 秒、`security_stamp` 照合で即時失効、トークンに個人情報（メール）・プロジェクト権限を入れない | 改ざんトークンのテスト |
| S-05 | Password | PBKDF2（`PasswordHasher`）、12 文字以上 + 弱いパスワードの拒否、5 回失敗で 15 分ロック、存在しない利用者でもダミー検証で時間差をなくす、一時パスワードは応答で 1 回のみ・保存しない・ログに出さない | 認証テスト |
| S-06 | CORS | **同一オリジン構成のため CORS を有効にしない**（`AddCors` を使わない）。開発時は Vite のプロキシで同一オリジン化 | ヘッダー検査 |
| S-07 | CSRF | 通常 API は `Authorization` ヘッダー必須のため Cookie だけでは実行できない。Cookie を使う refresh / logout は `SameSite=Strict` + `Path=/api/v1/auth` + Origin / Sec-Fetch-Site 検証 | CSRF テスト |
| S-08 | XSS | (1) React の自動エスケープ、(2) `dangerouslySetInnerHTML` 禁止（lint で検出）、(3) Markdown は生 HTML を無効化し、リンクは `http:` / `https:` / `mailto:` / 画面内相対パスのみ許可・外部リンクは `rel="noopener noreferrer"`、画像は Markdown から読み込まない（OD-28）、(4) CSP（§3）、(5) お知らせの `linkPath` は `/` で始まる画面内パスのみ | XSS テスト |
| S-09 | SQL Injection | EF Core の LINQ / パラメータ化のみ。生 SQL は採番の 1 箇所で `ExecuteSqlInterpolated`（パラメータ化）。`LIKE` の `%` `_` `\` はエスケープ。並べ替えは許可リストから式を選ぶ（文字列を SQL に連結しない） | SQLi テスト、コードレビュー |
| S-10 | Input Validation | DataAnnotations（OpenAPI と同じ制約）+ Application の業務検証。文字列は前後空白除去・制御文字拒否（改行を許す項目を除く）。JSON の最大サイズ 1MB、最大深さ 32。未知のプロパティは無視 | 境界値テスト |
| S-11 | File Upload | 10MB 上限（nginx・Kestrel・アプリの三重）、拡張子の許可リスト + 先頭バイト検査、保存名は GUID（利用者入力をパスに使わない）、保存先は Web 公開されない領域、ダウンロードは常に `attachment` + `nosniff` + 検証済み Content-Type、1 タスク 20 件、レート制限 20 回/分 | 添付テスト |
| S-12 | Path Traversal | 保存パスは `ルート/yyyy/MM/{Guid}` をサーバーで生成。読み出し時も `Path.GetFullPath` 後にルート配下であることを確認。元ファイル名は表示と `Content-Disposition` のみに使用（RFC 5987 でエンコード） | 添付テスト |
| S-13 | Rate Limit | ASP.NET Core RateLimiter: ログイン 10 回/分/IP、refresh 30 回/分/IP、パスワード変更 5 回/分/利用者（Phase 7 で追加）、全 API 300 回/分/利用者、アップロード 20 回/分/利用者、検索 60 回/分/利用者。超過は 429 + `Retry-After`。IP は X-Forwarded-For を信頼するネットワーク（既定: プライベート IP と loopback）からの接続でのみ使う（Phase 7） | レート制限テスト |
| S-14 | Error Leakage | 例外は Problem Details に変換し内部情報を出さない。`DeveloperExceptionPage` は Development のみ。404 と権限なしを区別しない。サーバーヘッダー（`Server`、`X-Powered-By`）を出さない | エラーテスト |
| S-15 | Log | 構造化 JSON、`traceId` 付き。**出力しない**: パスワード、一時パスワード、トークン、Cookie、`Authorization` ヘッダー、リクエスト本文、添付の中身。メールアドレスは監査ログのみ。利用者入力は構造化フィールドとして出力（ログ偽装防止） | ログ検査テスト |
| S-16 | Secret | Secret は環境変数のみ（`.env` は Git 管理外、`.env.example` は説明とダミー値のみ）。appsettings に Secret を書かない。JWT 鍵・DB パスワードの既定値を持たない（未設定なら起動失敗）。Plane の値を一切使わない | Secret 走査（Phase 7） |
| S-17 | Docker | 非 root 実行（api: `app`、web: nginx-unprivileged、db: mysql。Phase 7 で db も非 root、全サービス `cap_drop: ALL`、db は不要な部品を除いた派生イメージ）、DB は内部ネットワークのみ、読み取り専用ルートファイルシステム（api は添付ボリュームと `/tmp` のみ書き込み可、OD-29）、`no-new-privileges`、ベースイメージは公式・バージョン固定、ヘルスチェック、不要なポートを公開しない | Phase 7 で確認 |
| S-18 | MySQL | アプリ用ユーザーは DML のみ、マイグレーション用ユーザーと分離、root は初期化のみでコンテナ内からのみ接続可（`MYSQL_ROOT_HOST=localhost`。Phase 7）、ポート非公開、`utf8mb4`、CHECK 制約・外部キーで整合性担保、バックアップ手順を README に記載 | 権限確認 |
| S-19 | Dependency | バージョン固定（lock ファイル）、`dotnet list package --vulnerable --include-transitive`・`npm audit` で High 以上 0 件を Phase 7 の合格条件にする。採用ライブラリはライセンス（MIT / Apache-2.0 / BSD）を確認 | Phase 7 |
| S-20 | Swagger 公開 | Swagger UI と `/openapi/v1.json` は `TM_ENABLE_SWAGGER=true` の時だけ有効（本番既定 false）。有効時も OpenAPI に Secret・内部情報・例の実データを含めない。Swagger UI からの API 実行には通常どおりトークンが必要 | 契約テスト §6.5 |
| S-21 | API Access Control | 全 Controller に既定の認証要求、匿名可は `login` のみ（refresh / logout は Cookie 検証）。新しいエンドポイントが認可マトリクスに未登録ならテスト失敗 | 認可マトリクス |
| S-22 | Session 管理 | リフレッシュトークンはハッシュ保存、再利用検知で系列失効、パスワード変更・停止で全失効、絶対期限 30 日 | 認証テスト |
| S-23 | 監査 | [requirements.md](requirements.md) REQ-AUD-01 を記録、1 年保持、秘密情報なし | 監査テスト |
| S-24 | 大量取得の防止 | `pageSize` 最大 100、自分のタスク最大 500、検索最大 20、利用者検索最大 20。Phase 9: ガントは期間 366 日・500 件・日付未設定 50 件、円グラフは DB で集計（最大 20 行） | 境界値テスト |
| S-25 | ガント・円グラフ（Phase 9） | (1) 3 つの参照 API はすべて `ProjectAccessGuard`（非メンバー 404）または「自分の担当・参加中のプロジェクト」で絞る、(2) 日程の変更は既存の更新 API（権限・TK-02・楽観的排他・アーカイブ中 409）のみ、(3) 期間・`projectKey` は DataAnnotations と Domain（`ScheduleWindow`）で検証、(4) SQL は LINQ のみ（文字列連結なし）、(5) 画面は React のエスケープ・SVG の属性のみ（`dangerouslySetInnerHTML` なし、インラインの `<style>` を挿入しない = CSP を変えない）、(6) 認可マトリクスに 3 操作を追加 | 認可マトリクス、IDOR テスト、入力検証テスト、E2E（CSP 違反 0） |

## 3. HTTP セキュリティヘッダー

web（nginx）で付与（API の JSON 応答にも API 側で同等のものを付与）:

| ヘッダー | 値 |
|---|---|
| `Content-Security-Policy` | `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `X-Frame-Options` | `DENY` |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` |
| `Strict-Transport-Security` | TLS 終端側で付与（`max-age=31536000`）。HTTP の開発環境では付与しない |
| `Cache-Control` | API: `no-store`（認証が必要な応答）、静的ファイル: ハッシュ付きファイル名は長期キャッシュ、`index.html` は `no-cache` |

Swagger UI はインラインスクリプトを使うため、`/swagger/` だけ CSP を緩める必要がある場合は、そのパスに限定した設定にする（Phase 3 で確認、OD-30）。

## 4. Phase 1 で確認した Plane の弱点への対応

| Plane の弱点（analysis） | TaskManager の対策 |
|---|---|
| F-06 API Token・Webhook secret の平文保存 | 外部 API キー・Webhook を持たない。リフレッシュトークンはハッシュ保存 |
| F-07 オブジェクトレベル認可の漏れ | S-03、認可マトリクステスト |
| F-08 エラー形式の不統一 | Problem Details に統一 |
| F-09 root 実行・healthcheck なし | S-17、全サービスに healthcheck |
| F-10 API テストが CI で未実行 | [testing-strategy.md](testing-strategy.md) §10、OD-27 |
| F-11 ユーザー列挙 | 存在しないメールと誤パスワードを同じ応答・同程度の時間に。メール確認用 API を作らない（ロック通知のみ例外: OD-20） |
| REST API の CSRF 無効 + CORS 全許可 | Bearer トークン + 同一オリジン + CORS 無効（S-06, S-07） |

## 5. Secret の一覧と管理

| Secret | 生成方法（例） | 保管 |
|---|---|---|
| `TM_JWT_SIGNING_KEY` | `openssl rand -base64 48` | `.env`（Git 管理外） |
| `MYSQL_ROOT_PASSWORD` / `TM_DB_APP_PASSWORD` / `TM_DB_MIGRATOR_PASSWORD` | `openssl rand -base64 24` | 同上 |
| `TM_BOOTSTRAP_ADMIN_PASSWORD` | 管理者が決める（初回のみ使用。作成後は `.env` から削除を推奨） | 同上 |

- `.gitignore` に `.env`、`*.pfx`、`appsettings.*.local.json` を含める。
- README に「`.env.example` をコピーし、すべての値を新しく生成すること」と記載する。

## 6. Phase 7 のレビュー結果

[docs/security/security-review.md](../security/security-review.md) を参照。
