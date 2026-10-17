# 認証・認可解析（Plane `preview`）

## 目的

Plane の認証（ログイン・セッション・トークン・パスワード）と認可（ロール・オブジェクトレベル制御）を把握し、TaskManager のセキュリティ設計（Phase 2）の材料にする。Plane の方式を採用することはここでは決定しない。

## 解析範囲と根拠

- `apps/api/plane/settings/common.py`（REST_FRAMEWORK、SESSION、CSRF、CORS、パスワード検証）
- `apps/api/plane/authentication/`（session.py、middleware/session.py、views/app/*.py、provider/credentials/*.py、utils/login.py、rate_limit.py）
- `apps/api/plane/api/middleware/api_authentication.py`、`api/rate_limit.py`
- `apps/api/plane/app/permissions/{base,workspace,project}.py`
- `apps/api/plane/db/models/{user,session,api}.py`
- `apps/web/services/api.service.ts`、`apps/web/store/user/base-permissions.store.ts`、`apps/web/components/account/auth-forms/*.tsx`
- 回帰テスト: `apps/api/plane/tests/contract/app/*_scope_app.py` 等

## 認証方式の全体像（確認済み）

| 利用者 | 方式 | 保存 |
|---|---|---|
| Web アプリ | Django セッション（Cookie `session-id`、HttpOnly） | DB テーブル `sessions`（`device_info` に UA / IP / ドメイン） |
| 管理画面 | 別 Cookie `admin-session-id`（パスに `instances` を含む場合に切替）、有効期限 3600 秒 | 同上 |
| 公開 API | `X-Api-Key` ヘッダー | `api_tokens.token`（平文） |
| 共同編集サーバー | ユーザー ID + Cookie を JSON トークンで受け取り API に転送 | なし |

- **JWT は使用していない**（PyJWT は依存にあるが、ログインセッションには使われていない。用途は未確認 → Q-AUTH-01）。
- リフレッシュトークンの仕組みはない（セッション Cookie の有効期限 `SESSION_COOKIE_AGE` 既定 604800 秒 = 7 日）。
- OAuth の access/refresh token は `accounts` テーブルに保存（外部プロバイダ連携用）。

## Login（メール + パスワード）

```text
[web] 認証フォーム
  → GET /auth/get-csrf-token/ で CSRF トークン取得
  → <form method=POST action=/auth/sign-in/> (email, password, csrfmiddlewaretoken, next_path)
[api] SignInAuthEndpoint.post  (Django View, CsrfViewMiddleware が有効)
  → @throttle_auth_redirect（既定 10/分、AUTHENTICATION_RATE_LIMIT）
  → インスタンス未設定ならエラー
  → email/password 必須、email 形式検証、小文字化
  → ユーザー不存在 → USER_DOES_NOT_EXIST
  → EmailProvider.authenticate → check_password
  → user_login: django.contrib.auth.login + session["device_info"]
  → 302 リダイレクト（next_path は get_safe_redirect_url で検証）
エラーはすべて 302 + クエリ文字列のエラーコード
```

- 事前に `POST /auth/email-check/` で「既存ユーザーか」「パスワード未設定か」を返す（`{"existing": true/false, ...}`）。**ユーザー列挙が可能な設計**（確認済み。サインイン失敗時もユーザー不存在を区別して返す）。
- 設定でメール + パスワード認証、Magic code、サインアップ、各 OAuth を個別に無効化可能（インスタンス設定）。

## Sign up

- `POST /auth/sign-up/`。既存メールは `USER_ALREADY_EXIST`。`ENABLE_SIGNUP` で制御。
- パスワード強度: `zxcvbn` スコア 3 未満は拒否（リセット時に確認、サインアップ時も同様と推定 → 有力）。Django の `AUTH_PASSWORD_VALIDATORS`（類似性、最小長、よく使われるパスワード、数字のみ）も設定。

## Magic code（メールワンタイムコード）

- 6 桁（`secrets.randbelow(900000)+100000`）。Redis に保存（TTL あり）。
- 再発行は 3 回まで、誤入力は 1 トークンあたり 5 回まで（Redis の原子的 INCR で並行要求にも対応）。

## Logout

- `POST /auth/sign-out/`。`User.last_logout_time` / `last_logout_ip` を記録（モデル列から推定 → 有力）。

## Password reset / change / set

- `POST /auth/forgot-password/` → メール送信（Celery `forgot_password_task`）。
- リセット URL: `/auth/reset-password/<uidb64>/<token>/`。トークンは Django `PasswordResetTokenGenerator`（有効期限 `PASSWORD_RESET_TIMEOUT` = 3600 秒）。
- `change-password`（ログイン中の変更）、`set-password`（OAuth / Magic code で作成されパスワード未設定のユーザー向け、`is_password_autoset`）。

## Email verification

- `User.is_email_verified` 列あり。メールアドレス変更時に検証コード送信（`/api/users/me/email/generate-code/` → `PATCH /api/users/me/email/`）。
- 新規登録時のメール検証フローは、Magic code ログイン自体が実質的な検証となっている（有力）。

## Token / Cookie / CSRF / CORS（確認済み）

| 項目 | 設定 |
|---|---|
| Session Cookie | `HttpOnly`、`Secure` は CORS 許可オリジンが全て https の場合のみ、`SameSite` は Django 既定（Lax） |
| CSRF Cookie | `HttpOnly`、`CSRF_TRUSTED_ORIGINS` = CORS 許可オリジン |
| REST API の CSRF | **無効**（`BaseSessionAuthentication.enforce_csrf` が何もしない）。SameSite=Lax と CORS 設定に依存 |
| 認証フォーム（/auth/） | CSRF 有効（フォームに `csrfmiddlewaretoken`） |
| CORS | `CORS_ALLOWED_ORIGINS` 未設定時は **全オリジン許可** + `CORS_ALLOW_CREDENTIALS = True` |
| Frontend の Token 保存 | なし（Cookie のみ、`withCredentials: true`） |

## Authentication failure

- REST: 401 → Frontend の axios インターセプタが `/?next_path=<現在パス>` へ `window.location.replace`。
- /auth/: 302 でエラーコード付きリダイレクト。エラーコード定義は `authentication/adapter/error.py`。

## 認可（Role / Permission）

### ロール（確認済み）

| 値 | 名前 | 範囲 |
|---|---|---|
| 20 | Admin | ワークスペース / プロジェクト |
| 15 | Member | 同上 |
| 5 | Guest | 同上 |

- ワークスペースロールとプロジェクトロールは独立（`WorkspaceMember.role` / `ProjectMember.role`）。両方とも `is_active` を持つ。
- インスタンス管理者は別系統（`license` アプリ、`InstanceAdmin`）。

### 判定方式

1. **デコレータ `@allow_permission(allowed_roles, level="PROJECT"|"WORKSPACE", creator=False, model=None)`**（`app/permissions/base.py`）
   - WORKSPACE: URL の `slug` のワークスペースで、役割が許可リストにある有効メンバーか。
   - PROJECT: URL の `project_id` のプロジェクトで、役割が許可リストにある有効メンバーか。**または** そのプロジェクトの有効メンバーであり、かつワークスペース Admin であれば許可。
   - `creator=True`: ワークスペースの有効メンバーで、対象（`kwargs["pk"]`）の `created_by` が自分なら役割に関係なく許可。
2. **DRF Permission クラス**（`ProjectEntityPermission` 等）: 参照系（GET）はメンバーであれば許可、更新系は Admin / Member。
3. **View 内の追加制御**: ゲストかつ `guest_view_all_features=False` の場合は自分が作成した作業項目のみ閲覧可能、など。

### 代表的な権限マトリクス（作業項目、確認済み）

| 操作 | Admin | Member | Guest | 作成者 |
|---|---|---|---|---|
| 一覧 | ○ | ○ | ○（設定により自分作成分のみ） | - |
| 作成 | ○ | ○ | × | - |
| 取得 | ○ | ○ | ○（同上） | ○ |
| 更新 | ○ | ○ | × | ○ |
| 削除 | ○ | × | × | ○ |
| 一括削除 | ○ | × | × | - |

ワークスペース: 作成は誰でも（`DISABLE_WORKSPACE_CREATION` で制限可）、更新は Admin、削除は Admin。プロジェクト作成はワークスペース Admin / Member。

### Object-level authorization（IDOR）

- 基本方針: URL の `slug` と `project_id` でメンバーシップを確認し、クエリを `workspace__slug` + `project_id` で絞る。
- **URL の `project_id` と、対象オブジェクト（`issue_id` 等）の所属が一致するかの確認が漏れていた事例が多数**、回帰テストとして残っている（確認済み）:

| テスト | 内容 |
|---|---|
| `test_issue_list_guest_scope_app.py` | ゲストが ID 指定で他人の作業項目を読めた（GHSA-32c7-84jc-4w67） |
| `test_sub_issue_cross_project_scope_app.py` | URL のプロジェクトと異なるプロジェクトのサブ作業項目を読めた・付け替えできた |
| `test_project_member_is_active_authz.py` | ゲストが `is_active=false` を送って管理者を無効化できた（GHSA-hpgm-9r34-c4x5） |
| `test_webhook_secret_key_scope_app.py` | Webhook の署名用シークレットが参照 API で漏えいしていた |
| `test_draft_to_issue_owner_scope_app.py` | 他人の下書きを作業項目に変換できた |
| `test_workspace_file_asset_project_scope_app.py` | プロジェクト非所属のメンバーがプロジェクトの添付ファイルを取得・更新・削除できた |
| `cycle/issue.py` のコメント | 他テナントの CycleIssue を自分のサイクルへ付け替えできた（GHSA-4w5x-wc9w-f47x） |

- 教訓: 「ロール確認」と「対象オブジェクトの所属確認」は別物であり、全エンドポイントで両方必要。Serializer が `fields="__all__"` で書き込み可能な列を広く開けていたことも原因になっていた。

## Frontend の権限表示制御（確認済み）

- `allowPermissions(allowedRoles, level, workspaceSlug?, projectId?)`（`store/user/base-permissions.store.ts`）で、ストアに保持した自分のロールを判定してボタン等を表示/非表示。96 コンポーネントで使用。
- あくまで表示制御であり、最終判定は Backend。

## レート制限（確認済み）

| 対象 | 既定 |
|---|---|
| 匿名（DRF AnonRateThrottle） | 30/分 |
| `asset_id` スコープ | 5/分 |
| 認証（サインイン・Magic code） | 10/分（`AUTHENTICATION_RATE_LIMIT`） |
| API Key | 60/分（`API_KEY_RATE_LIMIT`、トークン毎の `allowed_rate_limit` 列もある）。`X-RateLimit-Remaining` / `X-RateLimit-Reset` を付与 |

## 発見事項（セキュリティ観点、Phase 7 でも参照）

- API Token と Webhook secret を平文保存（確認済み）。TaskManager ではハッシュ保存（API Key）を検討。
- REST API の CSRF 無効 + CORS 全許可（未設定時）+ credentials 許可の組み合わせは設定ミスに弱い。
- ユーザー列挙が可能（email-check、エラーコード）。
- `SECRET_KEY` 未設定時はランダム生成（再起動でセッション無効化）。既知の弱い値を検出して警告するコードあり。

## 不明点

- PyJWT の用途（Q-AUTH-01）
- サインアップ時のパスワード強度チェックの有無（Q-AUTH-02）
- SameSite の明示設定有無（production.py 未精読）（Q-AUTH-03）

## TaskManager への影響

- Plane はセッション Cookie 方式。TaskManager（ASP.NET Core + React SPA）では Cookie 認証（ASP.NET Core Identity）と JWT（Bearer）のどちらも選択肢。CLAUDE.md §19 は JWT の確認を求めている。
- ロール 3 段階 × 2 階層（ワークスペース / プロジェクト）+ 作成者特権は業務要件として参考になる。
- オブジェクトレベル認可の漏れが Plane の主要な脆弱性パターン。TaskManager では「リソースの所属確認」を共通化（例: リポジトリでテナント条件を強制、またはポリシーベース認可）する設計が必要。

## Phase 2 で決定する事項

- 認証方式（Cookie / JWT / 両方）、トークン保存場所、有効期限・更新方法
- パスワードポリシー、パスワードリセット、メール検証の要否
- 外部 OAuth / Magic code の要否
- ロール体系（階層、ゲストの扱い、作成者特権）
- オブジェクトレベル認可の実装方式
- CSRF / CORS / SameSite 方針
- レート制限の対象と値
- API Key（外部連携）の要否と保存方式
