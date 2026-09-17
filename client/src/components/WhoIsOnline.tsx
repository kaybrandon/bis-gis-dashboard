import { DownOutlined, TeamOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Dropdown, Typography } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import type { PresenceUser } from '../api'
import { api } from '../api'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'
import { PresenceChips, PresencePeople } from './PresencePeople'

const POLL_MS = 20_000
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

function usePresenceList(enabled: boolean) {
  const [items, setItems] = useState<PresenceUser[]>([])
  const [onlineCount, setOnlineCount] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(enabled)

  const load = useCallback(async () => {
    if (!enabled) return
    try {
      const result = await api.presence()
      setItems(result.items)
      setOnlineCount(result.onlineCount)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load who’s online.')
    } finally {
      setLoading(false)
    }
  }, [enabled])

  useEffect(() => {
    if (!enabled) return
    void load()
    const id = window.setInterval(() => void load(), POLL_MS)
    return () => window.clearInterval(id)
  }, [enabled, load])

  return { items, onlineCount, error, loading, load }
}

export function PresencePopover({ enabled }: { enabled: boolean }) {
  const { items, onlineCount, error, load } = usePresenceList(enabled)
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
        <Badge count={onlineCount} size="small" overflowCount={99}>
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
  const { items, onlineCount, error, loading, load } = usePresenceList(enabled)

  if (!enabled) return null

  return (
    <Card
      className="compact-card"
      loading={loading && items.length === 0}
      title={(
        <TitleWithHelp help="Staff who have a recent heartbeat. Online is the last 90 seconds; Away is up to about three minutes. The page or work item is what they last reported. Clocked in means the floating clock is running.">
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

function SiderPresenceList({ items }: { items: PresenceUser[] }) {
  if (items.length === 0) {
    return <div className="who-online-sider-empty">Nobody else is signed in right now.</div>
  }

  return (
    <ul className="who-online-sider-list">
      {items.map((row) => (
        <li key={row.userId}>
          <span className={`presence-dot is-${row.presenceStatus.toLowerCase()}`} />
          <div className="who-online-sider-person">
            <span className="who-online-sider-name">
              {row.displayName}
              {row.clockedIn ? ' · Clocked in' : ''}
            </span>
            <span className="who-online-sider-page">
              {row.workItemTitle ? (
                <Link to={`/documents/${row.workItemId}`}>{row.workItemTitle}</Link>
              ) : (
                row.pageName
              )}
            </span>
          </div>
        </li>
      ))}
    </ul>
  )
}

/** Left sider under Status. Collapsible, default closed, persist. Same presence rules as the list API. */
export function WhoIsOnlineSider({
  enabled,
  userId,
  collapsed = false,
}: {
  enabled: boolean
  userId?: string
  collapsed?: boolean
}) {
  const { items, onlineCount, error, load } = usePresenceList(enabled)
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
      <div className="who-online-sider is-collapsed" role="region" aria-label="Who’s online">
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
            <Badge count={onlineCount} size="small" overflowCount={99} color="#1890ff">
              <TeamOutlined />
            </Badge>
          </button>
        </Dropdown>
        <div className="who-online-sider-actions" data-slot="need-help-raise-hand" />
      </div>
    )
  }

  return (
    <div className="who-online-sider" role="region" aria-label="Who’s online">
      <div className="who-online-sider-bar">
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
          <span className="who-online-sider-count">{onlineCount}</span>
        </button>
        <div className="who-online-sider-actions" data-slot="need-help-raise-hand" />
      </div>
      {open && (
        <div id="who-online-sider-body" className="who-online-sider-body">
          {list}
        </div>
      )}
    </div>
  )
}
