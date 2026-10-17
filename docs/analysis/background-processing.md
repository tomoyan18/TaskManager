# Background 処理解析（Plane `preview`）

## 目的

Plane の非同期処理（Job / Queue / Worker / 定期実行 / 再試行 / 失敗処理）を把握し、TaskManager で必要かどうかを判断する材料を記録する。

## 解析範囲と根拠

- `apps/api/plane/celery.py`（Beat スケジュール）
- `apps/api/plane/settings/common.py`（`CELERY_*`、`CELERY_IMPORTS`）
- `apps/api/plane/bgtasks/*.py`（特に `issue_activities_task.py`、`notification_task.py`、`email_notification_task.py`、`webhook_task.py`、`deletion_task.py`、`issue_automation_task.py`、`workspace_seed_task.py`）
- `apps/api/bin/docker-entrypoint-{worker,beat}.sh`、`docker-compose.yml`

## 基盤（確認済み）

| 項目 | 内容 |
|---|---|
| ライブラリ | Celery 5.5 |
| ブローカー | RabbitMQ 3.13（`AMQP_URL` または `RABBITMQ_*`） |
| 結果バックエンド | `django-celery-results` が依存にあるが設定での使用は未確認 |
| スケジューラ | Celery Beat + `django_celery_beat.schedulers.DatabaseScheduler` |
| シリアライズ | JSON のみ |
| Worker | `celery -A plane worker -l info`（コンテナ `bgworker`） |
| Beat | `celery -A plane beat -l info`（コンテナ `beatworker`） |
| ログ | JSON 形式（python-json-logger） |
| 補助ストア | Redis（Magic code、ロック、Webhook の origin 一時保存） |

## イベント駆動タスク（API から `.delay()`）

| タスク | 起点 | 処理 |
|---|---|---|
| `issue_activity` | 作業項目・コメント・リンク・添付・関係・リアクション・サイクル/モジュール割当・下書き・受付の作成/更新/削除 | 変更差分（名前、説明、親、優先度、状態、日付、ラベル、担当者、見積もり、アーカイブ）を `IssueActivity` に一括保存 → `notifications.delay` |
| `notifications` | `issue_activity` 完了時 | 購読者・担当者・作成者・メンション対象へアプリ内通知とメール通知ログを作成、説明文のメンションを抽出して購読者に追加 |
| `model_activity` / `webhook_activity` / `webhook_send_task` | 各モデルの変更 | Webhook 配信 |
| `issue_description_version_task` | 作業項目の作成・更新 | 説明文のバージョン保存 |
| `recent_visited_task` | 詳細表示 | 最近見た項目の記録 |
| `soft_delete_related_objects` | 論理削除 | 関連オブジェクトの論理削除 |
| `workspace_seed` | ワークスペース作成 | デモ用ボットユーザー・プロジェクト・状態・ラベル・作業項目・ページ・サイクル・モジュール・ビューを作成 |
| メール系（`forgot_password_task`、`magic_link_code_task`、`workspace_invitation_task`、`project_invitation_task`、`user_activation_email_task` など） | 各操作 | SMTP 送信 |
| `export_task` / `analytic_plot_export` | エクスポート要求 | CSV / XLSX / JSON を生成して S3 に保存、メール通知 |
| `work_item_link_task` | リンク追加 | URL のタイトル等メタデータ取得（SSRF 対策付き） |
| `file_asset_task` / `storage_metadata_task` / `copy_s3_object` | ファイル | メタデータ取得・複製 |
| `page_transaction_task` / `page_version_task` | ページ | ページ内参照の記録、バージョン |

## 定期実行（Beat、確認済み、時刻は UTC）

