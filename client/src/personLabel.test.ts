import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { reportPersonLabel } from './personLabel.ts'

describe('reportPersonLabel', () => {
  it('uses Full name when it is on file', () => {
    assert.equal(reportPersonLabel({ displayName: 'arivera', fullName: 'Alex Rivera' }), 'Alex Rivera')
  })

  it('falls back to username when Full name is blank', () => {
    assert.equal(reportPersonLabel({ displayName: 'admin', fullName: '   ' }), 'admin')
    assert.equal(reportPersonLabel({ displayName: 'admin', fullName: null }), 'admin')
  })
})
