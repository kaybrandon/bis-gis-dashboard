import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { documentsHref } from './documentsHref.ts'

describe('documentsHref', () => {
  it('skips empty values and keeps the all-assignees sentinel', () => {
    assert.equal(documentsHref({}), '/documents')
    assert.equal(
      documentsHref({ bucket: 'all', assignedToUserId: 'all', statusId: '' }),
      '/documents?bucket=all&assignedToUserId=all',
    )
  })

  it('keeps the Unassigned Assigned-to sentinel', () => {
    assert.equal(
      documentsHref({ bucket: 'unassigned', assignedToUserId: 'unassigned' }),
      '/documents?bucket=unassigned&assignedToUserId=unassigned',
    )
  })
})

