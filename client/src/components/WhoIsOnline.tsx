import { DownOutlined, TeamOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Dropdown, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { PresenceUser } from '../api'
import { usePresence } from '../presence'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'
import { NeedHelpMark } from './NeedHelpMark'
import { PresenceChips, PresencePeople } from './PresencePeople'

const SIDER_OPEN_KEY = 'gis.whoIsOnlineOpen'

function siderOpenStorageKey(userId?: string) {
  return userId ? `${SIDER_OPEN_KEY}.${userId}` : SIDER_OPEN_KEY
}

/** Default closed (2026-09-16 density). Only an explicit "1" opens. */
function readSiderOpen(userId?: string) {
  try {
    return localStorage.getItem(siderOpenStorageKey(userId)) === '1'
  } catch {
    return false
  }
}

function writeSiderOpen(userId: string | undefined, open: boolean) {
  try {
    localStorage.setItem(siderOpenStorageKey(userId), open ? '1' : '0')
  } catch {
    /* ignore quota / private mode */
  }
}

export function PresencePopover({ enabled }: { enabled: boolean }) {
  const { items, onlineCount, error, load, needsHelpCount } = usePresence()
  const [open, setOpen] = useState(false)

  if (!enabled) return null

  return (
    <Dropdown
      trigger={['click']}
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) void load()
      }}
      popupRender={() => (
        <div className="notification-panel presence-panel">
          <div className="notification-panel-head">
            <Typography.Text strong>Who’s online</Typography.Text>
            <Typography.Text type="secondary">
              {onlineCount} online
            </Typography.Text>
          </div>
          {error ? <LoadError message={error} onRetry={() => void load()} /> : <PresencePeople items={items} />}
        </div>
      )}
    >
      <span className="app-header-presence">
        <Badge count={needsHelpCount || onlineCount} size="small" overflowCount={99} color={needsHelpCount ? '#EAB308' : undefined}>
          <Button
            type="text"
            className="app-header-icon-btn"
            icon={<TeamOutlined />}
            aria-label="Who’s online"
          />
        </Badge>
      </span>
    </Dropdown>
  )
}

export function WhoIsOnlineCard({ enabled, compact = false }: { enabled: boolean; compact?: boolean }) {
  const { items, onlineCount, error, loading, load } = usePresence()

  if (!enabled) return null

  return (
    <Card
      className="compact-card"
      loading={loading && items.length === 0}
      title={(
        <TitleWithHelp help="Staff who have a recent heartbeat. Online is the last 90 seconds; Away is up to about three minutes. The page or work item is what they last reported. Clocked in means the staff attendance clock is running. A yellow hand means they tapped Need help?">
          Who’s online
        </TitleWithHelp>
      )}
      extra={<Typography.Text type="secondary">{onlineCount} online</Typography.Text>}
    >
      {error && <LoadError message={error} onRetry={() => void load()} />}
      {!error && (compact ? <PresenceChips items={items} /> : <PresencePeople items={items} />)}
    </Card>
  )
}

function NeedHelpToggle({ compact = false }: { compact?: boolean }) {
  const { selfNeedsHelp, setNeedsHelp } = usePresence()
  const [saving, setSaving] = useState(false)

  async function toggle() {
    setSaving(true)
    try {
      await setNeedsHelp(!selfNeedsHelp)
    } finally {
      setSaving(false)
    }
  }

  return (
    <Button
      type={selfNeedsHelp ? 'primary' : 'default'}
      size="small"
      className={selfNeedsHelp ? 'need-help-toggle is-on' : 'need-help-toggle'}
      data-slot="need-help-raise-hand"
      loading={saving}
      aria-label={selfNeedsHelp ? 'Clear help' : 'Need help?'}
      onClick={(event) => {
        event.stopPropagation()
        void toggle()
      }}
    >
      {selfNeedsHelp ? <NeedHelpMark compact /> : null}
      {compact ? (selfNeedsHelp ? null : '!') : (selfNeedsHelp ? 'Clear help' : 'Need help?')}
    </Button>
  )
}

