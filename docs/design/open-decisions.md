# 未決定事項・判断記録（Open Decisions）

設計書は下表の「推奨」で書いている。**状態が「承認待ち」の項目は、ユーザーの承認をもって確定する**。却下された場合は該当する設計書を更新してから Phase 3 に進む。

> **2026-10-08 記録**: 承認待ちの 9 件（§1）について明示的な承認・却下の回答がないまま Phase 3 の実行指示を受けたため、**推奨案のまま Phase 3 を進めた**。状態は「承認待ち」のまま残す。Phase 3 の成果物（環境構築）で影響を受けるのは OD-03（OpenAPI の認証スキームを JWT Bearer で設定）のみで、却下された場合も変更は小さい。

状態の凡例:
- **承認待ち**: 推奨案で設計済み。業務判断が必要なためユーザーの確認を求める
- **技術確認待ち**: 推奨案で設計済み。Phase 3（環境構築）で実際に動かして確定する
- **決定**: 設計者が決定し設計に反映済み（変更は可能）
- **将来検討**: MVP の範囲外。記録のみ

## 1. 承認待ち（業務判断）

### OD-24 TaskManager の目的と MVP 範囲
- Decision: 「小〜中規模チームが、誰が・何を・いつまでにを共有し、抜け漏れと期限超過を防ぐ」社内タスク管理に絞り、[requirements.md](requirements.md) §7 の機能を対象外とする
- Alternatives: (A) 推奨案 / (B) Plane に近い多機能（サイクル・モジュール・ページ・受付・分析）/ (C) さらに絞る（マイルストーン・添付・タグなし）
- Evidence: analysis/feature-map.md（機能の重複、C 候補の高コスト）、analysis/ui-ux.md（情報量の多さ）
- Recommendation: A
- Reason: 中核機能を確実に作り、Phase 5〜8（テスト・セキュリティ・完成確認）まで品質を保てる規模にする
- Impact: 全設計書
- Decision status: **承認待ち**

### OD-01 組織構成（ワークスペース階層を持たない）
- Decision: 1 システム = 1 組織。プロジェクトの上位階層を持たない
- Alternatives: (A) 単一組織 / (B) 複数ワークスペース（テナント）
- Evidence: analysis/feature-map.md F-02、analysis/database.md（全表に workspace を冗長保持し、テナント境界のフィルタ漏れが IDOR の原因の一つ）
- Recommendation: A
- Reason: 階層が 1 つ減り画面・権限・テストが単純になる。社内利用では十分
- Impact: DB（`workspace_id` 列なし）、API（`/projects` 直下）、UI。後から B に変える場合は全表への列追加と URL 変更が必要
- Decision status: **承認待ち**

### OD-03 認証方式（JWT + ローテーションするリフレッシュ Cookie）
- Decision: [authentication-authorization.md](authentication-authorization.md) §1
- Alternatives: (A) JWT（メモリ）+ HttpOnly リフレッシュ Cookie / (B) Cookie セッションのみ（ASP.NET Core Cookie 認証）/ (C) JWT を localStorage
- Evidence: analysis/authentication-authorization.md（Plane はセッション + REST API の CSRF 無効）
- Recommendation: A
- Reason: CSRF の影響範囲を refresh / logout に限定し、XSS 時のトークン持ち出しを短時間に限定できる。CLAUDE.md §19 の「JWT」確認項目にも対応
- Impact: Api の認証設定、Frontend の API クライアント
- Decision status: **承認待ち**

### OD-08 メールを使わない（自己登録・メール確認・メールでのパスワード再設定なし）
- Decision: システム管理者がアカウント発行・一時パスワード再発行を行う
- Alternatives: (A) メールなし / (B) SMTP を導入しパスワード再設定メール・通知メールを送る
- Evidence: analysis/external-services.md（SMTP 設定・Secret・失敗処理が必要）
- Recommendation: A（将来 B を追加できるよう `IMailSender` の拡張点のみ設計）
- Reason: 外部依存と Secret を減らし、社内運用で十分な手順にする
- Impact: パスワードを忘れた利用者は管理者への依頼が必要（運用負荷）
- Decision status: **承認待ち**

### OD-04 担当者は 1 名
- Decision: タスクの担当者は 0〜1 名
- Alternatives: (A) 1 名 / (B) 複数名
- Evidence: analysis/database.md（Plane は複数担当）
- Recommendation: A
- Reason: 責任の所在が明確になり、「自分のタスク」・お知らせ・絞り込みが単純になる。複数人で行う仕事はサブタスクに分ける
- Impact: DB（`assignee_user_id` 1 列）、UI
- Decision status: **承認待ち**

