import { BellOutlined } from '@ant-design/icons'
import { App, Badge, Button, Dropdown, Empty, Typography } from 'antd'
import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { AppNotification } from '../api'
import { api } from '../api'

export function NotificationBell({
  open,
  onOpenChange,
}: {
  open?: boolean
  onOpenChange?: (open: boolean) => void
}) {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [unread, setUnread] = useState(0)
  const [items, setItems] = useState<AppNotification[]>([])
  const seen = useRef(new Set<string>())
  const primed = useRef(false)

  const load = useCallback(async (announce: boolean) => {
    try {
      const result = await api.notifications()
      setItems(result.items)
      setUnread(result.unreadCount)
      if (announce) {
        result.items
          .filter((row) => row.unread && !seen.current.has(row.id))
          .forEach((row) => message.info({ content: row.body, duration: 4 }))
      }
      result.items.forEach((row) => seen.current.add(row.id))
      primed.current = true
    } catch {
      /* keep last */
    }
  }, [message])

  useEffect(() => {
    void load(false)
    const id = window.setInterval(() => void load(primed.current), 20000)
    return () => window.clearInterval(id)
  }, [load])

  return (
    <Dropdown
      trigger={['click']}
      open={open}
      onOpenChange={onOpenChange}
      popupRender={() => (
        <div className="notification-panel">
          <div className="notification-panel-head">
            <Typography.Text strong>Notifications</Typography.Text>
            {unread > 0 && (
              <Button type="link" size="small" onClick={() => void api.readAllNotifications().then(() => load(false))}>
                Mark all read
              </Button>
            )}
          </div>
          {items.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No notifications." />
          ) : (
            items.map((item) => (
              <button
                key={item.id}
                type="button"
                className={`notification-item${item.unread ? ' is-unread' : ''}`}
                onClick={() => {
                  onOpenChange?.(false)
                  void api.readNotification(item.id).then(() => load(false))
                  if (item.workItemId) navigate(`/documents/${item.workItemId}`)
                }}
              >
                <Typography.Text strong>{item.title}</Typography.Text>
                <Typography.Text type="secondary">{item.body}</Typography.Text>
              </button>
            ))
          )}
        </div>
      )}
    >
      <Badge count={unread} size="small">
        <Button type="text" className="app-header-icon-btn" icon={<BellOutlined />} aria-label="Notifications" />
      </Badge>
    </Dropdown>
  )
}
