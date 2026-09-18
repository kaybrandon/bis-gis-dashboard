import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  BIS_MANAGE_DOCUMENTS,
  FORBIDDEN_SELECTION_LIME,
  manageDocumentsRowClassName,
  manageDocumentsTableTheme,
} from './bisManageDocuments.ts'

const here = dirname(fileURLToPath(import.meta.url))
const css = readFileSync(join(here, 'bisManageDocuments.css'), 'utf8')
const app = readFileSync(join(here, '../App.tsx'), 'utf8')
const page = readFileSync(join(here, '../pages/ManageDocumentsPage.tsx'), 'utf8')

describe('QC07 Manage Documents BIS colors', () => {
  it('locks BA / UX hex tokens', () => {
    assert.equal(BIS_MANAGE_DOCUMENTS.navy, '#142642')
    assert.equal(BIS_MANAGE_DOCUMENTS.teal, '#00BA8C')
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGreen, '#E8F8F3')
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGray, '#F3F4F6')
    assert.equal(BIS_MANAGE_DOCUMENTS.select, '#FDE68A')
    assert.equal(BIS_MANAGE_DOCUMENTS.selectBorder, '#F59E0B')
    assert.equal(BIS_MANAGE_DOCUMENTS.hover, '#FEF3C7')
    assert.equal(BIS_MANAGE_DOCUMENTS.textOnNavy, '#FFFFFF')
    assert.equal(BIS_MANAGE_DOCUMENTS.textOnRow, '#111827')
  })

  it('keeps hover lighter than select so selection stays obvious', () => {
    assert.equal(BIS_MANAGE_DOCUMENTS.hover, '#FEF3C7')
    assert.notEqual(BIS_MANAGE_DOCUMENTS.hover, BIS_MANAGE_DOCUMENTS.select)
    assert.equal(manageDocumentsTableTheme.components.Table.rowHoverBg, BIS_MANAGE_DOCUMENTS.hover)
    assert.equal(manageDocumentsTableTheme.components.Table.rowSelectedBg, BIS_MANAGE_DOCUMENTS.select)
  })

  it('never uses neon lime for selection', () => {
    for (const lime of FORBIDDEN_SELECTION_LIME) {
      assert.equal(css.toLowerCase().includes(lime.toLowerCase()), false)
      assert.notEqual(BIS_MANAGE_DOCUMENTS.select.toLowerCase(), lime.toLowerCase())
      assert.notEqual(BIS_MANAGE_DOCUMENTS.hover.toLowerCase(), lime.toLowerCase())
    }
  })

  it('scopes CSS to the Manage Documents grid wrapper', () => {
    assert.match(css, /\.manage-documents-grid\s*\{/)
    assert.match(css, /--bis-navy:\s*#142642/)
    assert.match(css, /--bis-row-green:\s*#E8F8F3/)
    assert.match(css, /--bis-row-gray:\s*#F3F4F6/)
    assert.match(css, /--bis-select:\s*#FDE68A/)
    assert.doesNotMatch(css, /:root\s*\{[\s\S]*--bis-navy/)
    assert.match(page, /manage-documents-grid/)
    assert.match(page, /bisManageDocuments\.css/)
  })

  it('does not retheme the rest of GIS chrome', () => {
    assert.match(app, /colorPrimary: '#1890ff'/)
    assert.doesNotMatch(app, /#142642/)
    assert.doesNotMatch(app, /#FDE68A/)
    assert.doesNotMatch(app, /bisManageDocuments/)
  })

  it('zebra-stripes and marks the selected row', () => {
    assert.equal(manageDocumentsRowClassName(0, false), 'manage-documents-row-a')
    assert.equal(manageDocumentsRowClassName(1, false), 'manage-documents-row-b')
    assert.equal(manageDocumentsRowClassName(0, true), 'manage-documents-row-a manage-documents-row-selected')
  })
})
