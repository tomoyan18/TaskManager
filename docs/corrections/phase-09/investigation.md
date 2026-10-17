# Phase 9 現状調査（investigation）

- 作成日: 2026-10-16
- 対象: ガントチャート機能とビュー、ステータス別円グラフ機能とビューの追加（Phase 9 CORRECTION）
- 目的: 修正の前に、プロジェクトの全ファイルを確認し、現状・矛盾・前提を記録する（`.claude/skills/phase-09-correction/CLAUDE.md`「作業順序 1・2」）
- 記載の「確認済み」は、本書の作成時に実際にファイルを開いて内容を読んだことを表す

---

## 1. 指示ファイルの確認

| ファイル | 確認 | 内容 |
|---|---|---|
| `CLAUDE.md`（ルート、632 行） | 全文 | Phase 1〜8 の進め方、技術スタック、命名・コピー禁止、DB・API・Swagger・UI/UX・日本語・テスト・セキュリティ・完成条件。**変更しない** |
| `.claude/skills/phase-09-correction/CLAUDE.md` | 全文 | Phase 9 の目的・対象外・作業順序・修正記録・更新対象の設計書・禁止事項・完了条件 |
| `.claude/skills/phase-09-correction/SKILL.md` | 全文 | ガント・円グラフの詳細要件、Backend・API・Frontend・テスト・README の要件 |
| 利用者の指示（チャット） | 全文 | 上記と同じ範囲。実行内容は日本語で出力する。Phase 10 へ進まない |

### 1.1 指示ファイルの配置場所の食い違い（記録）

| 記載元 | 書かれている場所 | 実際の場所 |
|---|---|---|
| Phase 9 の CLAUDE.md「位置づけ」 | `/Volumes/OutSSD/CSharp/TaskManager/Phase-09-Correction/CLAUDE.md`、`Phase-09-Correction/SKILL.md` | `.claude/skills/phase-09-correction/CLAUDE.md`、`SKILL.md` |
| 利用者の指示 | `/Volumes/OutSSD/CSharp/TaskManager/phase-09-correction/` | 同上 |

- `Phase-09-Correction/` と `phase-09-correction/`（リポジトリ直下）は存在しない（`ls` で確認）。
- 内容は同一の指示として扱い、実在する `.claude/skills/phase-09-correction/` の 2 ファイルに従う。ファイルの移動・複製はしない（利用者が管理するファイルのため）。

## 2. ルートの CLAUDE.md と Phase 9 指示の矛盾・調整

Phase 9 の指示では「矛盾する場合は Phase 9 の作業範囲に限り Phase 9 の CLAUDE.md を優先し、矛盾内容を記録する」とされている。

| # | ルートの CLAUDE.md（または既存の設計） | Phase 9 の指示 | 扱い |
|---|---|---|---|
| C-01 | §4 のフェーズは Phase 8 まで。「現在のフェーズ以外の作業を勝手に開始してはいけない」 | Phase 9（修正）の実施を指示 | 利用者が Phase 9 を明示的に指示したため、Phase 9 を現在のフェーズとして実施する。Phase 10 には進まない |
| C-02 | requirements.md §7「対象外: ガントチャート・カレンダー表示・分析ダッシュボード」、open-decisions.md OD-12（将来検討） | ガントチャートと、ステータス別の円グラフを追加 | Phase 9 を優先。requirements.md §7 と OD-12 を更新し、**ガント・ステータス別円グラフのみ**対象に加える。カレンダー表示、ステータス以外の集計、時系列集計は引き続き対象外 |
| C-03 | ルートの §5「各フェーズでは `.claude/skills/phase-XX-xxx/` の 2 ファイルを確認」 | 指示ファイルの場所を `Phase-09-Correction/` と記載 | §1.1 のとおり、実在する `.claude/skills/phase-09-correction/` を使う |
| C-04 | 既存方針: 期限・開始日は日付のみ（`DATE`、タイムゾーンなし）、日時は UTC 保存、「今日」は利用者のタイムゾーン（既定 `Asia/Tokyo`）で判定（requirements.md NFR-10、domain-model.md） | 「日時は UTC で保存、表示は Asia/Tokyo（既存方針がある場合はそれに従う）」 | 既存方針に従う。新しい日時列は追加しない。ガントの「今日」・期限切れは利用者のタイムゾーン（既定 `Asia/Tokyo`）で判定する |
| C-05 | 既存のステータス定義: プロジェクトごとの `WorkflowStatus`（既定は 未着手 / 対応中 / 確認待ち / 完了 / 中止。名前・色・並び順・区分は利用者が変更可能） | 円グラフの例「未対応、対応中、レビュー中、完了」（例を新たに追加しない） | 例のステータスは追加しない。既存の `WorkflowStatus` と区分（`StatusCategory`）をそのまま使う |
| C-06 | ステータスはプロジェクトごとに定義（複数プロジェクトで名前・色・並び順が異なる） | SKILL.md: 「自分のタスク」ページにも、各利用者のステータスごとの割合を表示 | 複数プロジェクトにまたがる集計で「ステータス」をどう束ねるかは既存設計に答えがない。open-decisions.md に記録し（OD-34）、推奨案で設計する（impact-analysis.md §4） |
| C-07 | ルート §11 に MySQL の接続情報（利用者名・パスワード）が記載されている（known-issues.md KI-01） | — | 値をリポジトリのファイルに書き写さない。テストでは従来どおり環境変数 `TM_TEST_MYSQL_CONNECTION` でのみ渡す |
| C-08 | ルート §10「Plane の Component・API・Table・Column 名を流用しない」 | 同じ（Plane のガント・Analytics をコピーしない） | 矛盾なし。Phase 1 の解析資料に Plane のガント（`BaseGanttRoot`、`gantt` レイアウト）・分析（`analytics`、`advance-analytics-charts` など）の名前が記録されている。これらの名前・構造は使わない（naming-rules.md で照合） |

