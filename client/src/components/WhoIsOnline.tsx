import { TeamOutlined } from '@ant-design/icons'
import { Badge, Button, Card, Dropdown, Typography } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import type { PresenceUser } from '../api'
import { api } from '../api'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'
import { PresenceChips, PresencePeople } from './PresencePeople'

const POLL_MS = 20_000

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
