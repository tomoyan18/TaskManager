import type { ReactNode } from 'react'
import { Dialog as AriaDialog, Heading, Modal, ModalOverlay } from 'react-aria-components'

type DialogProps = {
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  title: string
  children: ReactNode
  /** 閉じる前の確認（入力途中の破棄確認など）。false を返すと閉じない */
  canClose?: () => boolean
  wide?: boolean
  /** 危険な確認ダイアログでは alertdialog を使う */
  role?: 'dialog' | 'alertdialog'
}

/** React Aria のモーダル。フォーカスの閉じ込め・Esc で閉じる・閉じた後のフォーカス復帰を部品側で行う */
export function Dialog({ isOpen, onOpenChange, title, children, canClose, wide = false, role = 'dialog' }: DialogProps) {
  const handleOpenChange = (open: boolean) => {
    if (!open && canClose !== undefined && !canClose()) return
    onOpenChange(open)
  }
  return (
    <ModalOverlay isOpen={isOpen} onOpenChange={handleOpenChange} isDismissable={role === 'dialog'} className="tm-modal-overlay">
      <Modal className={wide ? 'tm-modal tm-modal--wide' : 'tm-modal'}>
        <AriaDialog className="tm-dialog" role={role}>
          <Heading slot="title" className="tm-dialog__title">{title}</Heading>
          {children}
        </AriaDialog>
      </Modal>
    </ModalOverlay>
  )
}
