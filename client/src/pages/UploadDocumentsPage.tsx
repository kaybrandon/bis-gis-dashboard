import { InboxOutlined } from '@ant-design/icons'
import { Button, Card, Checkbox, Col, Form, Input, Row, Select, Space, Typography, Upload, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { AssignableUser, LookupItem, OrgOption } from '../api'
import { api } from '../api'
import { useAuth } from '../auth'
import { ASSIGNED_TO_LABEL, ASSIGNED_TO_UPLOAD_HELP } from '../assignmentLabels'
import { AssignedTechnicianField, type AssignedTechnician } from '../components/AssignedTechnicianField'
import { CompanyContactBlock } from '../components/CompanyContactBlock'
import { TitleWithHelp } from '../components/HelpTip'
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

function formId(value: unknown): string {
  if (typeof value === 'string') return value
  if (value && typeof value === 'object' && 'value' in value) {
    return String((value as { value: unknown }).value ?? '')
  }
  return value == null ? '' : String(value)
}

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

export function UploadDocumentsPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [form] = Form.useForm()
  const [orgs, setOrgs] = useState<OrgOption[]>([])
  const [types, setTypes] = useState<LookupItem[]>([])
  const [assignees, setAssignees] = useState<AssignableUser[]>([])
  const [techs, setTechs] = useState<AssignedTechnician[]>([])
  const [limits, setLimits] = useState<UploadLimits>(DEFAULT_UPLOAD_LIMITS)
  const [saving, setSaving] = useState(false)
  const [queue, setQueue] = useState<UploadQueueItem[]>([])
  const [hideCompleted, setHideCompleted] = useState(false)
  const [completeDismissed, setCompleteDismissed] = useState(false)
  const organizationId = Form.useWatch('organizationId', form)
  const isStaff = !!user?.canMutateWorkItems
  const showOrgSelector = isStaff || (user?.organizations.length ?? 0) > 1
  const progress = useMemo(() => queueProgress(queue), [queue])
  useLeaveWhileUploading(progress.inFlight)

  useEffect(() => {
    if (!user?.canUpload) return
    setQueue([])
    Promise.all([api.organizations(), api.documentTypes(), api.settings()])
      .then(([lookupOrgs, t, settings]) => {
        const map = new Map<string, OrgOption>()
        for (const org of [...lookupOrgs, ...(user?.organizations ?? [])]) {
          if (org?.id) map.set(org.id, org)
        }
        const merged = [...map.values()]
        setOrgs(merged)
        setTypes(t)
        setLimits(parseUploadLimits(settings))
        const locked = !isStaff && user?.organizations.length === 1 ? user.organizations[0].id : undefined
        form.setFieldsValue({
          organizationId: locked ?? (merged.length === 1 ? merged[0].id : form.getFieldValue('organizationId')),
          documentTypeId: isStaff ? (t[0]?.id ?? form.getFieldValue('documentTypeId')) : undefined,
        })
      })
      .catch((err) => message.error(err instanceof Error ? err.message : 'Lookups failed to load.'))
  }, [form, isStaff, user])

  useEffect(() => {
    const id = formId(organizationId)
    if (!id) {
      setAssignees([])
      setTechs([])
      return
    }
    api.assignedTechnicians(id).then(setTechs).catch(() => setTechs([]))
    if (!isStaff) {
      setAssignees([])
      return
    }
    api.assignableUsers(id).then(setAssignees).catch(() => setAssignees([]))
  }, [isStaff, organizationId])

  if (!user?.canUpload) {
    return (
      <Card className="bis-theme-panel">
        <Typography.Title level={3} className="page-title">Upload Documents</Typography.Title>
        <Typography.Paragraph>Your role cannot upload documents.</Typography.Paragraph>
      </Card>
    )
  }

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} className="page-title" style={{ marginBottom: 0 }}>
          <TitleWithHelp
            help={isStaff
              ? 'Choose the client. New files auto-assign Assigned to the organization’s Assigned technician (primary, else first). Leave Assigned to blank to use that default, or pick a person for this batch. If the org has no Assigned technician, the document stays unassigned and staff can pick it up. Assigned technician is the org default — Assigned to is the work-item assignee.'
              : 'Files are assigned to your organization. BIS staff will set document type and priority after upload. Assigned technician is the org default, not a person you pick for this file.'}
          >
            Upload Documents
          </TitleWithHelp>
        </Typography.Title>
      </div>
      <Card className="form-panel compact-card bis-theme-panel" title="Files">
        <Form
          form={form}
          layout="vertical"
          className="dense-form"
          onFinish={async (values) => {
            const files = filesFromList(values.file)
            if (files.length === 0) {
              message.error(DEFAULT_REQUIRED_FILE_MESSAGE)
              return
            }
            const nextOrgId = formId(values.organizationId)
            const nextTypeId = formId(values.documentTypeId)
            const organization = orgs.find((o) => o.id === nextOrgId)
            const documentType = types.find((t) => t.id === nextTypeId)
            if (!nextOrgId || !organization) {
              message.error('Select a client organization.')
              return
            }
            setSaving(true)
            setHideCompleted(false)
            setCompleteDismissed(false)
            try {
              const items = await runUploadQueue(files, {
                maxFileBytes: limits.maxFileBytes,
                concurrency: limits.concurrency,
                acceptedExtensions: limits.acceptedExtensions,
                supportedTypesLabel: limits.supportedTypesLabel,
                onUpdate: setQueue,
                upload: async (file) => {
                  const body = new FormData()
                  body.append('organizationId', nextOrgId)
                  body.append('organizationName', organization.name)
                  if (isStaff && nextTypeId) {
                    body.append('documentTypeId', nextTypeId)
                    if (documentType?.name) body.append('documentTypeName', documentType.name)
                  }
                  if (values.title) body.append('title', values.title)
                  if (isStaff && values.assignedToUserId) body.append('assignedToUserId', formId(values.assignedToUserId))
                  if (isStaff && values.isPriority) body.append('isPriority', 'true')
                  if (isStaff && values.priorityNote) body.append('priorityNote', values.priorityNote)
                  if (values.clientNotes) body.append('clientNotes', values.clientNotes)
                  body.append('file', file)
                  const created = await api.upload(body)
                  return { id: created.id }
                },
              })
              const ids = items.filter((item) => item.status === 'done' && item.workItemId).map((item) => item.workItemId!)
              if (ids.length === 0) {
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
              {showOrgSelector ? (
                <Form.Item name="organizationId" label="Client" rules={[{ required: true, message: 'Select a client organization.' }]}>
                  <Select
                    showSearch
                    optionFilterProp="label"
                    options={orgs.map((o) => ({ value: o.id, label: o.name }))}
                    disabled={saving}
                  />
                </Form.Item>
              ) : (
                <>
                  <Form.Item name="organizationId" hidden>
                    <Input />
                  </Form.Item>
                  <Form.Item label="Client">
                    <Input value={user.organizations[0]?.name ?? ''} disabled />
                  </Form.Item>
                </>
              )}
            </Col>
            {isStaff && (
              <Col xs={24} sm={12}>
                <Form.Item name="documentTypeId" label="Document type" rules={[{ required: true, message: 'Select a document type.' }]}>
                  <Select options={types.map((t) => ({ value: t.id, label: t.name }))} disabled={saving} />
                </Form.Item>
              </Col>
            )}
            <Col xs={24} sm={12}>
              <AssignedTechnicianField techs={techs} disabled={saving} />
            </Col>
            <Col xs={24} sm={12}>
              <Form.Item name="title" label="Title" tooltip="Optional batch title; otherwise each file uses its name.">
                <Input placeholder="Optional title for the batch" disabled={saving} />
              </Form.Item>
            </Col>
            {isStaff && (
              <Col xs={24} sm={12}>
                <Form.Item
                  name="assignedToUserId"
                  label={ASSIGNED_TO_LABEL}
                  tooltip={ASSIGNED_TO_UPLOAD_HELP}
                >
                  <Select allowClear options={assignees.map((u) => ({ value: u.id, label: u.displayName }))} disabled={saving} />
                </Form.Item>
              </Col>
            )}
          </Row>
          {isStaff && (
            <div className="priority-row">
              <Form.Item name="isPriority" valuePropName="checked" className="priority-check">
                <Checkbox disabled={saving}>Priority</Checkbox>
              </Form.Item>
              <Form.Item name="priorityNote" className="priority-note" tooltip="Optional — applied to every file.">
                <Input placeholder="Needed by Friday" maxLength={500} disabled={saving} />
              </Form.Item>
            </div>
          )}
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
            tooltip={`Each file is its own work item. ${limits.supportedTypesLabel}. Max ${formatFileSize(limits.maxFileBytes)} per file · ${limits.concurrency} at a time · up to ${MAX_UPLOAD_BATCH} files.`}
          >
            <Upload.Dragger className="upload-dropzone" beforeUpload={() => false} multiple maxCount={MAX_UPLOAD_BATCH} accept={limits.accept} disabled={saving}>
              <p className="ant-upload-drag-icon">
                <InboxOutlined />
              </p>
              <p className="ant-upload-text">{dropzoneText()}</p>
              <p className="ant-upload-hint">{dropzoneHint(MAX_UPLOAD_BATCH)}</p>
            </Upload.Dragger>
          </Form.Item>
          <Button type="primary" htmlType="submit" loading={saving}>
            {saving && progress.total > 1 ? `Uploading ${progress.current} of ${progress.total}` : queue.length > 1 ? `Upload ${queue.length} files` : 'Upload'}
          </Button>
        </Form>
        {queue.length > 0 && (
          <UploadBatchProgress
            items={queue}
            hideCompleted={hideCompleted}
            onToggleHideCompleted={() => setHideCompleted((value) => !value)}
            completeVisible={!saving && !completeDismissed}
            onDismissComplete={() => setCompleteDismissed(true)}
            completeExtra={
              !saving && !progress.inFlight ? (
                <Space wrap>
                  {queue.filter((item) => item.status === 'done' && item.workItemId).length === 1 ? (
                    <Button
                      htmlType="button"
                      size="small"
                      onClick={() => navigate(`/documents/${queue.find((item) => item.workItemId)?.workItemId}`)}
                    >
                      Open document
                    </Button>
                  ) : null}
                  <Button htmlType="button" size="small" onClick={() => navigate('/documents')}>
                    Go to Manage Documents
                  </Button>
                </Space>
              ) : null
            }
          />
        )}
      </Card>
      <CompanyContactBlock />
    </Space>
  )
}
