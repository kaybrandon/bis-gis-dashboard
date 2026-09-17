export type QueueStatus = 'pending' | 'uploading' | 'done' | 'failed' | 'skipped'

export type UploadQueueItem = {
  key: string
  file: File
  status: QueueStatus
  error?: string
  workItemId?: string
}

export type UploadLimits = {
  maxFileBytes: number
  maxFileMegabytes: number
  concurrency: number
}

/** Default 50 MB per file; queue runs 3 at a time. Override via App Setting Uploads__MaxFileMegabytes. */
export const DEFAULT_UPLOAD_LIMITS: UploadLimits = {
  maxFileBytes: 52_428_800,
  maxFileMegabytes: 50,
  concurrency: 3,
}

export function parseUploadLimits(settings: Record<string, unknown> | null | undefined): UploadLimits {
  const raw = settings?.uploads
  if (!raw || typeof raw !== 'object') return DEFAULT_UPLOAD_LIMITS
  const u = raw as Record<string, unknown>
  const maxFileBytes =
    typeof u.maxFileBytes === 'number' && u.maxFileBytes > 0 ? u.maxFileBytes : DEFAULT_UPLOAD_LIMITS.maxFileBytes
  const maxFileMegabytes =
    typeof u.maxFileMegabytes === 'number' && u.maxFileMegabytes > 0
      ? u.maxFileMegabytes
      : Math.max(1, Math.round(maxFileBytes / (1024 * 1024)))
  const concurrency =
    typeof u.concurrency === 'number' && u.concurrency >= 1 && u.concurrency <= 8
      ? Math.floor(u.concurrency)
      : DEFAULT_UPLOAD_LIMITS.concurrency
  return { maxFileBytes, maxFileMegabytes, concurrency }
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} bytes`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(bytes >= 10 * 1024 ? 0 : 1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function oversizedMessage(file: File, maxBytes: number): string {
  return `${file.name} is ${formatFileSize(file.size)}, which is over the ${formatFileSize(maxBytes)} per-file limit. That file was not uploaded.`
}

export async function runUploadQueue(
  files: File[],
  options: {
    maxFileBytes: number
    concurrency: number
    upload: (file: File) => Promise<{ id: string }>
    onUpdate: (items: UploadQueueItem[]) => void
  },
): Promise<UploadQueueItem[]> {
  const items: UploadQueueItem[] = files.map((file, index) => {
    const key = `${index}-${file.name}-${file.size}`
    if (file.size > options.maxFileBytes) {
      return { key, file, status: 'skipped', error: oversizedMessage(file, options.maxFileBytes) }
    }
    return { key, file, status: 'pending' }
  })
  options.onUpdate(items.slice())

  const pending = items.map((item, index) => (item.status === 'pending' ? index : -1)).filter((index) => index >= 0)
  const concurrency = Math.max(1, Math.min(options.concurrency, 8, pending.length || 1))
  let cursor = 0

  async function worker() {
    while (true) {
      const slot = cursor
      cursor += 1
      if (slot >= pending.length) return
      const index = pending[slot]
      items[index] = { ...items[index], status: 'uploading' }
      options.onUpdate(items.slice())
      try {
        const result = await options.upload(items[index].file)
        items[index] = { ...items[index], status: 'done', workItemId: result.id }
      } catch (err) {
        items[index] = {
          ...items[index],
          status: 'failed',
          error: err instanceof Error ? err.message : 'Upload failed.',
        }
      }
      options.onUpdate(items.slice())
    }
  }

  await Promise.all(Array.from({ length: Math.min(concurrency, pending.length) }, () => worker()))
  return items
}

export const MAX_UPLOAD_BATCH = 200

export type QueueProgress = {
  total: number
  done: number
  failed: number
  skipped: number
  pending: number
  uploading: number
  remaining: number
  finished: number
  current: number
  percent: number
  inFlight: boolean
}

export function queueProgress(items: UploadQueueItem[]): QueueProgress {
  const total = items.length
  const done = items.filter((i) => i.status === 'done').length
  const failed = items.filter((i) => i.status === 'failed').length
  const skipped = items.filter((i) => i.status === 'skipped').length
  const pending = items.filter((i) => i.status === 'pending').length
  const uploading = items.filter((i) => i.status === 'uploading').length
  const remaining = pending + uploading
  const finished = done + failed + skipped
  const current = Math.min(total, finished + uploading)
  const percent = total === 0 ? 0 : Math.round((current / total) * 100)
  return {
    total,
    done,
    failed,
    skipped,
    pending,
    uploading,
    remaining,
    finished,
    current,
    percent,
    inFlight: remaining > 0,
  }
}

export function queueSummary(items: UploadQueueItem[]): string {
  const p = queueProgress(items)
  return `${p.done} uploaded, ${p.skipped} skipped, ${p.failed} failed.`
}

export function completeSummaryMessage(items: UploadQueueItem[]): string {
  const p = queueProgress(items)
  if (p.failed === 0 && p.skipped === 0) {
    return p.done === 1 ? '1 document uploaded.' : `${p.done} documents uploaded.`
  }
  return `${p.done} uploaded, ${p.skipped} skipped, ${p.failed} failed.`
}