### OD-02 プロジェクトを作成できる人
- Decision: 認証済みの全利用者が作成でき、作成者が管理者になる
- Alternatives: (A) 全利用者 / (B) システム管理者のみ / (C) 設定で切替
- Recommendation: A
- Reason: 現場で必要なときにすぐ始められる。不要なプロジェクトはアーカイブで整理
- Impact: `POST /projects` の権限
- Decision status: **承認待ち**

### OD-06 論理削除を採用しない
- Decision: 物理削除 + 確認ダイアログ + 変更履歴。プロジェクトはアーカイブ、利用者は停止
- Alternatives: (A) 物理削除 / (B) 全表に `deleted_at`（ゴミ箱機能）
- Evidence: analysis/database.md（論理削除と条件付き一意制約の組み合わせは MySQL で再現困難）
- Recommendation: A
- Reason: 一意制約を DB で正しく保証できる。誤削除は確認ダイアログで防ぐ
- Impact: 削除したタスクは復元できない
- Decision status: **承認待ち**

### OD-07 添付ファイルを MVP に含める
- Decision: 含める（10MB、許可形式のみ、Docker ボリュームに保存）
- Alternatives: (A) 含める / (B) 含めない（リンクの記載で代替）
- Recommendation: A
- Reason: 業務では資料の受け渡しが頻繁。セキュリティ設計（アップロード・パストラバーサル）も CLAUDE.md §19 の確認対象
- Impact: ストレージ・バックアップ・セキュリティテストの範囲
- Decision status: **承認待ち**

### OD-15 Entity 名 `Project` を例外として使う
- Decision: [naming-rules.md](naming-rules.md) §10
- Alternatives: (A) `Project` / (B) 別名（例 `WorkProject`）
- Recommendation: A
- Reason: 業務の一般名詞であり、別名にすると意味がずれて分かりにくい。責務・属性は独立に設計済み
- Impact: CLAUDE.md §10 の解釈
- Decision status: **承認待ち**

## 2. 技術確認待ち（Phase 3 で確定）

| ID | Decision | Alternatives | Evidence | Recommendation / Reason | Impact | 状態 |
|---|---|---|---|---|---|---|
| OD-05 | 主キーを UUID v7 の `CHAR(36)` にする | `BINARY(16)`、`BIGINT AUTO_INCREMENT` | MySql.EntityFrameworkCore の Guid 既定マッピング（未検証） | CHAR(36): 読みやすく変換不要。性能が問題なら BINARY(16) に変更。BIGINT は推測可能な ID になる | 全表 | 技術確認待ち |
| OD-16 | TypeScript の版 | 7.0.x / 6.0.x / 5.9.x | `openapi-typescript` 7.13.0 の peer 依存が `typescript ^5.x` | **5.9.3 に決定（Phase 3）**。型生成ツールが TS 6/7 に未対応のため | Frontend ツール | 決定 |
| OD-17 | 検索方式と日本語の扱い | `LIKE` / FULLTEXT（ngram）、照合順序 `utf8mb4_0900_ai_ci` / `utf8mb4_ja_0900_as_cs` | NFR-03 の規模 | MVP は `LIKE`（10 万件で計測し、遅ければ ngram FULLTEXT を追加）。ひらがな・カタカナの同一視はしない | 検索性能 | 技術確認待ち |
| OD-26 | OpenAPI 応答検証のライブラリ | JsonSchema.Net 等 / 自作の最小検証 | — | Phase 3 でライセンス・OpenAPI 3.1 対応を確認 | API テスト | **決定（Phase 5）: 自作の最小検証**（依存を増やさない。対応構文は docs/testing/test-design.md §3.6） |
| OD-29 | api コンテナの読み取り専用ルートファイルシステム | 有効 / 無効 | — | .NET の一時ファイル（アップロードのバッファ）を `/tmp` の tmpfs に向けて有効化 | Docker | 技術確認待ち |
| OD-30 | Swagger UI の CSP | パス限定で緩和 / Swagger UI の設定でインライン排除 | Swagger UI はインラインスクリプトを使う場合がある | `/swagger/` のみ限定的に緩和。本番は Swagger 無効のため影響小 | セキュリティヘッダー | 技術確認待ち |
| OD-18 | TLS の終端 | 前段（社内 LB / リバースプロキシ）/ web コンテナで TLS / Caddy 追加 | — | 前段で終端（社内運用の標準的構成）。単体で HTTPS が必要なら web に証明書を渡す設定を追加 | 公開方法、Cookie の Secure | 技術確認待ち（ユーザー環境の確認も必要） |

