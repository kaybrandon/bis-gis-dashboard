import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  ALL_ORGANIZATIONS,
  ALL_ORGANIZATIONS_LABEL,
  DASHBOARD_DEADLINE_KPIS,
  DASHBOARD_KPI_KEYS,
  dashboardOrgSelectValue,
  deadlineKpiHref,
  parseDashboardOrgId,
  showAllOrganizationsControl,
} from './dashboardKpis.ts'
import {
  PENDING_FORBIDDEN_GREEN,
  PENDING_HIGHLIGHT,
  PENDING_HIGHLIGHT_CARD_CLASS,
  PENDING_HIGHLIGHT_ITEM_CLASS,
  PENDING_HIGHLIGHT_ROW_CLASS,
  PENDING_HIGHLIGHT_TEXT,
  isPendingStatus,
} from './theme/pendingHighlight.ts'
import { BIS_MANAGE_DOCUMENTS } from './theme/bisManageDocuments.ts'

const here = dirname(fileURLToPath(import.meta.url))
const dashboard = readFileSync(join(here, 'pages/DashboardPage.tsx'), 'utf8')
const manage = readFileSync(join(here, 'pages/ManageDocumentsPage.tsx'), 'utf8')
const cards = readFileSync(join(here, 'components/WorkItemCards.tsx'), 'utf8')
const css = readFileSync(join(here, 'theme/pendingHighlight.css'), 'utf8')
const indexCss = readFileSync(join(here, 'index.css'), 'utf8')
const charts = readFileSync(join(here, 'components/DashboardCharts.tsx'), 'utf8')
const staffQueue = readFileSync(join(here, 'staffQueue.ts'), 'utf8')
const users = readFileSync(join(here, 'pages/UsersPage.tsx'), 'utf8')
const helpInbox = readFileSync(join(here, 'components/HelpInbox.tsx'), 'utf8')

