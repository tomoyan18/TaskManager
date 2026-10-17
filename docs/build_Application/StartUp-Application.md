# アプリケーションの起動手順

TaskManager を起動するためのコマンドと手順です。
**方法 A（Docker）** と **方法 B（開発用: Backend・Frontend を個別に起動）** の 2 通りがあります。
どちらも 2026-10-13 に、本書のコマンドで画面が開き、画面からバックエンド（API・DB）に接続できることを確認しました（§5）。

- 前提の OS: macOS（Linux でも同じ）。コマンドは zsh / bash で実行する。
- 作業ディレクトリ: リポジトリのルート（例: `/Volumes/OutSSD/CSharp/TaskManager`）

## 0. 必要なもの

| もの | 方法 A | 方法 B | 確認するコマンド |
|---|---|---|---|
| Docker Desktop（Docker Compose v2） | ○ | ○（DB に使う） | `docker version` / `docker compose version` |
| openssl | ○ | ○ | `openssl version` |
| .NET SDK 10.0.400 以上 | | ○ | `dotnet --version` |
| Node.js 24 以上 | | ○ | `node --version` |

Docker Desktop が起動していること（メニューバーのクジラのアイコン）を確認してから始めてください。
`docker info` が応答しない場合は Docker Desktop を再起動します（docs/environment/issues.md I-01）。

## 1. 初回だけ: 環境変数ファイル（.env）を作る

```bash
cd /Volumes/OutSSD/CSharp/TaskManager
cp .env.example .env
chmod 600 .env
```

`.env` をエディタで開き、空欄の値を設定します。値は次のコマンドで**それぞれ別に**生成してください（他の場所の値を使い回さない）。

```bash
openssl rand -hex 24      # MYSQL_ROOT_PASSWORD に貼る
openssl rand -hex 24      # TM_DB_APP_PASSWORD に貼る
openssl rand -hex 24      # TM_DB_MIGRATOR_PASSWORD に貼る
openssl rand -base64 48   # TM_JWT_SIGNING_KEY に貼る
```

| 変数 | 設定する値 |
|---|---|
| `TM_BOOTSTRAP_ADMIN_EMAIL` | 初期システム管理者のメールアドレス（例: `admin@example.test`） |
| `TM_BOOTSTRAP_ADMIN_PASSWORD` | 初期システム管理者の一時パスワード（12 文字以上。`;` `'` `"` を含めない。例: `openssl rand -hex 12` の値） |

`.env` は Git に登録されません。値を画面共有やチャットに貼らないでください。

## 2. 方法 A: Docker で起動する（推奨・本番相当）

### 2.1 起動

```bash
cd /Volumes/OutSSD/CSharp/TaskManager

# イメージをビルドしてバックグラウンドで起動する（初回は数分かかる）
docker compose up -d --build
```

### 2.2 起動の確認

```bash
# 4 つのサービスの状態を確認する
docker compose ps -a
```

次の状態になれば準備完了です（起動直後は `starting` のことがあります。数十秒待って再実行してください）。

| サービス | 期待する状態 |
|---|---|
| db | `Up ... (healthy)` |
| migrate | `Exited (0)`（マイグレーションを適用して終了する） |
| api | `Up ... (healthy)` |
| web | `Up ... (healthy)`、ポート `0.0.0.0:8080->8080/tcp` |

```bash
# 画面とバックエンドの接続を確認する
curl -s http://localhost:8080/healthz                # → ok
curl -s http://localhost:8080/api/v1/system/info     # → {"name":"TaskManager","version":"1.0.0"}
```

### 2.3 画面を開く

1. ブラウザで **http://localhost:8080** を開く。
2. `.env` の `TM_BOOTSTRAP_ADMIN_EMAIL` と `TM_BOOTSTRAP_ADMIN_PASSWORD` でログインする。
3. 「パスワードの設定」画面で新しいパスワード（12 文字以上。メールアドレスや名前を含まないもの）を設定する。
4. 「自分のタスク」が表示されれば、画面とバックエンドが接続できている。
5. 「利用者管理」→「利用者を追加」で利用者を発行し、表示された一時パスワードを本人に伝える。
6. 「プロジェクト一覧」→「プロジェクトを作成」から使い始める。

### 2.4 停止・再起動・ログ

| 目的 | コマンド |
|---|---|
| 停止する（データは残る） | `docker compose down` |
| もう一度起動する | `docker compose up -d` |
| ソースを変更した後に作り直す | `docker compose up -d --build` |
| API のログを見る | `docker compose logs -f api`（終了は Ctrl+C） |
| すべてのログを見る | `docker compose logs -f` |
| データも含めて消す（**DB と添付ファイルが消える**） | `docker compose down -v` |

### 2.5 うまくいかないとき

| 症状 | 確認・対処 |
|---|---|
| `MYSQL_ROOT_PASSWORD を .env に設定してください` で止まる | `.env` の空欄を埋める（§1） |
| api が healthy にならない | `docker compose logs api` を確認。`Jwt:SigningKey` のエラーなら `TM_JWT_SIGNING_KEY` が 32 バイト以上の Base64 か確認する |
| db が unhealthy | `docker compose logs db` を確認。`.env` の DB パスワードを後から変えた場合は、既存のボリュームと合わないため `docker compose down -v` で作り直す（データが消える） |
| ポート 8080 が使用中 | `.env` の `TM_WEB_PORT` を変える（例: 8081）。開くアドレスも変わる |
| ログインできない（初期管理者） | 初期管理者は「システム管理者が 1 人もいない」初回起動時だけ作られる。設定を後から入れた場合は `docker compose down -v` で作り直すか、既存の管理者で利用者を発行する |

