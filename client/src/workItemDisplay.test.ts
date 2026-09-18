import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { totalTimeLabel, workItemTimeLine } from './workItemDisplay.ts'

describe('CR06 Reviewed + Total time display', () => {
  it('keeps Review Yes/No and Total time as separate labels', () => {
    const line = workItemTimeLine(
      { isReviewed: true, hoursLabel: '2h 15m' },
      { showReview: true, showTotalTime: true },
    )
    assert.equal(line, 'Review Yes · Total time 2h 15m')
    assert.match(line, /Review Yes/)
    assert.match(line, /Total time 2h 15m/)
    assert.doesNotMatch(line, /^2h 15m$/)
  })

  it('does not replace Reviewed with Hours when only review is requested', () => {
    const line = workItemTimeLine(
      { isReviewed: false, hoursLabel: '1h' },
      { showReview: true, showTotalTime: false },
    )
    assert.equal(line, 'Review No')
    assert.doesNotMatch(line, /Hours/)
    assert.doesNotMatch(line, /1h/)
  })

  it('shows Total time for the work item even when Review is hidden', () => {
    assert.equal(
      workItemTimeLine({ hoursLabel: '45m' }, { showTotalTime: true }),
      'Total time 45m',
    )
    assert.equal(totalTimeLabel(undefined), '0m')
    assert.equal(totalTimeLabel(''), '0m')
  })
})
