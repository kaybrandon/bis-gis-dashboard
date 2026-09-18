export const PREVIEW_UNAVAILABLE_MESSAGE =
  'Preview is unavailable for this file. Download the original instead.'

export const TIFF_UNAVAILABLE_MESSAGE =
  'Preview is unavailable for this TIFF. Download the original instead.'

export type ViewerKind = 'pdf' | 'image' | 'tiff' | 'unavailable'

export function normalizeContentType(contentType?: string | null): string {
  return (contentType ?? '').split(';')[0]?.trim().toLowerCase() ?? ''
}

export function fileExtension(fileName: string): string {
  const trimmed = fileName.trim()
  const dot = trimmed.lastIndexOf('.')
  if (dot < 0 || dot === trimmed.length - 1) return ''
  return trimmed.slice(dot).toLowerCase()
}

export function isTiff(contentType?: string | null, fileName = ''): boolean {
  const mime = normalizeContentType(contentType)
  if (mime === 'image/tiff' || mime === 'image/tif' || mime === 'image/x-tiff') return true
  const ext = fileExtension(fileName)
  return ext === '.tif' || ext === '.tiff'
}

export function isBrowserImage(contentType?: string | null, fileName = ''): boolean {
  if (isTiff(contentType, fileName)) return false
  const mime = normalizeContentType(contentType)
  if (mime.startsWith('image/')) return true
  const ext = fileExtension(fileName)
  return ext === '.png' || ext === '.jpg' || ext === '.jpeg' || ext === '.gif' || ext === '.webp'
}

export function isPdf(contentType?: string | null, fileName = ''): boolean {
  const mime = normalizeContentType(contentType)
  if (mime.includes('pdf')) return true
  return fileExtension(fileName) === '.pdf'
}

export function viewerKind(contentType?: string | null, fileName = ''): ViewerKind {
  if (isTiff(contentType, fileName)) return 'tiff'
  if (isPdf(contentType, fileName)) return 'pdf'
  if (isBrowserImage(contentType, fileName)) return 'image'
  return 'unavailable'
}

/** TIFF uses /preview (first-page PNG). JPEG and other browser images keep /file. */
export function viewerFilePath(workItemId: string, contentType?: string | null, fileName = ''): string {
  const kind = viewerKind(contentType, fileName)
  if (kind === 'tiff') return `/api/work-items/${workItemId}/preview`
  return `/api/work-items/${workItemId}/file`
}

export function tiffMorePagesNote(pageCount: number): string | null {
  if (pageCount <= 1) return null
  return `This TIFF has ${pageCount} pages. Showing the first page.`
}

export function unavailablePreviewMessage(fileName: string): string {
  const name = fileName.trim() || 'This file'
  return `${name}: ${PREVIEW_UNAVAILABLE_MESSAGE}`
}
