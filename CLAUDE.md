# TaskManager Claude Code 指示書

## 1. プロジェクト概要

このプロジェクトでは、オープンソースの Plane `preview` ブランチを参考資料として調査し、独立したタスク管理システム `TaskManager` を新規開発する。

PlaneをC#へ単純移植するプロジェクトではない。

必ず以下の流れで進める。

```text
Plane
↓
解析
↓
要件・機能・データ・API・UI/UX等を抽出
↓
TaskManagerとして独立設計
↓
.NET 10 / React / Vite / TypeScript / MySQL / Docker
↓
実装
↓
テストコード作成
↓
テスト実行
↓
セキュリティ確認
↓
完成確認
```

---

## 2. ローカルディレクトリ

外部SSD上を以下の構成とする。

```text
<OutSSD>/
└── CSharp/
    ├── Plane/
    │   └── Planeのpreviewブランチ
    │
    └── TaskManager/
        ├── CLAUDE.md
        ├── .claude/
        ├── docs/
        ├── src/
        ├── frontend/
        ├── docker/
        ├── docker-compose.yml
        ├── .env.example
        └── README.md
```

macOSでは通常、以下のようなパスになる。

```text
/Volumes/OutSSD/CSharp/Plane
/Volumes/OutSSD/CSharp/TaskManager
```

Windows形式のパスを前提にしてはいけない。

`Plane` は参照用リポジトリ。
`TaskManager` は完全に独立した新規プロジェクト。

---

## 3. 技術スタック

- Backend: C# / ASP.NET Core / .NET 10
- Frontend: TypeScript / React / Vite
- Database: MySQL
- ORM: Entity Framework Core
- API: REST API
- API仕様・確認: Swagger / OpenAPI
- Runtime: Docker / Docker Compose
- Container: Linux
- IDE: VS Code
- OS: macOS

Angularは使用しない。

---

## 4. フェーズ

必ず次の順番で進める。

```text
Phase 1  ANALYSIS
↓
Phase 2  DESIGN
↓
Phase 3  ENVIRONMENT
↓
Phase 4  IMPLEMENTATION
↓
Phase 5  TEST_CODE
↓
Phase 6  TEST_EXECUTION
↓
Phase 7  SECURITY
↓
Phase 8  COMPLETION
```

現在のフェーズ以外の作業を勝手に開始してはいけない。

---

## 5. フェーズごとの指示ファイル

各フェーズでは、そのフェーズの以下2ファイルを必ず確認する。

```text
.claude/skills/phase-XX-xxx/CLAUDE.md
.claude/skills/phase-XX-xxx/SKILL.md
```

共通のこのファイルと、現在フェーズの指示を組み合わせて作業する。

---

## 6. Phase 1では実装禁止

Phase 1ではTaskManagerのコードを書いてはいけない。

行ってよいこと:

- Planeのソース解析
- ディレクトリ構造確認
- 依存関係確認
- API解析
- DB解析
- Frontend解析
- 認証・認可解析
- Docker解析
- テスト解析
- UI/UX解析
- 解析結果のMarkdown出力

行ってはいけないこと:

- C#実装
- React実装
- DB実装
- Docker環境構築
- Task_Managerの機能実装
- Planeコードのコピー

実装案を発見した場合は、解析結果または設計候補としてMarkdownへ記録する。

---

## 7. 無駄な大量解析を禁止

Planeリポジトリ全体を最初から最後まで無差別に読み込んではいけない。

次の順番で解析する。

```text
リポジトリ全体構造
↓
主要アプリ・パッケージ
↓
依存関係
↓
対象機能
↓
関連ファイル
↓
関連コードチェーン
↓
解析結果をMarkdownへ保存
```

機能ごとに必要なコードだけ追跡する。

基本的な追跡:

```text
Frontend
↓
API Client
↓
URL / HTTP Method
↓
Backend Endpoint
↓
Service / Use Case
↓
Entity / Model
↓
Database
↓
Migration
↓
Test
```

Frontend:

```text
Page
↓
Component
↓
Hook / State
↓
API Client
↓
Backend API
```

DB:

```text
Model
↓
Migration
↓
Relation
↓
API
↓
Frontend Type
```

解析済みの情報はMarkdownを再利用し、必要がない限りPlaneを何度も読み直さない。

---

## 8. 解析結果のMarkdown保存は必須

重要な解析結果をチャットだけに残してはいけない。

Phase 1では必ず以下を作成・更新する。

```text
docs/analysis/
├── repository-structure.md
├── architecture.md
├── feature-map.md
├── database.md
├── api.md
├── authentication-authorization.md
├── frontend.md
├── background-processing.md
├── external-services.md
├── testing.md
├── infrastructure.md
├── ui-ux.md
├── data-and-seed.md
└── open-questions.md
```

解析対象に存在しない項目も「確認した結果、該当なし」と記録する。

各Markdownには可能な限り以下を記録する。

- 目的
- 解析範囲
- 根拠となるファイル
- クラス・関数・ルート等
- 確認できた仕様
- データの関係
- 処理の流れ
- 不明点
- Task_Managerへの影響
- Phase 2で決定する事項

---

## 9. 推測禁止

重要な仕様を推測して確定扱いしてはいけない。

