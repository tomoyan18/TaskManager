# Phase 9 確認結果（verification-report）

- 実施日: 2026-10-16
- 対象: ガントチャート機能とビュー、ステータス別円グラフ機能とビュー
- 環境: macOS 15.8.1 / .NET SDK 10.0.401 / Node.js 24.10.0 / Docker Desktop（Engine 29.5.2）/ ローカル MySQL 8.0.46（Homebrew）/ コンテナ MySQL 8.4.11 / Playwright（Chromium）
- 本書の「成功」は、記載したコマンドを実際に実行して得た結果だけを指す。ローカル MySQL の接続情報は環境変数 `TM_TEST_MYSQL_CONNECTION` でのみ渡し、記録していない。

## 1. 結果の要約

| 区分 | コマンド・方法 | 環境 | 結果 |
|---|---|---|---|
| 変更前の基準（Backend） | `dotnet test --solution src/TaskManager.sln` | ローカル MySQL 8.0.46 | 994 / 994 成功（investigation.md §6） |
| 変更前の基準（Frontend） | `npx vitest run`（単独） | jsdom + MSW | 107 / 107 成功（investigation.md §6） |
| Backend ビルド | `dotnet build src/TaskManager.sln` | ローカル | 警告 0・エラー 0（警告はエラー扱い） |
| Backend テスト（変更後） | `dotnet test --solution src/TaskManager.sln` | ローカル MySQL 8.0.46 | **1,079 / 1,079 成功**（Domain 162・Application 232・Infrastructure 57・API 628。追加 85） |
| Backend テスト（変更後） | 同上（`TM_TEST_MYSQL_CONNECTION` なし = Testcontainers） | MySQL 8.4.11 | **1,079 / 1,079 成功**（11 分 3 秒） |
| Frontend 型チェック・lint・ビルド | `npx tsc -b`、`npx oxlint --deny-warnings`、`npm run build` | ローカル | 型エラー 0・警告 0・ビルド成功。初回読み込みの JS 307.75KB（gzip 95.4KB。変更前 307.6KB） |
| Frontend テスト | `npx vitest run` | jsdom + MSW | **144 / 144 成功**（追加 37） |
| E2E 型チェック | `npx tsc --noEmit`（task-manager-e2e） | ローカル | 型エラー 0 |
| Docker Compose | 空のボリュームから `docker compose down -v` → `up -d --build` | Docker Desktop | db healthy → migrate が `InitialSchema`・`AddTaskStatusSummaryIndex` を適用して Exited (0) → api healthy → web healthy |
| E2E | `TM_E2E_BASE_URL=http://localhost:8080 npx playwright test`（空の DB から） | Docker 構成（nginx + API + MySQL 8.4.11） | **9 / 9 成功**（既存 8 + E2E-09。axe の重大な違反なし、CSP 違反なし） |
| Swagger UI / OpenAPI | 開発用の上書き構成で Swagger UI を操作（§4） | Docker 構成 + `docker-compose.dev.yml` | 64 操作・17 タグ。追加 3 操作の表示と実行を確認 |
| 索引の利用 | `EXPLAIN`（5 万件のテスト用 DB、§5） | ローカル MySQL 8.0.46 | 集計はいずれも索引だけで実行（Using index） |
| 依存関係 | `npm audit`（web・e2e）、`dotnet list package --vulnerable --include-transitive` | ローカル | いずれも 0 件（パッケージの追加なし） |
| Secret | リポジトリの全ファイル（`.claude/`・ルート `CLAUDE.md` を除く 367 件）と `.env`・テスト用 DB 接続の Secret の値 6 件を照合 | ローカル | 一致 0 件 |
| 独立性 | 追加・変更したファイルで `plane`・`recharts`・`analytics` 等を検索。Plane のリポジトリの状態 | ローカル | 該当 0 件。Plane は `preview` のまま変更 0 件 |
| ルートの CLAUDE.md | 更新日時の確認 | ローカル | 2026-10-08 から変更なし |

## 2. 実行の記録

### 2.1 Backend（ローカル MySQL 8.0.46）

