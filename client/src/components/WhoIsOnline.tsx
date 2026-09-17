import { DownOutlined, TeamOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Dropdown, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { usePresence } from '../presence'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'
import { NeedHelpMark } from './NeedHelpMark'
import { PresenceChips, PresencePeople } from './PresencePeople'

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
        <TitleWithHelp help="Staff who have a recent heartbeat. Online is the last 90 seconds; Away is up to about three minutes. The page or work item is what they last reported. Clocked in means the floating clock is running. A yellow hand means they tapped Need help?">
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

function NeedHelpToggle() {
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
      onClick={(event) => {
        event.stopPropagation()
        void toggle()
      }}
    >
      {selfNeedsHelp ? <NeedHelpMark compact /> : null}
      {selfNeedsHelp ? 'Clear help' : 'Need help?'}
    </Button>
  )
}

/** Collapsible strip immediately under the top menu bar. Raise-hand lives here. */
export function WhoIsOnlineStrip({ enabled, userId }: { enabled: boolean; userId?: string }) {
  const { items, onlineCount, needsHelpCount, error, load } = usePresence()
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
    <div className="who-online-strip" role="region" aria-label="Who’s online" data-testid="who-online-strip">
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
          {needsHelpCount > 0 && <NeedHelpMark compact />}
        </button>
        <div className="who-online-strip-actions">
          <NeedHelpToggle />
        </div>
      </div>
      {open && (
        <div id="who-online-strip-body" className="who-online-strip-body">
          {error ? <LoadError message={error} onRetry={() => void load()} /> : <PresenceChips items={items} />}
        </div>
      )}
    </div>
  )
}
