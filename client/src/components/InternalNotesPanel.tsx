import { Button, Card, Collapse, Empty, Input, Space, Timeline, Typography, message } from 'antd'
import { TitleWithHelp } from './HelpTip'
import dayjs from 'dayjs'
import { useEffect, useState } from 'react'
import type { WorkItemDetail } from '../api'
import { api } from '../api'

export function InternalNotesPanel({
  item,
  onUpdated,
}: {
  item: WorkItemDetail
  onUpdated: (next: WorkItemDetail) => void
}) {
  const [draft, setDraft] = useState(item.internalNotes ?? '')
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    setDraft(item.internalNotes ?? '')
  }, [item.id, item.internalNotes])

  if (!item.canSeeInternalNotes) {
    return null
  }

  async function save() {
    setSaving(true)
    try {
      const updated = await api.updateWorkItem(item.id, { internalNotes: draft })
      onUpdated(updated)
      setDraft(updated.internalNotes ?? '')
      message.success('Internal Notes saved.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not save Internal Notes.')
    } finally {
      setSaving(false)
    }
  }

  const history = item.internalNotesHistory ?? []
  const lastEdited = item.internalNotesUpdatedAt
    ? `Last edited ${dayjs(item.internalNotesUpdatedAt).format('YYYY-MM-DD HH:mm')}${
        item.internalNotesUpdatedByName ? ` by ${item.internalNotesUpdatedByName}` : ''
      }`
    : null

  return (
    <Card
      size="small"
      title={(
        <TitleWithHelp help="Staff-only notes. Global Administrators, Administrators, and Editors can read and edit. Viewers and clients cannot see this field.">
          Internal Notes — staff only
        </TitleWithHelp>
      )}
    >
      {item.canEditInternalNotes ? (
        <Space direction="vertical" style={{ width: '100%' }} size={8}>
          <Input.TextArea
            rows={5}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            placeholder="Shared BIS notes — hidden from clients"
            maxLength={4000}
          />
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
            <Typography.Text type="secondary">{lastEdited}</Typography.Text>
            <Button type="primary" onClick={save} loading={saving} disabled={draft === (item.internalNotes ?? '')}>
              Save notes
            </Button>
          </div>
        </Space>
      ) : (
        <div>
          {item.internalNotes ? (
            <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>{item.internalNotes}</Typography.Paragraph>
          ) : (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No internal notes." />
          )}
          {lastEdited && <Typography.Text type="secondary">{lastEdited}</Typography.Text>}
        </div>
      )}
      {history.length > 0 && (
        <Collapse
          ghost
          style={{ marginTop: 8 }}
          items={[
            {
              key: 'history',
              label: `Note history (${history.length})`,
              children: (
                <Timeline
                  items={history.map((rev) => ({
                    children: (
                      <div>
                        <Typography.Text type="secondary">
                          {dayjs(rev.editedAt).format('YYYY-MM-DD HH:mm')} · {rev.editedByName}
                        </Typography.Text>
                        <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                          {rev.body || <Typography.Text type="secondary">Cleared</Typography.Text>}
                        </Typography.Paragraph>
                      </div>
                    ),
                  }))}
                />
              ),
            },
          ]}
        />
      )}
    </Card>
  )
}