## 3. 決定（設計者判断。変更は可能）

| ID | Decision | Alternatives | Reason | Impact |
|---|---|---|---|---|
| OD-11 | 手動並べ替え（ドラッグでの順序保存）なし。優先度 → 期限 → 番号で自動整列 | 浮動小数点の並び順列（Plane 方式） | 並び順の保存・競合の複雑さに対し、優先度と期限で十分 | ボードの列内順序 |
| OD-14 | バックグラウンド処理は単一インスタンス前提 | MySQL `GET_LOCK` で排他 | NFR-04（単一サーバー） | スケールアウト時に要対応 |
| OD-19 | デモデータは本番に入れず、開発用シーダーを Phase 5 で用意 | 初回ログイン時にサンプルを作る（Plane 方式） | 利用者が削除する手間をなくす | 初回は空状態の案内で補う |
| OD-20 | ロック中であることを利用者に通知する | 通常の失敗と同じ文言にする | 「なぜログインできないか」が分からない混乱を避ける。5 回失敗後のみ返すため列挙への影響は限定的 | 認証応答 |
| OD-22 | 一覧はページ送り（無限スクロールではない） | 無限スクロール | 件数・位置が分かり URL で共有できる | 表の操作 |
| OD-25 | 一覧の表示条件はサーバーに保存せず URL で保持 | 利用者 × プロジェクトでサーバー保存（Plane 方式） | 表・API・DB が単純になり、共有もしやすい。よく使う条件は「自分の担当」「期限切れ」をワンクリックで | 次回訪問時は既定条件に戻る |
| OD-28 | Markdown 内の画像を表示しない（リンクとして表示） | 添付画像のインライン表示 | 外部画像の読み込み（追跡・CSP 緩和）を避ける | 説明文の表現力 |

| OD-31 | Frontend の実装方針の簡略化（Phase 4）: 画面文言は各コンポーネントに記述し列挙の表示名のみ `constants/labels.ts` に集約、選択・日付はネイティブ部品 | 設計どおり `src/i18n/ja.ts` 集約・React Aria の Select/DatePicker | 日本語のみで翻訳予定がなく、ネイティブ部品は a11y・スマートフォン対応が標準で得られる | 文言の一括見直しが必要になった場合は集約ファイルへの移行が必要 |

OD-05（主キーの型）は Phase 4 のマイグレーション生成で `char(36)` になることを確認した（MySql.EntityFrameworkCore 10.0.9 の既定）。MySQL 上での性能確認は Phase 6。

## 4. 将来検討（MVP 範囲外）

| ID | 内容 | 現時点の判断材料 |
|---|---|---|
| OD-09 | メール通知・Webhook・外部 API キー | analysis/background-processing.md、external-services.md |
| OD-10 | リアルタイム更新（SignalR） | MVP はお知らせ件数の 60 秒ポーリングと画面復帰時の再取得 |
| OD-12 | カレンダー表示、分析ダッシュボード（ステータス以外の集計・時系列集計・折れ線グラフ） | 表・ボード・マイルストーン進捗で開始。**Phase 9 でガントチャートとステータス別円グラフのみ対象に追加**（§6 OD-32〜41）。カレンダー・その他の集計は引き続き将来検討 |
| OD-13 | 監査記録の閲覧画面 | MVP は DB とログのみ |
| OD-21 | ダークテーマ | デザイントークンを CSS 変数にしており追加は容易 |
| OD-23 | 操作の取り消し（元に戻す） | 確認ダイアログと変更履歴で補う |
| OD-27 | CI（GitHub Actions 等）の導入 | Phase 6 では手元で全テストを実行して記録。導入可否はリポジトリの運用に合わせて決める |

## 6. Phase 9（ガントチャート・ステータス別円グラフ）の判断（2026-10-16）

Phase 9 の指示で「open-decisions.md で判断する」とされた事項と、指示から一意に決まらなかった事項。業務判断が必要なものは「承認待ち」とし、推奨案で設計・実装した（Phase 2〜8 と同じ扱い）。根拠の詳細は docs/corrections/phase-09/impact-analysis.md。