| # | 内容 | 結果 | 失敗の原因と対応 |
|---|---|---|---|
| B-1 | 変更前の基準（全体） | 994 / 994 成功 | — |
| B-2 | 追加した Domain テスト（Domain 全体） | 162 / 162 成功 | — |
| B-3 | 追加した Application テスト（`TaskScheduleServiceTests`・`TaskStatusSummaryServiceTests`） | 16 / 17（1 件失敗） | **テストデータの不備**: 「保留」（区分: 進行中）を追加したうえで区分「進行中」のタスクを作ったため、テスト補助の `CreateTaskAsync` が「対応中」と「保留」のどちらを選ぶかが定まらなかった。テストで対象のタスクを明示的に「保留」に変更するよう修正（確認内容は変えていない） |
| B-4 | B-3 の再実行 | 17 / 17 成功 | — |
| B-5 | Infrastructure の `SchemaTests` | 19 / 19 成功 | — |
| B-6 | 追加した API テスト（`TaskInsightApiTests`）と `OpenApiDocumentTests` | 36 / 36 成功 | — |
| B-7 | 全体（変更後） | **1,079 / 1,079 成功**（5 分 19 秒） | — |

### 2.2 Backend（Testcontainers、MySQL 8.4.11）

| # | 内容 | 結果 |
|---|---|---|
| B-8 | 全体（`TM_TEST_MYSQL_CONNECTION` を設定せず、Testcontainers の `mysql:8.4.11` で実行。テスト用 DB はマイグレーション 2 件から生成した SQL を適用） | **1,079 / 1,079 成功**（11 分 3 秒） |

### 2.3 Frontend

| # | 内容 | 結果 | 失敗の原因と対応 |
|---|---|---|---|
| F-1 | 変更前の基準（Backend と同時に実行） | 105 / 107 | テストの時間制限（5 秒）切れ 2 件。CPU の混雑によるもので、単独で再実行すると 107 / 107（investigation.md §6）。以後は Backend と同時に実行しない |
| F-2 | 変更後の既存テスト | 107 / 107 成功 | — |
| F-3 | `schedule.test.ts` | 16 / 16 成功 | — |
| F-4 | `StatusPieChart.test.tsx` | 6 / 6 成功 | — |
| F-5 | `TaskScheduleView.test.tsx` | 11 / 11 成功 | — |
| F-6 | `MyTasksPage.test.tsx`（追加分を含む） | 6 / 7（1 件失敗） | **テストの待ち合わせ不足**: 「対象」の選択肢（参加中のプロジェクト）の読み込みを待たずに選んでいた。選択肢の表示を待ってから選ぶよう修正 |
| F-7 | F-6 の再実行 | 7 / 7 成功 | — |
| F-8 | `npm run build` | **失敗**（型エラー 1 件） | テストコードで `NodeListOf` をスプレッド構文で配列にしていた（`lib` に `DOM.Iterable` がない設定）。`Array.from` に変更 |
| F-9 | `npm run build`（修正後） | 成功 | — |
| F-10 | 画面の確認で見つけた表示の改善（§3）を反映後、全体 | **144 / 144 成功** | — |

### 2.4 E2E（Docker 構成）

| # | 内容 | 結果 | 失敗の原因と対応 |
|---|---|---|---|
| E-1 | 空の DB から 9 シナリオ | 9 / 9 成功（1.8 分） | — |
| E-2 | §3 の表示の改善を反映したイメージで、空の DB から 9 シナリオ | **8 番目で失敗**（E2E-08 のログインが 60 秒でタイムアウト。E2E-09 は未実行） | **ログインの回数制限（IP あたり 10 回/分）に達した**: 画面に「しばらく時間をおいてから再度お試しください。」（429）。E2E はシナリオ全体で同じ送信元から 11〜12 回ログインし、1 分の区切りの位置によって上限に達する（Phase 9 の機能とは無関係の既存の性質。E-1 は区切りの位置により成功）。**制限は緩めず**、E2E の `login` 補助関数を「回数制限の表示が出たら 61 秒待って 1 回だけやり直す」ように変更（`frontend/task-manager-e2e/tests/helpers.ts`） |
| E-3 | E-2 の修正後、空の DB から 9 シナリオ | **9 / 9 成功**（2.5 分。E2E-08 は 1.3 分: 回数制限に達して待機・やり直しが発生し、成功） | — |

E2E-09 で確認したこと: ガントの表示（週・日・月の切り替え、期間の表示、今日の線）、「表で見る」の開始日の入力での日程変更（トースト）、マウスのドラッグで棒を 2 日分移動して保存（12/23〜12/27 に変わること）、プロジェクトの円グラフ（確認待ち 1 件 100.0%、0 件のステータスの表示）、ステータス名から絞り込み済みの表への移動、自分の円グラフ（区分別。進行中 1 件 100.0%）、各画面の axe（WCAG 2.1 A/AA の serious・critical なし）、CSP 違反 0 件。

## 3. 画面での確認と、その結果の修正

README 用のキャプチャ（Docker 構成、空の DB に架空のデータを作成）を目視で確認し、次の 2 点を直した。