判断には可能な限り複数の根拠を使用する。

根拠:

- ソースコード
- API
- DBモデル
- Migration
- Frontend
- Test
- Configuration
- Docker
- Documentation

不明な場合は次のいずれかとして記録する。

```text
確認済み
有力
不明
追加確認が必要
```

---

## 10. Planeのコピー禁止

以下をそのままTaskManagerへ流用してはいけない。

- クラス名
- Entity名
- DTO名
- Service名
- Controller名
- Table名
- Column名
- Database名
- API名
- React Component名
- Project名
- Workspace名
- ユーザー情報
- メールアドレス
- URL
- 会社情報
- サンプルデータ
- Branding
- パスワード
- JWT Secret
- API Key
- OAuth Secret
- SMTP Secret
- その他の秘密情報

単純な文字列置換も禁止。

TaskManagerの責務・業務意味に基づいて新しい名前を設計する。

---

## 11. DB

MySQLを使用する。

MySQLの接続
- ホスト名：localhost
- ユーザー名：tomoyan
- パスワード：Tomoyaman@18

EF Core Migrationを使用する。

Planeと異なる独立したDB名、Table名、Column名を設計する。

DB設計では以下を確認する。

- Table
- Column
- PK
- FK
- Index
- Unique制約
- NULL
- Data Type
- Relation
- Delete behavior
- Audit fields
- Concurrency
- Migration

---

## 12. REST API

TaskManagerのBackendはREST APIとする。

APIについて以下を設計・実装する。

- HTTP Method
- Route
- Request
- Response
- Status Code
- Authentication
- Authorization
- Validation
- Error format
- Pagination
- Filter
- Sort

---

## 13. Swagger / OpenAPI

Swagger / OpenAPIを必須とする。

Swaggerは単なる画面ではなく、API契約として扱う。

Phase 2:

```text
API設計
↓
OpenAPI / Swaggerとして表現可能な状態にする
```

Phase 3:

```text
Swagger環境構築
↓
OpenAPI生成確認
```

Phase 4:

```text
API実装
↓
Swaggerと実装を一致させる
```

Phase 5～6:

```text
Swagger/API仕様をテスト対象として利用
```

Phase 8:

```text
Swagger UI
OpenAPI
Endpoint
Request/Response
認証
```

を最終確認する。

SwaggerにSecret等の機密情報を表示してはいけない。

---

## 14. UI/UX

Task_ManagerのUIデザインはPlaneから刷新する。

Planeから参考にしてよいもの:

- 必要な情報
- 業務フロー
- 操作頻度
- データ量
- ユーザー操作
- ナビゲーション上の要求

そのままコピーしてはいけない。

優先順位:

1. 分かりやすさ
2. 操作しやすさ
3. 情報を見つけやすいこと
4. 読みやすさ
5. 操作ミス防止
6. 一貫性
7. レスポンシブ
8. 見た目

---

## 15. 日本語

ユーザー向け表示は自然な日本語にする。

対象:

- Label
- Button
- Menu
- Dialog
- Error
- Validation
- Notification
- Tooltip
- Description
- Help
- Seed data
- Operator向けLog
- README等の利用者向け説明

単純な直訳は禁止。

技術的な識別子は英語でよい。

---

## 16. Backend

原則として以下をベースにする。

```text
src/
├── TaskManager.Api/
├── TaskManager.Application/
├── TaskManager.Domain/
├── TaskManager.Infrastructure/
└── TaskManager.sln
```

責務:

Domain:

- Entity
- Value Object
- Domain Rule

Application:

- Use Case
- Application Service
- DTO
- Validation
- Interface

Infrastructure:

- EF Core
- MySQL
- Repository等
- External Service

Api:

- Controller / Endpoint
- Authentication
- Authorization
- Middleware
- Swagger/OpenAPI
- HTTP処理

設計上の理由がある場合は変更してよいが、Markdownに理由を記録する。

---

## 17. Docker

Docker Composeを使用する。

Linux Containerを使用する。

Windows専用APIやWindows固有の構成を前提にしない。

`.env.example`を用意する。

実際のSecretはGitへ入れない。

---

## 18. Testing

以下を設計に応じて使用する。

- Unit Test
- Domain Test
- Application Test
- Integration Test
- API Test
- EF Core / MySQL Test
- Frontend Test
- E2E Test

実際に実行していないテストを「成功」と記載してはいけない。

---

## 19. Security

最低限以下を確認する。

- Authentication
- Authorization
- IDOR
- JWT
- Password
- CORS
- CSRF
- XSS
- SQL Injection
- Input Validation
- File Upload
- Path Traversal
- Rate Limit
- Error Leakage
- Log
- Secret
- Docker
- MySQL
- Dependency
- Swagger
- API Access Control

結果はMarkdownに記録する。

---

## 20. 完成条件

以下をすべて確認してから完成とする。

- 要件書
- 設計書
- DB設計
- API設計
- UI/UX設計
- セキュリティ設計
- 実装
- Migration
- Docker
- Backend Build
- Frontend Build
- Swagger
- Test Code
- Test Execution
- Security Review
- README
- Secret確認
- Planeとの独立性確認
- 各種Markdown更新

