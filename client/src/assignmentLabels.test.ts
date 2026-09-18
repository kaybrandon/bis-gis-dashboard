import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  ASSIGNED_TECHNICIAN_HELP,
  ASSIGNED_TECHNICIAN_LABEL,
  ASSIGNED_TECHNICIAN_UPLOAD_HELP,
  ASSIGNED_TECHS_HELP,
  ASSIGNED_TECHS_LABEL,
  ASSIGNED_TO_FILTER_HELP,
  ASSIGNED_TO_HELP,
  ASSIGNED_TO_LABEL,
  ASSIGNED_TO_UPLOAD_HELP,
  PRIMARY_ASSIGNED_TECH_HELP,
  PRIMARY_ASSIGNED_TECH_LABEL,
  UNASSIGNED_LABEL,
} from './assignmentLabels.ts'

describe('CR09 assignment field labels', () => {
  it('keeps Assigned technician and Assigned to as distinct labels', () => {
    assert.equal(ASSIGNED_TECHNICIAN_LABEL, 'Assigned technician')
    assert.equal(ASSIGNED_TECHS_LABEL, 'Assigned tech(s)')
    assert.equal(PRIMARY_ASSIGNED_TECH_LABEL, 'Primary assigned tech')
    assert.equal(ASSIGNED_TO_LABEL, 'Assigned to')
    assert.equal(UNASSIGNED_LABEL, 'Unassigned')
    assert.notEqual(ASSIGNED_TECHNICIAN_LABEL, ASSIGNED_TO_LABEL)
    assert.notEqual(ASSIGNED_TECHS_LABEL, ASSIGNED_TO_LABEL)
  })

  it('explains org default vs work-item assignee in help text', () => {
    for (const help of [ASSIGNED_TECHNICIAN_HELP, ASSIGNED_TECHS_HELP, ASSIGNED_TECHNICIAN_UPLOAD_HELP]) {
      assert.match(help, /Organization default/)
      assert.match(help, /Assigned to/)
      assert.doesNotMatch(help, /Who will get this work/)
    }
    assert.match(ASSIGNED_TECHNICIAN_HELP, /does not reassign existing/)
    assert.match(ASSIGNED_TECHS_HELP, /does not reassign existing/)
    assert.match(PRIMARY_ASSIGNED_TECH_HELP, /existing documents keep their Assigned to/)
    for (const help of [ASSIGNED_TO_HELP, ASSIGNED_TO_UPLOAD_HELP, ASSIGNED_TO_FILTER_HELP]) {
      assert.match(help, /Work-item assignee|work-item Assigned to/)
      assert.match(help, /not the org/)
    }
  })
})
