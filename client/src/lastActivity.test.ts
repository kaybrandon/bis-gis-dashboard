import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  compareLastActivity,
  compareTitle,
  formatExactCentralTime,
  formatLastActivity,
} from './lastActivity.ts'

describe('last activity display', () => {
  it('shows Never when there is no last-login timestamp', () => {
    assert.deepEqual(formatLastActivity(null), { label: 'Never' })
    assert.deepEqual(formatLastActivity(undefined), { label: 'Never' })
    assert.deepEqual(formatLastActivity(''), { label: 'Never' })
    assert.deepEqual(formatLastActivity('not-a-date'), { label: 'Never' })
  })

  it('never treats a missing login as Just now', () => {
    const now = new Date('2026-09-18T13:21:00Z')
    assert.notEqual(formatLastActivity(null, now).label, 'Just now')
    assert.equal(formatLastActivity(null, now).tooltip, undefined)
  })

  it('uses a real last-login timestamp for relative text and a CT tooltip', () => {
    const now = new Date('2026-09-18T18:21:00Z')
    const stamped = formatLastActivity('2026-09-18T18:20:00Z', now)
    assert.equal(stamped.label, 'Just now')
    assert.ok(stamped.tooltip)
    assert.match(stamped.tooltip ?? '', /2026/)
    assert.match(stamped.tooltip ?? '', /CT|CDT|CST/)

    const older = formatLastActivity('2026-09-17T18:21:00Z', now)
    assert.equal(older.label, '1 day ago')
    assert.equal(
      formatExactCentralTime(new Date('2026-09-18T18:20:00Z')),
      stamped.tooltip,
    )
  })
})

describe('last activity and title sort', () => {
  it('puts Never at the empty extreme so quiet accounts are findable', () => {
    const rows = ['2026-09-18T12:00:00Z', null, '2026-09-10T12:00:00Z', '']
    const asc = [...rows].sort(compareLastActivity)
    assert.deepEqual(asc, [null, '', '2026-09-10T12:00:00Z', '2026-09-18T12:00:00Z'])
    const desc = [...rows].sort((a, b) => compareLastActivity(b, a))
    assert.deepEqual(desc, ['2026-09-18T12:00:00Z', '2026-09-10T12:00:00Z', null, ''])
  })

  it('sorts Title case-insensitively', () => {
    const titles = ['Surveyor', null, 'analyst', '']
    const asc = [...titles].sort(compareTitle)
    assert.deepEqual(asc, [null, '', 'analyst', 'Surveyor'])
  })
})
