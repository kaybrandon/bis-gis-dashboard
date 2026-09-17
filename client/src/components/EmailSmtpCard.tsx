import { Button, Card, Col, Form, Input, InputNumber, Modal, Row, Space, Switch, Tag, Typography, message } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import type { EmailSettings } from '../api'
import { api } from '../api'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'

export function EmailSmtpCard() {
  const [form] = Form.useForm()
  const [settings, setSettings] = useState<EmailSettings | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [testOpen, setTestOpen] = useState(false)
  const [testTo, setTestTo] = useState('')

  const apply = useCallback((next: EmailSettings) => {
    setSettings(next)
    form.setFieldsValue({
      host: next.host ?? '',
      port: next.port || 587,
      useSsl: next.useSsl,
      from: next.from ?? '',
      fromName: next.fromName ?? '',
      user: next.user ?? '',
      password: '',
      replyTo: next.replyTo ?? '',
    })
  }, [form])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      apply(await api.emailSettings())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Email settings failed to load.')
    } finally {
      setLoading(false)
    }
  }, [apply])

  useEffect(() => {
    void load()
  }, [load])

  const configured = !!settings?.configured
  const passwordConfigured = !!settings?.passwordConfigured

  return (
    <Card
      className="compact-card form-panel"
      style={{ maxWidth: 720 }}
      loading={loading && !settings}
      title={(
        <Space wrap>
          <TitleWithHelp help="SMTP for Dashboard Send report, GIS Maintenance Report email, and the test message. The password is write-only — the browser never receives it. Prefer a Key Vault secret for the live App Service.">
            Email / SMTP
          </TitleWithHelp>
          <Tag color={configured ? 'green' : 'default'}>{configured ? 'Configured' : 'Not configured'}</Tag>
        </Space>
      )}
    >
      {error && <LoadError message={error} onRetry={() => void load()} />}
      {settings && (
        <Form
          form={form}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            setSaving(true)
            try {
              const next = await api.saveEmailSettings({
                host: values.host?.trim() || null,
                port: values.port,
                useSsl: !!values.useSsl,
                from: values.from?.trim() || null,
                fromName: values.fromName?.trim() || null,
                user: values.user?.trim() || null,
                password: values.password?.trim() || null,
                replyTo: values.replyTo?.trim() || null,
              })
              apply(next)
              message.success(next.configured ? 'SMTP saved.' : 'SMTP saved. Add a host and From address to enable sending.')
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Could not save SMTP.')
            } finally {
              setSaving(false)
            }
          }}
        >
          <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
            {settings.note}
            {passwordConfigured
              ? ` Password is ${settings.passwordSource === 'key-vault' ? 'from Key Vault' : settings.passwordSource === 'app-settings' ? 'from App Settings' : 'configured'}. Leave the field blank to keep it.`
              : ' Password is not set.'}
          </Typography.Paragraph>
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={16}>
              <Form.Item name="host" label="Host" rules={[{ required: true, message: 'Enter the SMTP host.' }]}>
                <Input placeholder="smtp.office365.com" disabled={saving} autoComplete="off" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={8}>
              <Form.Item name="port" label="Port" rules={[{ required: true, message: 'Enter the port.' }]}>
                <InputNumber min={1} max={65535} style={{ width: '100%' }} disabled={saving} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="useSsl" label="Use TLS/SSL" valuePropName="checked">
                <Switch disabled={saving} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="from"
                label="From address"
                rules={[{ required: true, type: 'email', message: 'Enter a valid From address.' }]}
              >
                <Input placeholder="noreply@bisconsultants.com" disabled={saving} autoComplete="off" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="fromName" label="From display name">
                <Input placeholder="GIS Dashboard" disabled={saving} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="user" label="Username">
                <Input placeholder="SMTP username" disabled={saving} autoComplete="off" />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="password"
                label="Password"
                extra={passwordConfigured ? 'Configured — leave blank to keep the current password.' : 'Not set. The value is never shown again.'}
              >
                <Input.Password
                  placeholder={passwordConfigured ? 'Configured — leave blank to keep' : 'Not set'}
                  disabled={saving}
                  autoComplete="new-password"
                />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="replyTo" label="Reply-to" rules={[{ type: 'email', message: 'Enter a valid reply-to address.' }]}>
                <Input placeholder="Optional" disabled={saving} />
              </Form.Item>
            </Col>
          </Row>
          <Space wrap>
            <Button type="primary" htmlType="submit" loading={saving}>
              Save
            </Button>
            <Button
              disabled={saving || !configured}
              loading={testing}
              onClick={() => {
                setTestTo('')
                setTestOpen(true)
              }}
            >
              Send test email
            </Button>
          </Space>
        </Form>
      )}

      <Modal
        title="Send a test email"
        open={testOpen}
        okText="Continue"
        confirmLoading={testing}
        onCancel={() => setTestOpen(false)}
        onOk={() => {
          const to = testTo.trim()
          if (!to || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(to)) {
            message.error('Enter a valid To address.')
            return Promise.reject()
          }
          return new Promise<void>((resolve, reject) => {
            Modal.confirm({
              title: 'Send this test email?',
              content: `Send a short SMTP test to ${to}.`,
              okText: 'Send test',
              onOk: async () => {
                setTesting(true)
                try {
                  const result = await api.sendTestEmail(to)
                  setTestOpen(false)
                  message.success(result.note || `Test email sent to ${to}.`)
                  resolve()
                } catch (err) {
                  message.error(err instanceof Error ? err.message : 'Test email failed.')
                  reject()
                } finally {
                  setTesting(false)
                }
              },
              onCancel: () => reject(),
            })
          })
        }}
      >
        <Typography.Paragraph type="secondary">
          The message uses the saved SMTP settings. Confirm the address before it sends.
        </Typography.Paragraph>
        <Input
          value={testTo}
          onChange={(event) => setTestTo(event.target.value)}
          placeholder="you@bisconsultants.com"
          aria-label="Test email To address"
        />
      </Modal>
    </Card>
  )
}
