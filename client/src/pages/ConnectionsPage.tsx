import {
  ApiOutlined,
  EditOutlined,
  FolderOpenOutlined,
  PlayCircleOutlined,
  PlusOutlined,
} from '@ant-design/icons'
import {
  Alert,
  App,
  Button,
  Card,
  Form,
  Input,
  Radio,
  Select,
  Space,
  Typography,
} from 'antd'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Navigate } from 'react-router-dom'
import type { CheckFoldersResponse, FileConnection, FileKind, FileServer, LanConnection, SyncControl } from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { LoadError } from '../components/LoadError'

type SourceKind = 'azure' | 'local' | 'unc'
type ConnRow = {
  key: string
  organizationId: string
  organizationName: string
  ftp?: FileConnection
  agent?: LanConnection
  status: string
  canManage: boolean
}

function normalizeAzurePath(path?: string | null) {
  let value = (path ?? '').trim().replace(/\\/g, '/')
  while (value.startsWith('/')) value = value.slice(1)
  if (value.toLowerCase().startsWith('workfiles/')) {
    value = value.slice('workfiles/'.length)
  }
  return value
}

function isAzurePath(path?: string | null) {
  const value = normalizeAzurePath(path).toLowerCase()
  return value.startsWith('orgs/')
}

function isUncPath(path?: string | null) {
  const value = (path ?? '').trim()
  return value.startsWith('\\\\') || value.startsWith('//')
}

function inferKind(path?: string | null): SourceKind {
  if (isAzurePath(path)) return 'azure'
  if (isUncPath(path)) return 'unc'
  return 'local'
}

function displayPath(path?: string | null) {
  const trimmed = (path ?? '').trim()
  if (!trimmed) return '—'
  if (isAzurePath(trimmed)) {
    const prefix = normalizeAzurePath(trimmed).replace(/\/+$/, '')
    return `workfiles/${prefix}`
  }
  return trimmed
}

function azureOrgPath(code?: string) {
  const slug = (code ?? 'client').toLowerCase().replace(/[^a-z0-9]+/g, '')
  return `workfiles/orgs/${slug || 'client'}/shapefiles`
}

function formatHeartbeat(agent?: LanConnection) {
  if (agent?.lastHeartbeatAt) {
    const date = new Date(agent.lastHeartbeatAt)
    if (!Number.isNaN(date.getTime())) return date.toLocaleString()
  }
  if (agent?.heartbeatLabel) return agent.heartbeatLabel
  if (agent?.enrolled) return 'Never — enrolled agent has not checked in'
  return 'None — agent not enrolled or not running'
}

function rowStatus(ftp?: FileConnection, agent?: LanConnection) {
  if (agent) {
    if (agent.status === 'Error' || agent.lastErrorCode || agent.lastError) return 'Error'
    if (agent.status === 'Offline') return 'Offline'
    if (agent.heartbeatOk || agent.status === 'Online') return 'Online'
    return agent.status || 'Idle'
  }
  if (ftp?.lastError || ftp?.status === 'Failed') return 'Offline'
  return ftp?.status || 'Idle'
}

function mergeRows(files: FileConnection[], agents: LanConnection[]): ConnRow[] {
  const ids = [...new Set([...files.map((x) => x.organizationId), ...agents.map((x) => x.organizationId)])]
  return ids.map((organizationId) => {
    const ftp = files.find((x) => x.organizationId === organizationId)
    const agent = agents.find((x) => x.organizationId === organizationId)
    return {
      key: organizationId,
      organizationId,
      organizationName: ftp?.organizationName ?? agent?.organizationName ?? 'Client',
      ftp,
      agent,
      status: rowStatus(ftp, agent),
      canManage: Boolean(ftp?.canManage || agent?.canManage),
    }
  })
}

function formatWhen(value?: string | null) {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString()
}

function PathText({ path }: { path?: string | null }) {
  return <Typography.Text strong>{displayPath(path)}</Typography.Text>
}

