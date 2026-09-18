import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { BIS_MANAGE_DOCUMENTS } from './bisManageDocuments.ts'

const here = dirname(fileURLToPath(import.meta.url))
const css = readFileSync(join(here, 'bisManageDocuments.css'), 'utf8')
const main = readFileSync(join(here, '../main.tsx'), 'utf8')
const shell = readFileSync(join(here, '../layout/AppShell.tsx'), 'utf8')
const dashboard = readFileSync(join(here, '../pages/DashboardPage.tsx'), 'utf8')
const charts = readFileSync(join(here, '../components/DashboardCharts.tsx'), 'utf8')
const pagesDir = join(here, '../pages')
const pageFiles = readdirSync(pagesDir).filter((name) => name.endsWith('Page.tsx'))

const themedPages = [
  'DashboardPage.tsx',
  'ManageDocumentsPage.tsx',
  'ViewDocumentPage.tsx',
  'UploadDocumentsPage.tsx',
  'ReportsPage.tsx',
  'ReportDetailPage.tsx',
  'TimeReportPage.tsx',
  'ProfilePage.tsx',
  'SettingsPage.tsx',
  'StatusPage.tsx',
  'UsersPage.tsx',
  'OrganizationsPage.tsx',
  'ConnectionsPage.tsx',
  'LoginPage.tsx',
  'ForgotPasswordPage.tsx',
  'ResetPasswordPage.tsx',
  'PublicUploadPage.tsx',
] as const

describe('GIS-UI-02 app-wide Manage Documents theme', () => {
  it('loads MD theme CSS globally so every page wrapper can use it', () => {
    assert.match(main, /theme\/bisManageDocuments\.css/)
    assert.match(shell, /className="content-wrap"/)
    assert.match(css, /\.content-wrap \.ant-card/)
    assert.match(css, /\.login-wrap \.ant-card/)
    assert.match(css, /\.content-wrap \.ant-table-thead/)
  })

  it('reuses QC07 tokens for headers, zebra rows, and borders — no new palette', () => {
    assert.match(css, new RegExp(`background:\\s*var\\(--bis-navy\\)`))
    assert.match(css, new RegExp(`background:\\s*var\\(--bis-row-green\\)`))
    assert.match(css, new RegExp(`background:\\s*var\\(--bis-row-gray\\)`))
    assert.match(css, /--bis-navy:\s*#142642/)
    assert.match(css, /--bis-teal:\s*#00BA8C/)
    assert.equal(css.includes('#AED20F'), false)
    assert.equal(BIS_MANAGE_DOCUMENTS.navy, '#142642')
  })

  it('gives card headers and grid headers navy + white text, not flat gray', () => {
    assert.match(css, /\.content-wrap \.ant-card > \.ant-card-head[\s\S]*background:\s*var\(--bis-navy\)/)
    assert.match(css, /\.content-wrap \.ant-table-thead > tr > th[\s\S]*background:\s*var\(--bis-navy\)/)
    assert.match(css, /\.content-wrap \.ant-card-head-title[\s\S]*color:\s*var\(--bis-text-on-navy\)/)
    assert.match(css, /--bis-text-on-navy:\s*#ffffff/)
    assert.match(css, /--bis-text-on-row:\s*#111827/)
    assert.match(css, /border-top:\s*8px solid var\(--bis-navy\)/)
    assert.match(css, /\.content-wrap \.conn-card[\s\S]*border-top:\s*8px solid var\(--bis-navy\)/)
  })

  it('themes Dashboard chart panels and Recently completed (Figure 2) and keeps UI-03 bar colors', () => {
    assert.match(dashboard, /Recently completed/)
    assert.match(dashboard, /<Table/)
    assert.match(dashboard, /className="kpi-card bis-theme-panel"/)
    assert.match(charts, /className="chart-card bis-theme-panel"/)
    assert.match(charts, /dashboardCategoryBarPlot/)
    assert.match(charts, /from '\.\.\/dashboardBarPalette'/)
    assert.doesNotMatch(charts, /color="#2f54eb"/)
    assert.doesNotMatch(charts, /color="#fa8c16"/)
    assert.doesNotMatch(charts, /color="#13c2c2"/)
    assert.match(css, /\.content-wrap \.kpi-card \.ant-statistic-title/)
  })

  it('covers every application page that hosts boxes, panels, or grids', () => {
    for (const name of themedPages) {
      assert.ok(pageFiles.includes(name), `missing page ${name}`)
      const source = readFileSync(join(pagesDir, name), 'utf8')
      const usesBox = /<Card|<Table|className="(login-wrap|conn-card|maintenance-report|form-panel|compact-card)/.test(source)
      assert.equal(usesBox, true, `${name} should render a card, table, or panel`)
      assert.match(
        source,
        /bis-theme-panel|bis-theme-grid|manage-documents-grid/,
        `${name} should opt into the shared MD panel/grid classes`,
      )
    }
    const extras = pageFiles.filter((name) => !(themedPages as readonly string[]).includes(name))
    assert.deepEqual(extras, [], `new page(s) need GIS-UI-02 theme coverage: ${extras.join(', ')}`)
  })

  it('does not restyle filter-toolbar layout (GIS-UI-01) or overwrite UI-03 bar colors', () => {
    const index = readFileSync(join(here, '../index.css'), 'utf8')
    assert.match(index, /\.filter-toolbar \{/)
    assert.doesNotMatch(css, /\.filter-toolbar \{/)
    assert.match(charts, /\{\.\.\.dashboardCategoryBarPlot\(/)
    assert.doesNotMatch(css, /DASHBOARD_BAR_PALETTE/)
  })
})
