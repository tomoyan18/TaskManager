# Repository 構造解析（Plane `preview`）

## 目的

Plane リポジトリの構造上の意味（アプリ境界・責務・依存関係）を把握し、TaskManager の独立設計（Phase 2）の前提情報とする。ファイル一覧ではなく「どこに何の責務があるか」を記録する。

## 解析範囲と前提

| 項目 | 値 | 状態 |
|---|---|---|
| 参照パス | `/Volumes/OutSSD/CSharp/plane`（実ディレクトリ名は小文字 `plane`。APFS が大文字小文字を区別しないため `Plane` でもアクセス可） | 確認済み |
| ブランチ | `preview` | 確認済み |
| HEAD | `7466675e47 fix(security): bump pins overtaken by the 2026-10-01 advisories (#9924)` | 確認済み |
| working tree | `git status --porcelain` 出力 0 行（変更なし） | 確認済み |
| ルート `package.json` version | `1.4.2` / License `AGPL-3.0` | 確認済み |

## Repository の目的

`package.json` の description: "Open-source project management"。プロジェクト単位で作業項目（Issue / Work item）を管理し、サイクル（スプリント）、モジュール、ビュー、ページ（Wiki）、受付（Intake）、通知、分析を提供するプロジェクト管理 SaaS / セルフホスト製品。

## 最上位ディレクトリ

| パス | 責務 | 根拠 |
|---|---|---|
| `apps/` | 実行可能アプリ群（6 個） | `ls apps` |
| `packages/` | フロントエンド共通ライブラリ（14 個） | `ls packages`, `pnpm-workspace.yaml` |
| `deployments/` | セルフホスト用配布（aio / cli / kubernetes / swarm、いずれも `community`） | `ls deployments` |
| `docker-compose.yml` | 本番相当のフル構成（ビルドから） | 直接確認 |
| `docker-compose-local.yml` | ローカル開発用 | 存在確認のみ |
| `docker-compose-test.yml` | API テスト用の隔離スタック | 直接確認 |
| `.env.example` | DB / Redis / RabbitMQ / S3 / Proxy 等の環境変数テンプレート | 直接確認（値は転記しない） |
| `setup.sh` | `.env` 生成スクリプト | `AGENTS.md` の記述 |
| `turbo.json`, `pnpm-workspace.yaml`, `pnpm-lock.yaml` | JS モノレポのビルド管理（pnpm + Turborepo） | 直接確認 |
| `.github/` | CI ワークフロー（lint、型チェック、ビルド、マイグレーションチェック、CodeQL、i18n 同期等） | ファイル名と API 系の内容を確認 |
| `AGENTS.md`, `CONTRIBUTING.md`, `SECURITY.md` | 開発者向けドキュメント | `AGENTS.md` 読了 |
| `docs/linting.md` | Lint 方針のみ | `ls docs` |

## アプリケーション境界（apps/）

| アプリ | 技術 | 責務 | 公開パス（Proxy） |
|---|---|---|---|
| `apps/api` | Python 3.12 / Django 5.2 / DRF 3.17 / Celery 5.5 | REST API、認証、バックグラウンドジョブ、DB マイグレーション | `/api/*`, `/auth/*`, `/static/*` |
| `apps/web` | React 19 / React Router 8（SSR 無効の SPA）/ Vite / MobX / SWR / Tailwind 4 | メインの業務 UI | `/*` |
| `apps/admin` | 同上 | インスタンス管理画面（認証方式・SMTP・AI・画像・ワークスペース設定） | `/god-mode/*` |
| `apps/space` | 同上 | 公開ボード（プロジェクトを匿名/外部向けに公開） | `/spaces/*` |
| `apps/live` | Node / Express 4 / Hocuspocus（Yjs）| ページ・説明文の共同編集サーバー、PDF エクスポート | `/live/*` |
| `apps/proxy` | Caddy | リバースプロキシ・TLS・アップロードサイズ制限 | 80/443 |

根拠: `apps/proxy/Caddyfile.ce`、各 `package.json`、`apps/api/requirements/base.txt`、`apps/web/react-router.config.ts`（`ssr: false`）。

`pnpm-workspace.yaml` で `apps/api` と `apps/proxy` は JS ワークスペースから除外されている（確認済み）。

## Backend（apps/api/plane/）の内部構造

