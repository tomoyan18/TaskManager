# 認証・認可設計

TaskManager 独自の設計。Plane のセッション方式・Secret・設定値は使わない（Phase 1 の解析は判断材料としてのみ使用）。

## 1. 方針の要約

| 項目 | 決定 |
|---|---|
| 認証方式 | メールアドレス + パスワード。**アクセストークン（JWT）+ リフレッシュトークン（HttpOnly Cookie）** |
| JWT の使用 | 使用する（アクセストークンのみ） |
| アクセストークン | JWT（HS256）、有効期限 **15 分**、ブラウザのメモリにのみ保持 |
| リフレッシュトークン | ランダム 32 バイト（Base64URL）、Cookie `tm_refresh`、DB にはハッシュのみ保存、**使うたびにローテーション**、有効 14 日・ログインから最長 30 日 |
| パスワードハッシュ | `PasswordHasher<T>`（PBKDF2-HMAC-SHA512、.NET 10 既定の反復回数） |
| アカウント発行 | システム管理者が発行（自己登録なし） |
| パスワード再設定 | システム管理者が一時パスワードを再発行（メール送信なし） |
| メール確認 | 行わない（管理者が発行するため。OD-08） |
| ロール | システムロール 2 種 + プロジェクトロール 3 種 |
| 認可の実装 | Api: 認証ポリシー（ログイン必須 / SA）、Application: `ProjectAccessGuard` による所属・ロール・アーカイブ判定 |

### 方式を選んだ理由

- **JWT + HttpOnly リフレッシュ Cookie**: SPA で API を呼ぶ際に `Authorization` ヘッダーを使うため、Cookie 認証のような CSRF の影響を通常 API が受けない。アクセストークンはメモリのみで XSS 時の持ち出しを短時間に限定し、リフレッシュトークンは JavaScript から読めない Cookie に置く。
- **却下: Cookie セッションのみ**（Plane 方式）: 全 API に CSRF 対策が必要になる。Plane では REST API の CSRF 検証を無効化していた（analysis/authentication-authorization.md）。
- **却下: JWT を localStorage に保存**: XSS で長期間使えるトークンが盗まれる。
- **却下: ASP.NET Core Identity 一式**: 表構造が固定され、TaskManager 独自の DB 設計・命名と合わない。パスワードハッシュ部分のみ利用する。

## 2. トークン仕様

### 2.1 アクセストークン（JWT）

| クレーム | 値 |
|---|---|
| `iss` | `TM_JWT_ISSUER`（例 `taskmanager`） |
| `aud` | `TM_JWT_AUDIENCE`（例 `taskmanager-web`） |
| `sub` | 利用者 ID（GUID） |
| `name` | 表示名 |
| `role` | `member` / `administrator`（システムロール） |
| `stamp` | `security_stamp` |
| `pwchg` | `1`（パスワード変更が必要な場合のみ） |
| `sid` | リフレッシュセッションの FamilyId |
| `iat` / `nbf` / `exp` | 発行時刻 / 有効開始 / 発行 + 15 分 |
| `jti` | GUID |

- 署名: HS256、鍵は `TM_JWT_SIGNING_KEY`（Base64 で 32 バイト以上。起動時に長さを検証し、不足なら起動失敗）
- 検証: 署名、`iss`、`aud`、有効期限（許容誤差 30 秒）、`alg` は HS256 のみ許可
- **即時失効**: 認証後に `sub` と `stamp` を DB の `security_stamp` と照合（`IMemoryCache` で 30 秒キャッシュ）。不一致・停止中なら 401。これによりパスワード変更・利用停止がアクセストークンの期限を待たずに反映される
- プロジェクトロールはトークンに入れない（変更が即時反映されるよう毎回 DB で判定）

### 2.2 リフレッシュトークン