describe('GIS-UI-04 Pending / All Pending light blue', () => {
  it('uses a readable GIS light blue, not mint or lime', () => {
    assert.equal(PENDING_HIGHLIGHT, '#E6F4FF')
    assert.equal(PENDING_HIGHLIGHT_TEXT, '#111827')
    assert.notEqual(PENDING_HIGHLIGHT.toLowerCase(), BIS_MANAGE_DOCUMENTS.rowGreen.toLowerCase())
    assert.notEqual(PENDING_HIGHLIGHT.toLowerCase(), BIS_MANAGE_DOCUMENTS.select.toLowerCase())
    assert.notEqual(PENDING_HIGHLIGHT.toLowerCase(), BIS_MANAGE_DOCUMENTS.navy.toLowerCase())
    for (const green of PENDING_FORBIDDEN_GREEN) {
      assert.notEqual(PENDING_HIGHLIGHT.toLowerCase(), green.toLowerCase())
      assert.equal(css.toLowerCase().includes(green.toLowerCase()), false)
    }
    assert.match(css, /--pending-highlight:\s*#E6F4FF/)
    assert.match(css, /--pending-highlight-text:\s*#111827/)
  })

  it('applies the highlight on every Pending surface, not one chip', () => {
    assert.equal(isPendingStatus('Pending'), true)
    assert.equal(isPendingStatus('Complete'), false)
    assert.match(manage, /PENDING_HIGHLIGHT_ITEM_CLASS/)
    assert.match(manage, /PENDING_HIGHLIGHT_ROW_CLASS/)
    assert.match(manage, /bucket === 'pending' \? 'bucket-select pending-highlight'/)
    assert.match(manage, /isPendingStatus\(row\.statusName\)/)
    assert.match(dashboard, /PENDING_HIGHLIGHT_CARD_CLASS/)
    assert.match(dashboard, /key === 'pending'/)
    assert.match(cards, /PENDING_HIGHLIGHT_CARD_CLASS/)
    assert.match(cards, /isPendingStatus\(item\.statusName\)/)
    assert.match(css, /\.pending-highlight-item/)
    assert.match(css, /\.pending-highlight-card/)
    assert.match(css, /\.pending-highlight-row/)
    assert.match(css, /\.bucket-select\.pending-highlight/)
  })

  it('keeps yellow select and navy headers distinct from Pending blue', () => {
    assert.equal(BIS_MANAGE_DOCUMENTS.select, '#FDE68A')
    assert.equal(BIS_MANAGE_DOCUMENTS.navy, '#142642')
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGreen, '#E8F8F3')
    assert.match(manage, /manageDocumentsRowClassName/)
  })
})

describe('GIS-UI-05 Dashboard deadline KPI cards', () => {
  it('renders Due this week, First deadline, and Final deadline beside Priority', () => {
    assert.deepEqual([...DASHBOARD_KPI_KEYS], [
      'active',
      'pending',
      'completed',
      'priority',
      'duethisweek',
      'firstdeadline',
      'finaldeadline',
    ])
    assert.deepEqual(DASHBOARD_DEADLINE_KPIS.map((item) => item.label), [
      'Due this week',
      'First deadline',
      'Final deadline',
    ])
    assert.match(dashboard, /DASHBOARD_KPI_KEYS\.map/)
    assert.match(dashboard, /className="dashboard-kpis"/)
    assert.match(indexCss, /\.dashboard-kpis/)
    assert.match(dashboard, /Due this week/)
    assert.match(dashboard, /First deadline/)
    assert.match(dashboard, /Final deadline/)
    assert.match(dashboard, /priority: 'Priority'/)
  })

  it('clicks through to the matching Manage Documents buckets', () => {
    const scope = {
      organizationId: 'org-1',
      statusId: 'status-1',
      assignedToUserId: 'all',
    }
    assert.equal(
      deadlineKpiHref('duethisweek', scope),
      '/documents?bucket=duethisweek&organizationId=org-1&statusId=status-1&assignedToUserId=all',
    )
    assert.equal(
      deadlineKpiHref('firstdeadline', scope),
      '/documents?bucket=firstdeadline&organizationId=org-1&statusId=status-1&assignedToUserId=all',
    )
    assert.equal(
      deadlineKpiHref('finaldeadline', { organizationId: 'org-1' }),
      '/documents?bucket=finaldeadline&organizationId=org-1',
    )
    assert.match(dashboard, /deadlineKpiDocumentsQuery/)
    assert.match(manage, /key: 'duethisweek'/)
    assert.match(manage, /key: 'firstdeadline'/)
    assert.match(manage, /key: 'finaldeadline'/)
  })
})

describe('GIS-UI-06 Dashboard All organizations clear', () => {
  it('clears a single-org filter completely', () => {
    assert.equal(ALL_ORGANIZATIONS, 'all')
    assert.equal(ALL_ORGANIZATIONS_LABEL, 'All organizations')
    assert.equal(parseDashboardOrgId(ALL_ORGANIZATIONS), undefined)
    assert.equal(parseDashboardOrgId(undefined), undefined)
    assert.equal(parseDashboardOrgId('org-1'), 'org-1')
    assert.equal(dashboardOrgSelectValue('org-1'), 'org-1')
    assert.equal(dashboardOrgSelectValue(undefined), ALL_ORGANIZATIONS)
    assert.equal(showAllOrganizationsControl('org-1'), true)
    assert.equal(showAllOrganizationsControl(undefined), false)
    assert.match(dashboard, /ALL_ORGANIZATIONS_LABEL/)
    assert.match(dashboard, /showAllOrganizationsControl\(orgId\)/)
    assert.match(dashboard, /setOrgId\(undefined\)/)
    assert.match(dashboard, /parseDashboardOrgId/)
    assert.match(dashboard, /className="all-organizations-clear"/)
  })
})

describe('GIS-UI-04/05/06 keep landed UI-01/UI-02/UI-03 and stay off later tickets', () => {
  it('keeps UI-01 sider filters, UI-02 panel classes, and UI-03 bar colors', () => {
    assert.match(manage, /className="documents-sider"/)
    assert.match(manage, /className="documents-detail-filters"/)
    assert.match(manage, /className="bucket-sider bis-theme-panel"/)
    assert.match(manage, /placeholder="Group by"/)
    assert.match(dashboard, /kpi-card bis-theme-panel/)
    assert.match(charts, /DASHBOARD_BAR_PALETTE|dashboardCategoryBarPlot|dashboardBarPalette/)
    assert.equal(BIS_MANAGE_DOCUMENTS.rowGreen, '#E8F8F3')
    assert.doesNotMatch(dashboard, /GIS-UI-07|GIS-UI-08|GIS-UI-09/)
  })

  it('preserves CR05 my-queue, CR11 statuses, Need-help, and Users Archive', () => {
    assert.match(staffQueue, /shouldDefaultAssigneeToAll/)
    assert.match(staffQueue, /shouldProbeMyQueue/)
    assert.match(manage, /label: 'On-Hold'/)
    assert.match(manage, /label: 'Complete'/)
    assert.match(helpInbox, /inboxUnreadCount/)
    assert.match(users, /showArchived/)
  })
})
