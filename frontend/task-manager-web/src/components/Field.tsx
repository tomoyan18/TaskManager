import { useId, type ReactElement, type ReactNode } from 'react'

type FieldProps = {
  label: string
  required?: boolean
  hint?: ReactNode
  error?: string
  /** 入力部品。id と aria 属性を受け取る関数 */
  children: (props: { id: string; 'aria-invalid': boolean; 'aria-describedby'?: string; 'aria-required'?: boolean }) => ReactElement
}

/** ラベル・補足・エラーを入力部品に関連付ける（ui-design.md §7.6、§8: ラベルは常に表示） */
export function Field({ label, required = false, hint, error, children }: FieldProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined
  return (
    <div className="tm-field">
      <label className="tm-field__label" htmlFor={id}>
        {label}
        {required && <span className="tm-field__required">必須</span>}
      </label>
      {children({ id, 'aria-invalid': error !== undefined, 'aria-describedby': describedBy, 'aria-required': required || undefined })}
      {hint && <div id={hintId} className="tm-field__hint">{hint}</div>}
      {error && <div id={errorId} className="tm-field__error">⚠ {error}</div>}
    </div>
  )
}
