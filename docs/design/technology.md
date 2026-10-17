# 技術選定

- 確認日: 2026-10-06（NuGet / npm / Docker Hub の公開情報を確認）
- 方針: CLAUDE.md §3 の必須技術（.NET 10、React、Vite、TypeScript、MySQL、EF Core、REST、Swagger/OpenAPI、Docker）を前提に、それ以外のライブラリは「必要性が説明できるもの」だけを採用する。
- バージョンは確認時点の最新安定版。**正確なパッチ版は Phase 3 の環境構築時に互換性を確認して固定**し、本書を更新する。

## 1. 開発環境（ローカルで確認済み）

| 項目 | 確認結果 |
|---|---|
| OS | macOS（Darwin 24.2） |
| .NET SDK | 10.0.401 がインストール済み（他に 3.1 / 6 / 7 / 8） |
| Node.js | v24.10.0 |
| Docker | 29.5.2 |
| IDE | VS Code |

`global.json` で .NET SDK 10 を固定する（Phase 3）。

## 2. Backend

| 用途 | 採用 | 確認時点の版 | 理由 | 却下した候補 |
|---|---|---|---|---|
| ランタイム | .NET 10（ASP.NET Core） | SDK 10.0.401 | CLAUDE.md 指定 | — |
| Web API | ASP.NET Core **Controller**（`[ApiController]`） | 10.0.x | ルーティング・モデル検証・OpenAPI 生成が揃い、エンドポイント数（約 50）を機能単位で整理しやすい | Minimal API（エンドポイント数が多いと整理しにくい） |
| ORM | EF Core | 10.0.12 | CLAUDE.md 指定 | — |
| MySQL プロバイダ | **MySql.EntityFrameworkCore**（Oracle 公式） | 10.0.9（net10.0 向けに EF Core 10.0.9 へ依存） | **EF Core 10 に対応した安定版が存在する唯一の候補** | Pomelo.EntityFrameworkCore.MySql（最新安定版 9.0.0 で EF Core 10 未対応。確認日時点） |
| OpenAPI 生成 | Microsoft.AspNetCore.OpenApi | 10.0.12 | .NET 標準。OpenAPI 3.1 を出力。ビルド時生成（`Microsoft.Extensions.ApiDescription.Server`）で契約ファイルを出力できる | Swashbuckle の生成機能（.NET 標準に移行済み） |
| Swagger UI | Swashbuckle.AspNetCore.SwaggerUI（UI のみ） | 10.2.3 | CLAUDE.md が Swagger UI を求める。生成は .NET 標準、表示だけ Swagger UI を使う | Scalar（Swagger UI 指定のため） |
| 認証 | Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.12 | アクセストークン検証 | — |
| パスワードハッシュ | Microsoft.Extensions.Identity.Core の `PasswordHasher<T>` のみ利用 | 10.0.12 | 実績ある PBKDF2 実装（HMAC-SHA512）。Identity 全体（独自の表構成）は使わず、表を TaskManager 独自に設計できる | ASP.NET Core Identity 一式（表名・列名が固定され独自設計と衝突）、BCrypt 系外部ライブラリ |
| 入力検証 | DataAnnotations（Request DTO）+ Application での業務検証 | 標準 | 制約（必須・長さ・範囲・正規表現）が **OpenAPI スキーマに自動反映** され、契約と実装がずれない | FluentValidation（OpenAPI に反映されず二重管理になる） |
| レート制限 | ASP.NET Core 標準 `Microsoft.AspNetCore.RateLimiting` | 標準 | 追加依存なし | — |
| ログ | Microsoft.Extensions.Logging + JSON コンソール | 標準 | 追加依存なし。Docker で標準出力を収集 | Serilog（MVP では不要） |
| 時刻 | `TimeProvider` | 標準 | テストで時刻固定 | — |
| ID | `Guid.CreateVersion7()` | 標準（.NET 9 以降） | 時系列順の UUID でインデックス断片化を抑える | 連番 BIGINT（OD-05 参照） |

## 3. テスト（Backend）

