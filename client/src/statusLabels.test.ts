import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { reviewLabel, statusLabel } from './statusLabels.ts'

describe('status and review labels', () => {
  it('keeps Needs Review as its own status label', () => {
    assert.equal(statusLabel('Needs Review'), 'Needs Review')
    assert.equal(statusLabel('In Progress'), 'Active')
    assert.equal(statusLabel('Held'), 'On-Hold')
    assert.equal(statusLabel('Worked'), 'Complete')
  })

  it('does not treat Needs Review as Reviewed Yes', () => {
    assert.equal(reviewLabel(true), 'Yes')
    assert.equal(reviewLabel(false), 'No')
    assert.notEqual(statusLabel('Needs Review'), 'Yes')
    assert.notEqual(statusLabel('Needs Review'), reviewLabel(true))
  })
})
