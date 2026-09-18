import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { documentsHref } from './documentsHref.ts'
import {
  DOCUMENTS_BY_CAD_HELP,
  DOCUMENTS_BY_CAD_TITLE,
  DOCUMENTS_BY_TECHNICIAN_HELP,
  DOCUMENTS_BY_TECHNICIAN_TITLE,
  UNASSIGNED_VOLUME_ID,
  documentsByCadQuery,
  documentsByTechnicianQuery,
  isUnassignedVolumeId,
  technicianVolumeFilterId,
} from './dashboardVolume.ts'
import { UNASSIGNED } from './staffQueue.ts'

const filters = {
  organizationId: 'org-1',
  statusId: 'status-1',
  assignedToUserId: 'all',
  from: '2026-07-01T00:00:00.000Z',
  to: '2026-09-18T23:59:59.999Z',
}

describe('CR10 document volume charts', () => {
  it('names count charts separately from Hours charts', () => {
    assert.equal(DOCUMENTS_BY_CAD_TITLE, 'Documents by CAD')
    assert.equal(DOCUMENTS_BY_TECHNICIAN_TITLE, 'Documents by technician')
    assert.match(DOCUMENTS_BY_CAD_HELP, /Organization \(client\) name/)
    assert.match(DOCUMENTS_BY_CAD_HELP, /uploaded/)
    assert.match(DOCUMENTS_BY_TECHNICIAN_HELP, /Assigned to/)
    assert.match(DOCUMENTS_BY_TECHNICIAN_HELP, /Unassigned/)
    assert.doesNotMatch(DOCUMENTS_BY_CAD_TITLE, /Hours/)
    assert.doesNotMatch(DOCUMENTS_BY_TECHNICIAN_TITLE, /Hours/)
  })

  it('maps the Unassigned volume sentinel to the Assigned to filter', () => {
    assert.equal(isUnassignedVolumeId(UNASSIGNED_VOLUME_ID), true)
    assert.equal(isUnassignedVolumeId(''), true)
    assert.equal(technicianVolumeFilterId(UNASSIGNED_VOLUME_ID), UNASSIGNED)
    assert.equal(technicianVolumeFilterId('dddddddd-0000-0000-0000-000000000002'), 'dddddddd-0000-0000-0000-000000000002')
  })

  it('CAD click keeps filters and uses uploaded/created dates', () => {
    const query = documentsByCadQuery(filters, 'aaaaaaaa-0000-0000-0000-000000000002')
    assert.equal(query.organizationId, 'aaaaaaaa-0000-0000-0000-000000000002')
    assert.equal(query.statusId, 'status-1')
    assert.equal(query.assignedToUserId, 'all')
    assert.equal(query.uploadedFrom, filters.from)
    assert.equal(query.uploadedTo, filters.to)
    assert.equal(query.bucket, 'all')
    assert.equal(
      documentsHref(query),
      '/documents?bucket=all&organizationId=aaaaaaaa-0000-0000-0000-000000000002&statusId=status-1&assignedToUserId=all&uploadedFrom=2026-07-01T00%3A00%3A00.000Z&uploadedTo=2026-09-18T23%3A59%3A59.999Z',
    )
  })

  it('technician click including Unassigned filters Manage Documents', () => {
    const assigned = documentsByTechnicianQuery(filters, 'dddddddd-0000-0000-0000-000000000002')
    assert.equal(assigned.assignedToUserId, 'dddddddd-0000-0000-0000-000000000002')
    assert.equal(assigned.organizationId, 'org-1')
    assert.equal(assigned.uploadedFrom, filters.from)

    const unassigned = documentsByTechnicianQuery(filters, UNASSIGNED_VOLUME_ID)
    assert.equal(unassigned.assignedToUserId, UNASSIGNED)
    assert.equal(documentsHref(unassigned).includes('assignedToUserId=unassigned'), true)
  })
})
