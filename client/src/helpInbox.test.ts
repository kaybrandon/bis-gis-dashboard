import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { formatHelpInboxTime, previewHelpInbox } from './helpInbox.ts'

describe('help inbox copy', () => {
  it('shows Chicago clock time for the same day and a short date otherwise', () => {
    const now = new Date('2026-09-18T15:10:00-05:00')
    assert.equal(formatHelpInboxTime('2026-09-18T15:51:00-05:00', now), '3:51 PM')
    assert.equal(formatHelpInboxTime('2026-09-17T19:51:00-05:00', now), 'Sep 17')
  })

  it('keeps a short preview and ellipsizes long bodies', () => {
    assert.equal(previewHelpInbox('Need help?'), 'Need help?')
    assert.equal(previewHelpInbox('  Can you   take a look?  '), 'Can you take a look?')
    const long = 'Northridge plat needs a bearing check on the west line before QC this afternoon'
    assert.equal(previewHelpInbox(long, 24), 'Northridge plat needs a…')
  })
})
