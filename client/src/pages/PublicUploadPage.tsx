import { InboxOutlined } from '@ant-design/icons'
import { Button, Card, Checkbox, Col, Form, Input, Row, Select, Typography, Upload, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'
import type { LookupItem } from '../api'
import { publicApi } from '../api'
import { AssignedTechnicianField, type AssignedTechnician } from '../components/AssignedTechnicianField'
import { CompanyContactBlock } from '../components/CompanyContactBlock'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { PoweredByFooter } from '../components/PoweredByFooter'
import { UploadBatchProgress } from '../components/UploadBatchProgress'
import { DEFAULT_REQUIRED_FILE_MESSAGE, dropzoneHint, dropzoneText } from '../uploadFileTypes'
import {
  DEFAULT_UPLOAD_LIMITS,
  MAX_UPLOAD_BATCH,
  formatFileSize,
  parseUploadLimits,
  queueProgress,
  runUploadQueue,
  type UploadLimits,
  type UploadQueueItem,
} from '../uploadQueue'
import { useLeaveWhileUploading } from '../useLeaveGuard'

function filesFromList(list: { originFileObj?: File }[] | undefined): File[] {
  const seen = new Set<string>()
  const files: File[] = []
  for (const entry of list ?? []) {
    const file = entry?.originFileObj
    if (!file) continue
    const key = `${file.name}:${file.size}:${file.lastModified}`
    if (seen.has(key)) continue
    seen.add(key)
    files.push(file)
  }
  return files
}

export function PublicUploadPage() {
  const { token } = useParams<{ token: string }>()
  const [form] = Form.useForm()
  const [orgName, setOrgName] = useState<string | null>(null)
  const [techs, setTechs] = useState<AssignedTechnician[]>([])
  const [types, setTypes] = useState<LookupItem[]>([])
  const [limits, setLimits] = useState<UploadLimits>(DEFAULT_UPLOAD_LIMITS)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [queue, setQueue] = useState<UploadQueueItem[]>([])
  const [hideCompleted, setHideCompleted] = useState(false)
  const [completeDismissed, setCompleteDismissed] = useState(false)
  const progress = useMemo(() => queueProgress(queue), [queue])
  useLeaveWhileUploading(progress.inFlight)

  const load = () => {
    if (!token?.trim()) {
      setError('This upload link is not valid.')
      return
    }
    publicApi
      .info(token)
      .then((info) => {
        setOrgName(info.organizationName)
        setTechs(info.assignedTechnicians ?? [])
        setTypes(info.documentTypes)
        setLimits(parseUploadLimits({
          uploads: {
            maxFileBytes: info.maxFileBytes || DEFAULT_UPLOAD_LIMITS.maxFileBytes,
            maxFileMegabytes: info.maxFileMegabytes || DEFAULT_UPLOAD_LIMITS.maxFileMegabytes,
            concurrency: info.concurrency || DEFAULT_UPLOAD_LIMITS.concurrency,
            acceptedExtensions: info.acceptedExtensions,
            supportedTypesLabel: info.supportedTypesLabel,
            accept: info.accept,
          },
        }))
        form.setFieldsValue({
          clientName: info.organizationName,
          documentTypeId: info.documentTypes[0]?.id,
        })
        setError(null)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'This upload link is not valid.'))
  }

  useEffect(() => {
    load()
  }, [form, token])

  return (
    <div className="login-wrap">
      <Card
        className="login-card compact-card is-upload bis-theme-panel"
        title={(
          <TitleWithHelp help="Send one or many PDF, Word, Excel, or image files with this link. You do not need an account. Assigned technician is this client’s organization default — not a person you pick for the file.">
            Upload files
          </TitleWithHelp>
        )}
      >
        {error && (
          <div style={{ marginBottom: 16 }}>
            <LoadError message={error} onRetry={token?.trim() ? () => { setError(null); load() } : undefined} />
          </div>
        )}
        {!error && !orgName && token && (
          <Typography.Paragraph type="secondary">Loading client…</Typography.Paragraph>
        )}
        {!error && orgName && (
          <Form
            form={form}
            layout="vertical"
            className="dense-form"
            onFinish={async (values: {
              documentTypeId: string
              isPriority?: boolean
              priorityNote?: string
              clientNotes?: string
              file: { originFileObj?: File }[]
            }) => {
              const files = filesFromList(values.file)
              if (files.length === 0 || !token) {
                message.error(DEFAULT_REQUIRED_FILE_MESSAGE)
                return
              }
              setSaving(true)
              setHideCompleted(false)
              setCompleteDismissed(false)
              try {
                const type = types.find((t) => t.id === values.documentTypeId)
                const items = await runUploadQueue(files, {
                  maxFileBytes: limits.maxFileBytes,
                  concurrency: limits.concurrency,
                  acceptedExtensions: limits.acceptedExtensions,
                  supportedTypesLabel: limits.supportedTypesLabel,
                  onUpdate: setQueue,
                  upload: async (file) => {
                    const body = new FormData()
                    body.append('documentTypeId', values.documentTypeId)
                    if (type) body.append('documentTypeName', type.name)
                    if (values.isPriority) body.append('isPriority', 'true')
                    if (values.priorityNote) body.append('priorityNote', values.priorityNote)
                    if (values.clientNotes) body.append('clientNotes', values.clientNotes)
                    body.append('file', file)
                    const result = await publicApi.upload(token, body)
                    return { id: result.id }
                  },
                })
                const uploaded = items.filter((item) => item.status === 'done')
                if (uploaded.length > 0) {
                  form.setFieldsValue({ file: [] })
                  void publicApi.received(token, {
                    fileCount: uploaded.length,
                    fileNames: uploaded.map((item) => item.file.name),
                  }).catch(() => undefined)
                } else {
                  message.error('No files were uploaded.')
                }
              } catch (err) {
                message.error(err instanceof Error ? err.message : 'Upload failed.')
              } finally {
                setSaving(false)
              }
            }}
          >
            <Row gutter={[12, 0]}>
              <Col xs={24} sm={12}>
                <Form.Item
                  name="clientName"
                  label="Client"
                  tooltip="This link only accepts files for this client."
                >
                  <Input readOnly className="public-upload-client" aria-readonly="true" />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <Form.Item name="documentTypeId" label="Document type" rules={[{ required: true, message: 'Choose a document type.' }]}>
                  <Select options={types.map((t) => ({ value: t.id, label: t.name }))} disabled={saving} />
                </Form.Item>
              </Col>
              <Col xs={24} sm={12}>
                <AssignedTechnicianField techs={techs} disabled={saving} />
              </Col>
            </Row>
            <Form.Item
              name="clientNotes"
              label="Client notes"
              tooltip="Optional. Added as a client-visible comment on each document in this batch."
            >
              <Input.TextArea
                rows={3}
                maxLength={2000}
                placeholder="Optional notes for the technicians — shown as a comment on each file"
                disabled={saving}
              />
            </Form.Item>
            <Form.Item
              name="file"
              label="Files"
              valuePropName="fileList"
              getValueFromEvent={(e) => e?.fileList}
              rules={[{ required: true, message: DEFAULT_REQUIRED_FILE_MESSAGE }]}
              tooltip={`Each file is its own work item. ${limits.supportedTypesLabel}. Max ${formatFileSize(limits.maxFileBytes)} per file · up to ${MAX_UPLOAD_BATCH} files.`}
            >
              <Upload.Dragger className="upload-dropzone" multiple maxCount={MAX_UPLOAD_BATCH} beforeUpload={() => false} accept={limits.accept} disabled={saving}>
                <p className="ant-upload-drag-icon"><InboxOutlined /></p>
                <p className="ant-upload-text">{dropzoneText()}</p>
                <p className="ant-upload-hint">{dropzoneHint(MAX_UPLOAD_BATCH)}</p>
              </Upload.Dragger>
            </Form.Item>
            <div className="priority-row">
              <Form.Item name="isPriority" valuePropName="checked" className="priority-check">
                <Checkbox disabled={saving}>This is priority work</Checkbox>
              </Form.Item>
              <Form.Item
                name="priorityNote"
                className="priority-note"
                tooltip="Optional — applied to every file. Visible to the technicians who pick this up."
              >
                <Input placeholder="Needed by Friday" maxLength={500} disabled={saving} />
              </Form.Item>
            </div>
            <Button type="primary" htmlType="submit" loading={saving} block disabled={!!error}>
              {saving && progress.total > 1 ? `Uploading ${progress.current} of ${progress.total}` : 'Upload'}
            </Button>
          </Form>
        )}
        {queue.length > 0 && (
          <UploadBatchProgress
            items={queue}
            hideCompleted={hideCompleted}
            onToggleHideCompleted={() => setHideCompleted((value) => !value)}
            completeVisible={!saving && !completeDismissed}
            onDismissComplete={() => setCompleteDismissed(true)}
            completeDescription={orgName ? `Files land in ${orgName} as Pending.` : undefined}
          />
        )}
      </Card>
      <CompanyContactBlock className="company-contact-public" />
      <PoweredByFooter onDark />
    </div>
  )
}