## 3. 確認したファイル

確認日: 2026-10-16。Git 管理対象（未追跡を含む）のうち、`.claude/` と生成物（`bin`・`obj`・`node_modules`・`dist`・`artifacts` など `.gitignore` の対象）を除いた **321 ファイル**。

| 区分 | ファイル数 | 行数 | 確認方法 |
|---|---|---|---|
| ルート（`CLAUDE.md`、`README.md`、`.env.example`、`.gitignore`、`.dockerignore`、`.editorconfig`、`Directory.Build.props`、`Directory.Packages.props`、`global.json`、`dotnet-tools.json`、`docker-compose.yml`、`docker-compose.dev.yml`） | 12 | 1,260 | 全文 |
| `docker/`（`api/Dockerfile`、`web/Dockerfile`、`web/nginx.conf`、`mysql/Dockerfile`、`mysql/init/01-create-users.sh`） | 5 | 159 | 全文 |
| `docs/design/`（15） | 15 | 3,538 | 全文 |
| `docs/analysis/`（15） | 15 | 2,473 | 14 ファイルは全文。`appendix-api-endpoints.md`（Plane の URL の機械抽出 400 行）は冒頭の説明と、ガント・分析に関係する行（`analytic.py` 系）を確認 |
| `docs/environment/`（5）、`docs/implementation/status.md`、`docs/testing/`（3）、`docs/security/security-review.md`、`docs/completion/`（2）、`docs/build_Application/StartUp-Application.md` | 13 | 1,961 | 全文（`test-coverage.md` は見出し構成と、API・Frontend のテスト一覧を確認） |
| `docs/api/openapi.v1.json` | 1 | 9,303 | 構造と件数を機械的に確認（操作 61、スキーマ 75）。タスク・ステータス・プロジェクト関連のスキーマを確認 |
| `docs/images/`（PNG 8 枚） | 8 | — | README で参照されていることを確認（画像の再撮影は Phase 9 のガント・円グラフのみ追加） |
| `src/TaskManager.Domain`（21） | 21 | 1,445 | 全文 |
| `src/TaskManager.Application`（41） | 41 | 4,021 | 全文 |
| `src/TaskManager.Infrastructure`（18） | 18 | 4,237 | 手書きのコードは全文。自動生成のマイグレーション（`InitialSchema.cs` 869 行）は表定義・索引・`Down` を確認、`*.Designer.cs`・`AppDbContextModelSnapshot.cs`（自動生成）は構成を確認 |
| `src/TaskManager.Api`（34）、`src/TaskManager.sln` | 35 | 2,368 | Controller・Program・OpenAPI・エラー処理・認可・レート制限は全文。認証まわり（`AuthController`・`JwtOptions` など）は構成と公開メソッドを確認 |
| `frontend/task-manager-web`（88） | 88 | 17,519 | 画面・API クライアント・フック・部品・スタイル・テスト基盤は全文。`schema.d.ts`（生成物）と `package-lock.json` は対象外（生成物）。認証・利用者管理・設定・お知らせの画面は構成と API 呼び出しを確認 |
| `frontend/task-manager-e2e`（6） | 6 | 454 | 全文（`package-lock.json` を除く） |
| `tests/`（43） | 43 | 6,101 | テスト基盤（`TaskManager.Testing`）、認可マトリクス、OpenAPI 契約、スキーマ、自分のタスク・タスクのテストは全文。その他は構成と対象を確認 |

