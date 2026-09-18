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
  queuePresetForAssigneeDefault,
  isGlobalAdministratorRole,
  isStaffQueueRole,
  isUnassignedFilter,
  resolveAssigneeFilter,
  shouldDefaultAssigneeToAll,
  shouldProbeMyQueue,
} from './staffQueue.ts'

const editor = { id: 'editor-1', role: 'Editor' as const, canSeeDashboardAssignee: true }
const admin = { id: 'admin-1', role: 'Administrator' as const, canSeeDashboardAssignee: true }
const globalAdmin = { id: 'ga-1', role: 'GlobalAdministrator' as const, canSeeDashboardAssignee: true }
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

  it('defaults staff with assigned work to the signed-in user', () => {
    assert.equal(resolveAssigneeFilter(undefined, editor), 'editor-1')
    assert.equal(resolveAssigneeFilter(null, editor), 'editor-1')
    assert.equal(resolveAssigneeFilter('', editor), 'editor-1')
    assert.equal(resolveAssigneeFilter(undefined, editor, 4), 'editor-1')
    assert.equal(resolveAssigneeFilter(undefined, admin, 1), 'admin-1')
  })

  it('defaults Global Admin and a zero my-queue to Assignee = All', () => {
    assert.equal(isGlobalAdministratorRole(globalAdmin), true)
    assert.equal(isGlobalAdministratorRole(editor), false)
    assert.equal(shouldDefaultAssigneeToAll(globalAdmin), true)
    assert.equal(shouldDefaultAssigneeToAll(editor, 0), true)
    assert.equal(shouldDefaultAssigneeToAll(admin, 0), true)
    assert.equal(shouldDefaultAssigneeToAll(editor, 2), false)
    assert.equal(shouldDefaultAssigneeToAll(viewer, 0), false)
    assert.equal(resolveAssigneeFilter(undefined, globalAdmin), undefined)
    assert.equal(resolveAssigneeFilter(null, globalAdmin), undefined)
    assert.equal(resolveAssigneeFilter('', globalAdmin), undefined)
    assert.equal(resolveAssigneeFilter(undefined, editor, 0), undefined)
    assert.equal(resolveAssigneeFilter(undefined, admin, 0), undefined)
  })

  it('probes my-queue only for non-GA staff without an explicit assignee', () => {
    assert.equal(shouldProbeMyQueue(undefined, editor), true)
    assert.equal(shouldProbeMyQueue(null, admin), true)
    assert.equal(shouldProbeMyQueue('', editor), true)
    assert.equal(shouldProbeMyQueue(undefined, globalAdmin), false)
    assert.equal(shouldProbeMyQueue(undefined, viewer), false)
    assert.equal(shouldProbeMyQueue(undefined, uploader), false)
    assert.equal(shouldProbeMyQueue(ALL_ASSIGNEES, editor), false)
    assert.equal(shouldProbeMyQueue(UNASSIGNED, editor), false)
    assert.equal(shouldProbeMyQueue('other-9', editor), false)
  })

  it('keeps an explicit staff assignee and treats all as unscoped', () => {
    assert.equal(resolveAssigneeFilter('other-9', editor), 'other-9')
    assert.equal(resolveAssigneeFilter(ALL_ASSIGNEES, editor), undefined)
    assert.equal(resolveAssigneeFilter('other-9', globalAdmin), 'other-9')
    assert.equal(resolveAssigneeFilter(ALL_ASSIGNEES, globalAdmin), undefined)
    assert.equal(resolveAssigneeFilter('other-9', editor, 0), 'other-9')
  })

  it('keeps Unassigned as work-item Assigned to with no person', () => {
    assert.equal(resolveAssigneeFilter(UNASSIGNED, editor), UNASSIGNED)
    assert.equal(resolveAssigneeFilter(UNASSIGNED, globalAdmin), UNASSIGNED)
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
    assert.equal(queuePresetForAssigneeDefault('mine', undefined, undefined, globalAdmin), '')
    assert.equal(queuePresetForAssigneeDefault('mine', undefined, undefined, editor), '')
    assert.equal(queuePresetForAssigneeDefault('mine', undefined, 'editor-1', editor), 'mine')
    assert.equal(queuePresetForAssigneeDefault('priority', undefined, undefined, globalAdmin), 'priority')
    assert.equal(queuePresetForAssigneeDefault('mine', ALL_ASSIGNEES, undefined, editor), 'mine')
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