| 用途 | 採用 | 確認時点の版 | 理由 |
|---|---|---|---|
| テストフレームワーク | xUnit（v3 系を Phase 3 で確認） | — | .NET で標準的 |
| API テスト | Microsoft.AspNetCore.Mvc.Testing（WebApplicationFactory） | 10.0.12 | 実際のパイプラインで API を検証 |
| MySQL | Testcontainers.MySql | 4.15.0 | 本番と同じ MySQL 8.4 でテスト |
| アサーション | 標準の Assert（追加ライブラリは採用しない） | — | ライセンス・依存を増やさない |
| OpenAPI 契約 | ビルド時に生成した `openapi.v1.json` とコミット済みスナップショットの比較 + 応答の JSON Schema 検証 | — | [testing-strategy.md](testing-strategy.md) §6 |

## 4. Frontend

| 用途 | 採用 | 確認時点の版 | 理由 | 却下した候補 |
|---|---|---|---|---|
| ライブラリ | React | 19.3.0 | CLAUDE.md 指定 | — |
| ビルド | Vite | 8.3.3 | CLAUDE.md 指定 | — |
| 言語 | TypeScript | 安定版 7.0.2 / 5.9.3 | CLAUDE.md 指定。**7.x はネイティブ実装への移行版のため、Vite・ESLint・Vitest との互換を Phase 3 で確認し、問題があれば 5.9 系を採用**（OD-16） | — |
| ルーティング | react-router | 8.4.0 | 標準的。SPA モードで使用 | TanStack Router（採用実績・情報量を優先） |
| サーバー状態 | @tanstack/react-query | 5.104.1 | キャッシュ・再取得・楽観的更新 | MobX / Redux（グローバルストアは不要） |
| API 型生成 | openapi-typescript | 7.13.0 | OpenAPI から型を生成 | 手書き型（ずれの原因） |
| API クライアント | openapi-fetch | 0.17.0 | 生成型と組み合わせて型安全に呼び出す。fetch ベースで軽量 | axios（型生成との一体化が弱い） |
| フォーム | react-hook-form | 7.89.0 | 再描画が少なく、大きめのフォームでも軽快 | — |
| 検証 | zod | 4.6.5 | スキーマから型と検証を同時に定義 | — |
| UI 部品 | react-aria-components | 1.21.1 | アクセシビリティ対応の部品（メニュー、ダイアログ、日付選択、コンボボックス） | 独自実装（a11y の作り込みが大きい）、大型 UI キット（見た目の自由度とバンドルサイズ） |
| スタイル | CSS Modules + CSS 変数（デザイントークン） | Vite 標準 | 追加依存なし。トークンで一貫性を担保 | Tailwind（Plane と同じ見た目に寄りやすい／クラス列が長く読みにくい） |
| Markdown | react-markdown + remark-gfm（生 HTML 無効） | Phase 3 で確認 | 安全な描画 | `dangerouslySetInnerHTML` |
| 日付 | date-fns（+ タイムゾーン対応は date-fns-tz または `Intl`） | Phase 3 で確認 | 期限区分の計算と日本語表示 | — |
| アイコン | lucide-react（または同等の MIT ライセンスのアイコン） | Phase 3 で確認 | 軽量・一貫したアイコン | — |
| ドラッグ&ドロップ | React Aria の DnD（`useDrag` / `useDrop`、キーボード操作対応） | 同上 | ボード操作をキーボードでも可能にする | 別途 DnD ライブラリ |

## 5. テスト（Frontend / E2E）

| 用途 | 採用 | 確認時点の版 |
|---|---|---|
| 単体・コンポーネント | Vitest + @testing-library/react | Vitest 5.0.3 |
| API モック | MSW | 3.0.2 |
| E2E | Playwright | 1.63.0 |
| a11y 自動検査 | @axe-core/playwright | Phase 3 で確認 |

## 6. インフラ

