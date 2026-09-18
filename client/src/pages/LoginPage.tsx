import { Alert, Button, Card, Form, Input, Typography } from 'antd'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { useAuth } from '../auth'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  return (
    <div className="login-wrap">
      <Card className="login-card compact-card bis-theme-panel" title="GIS Dashboard">
        <Typography.Paragraph type="secondary" className="page-lead">
          Sign in with your username or email. Global Administrators see every client; other users stay inside their assigned organization.
        </Typography.Paragraph>
        {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 12 }} />}
        <Form
          layout="vertical"
          className="dense-form"
          onFinish={async (values: { email: string; password: string }) => {
            setPending(true)
            setError(null)
            try {
              await login(values.email, values.password)
              navigate('/')
            } catch (err) {
              setError(err instanceof Error ? err.message : 'Sign-in failed.')
            } finally {
              setPending(false)
            }
          }}
        >
          <Form.Item
            name="email"
            label="Username or email"
            extra="Case-insensitive. Password is unchanged."
            rules={[{ required: true, message: 'Enter your username or email.' }]}
          >
            <Input autoComplete="username" placeholder="arivera or you@client.example" />
          </Form.Item>
          <Form.Item name="password" label="Password" rules={[{ required: true }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={pending}>
            Sign in
          </Button>
          <div className="login-forgot">
            <Link to="/forgot-password">Forgot password</Link>
          </div>
        </Form>
        <div className="demo-accounts" style={{ marginTop: 12 }}>
          <div>Demo seed (local only)</div>
          <div><code>admin</code> / <code>admin@bisconsultants.local</code> Global Administrator</div>
          <div><code>admin2</code> / <code>admin@democlient.local</code> Administrator / Demo Client</div>
          <div><code>arivera</code> / <code>editor@bisconsultants.local</code> Editor / Demo Client</div>
          <div><code>jhale</code> / <code>viewer@bisconsultants.local</code> Viewer / Demo Client</div>
          <div><code>rpatel</code> / <code>uploader@bisconsultants.local</code> Uploader / Demo Client</div>
          <div>Password: <code>Demo!Gis2026</code></div>
        </div>
      </Card>
      <PoweredByFooter onDark />
    </div>
  )
}
