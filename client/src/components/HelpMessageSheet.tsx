import { Button, Drawer, Input, Typography } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import {
  HELP_OFFLINE_REASON,
  HELP_REPLY_CHIPS,
  HELP_SEND_CHIPS,
  type HelpMessage,
  type HelpThread,
} from '../api'
import { api } from '../api'
import { useIsMobile } from '../layout/useIsMobile'
import { usePresence } from '../presence'
import { LoadError } from './LoadError'

export function HelpMessageSheet() {
  const { helpPeer, closeHelp } = usePresence()
  const isMobile = useIsMobile()
  const [thread, setThread] = useState<HelpThread | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [sending, setSending] = useState(false)
  const [draft, setDraft] = useState('')

  const load = useCallback(async () => {
    if (!helpPeer) return
    setLoading(true)
    setError(null)
    try {
      setThread(await api.helpThread(helpPeer.userId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load messages.')
    } finally {
      setLoading(false)
    }
  }, [helpPeer])

  useEffect(() => {
    setThread(null)
    setDraft('')
    if (helpPeer) void load()
  }, [helpPeer, load])

  async function send(chip?: string) {
    if (!helpPeer || !thread?.canCompose) return
    const body = draft.trim()
    if (!chip && !body) return
    setSending(true)
    try {
      const created = await api.sendHelpMessage({
        toUserId: helpPeer.userId,
        chip: chip ?? null,
        body: body || null,
      })
      setThread((current) =>
        current ? { ...current, items: [...current.items, created] } : current,
      )
      setDraft('')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not send.')
    } finally {
      setSending(false)
    }
  }

  const online = thread?.canCompose ?? helpPeer?.presenceStatus === 'Online'
  const disabledReason = thread?.composeDisabledReason || (!online ? HELP_OFFLINE_REASON : null)
  const name = thread?.withDisplayName || helpPeer?.displayName || 'Someone'

  return (
    <Drawer
      title={(
        <div className="help-sheet-title">
          <Typography.Text strong>{name}</Typography.Text>
          <Typography.Text type="secondary">
            {thread?.presenceStatus || helpPeer?.presenceStatus || 'Offline'}
          </Typography.Text>
        </div>
      )}
      open={Boolean(helpPeer)}
      onClose={closeHelp}
      placement={isMobile ? 'bottom' : 'right'}
      height={isMobile ? 420 : undefined}
      width={360}
      className="help-message-sheet"
      data-testid="help-message-sheet"
      destroyOnHidden
    >
      {error && <LoadError message={error} onRetry={() => void load()} />}
      <div className="help-sheet-thread">
        {loading && !thread && <Typography.Text type="secondary">Loading…</Typography.Text>}
        {thread && thread.items.length === 0 && (
          <Typography.Text type="secondary">No messages yet. Keep it short.</Typography.Text>
        )}
        {thread?.items.map((item) => (
          <HelpBubble key={item.id} item={item} />
        ))}
      </div>
      <div className="help-sheet-compose">
        {!online && (
          <Typography.Text type="secondary" className="help-sheet-offline">
            {disabledReason}
          </Typography.Text>
        )}
        <div className="help-sheet-chips">
          {HELP_SEND_CHIPS.map((chip) => (
            <Button
              key={chip.chip}
              size="small"
              disabled={!online || sending}
              onClick={() => void send(chip.chip)}
            >
              {chip.label}
            </Button>
          ))}
        </div>
        <div className="help-sheet-chips">
          {HELP_REPLY_CHIPS.map((chip) => (
            <Button
              key={chip.chip}
              size="small"
              disabled={!online || sending}
              onClick={() => void send(chip.chip)}
            >
              {chip.label}
            </Button>
          ))}
        </div>
        <Input.TextArea
          value={draft}
          onChange={(event) => setDraft(event.target.value)}
          maxLength={280}
          autoSize={{ minRows: 2, maxRows: 3 }}
          disabled={!online || sending}
          placeholder={online ? 'Short note' : HELP_OFFLINE_REASON}
        />
        <Button
          type="primary"
          size="small"
          disabled={!online || sending || !draft.trim()}
          onClick={() => void send()}
        >
          Send
        </Button>
      </div>
    </Drawer>
  )
}

function HelpBubble({ item }: { item: HelpMessage }) {
  return (
    <div className={item.mine ? 'help-bubble is-mine' : 'help-bubble'}>
      <Typography.Text>{item.body}</Typography.Text>
    </div>
  )
}