| 用途 | 採用 | 理由 |
|---|---|---|
| DB | `mysql:8.4`（LTS。確認時点 8.4.11） | 長期サポート版 |
| API 実行イメージ | `mcr.microsoft.com/dotnet/aspnet:10.0`（非 root ユーザー `app`） | 公式。ヘルスチェック用に curl を追加 |
| API ビルド | `mcr.microsoft.com/dotnet/sdk:10.0` | マルチステージビルド |
| Web 実行イメージ | `nginxinc/nginx-unprivileged`（安定版 alpine） | 非 root で 8080 待受 |
| Web ビルド | `node:24-alpine` | ローカルと同じメジャー版 |
| オーケストレーション | Docker Compose v2 | CLAUDE.md 指定 |

## 7. 文字コード・照合順序

- DB 既定: `utf8mb4` / `utf8mb4_0900_ai_ci`（大文字小文字・アクセントを区別しない検索）
- メール・プロジェクトキーなど厳密一致の列: 正規化して保存（小文字化・大文字化）したうえで一意インデックス
- 日本語のひらがな / カタカナの同一視は行わない（OD-17 で必要性を確認）

## 8. 採用しないもの（Plane で使われているが TaskManager では不要と判断）

| 技術 | 理由 |
|---|---|
| Redis | キャッシュ・ワンタイムコードが不要。レート制限はプロセス内で足りる |
| RabbitMQ / Celery 相当のキュー | 非同期処理は `BackgroundService` で足りる |
| S3 / MinIO | 添付は Docker ボリュームで足りる（将来差し替え可能な設計） |
| リアルタイム共同編集（Yjs 等） | 対象外 |
| リバースプロキシの別コンテナ（Caddy 等） | web（nginx）が中継を兼ねる。TLS は前段（社内のロードバランサ等）で終端する前提（OD-18） |

## 9. Phase 3 での確定事項（2026-10-08）

実際に導入・ビルドして確定した版と、設計からの変更点。正確な版はリポジトリの `Directory.Packages.props`、`dotnet-tools.json`、`frontend/task-manager-web/package-lock.json`、各 Dockerfile が正である。

| 項目 | Phase 2 の想定 | Phase 3 の確定 | 理由 |
|---|---|---|---|
| .NET SDK | 10.0.401 | `global.json`: 10.0.400 + `rollForward: latestFeature` | Docker の SDK イメージの最新が 10.0.400 のため。ローカルの 10.0.401 でもそのまま動く |
| ソリューション形式 | `TaskManager.sln` | `src/TaskManager.sln`（`dotnet new sln --format sln`） | .NET 10 SDK の既定は `.slnx`。指示どおり `.sln` を明示 |
| パッケージの版管理 | — | `Directory.Packages.props`（中央管理）+ `Directory.Build.props`（警告をエラー扱い、Nullable 有効） | 版の固定と品質の底上げ |
| MySQL プロバイダ | MySql.EntityFrameworkCore 10.0.9 | 同左 | 確認どおり |
| EF Core ツール | — | `dotnet-ef` 10.0.12 をローカルツールとして固定（`dotnet-tools.json`） | 誰が実行しても同じ版 |
| Swagger UI | 10.2.3 | Swashbuckle.AspNetCore.SwaggerUI 10.3.0 | 確認時点の最新安定版 |
| テスト基盤 | xUnit（v3 は要確認） | xunit.v3 4.0.1 + Microsoft.Testing.Platform（`global.json` の `test.runner`） | .NET 10 SDK の `dotnet test` は VSTest を使う xUnit v3 を実行できない。カバレッジは Phase 5 で `Microsoft.Testing.Extensions.CodeCoverage` を追加 |
| TypeScript（OD-16） | 7.0 か 5.9 | **5.9.3** | Vite テンプレートの既定は 6.0 だが、`openapi-typescript` 7.13.0 が `typescript ^5.x` のみに対応。型生成は契約の中核のため 5.9 系に揃えた |
| Lint | ESLint を想定 | **oxlint**（Vite テンプレートの既定）。`react/no-danger`・`jsx-a11y` を有効化 | 高速で設定が小さい。XSS 対策の `dangerouslySetInnerHTML` 検出も可能 |
| MSW | 3.0.2 | **2.15.0** | Vitest 5 の `@vitest/mocker` が `msw ^2.4.9` を要求 |
| React Aria Components | 1.21.1 | 1.22.0 | 確認時点の最新 |
| Markdown・日付・アイコン | Phase 3 で確認 | Phase 4 で必要になった時点で導入 | 使わないパッケージを先に入れない |
| Playwright / axe | 1.63.0 | Phase 5 で導入 | 同上 |
| API 実行イメージ | `aspnet:10.0` | `mcr.microsoft.com/dotnet/aspnet:10.0.12-noble`（Ubuntu 24.04） | 版を固定 |
| SDK イメージ | `sdk:10.0` | `mcr.microsoft.com/dotnet/sdk:10.0.400-noble` | 同上 |
| Web 実行イメージ | nginx-unprivileged（alpine） | `nginxinc/nginx-unprivileged:1.30.5-alpine`（安定版系列） | 同上 |
| Web ビルドイメージ | `node:24-alpine` | `node:24-alpine3.24` | 同上 |
| MySQL | 8.4 | `mysql:8.4.11` | 同上 |