- 生成: `RandomNumberGenerator.GetBytes(32)` → Base64URL
- 保存: SHA-256 ハッシュ（`refresh_session.token_hash`）
- Cookie: `tm_refresh`; `HttpOnly`; `Secure`（`TM_COOKIE_SECURE`、既定 true）; `SameSite=Strict`; `Path=/api/v1/auth`; `Max-Age` = 有効期限までの秒数
- ローテーション: `/auth/refresh` のたびに新トークンを発行し、旧セッションに `rotated_at_utc` を設定
- 再利用検知: ローテーション済みトークンが提示されたら、同じ `family_id` の全セッションを失効し、監査記録 `RefreshTokenReuseDetected`
- CSRF 対策（refresh / logout）: `SameSite=Strict` + Cookie の `Path` 限定 + `Origin` ヘッダーが自オリジンと一致すること（`Origin` がない場合は `Sec-Fetch-Site: same-origin` を要求。どちらもなければ 403 `auth.csrf_rejected`）

## 3. フロー

### 3.1 ログイン（`POST /auth/login`）

```text
1. レート制限（IP あたり 10 回/分）
2. email を正規化し利用者を検索
   - 見つからない → ダミーのハッシュ検証を実行（処理時間をそろえる）→ 401 auth.invalid_credentials
3. 停止中 → 401 auth.invalid_credentials（停止中であることは知らせない）
4. ロック中（locked_until_utc > now）→ 401 auth.locked
5. パスワード検証
   - 失敗 → failed_login_count++、5 回で 15 分ロック、監査記録（LoginFailed / AccountLocked）→ 401
   - 成功 → 再ハッシュが必要（PasswordVerificationResult.SuccessRehashNeeded）なら更新
6. failed_login_count=0、last_login_at_utc 更新
7. refresh_session 作成（family_id 新規）、アクセストークン発行
8. 監査記録 LoginSucceeded → 200 + Cookie
```

ロックの通知（`auth.locked`）は、そのメールアドレスのアカウントが存在することを示してしまう。ただし既に 5 回失敗した後にしか返さず、利用者にとって「なぜログインできないか」が分かることを優先した（OD-20 で確認）。

### 3.2 画面の再読み込み・トークン期限切れ

```text
SPA 起動時: POST /auth/refresh → 成功ならログイン状態、401 ならログイン画面
API 呼び出しで 401 auth.unauthenticated → POST /auth/refresh を 1 回だけ試行（同時に複数起きても 1 本にまとめる）→ 成功なら元のリクエストを再送、失敗ならログイン画面へ（元の URL を保持）
```

### 3.3 一時パスワードからの切替

```text
ログイン応答の user.mustChangePassword=true → パスワード変更画面へ強制遷移
API 側: トークンの pwchg=1 の場合、ログインが必要な API のうち /auth/password・GET /me 以外を 403 auth.password_change_required
      （/auth/refresh・/auth/logout と、匿名で呼べる /auth/login・/system/info はトークンの有無に関係なく通常どおり。Phase 6 で明確化）
変更成功 → 新トークン（pwchg なし）
```

### 3.4 ログアウト・利用停止・パスワード変更

| 操作 | 失効させるもの |
|---|---|
| ログアウト | 現在の refresh_session |
| パスワード変更（本人） | 他の全 refresh_session + security_stamp 更新（現在の端末には新トークンを発行） |
| パスワード再発行（SA） | 全 refresh_session + security_stamp 更新 |
| 利用停止（SA） | 全 refresh_session + security_stamp 更新 |

## 4. アカウントのライフサイクル

```text
[発行] SA が作成（一時パスワード, mustChangePassword=1）
   ↓ 初回ログイン → パスワード変更
[利用中] ⇄ [ロック中]（5 回失敗で 15 分、または SA が解除）
   ↓ SA が停止          ↑ SA が再開
[停止中]（ログイン不可、担当タスク・履歴は保持、新たに担当者にできない）
削除はしない
```

初期システム管理者: 起動時に有効な SA が 0 人なら、`TM_BOOTSTRAP_ADMIN_EMAIL` / `TM_BOOTSTRAP_ADMIN_PASSWORD` で作成（`mustChangePassword=1`）。変数が未設定なら警告ログを出して作成しない。既に SA がいる場合、これらの変数は無視する（ログにも値を出さない）。

## 5. パスワードポリシー