| スケジュール | タスク | 内容 |
|---|---|---|
| 5 分毎 | `stack_email_notification` | 未処理のメール通知ログを受信者・作業項目単位にまとめて送信（Redis ロックで多重実行防止） |
| `METRICS_PUSH_INTERVAL_MINUTES`（既定 360 分）毎 | `push_instance_metrics` | テレメトリ送信 |
| 毎日 0:00 | `hard_delete` | 論理削除から 60 日（`HARD_DELETE_AFTER_DAYS`）経過した行を物理削除 |
| 毎日 1:00 | `archive_and_close_old_issues` | プロジェクト設定（月数）に基づく自動アーカイブ・自動クローズ |
| 毎日 1:30 / 3:45 | `delete_old_s3_link` | 期限切れエクスポートの削除（同一タスクが 2 回登録されている） |
| 毎日 2:00 | `delete_unuploaded_file_asset` | 未完了アップロードの削除 |
| 毎日 2:30 | `delete_api_logs` | API ログ（14 日） |
| 毎日 2:45 | `delete_email_notification_logs` | メール通知ログ（7 日） |
| 毎日 3:00 | `delete_page_versions` | ページバージョン |
| 毎日 3:15 | `delete_issue_description_versions` | 説明文バージョン |
| 毎日 3:30 | `delete_webhook_logs` | Webhook ログ（14 日） |

## Retry / Failure handling（確認済み）

- `webhook_send_task`: `autoretry_for=(requests.RequestException,)`、`retry_backoff=600`、`max_retries=5`。最大再試行到達時に Webhook を無効化し、所有者へ無効化メール（`send_webhook_deactivation_email`）。HMAC-SHA256 署名（`X-Plane-Signature`）、配信 ID・イベント名ヘッダー、IP ピン留めによる SSRF 対策（`pinned_fetch`）。
- `issue_activity` ほか多くのタスク: 例外を `log_exception` で記録して終了（再試行なし）。履歴・通知が欠落しても API は成功扱い。
- 論理削除の波及が非同期のため、一時的に「親は削除済み・子は未削除」の状態がありうる（有力）。

## 外部連携

- SMTP（メール）、S3、Webhook 送信先、URL メタデータ取得、テレメトリ。詳細は [external-services.md](external-services.md)。

## 発見事項

- `delete_old_s3_link` が Beat に 2 回登録（1:30 と 3:45）（確認済み。意図的か重複かは不明）。
- 自動クローズで `default_state` 未設定時に状態をプロジェクトで絞らず取得している（[database.md](database.md) 参照、有力）。
- 作業項目の `updated_at` が履歴タスクの中で再更新される（`issue.save(update_fields=["updated_at"])`）。

## TaskManager で必要かどうかの判断材料

| 処理 | 必要性の観点 | .NET での実現候補（Phase 2 で決定） |
|---|---|---|
| 変更履歴の記録 | 監査・利用者の確認に有用。整合性を考えると同一トランザクションでの同期記録が単純 | EF Core `SaveChanges` 内で同期記録 |
| アプリ内通知 | 担当者変更・コメント・メンションの通知は業務上有用 | 同期作成、または Outbox + `BackgroundService` |
| メール通知・招待・パスワードリセット | 要件次第。SMTP が必要 | `BackgroundService` + Channel、または Hangfire 等 |
| Webhook | 外部連携要件がなければ不要 | — |
| 自動アーカイブ / クローズ | 便利だが必須ではない | `BackgroundService` + 定期タイマー |
| 物理削除・ログ清掃 | 論理削除を採用する場合に必要 | 同上 |
| エクスポート | 件数が多いなら非同期 | 小規模なら同期ダウンロード |
| デモデータ投入 | 初回体験向け | シード処理（Phase 2 で設計） |

RabbitMQ + 別 Worker コンテナという Plane の構成は、TaskManager の想定規模では過剰になる可能性が高い（推測ではなく判断材料として記録。決定は Phase 2）。

## 不明点

- `django-celery-results` の使用有無（Q-BG-01）
- 通知タスクの受信者決定ルールの全条件（購読設定・通知設定の組み合わせ）（Q-BG-02）

## Phase 2 で決定する事項

- 非同期基盤の要否（キュー / ワーカー / 定期実行）
- 履歴・通知を同期にするか非同期にするか
- メール送信の要否と送信方式
- 失敗時の再試行方針と利用者への見せ方
