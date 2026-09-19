import { QuestionCircleOutlined } from '@ant-design/icons'
import { Alert, Button, Modal } from 'antd'
import { useState, type ReactNode } from 'react'
import {
  AI_FILL_HELP_BULLETS,
  AI_FILL_HELP_TITLE,
  dismissAiFillHelpTip,
  helpEmphasisParts,
  isAiFillHelpTipDismissed,
} from '../aiFillHelp'

function HelpEmphasis({ text }: { text: string }): ReactNode {
  return helpEmphasisParts(text).map((part, index) => (
    part.bold ? <strong key={index}>{part.text}</strong> : <span key={index}>{part.text}</span>
  ))
}

export function AiFillHelpOpenButton({ onOpen }: { onOpen: () => void }) {
  return (
    <Button
      size="small"
      icon={<QuestionCircleOutlined />}
      onClick={onOpen}
      aria-label={AI_FILL_HELP_TITLE}
      data-slot="ai-fill-help-open"
    >
      {AI_FILL_HELP_TITLE}
    </Button>
  )
}

export function AiFillHelpFirstReviewTip({ onOpen }: { onOpen: () => void }) {
  const [visible, setVisible] = useState(() => !isAiFillHelpTipDismissed())
  if (!visible) return null
  return (
    <Alert
      className="ai-fill-help-tip"
      type="info"
      showIcon
      closable
      data-slot="ai-fill-help-tip"
      message={AI_FILL_HELP_TITLE}
      description={(
        <>
          <div>
            <HelpEmphasis text={AI_FILL_HELP_BULLETS[0]} />
          </div>
          <Button type="link" size="small" style={{ paddingInline: 0 }} onClick={onOpen}>
            {AI_FILL_HELP_TITLE}
          </Button>
        </>
      )}
      onClose={() => {
        dismissAiFillHelpTip()
        setVisible(false)
      }}
    />
  )
}

export function AiFillHelpModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  return (
    <Modal
      title={AI_FILL_HELP_TITLE}
      open={open}
      onCancel={onClose}
      footer={<Button type="primary" onClick={onClose}>Close</Button>}
      width={560}
      destroyOnHidden
      data-slot="ai-fill-help-modal"
    >
      <ul className="ai-fill-help-list">
        {AI_FILL_HELP_BULLETS.map((bullet) => (
          <li key={bullet}>
            <HelpEmphasis text={bullet} />
          </li>
        ))}
      </ul>
    </Modal>
  )
}

export function AiFillHelpScreen({
  open,
  onOpen,
  onClose,
  showTip = true,
}: {
  open: boolean
  onOpen: () => void
  onClose: () => void
  showTip?: boolean
}) {
  return (
    <>
      {showTip && <AiFillHelpFirstReviewTip onOpen={onOpen} />}
      <AiFillHelpModal open={open} onClose={onClose} />
    </>
  )
}
