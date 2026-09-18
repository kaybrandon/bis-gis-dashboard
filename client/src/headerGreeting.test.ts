import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { formatHeaderGreeting, greetingFirstName, timeOfDayGreeting } from './headerGreeting.ts'
import { MORALE_TAGLINES, nextMoraleTagline } from './morale/taglines.ts'

describe('header greeting', () => {
  it('uses the first token of Full name and never email', () => {
    assert.equal(greetingFirstName('Global Admin'), 'Global')
    assert.equal(greetingFirstName('Alex Rivera'), 'Alex')
    assert.equal(greetingFirstName('  Jordan Hale  '), 'Jordan')
    assert.equal(greetingFirstName(null), null)
    assert.equal(greetingFirstName(''), null)
    assert.equal(greetingFirstName('admin@bisconsultants.local'), null)
  })

  it('uses America/Chicago cutovers for morning / afternoon / evening', () => {
    assert.equal(timeOfDayGreeting(new Date('2026-09-17T11:59:00-05:00')), 'Good morning')
    assert.equal(timeOfDayGreeting(new Date('2026-09-17T12:00:00-05:00')), 'Good afternoon')
    assert.equal(timeOfDayGreeting(new Date('2026-09-17T16:59:00-05:00')), 'Good afternoon')
    assert.equal(timeOfDayGreeting(new Date('2026-09-17T17:00:00-05:00')), 'Good evening')
  })

  it('quotes the tagline on the same line as the greeting', () => {
    const at = new Date('2026-09-17T08:15:00-05:00')
    assert.equal(
      formatHeaderGreeting('Global Admin', 'Tiny wins stack up.', at),
      'Good morning, Global “Tiny wins stack up.”',
    )
    assert.equal(
      formatHeaderGreeting(null, 'Tiny wins stack up.', at),
      'Good morning “Tiny wins stack up.”',
    )
  })

  it('rotates taglines so the same user does not see the identical line every load', () => {
    const memory = new Map<string, string>()
    const storage = {
      getItem: (key: string) => memory.get(key) ?? null,
      setItem: (key: string, value: string) => {
        memory.set(key, value)
      },
    }
    const first = nextMoraleTagline('user-1', storage)
    const second = nextMoraleTagline('user-1', storage)
    assert.equal(first, MORALE_TAGLINES[0])
    assert.equal(second, MORALE_TAGLINES[1])
    assert.notEqual(first, second)
  })
})