| 気付いた点 | 修正 |
|---|---|
| 円グラフの表で、件数・割合の列が右寄せにならない（共通の `.tm-table td` の左寄せの方が CSS の詳細度が高かった） | `StatusPieChart.module.css` で `.tableWrap .number` として優先させた |
| 月の表示では短い棒の中のステータス名が「未…」と省略され、色以外でステータスが分かりにくい | ガントの行の見出し（日程の文の後）にステータス名を文字で表示した（`TaskScheduleView.tsx`） |

修正後に Frontend テスト（144 件）、Docker イメージの作り直し、E2E（E-3）、キャプチャの撮り直しを行った。キャプチャ取得中のブラウザのエラー・CSP 違反は 0 件。

## 4. Swagger / OpenAPI

| 確認 | 方法 | 結果 |
|---|---|---|
| 契約と実装の一致 | ビルド時生成の文書と既存のスナップショットの差分 | 追加 3 操作（`GetTaskSchedule`・`GetTaskStatusSummary`・`GetMyTaskStatusSummary`）と追加 4 スキーマのみ。既存の操作・スキーマの変更なし。スナップショットを更新し、実行中の API の文書との一致をテスト（`OpenApiDocumentTests`）で確認 |
| 文書の構成 | `OpenApiDocumentTests` | 64 操作すべてに tags・summary・operationId、bearerAuth、401・500、エラーは `application/problem+json`。`from`・`to` は `required: true`、`projectKey` は pattern 付き |
| 秘密情報 | 文書の文字列検査（`plane`・`signingkey`・`connectionstring`・`password=`・内部パス・`exception`・`mysql`） | 含まれない |
| Swagger UI | 開発用の上書き構成（`docker-compose.dev.yml`）で Playwright から操作 | タイトル「TaskManager API」、操作 64、タグ 17、追加 3 操作が表示される |
| 認証 | Authorize 前に `GetTaskStatusSummary` | 401（`auth.unauthenticated`） |
| 実行 | Authorize 後に `GetTaskSchedule`（10/5〜12/27）、`GetTaskStatusSummary`（SALES）、`GetMyTaskStatusSummary` | 200（応答はスキーマどおり） |
| エラー | `GetTaskSchedule` の期間 2026-10-05〜2028-01-01、`GetTaskStatusSummary` の存在しないプロジェクト | 400 `validation.failed`、404 `resource.not_found`（Problem Details） |
| 認可 | 認可マトリクス（3 操作 × 8 種類の呼び出し元 = 24 ケース）、IDOR テスト | 成功（B-7） |

## 5. DB（索引の利用）

5 万件（うち 1 プロジェクトに 1 万件、自分の担当 12,517 件、20 プロジェクト）のテスト用 DB（`tm_test_explain9`。確認後に削除）で `EXPLAIN` を実行した。SQL は Application の LINQ と同じ条件。

| クエリ | 使われた索引 | 備考 |
|---|---|---|
| プロジェクトのステータス別件数（#63: `project_id = ? GROUP BY status_id`） | `ix_task_item__project_id__status_id`（既存） | Using index（表の行を読まない） |
| 自分の担当・区分別（#64: `assignee_user_id = ?` + 参加中・非アーカイブのプロジェクト、`workflow_status` と結合して `GROUP BY category`） | `ix_task_item__assignee_user_id__project_id__status_id`（Phase 9 で追加） | Using index |
| 自分の担当・プロジェクト指定（#64 `projectKey`） | 同上 | Using index |
| ガントの期間（#62: 未完了のステータス × `COALESCE` の期間条件、`ORDER BY` + `LIMIT 501`） | `ix_task_item__status_id`（未完了のステータスごとに参照） | 1 プロジェクト 1 万件規模で、見積もり 900 行程度の並べ替え。NFR-03 の規模で許容 |

性能（応答時間）の計測は行っていない（NFR-01 相当の計測は Phase 9 の範囲外。件数・期間の上限で応答の大きさを抑えている: NFR-15）。

## 6. 要件ごとの確認

