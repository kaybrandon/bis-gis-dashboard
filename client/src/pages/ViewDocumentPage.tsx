import { DownloadOutlined, ExportOutlined, LeftOutlined, RightOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { Button, Card, Checkbox, Col, DatePicker, Dropdown, Input, InputNumber, Modal, Row, Select, Space, Spin, Tag, Tooltip, Typography, message } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import type { AiFillResponse, AssignableUser, LookupItem, StatusActions, WorkItemDetail, WorkItemQuery } from '../api'
import { api, authorizedBlob } from '../api'
import { CommentsPanel } from '../components/CommentsPanel'
import { LoadError } from '../components/LoadError'
import { DocumentViewer } from '../components/DocumentViewer'
import { InternalNotesPanel } from '../components/InternalNotesPanel'
import { TimeLogPanel } from '../components/TimeLogPanel'
import { isNeededByOverdue, neededByLabel } from '../neededBy'
import { statusLabel } from '../statusLabels'

type Draft = {
  title: string
  statusId: string
  documentTypeId: string
  assignedToUserId: string | null
  isSplit: boolean
  isSketch: boolean
  isPriority: boolean
  isReviewed: boolean
  priorityNote: string
  priorityNeededBy: Dayjs | null
  workedOn: Dayjs | null
  annexationCount: number
  correctionCount: number
  deedCount: number
  platCount: number
  propertyIds: string
}

type AiFieldKey =
  | 'title'
  | 'documentTypeId'
  | 'propertyIds'
  | 'annexationCount'
  | 'correctionCount'
  | 'deedCount'
  | 'platCount'
  | 'workedOn'

type AiHint = { confidence: number; approved: boolean }

function toDraft(item: WorkItemDetail): Draft {
  return {
    title: item.title,
    statusId: item.statusId,
    documentTypeId: item.documentTypeId,
    assignedToUserId: item.assignedToUserId ?? null,
    isSplit: item.isSplit,
    isSketch: item.isSketch,
    isPriority: !!item.isPriority,
    isReviewed: !!item.isReviewed,
    priorityNote: item.priorityNote ?? '',
    priorityNeededBy: item.priorityNeededBy ? dayjs(item.priorityNeededBy) : null,
    workedOn: item.workedOn ? dayjs(item.workedOn) : null,
    annexationCount: item.annexationCount,
    correctionCount: item.correctionCount,
    deedCount: item.deedCount,
    platCount: item.platCount,
    propertyIds: item.propertyIds ?? '',
  }
}

function sameDraft(a: Draft, b: Draft) {
  return JSON.stringify({
    ...a,
    workedOn: a.workedOn?.toISOString() ?? null,
    priorityNeededBy: a.priorityNeededBy?.toISOString() ?? null,
  }) === JSON.stringify({
    ...b,
    workedOn: b.workedOn?.toISOString() ?? null,
    priorityNeededBy: b.priorityNeededBy?.toISOString() ?? null,
  })
}

async function downloadFile(id: string, fileName: string) {
  const blob = await authorizedBlob(`/api/work-items/${id}/file`)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

function applyAiFill(current: Draft, result: AiFillResponse): { next: Draft; hints: Partial<Record<AiFieldKey, AiHint>> } {
  const hints: Partial<Record<AiFieldKey, AiHint>> = {}
  const next = { ...current }
  const { fields } = result

  if (fields.title.present && fields.title.value) {
    next.title = fields.title.value
    hints.title = { confidence: fields.title.confidence, approved: false }
  }
  if (fields.type.present && fields.type.documentTypeId) {
    next.documentTypeId = fields.type.documentTypeId
    hints.documentTypeId = { confidence: fields.type.confidence, approved: false }
  }
  if (fields.propertyIds.present && fields.propertyIds.value != null) {
    next.propertyIds = fields.propertyIds.value
    hints.propertyIds = { confidence: fields.propertyIds.confidence, approved: false }
  }
  if (fields.annexationCount.present && fields.annexationCount.value != null) {
    next.annexationCount = fields.annexationCount.value
    hints.annexationCount = { confidence: fields.annexationCount.confidence, approved: false }
  }
  if (fields.correctionCount.present && fields.correctionCount.value != null) {
    next.correctionCount = fields.correctionCount.value
    hints.correctionCount = { confidence: fields.correctionCount.confidence, approved: false }
  }
  if (fields.deedCount.present && fields.deedCount.value != null) {
    next.deedCount = fields.deedCount.value
    hints.deedCount = { confidence: fields.deedCount.confidence, approved: false }
  }
  if (fields.platCount.present && fields.platCount.value != null) {
    next.platCount = fields.platCount.value
    hints.platCount = { confidence: fields.platCount.confidence, approved: false }
  }
  if (fields.workedOn.present && fields.workedOn.value) {
    next.workedOn = dayjs(fields.workedOn.value)
    hints.workedOn = { confidence: fields.workedOn.confidence, approved: false }
  }

  return { next, hints }
}

function AiField({
  field,
  hints,
  onApprove,
  children,
}: {
  field: AiFieldKey
  hints: Partial<Record<AiFieldKey, AiHint>>
  onApprove: (field: AiFieldKey) => void
  children: ReactNode
}) {
  const hint = hints[field]
  if (!hint || hint.approved) return <>{children}</>
  return (
    <div className="ai-fill-field">
      {children}
      <div className="ai-fill-meta">
        <span>AI {Math.round(hint.confidence * 100)}%</span>
        <Button size="small" type="link" style={{ paddingInline: 0, height: 20 }} onClick={() => onApprove(field)}>
          Approve
        </Button>
      </div>
    </div>
  )
}

export function ViewDocumentPage() {
  const { id } = useParams<{ id: string }>()
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const [item, setItem] = useState<WorkItemDetail | null>(null)
  const [neighbors, setNeighbors] = useState<{ previousId?: string | null; nextId?: string | null }>({})
  const [types, setTypes] = useState<LookupItem[]>([])
  const [assignees, setAssignees] = useState<AssignableUser[]>([])
  const [actions, setActions] = useState<StatusActions | null>(null)
  const [saved, setSaved] = useState<Draft | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [reloadNonce, setReloadNonce] = useState(0)
  const [saving, setSaving] = useState(false)
  const [filling, setFilling] = useState(false)
  const [aiHints, setAiHints] = useState<Partial<Record<AiFieldKey, AiHint>>>({})
  const [aiOverall, setAiOverall] = useState<number | null>(null)

  const query: WorkItemQuery = {
    search: params.get('search') ?? undefined,
    organizationId: params.get('organizationId') ?? undefined,
    documentTypeId: params.get('documentTypeId') ?? undefined,
    statusId: params.get('statusId') ?? undefined,
    uploadedFrom: params.get('uploadedFrom') ?? undefined,
    uploadedTo: params.get('uploadedTo') ?? undefined,
    sortBy: params.get('sortBy') ?? 'uploadedAt',
    sortDir: params.get('sortDir') ?? 'desc',
    bucket: params.get('bucket') ?? undefined,
  }

  useEffect(() => {
    if (!id) return
    setError(null)
    setAiHints({})
    setAiOverall(null)
    Promise.all([
      api.workItem(id),
      api.neighbors(id, query),
      api.documentTypes(),
      api.statusActions(),
    ])
      .then(([detail, next, typeList, statusActions]) => {
        setItem(detail)
        const snap = toDraft(detail)
        setSaved(snap)
        setDraft(snap)
        setNeighbors(next)
        setTypes(typeList)
        setActions(statusActions)
        if (detail.canMutate) {
          return api.assignableUsers(detail.organizationId).then(setAssignees)
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Document was not found.'))
  }, [id, params, reloadNonce])

  const dirty = useMemo(() => !!draft && !!saved && !sameDraft(draft, saved), [draft, saved])
  const pendingAi = useMemo(
    () => (Object.entries(aiHints) as [AiFieldKey, AiHint][]).filter(([, hint]) => !hint.approved),
    [aiHints],
  )

  const patchDraft = (update: Partial<Draft>, field?: AiFieldKey) => {
    setDraft((current) => (current ? { ...current, ...update } : current))
    if (field) {
      setAiHints((current) => {
        if (!current[field]) return current
        const next = { ...current }
        delete next[field]
        return next
      })
    }
  }

  const approveField = (field: AiFieldKey) => {
    setAiHints((current) => {
      const hint = current[field]
      if (!hint) return current
      return { ...current, [field]: { ...hint, approved: true } }
    })
  }

  const approveAll = () => {
    setAiHints((current) => {
      const next: Partial<Record<AiFieldKey, AiHint>> = {}
      for (const [key, hint] of Object.entries(current) as [AiFieldKey, AiHint][]) {
        next[key] = { ...hint, approved: true }
      }
      return next
    })
  }

  const runAiFill = async () => {
    if (!item || !draft) return
    setFilling(true)
    try {
      const result = await api.aiFillFromPdf(item.id)
      const applied = applyAiFill(draft, result)
      setAiHints(applied.hints)
      setAiOverall(result.overallConfidence)
      if (Object.keys(applied.hints).length === 0) {
        message.warning(result.warning || 'No fields could be filled from this PDF.')
        return
      }
      setDraft(applied.next)
      message.success('AI fill applied to the form. Review amber fields, then Save. AI fill does not persist on its own.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'AI fill failed.')
    } finally {
      setFilling(false)
    }
  }

  const requestAiFill = () => {
    if (dirty) {
      Modal.confirm({
        title: 'Replace unsaved edits?',
        content: 'AI fill from PDF will overwrite Title, Type, Property IDs, counts, and Worked date when the PDF has them. Status, assignee, and flags stay as they are. Save is still required.',
        okText: 'Fill from PDF',
        onOk: () => runAiFill(),
      })
      return
    }
    void runAiFill()
  }

  if (error) {
    return (
      <Card>
        <Typography.Title level={4}>Document not available</Typography.Title>
        <LoadError message={error} onRetry={() => { setError(null); setReloadNonce((n) => n + 1) }} />
        <div style={{ marginTop: 12 }}>
          <Link to={`/documents?${params.toString()}`}>Back to Manage Documents</Link>
        </div>
      </Card>
    )
  }

  if (!item || !id || !draft) return <Spin />

  const layout = params.get('layout')
  const showForm = layout !== 'viewer'
  const showViewer = layout !== 'form'
  const typeLabel = types.find((t) => t.id === draft.documentTypeId)?.name ?? item.documentTypeName
  const priorityOn = item.canMutate ? draft.isPriority : !!item.isPriority
  const neededBy = item.canMutate
    ? (draft.priorityNeededBy ? draft.priorityNeededBy.toISOString() : null)
    : item.priorityNeededBy
  const neededLabel = neededByLabel(neededBy)
  const priorityNote = item.canMutate ? draft.priorityNote : item.priorityNote
  const priorityStrip = priorityOn ? (
    <div className="detail-priority">
      <Tag color="red" style={{ marginInlineEnd: 0 }}>Priority</Tag>
      {neededLabel && (
        <Tag color={isNeededByOverdue(true, neededBy) ? 'volcano' : 'gold'} style={{ marginInlineEnd: 0 }}>
          {isNeededByOverdue(true, neededBy) ? `Overdue · ${neededLabel}` : neededLabel}
        </Tag>
      )}
      {priorityNote ? <span>{priorityNote}</span> : null}
    </div>
  ) : null
  const isPopout = params.get('popout') === '1'

  const openPopout = (mode: 'viewer' | 'form') => {
    const next = new URLSearchParams()
    next.set('layout', mode)
    next.set('popout', '1')
    const url = `${window.location.origin}/documents/${id}?${next.toString()}`
    window.open(url, `gis-${mode}-${id}`, 'noopener,noreferrer,width=1280,height=860')
  }

  const neighborSearch = () => {
    const next = new URLSearchParams(params)
    return next.toString()
  }

  const setStatus = (statusId: string) => {
    setDraft((current) => {
      if (!current || !actions) return current
      const completing = statusId === actions.completeId || actions.completedIds.includes(statusId)
      return {
        ...current,
        statusId,
        workedOn: completing && !current.workedOn ? dayjs() : current.workedOn,
      }
    })
  }

  const save = async () => {
    setSaving(true)
    try {
      const updated = await api.updateWorkItem(item.id, {
        title: draft.title,
        statusId: draft.statusId,
        documentTypeId: draft.documentTypeId,
        assignedToUserId: draft.assignedToUserId,
        clearAssignment: !draft.assignedToUserId,
        isSplit: draft.isSplit,
        isSketch: draft.isSketch,
        isPriority: draft.isPriority,
        isReviewed: draft.isReviewed,
        priorityNote: draft.priorityNote,
        priorityNeededBy: draft.priorityNeededBy ? draft.priorityNeededBy.startOf('day').toISOString() : null,
        clearPriorityNeededBy: !draft.priorityNeededBy,
        workedOn: draft.workedOn ? draft.workedOn.toISOString() : null,
        clearWorkedOn: !draft.workedOn,
        annexationCount: draft.annexationCount,
        correctionCount: draft.correctionCount,
        deedCount: draft.deedCount,
        platCount: draft.platCount,
        propertyIds: draft.propertyIds,
      })
      setItem(updated)
      const snap = toDraft(updated)
      setSaved(snap)
      setDraft(snap)
      setAiHints({})
      setAiOverall(null)
      message.success('Work item saved.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Save failed.')
    } finally {
      setSaving(false)
    }
  }

  const aiButton = item.canMutate ? (
    <Tooltip title="Fills Title, Type, Property IDs, counts, and Worked date from the PDF text. Status, assignee, and flags are not changed. Save is still required.">
      <Button
        size="small"
        icon={<ThunderboltOutlined />}
        loading={filling}
        onClick={requestAiFill}
      >
        AI fill from PDF
      </Button>
    </Tooltip>
  ) : null

  return (
    <div className={`detail-page${isPopout ? ' is-popout' : ''}`}>
      <div className="detail-toolbar">
        <div className="detail-toolbar-lead">
          {!isPopout && (
            <Button size="small" onClick={() => navigate(`/documents?${params.toString()}`)}>Back to grid</Button>
          )}
          <Tag color={item.statusColor} style={{ marginInlineEnd: 0 }}>{statusLabel(item.statusName)}</Tag>
          <Typography.Text strong className="detail-toolbar-title" ellipsis={{ tooltip: draft.title || item.title }}>
            {draft.title || item.title}
          </Typography.Text>
          {aiOverall != null && pendingAi.length > 0 && (
            <Tag color="gold" style={{ marginInlineEnd: 0 }}>AI fill {Math.round(aiOverall * 100)}%</Tag>
          )}
        </div>
        <Space size={8} wrap>
          {aiOverall != null && pendingAi.length > 0 && (
            <Button size="small" onClick={approveAll}>Approve AI fields</Button>
          )}
          {!isPopout && (
            <Dropdown
              menu={{
                items: [
                  { key: 'viewer', label: 'Open viewer in new window', onClick: () => openPopout('viewer') },
                  { key: 'form', label: 'Open work item in new window', onClick: () => openPopout('form') },
                ],
              }}
            >
              <Button size="small" icon={<ExportOutlined />}>Pop out</Button>
            </Dropdown>
          )}
          <Button
            size="small"
            icon={<LeftOutlined />}
            disabled={!neighbors.previousId}
            onClick={() => neighbors.previousId && navigate(`/documents/${neighbors.previousId}?${neighborSearch()}`)}
          >
            Previous
          </Button>
          <Button
            size="small"
            disabled={!neighbors.nextId}
            onClick={() => neighbors.nextId && navigate(`/documents/${neighbors.nextId}?${neighborSearch()}`)}
          >
            Next <RightOutlined />
          </Button>
        </Space>
      </div>

      <div className={`split-viewer${showForm && showViewer ? '' : ' is-single'}`}>
        {showForm && (
        <div className="detail-pane">
          <Card
            className="detail-form-card"
            title="Work item"
            size="small"
            extra={(
              <Space size={8} wrap>
                {aiButton}
                <Button size="small" icon={<DownloadOutlined />} onClick={() => void downloadFile(item.id, item.fileName)}>
                  Download
                </Button>
              </Space>
            )}
          >
            {priorityStrip}

            {item.canMutate ? (
              <div className="detail-fields">
                <AiField field="title" hints={aiHints} onApprove={approveField}>
                  <Input size="small" value={draft.title} onChange={(e) => patchDraft({ title: e.target.value }, 'title')} />
                </AiField>
                {actions && (
                  <Select
                    size="small"
                    style={{ width: '100%' }}
                    value={draft.statusId}
                    onChange={(value) => setStatus(value)}
                    options={[
                      { value: actions.activeId, label: 'Active' },
                      { value: actions.pendingId, label: 'Pending' },
                      { value: actions.completeId, label: 'Complete' },
                      { value: actions.onHoldId, label: 'On-Hold' },
                      { value: actions.cancelledId, label: 'Cancelled' },
                      ...(![
                        actions.activeId,
                        actions.pendingId,
                        actions.completeId,
                        actions.onHoldId,
                        actions.cancelledId,
                      ].includes(draft.statusId)
                        ? [{ value: draft.statusId, label: statusLabel(item.statusName) }]
                        : []),
                    ]}
                  />
                )}
                <AiField field="documentTypeId" hints={aiHints} onApprove={approveField}>
                  <Select
                    size="small"
                    style={{ width: '100%' }}
                    value={draft.documentTypeId}
                    options={types.map((t) => ({ value: t.id, label: t.name }))}
                    onChange={(documentTypeId) => patchDraft({ documentTypeId }, 'documentTypeId')}
                  />
                </AiField>
                <Select
                  allowClear
                  size="small"
                  style={{ width: '100%' }}
                  placeholder="Assigned to"
                  value={draft.assignedToUserId ?? undefined}
                  options={assignees.map((u) => ({ value: u.id, label: u.displayName }))}
                  onChange={(value) => setDraft({ ...draft, assignedToUserId: value ?? null })}
                />
                <Space wrap size={[12, 4]} className="detail-flags">
                  <Checkbox checked={draft.isSplit} onChange={(e) => setDraft({ ...draft, isSplit: e.target.checked })}>
                    Split?
                  </Checkbox>
                  <Checkbox checked={draft.isSketch} onChange={(e) => setDraft({ ...draft, isSketch: e.target.checked })}>
                    Sketch?
                  </Checkbox>
                  {item.canSetPriority && (
                    <Checkbox checked={draft.isPriority} onChange={(e) => setDraft({ ...draft, isPriority: e.target.checked })}>
                      Priority
                    </Checkbox>
                  )}
                  {item.canMutate && (
                    <Checkbox checked={draft.isReviewed} onChange={(e) => setDraft({ ...draft, isReviewed: e.target.checked })}>
                      Reviewed
                    </Checkbox>
                  )}
                </Space>
                {item.canSetPriority && draft.isPriority && (
                  <>
                    <DatePicker
                      size="small"
                      style={{ width: '100%' }}
                      value={draft.priorityNeededBy}
                      onChange={(value) => setDraft({ ...draft, priorityNeededBy: value })}
                      placeholder="Needed by"
                    />
                    <Input
                      size="small"
                      value={draft.priorityNote}
                      maxLength={500}
                      placeholder="Why this is priority — visible to techs"
                      onChange={(e) => setDraft({ ...draft, priorityNote: e.target.value })}
                    />
                  </>
                )}
                <AiField field="workedOn" hints={aiHints} onApprove={approveField}>
                  <DatePicker
                    size="small"
                    style={{ width: '100%' }}
                    value={draft.workedOn}
                    onChange={(value) => patchDraft({ workedOn: value }, 'workedOn')}
                    placeholder="Worked date"
                  />
                </AiField>
                <Row gutter={[8, 8]}>
                  <Col span={12}>
                    <div className="detail-field-label">Annexations</div>
                    <AiField field="annexationCount" hints={aiHints} onApprove={approveField}>
                      <InputNumber size="small" min={0} style={{ width: '100%' }} value={draft.annexationCount} onChange={(v) => patchDraft({ annexationCount: v ?? 0 }, 'annexationCount')} />
                    </AiField>
                  </Col>
                  <Col span={12}>
                    <div className="detail-field-label">Corrections</div>
                    <AiField field="correctionCount" hints={aiHints} onApprove={approveField}>
                      <InputNumber size="small" min={0} style={{ width: '100%' }} value={draft.correctionCount} onChange={(v) => patchDraft({ correctionCount: v ?? 0 }, 'correctionCount')} />
                    </AiField>
                  </Col>
                  <Col span={12}>
                    <div className="detail-field-label">Deeds</div>
                    <AiField field="deedCount" hints={aiHints} onApprove={approveField}>
                      <InputNumber size="small" min={0} style={{ width: '100%' }} value={draft.deedCount} onChange={(v) => patchDraft({ deedCount: v ?? 0 }, 'deedCount')} />
                    </AiField>
                  </Col>
                  <Col span={12}>
                    <div className="detail-field-label">Plats/Replats</div>
                    <AiField field="platCount" hints={aiHints} onApprove={approveField}>
                      <InputNumber size="small" min={0} style={{ width: '100%' }} value={draft.platCount} onChange={(v) => patchDraft({ platCount: v ?? 0 }, 'platCount')} />
                    </AiField>
                  </Col>
                </Row>
                <AiField field="propertyIds" hints={aiHints} onApprove={approveField}>
                  <Input.TextArea
                    rows={3}
                    value={draft.propertyIds}
                    onChange={(e) => patchDraft({ propertyIds: e.target.value }, 'propertyIds')}
                    placeholder="Property IDs — one per line"
                  />
                </AiField>
                <Space>
                  <Button size="small" disabled={!dirty} onClick={() => {
                    if (saved) setDraft(saved)
                    setAiHints({})
                    setAiOverall(null)
                  }}>Undo</Button>
                  <Button size="small" type="primary" loading={saving} disabled={!dirty} onClick={() => void save()}>Save</Button>
                </Space>
              </div>
            ) : (
              <div className="detail-readonly">
                <span>Assigned to {item.assignedToName || 'Unassigned'}</span>
                {item.isPriority && neededByLabel(item.priorityNeededBy) ? (
                  <span>{isNeededByOverdue(item.isPriority, item.priorityNeededBy) ? 'Overdue · ' : ''}{neededByLabel(item.priorityNeededBy)}</span>
                ) : null}
                <span>
                  Split? {item.isSplit ? 'Yes' : 'No'} · Sketch? {item.isSketch ? 'Yes' : 'No'} · Reviewed {item.isReviewed ? 'Yes' : 'No'}
                </span>
                <span>Worked {item.workedOn ? dayjs(item.workedOn).format('YYYY-MM-DD') : '—'}</span>
                <span>
                  Annexations {item.annexationCount} · Corrections {item.correctionCount} · Deeds {item.deedCount} · Plats/Replats {item.platCount}
                </span>
                <span style={{ whiteSpace: 'pre-wrap' }}>{item.propertyIds || 'No property IDs.'}</span>
              </div>
            )}
          </Card>

          <CommentsPanel workItemId={id} canPost={item.canPostComments} />
          <InternalNotesPanel item={item} onUpdated={setItem} />
          <TimeLogPanel item={item} />
          <div className="detail-pane-footer">
            <div>{item.organizationName} · {typeLabel}</div>
            <div>
              Uploaded {dayjs(item.uploadedAt).format('YYYY-MM-DD HH:mm')} by {item.uploadedByName}
              {' · '}Hours {item.hoursLabel}
            </div>
          </div>
        </div>
        )}

        {showViewer && (
        <Card
          className="viewer-pane"
          title="Viewer"
          size="small"
          extra={!showForm ? (
            <Button size="small" icon={<DownloadOutlined />} onClick={() => void downloadFile(item.id, item.fileName)}>
              Download
            </Button>
          ) : undefined}
        >
          <DocumentViewer workItemId={id} contentType={item.contentType} fileName={item.fileName} />
        </Card>
        )}
      </div>
    </div>
  )
}