### OD-32 タスクの依存関係（先行・後続）をガントで扱うか
- Decision: 扱わない（依存の線・自動の日程調整なし）
- Alternatives: (A) 扱わない / (B) 依存関係の表（タスク × タスク）を追加し、ガントに線を描く / (C) 依存関係を保存し、先行の遅れで後続を自動でずらす
- Evidence: 既存の Entity・表に依存関係はない（investigation.md §4.1）。B・C は新しい表・API・認可・削除時の扱い・循環の検出が必要。Phase 9 の範囲（ガントの表示と日程の変更）を超える
- Recommendation: A
- Reason: 既存の項目（開始日・期限）だけで、日程の重なりと遅れは把握できる。依存は必要になった時点で別の修正として設計する
- Impact: ガントは棒・今日の線・マイルストーンの目印のみ
- Decision status: **承認待ち**

### OD-33 マイルストーンをガントで扱うか
- Decision: 扱う（まとめ方「マイルストーン」と、目標日の ◆ の目印）。マイルストーン自体の期間（開始日）は持たない
- Alternatives: (A) まとめ方 + 目標日の目印 / (B) 扱わない / (C) マイルストーンに開始日を追加し、期間の棒を描く
- Evidence: 既存の Milestone は目標日（`TargetDate`）のみ（domain-model.md §4.7）。利用者の指示に「マイルストーンでのグループ化・切り替え」がある
- Recommendation: A
- Reason: 既存データだけで実現でき、スキーマの変更がない
- Impact: Frontend は既存の `GET .../milestones` を使う。API の追加なし
- Decision status: **承認待ち**

### OD-34 「自分のタスク」の円グラフの集計単位
- Decision: 「すべてのプロジェクト」はステータスの区分（未着手・進行中・完了・中止）で集計し、プロジェクトを選んだときはそのプロジェクトのステータスで集計する
- Alternatives: (A) 推奨案 / (B) 全プロジェクトのステータスを「プロジェクト名: ステータス名」で並べる / (C) 同じ名前のステータスを名前で合算する
- Evidence: ステータスはプロジェクトごとに定義され、名前・色・並び順・数が異なる（investigation.md §4.2、C-06）。区分は既存のステータス定義（`StatusCategory`）の一部
- Recommendation: A
- Reason: B は項目数が最大 20 × プロジェクト数になり円グラフとして読めない。C は同名でも意味が違うステータスを混ぜる。A は既存の定義だけを使い、新しい分類を作らない
- Impact: API #64 の応答に `groupedBy`（`status` / `category`）を持たせる。区分の表示色は既定ステータスの色（未着手 Gray、進行中 Blue、完了 Green、中止 Red）
- Decision status: **承認待ち**

### OD-35 棒のドラッグによる日程変更
- Decision: 採用（移動 = 開始日と期限を同じ日数ずらす、右端の伸縮 = 期限のみ）。キーボード・支援技術では表形式の代替表示の日付欄で同じ変更ができる
- Alternatives: (A) 採用 / (B) 表示のみ（変更はタスク詳細で行う）
- Evidence: 既存の `PATCH /tasks/{taskNumber}` が開始日・期限・`version` を受け付け、権限（EditTasks）・TK-02・楽観的排他・変更履歴・アーカイブ中の 409 を備えている（investigation.md §7）
- Recommendation: A
- Reason: 新しい API・規則を作らずに、遅れの把握から調整までを 1 画面で行える
- Impact: Frontend のみ（ポインター操作）。日程変更の検証はサーバーが最終判断
- Decision status: **承認待ち**

### OD-36 割合（%）の計算を Backend / Frontend のどちらで行うか
- Decision: Backend（Domain の `ShareRounding`）で計算し、API の `percent` で返す。Frontend は表示のみ
- Alternatives: (A) Backend / (B) Frontend で件数から計算
- Recommendation: A
- Reason: 丸め規則（最大剰余法で合計 100.0%）を 1 か所で定義し、Domain の単体テストで保証できる。API の利用者（画面以外も含む）で値が一致する
- Impact: `StatusCountResponse.percent`（number）
- Decision status: 決定（設計者判断。変更可能）

### OD-37 アーカイブ・削除されたタスクを円グラフに含めるか
- Decision: タスクの削除は物理削除（OD-06）のため対象が存在しない。アーカイブはプロジェクト単位: プロジェクトの円グラフはアーカイブ中でも閲覧でき、そのプロジェクトの全タスクを含める。自分のタスクの「すべてのプロジェクト」はアーカイブ中のプロジェクトを除く（既存の「自分のタスク」と同じ）。完了・中止の区分は含める（割合の意味を保つため）
- Alternatives: (A) 推奨案 / (B) 完了・中止を除く / (C) 自分のタスクにアーカイブ中のプロジェクトも含める
- Recommendation: A
- Reason: 既存の画面の対象範囲と一致させ、利用者の混乱を避ける
- Decision status: **承認待ち**

