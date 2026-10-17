# 外部サービス解析（Plane `preview`）

## 目的

Plane が依存する外部サービスの用途・呼び出し箇所・認証・設定・Secret の扱い・失敗時処理を記録する。Secret の値は記録しない。

## 解析範囲と根拠

- `apps/api/plane/settings/common.py`、`settings/storage.py`
- `apps/api/plane/utils/instance_config_variables/core.py`（設定キー定義）
- `apps/api/requirements/base.txt`
- `apps/api/plane/bgtasks/webhook_task.py`、`email_notification_task.py`
- `.env.example`（キー名のみ参照、値は転記しない）
- `apps/api/plane/authentication/provider/oauth/*.py`（存在確認）

## 設定の保存場所（確認済み）

- 環境変数（`.env`）と、DB に保存されるインスタンス設定（管理画面から編集）の 2 系統。
- インスタンス設定はキー毎に `is_encrypted` フラグを持ち、Secret 系（OAuth Client Secret、SMTP パスワード、LLM API Key、Unsplash Key）は暗号化保存（暗号方式の詳細は未精読 → Q-EXT-01）。

## サービス一覧

| サービス | 用途 | 呼び出し元 | 認証 / 設定キー（値は記録しない） | 失敗時 |
|---|---|---|---|---|
| PostgreSQL | 主 DB | Django ORM | `DATABASE_URL` または `POSTGRES_*`、Read replica は `DATABASE_READ_REPLICA_*` | 起動時 `wait_for_db` で待機 |
| Redis（Valkey） | キャッシュ、Magic code、ロック | `django_redis`、`settings/redis.py` | `REDIS_URL`（`rediss://` で TLS、証明書検証なし設定） | 未確認 |
| RabbitMQ | Celery ブローカー | Celery | `AMQP_URL` または `RABBITMQ_*` | 未確認 |
| S3 / MinIO | ファイル保存（添付、アバター、カバー画像、エクスポート） | `settings/storage.py`（boto3）、`django-storages` | `AWS_ACCESS_KEY_ID`、`AWS_SECRET_ACCESS_KEY`、`AWS_S3_BUCKET_NAME`、`AWS_S3_ENDPOINT_URL`、`AWS_REGION`、`USE_MINIO` | presigned 生成失敗時は print して None（`storage.py`） |
| SMTP | 招待、パスワードリセット、Magic code、通知メール | `utils/email.py`、各メールタスク | `ENABLE_SMTP`、`EMAIL_HOST`、`EMAIL_HOST_USER`、`EMAIL_HOST_PASSWORD`、`EMAIL_PORT`、`EMAIL_FROM`、`EMAIL_USE_TLS`、`EMAIL_USE_SSL` | タスク内で例外ログ |
| Google / GitHub / GitLab / Gitea OAuth | ソーシャルログイン | `authentication/provider/oauth/*.py` | `IS_*_ENABLED`、`*_CLIENT_ID`、`*_CLIENT_SECRET`、`GITLAB_HOST` / `GITEA_HOST`、`GITHUB_ORGANIZATION_ID` | エラーコード付きリダイレクト |
| LLM（OpenAI 等） | AI アシスタント（文章生成） | `app/views/external/base.py` | `LLM_PROVIDER`、`LLM_MODEL`、`LLM_API_KEY`（旧 `OPENAI_API_KEY`、`GPT_ENGINE` は deprecated） | 未確認 |
| Unsplash | カバー画像検索 | `/api/unsplash/` | `UNSPLASH_ACCESS_KEY` | 未確認 |
| Webhook 送信先 | 外部通知 | `bgtasks/webhook_task.py` | Webhook 毎の HMAC secret | 最大 5 回再試行（600 秒バックオフ）→ 無効化 + メール |
| URL メタデータ取得 | リンクのタイトル等 | `bgtasks/work_item_link_task.py` | なし | SSRF 対策（許可/拒否 IP・ホスト設定 `WEBHOOK_ALLOWED_IPS` 等） |
| テレメトリ | 利用状況の送信 | `license/bgtasks/telemetry_metrics` | 未確認 | 未確認 |
| OpenTelemetry | トレース | `opentelemetry-*` | OTLP エンドポイント（`utils/otlp_endpoints.py`） | 未確認 |
| Scout APM | APM | `scout-apm` | 未確認 | 未確認 |
| Slack SDK | 連携（`SlackProjectSync` モデル） | 依存のみ確認。CE での実使用は未確認 | — | — |
| GitHub 連携 | Issue 同期（`GithubIssueSync` 等モデル） | 依存のみ確認。CE での実使用は未確認 | `GITHUB_ACCESS_TOKEN` | — |
| Let's Encrypt（ACME） | TLS 証明書 | Caddy | `CERT_EMAIL`、`CERT_ACME_CA`、`CERT_ACME_DNS` | — |

Payment / 課金: 確認した結果、CE には決済サービス連携のコードは見当たらない（`billing` 画面ルートは存在するが内容未確認 → Q-EXT-02）。
Search エンジン（Elasticsearch 等）: 確認した結果、該当なし。検索は DB の部分一致。
Analytics（外部）: `ANALYTICS_SECRET_KEY` / `ANALYTICS_BASE_API` の設定キーあり、使用箇所は未確認。

## セキュリティ上の記録

- `.env.example` には MinIO / DB / RabbitMQ 用のプレースホルダ値が含まれる（転記しない）。
- `settings/common.py` の S3 設定で `AWS_ACCESS_KEY_ID` 等に既定値がある（開発用プレースホルダ）。TaskManager では既定 Secret を持たない方針が望ましい。
- `AWS_DEFAULT_ACL = "public-read"`、`AWS_QUERYSTRING_AUTH = False` の設定がある一方、ダウンロードは presigned URL を使用している。実際の公開範囲はバケットポリシー次第（Q-EXT-03）。
- `TRUSTED_PROXIES=0.0.0.0/0`（Caddy 既定）で X-Forwarded-For を全面信頼。

## TaskManager への影響

- 外部サービスへの依存が多いほど、Docker 構成・Secret 管理・テストの負担が増える。
- MVP で必須になりうるのは DB のみ。ファイル保存・メールは要件次第。

## Phase 2 で決定する事項

- ファイル保存先（ローカルボリューム / MinIO）
- メール送信の要否（開発時は MailHog / Mailpit 等のローカル SMTP を使うか）
- 外部 OAuth の要否
- AI 機能・画像検索の要否（Plane 固有機能として除外するか）
- Secret の受け渡し方式（`.env` / Docker secrets / ユーザーシークレット）
