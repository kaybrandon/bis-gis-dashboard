export function NeedHelpMark({ compact = false }: { compact?: boolean }) {
  return (
    <span
      className={compact ? 'need-help-mark is-compact' : 'need-help-mark'}
      title="Need help"
      aria-label="Need help"
    >
      <span className="need-help-mark-hand" aria-hidden>
        ✋
      </span>
      <span className="need-help-mark-bang" aria-hidden>
        !
      </span>
    </span>
  )
}
