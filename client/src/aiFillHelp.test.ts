import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  AI_FILL_HELP_BULLETS,
  AI_FILL_HELP_TIP_STORAGE_KEY,
  AI_FILL_HELP_TITLE,
  dismissAiFillHelpTip,
  helpEmphasisParts,
  isAiFillHelpTipDismissed,
} from './aiFillHelp.ts'

const here = dirname(fileURLToPath(import.meta.url))
const help = readFileSync(join(here, 'aiFillHelp.ts'), 'utf8')
const screen = readFileSync(join(here, 'components/AiFillHelpScreen.tsx'), 'utf8')
const review = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')

const stamped = [
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

describe('GIS How AI fill works help', () => {
  it('locks the stamped title and bullets', () => {
    assert.equal(AI_FILL_HELP_TITLE, 'How AI fill works')
    assert.deepEqual([...AI_FILL_HELP_BULLETS], [...stamped])
    assert.match(screen, /AI_FILL_HELP_TITLE/)
    assert.match(screen, /AI_FILL_HELP_BULLETS/)
    assert.doesNotMatch(help, /DeedAi|deedAi|deed-ai/)
    assert.doesNotMatch(screen, /DeedAi|deedAi|deed-ai/)
  })

  it('covers vision PDFs and the same amber Approve \+ Save rule', () => {
    const copy = AI_FILL_HELP_BULLETS.join('\n')
    assert.match(copy, /Scanned \/ image-only PDFs/)
    assert.match(copy, /selectable text layer/)
    assert.match(copy, /\*\*amber\*\*/)
    assert.match(copy, /\*\*Approve\*\* \(or edit\) and \*\*Save\*\*/)
    assert.match(copy, /never silent-commits/)
    assert.match(copy, /\*\*pending\*\*, \*\*finished\*\*, or \*\*failed\*\*/)
    assert.match(copy, /\*\*Retry\*\* \/ \*\*AI fill from PDF\*\*/)
    assert.match(copy, /hint\*\* for triage/)
    assert.match(copy, /\*\*manual\*\* only/)
    assert.match(copy, /\*\*CAD \/ web map\*\*/)
    assert.match(copy, /one per line/)
    assert.match(copy, /Saved values stay/)
  })

  it('is discoverable from Review via a help link and first-Review soft tip', () => {
    assert.match(review, /AiFillHelpOpenButton/)
    assert.match(review, /AiFillHelpModal/)
    assert.match(review, /AiFillHelpFirstReviewTip/)
    assert.match(review, /setAiFillHelpOpen/)
    assert.match(review, /title-with-help/)
    assert.match(review, /help-tip-button/)
    assert.match(screen, /data-slot="ai-fill-help-open"/)
    assert.match(screen, /data-slot="ai-fill-help-tip"/)
    assert.match(screen, /data-slot="ai-fill-help-modal"/)
    assert.match(screen, /AiFillHelpFirstReviewTip/)
    assert.equal(AI_FILL_HELP_TIP_STORAGE_KEY, 'gis.aiFillHelpTip')
  })

  it('does not change Approve \/ Save rules on Review', () => {
    assert.match(review, /const approveField =/)
    assert.match(review, /<Button size="small" type="primary" loading=\{saving\} disabled=\{!dirty\} onClick=\{\(\) => void save\(\)\}>Save<\/Button>/)
    assert.match(review, /Approve AI fields/)
    assert.match(review, /Review amber fields, then Save/)
    assert.doesNotMatch(review, /silent-commit|autoSaveAi|save\(\)\s*;\s*\/\/ AI/)
    assert.doesNotMatch(screen, /api\.updateWorkItem|api\.aiFillFromPdf/)
  })

  it('renders locked emphasis markers and remembers tip dismiss', () => {
    assert.deepEqual(helpEmphasisParts('AI **assists** Review'), [
      { text: 'AI ', bold: false },
      { text: 'assists', bold: true },
      { text: ' Review', bold: false },
    ])
    const memory = new Map<string, string>()
    const storage = {
      getItem: (key: string) => memory.get(key) ?? null,
      setItem: (key: string, value: string) => {
        memory.set(key, value)
      },
    }
    assert.equal(isAiFillHelpTipDismissed(storage), false)
    dismissAiFillHelpTip(storage)
    assert.equal(isAiFillHelpTipDismissed(storage), true)
  })
})
