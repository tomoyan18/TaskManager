# 機能マップ（Plane `preview`）

## 目的

Plane の主要機能を、画面・Frontend・API・Backend・DB・認可・非同期処理・テストの対応と、機能間の依存関係とともに整理する。TaskManager でどの機能を採用するかは Phase 2 で決定する。

## 根拠

- 画面ルート: `apps/web/app/routes/core.ts`
- API: [appendix-api-endpoints.md](appendix-api-endpoints.md)
- Model: `apps/api/plane/db/models/`
- テスト: `apps/api/plane/tests/`
- 詳細チェーンは [api.md](api.md)、[database.md](database.md)、[frontend.md](frontend.md)

## 機能間の依存関係

```text
ユーザー / 認証
  └─ ワークスペース（テナント境界、メンバー・招待・ロール）
       └─ プロジェクト（メンバー・ロール・識別子・機能 ON/OFF）
            ├─ 状態（ワークフロー）  ─┐
            ├─ ラベル                ├─> 作業項目（中核）
            ├─ 見積もり             ─┘     ├─ コメント / リアクション / メンション
            │                              ├─ 添付ファイル（ストレージ）
            │                              ├─ リンク / 関係 / サブ作業項目
            │                              ├─ 購読 → 通知（アプリ内 / メール）
            │                              └─ 履歴（アクティビティ）
            ├─ サイクル（期間で作業項目を束ねる）
            ├─ モジュール（機能単位で作業項目を束ねる）
            ├─ ビュー（フィルタの保存）
            ├─ 受付（Intake: 外部・未整理の作業項目を審査）
            ├─ ページ（Wiki、共同編集）
            └─ 公開ボード（Space）
  横断: 検索 / お気に入り / 最近見た項目 / ホーム / 分析 / エクスポート / Webhook / API Token / 付箋 / 下書き
```

## 機能一覧

凡例 — 優先度候補: A = TaskManager の中核候補、B = 有用だが後回し可、C = Plane 固有・高コストで要否を慎重に判断（**候補であり決定ではない**）

### F-01 認証・アカウント（候補 A）

- 概要: サインイン / サインアップ / サインアウト、パスワード設定・変更・リセット、Magic code、OAuth（Google / GitHub / GitLab / Gitea）、プロフィール、オンボーディング。
- 画面: `/`（サインイン）、`/sign-up`、`/accounts/forgot-password`、`/accounts/reset-password`、`/accounts/set-password`、`/onboarding`、`/settings/profile/:tab`
- API: `/auth/*`、`/api/users/me/*`
- DB: `users`、`profiles`、`accounts`、`sessions`
- 認可: 未認証可（認証系）、本人のみ（プロフィール）
- 非同期: パスワードリセットメール、Magic code メール、ユーザー有効化/無効化メール
- テスト: `contract/app/test_authentication.py`、`contract/api/test_authentication.py`
- 参考点: 状態に応じた遷移（未オンボーディング → オンボーディング、ワークスペースなし → 作成画面）
- 再設計: 認証方式（[authentication-authorization.md](authentication-authorization.md)）、ユーザー列挙の防止

### F-02 ワークスペース・メンバー・招待（候補 A）

- 概要: ワークスペース作成（slug）、設定、メンバー一覧・ロール変更・退出、メール招待・受諾。
- 画面: `/create-workspace`、`/workspace-invitations`、`/invitations`、`/:ws/settings`、`/:ws/settings/members`
- API: `/api/workspaces/`、`/api/workspaces/{slug}/members/`、`/invitations/`、`/api/users/me/workspaces/invitations/`
- DB: `workspaces`、`workspace_members`、`workspace_member_invites`
- 認可: 更新・削除は Admin
- 非同期: 招待メール、作成時のデモデータ投入
- テスト: `test_workspace_app.py`、`unit/models/test_workspace_model.py`
- 参考点: slug 予約語・重複事前確認 API
- 再設計: 単一組織前提ならワークスペース階層自体の要否

### F-03 プロジェクト・プロジェクトメンバー（候補 A）