全ファイルの一覧は作業用に生成した（リポジトリ外: スクラッチパッドの `p9-files.txt`）。Plane のリポジトリ（`/Volumes/OutSSD/CSharp/plane`）は Phase 9 では開いていない（Phase 1 の解析資料のみを参照）。

## 4. 現状（機能に関係する部分）

### 4.1 タスク（`TaskItem`）の項目（確認済み: `Domain/Tasks/TaskItem.cs`、`Persistence/Configurations/TaskConfigurations.cs`、`InitialSchema.cs`）

| 項目 | 型（C#） | 列（MySQL） | Phase 9 での利用 |
|---|---|---|---|
| 開始日 | `DateOnly? StartDate` | `start_date DATE NULL` | ガントの棒の始点 |
| 期限 | `DateOnly? DueDate` | `due_date DATE NULL` | ガントの棒の終点 |
| ステータス | `Guid StatusId` | `status_id CHAR(36)`（FK `workflow_status`、RESTRICT） | 円グラフの集計軸、ガントのグループ |
| 担当者 | `Guid? AssigneeUserId` | `assignee_user_id` | ガントのグループ、自分のタスクの集計 |
| マイルストーン | `Guid? MilestoneId` | `milestone_id`（SET NULL） | ガントのグループ |
| 完了日時 | `DateTime? CompletedAtUtc` | `completed_at_utc DATETIME(6)` | 表示のみ（集計には使わない） |
| 更新日時 | `DateTime UpdatedAtUtc` | `updated_at_utc DATETIME(6)`（UTC） | 変更なし |
| 楽観的排他 | `uint RowVersion` | `row_version INT UNSIGNED`（Concurrency Token） | ガントのドラッグでの日程変更に使う（既存の `version`） |

- 業務ルール TK-02: 開始日 ≤ 期限（Domain `ChangeDates` と CHECK 制約 `ck_task_item__date_range` の二重）、範囲 2000-01-01〜2099-12-31。
- **開始日・期限は既に存在するため、Entity・列の追加は不要**。
- 期限切れ: `TaskItem.IsOverdue(due, category, today)` = 終了区分でなく期限 < 今日。今日は利用者のタイムゾーン（`TimeProvider.TodayIn(user.TimeZoneId)`）。
- 論理削除なし（OD-06）。タスクは物理削除。アーカイブはプロジェクト単位（`project.archived_at_utc`）。

### 4.2 ステータスの定義（確認済み: `Domain/Projects/WorkflowStatus.cs`、`Project.CreateDefaultStatuses`、`Domain/Common/Enums.cs`）

- プロジェクトごとの `WorkflowStatus`: `Name`（1〜30 文字、プロジェクト内一意）、`Category`（`StatusCategory`: 未着手 1 / 進行中 2 / 完了 3 / 中止 4）、`Color`（`StatusColor` 9 色）、`SortOrder`（1〜）、`IsDefault`。1 プロジェクト 1〜20 件。
- 既定 5 件: 未着手（Gray）/ 対応中（Blue）/ 確認待ち（Purple）/ 完了（Green）/ 中止（Red）。
- 表示色は Frontend の `styles/tokens.css` の `--sc-{color}-bg / -fg / -bar`（コントラスト確認済みのパレット）。区分の日本語は `constants/labels.ts` の `CATEGORY_LABELS`。
- 既存の集計: `GET /statuses` の `taskCount`（`GROUP BY status_id`）、`GET /projects/{key}` の `taskCounts`（区分別。`Join` + `GroupBy`）。いずれも MySql.EntityFrameworkCore で SQL の `GROUP BY` に変換されていることを Phase 5〜8 のテストで確認済み。

### 4.3 既存のビューと切り替え（確認済み: `ProjectTasksPage.tsx`、`useTaskListParams.ts`、`ui-design.md`）

