// URL・パスの安全性の判定（Markdown のリンク・ログイン後の移動先で共用。初回読み込みを軽く保つため部品から分離）

/**
 * 画面内の相対パスか。ブラウザは `/\evil.example` や `/<タブ>/evil.example` を `//evil.example`（外部サイト）として扱うため、
 * 2 文字目の `/` `\\` と、制御文字・空白を含むものは拒否する（Phase 7 で修正）
 */
export function isSafeInternalPath(path: string): boolean {
  // oxlint-disable-next-line no-control-regex -- 制御文字を含むパスを拒否するため、意図して制御文字を指定している
  return /^\/(?![/\\])/.test(path) && !/[\\\s\u0000-\u001f\u007f]/.test(path)
}
