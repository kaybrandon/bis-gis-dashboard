import { Switch } from 'antd'

export function ShowArchivedSwitch({
  checked,
  onChange,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <label className="users-show-archived">
      <Switch size="small" checked={checked} onChange={onChange} />
      <span>Show archived</span>
    </label>
  )
}