## 3. 方法 B: 開発用に Backend・Frontend を個別に起動する

DB だけを Docker で動かし、API と画面はローカルで動かします（ソースの変更がすぐ反映されます）。
§1 の `.env` を作ってから行ってください。ターミナルを 2 つ使います。

### 3.1 DB を起動してマイグレーションを適用する（ターミナル 1）

```bash
cd /Volumes/OutSSD/CSharp/TaskManager

# DB を 127.0.0.1:3307 に公開して起動し、マイグレーションを適用する
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db migrate

# db が healthy、migrate が Exited (0) になったことを確認する
docker compose ps -a
```

### 3.2 API を起動する（ターミナル 1 のまま）

```bash
cd /Volumes/OutSSD/CSharp/TaskManager

# .env を読み込み、API の設定を環境変数で渡す（値は画面に表示しない）
set -a; . ./.env; set +a
export Jwt__SigningKey="$TM_JWT_SIGNING_KEY"
export ConnectionStrings__TaskManager="Server=127.0.0.1;Port=${TM_DB_PORT:-3307};Database=${TM_DB_NAME:-taskmanager_db};User ID=${TM_DB_APP_USER:-taskmanager_app};Password=${TM_DB_APP_PASSWORD};SslMode=Required"
export Bootstrap__AdminEmail="$TM_BOOTSTRAP_ADMIN_EMAIL" Bootstrap__AdminPassword="$TM_BOOTSTRAP_ADMIN_PASSWORD"
export Attachments__RootPath="$PWD/artifacts/attachments"
mkdir -p artifacts/attachments

# 起動する（http://localhost:5080。停止は Ctrl+C）
dotnet run --project src/TaskManager.Api --launch-profile http
```

`Now listening on: http://localhost:5080` と表示されれば起動しています。別のターミナルで確認できます。

```bash
curl -s http://localhost:5080/health/ready    # → Healthy（DB に接続できている）
```

### 3.3 画面を起動する（ターミナル 2）

```bash
cd /Volumes/OutSSD/CSharp/TaskManager/frontend/task-manager-web

npm ci          # 初回、または package-lock.json が変わったとき
npm run dev     # http://localhost:5173。/api は http://localhost:5080 に中継される（停止は Ctrl+C）
```

```bash
# 画面経由で API に接続できることを確認する
curl -s http://localhost:5173/api/v1/system/info   # → {"name":"TaskManager","version":"1.0.0"}
```

ブラウザで **http://localhost:5173** を開き、§2.3 と同じ手順でログインします。

### 3.4 開発時に使える画面

| URL | 内容 |
|---|---|
| http://localhost:5173 | 画面（Vite 開発サーバー） |
| http://localhost:5080/swagger | Swagger UI（Development のみ。右上の Authorize にアクセストークンを入れて実行） |
| http://localhost:5080/openapi/v1.json | OpenAPI 文書 |
| http://localhost:5080/health/ready | ヘルスチェック（DB 接続を含む） |

Swagger UI 用のアクセストークンは、Swagger UI の `POST /api/v1/auth/login` を「Try it out」で実行して得られる `accessToken` を使います（有効期限 15 分）。

### 3.5 停止

```bash
# ターミナル 1・2 で Ctrl+C（API と画面を止める）
cd /Volumes/OutSSD/CSharp/TaskManager
docker compose -f docker-compose.yml -f docker-compose.dev.yml down   # DB を止める（データは残る）
```

方法 A と方法 B は同じボリューム（DB）を使います。方法 B の後に方法 A で起動する場合は、`docker compose -f docker-compose.yml -f docker-compose.dev.yml down` で止めてから §2.1 を実行してください（ポート 5080・3307 の公開が残らないようにするため）。

## 4. 方法 A の Swagger を一時的に使う

本番相当の構成では Swagger を公開しません。Docker のまま Swagger を確認したい場合は、開発用の上書き設定で起動します（API を 127.0.0.1:5080 にだけ公開します）。

```bash
cd /Volumes/OutSSD/CSharp/TaskManager
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
# → http://localhost:5080/swagger
# 戻すとき
docker compose -f docker-compose.yml -f docker-compose.dev.yml down && docker compose up -d
```

## 5. この手順の確認結果（2026-10-13）

| 方法 | 確認した内容 | 結果 |
|---|---|---|
| A（Docker） | 空のボリュームから `docker compose up -d --build` | db healthy → migrate Exited (0) → api healthy → web healthy（約 80 秒） |
| A（Docker） | `curl http://localhost:8080/healthz`、`/api/v1/system/info` | `ok`、`{"name":"TaskManager","version":"1.0.0"}` |
| A（Docker） | ブラウザで初期管理者のログイン → パスワード変更 → 利用者の発行 → プロジェクト・タスクの操作（E2E 8 シナリオ） | 8 / 8 成功 |
| A + 開発用の上書き（§4） | Swagger UI を開き、Authorize 前後で `GET /api/v1/me` を実行 | 401 → 200 |
| B（個別起動） | §3.1〜3.3 のコマンドをそのまま実行 | API `Healthy`、画面 200、画面経由の API 200。ブラウザでログインしてタスク一覧を表示し、再読み込み後もログインが保たれる |
| A（Docker、2026-10-16 Phase 9） | 空のボリュームから `docker compose up -d --build` | migrate が `InitialSchema` と `AddTaskStatusSummaryIndex`（Phase 9 で追加した索引）を適用して正常終了、api・web healthy。E2E 9 / 9 成功（ガント・円グラフを含む。docs/corrections/phase-09/verification-report.md） |
