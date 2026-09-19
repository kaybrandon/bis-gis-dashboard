import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const review = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')
const panel = readFileSync(join(here, 'components/DeedPlatPanel.tsx'), 'utf8')
const timeLog = readFileSync(join(here, 'components/TimeLogPanel.tsx'), 'utf8')
const comments = readFileSync(join(here, 'components/CommentsPanel.tsx'), 'utf8')
const notes = readFileSync(join(here, 'components/InternalNotesPanel.tsx'), 'utf8')
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const api = readFileSync(join(here, 'api.ts'), 'utf8')
const css = readFileSync(join(here, 'index.css'), 'utf8')
const fill = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/AiFill/WorkItemAiFillService.cs'), 'utf8')

describe('QC4-07 Time log above Comments + QC4-06 deed/plat panel', () => {
  it('renders Work item, Time log, deed/plat, Comments, then Internal Notes', () => {
    const time = review.indexOf('<TimeLogPanel')
    const deed = review.indexOf('<DeedPlatPanel')
    const comment = review.indexOf('<CommentsPanel')
    const internal = review.indexOf('<InternalNotesPanel')
    assert.ok(time > 0)
    assert.ok(deed > time, 'Fail if: QC4-06 panel not under Time log')
    assert.ok(comment > deed, 'Fail if: Time log still below Comments')
    assert.ok(internal > comment)
  })

  it('keeps time logging controls and comment/note ACL copy', () => {
    assert.match(timeLog, /Time log/)
    assert.match(timeLog, /Log time/)
    assert.match(timeLog, /Entry note/)
    assert.match(timeLog, /Worked on/)
    assert.match(timeLog, /Total time/)
    assert.match(comments, /Comments — client visible/)
    assert.match(comments, /Client-visible comment/)
    assert.match(notes, /Internal Notes — staff only/)
    assert.match(notes, /hidden from clients/)
    assert.match(notes, /canSeeInternalNotes/)
    assert.match(review, /canPostComments/)
  })

  it('deed/plat panel has the five named fields and does not silently overwrite saved corrections', () => {
    assert.match(panel, /aria-label="Survey"/)
    assert.match(panel, /aria-label="Abstract"/)
    assert.match(panel, /aria-label="Lot\/Block"/)
    assert.match(panel, /aria-label="Subdivision"/)
    assert.match(panel, /aria-label="Legal Description"/)
    assert.match(panel, /Input\.TextArea/)
    assert.match(panel, /Save deed \/ plat/)
    assert.match(panel, /deedPlatManual/)
    assert.match(fill, /ApplyDeedPlatAi/)
    assert.match(fill, /SurveyManual/)
    assert.match(fill, /legalDescription/)
    assert.match(fill, /Do not extract or return propertyIds/)
    assert.doesNotMatch(review, /<AiField field="propertyIds"/)
  })

  it('Manage Documents exports CSV and Excel and stays usable in the detail pane', () => {
    assert.match(manage, /Export Excel/)
    assert.match(manage, /Export CSV/)
    assert.match(api, /format=\$\{format\}/)
    assert.match(api, /gis-work-items\.xlsx/)
    assert.match(css, /\.deed-plat-panel/)
    assert.match(css, /\.detail-pane/)
    assert.match(css, /grid-template-columns: minmax\(280px, 360px\) 1fr/)
  })
})
