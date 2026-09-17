import { Form, Input } from 'antd'

export type AssignedTechnician = { name: string; isPrimary?: boolean }

export function assignedTechnicianLabel(techs: AssignedTechnician[]) {
  const names = techs.map((row) => row.name).filter(Boolean)
  return names.length > 0 ? names.join(', ') : 'Not assigned yet'
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
      label="Assigned technician"
      tooltip="Who will get this work — the organization’s primary Assigned tech when set, otherwise the Assigned tech(s). First name and last initial only."
    >
      <Input readOnly className="assigned-tech-readonly" value={assignedTechnicianLabel(techs)} disabled={disabled} aria-readonly="true" />
    </Form.Item>
  )
}
