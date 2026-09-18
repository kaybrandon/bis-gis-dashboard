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

/**
 * CR05 — staff Manage Documents / Dashboard default to the signed-in assignee.
 * Viewer/Uploader never get an assignee scope (QC08).
 * `all` means the staff member cleared the filter.
 */
export function resolveAssigneeFilter(
  raw: string | null | undefined,
  user?: { id?: string; canSeeDashboardAssignee?: boolean; role?: string } | null,
): string | undefined {
  if (!canSeeDashboardAssignee(user)) return undefined
  if (raw === ALL_ASSIGNEES) return undefined
  if (raw === UNASSIGNED) return UNASSIGNED
  if (raw) return raw
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
