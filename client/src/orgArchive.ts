export type ArchiveableOrg = {
  id: string
  name: string
  isArchived?: boolean
}

export function orgDisplayName(name: string, isArchived?: boolean) {
  const value = name.trim()
  if (!isArchived || value.length === 0) return value
  return value.toLowerCase().endsWith('(archived)') ? value : `${value} (archived)`
}

export function orgPickerOption(org: ArchiveableOrg) {
  return {
    value: org.id,
    label: orgDisplayName(org.name, org.isArchived),
    className: org.isArchived ? 'org-option-archived' : undefined,
  }
}
