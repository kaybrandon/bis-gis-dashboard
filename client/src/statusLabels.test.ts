import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  CANONICAL_STATUS_LABELS,
  chartStatusCounts,
  isCanonicalStatus,
  reviewLabel,
  statusLabel,
} from './statusLabels.ts'

describe('status and review labels', () => {
  it('keeps Needs Review as its own status label', () => {
    assert.equal(statusLabel('Needs Review'), 'Needs Review')
    assert.equal(statusLabel('In Progress'), 'Active')
    assert.equal(statusLabel('Held'), 'On-Hold')
    assert.equal(statusLabel('On Hold'), 'On-Hold')
    assert.equal(statusLabel('Worked'), 'Complete')
  })

  it('does not treat Needs Review as Reviewed Yes', () => {
    assert.equal(reviewLabel(true), 'Yes')
    assert.equal(reviewLabel(false), 'No')
    assert.notEqual(statusLabel('Needs Review'), 'Yes')
    assert.notEqual(statusLabel('Needs Review'), reviewLabel(true))
  })

  it('exposes the BA-locked canonical set and does not equate QC\'d', () => {
    assert.deepEqual([...CANONICAL_STATUS_LABELS], [
      'Active',
      'Pending',
      'Complete',
      'On-Hold',
      'Cancelled',
      'Needs Review',
    ])
    assert.equal(isCanonicalStatus('Active'), true)
    assert.equal(isCanonicalStatus('In Progress'), true)
    assert.equal(isCanonicalStatus('Needs Review'), true)
    assert.equal(isCanonicalStatus("QC'd"), false)
    assert.equal(statusLabel("QC'd"), "QC'd")
    assert.notEqual(statusLabel("QC'd"), 'Complete')
  })

  it('charts keep canonical statuses only and leave QC\'d unmapped', () => {
    const slices = chartStatusCounts([
      { name: 'In Progress', count: 2 },
      { name: 'Needs Review', count: 1 },
      { name: "QC'd", count: 4 },
      { name: 'Worked', count: 3 },
      { name: 'Held', count: 0 },
    ])
    assert.deepEqual(slices.map((row) => statusLabel(row.name)), ['Active', 'Needs Review', 'Complete'])
    assert.equal(slices.some((row) => row.name === "QC'd"), false)
  })
})
