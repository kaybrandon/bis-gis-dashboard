import { Button, Card, Col, Form, Input, Modal, Row, Select, Switch, Table, Tag, message } from 'antd'
import { useEffect, useState } from 'react'
import { api } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { roleHasAllOrganizations, roleLabel, roleRequiresOrganizationAssignment } from '../roles'

type UserRow = {
  id: string
  email: string
  displayName: string
  fullName?: string | null
  workPhone?: string | null
  role: string
  isActive: boolean
  organizations: Array<{ organizationId: string; organizationName: string }>
}

export function UsersPage() {
  const { user } = useAuth()
  const [rows, setRows] = useState<UserRow[]>([])
  const [orgs, setOrgs] = useState<Array<{ id: string; name: string }>>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<UserRow | null>(null)
  const [error, setError] = useState<string | null>(null)
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

  const load = () => {
    Promise.all([api.adminUsers(), api.adminOrgs()])
      .then(([users, organizations]) => {
        setRows(users as UserRow[])
        setOrgs(organizations as Array<{ id: string; name: string }>)
        setError(null)
      })
      .catch((err) => setError(err.message))
  }

  useEffect(() => { load() }, [])

  return (
    <Card
      title={(
        <TitleWithHelp help="Global Administrator, Administrator, and Editor see and work documents from every organization. New Editor and Administrator accounts receive every current organization automatically, including organizations added later. Uploader is Viewer visibility plus uploads and client-visible Comments on assigned orgs. Viewer stays read-only for upload and comments. There is no Client role — a client is an organization.">
          Users & organization assignments
        </TitleWithHelp>
      )}
      extra={<Button type="primary" onClick={() => setOpen(true)}>Add User</Button>}
      styles={{ header: { flexWrap: 'wrap', gap: 8 } }}
    >
      {error && <LoadError message={error} onRetry={() => load()} />}
      <Table
        rowKey="id"
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content' }}
        columns={[
          { title: 'Username', dataIndex: 'displayName' },
          { title: 'Full name', dataIndex: 'fullName', render: (value?: string | null) => value || '—' },
          { title: 'Email', dataIndex: 'email' },
          { title: 'Role', dataIndex: 'role', render: (value: string) => <Tag>{roleLabel(value)}</Tag> },
          {
            title: 'Organizations',
            render: (_, row) => roleHasAllOrganizations(row.role)
              ? 'All organizations'
              : row.organizations.map((o) => o.organizationName).join(', ') || '—',
          },
          {
            title: 'Active',
            dataIndex: 'isActive',
            render: (value: boolean) => <Tag color={value ? 'green' : 'default'}>{value ? 'Active' : 'Inactive'}</Tag>,
          },
          {
            title: '',
            key: 'actions',
            render: (_, row) => (
              <Button
                size="small"
                onClick={() => {
                  setEditing(row)
                  editForm.setFieldsValue({
                    displayName: row.displayName,
                    fullName: row.fullName ?? '',
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