| 規則 | 値 | 理由 |
|---|---|---|
| 長さ | 12〜128 文字 | 長さを重視（NIST SP 800-63B の考え方） |
| 文字種 | 制限なし（全角も可）。ただし前後の空白は不可 | 複雑さの強制より長さ |
| 禁止 | メールアドレスのローカル部・表示名を含む、同じ文字の 4 回以上の連続、TaskManager 用の弱いパスワード一覧（リポジトリに同梱する独自リスト。例: `password1234`, `taskmanager`, `qwertyuiop`, `123456789012`） | 推測容易な値の排除 |
| 再利用 | 現在のパスワードと同じものは不可 | |
| 有効期限 | なし（定期変更は強制しない） | 定期変更は弱いパスワードを誘発する |
| 一時パスワード | 16 文字（英大文字・小文字・数字・記号から暗号論的乱数で生成、紛らわしい文字 `0O1lI` を除く） | 口頭・チャットで伝えやすく十分な強度 |

画面での説明文: 「12 文字以上で設定してください。長いほど安全です。メールアドレスや名前を含むもの、よく使われるものは使えません。」

## 6. ロールと権限

### 6.1 システムロール

| ロール | 画面表示 | できること |
|---|---|---|
| `Member` | 一般利用者 | プロジェクト作成、参加しているプロジェクトでの操作 |
| `Administrator` | システム管理者 | 上記 + 利用者管理、全プロジェクトの一覧（名前・キー・管理者のみ）、メンバー・ロールの変更（管理者不在時の救済）、アーカイブ済みプロジェクトの削除 |

システム管理者であっても、メンバーでないプロジェクトのタスク内容は見られない（最小権限）。必要なら自分をメンバーに追加する（監査記録が残る）。

### 6.2 プロジェクトロールと権限

| 権限（Permission） | 閲覧者 Viewer | 編集者 Editor | 管理者 Owner | SA（非メンバー） |
|---|---|---|---|---|
| ViewProject（プロジェクト・タスク・コメント・履歴・添付の参照） | ○ | ○ | ○ | × |
| CreateTasks / EditTasks | × | ○ | ○ | × |
| DeleteOwnTask（自分が作成したタスク） | × | ○ | ○ | × |
| DeleteAnyTask | × | × | ○ | × |
| Comment（投稿・自分のコメントの編集削除） | × | ○ | ○ | × |
| DeleteAnyComment | × | × | ○ | × |
| UploadAttachments（追加・自分の添付の削除） | × | ○ | ○ | × |
| DeleteAnyAttachment | × | × | ○ | × |
| ManageTags / ManageMilestones | × | ○ | ○ | × |
| ManageStatuses | × | × | ○ | × |
| EditProjectSettings / ArchiveProject | × | × | ○ | × |
| ManageMembers | × | × | ○ | ○ |
| 担当者になれる | × | ○ | ○ | — |
| DeleteProject（アーカイブ後） | × | × | × | ○ |
| 脱退（自分をメンバーから外す） | ○ | ○ | ○（最後の管理者を除く） | — |

アーカイブ中のプロジェクトでは、ViewProject と（OW の）復元、SA の削除・メンバー管理以外はすべて 409 `project.archived`。

### 6.3 ガントチャート・ステータス別円グラフの権限（Phase 9 で追加）

新しい権限（Permission）は作らず、既存の権限に対応付ける。

| 操作 | API | 権限 | 非メンバー | アーカイブ中 |
|---|---|---|---|---|
| ガントの表示 | #62 `GetTaskSchedule` | ViewProject（閲覧者以上） | 404 | 参照可 |
| ガントでの日程変更（棒のドラッグ・表の日付欄） | #44 `UpdateTask`（既存） | EditTasks（編集者以上） | 404 | 409 |
| プロジェクトの円グラフ | #63 `GetTaskStatusSummary` | ViewProject | 404 | 参照可 |
| 自分のタスクの円グラフ（全プロジェクト） | #64 `GetMyTaskStatusSummary` | ログイン済み（本人） | —（参加中のプロジェクトだけを集計） | アーカイブ中のプロジェクトは集計に含めない |
| 自分のタスクの円グラフ（プロジェクト指定） | #64（`projectKey`） | ViewProject | 404 | 参照可 |

- SA（非メンバー）は、他の参照系と同じく #62・#63・#64（プロジェクト指定）とも 404（内容を見られない）。
- 一時パスワード状態は 403 `auth.password_change_required`（既定のフィルター）。


