import { DeleteOutlined, EditOutlined } from '@ant-design/icons'
import {
  Button,
  Card,
  DatePicker,
  Empty,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Space,
  Table,
  Typography,
  message,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { useEffect, useState } from 'react'
import type { TimeEntry, WorkItemDetail } from '../api'
import { api } from '../api'
import { TIME_LOGGED_EVENT } from '../timeClock'
import { TitleWithHelp } from './HelpTip'

type TimeForm = {
  hours: number
  minutes: number
  workedOn: dayjs.Dayjs
  note?: string
}

export function TimeLogPanel({ item }: { item: WorkItemDetail }) {
  const [createForm] = Form.useForm<TimeForm>()
  const [editForm] = Form.useForm<TimeForm>()
  const [entries, setEntries] = useState<TimeEntry[]>([])
  const [totalLabel, setTotalLabel] = useState('0m')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [editing, setEditing] = useState<TimeEntry | null>(null)

  async function reload() {
    setLoading(true)
    try {
      const result = await api.timeEntries(item.id)
      setEntries(result.items)
      setTotalLabel(result.totalLabel)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Time logs could not be loaded.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (!item.canSeeTimeLogs) return
    reload()
    const onLogged = (event: Event) => {
      const workItemId = (event as CustomEvent<{ workItemId?: string }>).detail?.workItemId
      if (workItemId === item.id) void reload()
    }
    window.addEventListener(TIME_LOGGED_EVENT, onLogged)
    return () => window.removeEventListener(TIME_LOGGED_EVENT, onLogged)
  }, [item.id, item.canSeeTimeLogs])

  if (!item.canSeeTimeLogs) {
    return null
  }

  async function submit(values: TimeForm, entryId?: string) {
    setSaving(true)
    const body = {
      hours: values.hours ?? 0,
      minutes: values.minutes ?? 0,
      workedOn: values.workedOn.startOf('day').toISOString(),
      note: values.note,
    }
    try {
      if (entryId) {
        await api.updateTimeEntry(item.id, entryId, body)
        message.success('Time entry updated.')
        setEditing(null)
      } else {
        await api.createTimeEntry(item.id, body)
        message.success('Time logged.')
        createForm.resetFields()
        createForm.setFieldsValue({ hours: 0, minutes: 30, workedOn: dayjs() })
      }
      await reload()
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not save time entry.')
    } finally {
      setSaving(false)
    }
  }

  const columns: ColumnsType<TimeEntry> = [
    {
      title: 'Worked on',
      dataIndex: 'workedOn',
      render: (value: string) => dayjs(value).format('YYYY-MM-DD'),
    },
    { title: 'Duration', dataIndex: 'durationLabel' },
    { title: 'Logged by', dataIndex: 'loggedByName' },
    {
      title: 'Note',
      dataIndex: 'note',
      render: (value?: string | null) => value || <Typography.Text type="secondary">—</Typography.Text>,
    },
  ]

  if (item.canLogTime) {
    columns.push({
      title: '',
      key: 'actions',
      width: 72,
      render: (_, row) =>
        row.canEdit ? (
          <Space>
            <Button
              size="small"
              type="text"
              icon={<EditOutlined />}
              aria-label="Edit time entry"
              onClick={() => {
                setEditing(row)
                editForm.setFieldsValue({
                  hours: Math.floor(row.minutes / 60),
                  minutes: row.minutes % 60,
                  workedOn: dayjs(row.workedOn),
                  note: row.note ?? undefined,
                })
              }}
            />
            <Popconfirm
              title="Delete this time entry?"
              onConfirm={async () => {
                try {
                  await api.deleteTimeEntry(item.id, row.id)
                  message.success('Time entry deleted.')
                  await reload()
                } catch (err) {
                  message.error(err instanceof Error ? err.message : 'Could not delete time entry.')
                }
              }}
            >
              <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label="Delete time entry" />
            </Popconfirm>
          </Space>
        ) : null,
    })
  }

  return (
    <Card
      size="small"
      className="bis-theme-panel"
      title={(
        <TitleWithHelp help="Time logged on this work item. Viewers can read; Editors log their own; Administrators can manage all entries in scope.">
          Time log
        </TitleWithHelp>
      )}
      extra={<Typography.Text type="secondary">Total time {totalLabel}</Typography.Text>}
    >
      {item.canLogTime && (
        <Form
          form={createForm}
          layout="vertical"
          className="dense-form"
          initialValues={{ hours: 0, minutes: 30, workedOn: dayjs() }}
          onFinish={(values) => submit(values)}
          style={{ marginBottom: 12 }}
        >
          <DurationFields />
          <Button type="primary" htmlType="submit" loading={saving && !editing}>
            Log time
          </Button>
        </Form>
      )}
      {error && <Typography.Text type="danger">{error}</Typography.Text>}
      <Table
        rowKey="id"
        className="bis-theme-grid"
        size="small"
        loading={loading}
        columns={columns}
        dataSource={entries}
        pagination={false}
        locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No time logged yet." /> }}
        scroll={{ x: 420 }}
      />

      <Modal
        title="Edit time entry"
        open={!!editing}
        onCancel={() => setEditing(null)}
        onOk={() => editForm.submit()}
        confirmLoading={saving}
        destroyOnHidden
      >
        <Form
          form={editForm}
          layout="vertical"
          className="dense-form"
          onFinish={(values) => editing && submit(values, editing.id)}
        >
          <DurationFields />
        </Form>
      </Modal>
    </Card>
  )
}

function DurationFields() {
  return (
    <>
      <Space wrap size={8} style={{ width: '100%' }}>
        <Form.Item name="hours" label="Hours" style={{ marginBottom: 8 }}>
          <InputNumber min={0} max={24} style={{ width: 88 }} />
        </Form.Item>
        <Form.Item name="minutes" label="Minutes" style={{ marginBottom: 8 }}>
          <InputNumber min={0} max={59} style={{ width: 88 }} />
        </Form.Item>
        <Form.Item name="workedOn" label="Worked on" style={{ marginBottom: 8 }}>
          <DatePicker allowClear={false} />
        </Form.Item>
      </Space>
      <Form.Item name="note" label="Entry note" style={{ marginBottom: 8 }}>
        <Input placeholder="Optional — not Internal Notes" maxLength={500} />
      </Form.Item>
    </>
  )
}
