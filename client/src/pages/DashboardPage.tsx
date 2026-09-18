import { CheckCircleOutlined, ClockCircleOutlined, DownloadOutlined, MailOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { Button, Card, Col, DatePicker, Form, Input, Modal, Row, Select, Space, Statistic, Table, Typography, message } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import type { AssignableUser, DashboardQuery, DashboardRecipient, DashboardResponse, LookupItem, OrgOption } from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { DashboardCharts } from '../components/DashboardCharts'
import { ASSIGNED_TO_FILTER_HELP, ASSIGNED_TO_HELP, ASSIGNED_TO_LABEL, UNASSIGNED_LABEL } from '../assignmentLabels'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { WorkPresenceMarks } from '../components/PresencePeople'
import { WorkItemCards } from '../components/WorkItemCards'
import { documentsHref, type DocumentsHrefQuery } from '../documentsHref'
import { useIsMobile } from '../layout/useIsMobile'
import { canSeeDashboardAssignee } from '../roles'
import { statusLabel } from '../statusLabels'
import { UNASSIGNED, activeDocumentsQuery, assigneeHrefValue, resolveAssigneeFilter } from '../staffQueue'

const kpiIcon: Record<string, ReactNode> = {
  active: <ThunderboltOutlined />,
  pending: <ClockCircleOutlined />,
  completed: <CheckCircleOutlined />,
  priority: <ThunderboltOutlined />,
}

const kpiFallback: Record<string, string> = {
  active: 'Active',
  pending: 'Pending',
  completed: 'Completed',
  priority: 'Priority',
}

const defaultRange = (): [Dayjs, Dayjs] => [dayjs().subtract(29, 'day'), dayjs()]

export function DashboardPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const isMobile = useIsMobile()
  const [data, setData] = useState<DashboardResponse | null>(null)
  const [orgs, setOrgs] = useState<OrgOption[]>([])
  const [statuses, setStatuses] = useState<LookupItem[]>([])
  const [assignees, setAssignees] = useState<AssignableUser[]>([])
  const [orgId, setOrgId] = useState<string | undefined>()
  const [statusId, setStatusId] = useState<string | undefined>()
  const [assignedTo, setAssignedTo] = useState<string | undefined>(() => resolveAssigneeFilter(undefined, user))
  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [downloading, setDownloading] = useState(false)
  const [emailOpen, setEmailOpen] = useState(false)
  const [recipients, setRecipients] = useState<DashboardRecipient[]>([])
  const [sending, setSending] = useState(false)
  const [emailForm] = Form.useForm<{ userIds: string[]; extra: string }>()

  const showAssignee = canSeeDashboardAssignee(user)

  const loadLookups = useCallback(async () => {
    try {
      const [o, s, a] = await Promise.all([
        api.organizations(),
        api.statuses(),
        showAssignee ? api.assignees() : Promise.resolve([] as AssignableUser[]),
      ])
      setOrgs(o)
      setStatuses(s)
      setAssignees(a)
    } catch {
      /* keep empty */
    }
  }, [showAssignee])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setData(await api.dashboard(dashboardQuery()))
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Dashboard failed to load.'
      setError(text)
    } finally {
      setLoading(false)
    }
  }, [assignedTo, orgId, range, statusId])

  const dashboardQuery = (): DashboardQuery => ({
    organizationId: orgId,
    statusId,
    assignedToUserId: showAssignee ? assignedTo : undefined,
    from: range[0].startOf('day').toISOString(),
    to: range[1].endOf('day').toISOString(),
  })

  useEffect(() => {
    void loadLookups()
  }, [loadLookups])

  useEffect(() => {
    void load()
  }, [load])

  const kpi = (key: string) => data?.kpis.find((item) => item.key === key)
  const filters = {
    organizationId: orgId,
    statusId,
    assignedToUserId: showAssignee ? assigneeHrefValue(assignedTo, user) : undefined,
    from: range[0].startOf('day').toISOString(),
    to: range[1].endOf('day').toISOString(),
  }

  const openDocuments = (query: DocumentsHrefQuery) => {
    navigate(documentsHref(query))
  }

  const openKpi = (key: 'active' | 'pending' | 'completed' | 'priority') => {
    if (key === 'pending') {
      openDocuments({ ...filters, bucket: 'pending' })
      return
    }
    if (key === 'priority') {
      openDocuments({ ...filters, bucket: 'priority' })
      return
    }
    if (key === 'active') {
      const activeId = statuses.find((s) => statusLabel(s.name) === 'Active')?.id
      openDocuments(activeDocumentsQuery({
        organizationId: orgId,
        assignedToUserId: assignedTo,
        activeStatusId: activeId,
        user,
      }))
      return
    }
    openDocuments({
      organizationId: orgId,
      statusId,
      assignedToUserId: showAssignee ? assignedTo : undefined,
      bucket: 'completed',
      workedFrom: range[0].startOf('day').toISOString(),
      workedTo: range[1].endOf('day').toISOString(),
    })
  }

  const downloadPdf = async () => {
    setDownloading(true)
    try {
      await api.dashboardPdf(dashboardQuery())
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not download the dashboard PDF.')
    } finally {
      setDownloading(false)
    }
  }

  const openEmail = async () => {
    try {
      setRecipients(await api.dashboardRecipients(orgId))
      emailForm.setFieldsValue({ userIds: [], extra: '' })
      setEmailOpen(true)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not load recipients.')
    }
  }

  const sendEmail = async () => {
    const values = await emailForm.validateFields()
    const extraEmails = (values.extra ?? '')
      .split(/[,;\s]+/)
      .map((item) => item.trim())
      .filter(Boolean)
    const userIds = values.userIds ?? []
    if (userIds.length === 0 && extraEmails.length === 0) {
      message.error('Choose at least one recipient.')
      return
    }
    const names = [
      ...recipients.filter((r) => userIds.includes(r.id)).map((r) => r.displayName),
      ...extraEmails,
    ]
    Modal.confirm({
      title: 'Send this dashboard PDF?',
      content: `Email the current dashboard view (${data?.rangeLabel ?? 'selected dates'}) to ${names.join(', ')}.`,
      okText: 'Send report',
      onOk: async () => {
        setSending(true)
        try {
          const result = await api.emailDashboard(dashboardQuery(), { userIds, extraEmails })
          setEmailOpen(false)
          message.success(result.note || 'Dashboard PDF sent.')
        } catch (err) {
          message.error(err instanceof Error ? err.message : 'Could not send the dashboard.')
        } finally {
          setSending(false)
        }
      },
    })
  }

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} className="page-title" style={{ margin: 0 }}>
          <TitleWithHelp help={`Active, pending, completed, and priority volume for ${user?.displayName ?? 'your account'}. Staff default to items Assigned to you; switch Assigned to — all, Unassigned, or another person. Assigned to is the work-item assignee (queues/reports), not the organization’s Assigned technician. Document counts by CAD (v1 = Organization name) and by technician (Assigned to, including Unassigned) use created/uploaded dates in the selected range and match the filtered Manage Documents list. The Active tile is the full matching Active total (not one page) and opens that same queue. The date range filters pending/completed/priority and volume charts (default last 30 days), not the Active count. Viewer and Uploader do not see Assigned to and stay in assigned organizations.`}>
            Dashboard
          </TitleWithHelp>
        </Typography.Title>
      </div>

      <div className="filter-toolbar">
        <Select
          allowClear
          placeholder="Organization"
          className="filter-field"
          value={orgId}
          onChange={setOrgId}
          options={orgs.map((o) => ({ value: o.id, label: o.name }))}
        />
        <Select
          allowClear
          placeholder="Status"
          className="filter-field"
          value={statusId}
          onChange={setStatusId}
          options={statuses.map((s) => ({ value: s.id, label: statusLabel(s.name) }))}
        />
        {showAssignee && (
          <Select
            allowClear
            placeholder="All Assigned to"
            className="filter-field"
            title={ASSIGNED_TO_FILTER_HELP}
            value={assignedTo}
            onChange={setAssignedTo}
            options={[
              { value: UNASSIGNED, label: UNASSIGNED_LABEL },
              ...assignees.map((a) => ({ value: a.id, label: a.displayName })),
            ]}
          />
        )}
        <DatePicker.RangePicker
          allowClear={false}
          value={range}
          className="filter-field-range"
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <div className="filter-actions">
          <Button size="small" icon={<DownloadOutlined />} loading={downloading} onClick={() => void downloadPdf()}>
            Download PDF
          </Button>
          {user?.canMutateWorkItems && (
            <Button size="small" icon={<MailOutlined />} onClick={() => void openEmail()}>
              Send report
            </Button>
          )}
        </div>
      </div>

      {error && <LoadError message={error} onRetry={() => void load()} />}

      <Row gutter={[8, 8]}>
        {(['active', 'pending', 'completed', 'priority'] as const).map((key) => {
          const card = kpi(key)
          return (
            <Col xs={12} md={6} key={key}>
              <Card
                loading={loading}
                size="small"
                hoverable
                className="kpi-card"
                role="button"
                tabIndex={0}
                aria-label={`Open ${card?.label ?? kpiFallback[key]} work items`}
                styles={{ body: { padding: isMobile ? '10px 8px' : 24 } }}
                onClick={() => openKpi(key)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault()
                    openKpi(key)
                  }
                }}
              >
                <Statistic
                  title={card?.label ?? kpiFallback[key]}
                  value={card?.count ?? 0}
                  prefix={isMobile ? undefined : kpiIcon[key]}
                  valueStyle={card?.color ? { color: card.color } : undefined}
                />
              </Card>
            </Col>
          )
        })}
      </Row>

      <DashboardCharts data={data} loading={loading} filters={filters} onOpen={openDocuments} />

      <Card title="Recently completed" loading={loading && !isMobile}>
        {isMobile ? (
          <WorkItemCards
            items={data?.recentCompleted ?? []}
            loading={loading}
            emptyText="No completed work items match these filters."
            onOpen={(id) => navigate(`/documents/${id}`)}
          />
        ) : (
          <Table
            rowKey="id"
            size="small"
            pagination={false}
            dataSource={data?.recentCompleted ?? []}
            locale={{ emptyText: 'No completed work items match these filters.' }}
            onRow={(row) => ({
              onClick: () => navigate(`/documents/${row.id}`),
              style: { cursor: 'pointer' },
            })}
            columns={[
              { title: 'File name', dataIndex: 'fileName', ellipsis: true },
              { title: 'Client name', dataIndex: 'organizationName', width: 180 },
              { title: 'Status', dataIndex: 'statusName', width: 120, render: (name: string) => statusLabel(name) },
              {
                title: <TitleWithHelp help={ASSIGNED_TO_HELP}>{ASSIGNED_TO_LABEL}</TitleWithHelp>,
                dataIndex: 'assignedToName',
                width: 180,
                render: (v: string | null, row) => (
                  <span>
                    {v ?? '—'}
                    <WorkPresenceMarks workItemId={row.id} assignedToUserId={row.assignedToUserId} />
                  </span>
                ),
              },
              {
                title: 'Worked date',
                dataIndex: 'workedOn',
                width: 140,
                render: (v: string | null) => (v ? dayjs(v).format('YYYY-MM-DD') : '—'),
              },
              { title: 'Hours', dataIndex: 'hoursLabel', width: 90 },
            ]}
          />
        )}
      </Card>

      <Modal
        title="Send dashboard report"
        open={emailOpen}
        okText="Send report"
        confirmLoading={sending}
        onCancel={() => setEmailOpen(false)}
        onOk={() => void sendEmail()}
      >
        <Typography.Paragraph type="secondary">
          Emails the PDF of the current dashboard filters ({data?.rangeLabel ?? 'selected dates'}). You will confirm before it sends.
        </Typography.Paragraph>
        <Form form={emailForm} layout="vertical">
          <Form.Item name="userIds" label="People">
            <Select
              mode="multiple"
              allowClear
              placeholder="Select people"
              options={recipients.map((r) => ({ value: r.id, label: `${r.displayName} (${r.email})` }))}
            />
          </Form.Item>
          <Form.Item name="extra" label="Additional email addresses">
            <Input.TextArea rows={2} placeholder="name@example.com, one per line or comma-separated" />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
