# frontend/task-manager-web/src の構成

docs/design/architecture.md §6 と docs/design/ui-design.md §7 に対応する。

| ディレクトリ | 役割 |
|---|---|
| `app/` | ルーティング、レイアウト（AppShell）、エラー境界 |
| `api/` | openapi-fetch クライアントと、OpenAPI から生成した型（`schema.d.ts` は手で編集しない。`npm run generate:api` で再生成） |
| `features/` | 機能単位（auth, my-tasks, projects, tasks, milestones, notices, search, admin-users）。Phase 4 で追加 |
| `components/` | 汎用 UI 部品 |
| `hooks/` | 汎用フック |
| `i18n/` | 画面文言（日本語） |
| `styles/` | デザイントークン（CSS 変数）と全体スタイル |
| `utils/` | 日付表示・タスクキー解析など |
| `constants/` | 入力上限など API 設計と共通の定数 |
