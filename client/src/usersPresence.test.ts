import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { isLiveOnline } from './usersPresence.ts'

describe('Users live online dot', () => {
  it('shows green only for a real Online presence', () => {
    assert.equal(isLiveOnline('Online'), true)
    assert.equal(isLiveOnline('Online', false), true)
  })

  it('shows no dot when offline, away, or unknown (not a gray fake-online)', () => {
    assert.equal(isLiveOnline('Away'), false)
    assert.equal(isLiveOnline('Offline'), false)
    assert.equal(isLiveOnline(null), false)
    assert.equal(isLiveOnline(undefined), false)
    assert.equal(isLiveOnline(''), false)
  })

  it('never shows a live green dot on an archived user', () => {
    assert.equal(isLiveOnline('Online', true), false)
    assert.equal(isLiveOnline('Away', true), false)
  })
})
