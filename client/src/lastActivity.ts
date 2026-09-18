/** Last activity uses America/Chicago for the exact tooltip (GIS Users AC). */
export const LAST_ACTIVITY_TIME_ZONE = 'America/Chicago'

export type LastActivityDisplay = {
  label: string
  tooltip?: string
}

export function formatLastActivity(
  lastLoginAt: string | null | undefined,
  now: Date = new Date(),
): LastActivityDisplay {
  if (lastLoginAt == null || lastLoginAt === '') {
    return { label: 'Never' }
  }

  const at = new Date(lastLoginAt)
  if (Number.isNaN(at.getTime())) {
    return { label: 'Never' }
  }

  return {
    label: formatRelativeLastActivity(at, now),
    tooltip: formatExactCentralTime(at),
  }
}

export function formatExactCentralTime(at: Date, timeZone = LAST_ACTIVITY_TIME_ZONE): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(at)
}

export function formatRelativeLastActivity(at: Date, now: Date = new Date()): string {
  const seconds = Math.round((now.getTime() - at.getTime()) / 1000)
  if (seconds < 45) {
    return 'Just now'
  }

  if (seconds < 90) {
    return '1 minute ago'
  }

  const minutes = Math.round(seconds / 60)
  if (minutes < 45) {
    return `${minutes} minutes ago`
  }

  if (minutes < 90) {
    return '1 hour ago'
  }

  const hours = Math.round(minutes / 60)
  if (hours < 36) {
    return `${hours} hours ago`
  }

  const days = Math.round(hours / 24)
  if (days < 14) {
    return days === 1 ? '1 day ago' : `${days} days ago`
  }

  const weeks = Math.round(days / 7)
  if (weeks < 8) {
    return weeks === 1 ? '1 week ago' : `${weeks} weeks ago`
  }

  const months = Math.round(days / 30)
  if (months < 18) {
    return months === 1 ? '1 month ago' : `${months} months ago`
  }

  const years = Math.max(1, Math.round(days / 365))
  return years === 1 ? '1 year ago' : `${years} years ago`
}

/** Never / missing timestamps sort to the empty extreme so quiet accounts are findable. */
export function compareLastActivity(
  a: string | null | undefined,
  b: string | null | undefined,
): number {
  const av = parseStamp(a)
  const bv = parseStamp(b)
  return av - bv
}

export function compareTitle(a: string | null | undefined, b: string | null | undefined): number {
  return (a ?? '').localeCompare(b ?? '', undefined, { sensitivity: 'base' })
}

function parseStamp(value: string | null | undefined): number {
  if (value == null || value === '') {
    return Number.NEGATIVE_INFINITY
  }

  const ms = Date.parse(value)
  return Number.isNaN(ms) ? Number.NEGATIVE_INFINITY : ms
}
