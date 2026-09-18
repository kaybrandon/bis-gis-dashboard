import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import { orgDisplayName, orgPickerOption } from './orgArchive.ts'

const here = dirname(fileURLToPath(import.meta.url))
const orgsPage = readFileSync(join(here, 'pages/OrganizationsPage.tsx'), 'utf8')
const dashboard = readFileSync(join(here, 'pages/DashboardPage.tsx'), 'utf8')
const upload = readFileSync(join(here, 'pages/UploadDocumentsPage.tsx'), 'utf8')
const users = readFileSync(join(here, 'pages/UsersPage.tsx'), 'utf8')

describe('org archive display', () => {
  it('keeps the live name and adds an archived suffix once', () => {
    assert.equal(orgDisplayName('Demo Client'), 'Demo Client')
    assert.equal(orgDisplayName('Demo Client', false), 'Demo Client')
    assert.equal(orgDisplayName('Demo Client', true), 'Demo Client (archived)')
    assert.equal(orgDisplayName('Demo Client (archived)', true), 'Demo Client (archived)')
  })

  it('grays archived picker options', () => {
    const live = orgPickerOption({ id: '1', name: 'Demo Client' })
    assert.equal(live.label, 'Demo Client')
    assert.equal(live.className, undefined)
    const archived = orgPickerOption({ id: '2', name: 'Old Client', isArchived: true })
    assert.equal(archived.label, 'Old Client (archived)')
    assert.equal(archived.className, 'org-option-archived')
  })
})

describe('GIS-UI-09 archive organizations UI', () => {
  it('Organizations grid has Archive, Show archived, and gray archived rows', () => {
    assert.match(orgsPage, /Show archived/)
    assert.match(orgsPage, /confirmArchive/)
    assert.match(orgsPage, /archiveOrg/)
    assert.match(orgsPage, /restoreOrg/)
    assert.match(orgsPage, /orgs-row-archived/)
    assert.doesNotMatch(orgsPage, /api\.deleteOrg|method: 'DELETE'/)
  })

  it('Dashboard and Upload pickers have a Show archived path', () => {
    assert.match(dashboard, /Show archived/)
    assert.match(dashboard, /api\.organizations\(showArchived\)/)
    assert.match(upload, /Show archived/)
    assert.match(upload, /api\.organizations\(showArchived\)/)
  })

  it('does not break Users archive', () => {
    assert.match(users, /archiveUser/)
    assert.match(users, /restoreUser/)
    assert.match(users, /users-row-archived/)
    assert.match(users, /Show archived/)
  })
})
