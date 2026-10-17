# TaskManager 設計書（Phase 2）

読む順番の目安:

1. [requirements.md](requirements.md) — 目的・対象ユーザー・対象外・機能要件・非機能要件
2. [open-decisions.md](open-decisions.md) — **承認待ちの判断**（ここを先に確認）
3. [naming-rules.md](naming-rules.md) — 用語集と命名規則、Plane との独立性の照合
4. [architecture.md](architecture.md) — 層構成・依存方向・Frontend 構成・Docker Compose
5. [technology.md](technology.md) — 採用技術と版（2026-10-06 確認）
6. [domain-model.md](domain-model.md) — Entity・業務ルール・Application Service
7. [database-design.md](database-design.md) — MySQL の表定義・制約・索引・採番
8. [api-design.md](api-design.md) — REST API と OpenAPI 契約（64 エンドポイント。Phase 9 で 3 件追加）
9. [authentication-authorization.md](authentication-authorization.md) — 認証・ロール・権限・オブジェクト認可
10. [ui-design.md](ui-design.md) / [ux-design.md](ux-design.md) — 画面・部品・操作・文言
11. [security-design.md](security-design.md) — セキュリティ対策一覧（Phase 7 の検査表）
12. [testing-strategy.md](testing-strategy.md) — テストの種類・認可マトリクス・OpenAPI 契約テスト
13. [migration-strategy.md](migration-strategy.md) — EF Core マイグレーションと初期データ

根拠となる Phase 1 解析: [../analysis/](../analysis/)

Phase 9（ガントチャート・ステータス別円グラフの追加）の調査・影響分析・変更記録: [../corrections/phase-09/](../corrections/phase-09/)
