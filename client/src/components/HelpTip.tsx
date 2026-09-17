import { QuestionCircleOutlined } from '@ant-design/icons'
import { Tooltip } from 'antd'
import type { ReactNode } from 'react'

export function HelpTip({ title }: { title: ReactNode }) {
  return (
    <Tooltip title={title}>
      <QuestionCircleOutlined className="help-tip" aria-label="Help" />
    </Tooltip>
  )
}

export function TitleWithHelp({ children, help }: { children: ReactNode; help: ReactNode }) {
  return (
    <span className="title-with-help">
      {children}
      <HelpTip title={help} />
    </span>
  )
}
