/** GIS-UI-04 — Pending / All Pending highlight. Light blue in the GIS navy/blue family. */
export const PENDING_HIGHLIGHT = '#E6F4FF'
export const PENDING_HIGHLIGHT_TEXT = '#111827'
export const PENDING_HIGHLIGHT_BORDER = '#91CAFF'

/** QC07 mint zebra — must not be reused as the Pending highlight. */
export const PENDING_FORBIDDEN_GREEN = ['#E8F8F3', '#52c41a', '#AED20F', '#D4FF00', '#f6ffed', '#d9f7be'] as const

export const PENDING_HIGHLIGHT_CLASS = 'pending-highlight'
export const PENDING_HIGHLIGHT_ROW_CLASS = 'pending-highlight-row'
export const PENDING_HIGHLIGHT_CARD_CLASS = 'pending-highlight-card'
export const PENDING_HIGHLIGHT_ITEM_CLASS = 'pending-highlight-item'

export function isPendingStatus(name?: string | null) {
  return (name ?? '').trim().toLowerCase() === 'pending'
}

export function pendingHighlightClass(isPending: boolean, extra = '') {
  return [extra, isPending ? PENDING_HIGHLIGHT_CLASS : ''].filter(Boolean).join(' ')
}
