import { GREETING_TIME_ZONE } from './headerGreeting'

export const HELP_INBOX_PREVIEW_MAX = 80

function chicagoDateKey(at: Date, timeZone = GREETING_TIME_ZONE): string {
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(at)
}

/** Compact header-inbox time: same Chicago day → 3:51 PM; older → Sep 17. */
export function formatHelpInboxTime(
  iso: string,
  now: Date = new Date(),
  timeZone = GREETING_TIME_ZONE,
): string {
  const at = new Date(iso)
  if (Number.isNaN(at.getTime())) return ''
  const sameDay = chicagoDateKey(at, timeZone) === chicagoDateKey(now, timeZone)
  if (sameDay) {
    return new Intl.DateTimeFormat('en-US', {
      timeZone,
      hour: 'numeric',
      minute: '2-digit',
    }).format(at)
  }
  return new Intl.DateTimeFormat('en-US', {
    timeZone,
    month: 'short',
    day: 'numeric',
  }).format(at)
}

export function previewHelpInbox(body: string, max = HELP_INBOX_PREVIEW_MAX): string {
  const text = body.trim().replace(/\s+/g, ' ')
  if (text.length <= max) return text
  return `${text.slice(0, max - 1).trimEnd()}…`
}
