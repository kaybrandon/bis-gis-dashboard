export const DEFAULT_SUPPORTED_TYPES_LABEL = 'PDF, Word (.doc, .docx), Excel (.xls, .xlsx), or images'

export const DEFAULT_REQUIRED_FILE_MESSAGE = 'Attach at least one PDF, Word, Excel, or image.'

export const DEFAULT_ACCEPTED_EXTENSIONS = [
  '.pdf',
  '.doc',
  '.docx',
  '.xls',
  '.xlsx',
  '.png',
  '.jpg',
  '.jpeg',
  '.gif',
  '.webp',
  '.tif',
  '.tiff',
]

export const DEFAULT_UPLOAD_ACCEPT =
  '.pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.gif,.webp,.tif,.tiff,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,image/png,image/jpeg,image/gif,image/webp,image/tiff'

const DEFAULT_ALLOWED_TYPES = new Set([
  'application/pdf',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'image/png',
  'image/jpeg',
  'image/jpg',
  'image/gif',
  'image/webp',
  'image/tiff',
])

export function fileExtension(fileName: string): string {
  const trimmed = fileName.trim()
  const dot = trimmed.lastIndexOf('.')
  if (dot < 0 || dot === trimmed.length - 1) return ''
  return trimmed.slice(dot).toLowerCase()
}

export function isAllowedUploadFile(
  file: { name: string; type?: string },
  extensions: readonly string[] = DEFAULT_ACCEPTED_EXTENSIONS,
): boolean {
  const ext = fileExtension(file.name)
  if (ext && extensions.some((allowed) => allowed.toLowerCase() === ext)) return true
  const mime = (file.type ?? '').split(';')[0]?.trim().toLowerCase()
  return !!mime && DEFAULT_ALLOWED_TYPES.has(mime)
}

export function unsupportedTypeMessage(
  fileName: string,
  label: string = DEFAULT_SUPPORTED_TYPES_LABEL,
): string {
  return `${fileName} is not a supported type. Upload a ${label}.`
}

export function dropzoneText(): string {
  return 'Drop PDF, Word, Excel, or image files, or click to browse'
}

export function dropzoneHint(maxFiles: number): string {
  return `${DEFAULT_SUPPORTED_TYPES_LABEL}. Up to ${maxFiles} files. Oversized files are skipped; the rest still upload.`
}
