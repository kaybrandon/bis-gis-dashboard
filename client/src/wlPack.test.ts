import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { CANONICAL_STATUS_LABELS, reviewLabel, statusLabel } from './statusLabels.ts'
import { statusSelectOptions } from './statusSelectOptions.ts'
import { resolveAssigneeFilter } from './staffQueue.ts'
import { viewerKind } from './documentPreview.ts'

const here = dirname(fileURLToPath(import.meta.url))
const app = readFileSync(join(here, 'App.tsx'), 'utf8')
const shell = readFileSync(join(here, 'layout/AppShell.tsx'), 'utf8')
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const upload = readFileSync(join(here, 'pages/UploadDocumentsPage.tsx'), 'utf8')
const detail = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')
const dashboard = readFileSync(join(here, 'pages/DashboardPage.tsx'), 'utf8')
const charts = readFileSync(join(here, 'components/DashboardCharts.tsx'), 'utf8')

describe('WL pack UI Musts', () => {
  it('WL03/WL06 — Upload Documents is its own left-nav page and + Upload goes there', () => {
    assert.match(app, /path="\/upload-documents"/)
    assert.match(app, /user\.canUpload \? <UploadDocumentsPage/)
    assert.match(shell, /to="\/upload-documents">Upload Documents/)
    assert.match(shell, /user\?\.canUpload/)
    assert.match(manage, /navigate\('\/upload-documents'\)/)
    assert.match(manage, />\s*Upload\s*</)
    assert.match(upload, /multiple/)
    assert.doesNotMatch(manage, /<Upload\.Dragger/)
  })

  it('WL04/WL05 — staff pick client, type, and priority; clients do not', () => {
    assert.match(upload, /const isStaff = !!user\?\.canMutateWorkItems/)
    assert.match(upload, /showOrgSelector = isStaff \|\| \(user\?\.organizations.length \?\? 0\) > 1/)
    assert.match(upload, /locked = !isStaff && user\?\.organizations.length === 1/)
    assert.match(upload, /\{isStaff && \(/)
    assert.match(upload, /label="Document type"/)
    assert.match(upload, />Priority</)
    assert.match(upload, /if \(isStaff && nextTypeId\)/)
    assert.match(upload, /if \(isStaff && values.isPriority\)/)
    assert.match(upload, /body.append\('organizationId', nextOrgId\)/)
  })

  it('WL07 — selectors, filters, tiles, charts use the six statuses; Needs Review ≠ Reviewed', () => {
    // WL07 close — CR11 already ships this. Fail if: form ≠ charts with no mapping · Needs Review dropped · silent remap.
    const canonical = ['Active', 'Pending', 'Complete', 'On-Hold', 'Cancelled', 'Needs Review']
    const actions = {
      pendingId: 'pending',
      activeId: 'active',
      onHoldId: 'hold',
      completeId: 'complete',
      cancelledId: 'cancelled',
      needsReviewId: 'needs-review',
      completedIds: ['complete', 'qcd'],
    }
    assert.deepEqual([...CANONICAL_STATUS_LABELS], canonical)
    assert.deepEqual(statusSelectOptions(actions).map((option) => option.label), canonical)
    assert.equal(statusLabel('In Progress'), 'Active', 'Fail if: form ≠ charts with no mapping.')
    assert.equal(statusLabel('Held'), 'On-Hold')
    assert.equal(statusLabel('Worked'), 'Complete')
    assert.equal(statusLabel('Needs Review'), 'Needs Review', 'Fail if: Needs Review dropped.')
    assert.notEqual(statusLabel('Needs Review'), reviewLabel(true))
    assert.notEqual(statusLabel('Needs Review'), 'Reviewed')
    assert.equal(statusLabel("QC'd"), "QC'd", 'Fail if: silent remap of QC\'d.')

    assert.match(detail, /statusSelectOptions\(actions/)
    assert.match(manage, /statusSelectOptions\(actions/)
    assert.match(manage, /options=\{statuses\.map\(\(s\) => \(\{ value: s\.id, label: statusLabel\(s\.name\) \}\)\)\}/)
    assert.match(dashboard, /options=\{statuses\.map\(\(s\) => \(\{ value: s\.id, label: statusLabel\(s\.name\) \}\)\)\}/)
    assert.match(manage, /label: 'On-Hold'/)
    assert.match(manage, /label: 'Complete'/)
    assert.match(dashboard, /active: 'Active'/)
    assert.match(dashboard, /pending: 'Pending'/)
    assert.match(charts, /chartStatusCounts/)
    assert.match(charts, /statusLabel\(x\.name\)/)
    assert.match(manage, /title: 'Review'/)
    assert.match(manage, /reviewLabel\(value\)/)
  })

  it('WL08 — document type options keep API order Deed…Other', () => {
    assert.match(upload, /types.map\(\(t\) => \(\{ value: t.id, label: t.name \}\)\)/)
    assert.match(manage, /docTypes.map\(\(t\) => \(\{ value: t.id, label: t.name \}\)\)/)
    assert.equal(upload.includes('Subdivision'), false, 'Fail if: Upload hardcodes a type order instead of API order.')
  })

  it('WL10 — Reviewed checkbox sits next to Priority on the work item', () => {
    const flags = detail.match(/className="detail-flags"[\s\S]+?<\/Space>/)
    assert.ok(flags, 'Fail if: work-item flags row is missing.')
    const priorityAt = flags[0].search(/>\s*Priority\s*</)
    const reviewedAt = flags[0].search(/>\s*Reviewed\s*</)
    assert.ok(priorityAt >= 0, 'Fail if: Priority is not on the work-item flags row.')
    assert.ok(reviewedAt >= 0, 'Fail if: Reviewed is not on the work-item flags row.')
    assert.ok(reviewedAt > priorityAt, 'Fail if: Reviewed is not next to / after Priority.')
    assert.ok(reviewedAt - priorityAt < 500, 'Fail if: Reviewed is not next to Priority.')
  })

  it('WL11 — Manage Documents shows Review Yes/No', () => {
    assert.match(manage, /title: 'Review'/)
    assert.match(manage, /dataIndex: 'isReviewed'/)
    assert.match(manage, /reviewLabel\(value\)/)
    assert.match(manage, /showReview/)
    assert.equal(reviewLabel(true), 'Yes')
    assert.equal(reviewLabel(false), 'No')
  })

  it('WL12 — TIFF uses first-page preview; JPEG stays an image', () => {
    assert.equal(viewerKind('image/tiff', 'scan.tif'), 'tiff')
    assert.equal(viewerKind('image/jpeg', 'photo.jpg'), 'image')
    assert.equal(viewerKind('image/png', 'scan.png'), 'image')
  })

  it('WL13 — staff Manage Documents defaults to the signed-in Assigned to when they have work', () => {
    const editor = { id: 'editor-1', role: 'Editor' as const, canSeeDashboardAssignee: true }
    assert.equal(resolveAssigneeFilter(undefined, editor), 'editor-1')
    assert.equal(resolveAssigneeFilter(undefined, editor, 3), 'editor-1')
    assert.match(manage, /resolveAssigneeFilter\(/)
    assert.match(manage, /shouldProbeMyQueue/)
    assert.match(manage, /if \(!queueScopeReady\) return/)
  })
})
