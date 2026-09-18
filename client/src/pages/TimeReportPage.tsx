import { PrinterOutlined, DownloadOutlined } from '@ant-design/icons'
import { Button, Card, DatePicker, Empty, Select, Space, Statistic, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import type {
  TimeReportBucket,
  TimeReportLine,
  TimeReportNamedTotal,
  TimeReportPeriodTotal,
  TimeReportQuery,
  TimeReportResponse,
  TimeReportWorkItemTotal,
} from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'

const emptyReport: TimeReportResponse = {
  from: '',
  to: '',
  bucket: 'month',
  canViewTeam: false,
  totalMinutes: 0,
  totalHours: 0,
  totalLabel: '0m',
  peopleCount: 0,
  clientCount: 0,
  workItemCount: 0,
  people: [],
  byPerson: [],
  byClient: [],
  byPeriod: [],
  byWorkItem: [],
  entries: [],
}

export function TimeReportPage() {
  const { user } = useAuth()
  const [range, setRange] = useState<[Dayjs, Dayjs]>([dayjs().startOf('month'), dayjs()])
  const [bucket, setBucket] = useState<TimeReportBucket>('month')
  const [orgId, setOrgId] = useState<string | undefined>()
  const [userId, setUserId] = useState<string | undefined>()
  const [report, setReport] = useState<TimeReportResponse>(emptyReport)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const query = useMemo<TimeReportQuery>(() => ({
    from: range[0].format('YYYY-MM-DD'),
    to: range[1].format('YYYY-MM-DD'),
    organizationId: orgId,
    userId,
    bucket,
  }), [range, orgId, userId, bucket])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setReport(await api.timeReport(query))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Time report failed to load.')
      setReport(emptyReport)
    } finally {
      setLoading(false)
    }
  }, [query])

  useEffect(() => {
    void load()
  }, [load])

  const personColumns: ColumnsType<TimeReportNamedTotal> = [
    { title: 'Person', dataIndex: 'name' },
    { title: 'Hours', dataIndex: 'hoursLabel', width: 110 },
    { title: 'Entries', dataIndex: 'entryCount', width: 90, responsive: ['sm'] },
  ]
  const clientColumns: ColumnsType<TimeReportNamedTotal> = [
    { title: 'Client', dataIndex: 'name' },
    { title: 'Hours', dataIndex: 'hoursLabel', width: 110 },
    { title: 'Entries', dataIndex: 'entryCount', width: 90, responsive: ['sm'] },
  ]
  const periodColumns: ColumnsType<TimeReportPeriodTotal> = [
    { title: 'Period', dataIndex: 'label' },
    { title: 'Hours', dataIndex: 'hoursLabel', width: 110 },
    { title: 'Entries', dataIndex: 'entryCount', width: 90, responsive: ['sm'] },
  ]
  const workItemColumns: ColumnsType<TimeReportWorkItemTotal> = [
    {
      title: 'Work item',
      dataIndex: 'fileName',
      render: (name: string, row) => <Link to={`/documents/${row.id}`}>{name}</Link>,
    },
    { title: 'Client', dataIndex: 'organizationName', responsive: ['md'] },
    { title: 'Hours', dataIndex: 'hoursLabel', width: 110 },
  ]
  const entryColumns: ColumnsType<TimeReportLine> = [
    { title: 'Worked on', dataIndex: 'workedOn', render: (value: string) => dayjs(value).format('YYYY-MM-DD'), width: 120 },
    { title: 'Person', dataIndex: 'loggedByName' },
    { title: 'Client', dataIndex: 'organizationName', responsive: ['md'] },
    {
      title: 'Work item',
      dataIndex: 'fileName',
      render: (name: string, row) => <Link to={`/documents/${row.workItemId}`}>{name}</Link>,
    },
    { title: 'Hours', dataIndex: 'hoursLabel', width: 100 },
    { title: 'Note', dataIndex: 'note', responsive: ['lg'], render: (value?: string | null) => value || '—' },
  ]

  const empty = !loading && report.entries.length === 0

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }} className="time-report-page">
      <div className="time-report-heading">
        <div>
          <Typography.Title level={3} className="page-title" style={{ margin: 0 }}>
            <TitleWithHelp
              help={
                user?.canViewTeamTimeReport
                  ? 'Hours logged on GIS work items. Technicians always see their own card. You can also review team hours for organizations you can access.'
                  : 'Hours logged on GIS work items. Technicians always see their own card. Team hours for a client appear here only when a Global Administrator turns that organization on.'
              }
            >
              Time report card
            </TitleWithHelp>
          </Typography.Title>
        </div>
        <Space wrap className="time-report-actions">
          <Button icon={<DownloadOutlined />} onClick={() => void api.exportTimeReport(query)}>
            Export CSV
          </Button>
          <Button icon={<PrinterOutlined />} onClick={() => window.print()}>
            Print
          </Button>
        </Space>
      </div>

      <Card className="time-report-filters compact-card bis-theme-panel">
        <div className="filter-toolbar">
          <DatePicker.RangePicker
            value={range}
            allowClear={false}
            onChange={(value) => {
              if (value?.[0] && value[1]) setRange([value[0], value[1]])
            }}
            className="filter-field-lg"
          />
          <Select
            value={bucket}
            onChange={setBucket}
            className="filter-field-sm"
            options={[
              { value: 'day', label: 'By day' },
              { value: 'week', label: 'By week' },
              { value: 'month', label: 'By month' },
            ]}
          />
          <Select
            allowClear
            placeholder="All assigned clients"
            className="filter-field"
            value={orgId}
            onChange={setOrgId}
            options={user?.organizations.map((o) => ({ value: o.id, label: o.name }))}
          />
          {(user?.canViewTeamTimeReport || report.canViewTeam) && (
            <Select
              allowClear
              placeholder="All people"
              className="filter-field"
              value={userId}
              onChange={setUserId}
              options={report.people.map((p) => ({ value: p.id, label: p.name }))}
            />
          )}
        </div>
      </Card>

      {error && <LoadError message={error} onRetry={() => void load()} />}

      <div className="time-report-kpis">
        <Card className="bis-theme-panel" title="Hours" loading={loading}><Statistic value={report.totalLabel} /></Card>
        <Card className="bis-theme-panel" title="People" loading={loading}><Statistic value={report.peopleCount} /></Card>
        <Card className="bis-theme-panel" title="Clients" loading={loading}><Statistic value={report.clientCount} /></Card>
        <Card className="bis-theme-panel" title="Work items" loading={loading}><Statistic value={report.workItemCount} /></Card>
      </div>

      {empty ? (
        <Card className="bis-theme-panel">
          <Empty description="No hours logged in this period. Time comes from the hours already entered on GIS work items." />
        </Card>
      ) : (
        <>
          <div className="time-report-summaries">
            <Card title="By person" size="small" className="bis-theme-panel">
              <Table rowKey="id" className="bis-theme-grid" size="small" pagination={false} loading={loading} columns={personColumns} dataSource={report.byPerson} />
            </Card>
            <Card title="By client" size="small" className="bis-theme-panel">
              <Table rowKey="id" className="bis-theme-grid" size="small" pagination={false} loading={loading} columns={clientColumns} dataSource={report.byClient} />
            </Card>
            <Card title="By period" size="small" className="bis-theme-panel">
              <Table rowKey="key" className="bis-theme-grid" size="small" pagination={false} loading={loading} columns={periodColumns} dataSource={report.byPeriod} />
            </Card>
          </div>
          <Card title="By work item" size="small" className="bis-theme-panel">
            <Table
              rowKey="id"
              className="bis-theme-grid"
              size="small"
              loading={loading}
              columns={workItemColumns}
              dataSource={report.byWorkItem}
              pagination={{ pageSize: 8, hideOnSinglePage: true }}
              scroll={{ x: 'max-content' }}
            />
          </Card>
          <Card title="Line items" size="small" className="bis-theme-panel">
            <Table
              rowKey={(row) => `${row.workItemId}-${row.workedOn}-${row.loggedByName}-${row.minutes}-${row.note ?? ''}`}
              className="bis-theme-grid"
              size="small"
              loading={loading}
              columns={entryColumns}
              dataSource={report.entries}
              pagination={{ pageSize: 12, hideOnSinglePage: true }}
              scroll={{ x: 'max-content' }}
            />
          </Card>
        </>
      )}
    </Space>
  )
}
