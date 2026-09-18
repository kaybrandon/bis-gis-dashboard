import { ArrowLeftOutlined, DownloadOutlined, MailOutlined } from '@ant-design/icons'
import { Pie } from '@ant-design/plots'
import {
  Button,
  Card,
  Empty,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd'
import dayjs from 'dayjs'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import type { ReportDetail, ReportRecipient } from '../api'
import { api } from '../api'
import { TitleWithHelp } from '../components/HelpTip'
import { reportPersonLabel } from '../personLabel'
import { LoadError } from '../components/LoadError'
import { useAuth } from '../auth'
import { useIsMobile } from '../layout/useIsMobile'

function formatCount(value?: number | null, available?: boolean) {
  if (!available || value == null) return 'Not available'
  return value.toLocaleString()
}

export function ReportDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { user } = useAuth()
  const navigate = useNavigate()
  const isMobile = useIsMobile()
  const [report, setReport] = useState<ReportDetail | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [emailOpen, setEmailOpen] = useState(false)
  const [recipients, setRecipients] = useState<ReportRecipient[]>([])
  const [sending, setSending] = useState(false)
  const [form] = Form.useForm<{ userIds: string[]; extra: string }>()

  const load = useCallback(async () => {
    if (!id) return
    setLoading(true)
    setError(null)
    try {
      setReport(await api.report(id))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Report was not found.')
      setReport(null)
    } finally {
      setLoading(false)
    }
  }, [id])

  useEffect(() => {
    void load()
  }, [load])

  const openEmail = async () => {
    if (!report) return
    try {
      setRecipients(await api.reportRecipients(report.organizationId))
      form.setFieldsValue({ userIds: [], extra: '' })
      setEmailOpen(true)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load recipients.')
    }
  }

  const send = async () => {
    if (!report) return
    const values = await form.validateFields()
    const extraEmails = (values.extra ?? '')
      .split(/[,;\s]+/)
      .map((item) => item.trim())
      .filter(Boolean)
    setSending(true)
    try {
      const result = await api.emailReport(report.id, {
        userIds: values.userIds ?? [],
        extraEmails,
      })
      setEmailOpen(false)
      await load()
      setNotice(result.note ?? (result.delivered ? 'Email sent with the PDF attached.' : 'Dry-run recorded. Email is not configured.'))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not email the report.')
    } finally {
      setSending(false)
    }
  }

  if (!loading && !report) {
    return (
      <Card className="bis-theme-panel">
        <Space direction="vertical">
          <Link to="/reports"><ArrowLeftOutlined /> All reports</Link>
          <Typography.Text type="danger">{error ?? 'Report was not found.'}</Typography.Text>
        </Space>
      </Card>
    )
  }

  const snap = report?.snapshot
  const parcel = snap?.parcelStatus
  const canEmail = !!report?.canEmail && !!user?.canMutateWorkItems
  const pieData = (snap?.maintenanceByType ?? []).map((row) => ({
    type: `${row.name} (${row.count})`,
    value: row.count,
    color: row.color ?? undefined,
  }))
  const pieColors = pieData.map((row) => row.color).filter(Boolean) as string[]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Button type="link" style={{ paddingLeft: 0 }} onClick={() => navigate('/reports')}>
          <ArrowLeftOutlined /> All reports
        </Button>
        <Space wrap>
          {report && (
            <>
              <Button icon={<DownloadOutlined />} onClick={() => void api.downloadReportPdf(report.id).catch((err) => setError(err.message))}>
                Download PDF
              </Button>
              <Button icon={<DownloadOutlined />} onClick={() => void api.downloadReportCsv(report.id).catch((err) => setError(err.message))}>
                Download CSV file
              </Button>
            </>
          )}
          {canEmail && (
            <Button type="primary" icon={<MailOutlined />} onClick={() => void openEmail()}>
              Email report
            </Button>
          )}
        </Space>
      </Space>

      {error && <LoadError message={error} onRetry={() => void load()} />}
      {notice && <Typography.Text type="warning">{notice}</Typography.Text>}

      <div className="maintenance-report" aria-busy={loading}>
        <header className="maintenance-report-header">
          <div>
            <div className="maintenance-report-brand">BIS</div>
            <div className="maintenance-report-brand-sub">CONSULTING</div>
          </div>
          <div className="maintenance-report-title">
            <Typography.Title level={2} style={{ margin: 0 }}>{snap?.title ?? 'GIS Maintenance Report'}</Typography.Title>
            <Typography.Text type="secondary">{snap?.monthLabel}</Typography.Text>
          </div>
          <div className="maintenance-report-contact">
            <div>Phone: {snap?.contact.phone}</div>
            <div><a href={`mailto:${snap?.contact.email}`}>{snap?.contact.email}</a></div>
            <div>{snap?.contact.department}</div>
          </div>
        </header>

        <p className="maintenance-report-meta">
          Dated: {report
            ? (snap?.cadence === 'Annual'
              ? dayjs(`${report.year}-12-31`).format('MMMM DD, YYYY')
              : dayjs(report.generatedAt).format('MMMM DD, YYYY'))
            : '—'}
        </p>
        <Typography.Title level={4} style={{ marginTop: 0 }}>{snap?.organizationName}</Typography.Title>
        <Typography.Paragraph>{snap?.intro}</Typography.Paragraph>
        {report && (
          <Typography.Paragraph type="secondary">
            Version {report.version}
            {report.generatedByName ? ` · Generated by ${report.generatedByName}` : ''}
            {report.emailed
              ? ` · Last emailed ${dayjs(report.lastEmailedAt).format('YYYY-MM-DD HH:mm')}`
              : ' · Not emailed yet'}
          </Typography.Paragraph>
        )}

        <div className={snap?.includeParcelStatus === false ? 'maintenance-report-grid maintenance-report-grid-annual' : 'maintenance-report-grid'}>
          {snap?.includeParcelStatus !== false && (
          <section>
            <h3>Parcel Status To Date</h3>
            <table className="maintenance-report-table">
              <tbody>
                <tr>
                  <th>Total Real Accounts</th>
                  <td>{formatCount(parcel?.totalRealAccounts, parcel?.available)}</td>
                </tr>
                <tr>
                  <th>Parcels With Ownership Information</th>
                  <td>{formatCount(parcel?.parcelsWithOwnership, parcel?.available)}</td>
                </tr>
                <tr>
                  <th>Missing Real Accounts</th>
                  <td>{formatCount(parcel?.missingRealAccounts, parcel?.available)}</td>
                </tr>
                <tr>
                  <th>Percent Complete</th>
                  <td>{parcel?.available && parcel.percentComplete != null ? `${parcel.percentComplete}%` : 'Not available'}</td>
                </tr>
              </tbody>
            </table>
            {!parcel?.available && (
              <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>
                {parcel?.note}
              </Typography.Paragraph>
            )}
          </section>
          )}

          <section>
            <h3>Total Maintenance Items Completed in {snap?.monthName}</h3>
            {pieData.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={`No completed maintenance items ${snap?.periodPhrase ?? 'this month'}.`} />
            ) : (
              <Pie
                data={pieData}
                angleField="value"
                colorField="type"
                height={isMobile ? 220 : 260}
                innerRadius={0.68}
                legend={{ color: { position: 'bottom' } }}
                scale={pieColors.length ? { color: { range: pieColors } } : undefined}
                annotations={[
                  {
                    type: 'text',
                    data: [],
                    style: {
                      text: `Total\n${snap?.completed ?? 0}`,
                      x: '50%',
                      y: '50%',
                      textAlign: 'center',
                      fontSize: 18,
                      fontWeight: 600,
                    },
                  },
                ]}
              />
            )}
          </section>
        </div>

        <section>
          <div className="maintenance-report-section-head">
            <h3>Completed Maintenance Items in {snap?.monthName}</h3>
            {report && (
              <Button type="link" onClick={() => void api.downloadReportCsv(report.id).catch((err) => setError(err.message))}>
                Download CSV file
              </Button>
            )}
          </div>
          {isMobile ? (
            <Space direction="vertical" size={8} style={{ width: '100%' }}>
              {(snap?.completedItems.length ?? 0) === 0 && (
                <Empty description={`No work items were completed ${snap?.periodPhrase ?? 'this month'}.`} />
              )}
              {snap?.completedItems.map((row) => (
                <Card key={`${row.fileName}-${row.uploadedAt}`} size="small" className="bis-theme-panel">
                  <Typography.Text strong>{row.fileName}</Typography.Text>
                  <div><Typography.Text type="secondary">Uploaded {dayjs(row.uploadedAt).format('MMM DD, YYYY')} · Worked {row.workedOn ? dayjs(row.workedOn).format('MMM DD, YYYY') : '—'}</Typography.Text></div>
                  <div>
                    <Typography.Text type="secondary">
                      Annexations {row.annexations} · Corrections {row.corrections} · Plats {row.plats} · Deeds {row.deeds} · Sketch {row.sketch ? 'Yes' : 'No'}
                    </Typography.Text>
                  </div>
                  {row.propertyIds && <div><Typography.Text type="secondary">Property Ids {row.propertyIds}</Typography.Text></div>}
                </Card>
              ))}
            </Space>
          ) : (
            <Table
              rowKey={(row) => `${row.fileName}-${row.uploadedAt}`}
              className="bis-theme-grid"
              size="small"
              pagination={false}
              dataSource={snap?.completedItems ?? []}
              scroll={{ x: 'max-content' }}
              locale={{ emptyText: `No work items were completed ${snap?.periodPhrase ?? 'this month'}.` }}
              columns={[
                { title: 'File Name', dataIndex: 'fileName', ellipsis: true },
                { title: 'Upload Date', dataIndex: 'uploadedAt', render: (v: string) => dayjs(v).format('MMM DD, YYYY') },
                { title: 'Worked Date', dataIndex: 'workedOn', render: (v: string | null) => (v ? dayjs(v).format('MMM DD, YYYY') : '—') },
                { title: 'Annexations', dataIndex: 'annexations', align: 'right' },
                { title: 'Corrections', dataIndex: 'corrections', align: 'right' },
                { title: 'Plats', dataIndex: 'plats', align: 'right' },
                { title: 'Deeds', dataIndex: 'deeds', align: 'right' },
                { title: 'Sketch', dataIndex: 'sketch', render: (v: boolean) => (v ? 'Yes' : 'No') },
                { title: 'Property Ids', dataIndex: 'propertyIds', ellipsis: true, render: (v: string) => v || '—' },
              ]}
            />
          )}
        </section>

        <footer className="maintenance-report-footer">
          <p>{snap?.closing}</p>
          <p><strong>Thank you,</strong><br />{snap?.contact.department}<br />{snap?.contact.company}</p>
        </footer>
      </div>

      {(report?.emails.length ?? 0) > 0 && (
        <Card title="Email log" size="small" className="bis-theme-panel">
          <Table
            rowKey="id"
            className="bis-theme-grid"
            size="small"
            pagination={false}
            dataSource={report?.emails ?? []}
            columns={[
              { title: 'When', dataIndex: 'sentAt', render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm') },
              { title: 'Recipients', dataIndex: 'recipients', ellipsis: true },
              {
                title: 'Result',
                key: 'result',
                render: (_, row) => row.delivered
                  ? <Tag color="green">Sent</Tag>
                  : <Tag color={row.mode === 'dry-run' ? 'gold' : 'red'}>{row.mode === 'dry-run' ? 'Dry-run' : 'Failed'}</Tag>,
              },
            ]}
          />
        </Card>
      )}

      <Modal
        title={(
          <TitleWithHelp help={`Recipients must belong to ${report?.organizationName ?? 'this client'}. Extra addresses are allowed. The message includes the HTML summary, a sign-in link, and the GIS Maintenance Report PDF. If SMTP is not configured, GIS Dashboard records a dry-run and does not mark the report as sent.`}>
            Email this GIS Maintenance Report
          </TitleWithHelp>
        )}
        open={emailOpen}
        onCancel={() => setEmailOpen(false)}
        onOk={() => void send()}
        confirmLoading={sending}
        okText="Send"
        destroyOnHidden
      >
        <Form form={form} layout="vertical" className="dense-form">
          <Form.Item name="userIds" label="People in this organization">
            <Select
              mode="multiple"
              placeholder="Select people"
              optionFilterProp="label"
              options={recipients.map((r) => ({ value: r.id, label: `${reportPersonLabel(r)} <${r.email}>` }))}
            />
          </Form.Item>
          <Form.Item name="extra" label="Additional email addresses">
            <Input.TextArea rows={2} placeholder="name@example.com, another@example.com" />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
