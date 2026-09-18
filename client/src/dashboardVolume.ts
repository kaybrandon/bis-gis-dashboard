import { UNASSIGNED_LABEL } from './assignmentLabels.ts'
import type { DocumentsHrefQuery } from './documentsHref.ts'
import { UNASSIGNED } from './staffQueue.ts'

/** API NamedCount id for documents with no Assigned to (CR10). */
export const UNASSIGNED_VOLUME_ID = '00000000-0000-0000-0000-000000000000'

export const DOCUMENTS_BY_CAD_TITLE = 'Documents by CAD'
export const DOCUMENTS_BY_TECHNICIAN_TITLE = 'Documents by technician'

export const DOCUMENTS_BY_CAD_HELP =
  'v1 CAD is the Organization (client) name. Counts are documents created or uploaded in the selected date range. Click a bar to open Manage Documents for that client.'

export const DOCUMENTS_BY_TECHNICIAN_HELP =
  `Technician is the document Assigned to, not the organization’s Assigned technician. ${UNASSIGNED_LABEL} is work with no assignee. Counts are documents created or uploaded in the selected date range. Click a bar to open that queue.`

export function isUnassignedVolumeId(id?: string | null) {
  return !id || id === UNASSIGNED_VOLUME_ID
}

export function technicianVolumeFilterId(id?: string | null): string {
  return isUnassignedVolumeId(id) ? UNASSIGNED : id ?? UNASSIGNED
}

export type VolumeFilters = {
  organizationId?: string
  statusId?: string
  assignedToUserId?: string
  from?: string
  to?: string
}

/** Same dashboard filters → Manage Documents uploaded/created range (CR10). */
export function volumeUploadedQuery(
  filters: VolumeFilters,
  extra: Partial<DocumentsHrefQuery> = {},
): DocumentsHrefQuery {
  return {
    bucket: 'all',
    organizationId: filters.organizationId,
    statusId: filters.statusId,
    assignedToUserId: filters.assignedToUserId,
    uploadedFrom: filters.from,
    uploadedTo: filters.to,
    ...extra,
  }
}

export function documentsByCadQuery(filters: VolumeFilters, cadId: string): DocumentsHrefQuery {
  return volumeUploadedQuery(filters, { organizationId: cadId })
}

export function documentsByTechnicianQuery(filters: VolumeFilters, technicianId: string): DocumentsHrefQuery {
  return volumeUploadedQuery(filters, { assignedToUserId: technicianVolumeFilterId(technicianId) })
}
