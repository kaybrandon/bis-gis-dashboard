import { Form, Input } from 'antd'
import { ASSIGNED_TECHNICIAN_LABEL, ASSIGNED_TECHNICIAN_UPLOAD_HELP, UNASSIGNED_LABEL } from '../assignmentLabels'

export type AssignedTechnician = { name: string; isPrimary?: boolean }

export function assignedTechnicianLabel(techs: AssignedTechnician[]) {
  const names = techs.map((row) => row.name).filter(Boolean)
  return names.length > 0 ? names.join(', ') : UNASSIGNED_LABEL
}

export function AssignedTechnicianField({
  techs,
  disabled,
}: {
  techs: AssignedTechnician[]
  disabled?: boolean
}) {
  return (
    <Form.Item
      label={ASSIGNED_TECHNICIAN_LABEL}
      tooltip={ASSIGNED_TECHNICIAN_UPLOAD_HELP}
    >
      <Input readOnly className="assigned-tech-readonly" value={assignedTechnicianLabel(techs)} disabled={disabled} aria-readonly="true" />
    </Form.Item>
  )
}
