import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  ATTENDANCE_CLOCK_STORAGE,
  TIME_CLOCK_STORAGE,
  attendancePresence,
  emptyClock,
  formatElapsed,
  isClockedIn,
  loadClock,
  saveClock,
} from './timeClock.ts'

class MemoryStorage {
  private readonly data = new Map<string, string>()
  getItem(key: string) {
    return this.data.get(key) ?? null
  }
  setItem(key: string, value: string) {
    this.data.set(key, value)
  }
  removeItem(key: string) {
    this.data.delete(key)
  }
}

function withStorage(run: (storage: MemoryStorage) => void) {
  const storage = new MemoryStorage()
  const previous = globalThis.localStorage
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: storage })
  try {
    run(storage)
  } finally {
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: previous })
  }
}

describe('CR07 attendance clock', () => {
  it('starts clocked out and does not bind a work item', () => {
    withStorage(() => {
      const clock = loadClock()
      assert.equal(clock.startedAt, null)
      assert.equal(isClockedIn(clock), false)
      assert.deepEqual(attendancePresence(clock), { clockedIn: false, clockWorkItemId: null })
    })
  })

  it('persists clock-in without a document target', () => {
    withStorage((storage) => {
      saveClock({ startedAt: 1_700_000_000_000 })
      const clock = loadClock()
      assert.equal(clock.startedAt, 1_700_000_000_000)
      assert.equal(isClockedIn(clock), true)
      assert.deepEqual(attendancePresence(clock), { clockedIn: true, clockWorkItemId: null })
      assert.equal(JSON.parse(storage.getItem(ATTENDANCE_CLOCK_STORAGE) ?? '{}').startedAt, 1_700_000_000_000)
    })
  })

  it('ignores the old work-item timer storage key', () => {
    withStorage((storage) => {
      storage.setItem(TIME_CLOCK_STORAGE, JSON.stringify({
        target: { id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', fileName: 'plat.pdf', organizationName: 'Demo' },
        startedAt: 1_700_000_000_000,
        note: 'document timer',
      }))
      const clock = loadClock()
      assert.deepEqual(clock, emptyClock)
      assert.equal(isClockedIn(clock), false)
    })
  })

  it('formats elapsed attendance time', () => {
    assert.equal(formatElapsed(0, 12_000), '0:12')
    assert.equal(formatElapsed(0, 3_661_000), '1:01:01')
  })
})
