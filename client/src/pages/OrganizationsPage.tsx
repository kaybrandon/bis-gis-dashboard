import { CopyOutlined, FileTextOutlined, LinkOutlined } from '@ant-design/icons'
import { Button, Card, Col, Descriptions, Form, Input, InputNumber, Modal, Row, Select, Space, Switch, Table, Tag, Typography, message } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useState, type MouseEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import type { UploadLink } from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'

type AssignedTech = { id: string; displayName: string; isPrimary?: boolean }

type UserRow = {
  id: string
  displayName: string
  role?: string
  isActive?: boolean
  isArchived?: boolean
  organizations?: Array<{ organizationId: string }>
}

type OrgRow = {
  id: string
  name: string
  code: string
  isActive: boolean
  createdAt: string
  hasUploadToken?: boolean
  parcelTotalRealAccounts?: number | null
  parcelWithOwnership?: number | null
  timeReportCardsVisible?: boolean
  assignedTechs?: AssignedTech[]
  members?: AssignedTech[]
}

function canAssignAsTech(user: UserRow) {
  if (user.isActive === false) return false
  if (user.isArchived) return false
  if (!user.role) return true
  return user.role === 'GlobalAdministrator' || user.role === 'Editor' || user.role === 'Administrator'
}

function techOptionLabel(user: UserRow) {
  if (!user.role) return user.displayName
  return `${user.displayName} (${user.role === 'GlobalAdministrator' ? 'Global Administrator' : user.role})`
}

function names(people?: AssignedTech[]) {
  return people?.length
    ? people.map((person) => person.isPrimary ? `${person.displayName} (Primary)` : person.displayName).join(', ')
    : '—'
}

function stopRowClick(event: MouseEvent) {
  event.stopPropagation()
}

