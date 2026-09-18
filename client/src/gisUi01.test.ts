import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const css = readFileSync(join(here, 'index.css'), 'utf8')
const themeCss = readFileSync(join(here, 'theme/bisManageDocuments.css'), 'utf8')
const app = readFileSync(join(here, 'App.tsx'), 'utf8')

function section(source: string, startClass: string, endClass: string) {
  const start = source.indexOf(`className="${startClass}"`)
  const end = source.indexOf(`className="${endClass}"`)
  assert.ok(start >= 0, `Fail if: ${startClass} is missing.`)
  assert.ok(end > start, `Fail if: ${endClass} does not follow ${startClass}.`)
  return source.slice(start, end)
}

describe('GIS-UI-01 Manage Documents filter layout', () => {
  it('stacks the seven detail filters below the left nav, not above the grid', () => {
    const sider = section(manage, 'documents-sider', 'documents-main')
    assert.match(sider, /className="bucket-sider bis-theme-panel"/)
    assert.match(sider, /className="documents-detail-filters"/)
    assert.match(sider, /\{filterSelects\}/)
    assert.ok(
      sider.indexOf('className="bucket-sider"') < sider.indexOf('className="documents-detail-filters"'),
      'Fail if: detail filters are not below the left nav box.',
    )

    const filtersStart = manage.indexOf('const filterSelects =')
    const filtersEnd = manage.indexOf('return (', filtersStart)
    assert.ok(filtersStart >= 0 && filtersEnd > filtersStart, 'Fail if: filterSelects is missing.')
    const filters = manage.slice(filtersStart, filtersEnd)
    assert.match(filters, /placeholder="Status"/)
    assert.match(filters, /placeholder="All Assigned to"/)
    assert.match(filters, /placeholder="Client"/)
    assert.match(filters, /placeholder="Document type"/)
    assert.match(filters, /Uploaded from/)
    assert.match(filters, /Worked from/)
    assert.match(filters, /placeholder="Group by"/)

    const statusAt = filters.indexOf('placeholder="Status"')
    const assignedAt = filters.indexOf('placeholder="All Assigned to"')
    const clientAt = filters.indexOf('placeholder="Client"')
    const typeAt = filters.indexOf('placeholder="Document type"')
    const uploadedAt = filters.indexOf('Uploaded from')
    const workedAt = filters.indexOf('Worked from')
    const groupAt = filters.indexOf('placeholder="Group by"')
    assert.ok(statusAt < assignedAt && assignedAt < clientAt && clientAt < typeAt)
    assert.ok(typeAt < uploadedAt && uploadedAt < workedAt && workedAt < groupAt)

    const toolbar = section(manage, 'documents-quick-toolbar', 'manage-documents-grid')
    assert.doesNotMatch(toolbar, /placeholder="Status"/)
    assert.doesNotMatch(toolbar, /placeholder="Client"/)
    assert.doesNotMatch(toolbar, /placeholder="Document type"/)
    assert.doesNotMatch(toolbar, /Uploaded from/)
    assert.doesNotMatch(toolbar, /Worked from/)
    assert.doesNotMatch(toolbar, /placeholder="Group by"/)
    assert.doesNotMatch(toolbar, /filterSelects/)
    assert.doesNotMatch(manage, /className="filter-toolbar"/)
  })

  it('keeps the compact quick toolbar and right-aligns search at desktop', () => {
    const toolbar = section(manage, 'documents-quick-toolbar', 'manage-documents-grid')
    assert.match(toolbar, />My queue</)
    assert.match(toolbar, />\s*Assigned to me\s*</)
    assert.match(toolbar, />\s*Priority\s*</)
    assert.match(toolbar, />\s*Due this week\s*</)
    assert.match(toolbar, />Clear</)
    assert.match(toolbar, /documents-search/)
    assert.match(toolbar, /Search file, client, or assignee/)

    assert.match(css, /\.documents-quick-toolbar\s+\.documents-search\s*\{[^}]*margin-left:\s*auto/)
    assert.match(manage, /Export to Excel/)
  })

  it('keeps combined filtering, date ranges, grouping, search, and Clear wired', () => {
    assert.match(manage, /statusId,/)
    assert.match(manage, /assignedToUserId: showAssignee \? assignedTo : undefined/)
    assert.match(manage, /organizationId: orgId/)
    assert.match(manage, /documentTypeId: docTypeId/)
    assert.match(manage, /uploadedFrom: uploaded/)
    assert.match(manage, /workedFrom: worked/)
    assert.match(manage, /groupBy,/)
    assert.match(manage, /search: search \|\| undefined/)
    assert.match(manage, /setStatusId\(v\)/)
    assert.match(manage, /setAssignedTo\(v\)/)
    assert.match(manage, /setOrgId\(v\)/)
    assert.match(manage, /setDocTypeId\(v\)/)
    assert.match(manage, /setUploaded\(v\)/)
    assert.match(manage, /setWorked\(v\)/)
    assert.match(manage, /setGroupBy\(v\)/)
    assert.match(manage, /setSearch\(value\)/)
    assert.match(manage, /onClick=\{\(\) => applyPreset\(''\)\}/)
    assert.match(manage, /shouldProbeMyQueue/)
    assert.match(manage, /resolveAssigneeFilter/)
  })

  it('keeps left nav + grid from overlapping on resize and does not invent annotation theme', () => {
    assert.match(css, /\.documents-sider\s*\{[^}]*min-width:\s*0/)
    assert.match(css, /\.documents-main\s*\{[^}]*min-width:\s*0/)
    assert.match(css, /\.documents-detail-filters[\s\S]*?\.ant-picker\s*\{[^}]*max-width:\s*100%/)
    assert.doesNotMatch(css, /\.documents-detail-filters[^{]*\{[^}]*border:\s*[1-9].*solid\s*#000/)
    assert.doesNotMatch(css, /\.documents-detail-filters[\s\S]{0,200}--bis-navy/)
    assert.doesNotMatch(themeCss, /\.documents-detail-filters/)
    assert.match(app, /colorPrimary: '#1890ff'/)
    assert.doesNotMatch(app, /#142642/)
  })
})
