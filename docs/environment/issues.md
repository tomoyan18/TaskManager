# 環境構築で発生した問題と対応（Phase 3）

| ID | 状態 | 概要 |
|---|---|---|
| I-01 | 解決（Phase 6、2026-10-12） | Docker Desktop がイメージを取得できず、その後デーモンが 500 を返す状態になった |
| I-02 | 解決 | Docker デーモンが起動していなかった |
| I-03 | 解決 | Docker の SDK イメージ（10.0.400）がローカル SDK（10.0.401）より古く、global.json と合わない |
| I-04 | 解決 | .NET 10 SDK の `dotnet test` が xUnit v3（VSTest）を実行できない |
| I-05 | 解決 | `openapi-typescript` が TypeScript 6 に未対応 |
| I-06 | 解決 | MSW 3 が Vitest 5 と非互換 |
| I-07 | 解決 | Swagger UI が 401 になる |
| I-08 | 記録 | テンプレートのサンプルファイル削除がワイルドカードのため安全確認で止まった |
| I-09 | 記録 | macOS に `timeout` コマンドがない |
| I-10 | 回避（Phase 5） | Docker Desktop の不調が続くため、MySQL だけローカル（Homebrew の MySQL 8.0）で起動して確認した |
| I-11 | 解決（Phase 6） | api イメージのビルドで、ローカルでは無効にしたコード分析の規則がエラーになる |
| I-12 | 解決（Phase 6） | MySQL コンテナの初期化スクリプトが Permission denied で失敗し、DB 利用者が作られない |

---

## I-01 Docker Desktop がイメージを取得できない（Phase 6 で解決）

- 症状:
  1. `docker compose ... up -d --build db migrate` が 10 分以上出力なしで進まない
  2. `docker compose build --progress=plain migrate` が `resolve image config for docker-image://docker.io/docker/dockerfile:1` で `DeadlineExceeded: context deadline exceeded`
  3. `docker pull hello-world`（Docker Hub）、`docker pull mcr.microsoft.com/dotnet/aspnet:10.0.12-noble`（MCR）がどちらも数分間進まない
  4. ローカルに存在する `ubuntu:24.04` での `docker run` も応答しない
  5. Docker Desktop を `osascript -e 'quit app "Docker"'` で終了させようとしたが終了せず、以後 `docker info` が `500 Internal Server Error` を返す
- 確認したこと:
  - Mac 本体からは `https://mcr.microsoft.com/v2/`（200）、`https://registry-1.docker.io/v2/`（401＝到達可能）に接続できる → インターネット接続自体は問題ない
  - `docker info` の表示: `HTTP Proxy: http.docker.internal:3128`、`HTTPS Proxy: http.docker.internal:3128`（Docker Desktop 内部のプロキシ経由で通信する設定）
  - `docker buildx ls`: `desktop-linux` ビルダーは running
- 推定原因（**未確定**）: Docker Desktop の VM 側のネットワーク（内部プロキシ）または Docker Desktop 自体の状態異常。Docker Desktop はこの作業の開始時点で停止しており、作業中に `open -a Docker` で起動したもの。
- 行わなかったこと: Docker Desktop のプロセスの強制終了、設定ファイルの変更、ローカル（Homebrew）の MySQL 8.0 の使用（利用者の既存環境を変更するため、また設計の MySQL 8.4 と異なるため）
- 利用者にお願いしたい操作:
  1. Docker Desktop の画面を開き、ダイアログ（終了確認・更新・サインイン・利用規約など）が出ていないか確認する
  2. Docker Desktop を再起動する（メニューバーのクジラアイコン → Restart。終了しない場合は Troubleshoot → Restart Docker Desktop）
  3. Settings → Resources → Proxies が意図した設定か確認する（社内プロキシを使わない環境なら「Manual proxy configuration」がオフ）
  4. `docker pull hello-world` が数秒〜数十秒で終わることを確認する
  5. 解消後、[environment-setup.md](environment-setup.md) §5 の手順で残りの確認（MySQL 起動・接続・マイグレーション・コンテナ起動）を再開する

## I-02 Docker デーモンが起動していなかった（解決）

- 症状: `docker info` → `failed to connect to the docker API at unix:///Users/tomoya/.docker/run/docker.sock`
- 対応: `open -a Docker` で Docker Desktop を起動 → `docker info` で `linux x86_64 29.5.2` を確認

## I-03 SDK イメージの版と global.json（解決）

- 症状: MCR の SDK イメージの最新が `10.0.400`、ローカルは `10.0.401`。`global.json` を 10.0.401 にすると Docker 内のビルドが SDK 不一致で失敗する
- 対応: `global.json` を `"version": "10.0.400", "rollForward": "latestFeature"` に変更。ローカル（10.0.401）と Docker（10.0.400）の両方で同じ設定が使える

## I-04 `dotnet test` と xUnit v3（解決）

