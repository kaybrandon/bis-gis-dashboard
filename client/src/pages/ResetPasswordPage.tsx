import { Alert, Button, Card, Form, Input, Typography } from 'antd'
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api'
import { PoweredByFooter } from '../components/PoweredByFooter'

export function ResetPasswordPage() {
  const [params] = useSearchParams()
  const token = useMemo(() => (params.get('token') ?? '').trim(), [params])
  const [pending, setPending] = useState(false)
  const [done, setDone] = useState(false)
  const [error, setError] = useState<string | null>(token ? null : 'This reset link is invalid or has expired.')

  return (
    <div className="login-wrap">
      <Card className="login-card compact-card">
        <Typography.Title level={3} style={{ marginBottom: 4 }}>Choose a new password</Typography.Title>
        <Typography.Paragraph type="secondary" className="page-lead">
          The link from your email works once and expires in 30 minutes.
        </Typography.Paragraph>
        {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 12 }} />}
        {done && (
          <Alert
            type="success"
            showIcon
            message="Your password was reset. You can sign in now."
            style={{ marginBottom: 12 }}
          />
        )}
        {!done && token && (
          <Form
            layout="vertical"
            className="dense-form"
            onFinish={async (values: { newPassword: string; confirmPassword: string }) => {
              setPending(true)
              setError(null)
              try {
                await api.resetPassword(token, values.newPassword, values.confirmPassword)
                setDone(true)
              } catch (err) {
                setError(err instanceof Error ? err.message : 'This reset link is invalid or has expired.')
              } finally {
                setPending(false)
              }
            }}
          >
            <Form.Item
              name="newPassword"
              label="New password"
              extra="At least 8 characters, with an uppercase letter, a number, and a symbol."
              rules={[{ required: true, message: 'Enter a new password.' }, { min: 8, message: 'Use at least 8 characters.' }]}
            >
              <Input.Password autoComplete="new-password" />
            </Form.Item>
            <Form.Item
              name="confirmPassword"
              label="Confirm password"
              dependencies={['newPassword']}
              rules={[
                { required: true, message: 'Confirm the new password.' },
                ({ getFieldValue }) => ({
                  validator(_, value) {
                    if (!value || getFieldValue('newPassword') === value) return Promise.resolve()
                    return Promise.reject(new Error('New password and confirmation do not match.'))
                  },
                }),
              ]}
            >
              <Input.Password autoComplete="new-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={pending}>
              Reset password
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
