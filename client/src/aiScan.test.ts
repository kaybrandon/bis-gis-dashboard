import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  applyAiFill,
  applyAutoAiScan,
  fieldMatchesBaseline,
  isAiScanFailed,
  isAiScanInFlight,
  type AiFillDraft,
} from './aiScan.ts'
import type { AiFillResponse, WorkItemAiScanBaseline } from './api.ts'

const baseline: WorkItemAiScanBaseline = {
  title: 'upload.pdf',
  documentTypeId: 'type-deed',
  propertyIds: '',
  annexationCount: 0,
  correctionCount: 0,
  deedCount: 0,
  platCount: 0,
  workedOn: null,
}

const draft: AiFillDraft = {
  title: 'upload.pdf',
  documentTypeId: 'type-deed',
  propertyIds: '',
  annexationCount: 0,
  correctionCount: 0,
  deedCount: 0,
  platCount: 0,
  workedOn: null,
}

const result: AiFillResponse = {
  overallConfidence: 0.84,
  deployment: 'gpt-4.1-mini',
  difficulty: { band: 'Medium', why: 'Correction on a web map.', reasons: ['Correction on a web map.'], overridden: false },
  fields: {
    title: { present: true, value: 'CORRECTION CR 315A', confidence: 0.9 },
    type: { present: true, documentTypeId: 'type-other', documentTypeName: 'Other', confidence: 0.8 },
    propertyIds: { present: true, value: '11402\n11418', confidence: 0.7 },
    annexationCount: { present: true, value: 0, confidence: 0.5 },
    correctionCount: { present: true, value: 1, confidence: 0.8 },
    deedCount: { present: true, value: 0, confidence: 0.5 },
    platCount: { present: true, value: 0, confidence: 0.5 },
    workedOn: { present: true, value: '2026-03-15', confidence: 0.6 },
  },
}

describe('auto AI-scan apply rules', () => {
  it('pre-populates unchanged upload fields and leaves dirty human edits', () => {
    const clean = applyAutoAiScan(draft, result, baseline)
    assert.equal(clean.next.title, 'CORRECTION CR 315A')
    assert.equal(clean.next.propertyIds, '11402\n11418')
    assert.ok(clean.hints.title)
    assert.equal(clean.hints.title?.approved, false)

    const edited = applyAutoAiScan({ ...draft, title: 'Staff title', propertyIds: '' }, result, baseline)
    assert.equal(edited.next.title, 'Staff title')
    assert.equal(edited.hints.title, undefined)
    assert.equal(edited.next.propertyIds, '11402\n11418')
    assert.ok(edited.hints.propertyIds)
  })

  it('manual fill still overwrites when the user confirms', () => {
    const edited = { ...draft, title: 'Staff title' }
    const applied = applyAiFill(edited, result)
    assert.equal(applied.next.title, 'CORRECTION CR 315A')
    assert.ok(applied.hints.title)
  })

  it('matches baseline dates and blank property IDs', () => {
    assert.equal(fieldMatchesBaseline(draft, baseline, 'workedOn'), true)
    assert.equal(fieldMatchesBaseline({ ...draft, workedOn: '2026-03-15' }, baseline, 'workedOn'), false)
    assert.equal(isAiScanInFlight('pending'), true)
    assert.equal(isAiScanInFlight('running'), true)
    assert.equal(isAiScanFailed('unconfigured'), true)
    assert.equal(isAiScanFailed('failed'), true)
    assert.equal(isAiScanInFlight('succeeded'), false)
  })
})
