export type DocumentsHrefQuery = {
  bucket?: string
  organizationId?: string
  statusId?: string
  assignedToUserId?: string
  uploadedFrom?: string
  uploadedTo?: string
  workedFrom?: string
  workedTo?: string
}

export function documentsHref(query: DocumentsHrefQuery) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value) params.set(key, value)
  }
  const qs = params.toString()
  return qs ? `/documents?${qs}` : '/documents'
}

export function endOfDayIso(date: string) {
  return `${date}T23:59:59.999Z`
}

export function startOfDayIso(date: string) {
  return `${date}T00:00:00.000Z`
}
