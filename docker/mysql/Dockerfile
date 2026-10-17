# TaskManager の DB イメージ（公式 mysql:8.4.11 を元に、使わない部品を除き、既知の脆弱性を減らす。Phase 7）
#  - openssl / openssl-libs を修正版に更新する（mysqld の TLS が使う）
#  - MySQL Shell（mysqlsh。同梱の Python ライブラリに既知の脆弱性がある）は使わないため削除する
#  - gosu（root から mysql 利用者へ切り替える道具。古い Go で作られている）は、最初から mysql 利用者で動かすため削除する
FROM mysql:8.4.11

RUN microdnf -y update openssl openssl-libs \
 && microdnf -y remove mysql-shell \
 && microdnf clean all \
 && rm -f /usr/local/bin/gosu

# 公式イメージのエントリポイントは、root 以外で起動すると利用者の切り替え（gosu）を行わない
USER mysql
