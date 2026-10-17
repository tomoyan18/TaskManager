# 未解決事項・不明点（Phase 1 時点）

## 目的

Phase 1 の解析で確定できなかった事項、および Plane の仕様を TaskManager に持ち込む前に判断が必要な事項を記録する。「多分こうだろう」で終わらせず、現状・調査済みファイル・不明な理由・追加調査方法・Phase 2 での決定事項を明記する。

状態の凡例: **確認済み** / **有力** / **不明** / **追加確認が必要**

## A. Plane の仕様に関する不明点

### Q-ARC-01 API 応答キャッシュの適用範囲
- 質問: `utils/cache.py` のキャッシュはどの API に適用され、どう無効化されるか。
- 現在分かっていること: Redis が `CACHES.default`、`clear_cache` コマンドが起動時に実行される。
- 調査したファイル: `settings/common.py`、`bin/docker-entrypoint-api.sh`
- なぜ不明か: デコレータの使用箇所を網羅していない。
- 追加調査方法: `grep -rn "cache_response\|invalidate_cache" apps/api/plane` で使用箇所と無効化処理を確認。
- Phase 2 で決定: TaskManager でサーバーキャッシュを導入するか（状態: 不明）。

### Q-ARC-02 Django Channels の用途
- 質問: `channels` 依存はリアルタイム配信に使われているか。
- 現在分かっていること: 共同編集は `apps/live`（Hocuspocus）が担う。`asgi.py` が存在。
- 調査したファイル: `requirements/base.txt`、`settings/common.py`（`INSTALLED_APPS` に channels はない）
- なぜ不明か: `asgi.py` を読んでいない。
- 追加調査方法: `apps/api/plane/asgi.py` を確認。
- Phase 2 で決定: リアルタイム更新（SignalR 等）の要否（状態: 有力＝未使用、追加確認が必要）。

### Q-DB-01 Frontend 型と API 応答の一致
- 質問: `packages/types` の型定義と Serializer 出力は一致しているか。
- 現在分かっていること: 作業項目作成の応答フィールドは View 内の `.values(...)` で列挙されている。
- 調査したファイル: `app/views/issue/base.py`、`apps/web/services/issue/issue.service.ts`
- なぜ不明か: 型定義ファイルを精読していない。
- 追加調査方法: `packages/types/src/issues/*.ts` と Serializer を突き合わせる。
- Phase 2 で決定: OpenAPI から Frontend 型を生成するか（状態: 追加確認が必要）。

### Q-DB-02 論理削除の波及範囲
- 質問: `soft_delete_related_objects` はどの関連を対象とし、復元はできるか。
- 現在分かっていること: Celery で非同期実行。`restore_related_objects` はタスク化されていない（コメントアウト）。
- 調査したファイル: `db/mixins.py`、`bgtasks/deletion_task.py`（一部）
- なぜ不明か: 関数本体を全文確認していない。
- 追加調査方法: `bgtasks/deletion_task.py` の 17〜110 行を読む。
- Phase 2 で決定: 論理削除の採否と復元機能の要否（状態: 追加確認が必要）。

### Q-API-01 内部 API と公開 API v1 の差分
- 質問: 同機能の `app/views` と `api/views` で検証・応答・権限に差があるか。
- 現在分かっていること: 公開 API は API Key 認証・レート制限、OpenAPI 対象。
- 調査したファイル: `api/views/base.py`、`api/urls/*.py`
- なぜ不明か: `api/views/issue.py` を精読していない。
- 追加調査方法: 作業項目の作成・更新処理を両方比較。
- Phase 2 で決定: TaskManager で API を 1 系統に統一するか（状態: 不明）。

### Q-API-02 `rich_filters` の仕様
- 質問: 保存ビュー等の `rich_filters`（JSON 式）の文法と評価方法。
- 現在分かっていること: 各ユーザー設定・ビュー・サイクル・モジュールに JSON 列として存在。Frontend に `components/rich-filters/` がある。
- 調査したファイル: モデル定義のみ。
- なぜ不明か: 評価ロジックを追っていない。
- 追加調査方法: `grep -rn rich_filters apps/api/plane/utils` で評価関数を特定。
- Phase 2 で決定: TaskManager のフィルタ表現（クエリパラメータのみ / 式）（状態: 不明）。

