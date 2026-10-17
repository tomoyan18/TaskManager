# マイグレーション戦略

本書は **DB スキーマのマイグレーション（EF Core）** と初期データの方針を定める。Plane からのデータ移行は行わない（TaskManager は独立した新規システムで、Plane のデータ・サンプル・利用者情報を一切持ち込まない）。

## 1. 方針

| 項目 | 方針 |
|---|---|
| 方式 | EF Core Code First マイグレーション |
| 置き場所 | `src/TaskManager.Infrastructure/Persistence/Migrations/` |
| 生成コマンド | `dotnet tool restore` の後 `dotnet ef migrations add <名前> --project src/TaskManager.Infrastructure --startup-project src/TaskManager.Infrastructure`（Phase 3 で決定: 設計時は `DesignTimeAppDbContextFactory` を使い、API の起動設定（JWT 鍵など）に依存させない） |
| 名前 | PascalCase で変更内容を表す（例 `InitialSchema`, `AddTaskAttachment`）。EF Core が先頭に日時を付ける |
| 1 回目 | `InitialSchema`（[database-design.md](database-design.md) の全表） |
| 適用方法 | **マイグレーションバンドル**（`dotnet ef migrations bundle`）を `migrate` コンテナで実行。API 起動時の自動 `Migrate()` は使わない |
| DB ユーザー | 適用は `TM_DB_MIGRATOR_USER`（DDL 権限）、API は DML のみのユーザー |
| 冪等性 | バンドルは適用済みのマイグレーションを飛ばすため、毎回の起動で実行してよい |

### API 起動時の自動適用を使わない理由

- API の DB ユーザーに DDL 権限を与えずに済む（[security-design.md](security-design.md) S-18）。
- 将来 API を複数起動しても、マイグレーションが同時実行されない。
- 失敗時に API が起動しないため、壊れたスキーマで動かない（Compose の `service_completed_successfully`）。

## 2. 起動時の流れ（Docker Compose）

```text
db（healthy）
 → migrate: ./efbundle --connection "$MIGRATOR_CONNECTION"   成功 → 終了コード 0
 → api: 起動
     1. 接続確認
     2. 未適用のマイグレーションがあれば起動を中止（ログ「データベースが最新ではありません。migrate を実行してください。」）
     3. 初期システム管理者シーダー（§6）
     4. 受付開始
```

## 3. 開発時の運用

| 作業 | 手順 |
|---|---|
| モデル変更 | Entity と `IEntityTypeConfiguration` を変更 → `migrations add` → 生成されたコードと `migrations script` の SQL を確認 → テスト（Infrastructure.Tests のマイグレーション適用テスト）|
| ローカル DB へ適用 | `dotnet ef database update`（開発用 DB ユーザーで）、または `docker compose run --rm migrate` |
| やり直し | 未コミット・未共有のマイグレーションのみ `migrations remove` |
| 確認 | `dotnet ef migrations has-pending-model-changes` でモデルとマイグレーションのずれを検出し、テストでも検査（Plane の CI と同様の考え方: analysis/testing.md） |

## 4. 変更のルール

1. **適用済み（コミット済み）のマイグレーションは編集しない**。修正は新しいマイグレーションで行う。
2. 列の追加は「NULL 可」または「既定値あり」で行う。
3. 破壊的変更（列の削除・名前変更・型の縮小）は 2 段階で行う（拡張 → コード移行 → 縮小）。
4. 名前変更は EF Core が削除 + 追加と誤認しないよう、生成コードを `RenameColumn` に修正して確認する。
5. CHECK 制約や EF Core が生成できない DDL は `migrationBuilder.Sql(...)` で記述し、`Down` も書く。
6. データ移行（既存行の更新）が必要な場合も、同じマイグレーション内で SQL として記述し、件数の多い更新は分割する。
7. マイグレーションには Secret・個人情報・利用者データを含めない（`HasData` で業務データを入れない）。

## 5. ロールバックとバックアップ

- 基本は **前進修正**（問題を修正する新しいマイグレーションを作る）。`Down` は開発環境でのやり直し用。
- 本番相当の環境では、マイグレーション前に `mysqldump --single-transaction` でバックアップを取る手順を README に記載する。
- 添付ファイルのボリュームも同時にバックアップする（DB と整合させるため、可能なら API を停止して取得）。

## 6. 初期データ（シード）

| データ | 作成方法 | 時期 |
|---|---|---|
| 初期システム管理者 | API 起動時の `BootstrapAdministratorSeeder`。有効な SA が 0 人で、`TM_BOOTSTRAP_ADMIN_EMAIL` と `TM_BOOTSTRAP_ADMIN_PASSWORD` が設定されている場合のみ作成（`must_change_password=1`）。パスワードはポリシーを満たさなければ作成せずエラーログ | 起動時（冪等） |
| 既定ステータス 5 件 | プロジェクト作成時に Application が作成（[domain-model.md](domain-model.md) P-04） | プロジェクト作成時 |
| デモデータ | 本番には入れない。開発・E2E 用に `tools/DevSeeder`（または API の Development 限定コマンド）で、架空の日本語データ（例: プロジェクト「社内イベント準備」、タスク「会場の候補を3つ挙げる」）を作成 | 任意（OD-19） |

## 7. 将来の移行

- MySQL のメジャーバージョン更新（8.4 → 次の LTS）: 新バージョンで全テスト（Testcontainers のイメージを変更）→ バックアップ → 更新。
- 添付の保存先を S3 互換に移す場合: `IAttachmentStorage` の新実装 + 既存ファイルのコピー手順を別途設計。

## 8. マイグレーションの履歴

| 名前（EF Core が日時を付加） | Phase | 内容 | `Down` |
|---|---|---|---|
| `InitialSchema` | 4 | 全 14 表・制約・索引（[database-design.md](database-design.md)） | 全表の削除 |
| `AddTaskStatusSummaryIndex` | 9 | `task_item` に索引 `ix_task_item__assignee_user_id__project_id__status_id` を追加（自分の担当のステータス別集計用）。表・列・既存の索引は変更しない | 索引の削除 |

- `AddTaskStatusSummaryIndex` は索引の追加だけのため、既存のデータの移行はない（§4 のルール 6 は該当なし）。
- 適用は従来どおり migrate コンテナ（efbundle）が起動時に行う。テスト用 DB は全マイグレーションから生成した SQL を適用するため（`tests/TaskManager.Testing/MySqlTestServer.cs`）、追加の作業はない。
- 適用済みのマイグレーション（`InitialSchema`）は編集しない（§4 のルール 1）。

