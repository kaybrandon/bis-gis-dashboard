/**
 * Rotating header morale taglines (GIS-DASHBOARD-MORALE-AC).
 * Warm, light, professional — no sarcasm, politics, or religion.
 * Admin-tunable later; this is the locked in-repo config for greeting restore.
 */
export const MORALE_TAGLINES = [
  'Tiny wins stack up. ✨',
  'One plat closer. 📍',
  'Steady hands, clean maps. 🗺️',
  'Keep the queue moving. 🚀',
  'Small steps add up. 🌱',
  'Good work looks quiet. ✅',
] as const

export const TAGLINE_STORAGE_KEY = 'gis.moraleTaglineIndex'

export function taglineStorageKey(userId?: string | null) {
  return userId ? `${TAGLINE_STORAGE_KEY}.${userId}` : TAGLINE_STORAGE_KEY
}

/** Next tagline in the list so the same user does not see the identical line every load. */
export function nextMoraleTagline(userId?: string | null, storage?: Pick<Storage, 'getItem' | 'setItem'>): string {
  const list: readonly string[] = MORALE_TAGLINES
  const store = storage ?? (typeof localStorage === 'undefined' ? undefined : localStorage)
  if (!store || list.length === 0) {
    return list[0] ?? ''
  }

  const key = taglineStorageKey(userId)
  const last = Number.parseInt(store.getItem(key) ?? '', 10)
  const next = Number.isFinite(last) ? (last + 1) % list.length : 0
  try {
    store.setItem(key, String(next))
  } catch {
    /* quota / private mode */
  }
  return list[next]
}
