export const TIME_LOGGED_EVENT = 'gis-time-logged'
export const TIME_CLOCK_CHANGED = 'gis-time-clock-changed'
export const TIME_CLOCK_STORAGE = 'gis.timeClock'
export const TIME_CLOCK_POS_STORAGE = 'gis.timeClock.pos'

export type TimeClockTarget = {
  id: string
  fileName: string
  organizationName: string
  statusName?: string
}

export type TimeClockState = {
  target: TimeClockTarget | null
  startedAt: number | null
  note: string
}

export const emptyClock: TimeClockState = {
  target: null,
  startedAt: null,
  note: '',
}

export function loadClock(): TimeClockState {
  try {
    const raw = localStorage.getItem(TIME_CLOCK_STORAGE)
    if (!raw) return emptyClock
    const parsed = JSON.parse(raw) as TimeClockState
    if (!parsed || typeof parsed !== 'object') return emptyClock
    const target = parsed.target && typeof parsed.target.id === 'string' && parsed.target.id
      ? parsed.target
      : null
    return {
      target,
      startedAt: target && typeof parsed.startedAt === 'number' ? parsed.startedAt : null,
      note: parsed.note ?? '',
    }
  } catch {
    return emptyClock
  }
}

export function saveClock(state: TimeClockState) {
  localStorage.setItem(TIME_CLOCK_STORAGE, JSON.stringify(state))
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(TIME_CLOCK_CHANGED))
  }
}

export function elapsedMinutes(startedAt: number, now = Date.now()) {
  const minutes = Math.round((now - startedAt) / 60_000)
  return Math.min(24 * 60, Math.max(1, minutes))
}

export function formatElapsed(startedAt: number, now = Date.now()) {
  const total = Math.max(0, Math.floor((now - startedAt) / 1000))
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
  return `${m}:${String(s).padStart(2, '0')}`
}

export function notifyTimeLogged(workItemId: string) {
  window.dispatchEvent(new CustomEvent(TIME_LOGGED_EVENT, { detail: { workItemId } }))
}

export type ClockPosition = { x: number; y: number }

export function clockPosKey(userId: string) {
  return `${TIME_CLOCK_POS_STORAGE}.${userId}`
}

export function defaultClockPos(fabWidth = 148, fabHeight = 40): ClockPosition {
  if (typeof window === 'undefined') return { x: 16, y: 16 }
  return {
    x: Math.max(8, window.innerWidth - fabWidth - 16),
    y: Math.max(8, window.innerHeight - fabHeight - 16),
  }
}

export function clampClockPos(pos: ClockPosition, fabWidth: number, fabHeight: number): ClockPosition {
  if (typeof window === 'undefined') return pos
  const maxX = Math.max(8, window.innerWidth - fabWidth - 8)
  const maxY = Math.max(8, window.innerHeight - fabHeight - 8)
  return {
    x: Math.min(Math.max(8, pos.x), maxX),
    y: Math.min(Math.max(8, pos.y), maxY),
  }
}

export function loadClockPos(userId: string): ClockPosition | null {
  try {
    const raw = localStorage.getItem(clockPosKey(userId))
    if (!raw) return null
    const parsed = JSON.parse(raw) as ClockPosition
    if (typeof parsed?.x !== 'number' || typeof parsed?.y !== 'number') return null
    return parsed
  } catch {
    return null
  }
}

export function saveClockPos(userId: string, pos: ClockPosition) {
  localStorage.setItem(clockPosKey(userId), JSON.stringify(pos))
}

export function documentIdFromPath(pathname: string) {
  const match = /^\/documents\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i.exec(pathname)
  return match?.[1] ?? null
}
