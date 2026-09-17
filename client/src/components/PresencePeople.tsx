import { ClockCircleOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Empty, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { PresenceUser } from '../api'
import { authorizedBlob } from '../api'
import { usePresence } from '../presence'
import { NeedHelpMark } from './NeedHelpMark'

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

function openPeer(row: PresenceUser, selfUserId: string | undefined, openHelp: (peer: PresenceUser) => void) {
  if (row.userId === selfUserId) return
  openHelp(row)
}

export function PresencePeople({
  items,
  empty = 'Nobody else is signed in right now.',
}: {
  items: PresenceUser[]
  empty?: string
}) {
  const { selfUserId, openHelp } = usePresence()
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
        const clickable = row.userId !== selfUserId
        return (
          <div
            key={row.userId}
            className={clickable ? 'presence-row is-clickable' : 'presence-row'}
            role={clickable ? 'button' : undefined}
            tabIndex={clickable ? 0 : undefined}
            onClick={() => openPeer(row, selfUserId, openHelp)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault()
                openPeer(row, selfUserId, openHelp)
              }
            }}
          >
            <PresenceAvatar user={row} />
            <div className="presence-row-body">
              <div className="presence-row-name">
                <Typography.Text strong ellipsis>
                  {row.displayName}
                </Typography.Text>
                {row.needsHelp && <NeedHelpMark />}
                {row.unreadHelpCount > 0 && (
                  <Tag color="gold" className="presence-status">{row.unreadHelpCount}</Tag>
                )}
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

export function PresenceChips({
  items,
  dark = false,
}: {
  items: PresenceUser[]
  dark?: boolean
}) {
  const { selfUserId, openHelp } = usePresence()
  if (items.length === 0) {
    return <Typography.Text type="secondary">Nobody else is signed in right now.</Typography.Text>
  }

  return (
    <Space size={[4, 4]} wrap>
      {items.map((row) => {
        const clickable = row.userId !== selfUserId
        return (
          <Tag
            key={row.userId}
            className={dark ? 'presence-chip is-dark' : 'presence-chip'}
            data-needs-help={row.needsHelp ? 'true' : 'false'}
            onClick={clickable ? () => openHelp(row) : undefined}
            style={clickable ? { cursor: 'pointer' } : undefined}
          >
            <span className={`presence-dot is-${row.presenceStatus.toLowerCase()}`} />
            {row.displayName}
            {row.needsHelp ? <NeedHelpMark compact /> : null}
            {row.unreadHelpCount > 0 ? ` · ${row.unreadHelpCount}` : ''}
            {row.clockedIn ? ' · Clocked in' : ''}
            {row.workItemTitle
              ? (
                  <>
                    {' · '}
                    <Link to={`/documents/${row.workItemId}`} onClick={(event) => event.stopPropagation()}>
                      {row.workItemTitle}
                    </Link>
                  </>
                )
              : ` · ${row.pageName}`}
          </Tag>
        )
      })}
    </Space>
  )
}

export function WorkPresenceMarks({
  workItemId,
  assignedToUserId,
}: {
  workItemId: string
  assignedToUserId?: string | null
}) {
  const { enabled, items, openHelp } = usePresence()
  if (!enabled) return null

  const assigned = assignedToUserId ? items.find((row) => row.userId === assignedToUserId) : undefined
  const onItem = items.filter((row) => row.workItemId === workItemId && row.userId !== assignedToUserId)
  if (!assigned?.needsHelp && onItem.length === 0) return null

  return (
    <span className="work-presence-marks">
      {assigned?.needsHelp && (
        <button
          type="button"
          className="work-presence-assigned"
          onClick={(event) => {
            event.stopPropagation()
            if (assigned.userId) openHelp(assigned)
          }}
        >
          <NeedHelpMark compact />
        </button>
      )}
      {onItem.map((row) => (
        <Tag
          key={row.userId}
          className="presence-chip work-presence-chip"
          data-needs-help={row.needsHelp ? 'true' : 'false'}
          onClick={(event) => {
            event.stopPropagation()
            openHelp(row)
          }}
        >
          <span className={`presence-dot is-${row.presenceStatus.toLowerCase()}`} />
          {row.displayName}
          {row.needsHelp ? <NeedHelpMark compact /> : null}
        </Tag>
      ))}
    </span>
  )
}
