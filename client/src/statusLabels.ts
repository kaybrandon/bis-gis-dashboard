export const CANONICAL_STATUS_LABELS = [
  'Active',
  'Pending',
  'Complete',
  'On-Hold',
  'Cancelled',
  'Needs Review',
] as const

export type CanonicalStatusLabel = (typeof CANONICAL_STATUS_LABELS)[number]

export function statusLabel(name: string | null | undefined): string {
  switch (name) {
    case 'In Progress':
      return 'Active'
    case 'Held':
    case 'On Hold':
      return 'On-Hold'
    case 'Worked':
      return 'Complete'
    default:
      return name || '—'
  }
}

export function isCanonicalStatus(name: string | null | undefined): boolean {
  return (CANONICAL_STATUS_LABELS as readonly string[]).includes(statusLabel(name))
}

export function chartStatusCounts<T extends { name: string; count: number }>(rows: T[]): T[] {
  return rows.filter((row) => row.count > 0 && isCanonicalStatus(row.name))
}

export function reviewLabel(reviewed: boolean | undefined): string {
  return reviewed ? 'Yes' : 'No'
}
