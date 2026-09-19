import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const page = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const css = readFileSync(join(here, 'theme/bisManageDocuments.css'), 'utf8')

describe('QC4-02 Manage Documents File Name grid', () => {
  it('keeps File Name sticky with a min width and does not hide it', () => {
    assert.match(page, /title: 'File name'/)
    assert.match(page, /key: 'filename'/)
    assert.match(page, /className: 'manage-documents-filename'/)
    assert.match(page, /fixed: 'left'/)
    assert.match(page, /minWidth: 220/)
    assert.match(page, /width: 280/)
    assert.match(page, /scroll=\{\{ x: 'max-content' \}\}/)
    assert.doesNotMatch(page, /scroll=\{\{ x: 980 \}\}/)
    assert.doesNotMatch(page, /responsive:\s*\[/)
    assert.doesNotMatch(page, /display:\s*['"]none['"]/)
  })

  it('gives other columns min widths so they cannot collapse to zero', () => {
    assert.match(page, /key: 'client'[\s\S]*minWidth: 120/)
    assert.match(page, /key: 'status'[\s\S]*minWidth: 120/)
    assert.match(page, /key: 'assignedto'[\s\S]*minWidth: 140/)
    assert.match(page, /key: 'uploadedAt'[\s\S]*minWidth: 140/)
    assert.match(page, /key: 'workedon'[\s\S]*minWidth: 110/)
    assert.match(page, /key: 'difficulty'[\s\S]*minWidth: 96/)
    assert.match(page, /key: 'reviewed'[\s\S]*minWidth: 72/)
    assert.match(page, /key: 'hours'[\s\S]*minWidth: 88/)
  })

  it('exposes the full file name on hover or focus and allows horizontal scroll', () => {
    assert.match(page, /ellipsis=\{\{ tooltip: name \}\}/)
    assert.match(page, /title=\{name\}/)
    assert.match(css, /overflow-x:\s*auto/)
    assert.match(css, /\.manage-documents-filename[\s\S]*min-width:\s*220px/)
    assert.match(css, /display:\s*table-cell\s*!important/)
    assert.match(page, /className="documents-quick-toolbar"/)
    assert.match(page, /wrap="wrap"/)
  })
})
