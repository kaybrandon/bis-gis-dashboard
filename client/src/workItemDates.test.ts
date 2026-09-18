import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  NEEDED_BY_LABEL,
  WORKED_LABEL,
  changeNeededBy,
  changeWorkedOn,
  neededByBadgeText,
  toDateSavePayload,
  workItemDateLabels,
} from './workItemDates.ts'

const worked = '2026-09-01T00:00:00.000Z'
const neededBy = '2026-10-05T00:00:00.000Z'
const laterWorked = '2026-09-18T00:00:00.000Z'
const laterNeeded = '2026-10-12T00:00:00.000Z'

describe('CR01 Worked vs Needed by independence', () => {
  it('changing Worked does not rewrite Needed by', () => {
    const draft = { title: 'Plat', workedOn: worked, priorityNeededBy: neededBy }
    const next = changeWorkedOn(draft, laterWorked)
    assert.equal(next.workedOn, laterWorked)
    assert.equal(next.priorityNeededBy, neededBy)
    assert.equal(next.title, 'Plat')
  })

  it('changing Needed by does not rewrite Worked', () => {
    const draft = { title: 'Plat', workedOn: worked, priorityNeededBy: neededBy }
    const next = changeNeededBy(draft, laterNeeded)
    assert.equal(next.priorityNeededBy, laterNeeded)
    assert.equal(next.workedOn, worked)
    assert.equal(next.title, 'Plat')
  })

  it('save payload keeps both dates and never copies one onto the other', () => {
    const afterWorked = changeWorkedOn(
      { workedOn: worked, priorityNeededBy: neededBy },
      laterWorked,
    )
    const payload = toDateSavePayload(afterWorked)
    assert.equal(payload.workedOn, laterWorked)
    assert.equal(payload.priorityNeededBy, neededBy)
    assert.equal(payload.clearWorkedOn, false)
    assert.equal(payload.clearPriorityNeededBy, false)
    assert.notEqual(payload.workedOn, payload.priorityNeededBy)
  })

  it('clearing one date does not clear the other', () => {
    const clearedWorked = toDateSavePayload(changeWorkedOn(
      { workedOn: worked, priorityNeededBy: neededBy },
      null,
    ))
    assert.equal(clearedWorked.workedOn, null)
    assert.equal(clearedWorked.clearWorkedOn, true)
    assert.equal(clearedWorked.priorityNeededBy, neededBy)
    assert.equal(clearedWorked.clearPriorityNeededBy, false)

    const clearedNeeded = toDateSavePayload(changeNeededBy(
      { workedOn: worked, priorityNeededBy: neededBy },
      null,
    ))
    assert.equal(clearedNeeded.priorityNeededBy, null)
    assert.equal(clearedNeeded.clearPriorityNeededBy, true)
    assert.equal(clearedNeeded.workedOn, worked)
    assert.equal(clearedNeeded.clearWorkedOn, false)
  })

  it('Needed by badge uses the saved Needed by value, not Worked', () => {
    assert.equal(
      neededByBadgeText({ neededBy, workedOn: worked }),
      'Needed by 2026-10-05',
    )
    assert.equal(
      neededByBadgeText({ neededBy, workedOn: laterWorked }),
      'Needed by 2026-10-05',
    )
    assert.notEqual(
      neededByBadgeText({ neededBy, workedOn: worked }),
      'Needed by 2026-09-01',
    )
    assert.equal(neededByBadgeText({ neededBy: null, workedOn: worked }), null)
  })
})

describe('CR02 persistent Worked and Needed by labels', () => {
  it('uses the exact BA-locked label strings', () => {
    assert.equal(WORKED_LABEL, 'Worked')
    assert.equal(NEEDED_BY_LABEL, 'Needed by')
    assert.notEqual(WORKED_LABEL, 'Worked date')
  })

  it('keeps both labels when values are empty, populated, cleared, edited, or reloaded', () => {
    for (const state of ['empty', 'populated', 'cleared', 'edited', 'reloaded'] as const) {
      const labels = workItemDateLabels(state)
      assert.equal(labels.worked, 'Worked')
      assert.equal(labels.neededBy, 'Needed by')
    }
  })
})