## 10. Phase 9: グラフ・ガントチャートのライブラリの評価（2026-10-16）

ガントチャートとステータス別円グラフのために、ライブラリを追加するかを評価した。版・ライセンス・最終更新・展開後サイズは npm レジストリ（`npm view <名前> version license time.modified dist.unpackedSize dependencies`、2026-10-16 実行）で確認した値。

| 候補 | 用途 | 版 / ライセンス | 最終更新 | 展開後サイズ（npm） | 依存 | 評価 |
|---|---|---|---|---|---|---|
| recharts | 円グラフ | 3.10.1 / MIT | 2026-10-13 | 約 7.5MB | react-redux・immer・reselect・victory-vendor（d3）ほか | 保守は活発。依存が多く初回読み込みが増える。Plane の Frontend が採用（analysis/frontend.md）しており、独立性の観点でも避ける |
| chart.js + react-chartjs-2 | 円グラフ | 4.5.1 / MIT、5.3.1 / MIT | 2025-10 | 約 6.2MB + 55KB | @kurkle/color | 保守あり。Canvas 描画のため、円の中身をスクリーンリーダーに伝えるには別途の代替が必要 |
| @nivo/pie | 円グラフ | 0.99.0 / MIT | 2025-05 | 約 190KB（+ @nivo/core 等） | d3-shape・@nivo/* 6 パッケージ | 0.x 系。依存が多い |
| @visx/shape | 円グラフの部品 | 4.0.0 / MIT | 2026-06 | 約 230KB | @visx/* 4 パッケージ | 低レベル部品。結局 SVG を自分で組む量が多い |
| frappe-gantt | ガント | 1.2.2 / MIT | 2026-02 | 約 250KB | なし | DOM を直接操作する作りで React の状態管理と二重になる。見た目は同梱 CSS に依存 |
| gantt-task-react | ガント | 0.3.9 / MIT | 2022-07 | 約 620KB | なし | 2022 年以降更新がない（保守状況が不明） |
| **採用: 追加しない（SVG + CSS Modules で自作）** | 両方 | — | — | 0（追加の依存なし） | — | 下記の理由 |

採用の理由:

1. **CSP**: 画面の CSP は `style-src 'self'`（react-aria の固定スタイルのハッシュのみ許可）。実行時に `<style>` を挿入するライブラリは違反になる（E2E-08 が検出する）。自作なら CSS Modules とクラス指定だけで描ける。
2. **必要な機能が小さい**: 円グラフは扇形の SVG パス数本、ガントは日付 → 位置の一次変換と棒の配置のみ。軸の自動計算・アニメーション・ズームは不要。
3. **アクセシビリティ**: 円グラフは表（凡例を兼ねる）と要約、ガントは表形式の代替を必ず出す設計のため、ライブラリの a11y 機能に依存しない。
4. **初回読み込み**: 新しい画面は既存どおり遅延読み込みの画面に含め、初回読み込み（307KB）を増やさない。
5. **独立性**: Plane が使うライブラリ（recharts）・部品構成に寄らない。

日付の計算は既存の `utils/dates.ts`（`Intl` と UTC 基準の日付演算）を拡張する（date-fns を使わない方針は Phase 4 の C-09 と同じ）。Backend にも追加のパッケージはない。

