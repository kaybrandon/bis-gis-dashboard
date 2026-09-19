/** QC4-03 — per-user Manage Documents column order. Browser-local; Reset is this user only. */

export const MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX = 'gis.manageDocumentsColumns'

export const MANAGE_DOCUMENTS_COLUMN_KEYS = [
  'filename',
  'client',
  'status',
  'assignedto',
  'uploadedAt',
  'workedon',
  'difficulty',
  'reviewed',
  'hours',
] as const

export type ManageDocumentsColumnKey = (typeof MANAGE_DOCUMENTS_COLUMN_KEYS)[number]

export const MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER: ManageDocumentsColumnKey[] = [
  ...MANAGE_DOCUMENTS_COLUMN_KEYS,
]

export const MANAGE_DOCUMENTS_COLUMN_LABELS: Record<ManageDocumentsColumnKey, string> = {
  filename: 'File name',
  client: 'Client name',
  status: 'Status',
  assignedto: 'Assigned to',
  uploadedAt: 'Upload date',
  workedon: 'Worked date',
  difficulty: 'Difficulty',
  reviewed: 'Review',
  hours: 'Total time',
}

/** QC4-02 guard — File name must keep a usable floor so reorder cannot collapse it away. */
export const MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH = 220

const FIXED_COLUMN_WIDTHS: Record<Exclude<ManageDocumentsColumnKey, 'filename'>, number> = {
  client: 160,
  status: 160,
  assignedto: 180,
  uploadedAt: 170,
  workedon: 140,
  difficulty: 120,
  reviewed: 90,
  hours: 110,
}

export const MANAGE_DOCUMENTS_GROUP_COLUMN_WIDTH = 160

const KNOWN_KEYS = new Set<string>(MANAGE_DOCUMENTS_COLUMN_KEYS)

export function manageDocumentsColumnPrefsKey(userId?: string | null) {
  return userId ? `${MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX}.${userId}` : MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX
}

export function isManageDocumentsColumnKey(value: unknown): value is ManageDocumentsColumnKey {
  return typeof value === 'string' && KNOWN_KEYS.has(value)
}

export function normalizeColumnOrder(order: unknown): ManageDocumentsColumnKey[] {
  const seen = new Set<ManageDocumentsColumnKey>()
  const next: ManageDocumentsColumnKey[] = []
  if (Array.isArray(order)) {
    for (const value of order) {
      if (!isManageDocumentsColumnKey(value) || seen.has(value)) continue
      next.push(value)
      seen.add(value)
    }
  }
  for (const key of MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER) {
    if (!seen.has(key)) next.push(key)
  }
  return next
}

export function moveColumn(
  order: readonly ManageDocumentsColumnKey[],
  key: ManageDocumentsColumnKey,
  toIndex: number,
): ManageDocumentsColumnKey[] {
  const from = order.indexOf(key)
  if (from < 0) return [...order]
  const clamped = Math.max(0, Math.min(toIndex, order.length - 1))
  if (from === clamped) return [...order]
  const next = order.slice()
  next.splice(from, 1)
  next.splice(clamped, 0, key)
  return next
}

function columnKeyOf(column: unknown) {
  return String((column as { key?: unknown }).key ?? '')
}

export function orderColumns<T>(columns: readonly T[], order: readonly string[]): T[] {
  const pinned = columns.filter((column) => columnKeyOf(column) === 'group')
  const rest = columns.filter((column) => columnKeyOf(column) !== 'group')
  const byKey = new Map(rest.map((column) => [columnKeyOf(column), column]))
  const ordered: T[] = []
  for (const key of order) {
    const column = byKey.get(key)
    if (!column) continue
    ordered.push(column)
    byKey.delete(key)
  }
  for (const column of rest) {
    if (byKey.has(columnKeyOf(column))) ordered.push(column)
  }
  return [...pinned, ...ordered]
}

export function manageDocumentsTableScrollX(hasGroupColumn: boolean) {
  const others = Object.values(FIXED_COLUMN_WIDTHS).reduce((sum, width) => sum + width, 0)
  return MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH + others + (hasGroupColumn ? MANAGE_DOCUMENTS_GROUP_COLUMN_WIDTH : 0)
}

function storageOf(storage?: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'> | null) {
  if (storage) return storage
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

export function readColumnOrder(
  userId?: string | null,
  storage?: Pick<Storage, 'getItem'> | null,
): ManageDocumentsColumnKey[] {
  try {
    const store = storage ?? storageOf()
    const raw = store?.getItem(manageDocumentsColumnPrefsKey(userId))
    if (!raw) return [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER]
    const parsed = JSON.parse(raw) as { order?: unknown } | unknown
    const order = parsed && typeof parsed === 'object' && 'order' in parsed ? parsed.order : parsed
    return normalizeColumnOrder(order)
  } catch {
    return [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER]
  }
}

export function writeColumnOrder(
  userId: string | null | undefined,
  order: readonly ManageDocumentsColumnKey[],
  storage?: Pick<Storage, 'setItem'> | null,
) {
  if (!userId) return
  try {
    const store = storage ?? storageOf()
    store?.setItem(
      manageDocumentsColumnPrefsKey(userId),
      JSON.stringify({ order: normalizeColumnOrder(order) }),
    )
  } catch {
    /* ignore quota / private mode */
  }
}

export function resetColumnOrder(
  userId: string | null | undefined,
  storage?: Pick<Storage, 'removeItem'> | null,
): ManageDocumentsColumnKey[] {
  if (userId) {
    try {
      const store = storage ?? storageOf()
      store?.removeItem(manageDocumentsColumnPrefsKey(userId))
    } catch {
      /* ignore */
    }
  }
  return [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER]
}
