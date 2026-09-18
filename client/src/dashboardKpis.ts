import type { DocumentsHrefQuery } from './documentsHref.ts'
import { documentsHref } from './documentsHref.ts'

/** Sentinel for the Dashboard org filter — all orgs, not a stored organization id. */
export const ALL_ORGANIZATIONS = 'all'
export const ALL_ORGANIZATIONS_LABEL = 'All organizations'

export const DASHBOARD_CORE_KPI_KEYS = ['active', 'pending', 'completed', 'priority'] as const

export const DASHBOARD_DEADLINE_KPI_KEYS = ['duethisweek', 'firstdeadline', 'finaldeadline'] as const

export const DASHBOARD_KPI_KEYS = [...DASHBOARD_CORE_KPI_KEYS, ...DASHBOARD_DEADLINE_KPI_KEYS] as const

export type DashboardKpiKey = (typeof DASHBOARD_KPI_KEYS)[number]

export const DASHBOARD_DEADLINE_KPIS: ReadonlyArray<{
  key: (typeof DASHBOARD_DEADLINE_KPI_KEYS)[number]
  label: string
  bucket: 'duethisweek' | 'firstdeadline' | 'finaldeadline'
}> = [
  { key: 'duethisweek', label: 'Due this week', bucket: 'duethisweek' },
  { key: 'firstdeadline', label: 'First deadline', bucket: 'firstdeadline' },
  { key: 'finaldeadline', label: 'Final deadline', bucket: 'finaldeadline' },
]

export type DashboardScopeFilters = {
  organizationId?: string
  statusId?: string
  assignedToUserId?: string
}

export function parseDashboardOrgId(value?: string | null) {
  if (!value || value === ALL_ORGANIZATIONS) return undefined
  return value
}

export function dashboardOrgSelectValue(organizationId?: string) {
  return organizationId ?? ALL_ORGANIZATIONS
}

export function showAllOrganizationsControl(organizationId?: string) {
  return Boolean(organizationId)
}

export function deadlineKpiDocumentsQuery(
  key: (typeof DASHBOARD_DEADLINE_KPI_KEYS)[number],
  filters: DashboardScopeFilters,
): DocumentsHrefQuery {
  const meta = DASHBOARD_DEADLINE_KPIS.find((item) => item.key === key)
  return {
    bucket: meta?.bucket ?? key,
    organizationId: filters.organizationId,
    statusId: filters.statusId,
    assignedToUserId: filters.assignedToUserId,
  }
}

export function deadlineKpiHref(
  key: (typeof DASHBOARD_DEADLINE_KPI_KEYS)[number],
  filters: DashboardScopeFilters,
) {
  return documentsHref(deadlineKpiDocumentsQuery(key, filters))
}
