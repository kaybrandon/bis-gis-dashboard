import { DownOutlined, TeamOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Dropdown, Typography } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import type { PresenceUser } from '../api'
import { api } from '../api'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'
import { PresenceChips, PresencePeople } from './PresencePeople'

const POLL_MS = 20_000
const STRIP_KEY = 'gis.whoIsOnlineOpen'

function stripStorageKey(userId?: string) {
  return userId ? `${STRIP_KEY}.${userId}` : STRIP_KEY
}

function readStripOpen(userId?: string) {
  try {
    return localStorage.getItem(stripStorageKey(userId)) !== '0'
  } catch {
    return true
  }
}

function writeStripOpen(userId: string | undefined, open: boolean) {
  try {
    localStorage.setItem(stripStorageKey(userId), open ? '1' : '0')
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

/** App chrome strip immediately under the top menu bar. Same presence rules as the list API. */
export function WhoIsOnlineStrip({ enabled, userId }: { enabled: boolean; userId?: string }) {
  const { items, onlineCount, error, load } = usePresenceList(enabled)
  const [open, setOpen] = useState(() => readStripOpen(userId))

  useEffect(() => {
    setOpen(readStripOpen(userId))
  }, [userId])

  const toggle = () => {
    const next = !open
    setOpen(next)
    writeStripOpen(userId, next)
  }

  if (!enabled) return null

  return (
    <div className="who-online-strip" role="region" aria-label="Who’s online">
      <div className="who-online-strip-bar">
        <button
          type="button"
          className="who-online-strip-toggle"
          aria-expanded={open}
          aria-controls="who-online-strip-body"
          onClick={toggle}
        >
          <DownOutlined className={open ? 'who-online-chevron is-open' : 'who-online-chevron'} />
          <TeamOutlined />
          <Typography.Text strong>Who’s online</Typography.Text>
          <Typography.Text type="secondary">{onlineCount} online</Typography.Text>
        </button>
        {/* Reserved for Need help raise-hand (yellow) on this same strip. */}
        <div className="who-online-strip-actions" data-slot="need-help-raise-hand" />
      </div>
      {open && (
        <div id="who-online-strip-body" className="who-online-strip-body">
          {error ? <LoadError message={error} onRetry={() => void load()} /> : <PresenceChips items={items} />}
        </div>
      )}
    </div>
  )
}