export function OrganizationsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [rows, setRows] = useState<OrgRow[]>([])
  const [addOpen, setAddOpen] = useState(false)
  const [viewing, setViewing] = useState<OrgRow | null>(null)
  const [editing, setEditing] = useState<OrgRow | null>(null)
  const [linkOrg, setLinkOrg] = useState<OrgRow | null>(null)
  const [link, setLink] = useState<UploadLink | null>(null)
  const [linkLoading, setLinkLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [users, setUsers] = useState<UserRow[]>([])
  const [addForm] = Form.useForm()
  const [editForm] = Form.useForm()
  const assignedTechIds = Form.useWatch('assignedTechIds', editForm) as string[] | undefined
  const canGlobal = !!user?.canManageGlobalDirectory
  const canDirectory = !!user?.canManageDirectory
  const canAssignTechs = !!(user?.canManageAssignedTechs ?? user?.canManageDirectory)

  const load = () => {
    const jobs: Promise<unknown>[] = [
      api.adminOrgs().then((data) => setRows(data as OrgRow[])),
    ]
    if (canDirectory) {
      jobs.push(api.adminUsers().then((data) => setUsers(data as UserRow[])))
    } else if (canAssignTechs) {
      jobs.push(api.assignees().then((data) => setUsers(data.map((row) => ({
        id: row.id,
        displayName: row.displayName,
        isActive: true,
      })))))
    }
    Promise.all(jobs)
      .then(() => setError(null))
      .catch((err) => setError(err instanceof Error ? err.message : 'Could not load organizations.'))
  }

  useEffect(() => { load() }, [])

  const openView = (row: OrgRow) => setViewing(row)

  const openEdit = (row: OrgRow) => {
    setViewing(null)
    setEditing(row)
    editForm.setFieldsValue({
      ...row,
      assignedTechIds: row.assignedTechs?.map((tech) => tech.id) ?? [],
      primaryAssignedTechId: row.assignedTechs?.find((tech) => tech.isPrimary)?.id,
    })
  }

  const openReports = (row: OrgRow) => {
    navigate(`/reports?organizationId=${encodeURIComponent(row.id)}`)
  }

  const openLink = async (row: OrgRow) => {
    setLinkOrg(row)
    setLinkLoading(true)
    try {
      setLink(await api.orgUploadLink(row.id))
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not load the upload link.')
      setLinkOrg(null)
    } finally {
      setLinkLoading(false)
    }
  }

  const copyLink = async (path: string) => {
    const url = `${window.location.origin}${path}`
    await navigator.clipboard.writeText(url)
    message.success('Upload link copied.')
  }

  return (
    <Card
      title={(
        <TitleWithHelp help="Client organizations. Runtime isolation is per organization — this app does not call BIS Admin, FTP, or COLO. Each client has a no-login URL that only accepts file uploads. Click a name to view details, or open that client's GIS Maintenance Reports.">
          Organizations
        </TitleWithHelp>
      )}
      extra={canGlobal ? (
        <Button type="primary" onClick={() => setAddOpen(true)}>Add organization</Button>
      ) : null}
      styles={{ header: { flexWrap: 'wrap', gap: 8 } }}
    >
      {error && <LoadError message={error} onRetry={() => load()} />}
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content' }}
        onRow={(row) => ({
          onClick: () => openView(row),
          style: { cursor: 'pointer' },
        })}
        columns={[
          {
            title: 'Name',
            dataIndex: 'name',
            render: (name: string, row: OrgRow) => (
              <Button type="link" style={{ padding: 0, height: 'auto' }} onClick={(event) => { stopRowClick(event); openView(row) }}>
                {name}
              </Button>
            ),
          },
          { title: 'Code', dataIndex: 'code' },
          {
            title: 'Active',
            dataIndex: 'isActive',
            render: (value: boolean) => <Tag color={value ? 'green' : 'default'}>{value ? 'Active' : 'Inactive'}</Tag>,
          },
          { title: 'Created', dataIndex: 'createdAt', render: (value: string) => dayjs(value).format('YYYY-MM-DD') },
          {
            title: 'Assigned tech(s)',
            key: 'assignedTechs',
            render: (_: unknown, row: OrgRow) => names(row.assignedTechs),
          },
          {
            title: '',
            key: 'actions',
            render: (_, row) => (
              <div onClick={stopRowClick}>
                <Space wrap>
                  <Button size="small" icon={<FileTextOutlined />} onClick={() => openReports(row)}>
                    Reports
                  </Button>
                  <Button size="small" onClick={() => openEdit(row)}>
                    Edit
                  </Button>
                  {canDirectory ? (
                    <Button size="small" icon={<LinkOutlined />} onClick={() => void openLink(row)}>
                      Upload link
                    </Button>
                  ) : null}
                </Space>
              </div>
            ),
          },
        ]}
      />

      <Modal title="Add organization" open={addOpen} onCancel={() => setAddOpen(false)} onOk={() => addForm.submit()} destroyOnHidden width="min(520px, calc(100vw - 24px))">
        <Form
          form={addForm}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            try {
              await api.createOrg(values.name, values.code)
              message.success('Organization created.')
              setAddOpen(false)
              addForm.resetFields()
              load()
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Create failed.')
            }
          }}
        >
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={14}>
              <Form.Item name="name" label="Organization Name" rules={[{ required: true }]}><Input placeholder="Demo Client" /></Form.Item>
            </Col>
            <Col xs={24} sm={10}>
              <Form.Item
                name="code"
                label="Code"
                tooltip="ClientName — all together lowercase"
                rules={[{ required: true }]}
              >
                <Input placeholder="alexcad" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Modal>

      <Modal
        title={viewing ? viewing.name : 'Client'}
        open={!!viewing}
        onCancel={() => setViewing(null)}
        footer={viewing ? (
          <Space wrap>
            <Button onClick={() => setViewing(null)}>Close</Button>
            <Button icon={<FileTextOutlined />} onClick={() => openReports(viewing)}>Reports</Button>
            {canDirectory ? (
              <Button icon={<LinkOutlined />} onClick={() => { const row = viewing; setViewing(null); void openLink(row) }}>
                Upload link
              </Button>
            ) : null}
            <Button type="primary" onClick={() => openEdit(viewing)}>Edit</Button>
          </Space>
        ) : null}
        destroyOnHidden
        width="min(560px, calc(100vw - 24px))"
      >
        {viewing && (
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="Client">{viewing.name}</Descriptions.Item>
            <Descriptions.Item label="Code">{viewing.code}</Descriptions.Item>
            <Descriptions.Item label="Active">
              <Tag color={viewing.isActive ? 'green' : 'default'}>{viewing.isActive ? 'Active' : 'Inactive'}</Tag>
            </Descriptions.Item>
            <Descriptions.Item label="Created">{dayjs(viewing.createdAt).format('YYYY-MM-DD')}</Descriptions.Item>
            <Descriptions.Item label="Time report cards">
              {viewing.timeReportCardsVisible ? 'Client can view' : 'Staff only'}
            </Descriptions.Item>
            <Descriptions.Item label="Members">
              {viewing.members?.length ? names(viewing.members) : 'None assigned'}
            </Descriptions.Item>
            <Descriptions.Item label="Assigned tech(s)">
              {viewing.assignedTechs?.length ? names(viewing.assignedTechs) : 'None assigned'}
            </Descriptions.Item>
            <Descriptions.Item label="Total real accounts">
              {viewing.parcelTotalRealAccounts ?? '—'}
            </Descriptions.Item>
            <Descriptions.Item label="Parcels with ownership">
              {viewing.parcelWithOwnership ?? '—'}
            </Descriptions.Item>
          </Descriptions>
        )}
      </Modal>

      <Modal title="Edit organization" open={!!editing} onCancel={() => setEditing(null)} onOk={() => editForm.submit()} destroyOnHidden width="min(560px, calc(100vw - 24px))">
        <Form
          form={editForm}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            if (!editing) return
            try {
              await api.updateOrg(editing.id, {
                name: values.name,
                code: canGlobal ? values.code : undefined,
                isActive: canGlobal ? values.isActive : undefined,
                parcelTotalRealAccounts: canDirectory ? values.parcelTotalRealAccounts ?? null : undefined,
                parcelWithOwnership: canDirectory ? values.parcelWithOwnership ?? null : undefined,
                timeReportCardsVisible: canGlobal ? !!values.timeReportCardsVisible : undefined,
                assignedTechIds: canAssignTechs ? values.assignedTechIds ?? [] : undefined,
                primaryAssignedTechId: canAssignTechs ? values.primaryAssignedTechId ?? null : undefined,
              })
              message.success('Organization saved.')
              setEditing(null)
              load()
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Save failed.')
            }
          }}
        >
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={14}>
              <Form.Item name="name" label="Organization Name" rules={[{ required: canDirectory }]}><Input disabled={!canDirectory} /></Form.Item>
            </Col>
            <Col xs={24} sm={10}>
              <Form.Item
                name="code"
                label="Code"
                tooltip="ClientName — all together lowercase"
                rules={[{ required: canGlobal }]}
              >
                <Input placeholder="alexcad" disabled={!canGlobal} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="isActive" label="Active" valuePropName="checked">
                <Switch disabled={!canGlobal} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="timeReportCardsVisible"
                label="Client time report cards"
                valuePropName="checked"
                tooltip="Viewers see cards when on. Staff always see their own hours."
              >
                <Switch disabled={!canGlobal} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="parcelTotalRealAccounts"
                label="Total real accounts"
                tooltip="Optional parcel inventory. Not a live extract yet."
              >
                <InputNumber min={0} style={{ width: '100%' }} disabled={!canDirectory} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="parcelWithOwnership" label="Parcels with ownership">
                <InputNumber min={0} style={{ width: '100%' }} disabled={!canDirectory} />
              </Form.Item>
            </Col>
          </Row>
          {canAssignTechs ? (
            <Form.Item
              name="assignedTechIds"
              label="Assigned tech(s)"
              tooltip="Staff assigned to this client. Adding someone here also adds the organization on Users. Editors, Administrators, and Global Administrators only. Notified on priority work."
            >
              <Select
                mode="multiple"
                allowClear
                optionFilterProp="label"
                placeholder="Select one or more technicians"
                options={users
                  .filter((row) => canAssignAsTech(row))
                  .map((row) => ({
                    value: row.id,
                    label: techOptionLabel(row),
                  }))}
              />
            </Form.Item>
          ) : null}
          {canAssignTechs ? (
            <Form.Item
              name="primaryAssignedTechId"
              label="Primary assigned tech"
              tooltip="Default assignee for new uploads when no one else is chosen. If none is set, the first Assigned tech is primary."
            >
              <Select
                allowClear
                optionFilterProp="label"
                placeholder="Default technician for new work"
                options={users
                  .filter((row) => canAssignAsTech(row) && (assignedTechIds?.length ? assignedTechIds.includes(row.id) : true))
                  .map((row) => ({
                    value: row.id,
                    label: row.displayName,
                  }))}
              />
            </Form.Item>
          ) : (
            <Form.Item
              label="Assigned tech(s)"
              tooltip="Primary GIS contact(s) for this client. An Administrator or Editor sets this on the organization."
            >
              <Typography.Text>
                {editing?.assignedTechs?.length
                  ? editing.assignedTechs.map((tech) => tech.displayName).join(', ')
                  : 'None assigned'}
              </Typography.Text>
            </Form.Item>
          )}
          {!canGlobal && (
            <Typography.Paragraph type="secondary">
              {canDirectory
                ? 'Code, active status, and the time-report-card toggle can only be changed by a Global Administrator.'
                : 'Name, code, active status, parcel counts, and the time-report-card toggle stay with a Global Administrator or Administrator.'}
            </Typography.Paragraph>
          )}
        </Form>
      </Modal>

      <Modal
        title={(
          <TitleWithHelp help="Anyone with this URL can upload files into this organization without signing in. They cannot see other clients, users, or work items. Regenerating the token invalidates the previous link.">
            {linkOrg ? `Upload link — ${linkOrg.name}` : 'Upload link'}
          </TitleWithHelp>
        )}
        open={!!linkOrg}
        onCancel={() => { setLinkOrg(null); setLink(null) }}
        footer={null}
        loading={linkLoading}
      >
        {link && (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Input
              readOnly
              value={`${window.location.origin}${link.path}`}
              addonAfter={(
                <CopyOutlined
                  onClick={() => void copyLink(link.path)}
                  style={{ cursor: 'pointer' }}
                />
              )}
            />
            <Space wrap>
              <Button icon={<CopyOutlined />} onClick={() => void copyLink(link.path)}>Copy link</Button>
              <Button
                icon={<LinkOutlined />}
                href={`${window.location.origin}${link.path}`}
                target="_blank"
                rel="noopener"
              >
                Open link
              </Button>
              <Button
                danger
                onClick={async () => {
                  if (!linkOrg) return
                  try {
                    const next = await api.regenerateOrgUploadLink(linkOrg.id)
                    setLink(next)
                    message.success('Previous upload link is no longer valid.')
                  } catch (err) {
                    message.error(err instanceof Error ? err.message : 'Could not regenerate.')
                  }
                }}
              >
                Regenerate
              </Button>
            </Space>
          </Space>
        )}
      </Modal>
    </Card>
  )
}