### Q-API-03 更新応答 200 / 204 の使い分け
- 質問: 作業項目 PATCH は 204、他リソースは 200 + 本文。基準はあるか。
- 現在分かっていること: 作業項目は 204（`issue/base.py:713`）。
- 調査したファイル: `app/views/issue/base.py`、`project/base.py`
- なぜ不明か: 全 View を確認していない。統一された基準はないと思われる（有力）。
- 追加調査方法: `grep -rn "HTTP_204_NO_CONTENT" app/views` で PATCH の箇所を列挙。
- Phase 2 で決定: TaskManager の更新応答形式（状態: 有力＝基準なし）。

### Q-AUTH-01 PyJWT の用途
- 質問: PyJWT はどこで使われているか（ログインセッションには不使用）。
- 調査したファイル: `requirements/base.txt`、`authentication/`
- なぜ不明か: 使用箇所を検索していない。
- 追加調査方法: `grep -rn "import jwt" apps/api/plane`。
- Phase 2 で決定: TaskManager の認証方式（状態: 不明）。

### Q-AUTH-02 サインアップ時のパスワード強度チェック
- 質問: サインアップ時にも zxcvbn スコア 3 以上を要求しているか。
- 現在分かっていること: リセット時は確認済み（`password_management.py:145-146`）。
- 調査したファイル: `provider/credentials/email.py`、`views/app/password_management.py`
- なぜ不明か: サインアップ側の検証箇所を確認していない。
- 追加調査方法: `grep -rn zxcvbn apps/api/plane/authentication`。
- Phase 2 で決定: TaskManager のパスワードポリシー（状態: 有力）。

### Q-AUTH-03 Cookie の SameSite 設定
- 質問: 本番設定で `SESSION_COOKIE_SAMESITE` / `CSRF_COOKIE_SAMESITE` を明示しているか。
- 現在分かっていること: `common.py` には明示なし（Django 既定 Lax）。
- 調査したファイル: `settings/common.py`
- なぜ不明か: `settings/production.py` を読んでいない。
- 追加調査方法: `production.py` を確認。
- Phase 2 で決定: CSRF / SameSite 方針（状態: 追加確認が必要）。

### Q-FE-01 楽観的更新と失敗時のロールバック
- 質問: 作業項目更新失敗時にストアは元に戻るか。
- 調査したファイル: `store/issue/project/issue.store.ts`（概要のみ）
- なぜ不明か: `helpers/base-issues.store.ts` の `issueUpdate` を精読していない。
- 追加調査方法: 同メソッドの try/catch を確認。
- Phase 2 で決定: TaskManager の楽観的更新方針（状態: 有力＝楽観的更新あり）。

### Q-FE-02 サーバー検証エラーのフィールド単位表示
- 質問: 400 のフィールドエラーをフォーム項目に表示しているか。
- 調査したファイル: `components/issues/issue-modal/base.tsx`
- なぜ不明か: エラー処理部分を網羅していない。
- 追加調査方法: `setError(` の使用箇所を検索。
- Phase 2 で決定: エラー形式と表示方法（状態: 不明）。

### Q-BG-01 Celery 結果バックエンド
- 質問: `django-celery-results` は使われているか。
- 調査したファイル: `requirements/base.txt`、`settings/common.py`
- なぜ不明か: `INSTALLED_APPS` に含まれていないが、他の設定ファイルを見ていない。
- 追加調査方法: `grep -rn celery_results apps/api`。
- Phase 2 で決定: 非同期処理の結果管理の要否（状態: 有力＝未使用）。

