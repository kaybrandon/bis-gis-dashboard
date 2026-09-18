import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { PresenceUser } from './api'
import { api } from './api'

const POLL_MS = 20_000

export type HelpPeer = Pick<PresenceUser, 'userId' | 'displayName' | 'presenceStatus'> &
  Partial<PresenceUser>

type PresenceContextValue = {
  enabled: boolean
  selfUserId?: string
  items: PresenceUser[]
  onlineCount: number
  needsHelpCount: number
  loading: boolean
  error: string | null
  load: () => Promise<void>
  selfNeedsHelp: boolean
  setNeedsHelp: (needsHelp: boolean) => Promise<void>
  helpPeer: HelpPeer | null
  openHelp: (peer: HelpPeer) => void
  closeHelp: () => void
}

const PresenceContext = createContext<PresenceContextValue | null>(null)

export function PresenceProvider({
  enabled,
  selfUserId,
  children,
}: {
  enabled: boolean
  selfUserId?: string
  children: ReactNode
}) {
  const [items, setItems] = useState<PresenceUser[]>([])
  const [onlineCount, setOnlineCount] = useState(0)
  const [needsHelpCount, setNeedsHelpCount] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(enabled)
  const [selfNeedsHelp, setSelfNeedsHelp] = useState(false)
  const [helpPeer, setHelpPeer] = useState<HelpPeer | null>(null)

  const load = useCallback(async () => {
    if (!enabled) return
    try {
      const result = await api.presence()
      setItems(result.items)
      setOnlineCount(result.onlineCount)
      setNeedsHelpCount(result.needsHelpCount)
      const me = result.items.find((row) => row.userId === selfUserId)
      setSelfNeedsHelp(Boolean(me?.needsHelp))
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load who’s online.')
    } finally {
      setLoading(false)
    }
  }, [enabled, selfUserId])

  useEffect(() => {
    if (!enabled) {
      setItems([])
      setOnlineCount(0)
      setNeedsHelpCount(0)
      setSelfNeedsHelp(false)
      setLoading(false)
      return
    }
    void load()
    const id = window.setInterval(() => void load(), POLL_MS)
    return () => window.clearInterval(id)
  }, [enabled, load])

  const setNeedsHelp = useCallback(async (needsHelp: boolean) => {
    setSelfNeedsHelp(needsHelp)
    setItems((current) =>
      current.map((row) => (row.userId === selfUserId ? { ...row, needsHelp } : row)),
    )
    setNeedsHelpCount((count) => {
      const mine = items.find((row) => row.userId === selfUserId)?.needsHelp
      if (needsHelp && !mine) return count + 1
      if (!needsHelp && mine) return Math.max(0, count - 1)
      return count
    })
    await api.setNeedsHelp(needsHelp)
    await load()
  }, [items, load, selfUserId])

  const value = useMemo<PresenceContextValue>(
    () => ({
      enabled,
      selfUserId,
      items,
      onlineCount,
      needsHelpCount,
      loading,
      error,
      load,
      selfNeedsHelp,
      setNeedsHelp,
      helpPeer,
      openHelp: (peer) => {
        if (peer.userId === selfUserId) return
        setHelpPeer(peer)
      },
      closeHelp: () => setHelpPeer(null),
    }),
    [enabled, error, helpPeer, items, load, loading, needsHelpCount, onlineCount, selfNeedsHelp, selfUserId, setNeedsHelp],
  )

  return <PresenceContext.Provider value={value}>{children}</PresenceContext.Provider>
}

export function usePresence() {
  const ctx = useContext(PresenceContext)
  if (!ctx) {
    return {
      enabled: false,
      selfUserId: undefined,
      items: [],
      onlineCount: 0,
      needsHelpCount: 0,
      loading: false,
      error: null,
      load: async () => undefined,
      selfNeedsHelp: false,
      setNeedsHelp: async () => undefined,
      helpPeer: null,
      openHelp: () => undefined,
      closeHelp: () => undefined,
    } satisfies PresenceContextValue
  }
  return ctx
}
