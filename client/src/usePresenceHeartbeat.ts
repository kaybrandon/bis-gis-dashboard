import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import { api } from './api'
import { documentIdFromPath, loadClock, TIME_CLOCK_CHANGED } from './timeClock'

const HEARTBEAT_MS = 45_000

function payload(pathname: string) {
  const clock = loadClock()
  const routeId = documentIdFromPath(pathname)
  const clockedIn = clock.startedAt != null && Boolean(clock.target?.id)
  return {
    route: pathname || '/',
    workItemId: routeId,
    clockedIn,
    clockWorkItemId: clockedIn ? clock.target?.id ?? null : null,
  }
}

export function usePresenceHeartbeat(enabled: boolean) {
  const location = useLocation()

  useEffect(() => {
    if (!enabled) return

    let cancelled = false
    const send = () => {
      if (cancelled) return
      void api.heartbeat(payload(location.pathname)).catch(() => undefined)
    }

    send()
    const id = window.setInterval(send, HEARTBEAT_MS)
    const onClock = () => send()
    window.addEventListener(TIME_CLOCK_CHANGED, onClock)
    return () => {
      cancelled = true
      window.clearInterval(id)
      window.removeEventListener(TIME_CLOCK_CHANGED, onClock)
    }
  }, [enabled, location.pathname])
}
