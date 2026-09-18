export const ROLE_LABELS: Record<string, string> = {
  GlobalAdministrator: 'Global Administrator',
  Administrator: 'Administrator',
  Editor: 'Editor',
  Uploader: 'Uploader',
  Viewer: 'Viewer',
}

export function roleLabel(role: string) {
  return ROLE_LABELS[role] ?? role
}

/** QC03 — Editor/Administrator (and Global Administrator) do not pick organizations. */
export function roleRequiresOrganizationAssignment(role?: string | null) {
  return role === 'Viewer' || role === 'Uploader'
}

export function roleHasAllOrganizations(role?: string | null) {
  return role === 'GlobalAdministrator' || role === 'Administrator' || role === 'Editor'
}
