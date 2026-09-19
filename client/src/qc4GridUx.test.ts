import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { BIS_MANAGE_DOCUMENTS, manageDocumentsRowClassName } from './theme/bisManageDocuments.ts'
import { MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH } from './manageDocumentsColumnPrefs.ts'

const here = dirname(fileURLToPath(import.meta.url))
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const control = readFileSync(join(here, 'manageDocumentsColumnsControl.tsx'), 'utf8')
const reorder = readFileSync(join(here, 'manageDocumentsColumnReorder.tsx'), 'utf8')
const prefs = readFileSync(join(here, 'manageDocumentsColumnPrefs.ts'), 'utf8')
const themeCss = readFileSync(join(here, 'theme/bisManageDocuments.css'), 'utf8')
const pendingCss = readFileSync(join(here, 'theme/pendingHighlight.css'), 'utf8')

describe('QC4-03 Allow users to reorder Manage Documents columns', () => {
  it('offers drag handles plus an accessible Columns / Reset columns alternative', () => {
    assert.match(manage, /ManageDocumentsColumnsControl/)
    assert.match(manage, /persistColumnOrder/)
    assert.match(manage, /restoreDefaultColumns/)
    assert.match(manage, /decorateManageDocumentsColumns/)
    assert.match(manage, /orderColumns\(/)
    assert.match(reorder, /Drag to reorder/)
    assert.match(reorder, /draggable/)
    assert.match(control, /Reset columns/)
    assert.match(control, /Move \$\{MANAGE_DOCUMENTS_COLUMN_LABELS\[key\]\} earlier/)
    assert.match(control, /role="dialog"/)
    assert.match(control, /aria-haspopup="dialog"/)
  })

  it('keeps sort, inline edits, and row navigation wired after reorder decoration', () => {
    assert.match(manage, /sorter: true/)
    assert.match(manage, /setSortBy\(String\(s\.columnKey\)\)/)
    assert.match(manage, /statusSelectOptions\(actions/)
    assert.match(manage, /void patchRow\(row, \{ statusId: value \}/)
    assert.match(manage, /void patchRow\(row, \{\s*assignedToUserId:/)
    assert.match(manage, /navigate\(`\/documents\/\$\{row\.id\}`\)/)
    assert.match(manage, /rowKey="id"/)
  })

  it('persists order per user and Reset only removes that user key', () => {
    assert.match(prefs, /gis\.manageDocumentsColumns/)
    assert.match(prefs, /\$\{MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX\}\.\$\{userId\}/)
    assert.match(prefs, /function resetColumnOrder/)
    assert.match(prefs, /store\?\.removeItem\(manageDocumentsColumnPrefsKey\(userId\)\)/)
    assert.match(prefs, /if \(!userId\) return/)
    assert.match(manage, /writeColumnOrder\(user\?\.id, next\)/)
    assert.match(manage, /resetColumnOrder\(user\?\.id\)/)
  })

  it('does not let reorder drop QC4-02 File Name sticky / min-width / scroll', () => {
    assert.equal(MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH, 220)
    assert.match(prefs, /MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH = 220/)
    assert.match(manage, /className: 'manage-documents-filename'/)
    assert.match(manage, /fixed: 'left'/)
    assert.match(manage, /minWidth: 220/)
    assert.match(manage, /width: 280/)
    assert.match(manage, /scroll=\{\{ x: 'max-content' \}\}/)
    assert.match(manage, /ellipsis=\{\{ tooltip: name \}\}/)
    assert.match(manage, /title=\{name\}/)
    assert.match(reorder, /manage-documents-filename/)
    assert.match(reorder, /MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH/)
    assert.doesNotMatch(reorder, /responsive:\s*\[/)
    assert.match(themeCss, /\.manage-documents-filename[\s\S]*min-width:\s*220px/)
    assert.match(themeCss, /display:\s*table-cell\s*!important/)
    assert.match(themeCss, /overflow-x:\s*auto/)
    assert.doesNotMatch(control, /responsive:\s*\[/)
    assert.doesNotMatch(manage, /key: 'filename'[\s\S]{0,200}responsive:/)
    assert.doesNotMatch(manage, /scroll=\{\{ x: 980 \}\}/)
  })
})

describe('QC4-04 Restore alternating green row shading', () => {
  it('uses Time Report mint/gray tokens, not flat white-only rows', () => {
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGreen, '#E8F8F3')
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGray, '#F3F4F6')
    assert.match(themeCss, /tr\.manage-documents-row-a > td[\s\S]*background:\s*var\(--bis-row-green\)\s*!important/)
    assert.match(themeCss, /tr\.manage-documents-row-b > td[\s\S]*background:\s*var\(--bis-row-gray\)\s*!important/)
    assert.match(themeCss, /\.bis-theme-grid \.ant-table-tbody > tr:nth-child\(odd\) > td/)
    assert.equal(manageDocumentsRowClassName(0, false), 'manage-documents-row-a')
    assert.equal(manageDocumentsRowClassName(1, false), 'manage-documents-row-b')
    assert.match(manage, /manageDocumentsRowClassName\(index, selectedId === row\.id\)/)
    assert.doesNotMatch(themeCss, /manage-documents-row-a[^{]*\{[^}]*background:\s*#fff/)
  })

  it('keeps hover, select, and pending distinct and readable', () => {
    assert.match(themeCss, /tr:hover > td[\s\S]*background:\s*var\(--bis-hover\)\s*!important/)
    assert.match(themeCss, /manage-documents-row-selected > td[\s\S]*background:\s*var\(--bis-select\)\s*!important/)
    assert.match(themeCss, /--bis-text-on-row:\s*#111827/)
    assert.match(pendingCss, /pending-highlight-row/)
    assert.match(pendingCss, /--pending-highlight:\s*#E6F4FF/)
    assert.match(manage, /PENDING_HIGHLIGHT_ROW_CLASS/)
  })
})
