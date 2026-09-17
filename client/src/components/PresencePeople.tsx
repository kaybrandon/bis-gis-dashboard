import { ClockCircleOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Empty, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { PresenceUser } from '../api'
import { authorizedBlob } from '../api'

function PresenceAvatar({ user }: { user: PresenceUser }) {
  const [url, setUrl] = useState<string | null>(null)

  useEffect(() => {
    if (!user.hasAvatar) {
      setUrl(null)
      return
    }
    let revoked = false
    let objectUrl: string | null = null
    authorizedBlob(`/api/presence/${user.userId}/avatar`)
      .then((blob) => {
        if (revoked) return
        objectUrl = URL.createObjectURL(blob)
        setUrl(objectUrl)
      })
      .catch(() => {
        if (!revoked) setUrl(null)
      })
    return () => {
      revoked = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [user.userId, user.hasAvatar])

  return <Avatar size={28} src={url || undefined} icon={!url ? <UserOutlined /> : undefined} />
}

export function PresencePeople({
  items,
  empty = 'Nobody else is signed in right now.',
}: {
  items: PresenceUser[]
  empty?: string
}) {
  if (items.length === 0) {
    return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={empty} />
  }

  return (
    <div className="presence-list">
      {items.map((row) => {
        const location = row.workItemTitle
          ? (
              <Link to={`/documents/${row.workItemId}`} className="presence-work-link">
                {row.workItemTitle}
              </Link>
            )
          : row.pageName
        return (
          <div key={row.userId} className="presence-row">
            <PresenceAvatar user={row} />
            <div className="presence-row-body">
              <div className="presence-row-name">
                <Typography.Text strong ellipsis>
                  {row.displayName}
                </Typography.Text>
                <Tag color={row.presenceStatus === 'Online' ? 'success' : 'gold'} className="presence-status">
                  {row.presenceStatus}
                </Tag>
                {row.clockedIn && (
                  <Tag color="blue" icon={<ClockCircleOutlined />} className="presence-status">
                    Clocked in
                  </Tag>
                )}
              </div>
              <Typography.Text type="secondary" className="presence-location">
                {location}
              </Typography.Text>
            </div>
          </div>
        )
      })}
    </div>
  )
}

export function PresenceChips({ items }: { items: PresenceUser[] }) {
  if (items.length === 0) {
    return <Typography.Text type="secondary">Nobody else is signed in right now.</Typography.Text>
  }

  return (
    <Space size={[4, 4]} wrap>
      {items.map((row) => (
        <Tag key={row.userId} className="presence-chip">
          <span className={`presence-dot is-${row.presenceStatus.toLowerCase()}`} />
          {row.displayName}
          {row.clockedIn ? ' · Clocked in' : ''}
          {row.workItemTitle
            ? (
                <>
                  {' · '}
                  <Link to={`/documents/${row.workItemId}`}>{row.workItemTitle}</Link>
                </>
              )
            : ` · ${row.pageName}`}
        </Tag>
      ))}
    </Space>
  )
}
