# データ・シード解析（Plane `preview`）

## 目的

Plane の Seed / デモデータ / サンプルユーザー / 既定設定 / 初期構成の仕組みを把握する。Plane の実データ・メールアドレス・文言・サンプル内容は TaskManager に流用しない（CLAUDE.md §10）。TaskManager のデータは Phase 2 以降に新規設計する。

## 解析範囲と根拠

- `apps/api/plane/seeds/data/*.json`（構造＝キー名と件数のみ確認。内容は転記しない）
- `apps/api/plane/bgtasks/workspace_seed_task.py`
- `apps/api/plane/app/views/workspace/base.py`（シード起動箇所）
- `apps/api/plane/db/models/state.py`（`DEFAULT_STATES`）、`project.py` / `workspace.py`（既定 JSON）
- `apps/api/plane/db/management/commands/create_dummy_data.py`、`bgtasks/dummy_data_task.py`、`create_instance_admin.py`
- `apps/api/plane/license/management/commands/{register_instance,configure_instance}.py`（存在確認）
- `apps/api/plane/utils/instance_config_variables/core.py`

## 1. ワークスペース作成時のデモデータ（確認済み）

- 起点: `POST /api/workspaces/` 成功時に `workspace_seed.delay(workspace_id)`（`app/views/workspace/base.py:137`）。
- 処理: ボットユーザー（`is_bot=True`、`bot_type=WORKSPACE_SEED`、メールはワークスペース ID と `WEB_URL` のホスト名から生成）を作成し、以下を JSON から作成。

| ファイル | 件数 | キー（構造のみ） |
|---|---|---|
| `projects.json` | 1 | id, name, identifier, description, network, logo_props, cover_image |
| `states.json` | 5 | id, project_id, name, color, group, sequence, default |
| `labels.json` | 2 | id, project_id, name, color, sort_order |
| `issues.json` | 7 | id, project_id, name, description_html, description_stripped, priority, state_id, labels, sequence_id, sort_order, cycle_id, module_ids |
| `cycles.json` | 2 | id, project_id, name, sort_order, timezone, type |
| `modules.json` | 3 | id, project_id, name, description, status, sort_order |
| `pages.json` | 2 | id, project_id, name, description, description_html, description_stripped, access, logo_props, type |
| `views.json` | 1 | id, project_id, name, description, filters, display_filters, display_properties, rich_filters, access, sort_order |

- JSON 内の `id` は一時的な参照キーで、作成時に実 UUID へ対応付け（`project_map` 等）。サイクルは現在日時から 14 日単位で期間を計算。
- 目的: 初回利用者が空の画面ではなく、使い方の例を見られるようにする（オンボーディング）。

## 2. 開発・検証用ダミーデータ（確認済み）

- 管理コマンド `create_dummy_data`: 対話入力（ワークスペース名 / slug / 作成者メール / メンバー / プロジェクト数 / 作業項目数 / サイクル数 / モジュール数 / ページ数）→ Celery タスク `dummy_data_task` が Faker で生成（`Faker.seed(0)` で一部再現性あり）。
- 本番の初期データではなく、開発者向けの負荷・表示確認用。

## 3. 既定設定（確認済み）

| 対象 | 既定値の内容 | 根拠 |
|---|---|---|
| プロジェクト作成時の状態 | 6 件（バックログ相当〜トリアージ相当、各グループ 1 件、バックログを既定状態） | `state.py` `DEFAULT_STATES`、`project/base.py` |
| ユーザー × ワークスペース / プロジェクトの表示設定 | レイアウト=list、並び順=`-created_at`、サブ作業項目表示=true、空グループ表示=true、表示列（担当者、添付数、作成日、期限、見積もり、キー、ラベル、リンク、優先度、開始日、状態、サブ作業項目数、更新日）全て true | `workspace.py`、`project.py`、`issue.py` の `get_default_*` |
| プロジェクト | 公開範囲 Public(2)、ページ機能のみ ON（サイクル / モジュール / ビュー / 受付は OFF）、自動アーカイブ・自動クローズ 0（無効）、タイムゾーン UTC | `project.py` |
| メンバーロール | 招待・追加時の既定は Guest(5)。作成者は Admin(20) | `workspace.py`、`project.py`、`workspace/base.py` |
| ユーザー | タイムゾーン UTC、言語 en、週の開始日 日曜 | `user.py` |
| 並び順の初期値 | `sort_order` / `sequence` = 65535 | 各モデル |

## 4. 初期構成（インスタンス）（確認済み・一部有力）

- API 起動時に `register_instance`（マシン署名）→ `configure_instance`（インスタンス設定キーを環境変数の値で初期化、と推定 → 有力）→ `create_bucket`（S3 バケット作成）。
- 初回は管理画面（`/god-mode/`）でインスタンス管理者を登録し `is_setup_done` を立てる。未完了だとサインインが `INSTANCE_NOT_CONFIGURED` エラー。
- 管理者を CLI で作る `create_instance_admin <email>` コマンドもある。
- インスタンス設定キー: サインアップ可否、メール + パスワード可否、Magic code 可否、ワークスペース作成制限、OAuth 各種、SMTP、LLM、Unsplash（[external-services.md](external-services.md)）。

## 5. サンプルユーザー / 固定アカウント

- 確認した結果、固定のサンプルユーザー・既定パスワードはリポジトリ内に見当たらない（シードのボットユーザーはワークスペース毎に動的生成）。
- テスト用のユーザーは `tests/conftest.py` / `factories.py` で生成（テスト専用）。

## TaskManager への影響

- 初回利用時のデモデータは学習コストを下げるが、利用者が削除する手間も生む。TaskManager で入れるか、入れるならどの単位（初回のみ / 任意）かを決める必要がある。
- プロジェクト作成時の既定状態セットは業務上必須に近い（作業項目には状態が必須のため）。
- 開発用ダミーデータ生成は Phase 5〜6 のテスト・画面確認に有用。

## Phase 2 で決定する事項

- TaskManager 独自の既定状態（名前・色・グループ）を日本語で新規設計
- 初期管理者の作成方法（環境変数 / 初回セットアップ画面 / CLI）
- デモデータの要否と内容（自然な日本語で新規作成）
- 開発用シードの作成方法（EF Core の HasData / 専用シーダー）
- 既定の表示設定・ロール
