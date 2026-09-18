import type { DocumentsHrefQuery } from './documentsHref.ts'
import { canSeeDashboardAssignee } from './roles.ts'

/** URL sentinel: staff explicitly chose all assignees (not the CR05 default). */
export const ALL_ASSIGNEES = 'all'

/** URL/API sentinel: work items with no Assigned to (CR08). Not org Assigned technician. */
export const UNASSIGNED = 'unassigned'

export const QUEUE_SORT_BY = 'queue'
export const QUEUE_SORT_DIR = 'asc'

export function isStaffQueueRole(user?: { canSeeDashboardAssignee?: boolean; role?: string } | null) {
  return canSeeDashboardAssignee(user)
}

export function isGlobalAdministratorRole(user?: { role?: string } | null) {
  return user?.role === 'GlobalAdministrator'
}

/**
 * CR05 hotfix — empty my-queue default.
 * Global Admin, or staff whose personal queue is 0, land on Assignee = All.
 * Editors/staff with assigned work keep Assigned to me.
 */
export function shouldDefaultAssigneeToAll(
  user?: { canSeeDashboardAssignee?: boolean; role?: string } | null,
  myQueueCount?: number,
) {
  if (!canSeeDashboardAssignee(user)) return false
  return isGlobalAdministratorRole(user) || myQueueCount === 0
}

/** Probe my-queue only when staff would otherwise default to Assigned to me. */
export function shouldProbeMyQueue(
  raw: string | null | undefined,
  user?: { id?: string; canSeeDashboardAssignee?: boolean; role?: string } | null,
) {
  if (!canSeeDashboardAssignee(user) || !user?.id) return false
  if (raw) return false
  if (isGlobalAdministratorRole(user)) return false
  return true
}

/**
 * CR05 — staff Manage Documents / Dashboard default to the signed-in assignee
 * when they have assigned work. Global Admin and a 0 my-queue default to All.
 * Viewer/Uploader never get an assignee scope (QC08).
 * `all` means the staff member chose all assignees.
 */
export function resolveAssigneeFilter(
  raw: string | null | undefined,
  user?: { id?: string; canSeeDashboardAssignee?: boolean; role?: string } | null,
  myQueueCount?: number,
): string | undefined {
  if (!canSeeDashboardAssignee(user)) return undefined
  if (raw === ALL_ASSIGNEES) return undefined
  if (raw === UNASSIGNED) return UNASSIGNED
  if (raw) return raw
  if (shouldDefaultAssigneeToAll(user, myQueueCount)) return undefined
  return user?.id
}

export function isUnassignedFilter(value?: string | null) {
  return value === UNASSIGNED
}

/** Translate the Assigned to filter for API query strings (Guid or unassignedOnly). */
export function assignmentApiParams(assignedTo?: string): {
  assignedToUserId?: string
  unassignedOnly?: boolean
} {
  if (assignedTo === UNASSIGNED) return { unassignedOnly: true }
  if (assignedTo) return { assignedToUserId: assignedTo }
  return {}
}

/** When leaving Dashboard, staff must pass `all` if Assignee was cleared. */
export function assigneeHrefValue(
  assignedTo: string | undefined,
  user?: { canSeeDashboardAssignee?: boolean; role?: string } | null,
): string | undefined {
  if (!canSeeDashboardAssignee(user)) return undefined
  if (assignedTo === UNASSIGNED) return UNASSIGNED
  return assignedTo ?? ALL_ASSIGNEES
}

/** Saved Assigned-to-me must not trap GA / zero-queue on an empty mine bucket. */
export function queuePresetForAssigneeDefault(
  savedPreset: string,
  rawAssignee: string | null | undefined,
  assignedTo: string | undefined,
  user?: { canSeeDashboardAssignee?: boolean; role?: string } | null,
) {
  const defaultedToAll = !rawAssignee && assignedTo === undefined && canSeeDashboardAssignee(user)
  return defaultedToAll && savedPreset === 'mine' ? '' : savedPreset
}

export function defaultDocumentsBucket(
  urlBucket: string | null | undefined,
  savedPreset: string,
  user?: { canSeeDashboardAssignee?: boolean; role?: string } | null,
  hasOtherFilter?: boolean,
): string {
  if (urlBucket) return urlBucket
  if (savedPreset) return savedPreset
  if (canSeeDashboardAssignee(user)) return 'all'
  return hasOtherFilter ? 'all' : 'pending'
}

/** CR03 + CR05 — Active tile destination: same org/assignee, Active status, no date window. */
export function activeDocumentsQuery(args: {
  organizationId?: string
  assignedToUserId?: string
  activeStatusId?: string
  user?: { id?: string; canSeeDashboardAssignee?: boolean; role?: string } | null
}): DocumentsHrefQuery {
  return {
    bucket: 'all',
    organizationId: args.organizationId,
    statusId: args.activeStatusId,
    assignedToUserId: assigneeHrefValue(args.assignedToUserId, args.user),
  }
}
