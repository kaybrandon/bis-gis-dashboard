/** Time-of-day greeting uses America/Chicago (GIS-DASHBOARD-MORALE-AC). */
export const GREETING_TIME_ZONE = 'America/Chicago'

/** First token of signed-in Full name. Never email, username, userId, or a hardcoded person. */
export function greetingFirstName(fullName?: string | null): string | null {
  const first = fullName?.trim().split(/\s+/).find(Boolean)
  if (!first || first.includes('@')) return null
  return first
}

export function chicagoHour(at: Date = new Date(), timeZone = GREETING_TIME_ZONE): number {
  const hour = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hour: 'numeric',
    hourCycle: 'h23',
  })
    .formatToParts(at)
    .find((part) => part.type === 'hour')?.value
  const n = Number(hour)
  return Number.isFinite(n) ? n : 0
}

export type TimeOfDayGreeting = 'Good morning' | 'Good afternoon' | 'Good evening'

export function timeOfDayGreeting(at: Date = new Date(), timeZone = GREETING_TIME_ZONE): TimeOfDayGreeting {
  const hour = chicagoHour(at, timeZone)
  if (hour < 12) return 'Good morning'
  if (hour < 17) return 'Good afternoon'
  return 'Good evening'
}

/** Same-line header: `Good morning, Alex “Tiny wins stack up.”` */
export function formatHeaderGreeting(
  fullName: string | null | undefined,
  tagline: string,
  at: Date = new Date(),
  timeZone = GREETING_TIME_ZONE,
): string {
  const hello = timeOfDayGreeting(at, timeZone)
  const first = greetingFirstName(fullName)
  const lead = first ? `${hello}, ${first}` : hello
  return `${lead} “${tagline}”`
}
