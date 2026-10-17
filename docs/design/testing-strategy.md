# テスト戦略

## 1. 方針

- 実際に実行したテストだけを「成功」と記録する（CLAUDE.md §18）。結果は Phase 6 で `docs/test/` に記録する。
- **本番と同じ MySQL 8.4** で DB を伴うテストを行う（EF Core InMemory / SQLite は MySQL と挙動が違うため使わない）。
- **OpenAPI を API テストの契約** として使う（§6）。
- 認可は「ロール × 操作 × 所属」のマトリクスで網羅する（Phase 1 で確認した IDOR の再発防止。analysis/testing.md）。
- テストは Phase 5 で作成、Phase 6 で実行する。

## 2. テストの種類と範囲

| 種類 | プロジェクト / 場所 | 対象 | ツール | DB |
|---|---|---|---|---|
| Domain | `tests/TaskManager.Domain.Tests` | Entity・Value Object の不変条件（TK-01〜08、S-01〜04 など）、ステータス遷移による完了日時、期限区分の計算 | xUnit | なし |
| Application | `tests/TaskManager.Application.Tests` | ユースケース（Service）の正常系・異常系、`ProjectAccessGuard`、お知らせ・履歴の生成、パスワードポリシー | xUnit + Testcontainers.MySql | MySQL コンテナ |
| Infrastructure | `tests/TaskManager.Infrastructure.Tests` | EF Core の設定（列型・制約・インデックス）、マイグレーション適用、採番の同時実行、楽観的排他、CHECK 制約、添付ファイル保存（パス生成・削除） | xUnit + Testcontainers.MySql | MySQL コンテナ |
| API | `tests/TaskManager.Api.Tests` | HTTP の入出力、ステータスコード、Problem Details、認証・認可、レート制限、セキュリティヘッダー、OpenAPI 契約 | xUnit + WebApplicationFactory + Testcontainers.MySql | MySQL コンテナ |
| Frontend 単体 | `frontend/src/**/*.test.ts(x)` | 日付・期限区分の表示、タスクキー解析、フォーム検証、API エラー変換、主要コンポーネントの表示と操作 | Vitest + Testing Library + MSW | なし（MSW） |
| E2E | `e2e/` | 主要フロー（UF-01〜06）を実ブラウザで。アクセシビリティ自動検査 | Playwright + axe | Docker Compose の全体構成 |
| セキュリティ | API テスト + E2E + 手動 | §7 | 同上 | |

## 3. テストデータ

- 各テストで必要なデータを **ビルダー**（`TestDataBuilder`: 利用者・プロジェクト・タスクを作る補助）で作成する。Plane のサンプルデータは使わない。
- テスト用のメール・名前は架空の値（`owner@example.test`、「テスト 管理者」）。実在の個人情報は使わない。
- MySQL コンテナはテストクラス群で共有し、テストごとに DB を作り直すか、表を TRUNCATE する（Phase 5 で速度を見て選ぶ）。
- 時刻は `FakeTimeProvider`（`Microsoft.Extensions.TimeProvider.Testing`）で固定。

## 4. 重要な観点（必ずテストする）

| 区分 | 観点 |
|---|---|
| 採番 | 同じプロジェクトに 20 並列でタスク作成 → 番号が 1〜20 で重複・欠番なし。別プロジェクトは独立 |
| 楽観的排他 | 同じ `version` で 2 回更新 → 2 回目が 409 |
| 期限区分 | タイムゾーン（`Asia/Tokyo` と `UTC`）の日付境界、日曜日・月曜日、期限なし |
| 完了日時 | 未着手 → 完了で設定、完了 → 対応中で消去、完了 → 中止で消去 |
| 親子 | 2 段目の作成拒否、自己参照拒否、サブタスクありの削除拒否・一括削除 |
| ステータス削除 | 既定は削除不可、使用中は移動先必須、移動後に履歴が残る |
| メンバー削除 | 担当解除と履歴（actor なし）、最後の管理者を外せない |
| お知らせ | 本人には作らない、5 分以内の重複をまとめる |
| 認証 | ログイン成功・失敗、5 回でロック、ロック中は正しいパスワードでも不可、停止中は不可、リフレッシュのローテーション、再利用検知で系列失効、パスワード変更で他セッション失効、security stamp 不一致の即時失効、一時パスワード状態での API 制限 |
| 入力検証 | 各 Request の境界値（最小・最大・超過）、不正な列挙、不正な日付、未知のプロパティ（無視されること） |
| 添付 | 10MB ちょうど / 超過、拡張子と中身の不一致、許可外形式、ファイル名の `../`・制御文字、件数上限、ダウンロードのヘッダー |

## 5. 認可マトリクステスト

