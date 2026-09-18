import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { documentsHref } from './documentsHref.ts'
import {
  ALL_ASSIGNEES,
  QUEUE_SORT_BY,
  QUEUE_SORT_DIR,
  UNASSIGNED,
  activeDocumentsQuery,
  assigneeHrefValue,
  assignmentApiParams,
  defaultDocumentsBucket,
  isStaffQueueRole,
  isUnassignedFilter,
  resolveAssigneeFilter,
} from './staffQueue.ts'

const editor = { id: 'editor-1', role: 'Editor' as const, canSeeDashboardAssignee: true }
const viewer = { id: 'viewer-1', role: 'Viewer' as const, canSeeDashboardAssignee: false }
const uploader = { id: 'uploader-1', role: 'Uploader' as const, canSeeDashboardAssignee: false }

describe('CR05 staff queue defaults', () => {
  it('treats Editor and Administrators as staff queue roles', () => {
    assert.equal(isStaffQueueRole(editor), true)
    assert.equal(isStaffQueueRole({ role: 'Administrator', canSeeDashboardAssignee: true }), true)
    assert.equal(isStaffQueueRole({ role: 'GlobalAdministrator' }), true)
    assert.equal(isStaffQueueRole(viewer), false)
    assert.equal(isStaffQueueRole(uploader), false)
  })

  it('defaults staff assignee to the signed-in user', () => {
    assert.equal(resolveAssigneeFilter(undefined, editor), 'editor-1')
    assert.equal(resolveAssigneeFilter(null, editor), 'editor-1')
    assert.equal(resolveAssigneeFilter('', editor), 'editor-1')
  })

  it('keeps an explicit staff assignee and treats all as unscoped', () => {
    assert.equal(resolveAssigneeFilter('other-9', editor), 'other-9')
    assert.equal(resolveAssigneeFilter(ALL_ASSIGNEES, editor), undefined)
  })

  it('keeps Unassigned as work-item Assigned to with no person', () => {
    assert.equal(resolveAssigneeFilter(UNASSIGNED, editor), UNASSIGNED)
    assert.equal(assigneeHrefValue(UNASSIGNED, editor), UNASSIGNED)
    assert.equal(isUnassignedFilter(UNASSIGNED), true)
    assert.deepEqual(assignmentApiParams(UNASSIGNED), { unassignedOnly: true })
    assert.deepEqual(assignmentApiParams('editor-1'), { assignedToUserId: 'editor-1' })
    assert.deepEqual(assignmentApiParams(undefined), {})
  })

  it('never applies an assignee filter for Viewer or Uploader', () => {
    assert.equal(resolveAssigneeFilter('other-9', viewer), undefined)
    assert.equal(resolveAssigneeFilter(ALL_ASSIGNEES, uploader), undefined)
    assert.equal(assigneeHrefValue('other-9', viewer), undefined)
    assert.equal(assigneeHrefValue(undefined, uploader), undefined)
  })

  it('defaults staff Manage Documents to all items (assigned-to-me is the assignee filter)', () => {
    assert.equal(defaultDocumentsBucket(null, '', editor), 'all')
    assert.equal(defaultDocumentsBucket(undefined, '', editor), 'all')
  })

  it('lets a saved preset or URL bucket override the staff default', () => {
    assert.equal(defaultDocumentsBucket(null, 'priority', editor), 'priority')
    assert.equal(defaultDocumentsBucket('mine', '', editor), 'mine')
    assert.equal(defaultDocumentsBucket(null, 'unassigned', editor), 'unassigned')
  })

  it('keeps Viewer default pending when there is no other filter', () => {
    assert.equal(defaultDocumentsBucket(null, '', viewer, false), 'pending')
    assert.equal(defaultDocumentsBucket(null, '', viewer, true), 'all')
  })
})

describe('CR03 Active destination href', () => {
  it('opens the signed-in Active queue without a date window', () => {
    const query = activeDocumentsQuery({
      organizationId: 'org-1',
      assignedToUserId: 'editor-1',
      activeStatusId: 'status-active',
      user: editor,
    })
    assert.deepEqual(query, {
      bucket: 'all',
      organizationId: 'org-1',
      statusId: 'status-active',
      assignedToUserId: 'editor-1',
    })
    assert.equal(
      documentsHref(query),
      '/documents?bucket=all&organizationId=org-1&statusId=status-active&assignedToUserId=editor-1',
    )
  })

  it('passes all when staff cleared Assignee so the list does not snap back to me', () => {
    const query = activeDocumentsQuery({
      activeStatusId: 'status-active',
      user: editor,
    })
    assert.equal(query.assignedToUserId, ALL_ASSIGNEES)
    assert.equal(documentsHref(query), `/documents?bucket=all&statusId=status-active&assignedToUserId=${ALL_ASSIGNEES}`)
  })

  it('omits Assignee for Viewer', () => {
    const query = activeDocumentsQuery({
      assignedToUserId: 'editor-1',
      activeStatusId: 'status-active',
      user: viewer,
    })
    assert.equal(query.assignedToUserId, undefined)
    assert.equal(documentsHref(query), '/documents?bucket=all&statusId=status-active')
  })

  it('uses queue sort constants for the default order', () => {
    assert.equal(QUEUE_SORT_BY, 'queue')
    assert.equal(QUEUE_SORT_DIR, 'asc')
  })
})
