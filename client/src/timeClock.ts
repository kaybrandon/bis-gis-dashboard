export const TIME_LOGGED_EVENT = 'gis-time-logged'
export const TIME_CLOCK_CHANGED = 'gis-time-clock-changed'
export const TIME_CLOCK_STORAGE = 'gis.timeClock'
export const ATTENDANCE_CLOCK_STORAGE = 'gis.attendanceClock'
export const TIME_CLOCK_POS_STORAGE = 'gis.timeClock.pos'

export type AttendanceClockState = {
  startedAt: number | null
}

export const emptyClock: AttendanceClockState = {
  startedAt: null,
}

export function loadClock(): AttendanceClockState {
  try {
    const raw = localStorage.getItem(ATTENDANCE_CLOCK_STORAGE)
    if (!raw) return emptyClock
    const parsed = JSON.parse(raw) as AttendanceClockState
    if (!parsed || typeof parsed !== 'object') return emptyClock
    return {
      startedAt: typeof parsed.startedAt === 'number' ? parsed.startedAt : null,
    }
  } catch {
    return emptyClock
  }
}

export function saveClock(state: AttendanceClockState) {
  localStorage.setItem(ATTENDANCE_CLOCK_STORAGE, JSON.stringify({
    startedAt: state.startedAt,
  }))
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(TIME_CLOCK_CHANGED))
  }
}

export function isClockedIn(state: AttendanceClockState = loadClock()): boolean {
  return state.startedAt != null
}

export function attendancePresence(state: AttendanceClockState = loadClock()) {
  return {
    clockedIn: isClockedIn(state),
    clockWorkItemId: null as string | null,
  }
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

export function defaultClockPos(fabWidth = 132, fabHeight = 40): ClockPosition {
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