- 全エンドポイント（[api-design.md](api-design.md) §3 の 60 件）について、次の呼び出し元で期待ステータスを表にし、データ駆動テスト（`[Theory]` + `MemberData`）で検証する。

| 呼び出し元 | 例 |
|---|---|
| 未認証 | 401 |
| 一時パスワード状態 | 403 `auth.password_change_required`（除外 API 以外） |
| 非メンバー | 404 |
| 閲覧者 | 参照 200 / 更新 403 |
| 編集者 | 参照・更新 200 / 設定 403 |
| 管理者 | 200 |
| SA（非メンバー） | プロジェクト配下 404（メンバー管理・削除を除く）、Users 200 |
| 一般利用者 | Users 403 |
| アーカイブ中のプロジェクトの管理者 | 更新 409 |

- **IDOR 専用テスト**: プロジェクト A のメンバーが、URL はプロジェクト A のまま、Request 内やパスにプロジェクト B のステータス ID・タグ ID・マイルストーン ID・コメント ID・添付 ID・利用者 ID を指定 → 400 または 404 で、B のデータが変化しないこと。
- 期待値表は `tests/TaskManager.Api.Tests/Authorization/AuthorizationMatrix.cs` に 1 か所で定義し、新しいエンドポイント追加時に表への追加漏れを検出するテスト（全 operationId が表に存在すること）を置く。

## 6. OpenAPI 契約テスト

1. **スナップショット比較**: ビルド時に生成した OpenAPI 文書と、コミット済みの `docs/api/openapi.v1.json` を正規化（キー順）して比較。差分があれば失敗し、「設計書とスナップショットを更新してください」と表示。
2. **設計との対応**: スナップショットに次が含まれることを検査
   - [api-design.md](api-design.md) §3 の全 operationId とメソッド・パス
   - 全 operation に `tags`、`summary`、`security`（login を除く）、401/500 応答、該当する 400/403/404/409 応答（`application/problem+json`）
   - Request スキーマの制約（例: `CreateTaskRequest.title` の `minLength: 1`, `maxLength: 200`）
3. **応答の適合**: API テストで受け取った全応答（成功・エラー）を、OpenAPI のスキーマで検証する共通アサーション（`AssertMatchesOpenApi(response, operationId, status)`）。JSON Schema 検証ライブラリは Phase 3 で選定（OD-26）。
4. **Frontend**: 同じスナップショットから型を生成し、`tsc --noEmit` で Frontend の API 呼び出しと型の整合を検査（存在しない API の呼び出しは型エラーになる）。
5. **秘密情報の非掲載**: スナップショットに Secret 名・値・内部パス・例外型名・Plane の URL / 連絡先が含まれないことを文字列検査。

## 7. セキュリティテスト

| 対象 | 方法 |
|---|---|
| 認可・IDOR | §5 |
| 認証・トークン | §4 の認証観点、改ざん JWT（署名不正・`alg: none`・期限切れ・別 audience）の拒否 |
| CSRF | refresh / logout に別 Origin → 403、Origin なし + `Sec-Fetch-Site: cross-site` → 403 |
| XSS | Markdown（タイトル・説明・コメント）に `<script>`、`<img onerror>`、`javascript:` リンクを入れて保存 → Frontend 表示で実行されない（Vitest でレンダリング結果検査 + E2E） |
| SQL インジェクション | 検索・絞り込みに `' OR 1=1 --`、`%`、`_` → 期待どおりの結果（全件にならない、エラーにならない） |
| 入力サイズ | 1MB を超える JSON → 413、深いネストの JSON → 400 |
| レート制限 | ログイン 11 回目 → 429 + `Retry-After` |
| エラー情報漏えい | 意図的な例外 → 500 で例外メッセージ・スタックトレースを含まない |
| セキュリティヘッダー | API と web の応答ヘッダー（CSP、`X-Content-Type-Options` 等）を検査 |
| ログ | ログイン・パスワード変更のテスト中のログ出力にパスワード・トークンが含まれないこと（ログ収集プロバイダで検査） |
| 依存関係 | `dotnet list package --vulnerable`、`npm audit`（Phase 7） |
| コンテナ | 非 root 実行、DB ポート非公開の確認（Phase 7） |

## 8. Frontend テスト

| 対象 | 観点 |
|---|---|
| `utils/dates` | 期限表示（今日・明日・超過・年またぎ）、タイムゾーン |
| `utils/taskKey` | `SALES-12` の解析、小文字入力 |
| `api/client` | 401 → リフレッシュ → 再送、同時 401 で refresh を 1 回だけ、Problem Details の変換 |
| フォーム | 必須・文字数・日付順序のメッセージ、API の項目エラーの反映 |
| `TaskTable` / `TaskStatusBoard` | 表示、セル内変更、キーボードでのステータス変更、空状態 |
| 権限表示 | `permissions.canEdit=false` で編集部品が出ない |