- 概要: プロジェクト作成（名前・識別子・公開範囲・リード・既定担当者）、メンバー管理、参加、アーカイブ、機能 ON/OFF（サイクル / モジュール / ビュー / ページ / 受付）、自動アーカイブ・自動クローズ設定。
- 画面: `/:ws/projects`、`/:ws/settings/projects/:id/*`（一般、メンバー、機能、状態、ラベル、見積もり、自動化）
- API: `/api/workspaces/{slug}/projects/`、`.../members/`、`.../invitations/`、`.../join/`、`.../archive/`、`project-identifiers/`
- DB: `projects`、`project_members`、`project_member_invites`、`project_identifiers`、`project_user_properties`
- 認可: 作成はワークスペース Admin/Member、設定変更はプロジェクト Admin
- 非同期: 招待メール、Webhook、自動アーカイブ / クローズ（日次）
- テスト: `test_project_app.py`、`test_project_member_is_active_authz.py`、`test_project_join_cross_project_activation.py`
- 参考点: 識別子（例: 英大文字数文字）+ 連番で作業項目を人が呼べる
- 再設計: 公開範囲（Secret / Public）の意味、ロール体系

### F-04 状態（ワークフロー）（候補 A）

- 概要: プロジェクト毎の状態を 6 グループに分類して定義、既定状態、並び順、色。
- 画面: プロジェクト設定 > 状態
- API: `.../states/`、`.../states/{id}/mark-default/`
- DB: `states`
- 参考点: 名前は自由、集計・完了判定はグループで行う設計
- 再設計: Triage グループ（受付専用）の扱い

### F-05 作業項目（中核、候補 A）

- 概要: 作成・編集・削除・アーカイブ、タイトル、リッチテキスト説明、状態、優先度、担当者（複数）、ラベル（複数）、開始日・期限、見積もり、親子（サブ作業項目）、関係（ブロック等）、リンク、添付、コメント、リアクション、購読、履歴、バージョン、下書き、一括操作（日付・削除・アーカイブ）。
- 画面: `/:ws/projects/:id/issues`（リスト / カンバン / カレンダー / ガント / スプレッドシートの 5 レイアウト）、詳細 `/issues/:issueId`、ピーク表示（一覧上のサイドパネル）、`/:ws/browse/:workItem`（識別子でアクセス）、作成モーダル
- Frontend: `components/issues/**`、`store/issue/**`、`services/issue/*.service.ts`
- API: [api.md](api.md) 代表チェーン 1
- DB: `issues` ほか 15 テーブル以上
- 認可: [authentication-authorization.md](authentication-authorization.md) の権限マトリクス
- 非同期: 履歴、通知、メンション抽出、Webhook、説明文バージョン、最近見た項目、リンクのメタデータ取得
- テスト: `contract/api/test_issues.py`、`test_issue_assignee_label_validation.py`、`test_issue_attachment_size.py`、`test_issue_notifications.py`、`test_issue_history_app.py`、`test_issue_list_guest_scope_app.py`、`test_sub_issue_cross_project_scope_app.py`、`unit/utils/test_issue_filters.py` ほか
- 参考点: 必要な属性セット、一覧と詳細の分離、履歴の差分記録、表示 ID
- 再設計: 説明文の多重保存（JSON / HTML / binary / stripped）、レイアウト数、関係種別の数

### F-06 サイクル（スプリント）（候補 B）

- 概要: 期間を持つ作業項目の束。進捗・バーンダウン、未完了項目の次サイクルへの移送、アーカイブ。1 作業項目は同時に 1 サイクル。
- 画面: `/projects/:id/cycles`、`/cycles/:cycleId`、`/:ws/active-cycles`
- API: `.../cycles/`、`cycle-issues/`、`transfer-issues/`、`progress/`、`analytics/`、`date-check/`
- DB: `cycles`、`cycle_issues`、`cycle_user_properties`
- テスト: `test_cycles.py`、`test_cycle_issue_app.py`
- 参考点: 期間重複チェック（date-check）、終了後は追加不可
- 再設計: 期間の型（日時か日付か）、タイムゾーン

### F-07 モジュール（候補 B）

- 概要: 機能・成果物単位の束。状態（6 値）、リード、メンバー、リンク、期間。1 作業項目は複数モジュールに所属可。
- API: `.../modules/`、`modules/{id}/issues/`、`module-links/`
- DB: `modules`、`module_issues`、`module_members`、`module_links`
- テスト: `test_module_issue_destroy_app.py`、`test_modules_lite.py`
- 再設計: サイクルとの役割の重なり（「マイルストーン」1 つに統合できるか）

### F-08 ビュー（保存フィルタ）（候補 B）

