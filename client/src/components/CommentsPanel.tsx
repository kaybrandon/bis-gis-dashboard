import { Button, Card, Empty, Input, Space, Timeline, Typography, message } from 'antd'
import { TitleWithHelp } from './HelpTip'
import dayjs from 'dayjs'
import { useCallback, useEffect, useState } from 'react'
import type { Comment } from '../api'
import { api } from '../api'

export function CommentsPanel({
  workItemId,
  canPost,
}: {
  workItemId: string
  canPost: boolean
}) {
  const [items, setItems] = useState<Comment[]>([])
  const [body, setBody] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setItems(await api.comments(workItemId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Comments failed to load.')
    } finally {
      setLoading(false)
    }
  }, [workItemId])

  useEffect(() => {
    void load()
  }, [load])

  async function post() {
    const text = body.trim()
    if (!text) return
    setSaving(true)
    try {
      const created = await api.createComment(workItemId, text)
      setItems((current) => [...current, created])
      setBody('')
      message.success('Comment posted.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not post comment.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card
      size="small"
      className="bis-theme-panel"
      loading={loading}
      title={(
        <TitleWithHelp help="Client-visible thread. Clients and staff can read this. Uploaders and staff can post. Viewers can read but not post. A new comment emails the assignee and Assigned tech(s). Use @username to ping someone in the bell. Internal Notes stay staff-only.">
          Comments — client visible
        </TitleWithHelp>
      )}
    >
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
      {items.length === 0 && !error ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No comments yet." />
      ) : (
        <Timeline
          items={items.map((item) => ({
            children: (
              <div>
                <Typography.Text type="secondary">
                  {dayjs(item.createdAt).format('YYYY-MM-DD HH:mm')} · {item.authorName}
                </Typography.Text>
                <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>{item.body}</Typography.Paragraph>
              </div>
            ),
          }))}
        />
      )}
      {canPost ? (
        <Space direction="vertical" style={{ width: '100%' }} size={8}>
          <Input.TextArea
            rows={3}
            value={body}
            maxLength={2000}
            onChange={(e) => setBody(e.target.value)}
            placeholder="Client-visible comment. Use @username to notify someone."
          />
          <Button type="primary" onClick={() => void post()} loading={saving} disabled={!body.trim()}>
            Post comment
          </Button>
        </Space>
      ) : (
        <Typography.Text type="secondary">You can read this client-visible thread but cannot post.</Typography.Text>
      )}
    </Card>
  )
}
