import type { ReactNode } from 'react'
import { Button } from './Button'
import { Dialog } from './Dialog'

type ConfirmDialogProps = {
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  title: string
  children: ReactNode
  confirmLabel: string
  onConfirm: () => void
  busy?: boolean
  danger?: boolean
}

/**
 * 確認ダイアログ（ux-design.md §3）。「何を」「どうなるか」を本文に書き、ボタンは動詞にする。
 * 初期フォーカスは「キャンセル」。危険な操作は赤いボタンを右側に置く。
 */
export function ConfirmDialog({ isOpen, onOpenChange, title, children, confirmLabel, onConfirm, busy = false, danger = true }: ConfirmDialogProps) {
  return (
    <Dialog isOpen={isOpen} onOpenChange={onOpenChange} title={title} role="alertdialog">
      <div>{children}</div>
      <div className="tm-dialog__footer">
        <Button autoFocus onClick={() => onOpenChange(false)} disabled={busy}>キャンセル</Button>
        <Button variant={danger ? 'danger' : 'primary'} onClick={onConfirm} busy={busy} busyText="処理しています…">
          {confirmLabel}
        </Button>
      </div>
    </Dialog>
  )
}
