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