### OD-38 グラフ・ガントのライブラリ
- Decision: 追加しない（SVG + CSS Modules で自作）
- Alternatives / Evidence: technology.md §10（recharts、Chart.js、@nivo/pie、@visx/shape、frappe-gantt、gantt-task-react を評価）
- Recommendation / Reason: CSP（インラインスタイル不可）、必要な機能が小さい、初回読み込みを増やさない、Plane と同じライブラリを避ける
- Decision status: 決定（設計者判断。変更可能）

### OD-39 円グラフの置き場所
- Decision: プロジェクト: タスク画面の表示形式に「円グラフ」（`?view=chart`）。自分のタスク: 表示の切り替え「ステータス別の割合」（`/?view=chart`）
- Alternatives: (A) 推奨案 / (B) プロジェクトに新しいタブ「概要」を作る / (C) 一覧の上に常に表示
- Evidence: SKILL.md「円グラフは【自分のタスク】と【プロジェクト】のページに表示」、Phase 9 の CLAUDE.md「円グラフビュー」。既存の表示形式の切り替え（表 / ボード）と URL クエリでの保持（OD-25）
- Recommendation: A
- Reason: 既存の切り替えの部品と URL の規則をそのまま使え、画面（タブ）を増やさない。C は一覧の情報を押し下げる
- Decision status: **承認待ち**

### OD-40 ガントの表示期間と上限
- Decision: 日 = 14 日、週 = 12 週（月曜始まり）、月 = 12 か月。API は最大 366 日・500 件（超過は `truncated`）、日付未設定は件数 + 先頭 50 件
- Recommendation / Reason: 画面幅で読める列数（日 14 列・週 12 列・月 12 列）と、NFR-03（1 プロジェクト 10,000 件規模）での応答サイズの抑制
- Decision status: 決定（設計者判断。変更可能）

### OD-41 円グラフの凡例から一覧への移動
- Decision: プロジェクトの円グラフと、プロジェクトを選んだ自分のタスクの円グラフでは、ステータス名を表（SCR-06）へのリンクにする（`statusId` で絞り込み。完了・中止の区分は「完了・中止も表示」を付ける。自分のタスクは「担当者: 自分」も付ける）。区分別の集計（全プロジェクト）ではリンクにしない
- Alternatives: (A) 推奨案 / (B) 扇形のクリックで移動 / (C) 移動しない
- Reason: 表のリンクはキーボード・スクリーンリーダーでも同じ操作ができる。扇形のクリックだけに機能を持たせない
- Decision status: 決定（設計者判断。変更可能）

## 5. Phase 1 の未解決事項（analysis/open-questions.md）の扱い

Phase 1 の不明点は Plane の内部仕様に関するもので、TaskManager の設計は Plane の挙動に依存しないよう独自に決定した。このため **追加の Plane 解析は行っていない**。

| Phase 1 の質問 | Phase 2 での扱い |
|---|---|
| Q-ARC-01 キャッシュ / Q-BG-01 結果バックエンド | Redis・キューを採用しないため不要 |
| Q-ARC-02 リアルタイム | OD-10 |
| Q-DB-01 型の一致 | OpenAPI から型生成する設計で解決（testing-strategy §6） |
| Q-DB-02 論理削除の波及 | OD-06（論理削除を採用しない） |
| Q-API-01 内部/公開 API の差 | API を 1 系統にする |
| Q-API-02 rich_filters | 採用しない（クエリパラメータのフィルタのみ） |
| Q-API-03 200/204 | 更新は 200 + 本文に統一（削除・既読などの本文不要な操作のみ 204） |
| Q-AUTH-01〜03 | TaskManager 独自の認証設計で解決 |
| Q-FE-01 / 02 | ui-design §7.3、§7.6 で決定 |
| Q-BG-02 通知ルール | requirements REQ-NTC-01 で独自に決定 |
| Q-EXT-01〜03 | 外部サービスを使わない / 添付は非公開ボリューム |
| Q-INF-01 / 02 | architecture §7 で独自に決定 |
| Q-TEST-01 / 02 | 不要（独自のテスト戦略） |
| Q-UX-01 / 02 | ux-design §8、ui-design §8 で独自の目標を設定 |
