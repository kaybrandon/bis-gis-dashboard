import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { canSeeDashboardAssignee, roleCanSeeDashboardAssignee, roleHasAllOrganizations, roleRequiresOrganizationAssignment } from './roles.ts'

describe('QC03 organization assignment by role', () => {
  it('requires an Organizations picker only for Viewer and Uploader', () => {
    assert.equal(roleRequiresOrganizationAssignment('Viewer'), true)
    assert.equal(roleRequiresOrganizationAssignment('Uploader'), true)
    assert.equal(roleRequiresOrganizationAssignment('Editor'), false)
    assert.equal(roleRequiresOrganizationAssignment('Administrator'), false)
    assert.equal(roleRequiresOrganizationAssignment('GlobalAdministrator'), false)
    assert.equal(roleRequiresOrganizationAssignment(undefined), false)
  })

  it('treats staff and Global Administrator as all-organization roles', () => {
    assert.equal(roleHasAllOrganizations('Editor'), true)
    assert.equal(roleHasAllOrganizations('Administrator'), true)
    assert.equal(roleHasAllOrganizations('GlobalAdministrator'), true)
    assert.equal(roleHasAllOrganizations('Viewer'), false)
    assert.equal(roleHasAllOrganizations('Uploader'), false)
  })
})

describe('QC08 dashboard assignee visibility', () => {
  it('hides Dashboard Assignee for Viewer and Uploader', () => {
    assert.equal(roleCanSeeDashboardAssignee('Viewer'), false)
    assert.equal(roleCanSeeDashboardAssignee('Uploader'), false)
    assert.equal(canSeeDashboardAssignee({ role: 'Viewer' }), false)
    assert.equal(canSeeDashboardAssignee({ role: 'Uploader' }), false)
    assert.equal(canSeeDashboardAssignee({ role: 'Viewer', canSeeDashboardAssignee: false }), false)
    assert.equal(canSeeDashboardAssignee({ role: 'Uploader', canSeeDashboardAssignee: false }), false)
  })

  it('keeps Dashboard Assignee for Editor and Administrators', () => {
    assert.equal(roleCanSeeDashboardAssignee('Editor'), true)
    assert.equal(roleCanSeeDashboardAssignee('Administrator'), true)
    assert.equal(roleCanSeeDashboardAssignee('GlobalAdministrator'), true)
    assert.equal(canSeeDashboardAssignee({ role: 'Editor' }), true)
    assert.equal(canSeeDashboardAssignee({ role: 'Administrator', canSeeDashboardAssignee: true }), true)
    assert.equal(canSeeDashboardAssignee({ role: 'GlobalAdministrator' }), true)
    assert.equal(canSeeDashboardAssignee(undefined), false)
  })
})
