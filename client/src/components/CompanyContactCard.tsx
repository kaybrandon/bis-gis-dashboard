import { Button, Card, Col, Form, Input, Row, message } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import type { CompanyContact } from '../api'
import { api } from '../api'
import { TitleWithHelp } from './HelpTip'
import { LoadError } from './LoadError'

export function CompanyContactCard() {
  const [form] = Form.useForm()
  const [settings, setSettings] = useState<CompanyContact | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)

  const apply = useCallback((next: CompanyContact) => {
    setSettings(next)
    form.setFieldsValue({
      phone: next.phone ?? '',
      email: next.email ?? '',
      address: next.address ?? '',
      website: next.website ?? '',
    })
  }, [form])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      apply(await api.companyContact())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Company contact failed to load.')
    } finally {
      setLoading(false)
    }
  }, [apply])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <Card
      className="compact-card form-panel bis-theme-panel"
      style={{ maxWidth: 720 }}
      loading={loading && !settings}
      title={(
        <TitleWithHelp help="Shown on Upload Documents and the public upload link so clients can reach BIS Consultants. Phone, email, address, and website.">
          BIS Consultants
        </TitleWithHelp>
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
              apply(await api.saveCompanyContact({
                phone: values.phone?.trim() || null,
                email: values.email?.trim() || null,
                address: values.address?.trim() || null,
                website: values.website?.trim() || null,
              }))
              message.success('Company contact saved.')
            } catch (err) {
              message.error(err instanceof Error ? err.message : 'Company contact failed to save.')
            } finally {
              setSaving(false)
            }
          }}
        >
          <Row gutter={[12, 0]}>
            <Col xs={24} sm={12}>
              <Form.Item name="phone" label="Phone">
                <Input placeholder="800-247-9045" maxLength={40} disabled={saving} />
              </Form.Item>
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item
                name="email"
                label="Email"
                rules={[{ type: 'email', message: 'Enter a valid email address.' }]}
              >
                <Input placeholder="gissupport@bisconsultants.com" maxLength={200} disabled={saving} />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item name="address" label="Address">
            <Input.TextArea rows={2} placeholder="14802 Venture Dr. Farmers Branch Tx 75234" maxLength={300} disabled={saving} />
          </Form.Item>
          <Form.Item name="website" label="Website">
            <Input placeholder="www.bisconsultants.com" maxLength={200} disabled={saving} />
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={saving}>Save</Button>
        </Form>
      )}
    </Card>
  )
}