function SiderPresenceList({ items }: { items: PresenceUser[] }) {
  const { selfUserId, openHelp } = usePresence()

  if (items.length === 0) {
    return <div className="who-online-sider-empty">Nobody else is signed in right now.</div>
  }

  return (
    <ul className="who-online-sider-list">
      {items.map((row) => {
        const clickable = row.userId !== selfUserId
        return (
          <li key={row.userId}>
            <button
              type="button"
              className={clickable ? 'who-online-sider-person is-clickable' : 'who-online-sider-person'}
              data-testid="presence-sider-person"
              data-user-id={row.userId}
              disabled={!clickable}
              title={clickable ? `Message ${row.displayName}` : undefined}
              onClick={() => openHelp(row)}
            >
              <span className={`presence-dot is-${row.presenceStatus.toLowerCase()}`} />
              <span className="who-online-sider-copy">
                <span className="who-online-sider-name">
                  {row.displayName}
                  {row.needsHelp ? <NeedHelpMark compact /> : null}
                  {row.clockedIn ? ' · Clocked in' : ''}
                </span>
                <span className="who-online-sider-page">
                  {row.workItemTitle ? (
                    <Link to={`/documents/${row.workItemId}`} onClick={(event) => event.stopPropagation()}>
                      {row.workItemTitle}
                    </Link>
                  ) : (
                    row.pageName
                  )}
                </span>
              </span>
            </button>
          </li>
        )
      })}
    </ul>
  )
}

/** Left sider under Status. Collapsible, default closed, persist. Raise-hand lives here. */
export function WhoIsOnlineSider({
  enabled,
  userId,
  collapsed = false,
}: {
  enabled: boolean
  userId?: string
  collapsed?: boolean
}) {
  const { items, onlineCount, needsHelpCount, error, load } = usePresence()
  const [open, setOpen] = useState(() => readSiderOpen(userId))

  useEffect(() => {
    setOpen(readSiderOpen(userId))
  }, [userId])

  const toggle = () => {
    const next = !open
    setOpen(next)
    writeSiderOpen(userId, next)
  }

  if (!enabled) return null

  const list = error ? (
    <LoadError message={error} onRetry={() => void load()} />
  ) : (
    <SiderPresenceList items={items} />
  )

  if (collapsed) {
    return (
      <div className="who-online-sider is-collapsed" role="region" aria-label="Who’s online" data-testid="who-online-sider">
        <Dropdown
          trigger={['click']}
          placement="rightTop"
          popupRender={() => (
            <div className="notification-panel presence-panel">
              <div className="notification-panel-head">
                <Typography.Text strong>Who’s online</Typography.Text>
                <Typography.Text type="secondary">{onlineCount} online</Typography.Text>
              </div>
              {error ? <LoadError message={error} onRetry={() => void load()} /> : <PresencePeople items={items} />}
            </div>
          )}
        >
          <button type="button" className="who-online-sider-toggle" aria-label="Who’s online">
            <Badge count={needsHelpCount || onlineCount} size="small" overflowCount={99} color={needsHelpCount ? '#EAB308' : '#1890ff'}>
              <TeamOutlined />
            </Badge>
          </button>
        </Dropdown>
        <div className="who-online-sider-actions">
          <NeedHelpToggle compact />
        </div>
      </div>
    )
  }

  return (
    <div className="who-online-sider" role="region" aria-label="Who’s online" data-testid="who-online-sider">
      <button
        type="button"
        className="who-online-sider-toggle"
        aria-expanded={open}
        aria-controls="who-online-sider-body"
        onClick={toggle}
      >
        <DownOutlined className={open ? 'who-online-chevron is-open' : 'who-online-chevron'} />
        <TeamOutlined />
        <span className="who-online-sider-title">Who’s online</span>
        {needsHelpCount > 0 && <NeedHelpMark compact />}
        <span className="who-online-sider-count">{onlineCount}</span>
      </button>
      <div className="who-online-sider-actions">
        <NeedHelpToggle />
      </div>
      {open && (
        <div id="who-online-sider-body" className="who-online-sider-body">
          {list}
        </div>
      )}
    </div>
  )
}