### Q-BG-02 通知の受信者決定ルール
- 質問: 購読者・担当者・作成者・メンション・通知設定の組み合わせで誰に通知されるか。
- 現在分かっていること: メンション対象は購読者に自動追加（条件: 未購読・非担当・非作成者・有効メンバー）。`UserNotificationPreference` の 5 項目で抑止。
- 調査したファイル: `bgtasks/notification_task.py`（冒頭〜150 行）
- なぜ不明か: `notifications` 本体を全文確認していない。
- 追加調査方法: 同ファイル後半を精読。
- Phase 2 で決定: TaskManager の通知ルール（状態: 追加確認が必要）。

### Q-EXT-01 インスタンス設定の暗号化方式
- 質問: `is_encrypted` の値はどう暗号化され、鍵は何か。
- 調査したファイル: `utils/instance_config_variables/core.py`
- なぜ不明か: `license/utils` の暗号化関数を読んでいない。
- 追加調査方法: `grep -rn "encrypt" apps/api/plane/license`。
- Phase 2 で決定: TaskManager の Secret 管理方式（状態: 不明）。

### Q-EXT-02 課金（billing）画面の内容
- 質問: `/:ws/settings/billing` は CE で何を表示するか。
- 調査したファイル: `app/routes/core.ts`
- なぜ不明か: ページ本体を読んでいない（有償版案内と推定）。
- 追加調査方法: 該当 page.tsx を確認。
- Phase 2 で決定: TaskManager には課金機能を持たない前提でよいか（状態: 有力）。

### Q-EXT-03 S3 オブジェクトの公開範囲
- 質問: `AWS_DEFAULT_ACL="public-read"` 設定下で、アップロードされたファイルは署名なしで取得できるか。
- 現在分かっていること: アップロードは presigned POST、取得は presigned GET を使う。
- 調査したファイル: `settings/common.py`、`settings/storage.py`、`app/views/asset/v2.py`
- なぜ不明か: presigned POST の条件（ACL フィールド）と MinIO のバケットポリシー（`create_bucket`）を確認していない。
- 追加調査方法: `storage.py` の `generate_presigned_post` と `create_bucket` コマンドを読む。
- Phase 2 で決定: TaskManager のファイルアクセス制御（状態: 追加確認が必要）。

### Q-INF-01 ローカル開発構成
- 質問: `docker-compose-local.yml` と `Dockerfile.dev` の構成（ホットリロード、ポート公開）。
- なぜ不明か: 未読。
- 追加調査方法: 両ファイルを読む。
- Phase 2/3 で決定: TaskManager の開発用 Compose（状態: 不明）。

### Q-INF-02 Web 系 CI とデプロイ手順
- 質問: Web のビルド・テスト・デプロイの詳細。
- 現在分かっていること: API 系 CI は lint とマイグレーションチェックのみで pytest は実行しない（確認済み）。
- なぜ不明か: Web 系・デプロイ系ワークフローを読んでいない。
- 追加調査方法: `pull-request-build-lint-web-apps.yml`、`build-branch.yml` を読む。
- Phase 2 で決定: TaskManager の CI 方針（状態: 一部確認済み）。

### Q-TEST-01 テスト規約
- 質問: `TESTING_GUIDE.md` の命名・フィクスチャ規約。
- なぜ不明か: 未読。
- 追加調査方法: 同ファイルを読む。
- Phase 2 で決定: TaskManager のテスト規約（状態: 不明）。

### Q-TEST-02 Plane のテストが全件成功する状態か
- 質問: 現在の `preview` でテストは通るか。
- なぜ不明か: Phase 1 ではテストを実行しない（Plane の変更・環境構築を避けるため）。
- 追加調査方法: 必要なら隔離環境で `docker-compose-test.yml` を実行（Plane の working tree を変更しない範囲で）。
- Phase 2 で決定: 不要（TaskManager の設計には影響しない）（状態: 不明）。

