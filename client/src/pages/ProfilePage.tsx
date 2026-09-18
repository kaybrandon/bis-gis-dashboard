import { UploadOutlined } from '@ant-design/icons'
import { Avatar, Button, Card, Col, Descriptions, Form, Input, Row, Space, Typography, Upload, message } from 'antd'
import { useEffect, useState } from 'react'
import { authorizedBlob } from '../api'
import { api } from '../api'
import { accountTriggerLabel, orgScopeLabel } from '../accountLabel'
import { useAuth } from '../auth'

export function ProfilePage() {
  const { user, applyUser } = useAuth()
  const [form] = Form.useForm()
  const [saving, setSaving] = useState(false)
  const [photoUrl, setPhotoUrl] = useState<string | null>(null)
  const newPassword = Form.useWatch('newPassword', form)
  const confirmPassword = Form.useWatch('confirmPassword', form)

  useEffect(() => {
    if (!user) return
    form.setFieldsValue({
      userName: user.userName ?? user.displayName,
      fullName: user.fullName ?? '',
      email: user.email,
      workPhone: user.workPhone ?? '',
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    })
  }, [form, user])

  useEffect(() => {
    if (!user?.hasAvatar) {
      setPhotoUrl(null)
      return
    }
    let revoked = false
    authorizedBlob('/api/auth/me/avatar')
      .then((blob) => {
        if (revoked) return
        setPhotoUrl(URL.createObjectURL(blob))
      })
      .catch(() => {
        if (!revoked) setPhotoUrl(null)
      })
    return () => {
      revoked = true
    }
  }, [user?.hasAvatar, user?.id])

  useEffect(() => {
    return () => {
      if (photoUrl) URL.revokeObjectURL(photoUrl)
    }
  }, [photoUrl])

  if (!user) return null

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Typography.Title level={3} className="page-title" style={{ marginBottom: 0 }}>
        Profile
      </Typography.Title>
      <Card className="form-panel compact-card bis-theme-panel">
        <Form
          form={form}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            const changingPassword = !!(values.newPassword || values.confirmPassword || values.currentPassword)
            if (changingPassword && values.newPassword !== values.confirmPassword) {
              form.setFields([
                { name: 'confirmPassword', errors: ['New password and confirmation do not match.'] },
              ])
              return
            }
            setSaving(true)
            try {
              const next = await api.updateProfile({
                userName: values.userName,
                fullName: values.fullName || null,
                email: values.email,
                workPhone: values.workPhone || null,
                currentPassword: changingPassword ? values.currentPassword : undefined,
                newPassword: changingPassword ? values.newPassword : undefined,
                confirmPassword: changingPassword ? values.confirmPassword : undefined,
              })
              applyUser(next)
              form.setFieldsValue({ currentPassword: '', newPassword: '', confirmPassword: '' })
              message.success('Profile saved.')
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Could not save profile.')
            } finally {
              setSaving(false)
            }
          }}
        >
          <Space align="start" size={16} style={{ marginBottom: 12 }} wrap>
            <Avatar size={64} src={photoUrl || undefined}>
              {photoUrl ? null : accountTriggerLabel(user).slice(0, 1)}
            </Avatar>
            <Upload
              accept="image/jpeg,image/png,image/webp,image/gif"
              showUploadList={false}
              beforeUpload={async (file) => {
                if (file.size > 2 * 1024 * 1024) {
                  message.error('Photo must be 2 MB or less.')
                  return false
                }
                const body = new FormData()
                body.append('file', file)
                try {
                  const next = await api.uploadAvatar(body)
                  applyUser(next)
                  const blob = await authorizedBlob('/api/auth/me/avatar')
                  setPhotoUrl((current) => {
                    if (current) URL.revokeObjectURL(current)
                    return URL.createObjectURL(blob)
                  })
                  message.success('Photo updated.')
                } catch (err) {
                  message.error(err instanceof Error ? err.message : 'Could not upload photo.')
                }
                return false
              }}
            >
              <Button icon={<UploadOutlined />}>Upload photo</Button>
            </Upload>
          </Space>

          <Row gutter={[12, 0]}>
            <Col xs={24} sm={12}>
              <Form.Item name="userName" label="Username" extra="Used to sign in, along with email." rules={[{ required: true, message: 'Username is required.' }]}>
                <Input placeholder="arivera" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="fullName" label="Full name">
                <Input placeholder="Optional" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="email"
                label="Email"
                extra="You can sign in with username or email. Changing email does not change your username."
                rules={[{ required: true, type: 'email' }]}
              >
                <Input />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="workPhone" label="Work phone">
                <Input placeholder="Optional" inputMode="tel" />
              </Form.Item>
            </Col>
          </Row>

          <Descriptions bordered size="small" column={1} style={{ marginBottom: 12 }}>
            <Descriptions.Item label="Role">{user.roleDisplayName ?? user.role}</Descriptions.Item>
            <Descriptions.Item label="Organizations">{orgScopeLabel(user)}</Descriptions.Item>
          </Descriptions>

          <Typography.Title level={5} style={{ marginTop: 4 }}>Change password</Typography.Title>
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={8}>
              <Form.Item
                name="currentPassword"
                label="Current password"
                rules={[{
                  validator: async (_, value) => {
                    if ((newPassword || confirmPassword) && !value) {
                      throw new Error('Current password is required to set a new password.')
                    }
                  },
                }]}
              >
                <Input.Password autoComplete="current-password" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item
                name="newPassword"
                label="New password"
                rules={[{
                  validator: async (_, value) => {
                    if (!value) return
                    if (value.length < 8) throw new Error('New password must be at least 8 characters.')
                  },
                }]}
              >
                <Input.Password autoComplete="new-password" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item
                name="confirmPassword"
                label="Confirm new password"
                dependencies={['newPassword']}
                rules={[{
                  validator: async (_, value) => {
                    if (!newPassword && !value) return
                    if (value !== newPassword) throw new Error('New password and confirmation do not match.')
                  },
                }]}
              >
                <Input.Password autoComplete="new-password" />
              </Form.Item>
            </Col>
          </Row>
          <Button type="primary" htmlType="submit" loading={saving}>
            Save
          </Button>
        </Form>
      </Card>
    </Space>
  )
}
