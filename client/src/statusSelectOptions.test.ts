import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { statusSelectOptions } from './statusSelectOptions.ts'

const actions = {
  pendingId: 'pending',
  activeId: 'active',
  onHoldId: 'hold',
  completeId: 'complete',
  cancelledId: 'cancelled',
  needsReviewId: 'needs-review',
  completedIds: ['complete', 'qcd'],
}

describe('status select options', () => {
  it('includes exact Needs Review for roles that can change status', () => {
    const labels = statusSelectOptions(actions).map((option) => option.label)
    assert.deepEqual(labels, ['Active', 'Pending', 'Complete', 'On-Hold', 'Needs Review', 'Cancelled'])
    assert.equal(statusSelectOptions(actions).find((option) => option.value === 'needs-review')?.label, 'Needs Review')
  })

  it('keeps QC\'d as a current-value option without equating Needs Review to Reviewed', () => {
    const options = statusSelectOptions(actions, { id: 'qcd', name: "QC'd" })
    assert.equal(options.some((option) => option.label === "QC'd"), true)
    assert.equal(options.some((option) => option.label === 'Needs Review'), true)
    assert.equal(options.some((option) => option.label === 'Reviewed'), false)
  })
})