export function ConnectionsPage() {
  const { user } = useAuth()
  const { message, modal } = App.useApp()
  const canManage = Boolean(user?.canManageConnections)
  const [files, setFiles] = useState<FileConnection[]>([])
  const [agents, setAgents] = useState<LanConnection[]>([])
  const [servers, setServers] = useState<FileServer[]>([])
  const [kinds, setKinds] = useState<FileKind[]>([])
  const [sync, setSync] = useState<SyncControl | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [mode, setMode] = useState<'list' | 'form'>('list')
  const [editing, setEditing] = useState<ConnRow | null>(null)
  const [running, setRunning] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)
  const [check, setCheck] = useState<CheckFoldersResponse | null>(null)
  const [issuedToken, setIssuedToken] = useState<string | null>(null)
  const [form] = Form.useForm()
  const sourceKind = (Form.useWatch('sourceKind', form) as SourceKind | undefined) ?? 'azure'
  const sourcePath = Form.useWatch('sourcePath', form) as string | undefined
  const remoteFolder = Form.useWatch('remoteFolder', form) as string | undefined
  const organizationId = Form.useWatch('organizationId', form) as string | undefined

  const rows = useMemo(() => mergeRows(files, agents), [files, agents])
  const orgs = user?.organizations ?? []

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [nextFiles, nextAgents, nextServers] = await Promise.all([
        api.connections(),
        api.lanConnections().catch(() => []),
        api.fileServers().catch(() => []),
      ])
      setFiles(nextFiles)
      setAgents(nextAgents)
      setServers(nextServers)
      try {
        setKinds((await api.fileKinds()).kinds)
      } catch {
        setKinds([])
      }
      try {
        setSync(await api.syncControl())
      } catch {
        setSync(null)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not load connections.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  if (!user?.canSeeConnections) {
    return <Navigate to="/" replace />
  }

  const defaults = (orgId?: string) => {
    const org = orgs.find((item) => item.id === orgId) ?? orgs[0]
    return {
      organizationId: org?.id,
      sourceKind: 'azure' as SourceKind,
      sourcePath: azureOrgPath(org?.code),
      remoteFolder: 'C:\\GIS\\Outgoing',
      direction: 'Bidirectional',
      scheduleMinutes: 15,
      includeFtp: false,
      fileServerId: servers[0]?.id,
    }
  }

  const openNew = () => {
    setEditing(null)
    setCheck(null)
    setIssuedToken(null)
    form.resetFields()
    form.setFieldsValue(defaults())
    setMode('form')
  }

  const openEdit = (row: ConnRow) => {
    const org = orgs.find((item) => item.id === row.organizationId)
    const source = row.agent?.bisFolder ?? row.ftp?.sourcePath ?? azureOrgPath(org?.code)
    setEditing(row)
    setCheck(null)
    setIssuedToken(null)
    form.resetFields()
    form.setFieldsValue({
      ...defaults(row.organizationId),
      organizationId: row.organizationId,
      sourceKind: inferKind(source),
      sourcePath: isAzurePath(source) ? displayPath(source) : source,
      remoteFolder: row.agent?.remoteFolder
        ? (isAzurePath(row.agent.remoteFolder) ? displayPath(row.agent.remoteFolder) : row.agent.remoteFolder)
        : 'C:\\GIS\\Outgoing',
      direction: row.agent?.direction ?? 'Bidirectional',
      scheduleMinutes: row.agent?.scheduleMinutes ?? 15,
      includeFtp: Boolean(row.ftp && (row.ftp.passwordConfigured || row.ftp.ftpUrl)),
      fileServerId: row.ftp?.fileServerId ?? servers[0]?.id,
    })
    setMode('form')
  }

  const applySourceKind = (kind: SourceKind) => {
    const org = orgs.find((item) => item.id === organizationId) ?? orgs[0]
    const current = (form.getFieldValue('sourcePath') as string | undefined) ?? ''
    if (kind === 'azure') {
      form.setFieldValue('sourcePath', isAzurePath(current) ? displayPath(current) : azureOrgPath(org?.code))
    } else if (kind === 'local') {
      if (isAzurePath(current)) form.setFieldValue('sourcePath', 'C:\\GIS\\Outgoing')
    } else if (!isUncPath(current)) {
      form.setFieldValue('sourcePath', org ? `\\\\server\\gis\\${org.code.toLowerCase()}` : '\\\\server\\gis\\share')
    }
  }

  const runCheck = async (row?: ConnRow) => {
    setChecking(true)
    try {
      const result = row?.agent
        ? await api.checkLanFolders(row.agent.id)
        : await api.checkFolders({
            sourcePath: (form.getFieldValue('sourcePath') as string) || '',
            remoteFolder: (form.getFieldValue('remoteFolder') as string) || '',
            fileServerId: form.getFieldValue('fileServerId') as string | undefined,
          })
      setCheck(result)
      if (result.bothPassed) {
        message.success('Check folders: Pass / Pass')
        modal.confirm({
          title: 'Folders passed. Run sync now?',
          content: 'Source and Destination both passed. This runs the same job as Run now.',
          okText: 'Sync now',
          onOk: async () => {
            if (row?.agent) {
              const updated = await api.runLanNow(row.agent.id)
              setAgents((current) => current.map((item) => (item.id === updated.id ? updated : item)))
              message.success('Sync finished. Check the card for Pull / Push and any Error.')
              return
            }
            const values = await form.validateFields()
            await saveAndSync(values, true)
          },
        })
      } else {
        message.error('Check folders: Fail. See Pass / Fail on the form.')
      }
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Check folders failed.')
    } finally {
      setChecking(false)
    }
  }

  const saveAndSync = async (values: {
    organizationId: string
    sourcePath: string
    remoteFolder: string
    direction: string
    scheduleMinutes: number
    fileServerId?: string
  }, runAfter: boolean) => {
    const source = isAzurePath(values.sourcePath) ? displayPath(values.sourcePath) : values.sourcePath.trim()
    const dest = isAzurePath(values.remoteFolder) ? displayPath(values.remoteFolder) : values.remoteFolder.trim()
    if (editing?.ftp) {
      await api.updateConnection(editing.ftp.id, { sourcePath: source, fileServerId: values.fileServerId })
    } else {
      await api.createConnection({
        organizationId: values.organizationId,
        sourcePath: source,
        fileServerId: values.fileServerId || servers[0]?.id,
      })
    }
    if (editing?.agent) {
      const updated = await api.updateLanConnection(editing.agent.id, {
        bisFolder: source,
        remoteFolder: dest,
        direction: values.direction,
        scheduleMinutes: values.scheduleMinutes,
      })
      if (runAfter) {
        const ran = await api.runLanNow(updated.id)
        setAgents((current) => current.map((item) => (item.id === ran.id ? ran : item)))
      }
    } else {
      const created = await api.createLanConnection({
        organizationId: values.organizationId,
        bisFolder: source,
        remoteFolder: dest,
        direction: values.direction,
        scheduleMinutes: values.scheduleMinutes,
      })
      if (created.enrollToken) {
        setIssuedToken(created.enrollToken)
      }
      if (runAfter) {
        await api.runLanNow(created.id)
      }
    }
    message.success(editing ? 'Connection updated.' : 'Connection saved. Status is visible on the list.')
    if (!runAfter) {
      setMode('list')
      setEditing(null)
    }
    await load()
  }

  const onSave = async () => {
    const values = await form.validateFields()
    try {
      await saveAndSync(values, false)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not save the connection.')
    }
  }

  const runNow = async (row: ConnRow) => {
    if (!row.agent) {
      message.error('Add an agent before Run now.')
      return
    }
    setRunning(row.key)
    try {
      const updated = await api.runLanNow(row.agent.id)
      setAgents((current) => current.map((item) => (item.id === updated.id ? updated : item)))
      if (updated.lastError) {
        message.error(updated.lastError)
      } else {
        message.success(`Sync finished. Pull ${updated.lastPullCount} / Push ${updated.lastPushCount}.`)
      }
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Run now failed.')
    } finally {
      setRunning(null)
    }
  }

  if (mode === 'form') {
    return (
      <Space direction="vertical" size={16} style={{ width: '100%' }} className="conn-form-page">
        <div>
          <Typography.Title level={3} className="page-title" style={{ marginBottom: 0 }}>
            {editing ? 'Edit connection' : 'New connection'}
          </Typography.Title>
          <Typography.Paragraph type="secondary" className="page-lead" style={{ marginTop: 4 }}>
            {editing
              ? `Update ${editing.organizationName}'s Source, Destination, and Direction.`
              : 'Create Source, Destination, and Direction. The agent can run on a PC or a file server — Client is the organization only.'}
          </Typography.Paragraph>
        </div>
        <Card className="conn-form-card compact-card">
          <Form form={form} layout="vertical" className="dense-form conn-form">
            <section className="conn-section">
              <h2 className="conn-section-title">Client</h2>
              <Form.Item name="organizationId" label="Client" rules={[{ required: true, message: 'Select a client' }]}>
                <Select
                  disabled={!!editing}
                  options={orgs.map((org) => ({ value: org.id, label: org.name }))}
                  placeholder="Select the organization"
                  onChange={(id) => {
                    if (sourceKind === 'azure') {
                      const current = (form.getFieldValue('sourcePath') as string | undefined) ?? ''
                      form.setFieldValue('sourcePath', isAzurePath(current) ? displayPath(current) : azureOrgPath(orgs.find((org) => org.id === id)?.code))
                    }
                  }}
                />
              </Form.Item>
              <Form.Item name="fileServerId" hidden>
                <Input />
              </Form.Item>
            </section>

            <section className="conn-section">
              <h2 className="conn-section-title">Source + Destination + Direction</h2>
              <Form.Item
                name="sourceKind"
                label="Source type"
                extra="Azure is the BIS store. Use a PC local drive when the agent runs on that machine. UNC only when the agent can reach a real network share."
              >
                <Radio.Group
                  onChange={(event) => applySourceKind(event.target.value as SourceKind)}
                  options={[
                    { value: 'azure', label: 'Azure (BIS store)' },
                    { value: 'local', label: 'This PC (local path)' },
                    { value: 'unc', label: 'Network share (UNC)' },
                  ]}
                />
              </Form.Item>
              {sourceKind === 'azure' && (
                <Alert
                  type="info"
                  showIcon
                  className="conn-azure-help"
                  message="BIS-side store. These files live in Azure in resource group rg-bis-gis-dashboard."
                />
              )}
              {sourceKind === 'local' && (
                <Typography.Paragraph type="secondary">
                  Prefer a local drive path such as <Typography.Text strong>C:\GIS\Outgoing</Typography.Text>. Do not use UNC when the folder is on the machine running the agent.
                </Typography.Paragraph>
              )}
              {sourceKind === 'unc' && (
                <Typography.Paragraph type="secondary">
                  UNC works only if the share is reachable from the agent. If it is not, use a PC local drive path.
                </Typography.Paragraph>
              )}
              <Form.Item
                name="sourcePath"
                label="Source"
                rules={[{ required: true, message: 'Enter the Source path' }]}
                extra={
                  sourceKind === 'azure' ? (
                    <>
                      Folder path <PathText path={sourcePath || 'workfiles/orgs/...'} />
                    </>
                  ) : sourceKind === 'unc' ? (
                    'UNC works only if the share is reachable; otherwise use a PC local drive path.'
                  ) : (
                    'Folder the agent reads from on this PC.'
                  )
                }
              >
                <Input placeholder={sourceKind === 'azure' ? 'workfiles/orgs/client/files' : sourceKind === 'unc' ? '\\\\server\\gis\\outbox' : 'C:\\GIS\\Outgoing'} />
              </Form.Item>
              <Form.Item
                name="remoteFolder"
                label="Destination"
                rules={[{ required: true, message: 'Enter the Destination path' }]}
                extra={
                  isAzurePath(remoteFolder) ? (
                    <>
                      Folder path <PathText path={remoteFolder} />
                    </>
                  ) : (
                    'Folder the agent writes to on the other side (PC or file server). Prefer C:\\… when that folder is on the agent machine.'
                  )
                }
              >
                <Input placeholder={isAzurePath(remoteFolder) ? 'workfiles/orgs/client/files' : 'C:\\GIS\\Incoming'} />
              </Form.Item>
              <Form.Item
                name="direction"
                label="Direction"
                extra="Push sends Source → Destination. Pull sends Destination → Source. Bidirectional: newer LastWriteTime wins."
              >
                <Radio.Group
                  options={[
                    { value: 'Push', label: 'Push' },
                    { value: 'Pull', label: 'Pull' },
                    { value: 'Bidirectional', label: 'Bidirectional' },
                  ]}
                />
              </Form.Item>
              <Form.Item name="scheduleMinutes" label="Schedule">
                <Select
                  options={[
                    { value: 15, label: 'Every 15 minutes' },
                    { value: 30, label: 'Every 30 minutes' },
                    { value: 60, label: 'Every 60 minutes' },
                  ]}
                />
              </Form.Item>
              {kinds.length > 0 && (
                <Typography.Paragraph type="secondary">
                  Allowed kinds: {kinds.filter((item) => item.enabled).map((item) => item.displayName).join(', ')}.
                </Typography.Paragraph>
              )}
            </section>

            {check && (
              <Alert
                type={check.bothPassed ? 'success' : 'error'}
                showIcon
                message="Check folders"
                description={
                  <div>
                    <div>
                      Source: <Typography.Text strong>{check.source.result}</Typography.Text> — {check.source.message}
                    </div>
                    <div>
                      Destination: <Typography.Text strong>{check.destination.result}</Typography.Text> — {check.destination.message}
                    </div>
                  </div>
                }
              />
            )}

            {editing?.agent?.lastError && (
              <Alert type="error" showIcon message={`${editing.organizationName}: ${editing.agent.lastError}`} />
            )}

            {issuedToken && (
              <Alert type="success" showIcon message="Copy the enroll token now" description={`${issuedToken} — it is not shown again after you leave this page.`} />
            )}
          </Form>
          <div className="conn-form-footer">
            <Button onClick={() => { setMode('list'); setEditing(null) }}>Cancel</Button>
            {canManage && (
              <Button icon={<FolderOpenOutlined />} loading={checking} onClick={() => void runCheck(editing ?? undefined)}>
                Check folders
              </Button>
            )}
            {canManage && (
              <Button type="primary" onClick={() => void onSave()}>
                {editing ? 'Save connection' : 'Create connection'}
              </Button>
            )}
          </div>
        </Card>
      </Space>
    )
  }

  return (
    <div className="connections-page">
      <div className="connections-header">
        <div>
          <Typography.Title level={3} className="page-title connections-title">Connections</Typography.Title>
          <Typography.Paragraph type="secondary" className="connections-lead">
            Secure file sync between client servers and BIS.
          </Typography.Paragraph>
        </div>
        <Space wrap>
          {canManage && (
            <Button type="primary" icon={<PlusOutlined />} onClick={openNew}>
              New connection
            </Button>
          )}
        </Space>
      </div>

      {sync?.paused && <Alert type="warning" showIcon message="All sync is paused" description={sync.message} style={{ marginBottom: 16 }} />}
      {error && <LoadError message={error} onRetry={() => void load()} />}

      {!loading && rows.length === 0 && !error && (
        <Card className="conn-empty">
          <Typography.Title level={4}>No connections yet</Typography.Title>
          <Typography.Paragraph type="secondary">
            Add a connection with Source, Destination, and Direction. The agent can run on a PC or a file server.
          </Typography.Paragraph>
          {canManage && (
            <Button type="primary" icon={<PlusOutlined />} onClick={openNew}>
              New connection
            </Button>
          )}
        </Card>
      )}

      <div className="conn-card-list">
        {rows.map((row) => {
          const source = row.agent?.bisFolder || row.ftp?.sourcePath
          const dest = row.agent?.remoteFolder
          const direction = row.agent?.direction ?? 'Bidirectional'
          return (
            <div key={row.key} className="conn-card" role="button" tabIndex={0} onClick={() => openEdit(row)} onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault()
                openEdit(row)
              }
            }}>
              <div className="conn-card-main">
                <div className="conn-card-title">
                  <ApiOutlined />
                  <span className="conn-name">{row.organizationName}</span>
                  <span className={`conn-status is-${row.status.toLowerCase()}`}>
                    <i className="conn-dot" />
                    {row.status}
                  </span>
                  <span className="conn-agent-ver">Direction {direction}</span>
                </div>
                <div className="conn-meta">
                  <span>Source <PathText path={source} /></span>
                  <span>Destination <PathText path={dest} /></span>
                </div>
                <div className="conn-meta">
                  <span>Last heartbeat {formatHeartbeat(row.agent)}</span>
                  <span>Last sync {formatWhen(row.agent?.lastSyncAt)}</span>
                  {row.agent && (row.agent.lastPullCount || row.agent.lastSyncAt) ? <span>Pull {row.agent.lastPullCount ?? 0} files</span> : null}
                  {row.agent && (row.agent.lastPushCount || row.agent.lastSyncAt) ? <span>Push {row.agent.lastPushCount ?? 0}</span> : null}
                </div>
                {(row.agent?.lastError || row.ftp?.lastError) && (
                  <div className="conn-error">
                    {row.organizationName}: {row.agent?.lastErrorCode ? `[${row.agent.lastErrorCode}] ` : ''}
                    {row.agent?.lastError ?? row.ftp?.lastError}
                  </div>
                )}
              </div>
              <div className="conn-card-actions" onClick={(event) => event.stopPropagation()}>
                {row.canManage && (
                  <Button type="text" icon={<FolderOpenOutlined />} loading={checking} onClick={() => void runCheck(row)}>
                    Check folders
                  </Button>
                )}
                {row.canManage && row.agent && !sync?.paused && (
                  <Button type="text" icon={<PlayCircleOutlined />} loading={running === row.key} onClick={() => void runNow(row)}>
                    Run now
                  </Button>
                )}
                {row.canManage && (
                  <Button type="text" icon={<EditOutlined />} onClick={() => openEdit(row)}>
                    Edit
                  </Button>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
