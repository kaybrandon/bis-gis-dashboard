import { Button, Card, Col, Form, Input, Modal, Row, Select, Space, Switch, Table, Tag, Tooltip, message } from 'antd'
import { useEffect, useState } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { compareLastActivity, compareTitle, formatLastActivity } from '../lastActivity'
import { roleHasAllOrganizations, roleLabel, roleRequiresOrganizationAssignment } from '../roles'

type UserRow = {
  id: string
  email: string
  displayName: string
  fullName?: string | null
  title?: string | null
  workPhone?: string | null
  role: string
  isActive: boolean
  isArchived?: boolean
  lastLoginAt?: string | null
  organizations: Array<{ organizationId: string; organizationName: string }>
}

const ARCHIVE_CONFIRM = 'Archive this user? They can’t sign in or be newly assigned. Document history stays.'

function personName(row: UserRow) {
  return row.fullName?.trim() || row.displayName
}

export function UsersPage() {
  const { user } = useAuth()
  const [rows, setRows] = useState<UserRow[]>([])
  const [orgs, setOrgs] = useState<Array<{ id: string; name: string }>>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<UserRow | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showArchived, setShowArchived] = useState(false)
  const [addForm] = Form.useForm()
  const [editForm] = Form.useForm()
  const addRole = Form.useWatch('role', addForm)
  const editRole = Form.useWatch('role', editForm)

  const roleOptions = [
    ...(user?.canManageGlobalDirectory ? [{ value: 'GlobalAdministrator', label: 'Global Administrator' }] : []),
    { value: 'Administrator', label: 'Administrator' },
    { value: 'Editor', label: 'Editor' },
    { value: 'Uploader', label: 'Uploader' },
    { value: 'Viewer', label: 'Viewer' },
  ]

  const load = (includeArchived = showArchived) => {
    Promise.all([api.adminUsers(includeArchived), api.adminOrgs()])
      .then(([users, organizations]) => {
        setRows(users as UserRow[])
        setOrgs(organizations as Array<{ id: string; name: string }>)
        setError(null)
      })
      .catch((err) => setError(err.message))
  }

  useEffect(() => { load(showArchived) }, [showArchived])

  const confirmArchive = (row: UserRow) => {
    Modal.confirm({
      title: 'Archive this user?',
      content: ARCHIVE_CONFIRM,
      okText: 'Archive',
      okButtonProps: { danger: true },
      cancelText: 'Cancel',
      onOk: async () => {
        try {
          await api.archiveUser(row.id)
          message.success('User archived.')
          load()
        } catch (err) {
          message.error(err instanceof Error ? err.message : 'Archive failed.')
          throw err
        }
      },
    })
  }

  const restoreUser = async (row: UserRow) => {
    try {
      await api.restoreUser(row.id)
      message.success('User restored.')
      load()
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Restore failed.')
    }
  }

  return (
    <Card
      title={(
        <TitleWithHelp help="Global Administrator, Administrator, and Editor see and work documents from every organization. Uploader is Viewer visibility plus uploads and client-visible Comments on assigned orgs. Viewer stays read-only for upload and comments. There is no Client role — a client is an organization. Archive soft-deletes a user: they cannot sign in or be newly assigned, and document history still shows their name.">
          Users & organization assignments
        </TitleWithHelp>
      )}
      extra={(
        <Space wrap size={8}>
          <label className="users-show-archived">
            <Switch
              size="small"
              checked={showArchived}
              onChange={(checked) => setShowArchived(checked)}
            />
            <span>Show archived</span>
          </label>
          <Button type="primary" onClick={() => setOpen(true)}>Add User</Button>
        </Space>
      )}
      styles={{ header: { flexWrap: 'wrap', gap: 8 } }}
    >
      {error && <LoadError message={error} onRetry={() => load()} />}
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content' }}
        rowClassName={(row) => row.isArchived ? 'users-row-archived' : ''}
        columns={[
          {
            title: 'Name',
            key: 'name',
            sorter: (a, b) => compareTitle(personName(a), personName(b)),
            render: (_, row) => (
              <span>
                {personName(row)}
                {row.isArchived ? ' (archived)' : ''}
              </span>
            ),
          },
          { title: 'Username', dataIndex: 'displayName' },
          {
            title: 'Title',
            dataIndex: 'title',
            key: 'title',
            sorter: (a, b) => compareTitle(a.title, b.title),
            render: (value?: string | null) => value?.trim() || '—',
          },
          { title: 'Email', dataIndex: 'email' },
          {
            title: 'Role',
            dataIndex: 'role',
            key: 'role',
            sorter: (a, b) => roleLabel(a.role).localeCompare(roleLabel(b.role)),
            render: (value: string) => <Tag>{roleLabel(value)}</Tag>,
          },
          {
            title: 'Organizations',
            render: (_, row) => roleHasAllOrganizations(row.role)
              ? 'All organizations'
              : row.organizations.map((o) => o.organizationName).join(', ') || '—',
          },
          {
            title: 'Last activity',
            key: 'lastActivity',
            dataIndex: 'lastLoginAt',
            sorter: (a, b) => compareLastActivity(a.lastLoginAt, b.lastLoginAt),
            render: (value?: string | null) => {
              const display = formatLastActivity(value)
              const label = <span>{display.label}</span>
              return display.tooltip
                ? <Tooltip title={display.tooltip}>{label}</Tooltip>
                : label
            },
          },
          {
            title: 'Active',
            dataIndex: 'isActive',
            render: (_value: boolean, row) => row.isArchived
              ? <Tag>Archived</Tag>
              : <Tag color={row.isActive ? 'green' : 'default'}>{row.isActive ? 'Active' : 'Inactive'}</Tag>,
          },
          {
            title: '',
            key: 'actions',
            render: (_, row) => (
              <Space size={4}>
                <Button
                  size="small"
                  onClick={() => {
                    setEditing(row)
                    editForm.setFieldsValue({
                      displayName: row.displayName,
                      fullName: row.fullName ?? '',
                      title: row.title ?? '',
                      workPhone: row.workPhone ?? '',
                      email: row.email,
                      role: row.role,
                      isActive: row.isActive,
                      organizationIds: row.organizations.map((o) => o.organizationId),
                      password: undefined,
                    })
                  }}
                >
                  Edit
                </Button>
                {row.isArchived ? (
                  <Button size="small" onClick={() => restoreUser(row)}>Restore</Button>
                ) : (
                  <Button
                    size="small"
                    danger
                    disabled={row.id === user?.id}
                    onClick={() => confirmArchive(row)}
                  >
                    Archive
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />
      <Modal
        title="Add User"
        open={open}
        onCancel={() => setOpen(false)}
        onOk={() => addForm.submit()}
        destroyOnHidden
        width="min(520px, calc(100vw - 24px))"
      >
        <Form
          form={addForm}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            try {
              await api.createUser({
                ...values,
                organizationIds: roleRequiresOrganizationAssignment(values.role) ? values.organizationIds : [],
              })
              message.success('User created.')
              setOpen(false)
              addForm.resetFields()
              load()
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Create failed.')
            }
          }}
        >
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={12}>
              <Form.Item name="displayName" label="Username" extra="Used to sign in, along with email." rules={[{ required: true }]}><Input placeholder="arivera" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="fullName" label="Full name"><Input placeholder="Optional" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="title" label="Title"><Input placeholder="Optional job title" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="workPhone" label="Work phone"><Input placeholder="Optional" inputMode="tel" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="password" label="Password" rules={[{ required: true, min: 8 }]}><Input.Password /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="role" label="Role" rules={[{ required: true }]}>
                <Select options={roleOptions} />
              </Form.Item>
            </Col>
          </Row>
          {roleRequiresOrganizationAssignment(addRole) && (
            <Form.Item
              name="organizationIds"
              label="Organizations"
              rules={[{ required: true, type: 'array', min: 1 }]}
            >
              <Select mode="multiple" options={orgs.map((o) => ({ value: o.id, label: o.name }))} />
            </Form.Item>
          )}
          {addRole && !roleRequiresOrganizationAssignment(addRole) && addRole !== 'GlobalAdministrator' && (
            <Form.Item label="Organizations">
              <span>Associated with every organization automatically.</span>
            </Form.Item>
          )}
        </Form>
      </Modal>

      <Modal
        title="Edit User"
        open={!!editing}
        onCancel={() => setEditing(null)}
        onOk={() => editForm.submit()}
        destroyOnHidden
        width="min(520px, calc(100vw - 24px))"
      >
        <Form
          form={editForm}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            if (!editing) return
            try {
              await api.updateUser(editing.id, {
                displayName: values.displayName,
                fullName: values.fullName || null,
                title: values.title ?? '',
                workPhone: values.workPhone || null,
                email: values.email,
                role: values.role,
                organizationIds: roleRequiresOrganizationAssignment(values.role) ? values.organizationIds : [],
                isActive: values.isActive,
                password: values.password || undefined,
              })
              message.success('User saved.')
              setEditing(null)
              load()
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Save failed.')
            }
          }}
        >
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={12}>
              <Form.Item name="displayName" label="Username" extra="Used to sign in, along with email." rules={[{ required: true }]}><Input placeholder="arivera" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="fullName" label="Full name"><Input placeholder="Optional" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="title" label="Title"><Input placeholder="Optional job title" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="email" label="Email" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="workPhone" label="Work phone"><Input placeholder="Optional" inputMode="tel" /></Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="password" label="New password">
                <Input.Password placeholder="Leave blank to keep the current password" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="role" label="Role" rules={[{ required: true }]}>
                <Select options={roleOptions} />
              </Form.Item>
            </Col>
          </Row>
          {roleRequiresOrganizationAssignment(editRole) && (
            <Form.Item
              name="organizationIds"
              label="Organizations"
              rules={[{ required: true, type: 'array', min: 1 }]}
            >
              <Select mode="multiple" options={orgs.map((o) => ({ value: o.id, label: o.name }))} />
            </Form.Item>
          )}
          {editRole && !roleRequiresOrganizationAssignment(editRole) && editRole !== 'GlobalAdministrator' && (
            <Form.Item label="Organizations">
              <span>Associated with every organization automatically.</span>
            </Form.Item>
          )}
          <Form.Item name="isActive" label="Active" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}
