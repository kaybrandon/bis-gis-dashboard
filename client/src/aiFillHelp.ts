/** Stamped GIS Review help — copy locked. Do not drift meaning. */

export const AI_FILL_HELP_TITLE = 'How AI fill works'

export const AI_FILL_HELP_TIP_STORAGE_KEY = 'gis.aiFillHelpTip'

export const AI_FILL_HELP_BULLETS = [
  'AI **assists** Review — it does **not** finalize the document for you.',
  'After upload, a scan may run automatically. Status can show **pending**, **finished**, or **failed** (use **Retry** / **AI fill from PDF** if it fails). Upload still succeeds even if AI fails.',
  '**Scanned / image-only PDFs** are supported — you do not need a selectable text layer for AI fill to run.',
  'Proposed fields show in **amber**. You must **Approve** (or edit) and **Save** before they count. AI never silent-commits alone.',
  '**Difficulty** (Easy / Medium / Hard) is a **hint** for triage — staff can override; it is not a grade of your work.',
  '**Property IDs** are **manual** only — type or paste them yourself. AI does **not** suggest, fill, or approve this field.',
  'On **CAD / web map** PDFs, parcel labels on the map are **not** copied into Property IDs.',
  'Property IDs appear **one per line**. Saved values stay after reopen or an AI re-run.',
  'Manual **AI fill from PDF** remains available anytime for a re-run.',
] as const

export type HelpEmphasisPart = { text: string; bold: boolean }

export function helpEmphasisParts(text: string): HelpEmphasisPart[] {
  const parts: HelpEmphasisPart[] = []
  const re = /\*\*(.+?)\*\*/g
  let last = 0
  let match: RegExpExecArray | null
  while ((match = re.exec(text))) {
    if (match.index > last) {
      parts.push({ text: text.slice(last, match.index), bold: false })
    }
    parts.push({ text: match[1], bold: true })
    last = match.index + match[0].length
  }
  if (last < text.length) {
    parts.push({ text: text.slice(last), bold: false })
  }
  return parts
}

export function isAiFillHelpTipDismissed(storage?: Pick<Storage, 'getItem'> | null): boolean {
  try {
    const store = storage ?? (typeof localStorage === 'undefined' ? null : localStorage)
    return store?.getItem(AI_FILL_HELP_TIP_STORAGE_KEY) === '1'
  } catch {
    return false
  }
}

export function dismissAiFillHelpTip(storage?: Pick<Storage, 'setItem'> | null): void {
  try {
    const store = storage ?? (typeof localStorage === 'undefined' ? null : localStorage)
    store?.setItem(AI_FILL_HELP_TIP_STORAGE_KEY, '1')
  } catch {
    // Ignore quota / private-mode failures; the tip can show again.
  }
}
