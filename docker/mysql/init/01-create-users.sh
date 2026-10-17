#!/bin/bash
# MySQL 初回起動時のみ実行される（docker-entrypoint-initdb.d）。
# アプリ用（DML のみ）とマイグレーション用（DDL + DML）のユーザーを分ける（docs/design/architecture.md §7.5）。
#
# 公式イメージのエントリポイントは、実行可能と判定した .sh は別プロセスで実行し、そうでなければ source する。
# Docker Desktop のバインドマウントでは実行権限の判定が実際と食い違うことがあるため（Phase 6 で検出）、
# 実行権限を付けたうえで、どちらの方法で呼ばれても動くようにする。
# パスワードはコマンドライン引数に出さない（source の場合は docker_process_sql、実行の場合は MYSQL_PWD 環境変数）。
set -eo pipefail  # source されたときもエントリポイントと同じ設定（-u は付けない）

: "${TM_DB_APP_USER:?TM_DB_APP_USER を設定してください}"
: "${TM_DB_APP_PASSWORD:?TM_DB_APP_PASSWORD を設定してください}"
: "${TM_DB_MIGRATOR_USER:?TM_DB_MIGRATOR_USER を設定してください}"
: "${TM_DB_MIGRATOR_PASSWORD:?TM_DB_MIGRATOR_PASSWORD を設定してください}"
: "${MYSQL_DATABASE:?MYSQL_DATABASE を設定してください}"

tm_run_sql() {
  if declare -F docker_process_sql > /dev/null; then
    docker_process_sql --database=mysql
  else
    # 初期化中の一時サーバー（ソケット接続のみ）に root で接続する
    MYSQL_PWD="${MYSQL_ROOT_PASSWORD:?MYSQL_ROOT_PASSWORD を設定してください}" \
      mysql --protocol=socket --socket=/var/run/mysqld/mysqld.sock -uroot --database=mysql
  fi
}

tm_run_sql <<SQL
CREATE USER IF NOT EXISTS '${TM_DB_MIGRATOR_USER}'@'%' IDENTIFIED BY '${TM_DB_MIGRATOR_PASSWORD}';
GRANT ALL PRIVILEGES ON \`${MYSQL_DATABASE}\`.* TO '${TM_DB_MIGRATOR_USER}'@'%';
CREATE USER IF NOT EXISTS '${TM_DB_APP_USER}'@'%' IDENTIFIED BY '${TM_DB_APP_PASSWORD}';
GRANT SELECT, INSERT, UPDATE, DELETE ON \`${MYSQL_DATABASE}\`.* TO '${TM_DB_APP_USER}'@'%';
FLUSH PRIVILEGES;
SQL
