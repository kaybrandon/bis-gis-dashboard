export function statusLabel(name: string | null | undefined): string {
  switch (name) {
    case 'In Progress':
      return 'Active'
    case 'Held':
      return 'On-Hold'
    case 'Worked':
      return 'Complete'
    default:
      return name || '—'
  }
}

export function reviewLabel(reviewed: boolean | undefined): string {
  return reviewed ? 'Yes' : 'No'
}
