import { Button, Card, Space, Table, Tag, Typography } from 'antd'
import { useCallback, useEffect, useState } from 'react'
import { Navigate } from 'react-router-dom'
import type { EnvironmentCheck, EnvironmentStatus } from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { WhoIsOnlineCard } from '../components/WhoIsOnline'

const statusColor: Record<string, string> = {
  ok: 'green',
  degraded: 'gold',
  down: 'red',
  not_configured: 'default',
}

const statusLabel: Record<string, string> = {
  ok: 'OK',
  degraded: 'Degraded',
  down: 'Down',
  not_configured: 'Not configured',
}

function StatusTag({ value }: { value: string }) {
  return <Tag color={statusColor[value] ?? 'default'}>{statusLabel[value] ?? value}</Tag>
}

export function StatusPage() {
  const { user } = useAuth()
  const [status, setStatus] = useState<EnvironmentStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setStatus(await api.environmentStatus())
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Status checks failed to run.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  if (!user?.canManageGlobalDirectory) {
    return <Navigate to="/" replace />
  }

  const checkedAt = status?.checkedAt
    ? new Date(status.checkedAt).toLocaleString()
    : null

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Typography.Title level={3} className="page-title" style={{ marginBottom: 0 }}>
        Status
      </Typography.Title>
      <WhoIsOnlineCard enabled={Boolean(user?.canSeePresence ?? user?.canSeeInternalNotes)} />
      <Card
        className="compact-card bis-theme-panel"
        title={(
          <TitleWithHelp help="Live checks from this web app. App Insights, email, and Azure OpenAI are configuration only — not a ping to Azure Resource Manager.">
            Azure environment
          </TitleWithHelp>
        )}
        extra={<Button size="small" loading={loading} onClick={() => void load()}>Check again</Button>}
      >
        {error && <LoadError message={error} onRetry={() => void load()} />}
        {!status && !error && loading && (
          <Typography.Text type="secondary">Running live checks…</Typography.Text>
        )}
        {status && (
          <>
            <Space wrap style={{ marginBottom: 8 }}>
              <span>Overall</span>
              <StatusTag value={status.overall} />
              {checkedAt && <Typography.Text type="secondary">Last checked {checkedAt}</Typography.Text>}
            </Space>
            <Table<EnvironmentCheck>
              rowKey="key"
              className="bis-theme-grid"
              size="small"
              pagination={false}
              loading={loading}
              scroll={{ x: 'max-content' }}
              dataSource={status.checks}
              columns={[
                { title: 'Dependency', dataIndex: 'name' },
                {
                  title: 'Status',
                  dataIndex: 'status',
                  render: (value: string) => <StatusTag value={value} />,
                },
                {
                  title: 'Check',
                  dataIndex: 'mode',
                  render: (value: string) => (value === 'live' ? 'Live' : 'Configured'),
                },
                {
                  title: 'Detail',
                  dataIndex: 'detail',
                  render: (value: string, row) => (
                    <div>
                      <div>{value}</div>
                      {row.error && <Typography.Text type="danger">{row.error}</Typography.Text>}
                    </div>
                  ),
                },
              ]}
            />
          </>
        )}
      </Card>
    </Space>
  )
}
