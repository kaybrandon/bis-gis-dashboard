import { Card, Descriptions, Space, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { api } from '../api'
import { accountTriggerLabel } from '../accountLabel'
import { useAuth } from '../auth'
import { CompanyContactCard } from '../components/CompanyContactCard'
import { EmailSmtpCard } from '../components/EmailSmtpCard'
import { LoadError } from '../components/LoadError'

export function SettingsPage() {
  const { user } = useAuth()
  const [settings, setSettings] = useState<Record<string, unknown> | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    api.settings().then(setSettings).catch((err) => setError(err.message))
  }, [])

  const uploads = (settings?.uploads ?? {}) as {
    maxFileBytes?: number
    maxFileMegabytes?: number
    concurrency?: number
  }
  const isGlobal = user?.canManageGlobalDirectory

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Typography.Title level={3} className="page-title" style={{ marginBottom: 0 }}>
        {isGlobal ? 'Admin Settings' : 'Settings'}
      </Typography.Title>

      {isGlobal && <CompanyContactCard />}

      {isGlobal && <EmailSmtpCard />}

      <Card className="compact-card form-panel" style={{ maxWidth: 720 }} title="Account & uploads">
        {error && (
          <LoadError
            message={error}
            onRetry={() => {
              setError(null)
              api.settings().then(setSettings).catch((err) => setError(err.message))
            }}
          />
        )}
        <Descriptions bordered size="small" column={1} style={{ overflowWrap: 'anywhere' }}>
          <Descriptions.Item label="Signed in as">{accountTriggerLabel(user)} ({user?.email})</Descriptions.Item>
          <Descriptions.Item label="Role">{user?.roleDisplayName ?? user?.role}</Descriptions.Item>
          <Descriptions.Item label="Organizations">
            {user?.canManageGlobalDirectory ? 'All organizations' : user?.organizations.map((o) => o.name).join(', ') || '—'}
          </Descriptions.Item>
          <Descriptions.Item label="Upload size limit">
            {uploads.maxFileMegabytes ?? 50} MB per file
          </Descriptions.Item>
          <Descriptions.Item label="Upload queue">
            {uploads.concurrency ?? 3} files at a time. Large batches show a sticky progress bar (done / failed / skipped / remaining). Keep the window open until the queue finishes.
          </Descriptions.Item>
          <Descriptions.Item label="Change the size limit">
            Azure App Setting <Typography.Text code>Uploads__MaxFileMegabytes</Typography.Text>
            {' '}or <Typography.Text code>Uploads__MaxFileBytes</Typography.Text>. Restart after changing.
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </Space>
  )
}