## 7. 認可の実装

### 7.1 エンドポイント認可（Api）

| ポリシー | 適用 | 内容 |
|---|---|---|
| 既定（FallbackPolicy） | 全 Controller | 認証済み（`RequireAuthenticatedUser`）。`[AllowAnonymous]` は `login` のみ。refresh / logout は独自の Cookie 検証 |
| `PasswordChangeNotRequired` | 既定で全アクションに適用（フィルタ） | `pwchg=1` なら 403。除外: ChangePassword, GetMe, Logout, Refresh |
| `SystemAdministrator` | `UsersController`、`DeleteProject` | `role=administrator` |

### 7.2 リソース（オブジェクト）認可（Application）

**原則: プロジェクト配下のリソースは、必ず「URL のプロジェクト」から辿って取得する。**

```csharp
// 擬似コード（実装時のイメージ。Phase 4 で実装）
var access = await guard.RequireAsync(projectKey, ProjectPermission.EditTasks);
// → 非メンバー: NotFound、ロール不足: Forbidden、アーカイブ: Conflict
var task = await db.TaskItems
    .SingleOrDefaultAsync(t => t.ProjectId == access.ProjectId && t.Number == taskNumber)
    ?? throw new ResourceNotFoundException();
var comment = await db.TaskComments
    .SingleOrDefaultAsync(c => c.TaskId == task.Id && c.Id == commentId)
    ?? throw new ResourceNotFoundException();
```

- 子リソース（コメント・添付・ステータス・タグ・マイルストーン）は **親の ID を条件に含めて** 取得する。ID 単独で取得するクエリを書かない（Phase 1 で確認した IDOR の典型パターンを構造的に排除。analysis/authentication-authorization.md）。
- Request 内で参照される ID（`statusId`, `assigneeUserId`, `tagIds`, `milestoneId`, `parentTaskNumber`）も同じプロジェクトに属するかを検証し、属さなければ 400（存在を漏らさないよう「選択した〇〇が見つかりません」）。
- 「本人のみ」の判定（コメント編集など）は Application で `AuthorUserId == currentUser.Id` を確認。
- お知らせは常に `recipient_user_id = 現在の利用者` を条件に含める。
- 自分の担当の集計（Phase 9、#64）は常に `assignee_user_id = 現在の利用者` と「現在の利用者が参加中のプロジェクト」を条件に含める。`projectKey` を指定した場合も `ProjectAccessGuard` を通し、指定したプロジェクトの中で `assignee_user_id = 現在の利用者` に限る（他人の担当・他のプロジェクトの件数を返さない）。
- Request DTO は更新可能な項目だけを持つ（`isActive` や `role` のような権限に関わる項目を汎用の更新 DTO に含めない。Plane の `is_active` 書き換え事例への対策）。

### 7.3 テストでの担保

ロール × 操作 × 所属（自分のプロジェクト / 他プロジェクト / アーカイブ）の認可マトリクステストを全エンドポイントに対して作る（[testing-strategy.md](testing-strategy.md) §5）。

## 8. 失敗時の振る舞い

| 状況 | API | 画面 |
|---|---|---|
| ログイン失敗 | 401 `auth.invalid_credentials` | 「メールアドレスまたはパスワードが正しくありません。」入力したメールは残し、パスワード欄を空にする |
| ロック | 401 `auth.locked` | 「ログインに続けて失敗したため、一時的にロックされています。15 分ほど待ってから再度お試しください。」 |
| 試行過多 | 429 | 「しばらく時間をおいてから再度お試しください。」 |
| セッション切れ | 401 `auth.session_expired` | ログイン画面へ。「ログインの有効期限が切れました。もう一度ログインしてください。」ログイン後は元の画面へ戻る |
| 権限不足 | 403 `access.forbidden` | トースト「この操作を行う権限がありません。」（通常はボタン自体を表示しない） |
| 参照権限なし | 404 | 「ページが見つかりません」画面（存在しないのか権限がないのかを区別しない） |

## 9. 監査

[requirements.md](requirements.md) REQ-AUD-01 の出来事を `security_audit_event` に記録し、同じ内容を構造化ログにも出す。パスワード・トークン・一時パスワードは記録しない。
