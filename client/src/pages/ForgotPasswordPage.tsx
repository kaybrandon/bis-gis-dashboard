import { Alert, Button, Card, Form, Input, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { PoweredByFooter } from '../components/PoweredByFooter'

export function ForgotPasswordPage() {
  const [loading, setLoading] = useState(true)
  const [pending, setPending] = useState(false)
  const [available, setAvailable] = useState(true)
  const [notice, setNotice] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  useEffect(() => {
    api.passwordResetStatus()
      .then((status) => {
        setAvailable(status.available)
        if (!status.available) setNotice(status.message)
      })
      .catch((err) => {
        setAvailable(false)
        setNotice(err instanceof Error ? err.message : 'Password reset isn’t available yet — contact your admin.')
      })
      .finally(() => setLoading(false))
  }, [])

  return (
    <div className="login-wrap">
      <Card className="login-card compact-card bis-theme-panel">
        <Typography.Title level={3} style={{ marginBottom: 4 }}>Forgot password</Typography.Title>
        <Typography.Paragraph type="secondary" className="page-lead">
          Enter the email or username for your GIS Dashboard account.
        </Typography.Paragraph>
        {notice && (
          <Alert
            type={sent ? 'success' : available ? 'info' : 'warning'}
            showIcon
            message={notice}
            style={{ marginBottom: 12 }}
          />
        )}
        {!sent && available && (
          <Form
            layout="vertical"
            className="dense-form"
            onFinish={async (values: { emailOrUsername: string }) => {
              setPending(true)
              try {
                const result = await api.forgotPassword(values.emailOrUsername)
                setSent(true)
                setNotice(result.message)
              } catch (err) {
                setNotice(err instanceof Error ? err.message : 'Password reset isn’t available yet — contact your admin.')
                setAvailable(false)
              } finally {
                setPending(false)
              }
            }}
          >
            <Form.Item name="emailOrUsername" label="Email or username" rules={[{ required: true, message: 'Enter your email or username.' }]}>
              <Input autoComplete="username" placeholder="arivera or you@client.example" disabled={pending || loading} />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={pending} disabled={loading}>
              Send reset link
            </Button>
          </Form>
        )}
        <div className="login-forgot">
          <Link to="/login">Back to sign in</Link>
        </div>
      </Card>
      <PoweredByFooter onDark />
    </div>
  )
}