| 画面 | URL | 切り替え |
|---|---|---|
| SCR-06 タスク一覧（表） | `/projects/:key/tasks`（`view` なし = `table`） | `表` / `ボード` のボタン（`role="group"`、`aria-pressed`）。状態は URL クエリ `view` |
| SCR-07 タスク一覧（ボード） | `?view=board` | 同上 |
| SCR-04 自分のタスク | `/` | 切り替えなし（期限区分の一覧 + 「最近完了したものも表示」） |
| プロジェクト内タブ | `ProjectLayout`（タスク / マイルストーン / 設定） | `NavLink` |

- 絞り込み・並び・表示形式・ページは URL クエリで保持（OD-25）。`useTaskListParams` が `view` を `'table' | 'board'` に限定している。
- 一覧の取得は `GET /tasks`（最大 100 件 / ページ）。ガントに必要な「期間と重なるタスク」の条件は既存 API にない（`dueFrom` / `dueTo` は期限のみ）。

### 4.4 API の規約（確認済み: `api-design.md` §1〜§2、`OpenApiSetup.cs`、`ValidationProblemFactory.cs`）

- ベース `/api/v1`、プロジェクトは `projectKey`、JSON camelCase、列挙は camelCase 文字列、日付 `YYYY-MM-DD`、日時 ISO 8601 UTC。
- エラーは Problem Details（`code`・`traceId`・`errors`）、`application/problem+json`。404（非メンバー）/ 403（ロール不足）/ 409（アーカイブ中の変更）。
- ページング `page` / `pageSize`（最大 100）、並べ替えは許可リスト。集計・一覧の上限は件数で制限（自分のタスク 500 件 + `truncated`）。
- OpenAPI はコントローラーの属性（`EndpointName`・`Tags`・`EndpointSummary`・`ProducesResponseType`・`RequiredPermission`）から生成し、共通エラー・セキュリティ要件は `OpenApiSetup` の変換で付与。スナップショット `docs/api/openapi.v1.json` と一致しなければテストが失敗する。Frontend の型は同じスナップショットから生成（`npm run generate:api`）。
- 現在 61 操作。`OpenApiDocumentTests` は操作数（61）を検査している。

### 4.5 認可（確認済み: `ProjectAccessGuard.cs`、`ProjectPermission.cs`、`AuthorizationMatrix.cs`）

- 既定でログイン必須（FallbackPolicy）。一時パスワード状態は 403。
- プロジェクト配下は `ProjectAccessGuard.RequireAsync(projectKey, permission)`: 非メンバー 404、ロール不足 403、アーカイブ中は参照以外 409。SA は非メンバーのプロジェクトの内容を見られない（ManageMembers・DeleteProject を除き 404）。
- 「自分の〜」系（`/me/tasks`）は、参加中かつアーカイブされていないプロジェクトに限定（`ProjectMemberships.Any(...)` で絞る）。
- 認可マトリクス: 全 operationId × 8 種類の呼び出し元。**新しい操作はマトリクスへの登録が必須**（未登録ならテスト失敗）。
- IDOR: 子リソースは URL のプロジェクトから辿る。`IdorApiTests` で他プロジェクトの ID を指定した場合を確認。

### 4.6 テストと最新の結果（確認済み: `docs/testing/test-results.md`、`docs/completion/final-checklist.md`）

| 区分 | 件数（Phase 8 の記録） | 実行環境 |
|---|---|---|
| Backend（Domain 136 / Application 215 / Infrastructure 53 / API 590） | 994 / 994 成功（ローカル MySQL 8.0.46 で 2 回、Testcontainers MySQL 8.4.11 で 1 回） | 2026-10-13 |
| Frontend（Vitest） | 107 / 107 成功 | 同上 |
| E2E（Playwright、Docker 構成） | 8 / 8 成功 | 同上 |

Phase 9 開始時（変更前）の基準実行の結果は §6 に記録する。

### 4.7 グラフ・ガント・日付のライブラリ（確認済み: `package.json`、`Directory.Packages.props`、`technology.md`）

| 用途 | 現状 |
|---|---|
| グラフ（円グラフ等） | **なし** |
| ガントチャート | **なし** |
| 日付 | ライブラリなし。`utils/dates.ts`（`Intl.DateTimeFormat`・UTC 基準の日付計算）。date-fns は Phase 4 で不採用（status.md C-09） |
| アイコン | インライン SVG（`components/Icon.tsx`） |
| ドラッグ&ドロップ | HTML 標準 + キーボード代替（ボード。status.md C-08） |

