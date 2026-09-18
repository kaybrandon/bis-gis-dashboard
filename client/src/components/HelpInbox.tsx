import { MailOutlined } from '@ant-design/icons'
import { Badge, Button, Dropdown, Empty, Typography } from 'antd'
import { useState } from 'react'
import type { HelpInboxThread } from '../api'
import { formatHelpInboxTime } from '../helpInbox'
import { usePresence } from '../presence'
import { LoadError } from './LoadError'

export function HelpInbox({ enabled }: { enabled: boolean }) {
  const { inboxItems, inboxUnreadCount, inboxError, loadInbox, openHelp } = usePresence()
  const [open, setOpen] = useState(false)

  if (!enabled) return null

  return (
    <Dropdown
      trigger={['click']}
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) void loadInbox()
      }}
      popupRender={() => (
        <div className="notification-panel help-inbox-panel" data-testid="help-inbox-panel">
          <div className="notification-panel-head">
            <Typography.Text strong>Need help</Typography.Text>
            <Typography.Text type="secondary">
              {inboxUnreadCount > 0 ? `${inboxUnreadCount} waiting` : 'Waiting threads'}
            </Typography.Text>
          </div>
          {inboxError ? (
            <LoadError message={inboxError} onRetry={() => void loadInbox()} />
          ) : inboxItems.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No waiting Need-help messages." />
          ) : (
            inboxItems.map((item) => (
              <InboxRow
                key={item.withUserId}
                item={item}
                onOpen={() => {
                  setOpen(false)
                  openHelp({
                    userId: item.withUserId,
                    displayName: item.withDisplayName,
                    presenceStatus: item.presenceStatus,
                  })
                }}
              />
            ))
          )}
        </div>
      )}
    >
      <span className="app-header-presence">
        <Badge
          count={inboxUnreadCount}
          size="small"
          overflowCount={99}
          color={inboxUnreadCount ? '#EAB308' : undefined}
        >
          <Button
            type="text"
            className="app-header-icon-btn"
            icon={<MailOutlined />}
            aria-label={inboxUnreadCount > 0 ? `Need-help inbox, ${inboxUnreadCount} unread` : 'Need-help inbox'}
            data-testid="help-inbox-envelope"
          />
        </Badge>
      </span>
    </Dropdown>
  )
}

function InboxRow({ item, onOpen }: { item: HelpInboxThread; onOpen: () => void }) {
  return (
    <button
      type="button"
      className={`notification-item help-inbox-item${item.unread ? ' is-unread' : ''}`}
      data-testid="help-inbox-thread"
      data-user-id={item.withUserId}
      data-unread={item.unread ? 'true' : 'false'}
      onClick={onOpen}
    >
      <span className="help-inbox-who">
        <Typography.Text strong ellipsis>
          {item.withDisplayName}
        </Typography.Text>
        {item.unread ? <span className="help-inbox-unread" aria-label="Unread" /> : null}
      </span>
      <span className="help-inbox-meta">
        <Typography.Text type="secondary" ellipsis className="help-inbox-preview">
          {item.preview || 'Need help'}
        </Typography.Text>
        <Typography.Text type="secondary" className="help-inbox-time">
          {formatHelpInboxTime(item.lastAt)}
        </Typography.Text>
      </span>
    </button>
  )
}
