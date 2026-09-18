import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const review = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const cards = readFileSync(join(here, 'components/WorkItemCards.tsx'), 'utf8')
const api = readFileSync(join(here, 'api.ts'), 'utf8')
const chip = readFileSync(join(here, 'components/DocumentDifficultyChip.tsx'), 'utf8')

describe('GIS-UI-10 document difficulty surfaces', () => {
  it('shows score + why on Review and lets Editor+ override the band', () => {
    assert.match(review, /document-difficulty-panel/)
    assert.match(review, /DocumentDifficultyChip/)
    assert.match(review, /document-difficulty-reasons/)
    assert.match(review, /Override difficulty/)
    assert.match(review, /Use AI score/)
    assert.match(review, /Replace staff difficulty override\?/)
    assert.match(review, /Fill and re-score/)
    assert.match(review, /Fill, keep override/)
    assert.match(review, /rescore/)
    assert.match(review, /clearDifficultyOverride/)
    assert.match(review, /difficultyBand/)
  })

  it('shows a difficulty chip and column on the Manage Documents queue', () => {
    assert.match(manage, /title: 'Difficulty'/)
    assert.match(manage, /key: 'difficulty'/)
    assert.match(manage, /DocumentDifficultyChip/)
    assert.match(cards, /DocumentDifficultyChip/)
    assert.match(chip, /difficulty.overridden/)
  })

  it('reuses the AI-fill pass and keeps secrets off the SPA', () => {
    assert.match(api, /aiFillFromPdf/)
    assert.match(api, /rescore/)
    assert.match(api, /DocumentDifficulty/)
    assert.doesNotMatch(api, /documentIntelligence|DocumentIntelligence|formrecognizer|FormRecognizer/i)
    assert.doesNotMatch(review, /documentIntelligence|DocumentIntelligence|formrecognizer/i)
    assert.doesNotMatch(api, /AzureOpenAI__ApiKey|sk-[a-zA-Z0-9]|api-key\s*:/)
    assert.doesNotMatch(review, /AzureOpenAI__ApiKey/)
    assert.doesNotMatch(review, /no extractable text|cannot be AI-filled/i)
    assert.match(review, /scanned \/ image-only|page images/)
  })
})