- 概要: フィルタ・表示設定を名前付きで保存。プロジェクトビューとワークスペースビュー、公開/非公開、ロック。
- 画面: `/projects/:id/views`、`/:ws/workspace-views`
- API: `.../views/`、`/api/workspaces/{slug}/views/`、`/api/workspaces/{slug}/issues/`（横断一覧）
- DB: `issue_views`
- 参考点: 横断一覧（自分の担当、作成した項目など）は業務上よく使われる

### F-09 受付（Intake）（候補 C）

- 概要: 外部・メンバー以外からの起票を審査（受理 / 却下 / 保留 / 重複）してから本番に入れる。
- API: `.../intake-issues/`、公開ボード経由の起票
- DB: `intakes`、`intake_issues`
- 再設計: 必要なら「承認待ち」状態で簡略化できる

### F-10 ページ（Wiki・共同編集）（候補 C）

- 概要: リッチテキスト文書、階層、ロック、アーカイブ、バージョン、複製、リアルタイム共同編集（live サーバー）、PDF エクスポート。
- API: `.../pages/*`、live の WebSocket
- DB: `pages`、`project_pages`、`page_versions`、`page_logs`、`page_labels`
- 再設計: 共同編集は実装コストが非常に高い

### F-11 通知（候補 A〜B）

- 概要: アプリ内通知（既読・未読、アーカイブ、スヌーズ、メンションのみ表示）、メール通知（5 分毎まとめ）、通知設定。
- 画面: `/:ws/notifications`
- API: `/api/workspaces/{slug}/users/notifications/*`、`/api/users/me/notification-preferences/`
- DB: `notifications`、`user_notification_preferences`、`email_notification_logs`
- 非同期: `notifications`、`stack_email_notification`（5 分毎）
- テスト: `test_issue_notifications.py`

### F-12 検索・お気に入り・最近見た項目・ホーム（候補 B）

- 検索: `/api/workspaces/{slug}/search/`、`entity-search/`、`search-issues/`（部分一致）
- お気に入り: フォルダ付き、`user_favorites`
- 最近見た項目: `user_recent_visits`（詳細表示時に非同期記録）
- ホーム: ウィジェット（クイックリンク、最近、付箋）の並び・表示設定

### F-13 分析・ダッシュボード（候補 B）

- `/:ws/analytics/:tab`、`advance-analytics*`、`project-stats`、ユーザー別の活動グラフ・完了グラフ
- エクスポート（CSV / XLSX）

### F-14 エクスポート（候補 B）

- `POST /api/workspaces/{slug}/export-issues/` → 非同期で CSV / JSON / XLSX を S3 に作成 → `exporters` に履歴。期限切れリンクを日次削除。
- テスト: `unit/utils/test_csv_export_sanitization.py`、`test_xlsx_export_sanitization.py`（CSV インジェクション対策）

### F-15 Webhook・API Token（候補 C）

- Webhook: ワークスペース単位、イベント種別（project / issue / module / cycle / comment）、HMAC 署名、再試行、ログ、自動無効化メール、SSRF 対策。
- API Token: ユーザー毎、有効期限、最終利用日時。

### F-16 公開ボード（Space）（候補 C）

- プロジェクト等を匿名公開し、コメント・リアクション・投票・受付起票を許可（設定で ON/OFF）。

### F-17 インスタンス管理（Admin、候補 C）

- 認証方式の有効化、SMTP、AI、画像（Unsplash）、ワークスペース作成制限。`/god-mode/`。

### F-18 その他

- 付箋（Stickies、個人メモ）、下書き（ワークスペース単位）、テーマ、多言語（21 言語、ja 含む）、AI アシスタント（LLM）、Unsplash 画像検索、キーボードショートカット・コマンドパレット。

## TaskManager で参考にすべき点（総括）

- 作業項目を中心に、状態グループ・優先度・担当者・期限・ラベル・親子・コメント・履歴という最小構成が業務の核。
- 「自分の担当」「期限切れ」などの横断一覧と、保存フィルタの需要。
- 権限はワークスペース / プロジェクトの 2 階層。

## TaskManager で再設計が必要な点（総括）

- 機能数が非常に多く、同じ目的の機能が重複（サイクル / モジュール、ページ / 説明文、受付 / Triage 状態）。
- 共同編集、公開ボード、Webhook、インスタンス管理はコストが高く、要件として必要かを先に判断すべき。

## Phase 2 で決定する事項

- TaskManager の MVP 機能範囲（A/B/C 候補の確定）
- 用語（作業項目 / タスク、サイクル / スプリント 等）の日本語での命名
- 機能の統合・簡略化方針