### Q-UX-01 レスポンシブの実態
- 質問: スマートフォン幅での一覧・詳細・作成の使い勝手。
- なぜ不明か: 実画面を起動していない。
- 追加調査方法: Plane を起動して確認（Phase 1 の範囲外）、または公開デモを確認。
- Phase 2 で決定: TaskManager の対応画面幅（状態: 不明）。

### Q-UX-02 アクセシビリティの実態
- 質問: コントラスト比、スクリーンリーダー読み上げ、フォーカス順。
- なぜ不明か: 実画面検証をしていない。
- 追加調査方法: 実画面で axe 等の検査。
- Phase 2 で決定: TaskManager のアクセシビリティ目標（例: WCAG 2.2 AA）（状態: 不明）。

## B. 解析で見つかった Plane の問題点（TaskManager で繰り返さないための記録）

| ID | 内容 | 根拠 | 状態 |
|---|---|---|---|
| F-01 | 自動クローズで既定状態が未設定の場合、状態をプロジェクトで絞らずに取得 | `bgtasks/issue_automation_task.py:119-120` | 有力（実行未確認） |
| F-02 | 期限切れエクスポート削除タスクが Beat に 2 回登録 | `celery.py` | 確認済み（意図は不明） |
| F-03 | Frontend が CE に存在しない API を呼ぶ（有償版機能） | `services/issue/issue.service.ts`、`components/issues/bulk-operations/root.tsx` | 有力 |
| F-04 | `archived_at` の型が date / datetime で不統一 | `db/models/issue.py`、`cycle.py`、`module.py` | 確認済み |
| F-05 | 1 作業項目 1 サイクルのルールが DB 制約ではなくアプリ実装のみ | `db/models/cycle.py`、`app/views/cycle/issue.py` | 確認済み |
| F-06 | API Token / Webhook secret の平文保存 | `db/models/api.py`、`webhook.py`、`api/middleware/api_authentication.py` | 確認済み |
| F-07 | オブジェクトレベル認可の漏れが繰り返し発生（回帰テスト多数） | `tests/contract/app/*_scope_app.py` | 確認済み |
| F-08 | エラー応答形式の不統一 | `app/views/base.py`、`workspace/base.py` ほか | 確認済み |
| F-09 | 本番 Compose に healthcheck なし、API コンテナが root + `chmod 777` | `docker-compose.yml`、`Dockerfile.api` | 確認済み |
| F-10 | API テストが CI で実行されていない | `.github/workflows/` | 確認済み |
| F-11 | ユーザー列挙が可能（email-check、サインインのエラーコード） | `authentication/views/app/check.py`、`email.py` | 確認済み |

## C. Phase 2 で決定する主要事項（全資料の集約）

1. MVP 機能範囲（[feature-map.md](feature-map.md) の A/B/C 候補の確定）と日本語用語集
2. アプリ構成と Docker 構成（API / Web / MySQL / 任意サービス）
3. 認証方式（Cookie / JWT）、ロール体系、オブジェクトレベル認可の実装方式
4. DB 方針: 主キー、論理削除と一意制約の両立、採番方式、監査列、楽観的排他、Delete behavior、JSON 列の使用範囲
5. API 方針: URL 体系、統一エラー形式、ページネーション、フィルタ / ソート、更新応答、OpenAPI 生成方式
6. 非同期処理の要否（履歴・通知・メール・定期処理）
7. ファイル保存・メール送信・外部サービスの要否
8. Frontend 方針: 状態管理、フォーム、UI 部品、レイアウト種類、型生成
9. UI/UX: 画面一覧・遷移・ナビゲーション、[ui-ux.md](ui-ux.md) の R-UX 要求の採否
10. テスト方針: 種別、テスト DB、認可マトリクステスト、CI
11. 初期データ: 既定状態、初期管理者、デモデータ

## D. Phase 2 での扱い（2026-10-07 追記）

Phase 2 では、TaskManager の設計が Plane の内部仕様に依存しないよう独自に決定したため、上記の不明点を解消するための Plane の追加解析は行っていない。各質問の扱いは [../design/open-decisions.md](../design/open-decisions.md) §5 に記録した。