| ディレクトリ | 責務 |
|---|---|
| `urls.py` | ルート URL 振り分け（`/api/`, `/api/public/`, `/api/instances/`, `/api/v1/`, `/auth/`） |
| `app/` | Web アプリ用内部 API（views / serializers / permissions / urls） |
| `api/` | 外部公開 API v1（API Key 認証、レート制限、OpenAPI 対象） |
| `space/` | 公開ボード API |
| `authentication/` | サインイン/サインアップ/OAuth/Magic code/パスワード管理、セッション Middleware |
| `license/` | インスタンス登録・インスタンス管理者・インスタンス設定（暗号化設定値を含む） |
| `db/models/` | 全 Entity（約 30 ファイル） |
| `db/migrations/` | Django マイグレーション（`0001_initial` 〜 `0122_*`、122 本） |
| `db/management/commands/` | 運用コマンド（wait_for_db、create_dummy_data、reset_password 等） |
| `bgtasks/` | Celery タスク（約 33 ファイル） |
| `celery.py` | Celery アプリと Beat スケジュール |
| `settings/` | common / local / production / test / openapi / redis / storage |
| `middleware/` | リクエストボディサイズ制限、API ログ、Read replica ルーティング |
| `utils/` | ページネーション、フィルタ、並び替え allowlist、URL 安全性（SSRF 対策）、エクスポート等 |
| `seeds/data/` | ワークスペース作成時の初期デモデータ（JSON） |
| `tests/` | pytest（unit / contract / smoke） |
| `templates/`（apps/api 直下） | メールテンプレート |

## Frontend 共通パッケージ（packages/）

| パッケージ | 責務 |
|---|---|
| `types` | 共通 TypeScript 型（`TIssue` 等） |
| `constants` | 定数（表示フィルタ定義など） |
| `services` | API クライアント共通処理（URL 正規化など） |
| `shared-state` | MobX 共通ストア |
| `i18n` | i18next ベースの多言語化（`ja` を含む 21 ロケール） |
| `editor` | Tiptap ベースのリッチテキストエディタ |
| `blocks` | Plane 固有の複合 UI コンポーネント（テストあり） |
| `hooks`, `utils`, `logger`, `decorators` | 共通ユーティリティ |
| `tailwind-config`, `typescript-config` | ビルド設定 |
| `codemods` | コード移行ツール |

UI プリミティブは外部 npm パッケージ `@makeplane/propel` から提供される（`AGENTS.md` に明記、確認済み）。

## Database / Migration 関連

- DB: PostgreSQL 15.7（`docker-compose.yml` の `plane-db` イメージ）
- ORM: Django ORM、マイグレーションは `apps/api/plane/db/migrations/` と `apps/api/plane/license/migrations/`（7 本）
- 詳細は [database.md](database.md)

## Test 関連

- Backend: `apps/api/plane/tests/`（pytest、`pytest.ini`）
- Frontend: `packages/blocks`、`packages/services`、`packages/codemods`、`apps/live` にのみ vitest テストあり。`apps/web` には単体テストファイルが見つからない
- E2E: Playwright / Cypress の設定ファイルは見つからない
- 詳細は [testing.md](testing.md)

## Docker / Configuration / Scripts

- 各アプリに `Dockerfile.*`（本番）と `Dockerfile.dev`
- API コンテナのエントリポイント: `apps/api/bin/docker-entrypoint-{api,worker,beat,migrator}.sh`
- 詳細は [infrastructure.md](infrastructure.md)

## 外部サービス関連

- S3 互換ストレージ（MinIO / AWS S3）: `settings/storage.py`
- SMTP / OAuth（Google, GitHub, GitLab, Gitea）/ LLM / Unsplash: `utils/instance_config_variables/core.py`
- 詳細は [external-services.md](external-services.md)

## 主要依存関係（Backend）

`apps/api/requirements/base.txt` より（確認済み）: Django 5.2、djangorestframework 3.17、psycopg 3、django-redis、django-cors-headers、celery + django_celery_beat、django-storages + boto3、channels、openai、slack-sdk、openpyxl、zxcvbn（パスワード強度）、PyJWT、OpenTelemetry、drf-spectacular（OpenAPI）、nh3（HTML サニタイズ）。

## 依存関係の向き

```text
web / admin / space ──HTTP(cookie)──> api ──> PostgreSQL
        │                              ├──> Redis(Valkey)  : キャッシュ・Magic code・ロック
        │                              ├──> RabbitMQ      : Celery ブローカー
        │                              └──> MinIO/S3      : ファイル
        └──WebSocket──> live ──HTTP(cookie 転送)──> api
worker / beat-worker（api と同じイメージ）──> 上記と同じ DB / Redis / MQ / S3
```

## 不明点

- `.github/workflows/` のうち API 系（lint・マイグレーションチェック）は確認済みで pytest は実行されていない（[testing.md](testing.md)）。Web 系ワークフローとデプロイ手順の詳細は未読。→ [open-questions.md](open-questions.md) Q-INF-02

## TaskManager への影響

- Plane は 6 アプリ + 5 ミドルウェア（DB / Redis / MQ / S3 / Proxy）の大規模構成。TaskManager の規定構成（.NET 10 API + React/Vite + MySQL + Docker Compose）とは大きく異なり、そのまま参考にすると過剰な構成になる。
- 公開ボード（space）、共同編集（live）、インスタンス管理（admin）は独立アプリになっている。TaskManager で必要かは Phase 2 で判断する。

## Phase 2 で決定する事項

- TaskManager に必要なアプリ境界（API + Web の 2 つで足りるか）
- 共同編集、公開ボード、インスタンス管理機能の要否
- Redis / メッセージキュー / オブジェクトストレージの要否
