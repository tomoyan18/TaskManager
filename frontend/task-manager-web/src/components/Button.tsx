import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'

export type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant
  size?: 'normal' | 'small'
  iconOnly?: boolean
  /** 送信中など。押せない状態にし、文言を差し替える */
  busy?: boolean
  busyText?: string
}

export function Button({ variant = 'secondary', size = 'normal', iconOnly = false, busy = false, busyText, className, children, disabled, type = 'button', ...rest }: ButtonProps) {
  const classes = ['tm-btn']
  if (variant !== 'secondary') classes.push(`tm-btn--${variant}`)
  if (size === 'small') classes.push('tm-btn--small')
  if (iconOnly) classes.push('tm-btn--icon')
  if (className) classes.push(className)
  return (
    <button type={type} className={classes.join(' ')} disabled={disabled === true || busy} aria-busy={busy || undefined} {...rest}>
      {busy && busyText ? busyText : children}
    </button>
  )
}