| 要件 | 確認したテスト・方法 |
|---|---|
| ガント: 開始日〜期限の横棒 | Domain `ScheduleWindowTests`、Frontend `schedule.test.ts`・`TaskScheduleView.test.tsx`、E2E-09 |
| ガント: 日 / 週 / 月 の切り替え | Frontend（取得する期間が変わる）、E2E-09 |
| ガント: 前へ / 次へ / 今日 | Frontend（`shiftStart`・期間の取得）、E2E-09（今日の線） |
| ガント: 今日の線 | Frontend（`todayOffset`・表示）、E2E-09 |
| ガント: 日付未設定の別枠 | Application（件数・一覧）、API、Frontend |
| ガント: 期限切れの区別（色だけに頼らない） | Application（タイムゾーンでの判定）、Frontend（⚠・「◯日超過」・枠） |
| ガント: 担当者 / ステータス / マイルストーンでまとめる | Frontend `schedule.test.ts`・画面テスト |
| ガント: ドラッグでの日程変更（権限・開始日 ≤ 期限・楽観的排他） | API（既存の更新 API での移動・409・TK-02）、Frontend（閲覧者には部品なし・開始日より前の期限を送らない）、E2E-09（マウスのドラッグ） |
| ガント: キーボードの代替・表形式の代替 | Frontend（表の日付欄で版付きの保存）、E2E-09（表の開始日の入力）、axe |
| ガント: 期間の上限 | Domain・Application・API（366 日 / 367 日）、Frontend（月の期間が 366 日以内） |
| ガント: タスク詳細へのリンク | Frontend、E2E-09 |
| 円グラフ: 既存のステータス定義で件数と割合 | Application・API（名前・色・並び順・0 件）、E2E-09 |
| 円グラフ: 0 件のステータスも凡例に | Application・Frontend・E2E-09 |
| 円グラフ: 合計件数 | Application・Frontend |
| 円グラフ: 丸め方と合計 100% | Domain `ShareRoundingTests`、Application・API |
| 円グラフ: 空のプロジェクト | Application・Frontend |
| 円グラフ: アーカイブ・削除の扱い | Application（アーカイブ中のプロジェクトは参照可・自分の全体集計から除外） |
| 円グラフ: 凡例から絞り込んだ一覧へ | Frontend（リンク先）、E2E-09（移動） |
| 円グラフ: 色だけに頼らない・表の代替・スクリーンリーダー | Frontend（表・要約の文）、axe |
| 円グラフ: 自分のタスク・プロジェクトのページ | Frontend・E2E-09 |
| 認可（非メンバー 404・IDOR） | Application・API（認可マトリクス・`TaskInsightApiTests`） |
| 集計を DB で行う・索引 | Application（MySQL 上で実行）、Infrastructure（索引の存在）、§5（EXPLAIN） |
| 動的 SQL の文字列連結なし | コード検索（生 SQL・文字列連結なし） |
| マイグレーションの適用 | Infrastructure（適用順）、Docker（migrate のログ・`__EFMigrationsHistory`） |

## 7. 完了判定

| Phase 9 の完了条件（`.claude/skills/phase-09-correction/CLAUDE.md`） | 判定 | 根拠 |
|---|---|---|
| ガントチャートと、ステータス別の円グラフが設計どおりに動作する | 満たす | §6 の要件ごとの確認、E2E-09（Docker 構成の実画面） |
| 設計書・Swagger・実装が一致している | 満たす | 設計書（api-design.md #62〜#64 ほか）→ 実装 → ビルド時生成の OpenAPI → スナップショット（差分は追加分のみ）→ 実行中の API との一致テスト、Frontend の型生成と型チェック（§4） |
| 既存テストと追加テストがすべて通過している | 満たす | Backend 1,079 / 1,079（ローカル MySQL 8.0.46・MySQL 8.4.11 の両方）、Frontend 144 / 144、E2E 9 / 9。既存テストの削除・無効化なし（期待値の変更は操作数 61 → 64 のみ） |
| Docker Compose で起動し、Migration がある場合は適用される | 満たす | 空のボリュームから起動し、migrate が `AddTaskStatusSummaryIndex` を適用（`__EFMigrationsHistory` に 2 件、索引の存在を確認）。全サービス healthy |
| 修正記録が作成されている | 満たす | investigation.md・impact-analysis.md・change-log.md・本書 |
| README に実画面のキャプチャ（ガント・円グラフ） | 満たす | `docs/images/09〜12-*.png`（Docker 構成で起動した画面） |

残っている事項（完了を妨げないもの）:

- 業務判断が必要な事項は open-decisions.md §6（OD-32〜OD-41）に記録し、「承認待ち」は推奨案で実装した。却下された場合は該当の設計書と実装を変更する（known-issues.md KI-23）。
- 応答時間の計測（NFR-01 相当）は行っていない（§5）。
- ルートの `CLAUDE.md` 等の MySQL のパスワードの扱いは Phase 8 から変わらず利用者の対応待ち（known-issues.md KI-01）。

**Phase 9 STATUS: COMPLETE**
