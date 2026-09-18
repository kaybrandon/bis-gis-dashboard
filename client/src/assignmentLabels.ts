/** CR09 — org default technician(s) from Organizations (QC01). Not the work-item assignee. */
export const ASSIGNED_TECHNICIAN_LABEL = 'Assigned technician'
export const ASSIGNED_TECHS_LABEL = 'Assigned tech(s)'
export const PRIMARY_ASSIGNED_TECH_LABEL = 'Primary assigned tech'

export const ASSIGNED_TECHNICIAN_HELP =
  'Organization default technician(s) from Organizations (QC01). New uploads use this as Assigned to (primary, else first). Changing this does not reassign existing documents.'

export const ASSIGNED_TECHS_HELP =
  'Organization default technician(s). Used to auto-assign new uploads to Assigned to. This is not the document Assigned to. Changing this does not reassign existing documents. Editors, Administrators, and Global Administrators only. Notified on priority work.'

export const PRIMARY_ASSIGNED_TECH_HELP =
  'Default Assigned to for new uploads when staff do not pick someone else. Primary Assigned technician if set; otherwise the first Assigned tech. Future uploads only — existing documents keep their Assigned to.'

export const ASSIGNED_TECHNICIAN_UPLOAD_HELP =
  'Organization default technician(s) for this client — not the work-item Assigned to. New uploads copy this into Assigned to (primary, else first). First name and last initial only.'

/** CR09 — work-item assignee used by queues and reports. */
export const ASSIGNED_TO_LABEL = 'Assigned to'
export const ASSIGNED_TO_HELP =
  'Work-item assignee for this document. Personal queues and reports use Assigned to — not the organization’s Assigned technician.'
export const ASSIGNED_TO_UPLOAD_HELP =
  'Work-item assignee for this upload. Leave blank to use the organization’s Assigned technician (primary, else first). This is the queue field, not the org default.'
export const ASSIGNED_TO_FILTER_HELP =
  'Filter by work-item Assigned to. Unassigned is work with no assignee. This is not the organization’s Assigned technician.'
export const UNASSIGNED_LABEL = 'Unassigned'
