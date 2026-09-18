import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { difficultyReasons, difficultyTagColor, difficultyWhyText, isDifficultyBand } from './documentDifficulty.ts'

describe('GIS-UI-10 document difficulty helpers', () => {
  it('maps Easy / Medium / Hard to chip colors', () => {
    assert.equal(difficultyTagColor('Easy'), 'green')
    assert.equal(difficultyTagColor('Medium'), 'gold')
    assert.equal(difficultyTagColor('Hard'), 'red')
    assert.equal(difficultyTagColor(null), 'default')
  })

  it('prefers 1–3 reason bullets and falls back to a short why sentence', () => {
    assert.deepEqual(
      difficultyReasons({
        why: 'Metes-and-bounds legal; 4 parcels.',
        reasons: ['Metes-and-bounds legal', '4 parcels', 'Easements present', 'extra'],
      }),
      ['Metes-and-bounds legal', '4 parcels', 'Easements present'],
    )
    assert.equal(
      difficultyWhyText({ why: 'Lot-and-block plat with one parcel.', reasons: [] }),
      'Lot-and-block plat with one parcel.',
    )
    assert.deepEqual(difficultyReasons(null), [])
  })

  it('accepts only the three bands', () => {
    assert.equal(isDifficultyBand('Easy'), true)
    assert.equal(isDifficultyBand('Medium'), true)
    assert.equal(isDifficultyBand('Hard'), true)
    assert.equal(isDifficultyBand('Extreme'), false)
  })
})
