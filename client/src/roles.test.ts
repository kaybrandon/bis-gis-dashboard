import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import { roleHasAllOrganizations, roleRequiresOrganizationAssignment } from './roles.ts'

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