## 9. E2E シナリオ

| ID | シナリオ |
|---|---|
| E2E-01 | 初期管理者でログイン → パスワード変更 → 利用者発行 → 一時パスワードでログイン → パスワード変更 |
| E2E-02 | プロジェクト作成 → メンバー追加 → タスク作成（タイトルのみ）→ 担当者・期限設定 → ホームに表示 |
| E2E-03 | ボードでステータス変更（マウス）、メニューでステータス変更（キーボードのみ） |
| E2E-04 | コメント投稿 → 担当者にお知らせ → 既読 |
| E2E-05 | 添付ファイルの追加・ダウンロード・削除 |
| E2E-06 | 閲覧者で編集部品が表示されない、URL 直打ちの他プロジェクトは 404 画面 |
| E2E-07 | スマートフォン幅（390×844）で E2E-02 の主要部分 |
| E2E-08 | 全画面で axe の重大な違反（serious / critical）がない |

## 10. 実行と合格基準

| 段階 | コマンド（Phase 3 で確定） | 合格基準 |
|---|---|---|
| Backend | `dotnet test` | 全件成功。Domain + Application の行カバレッジ 80% 以上（NFR-13） |
| Frontend | `npm run test`、`npm run typecheck`、`npm run lint` | 全件成功、型エラー 0 |
| E2E | `docker compose up` 後に `npx playwright test` | 全シナリオ成功 |
| ビルド | `dotnet build -warnaserror`、`npm run build` | 警告 0（Backend）、ビルド成功 |

CI（GitHub Actions 等）の導入は OD-27。導入しない場合も Phase 6 で上記を手元で実行し結果を記録する。

## 11. Phase 5 での変更（2026-10-12）

テストコードの作成時に変えた点（ローカルの MySQL の利用、DB の共有方法、E2E の接続先、OD-26 の決定など）は
[docs/testing/test-design.md](../testing/test-design.md) §11 に記録した。

## 12. Phase 9 で追加するテスト（2026-10-16）

ガントチャート・ステータス別円グラフ（REQ-GNT-01〜03、REQ-CHT-01〜02）。詳細と結果は docs/corrections/phase-09/verification-report.md。

| 種類 | 対象 | 観点 |
|---|---|---|
| Domain | `ScheduleWindow` | 期間の検証（開始 > 終了、366 日、367 日、範囲外）、棒の範囲（両方・開始日だけ・期限だけ・なし）、期間との重なり（境界の日を含む） |
| Domain | `ShareRounding` | 割合の合計が 100.0 になる（1/3 ずつ、端数の大きい順、同じ端数は先の項目）、合計 0 件、0 件の項目、1 件だけ |
| Application（MySQL） | `TaskScheduleService` | 期間と重なるタスクの抽出（SQL の `COALESCE` 条件）、既定は未完了のみ・`includeClosed`、日付未設定の件数と一覧、並び順、期限切れの判定（利用者のタイムゾーン）、非メンバー 404・アーカイブ中は参照可、期間の検証 |
| Application（MySQL） | `TaskStatusSummaryService` | ステータス別の件数（0 件を含む・並び順）、割合の合計、空のプロジェクト、他のプロジェクトのタスクを含まない、自分の担当（全プロジェクト = 区分別、アーカイブ中・非参加のプロジェクト・他人の担当を含まない）、プロジェクト指定時のステータス別、非メンバーのプロジェクト指定は 404 |
| Infrastructure（MySQL） | スキーマ | 追加した索引の列と順序、マイグレーションとモデルの差分がない |
| API | 認可マトリクス | 3 操作 × 8 種類の呼び出し元（全 operationId の登録検査を含む） |
| API | 契約・入力・IDOR | 応答が OpenAPI に一致、`from` / `to` の欠落・逆転・367 日・形式不正は 400、`projectKey` の形式不正は 400、他のプロジェクトのキーを指定しても他人の件数が返らない（404）、操作数 64 |
| Frontend | `utils/schedule.ts` | 期間の計算（日・週・月、前へ / 次へ / 今日）、目盛り、棒の位置（期間からはみ出す棒の切り取り）、まとめ方 |
| Frontend | `StatusPieChart` | 0 件のステータスも表に出る、合計件数、割合の表示、要約の文、0 件の空状態、1 項目だけ（円全体） |
| Frontend | 画面 | ガントの表示・表示単位の切り替えで取得条件が変わる・日付未設定の枠・期限切れの表示・表で見る・閲覧者には日程変更の部品がない・エラー時の再試行、円グラフの表示・リンク、自分のタスクの切り替え |
| E2E | E2E-09 | ガントの表示と表示単位の切り替え（日・週・月）、表の日付欄での日程変更、プロジェクトの円グラフ、自分のタスクの円グラフ、axe の重大な違反なし、CSP 違反なし |