- 症状: `Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later.`
- 原因: xunit.v3 4.x は Microsoft.Testing.Platform（MTP）で動き、.NET 10 SDK の `dotnet test` は MTP の利用を `global.json` で明示する必要がある
- 対応: `global.json` に `"test": { "runner": "Microsoft.Testing.Platform" }` を追加。VSTest 用のパッケージ（Microsoft.NET.Test.Sdk、xunit.runner.visualstudio、coverlet.collector）を削除
- 補足: テストが 0 件のため `dotnet test` は終了コード 8（Zero tests ran）になる。Phase 5 でテストを追加すれば 0 になる。カバレッジは Phase 5 で `Microsoft.Testing.Extensions.CodeCoverage` を追加する

## I-05 TypeScript の版（解決）

- 症状: `npm install -D openapi-typescript` が `ERESOLVE`（`peer typescript@"^5.x" from openapi-typescript@7.13.0`、テンプレートは `typescript ~6.0.2`）
- 対応: TypeScript を `~5.9.3` に固定（OD-16 を決定）。`npm run typecheck` / `build` が成功することを確認

## I-06 MSW の版（解決）

- 症状: `msw@3.0.2` を入れると `npm ls` が `invalid: "^2.4.9" from @vitest/mocker`
- 対応: `msw@^2.15.0` に固定

## I-07 Swagger UI が 401（解決）

- 症状: `/swagger/index.html` が 401（Problem Details）
- 原因: 認可の既定ポリシー（ログイン必須）は、コントローラーに対応しないリクエストにも適用される。Swagger UI は静的ファイルを返すミドルウェアで、`UseAuthorization` の後に置いていた
- 対応: `UseSwaggerUI` を `UseAuthentication` / `UseAuthorization` より前に移動。公開の可否は `Swagger:Enabled` で制御（本番既定 false）。修正後 200 を確認

## I-08 サンプルファイルの削除が止まった（記録）

- 症状: `rm -rf ... public/*` を含むコマンドが安全確認で拒否された（ワイルドカードで対象が静的に特定できないため）。コマンド全体が未実行
- 対応: 削除対象を `ls` で確認し、ファイル名を 1 つずつ絶対パスで指定して削除した

## I-09 `timeout` コマンドがない（記録）

- macOS には GNU `timeout` がない。時間制限が必要な場合は `perl -e 'alarm 60; exec @ARGV' <command>` を使う（ただし `docker pull` は SIGALRM で止まらなかった）

## I-10 MySQL をローカルで起動して確認（回避・Phase 5）

- 2026-10-11 時点でも `docker info` が応答しない（I-01 は未解決）。
- Phase 5 の指示により、MySQL だけローカル PC で起動した（`brew services start mysql@8.0` → 8.0.46。Phase 4 時点の abseil の版ずれは解消していた）。
- 確認の内容と結果は [../implementation/status.md](../implementation/status.md) §8。
- 注意: mysql コマンドで日本語が `???` と表示される場合はクライアント側の文字コードの問題。`--default-character-set=utf8mb4` を付けて接続する（保存された値は HEX で UTF-8 を確認済み）。

### I-01 の解決（2026-10-12、Phase 6）

- 状態: 10/8 から Docker Desktop の内部 API が無応答（`/_ping` がタイムアウト、仮想マシンのプロセスなし）。
- 対応: 利用者の了承を得て Docker Desktop のプロセスを強制終了（`pkill -9 -f com.docker.backend` など。root の `com.docker.vmnetd` は対象外）し、`open -a Docker` で再起動。約 40 秒で応答が戻り、イメージの取得もできた。
- 再発時: Docker Desktop を再起動する（メニューの Restart、応答しない場合は上記）。

## I-11 api イメージのビルドでコード分析がエラーになる（解決・Phase 6）

- 症状: `docker compose up -d --build` で CA1711・CA1000 がエラー（ローカルの `dotnet build` は成功）。
- 原因: `docker/api/Dockerfile` が `.editorconfig` をコピーしておらず、規則の調整（C-14）がコンテナ内のビルドに効いていなかった。
- 対応: Dockerfile で `.editorconfig` もコピーする。

## I-12 MySQL の初期化スクリプトが Permission denied（解決・Phase 6）

- 症状: db が unhealthy になり、ログに `/docker-entrypoint-initdb.d/01-create-users.sh: Permission denied`。アプリ用・マイグレーション用の DB 利用者が作られない。
- 原因: 公式イメージのエントリポイントは「実行可能」と判定した .sh を実行し、そうでなければ source する。Docker Desktop のバインドマウントでは、実行権限のないファイル（644）も実行可能と判定され、実行に失敗した。スクリプトは source される前提（`docker_process_sql` を使用）だった。
- 対応: スクリプトに実行権限を付け、source されても別プロセスで実行されても動くようにした（別プロセスの場合はソケット経由で root 接続し、パスワードは `MYSQL_PWD` 環境変数で渡す）。既に初期化済みのボリュームではスクリプトが再実行されないため、`docker compose down -v` で作り直す必要がある（データが消える）。
