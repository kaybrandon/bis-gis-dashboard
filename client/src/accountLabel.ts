import type { AuthUser } from './api'

export function orgScopeLabel(user: AuthUser | null | undefined) {
  if (user?.canSeeAllOrganizations) return 'All organizations'
  if (user?.organizations.length === 1) return user.organizations[0].name
  if (user?.organizations.length) return user.organizations.map((org) => org.name).join(', ')
  return 'All assigned organizations'
}

/** Header trigger: Full name if set, else Username, else email. Skip role-as-name. */
export function accountTriggerLabel(user: AuthUser | null | undefined) {
  if (!user) return 'Account'
  const full = user.fullName?.trim()
  if (full) return full
  const name = (user.userName ?? user.displayName)?.trim()
  const roleLabels = [user.roleDisplayName, user.role].filter(Boolean)
  const nameIsRole = !!name && roleLabels.some((role) => role.toLowerCase() === name.toLowerCase())
  if (name && !nameIsRole) return name
  return user.email || name || 'Account'
}