- CSP（`docker/web/nginx.conf`）: `script-src 'self'`、`style-src 'self' 'sha256-…'`（react-aria の固定スタイルのみ）。インラインの `<style>` 要素・`style` 属性の HTML への直書きは違反になる。React の `style` プロパティ（CSSOM 経由）と SVG の表示属性（`fill` など）は既存画面で使用しており、E2E-08 で違反 0 件を確認済み。
- 初回読み込みの JS: 307KB（gzip 95KB）。画面は `lazy` で分割。

### 4.8 Docker・設定（確認済み）

- サービス: db（派生イメージ `taskmanager-db:8.4.11`）→ migrate（efbundle）→ api → web（nginx）。マイグレーションは migrate コンテナが自動適用（NFR-12）。
- テスト用 DB はマイグレーションから生成した SQL を適用する（`MySqlTestServer`）。マイグレーションを追加すれば自動的に反映される。
- Secret は `.env` と環境変数のみ。

## 5. Plane の解析資料のうち関係するもの（参照のみ）

| 資料 | 記録内容 | Phase 9 での扱い |
|---|---|---|
| analysis/feature-map.md F-05、F-13 | Plane は作業項目一覧に 5 レイアウト（ガント含む）、分析画面（`/:ws/analytics/:tab`）を持つ | 機能の存在のみ参考。画面・部品・API・名前は使わない |
| analysis/frontend.md | グラフに recharts、ガントは `BaseGanttRoot` | 同じライブラリ・名前を採用しない（technology.md で評価） |
| analysis/appendix-api-endpoints.md | `analytics`・`advance-analytics-charts`・`project-stats` などの URL | 同じ URL・名前を採用しない |
| analysis/ui-ux.md | レイアウト切り替えはユーザー × プロジェクトでサーバー保存 | TaskManager は URL クエリで保持する方針（OD-25）を継続 |

## 6. 変更前の基準テスト（Phase 9 開始時）

Phase 9 の変更を加える前に、既存のテストを実行した（2026-10-16）。

### 6.1 実行結果

| # | 区分 | コマンド | 環境 | 結果 |
|---|---|---|---|---|
| BL-1 | Backend 全体 | `dotnet test --solution src/TaskManager.sln` | ローカル MySQL 8.0.46（`TM_TEST_MYSQL_CONNECTION`。値は記録しない） | **994 / 994 成功**（Domain・Infrastructure・Application・API すべて合格、5 分 54 秒） |
| BL-2 | Frontend | `npx vitest run`（BL-1 と同時に実行） | jsdom + MSW | 105 / 107（2 件失敗）。失敗はどちらも `Test timed out in 5000ms`（`ProjectTasksPage.test.tsx` の 1 件目、`TaskDetailPage.test.tsx`）。Backend のテストと同時に走らせたため CPU が混み合い、テストの時間制限（5 秒）を超えたもの |
| BL-3 | Frontend | `npx vitest run`（単独で再実行） | 同上 | **107 / 107 成功** |

- BL-2 の失敗は、アプリの不具合ではなく実行環境の負荷による時間切れと判断した（同じコードを単独で実行すると全件成功）。Phase 9 のテスト実行では Backend と Frontend を同時に実行しない。
- E2E（Playwright）は DB を空にして実行する必要があるため（known-issues.md KI-14）、Phase 9 の変更後に Docker 構成で全シナリオ（既存 8 + 追加分）を実行する。

## 7. 調査から分かった前提（impact-analysis.md への入力）

1. タスクの開始日・期限、ステータス、楽観的排他は既にある → **Entity・列の追加は不要**。
2. ガントの日程変更は既存の `PATCH /tasks/{taskNumber}`（`startDate`・`dueDate`・`version`）で実現できる → 権限（EditTasks）・TK-02 の検証・楽観的排他・変更履歴・アーカイブ中の 409 をそのまま使える。
3. 期間と重なるタスクの取得、ステータス別の件数集計（プロジェクト単位・自分の担当単位）は既存 API にない → 新しい参照系 API が必要。
4. 自分の担当の集計（`assignee_user_id` で絞り `status_id` で集計）に合う索引はない（`ix_task_item__assignee_user_id__due_date` のみ）。プロジェクト単位の集計には `ix_task_item__project_id__status_id` がある。
5. グラフ・ガントのライブラリはなく、CSP が厳しい（インラインスタイルの挿入を行うライブラリは使えない）。
6. 新しい操作は、認可マトリクス・OpenAPI スナップショット・Frontend の型生成の 3 か所を同時に更新する必要がある。
