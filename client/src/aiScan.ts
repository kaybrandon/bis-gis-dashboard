import type { AiFillResponse, WorkItemAiScanBaseline } from './api'

export type AiFieldKey =
  | 'title'
  | 'documentTypeId'
  | 'propertyIds'
  | 'annexationCount'
  | 'correctionCount'
  | 'deedCount'
  | 'platCount'
  | 'workedOn'

export type AiHint = { confidence: number; approved: boolean }

export type AiFillDraft = {
  title: string
  documentTypeId: string
  propertyIds: string
  annexationCount: number
  correctionCount: number
  deedCount: number
  platCount: number
  workedOn: string | null
}

export function isAiScanInFlight(status?: string | null): boolean {
  return status === 'pending' || status === 'running'
}

export function isAiScanFailed(status?: string | null): boolean {
  return status === 'failed' || status === 'unconfigured'
}

export function applyAiFill<T extends AiFillDraft>(
  current: T,
  result: AiFillResponse,
): { next: T; hints: Partial<Record<AiFieldKey, AiHint>> } {
  return applyAiFillFields(current, result, () => true)
}

export function applyAutoAiScan<T extends AiFillDraft>(
  current: T,
  result: AiFillResponse,
  baseline?: WorkItemAiScanBaseline | null,
): { next: T; hints: Partial<Record<AiFieldKey, AiHint>> } {
  return applyAiFillFields(current, result, (key, draft) => fieldMatchesBaseline(draft, baseline, key))
}

function applyAiFillFields<T extends AiFillDraft>(
  current: T,
  result: AiFillResponse,
  allow: (key: AiFieldKey, draft: T) => boolean,
): { next: T; hints: Partial<Record<AiFieldKey, AiHint>> } {
  const hints: Partial<Record<AiFieldKey, AiHint>> = {}
  const next = { ...current }
  const { fields } = result

  if (allow('title', current) && fields.title.present && fields.title.value) {
    next.title = fields.title.value
    hints.title = { confidence: fields.title.confidence, approved: false }
  }
  if (allow('documentTypeId', current) && fields.type.present && fields.type.documentTypeId) {
    next.documentTypeId = fields.type.documentTypeId
    hints.documentTypeId = { confidence: fields.type.confidence, approved: false }
  }
  if (allow('propertyIds', current) && fields.propertyIds.present && fields.propertyIds.value != null) {
    next.propertyIds = fields.propertyIds.value
    hints.propertyIds = { confidence: fields.propertyIds.confidence, approved: false }
  }
  if (allow('annexationCount', current) && fields.annexationCount.present && fields.annexationCount.value != null) {
    next.annexationCount = fields.annexationCount.value
    hints.annexationCount = { confidence: fields.annexationCount.confidence, approved: false }
  }
  if (allow('correctionCount', current) && fields.correctionCount.present && fields.correctionCount.value != null) {
    next.correctionCount = fields.correctionCount.value
    hints.correctionCount = { confidence: fields.correctionCount.confidence, approved: false }
  }
  if (allow('deedCount', current) && fields.deedCount.present && fields.deedCount.value != null) {
    next.deedCount = fields.deedCount.value
    hints.deedCount = { confidence: fields.deedCount.confidence, approved: false }
  }
  if (allow('platCount', current) && fields.platCount.present && fields.platCount.value != null) {
    next.platCount = fields.platCount.value
    hints.platCount = { confidence: fields.platCount.confidence, approved: false }
  }
  if (allow('workedOn', current) && fields.workedOn.present && fields.workedOn.value) {
    next.workedOn = fields.workedOn.value
    hints.workedOn = { confidence: fields.workedOn.confidence, approved: false }
  }

  return { next, hints }
}

export function fieldMatchesBaseline(
  current: AiFillDraft,
  baseline: WorkItemAiScanBaseline | null | undefined,
  field: AiFieldKey,
): boolean {
  if (!baseline) return false
  switch (field) {
    case 'title':
      return normalizeText(current.title) === normalizeText(baseline.title)
    case 'documentTypeId':
      return current.documentTypeId === baseline.documentTypeId
    case 'propertyIds':
      return normalizeText(current.propertyIds) === normalizeText(baseline.propertyIds)
    case 'annexationCount':
      return current.annexationCount === baseline.annexationCount
    case 'correctionCount':
      return current.correctionCount === baseline.correctionCount
    case 'deedCount':
      return current.deedCount === baseline.deedCount
    case 'platCount':
      return current.platCount === baseline.platCount
    case 'workedOn':
      return normalizeDate(current.workedOn) === normalizeDate(baseline.workedOn)
    default:
      return false
  }
}

function normalizeText(value?: string | null): string {
  return (value ?? '').trim()
}

function normalizeDate(value?: string | null): string {
  if (!value) return ''
  return value.slice(0, 10)
}
