import { DownloadOutlined, ExportOutlined, LeftOutlined, QuestionCircleOutlined, RightOutlined, ThunderboltOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Checkbox, Col, DatePicker, Dropdown, Input, InputNumber, Modal, Row, Select, Space, Spin, Tag, Tooltip, Typography, message } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import type { AiFillResponse, AssignableUser, DocumentDifficultyBand, LookupItem, StatusActions, WorkItemDetail, WorkItemQuery } from '../api'
import { api, authorizedBlob } from '../api'
import {
  applyAiFill as applyAiFillFields,
  applyAutoAiScan,
  isAiScanFailed,
  isAiScanInFlight,
  type AiFieldKey,
  type AiFillDraft,
  type AiHint,
} from '../aiScan'
import { AI_FILL_HELP_TITLE } from '../aiFillHelp'
import { AiFillHelpFirstReviewTip, AiFillHelpModal, AiFillHelpOpenButton } from '../components/AiFillHelpScreen'
import { CommentsPanel } from '../components/CommentsPanel'
import { LoadError } from '../components/LoadError'
import { DocumentViewer } from '../components/DocumentViewer'
import { InternalNotesPanel } from '../components/InternalNotesPanel'
import { WorkPresenceMarks } from '../components/PresencePeople'
import { TimeLogPanel } from '../components/TimeLogPanel'
import { ASSIGNED_TO_HELP, ASSIGNED_TO_LABEL, UNASSIGNED_LABEL } from '../assignmentLabels'
import { isNeededByOverdue } from '../neededBy'
import { statusSelectOptions } from '../statusSelectOptions'
import { statusLabel } from '../statusLabels'
import { DocumentDifficultyChip } from '../components/DocumentDifficultyChip'
import { DIFFICULTY_BANDS, difficultyReasons, isDifficultyBand } from '../documentDifficulty'
import {
  NEEDED_BY_LABEL,
  WORKED_LABEL,
  changeNeededBy,
  neededByBadgeText,
  toDateSavePayload,
} from '../workItemDates'

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

function toAiDraft(draft: Draft): AiFillDraft {
  return {
    title: draft.title,
    documentTypeId: draft.documentTypeId,
    propertyIds: draft.propertyIds,
    annexationCount: draft.annexationCount,
    correctionCount: draft.correctionCount,
    deedCount: draft.deedCount,
    platCount: draft.platCount,
    workedOn: draft.workedOn ? draft.workedOn.format('YYYY-MM-DD') : null,
  }
}

function mergeAiDraft(current: Draft, next: AiFillDraft): Draft {
  return {
    ...current,
    title: next.title,
    documentTypeId: next.documentTypeId,
    propertyIds: next.propertyIds,
    annexationCount: next.annexationCount,
    correctionCount: next.correctionCount,
    deedCount: next.deedCount,
    platCount: next.platCount,
    workedOn: next.workedOn ? dayjs(next.workedOn) : null,
  }
}

function applyAiFill(current: Draft, result: AiFillResponse): { next: Draft; hints: Partial<Record<AiFieldKey, AiHint>> } {
  const applied = applyAiFillFields(toAiDraft(current), result)
  return { next: mergeAiDraft(current, applied.next), hints: applied.hints }
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
  const [appliedScanKey, setAppliedScanKey] = useState<string | null>(null)
  const [aiFillHelpOpen, setAiFillHelpOpen] = useState(false)

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
    setAppliedScanKey(null)
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

  useEffect(() => {
    if (!id || !item || !isAiScanInFlight(item.aiScan?.status)) return
    const timer = window.setInterval(() => {
      api.workItem(id)
        .then((fresh) => {
          setItem((current) => {
            if (!current) return fresh
            return {
              ...fresh,
              // Keep in-form difficulty if a local fill already set it.
              difficulty: fresh.difficulty ?? current.difficulty,
            }
          })
        })
        .catch(() => undefined)
    }, 2000)
    return () => window.clearInterval(timer)
  }, [id, item?.aiScan?.status])

  useEffect(() => {
    if (!item?.canMutate || !draft || !item.aiScan || item.aiScan.status !== 'succeeded' || !item.aiScan.result) {
      return
    }
    const key = `${item.id}:${item.aiScan.completedAt ?? 'done'}`
    if (appliedScanKey === key) return
    const applied = applyAutoAiScan(toAiDraft(draft), item.aiScan.result, item.aiScan.baseline)
    setAppliedScanKey(key)
    setAiOverall(item.aiScan.result.overallConfidence)
    if (item.aiScan.result.difficulty) {
      setItem((current) => (current ? { ...current, difficulty: item.aiScan?.result?.difficulty ?? current.difficulty } : current))
    }
    if (Object.keys(applied.hints).length === 0) {
      return
    }
    setAiHints((current) => ({ ...current, ...applied.hints }))
    setDraft(mergeAiDraft(draft, applied.next))
    message.success('AI scan applied to the form. Review amber fields, then Save. Difficulty is saved from this pass; field fill still needs Save.')
  }, [appliedScanKey, draft, item])

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

  const runAiFill = async (rescore = false) => {
    if (!item || !draft) return
    setFilling(true)
    try {
      const result = await api.aiFillFromPdf(item.id, { rescore })
      const applied = applyAiFill(draft, result)
      setAiHints(applied.hints)
      setAiOverall(result.overallConfidence)
      setItem((current) => (current ? { ...current, difficulty: result.difficulty } : current))
      if (result.difficulty.keptOverride) {
        message.info('Staff difficulty override kept. Confirm re-score or clear the override to use the new AI band.')
      }
      if (Object.keys(applied.hints).length === 0) {
        message.warning(result.warning || 'No fields could be filled from this PDF.')
        return
      }
      setDraft(applied.next)
      message.success('AI fill applied to the form. Review amber fields, then Save. Difficulty is saved from this pass; field fill still needs Save.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'AI fill failed.')
    } finally {
      setFilling(false)
    }
  }

  const startAiFill = () => {
    if (item?.difficulty?.overridden) {
      Modal.confirm({
        title: 'Replace staff difficulty override?',
        content: 'This document has a staff Easy / Medium / Hard override. Re-score only if you confirm. Keep override still fills fields from the PDF.',
        okText: 'Fill and re-score',
        cancelText: 'Fill, keep override',
        onOk: () => runAiFill(true),
        onCancel: () => { void runAiFill(false) },
      })
      return
    }
    void runAiFill(false)
  }

  const requestAiFill = () => {
    if (dirty) {
      Modal.confirm({
        title: 'Replace unsaved edits?',
        content: 'AI fill from PDF will overwrite Title, Type, Property IDs, counts, and Worked date when the PDF has them — including scanned or image-only files read from page images. Status, assignee, and flags stay as they are. Save is still required for those fields. A staff difficulty override is not wiped unless you confirm re-score.',
        okText: 'Fill from PDF',
        onOk: () => startAiFill(),
      })
      return
    }
    startAiFill()
  }

  const overrideDifficulty = async (band: DocumentDifficultyBand) => {
    if (!item) return
    try {
      const updated = await api.updateWorkItem(item.id, { difficultyBand: band })
      setItem(updated)
      message.success(`Difficulty set to ${band}.`)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not override difficulty.')
    }
  }

  const clearDifficultyOverride = async () => {
    if (!item) return
    try {
      const updated = await api.updateWorkItem(item.id, { clearDifficultyOverride: true })
      setItem(updated)
      message.success('Staff override cleared. Showing the last AI score.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not clear difficulty override.')
    }
  }

  if (error) {
    return (
      <Card className="bis-theme-panel">
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
  const workedForBadge = item.canMutate
    ? (draft.workedOn ? draft.workedOn.toISOString() : null)
    : item.workedOn
  const neededLabel = neededByBadgeText({ neededBy, workedOn: workedForBadge })
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
        ...toDateSavePayload({
          workedOn: draft.workedOn ? draft.workedOn.toISOString() : null,
          priorityNeededBy: draft.priorityNeededBy ? draft.priorityNeededBy.startOf('day').toISOString() : null,
        }),
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
    <Tooltip title="Fills Title, Type, Property IDs, counts, and Worked date from the PDF text, or from page images when the file is scanned / image-only. Scores Easy / Medium / Hard on the same pass. Status, assignee, and flags are not changed. Field fill still needs Save. A staff difficulty override sticks unless you confirm re-score.">
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
          <DocumentDifficultyChip difficulty={item.difficulty} />
          <Typography.Text strong className="detail-toolbar-title" ellipsis={{ tooltip: draft.title || item.title }}>
            {draft.title || item.title}
          </Typography.Text>
          {aiOverall != null && pendingAi.length > 0 && (
            <Tag color="gold" style={{ marginInlineEnd: 0 }}>AI fill {Math.round(aiOverall * 100)}%</Tag>
          )}
        </div>
        <Space size={8} wrap>
          <AiFillHelpOpenButton onOpen={() => setAiFillHelpOpen(true)} />
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
            className="detail-form-card bis-theme-panel"
            title={(
              <span className="title-with-help">
                Work item
                <button
                  type="button"
                  className="help-tip-button"
                  aria-label={AI_FILL_HELP_TITLE}
                  onClick={() => setAiFillHelpOpen(true)}
                >
                  <QuestionCircleOutlined className="help-tip" />
                </button>
              </span>
            )}
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
            <AiFillHelpFirstReviewTip onOpen={() => setAiFillHelpOpen(true)} />
            {isAiScanInFlight(item.aiScan?.status) && (
              <Alert
                className="ai-scan-banner"
                type="info"
                showIcon
                message={item.aiScan?.message || 'AI scan pending.'}
              />
            )}
            {isAiScanFailed(item.aiScan?.status) && (
              <Alert
                className="ai-scan-banner"
                type="warning"
                showIcon
                message={item.aiScan?.message || 'AI scan failed. Use AI fill from PDF to retry.'}
              />
            )}
            <div className="document-difficulty-panel">
              <div className="detail-field-label">Difficulty</div>
              {item.difficulty?.band ? (
                <>
                  <Space wrap size={8} align="start">
                    <DocumentDifficultyChip difficulty={item.difficulty} />
                    {item.canMutate && (
                      <Select
                        size="small"
                        aria-label="Override difficulty"
                        style={{ width: 140 }}
                        value={item.difficulty.band}
                        options={DIFFICULTY_BANDS.map((band) => ({ value: band, label: band }))}
                        onChange={(band) => {
                          if (isDifficultyBand(band)) void overrideDifficulty(band)
                        }}
                      />
                    )}
                    {item.canMutate && item.difficulty.overridden && (
                      <Button size="small" type="link" style={{ paddingInline: 0 }} onClick={() => void clearDifficultyOverride()}>
                        Use AI score
                      </Button>
                    )}
                  </Space>
                  <ul className="document-difficulty-reasons">
                    {difficultyReasons(item.difficulty).map((reason) => (
                      <li key={reason}>{reason}</li>
                    ))}
                  </ul>
                </>
              ) : (
                <Typography.Text type="secondary">
                  {isAiScanInFlight(item.aiScan?.status)
                    ? 'AI scan pending… Easy / Medium / Hard will appear on this same pass.'
                    : isAiScanFailed(item.aiScan?.status)
                      ? 'AI scan failed. Use AI fill from PDF to retry Easy / Medium / Hard.'
                      : item.canMutate
                        ? 'Run AI fill from PDF to score Easy / Medium / Hard from this extract.'
                        : 'Not scored yet.'}
                </Typography.Text>
              )}
            </div>

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
                    options={statusSelectOptions(actions, { id: draft.statusId, name: item.statusName })}
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
                <div>
                  <div className="detail-field-label" title={ASSIGNED_TO_HELP}>{ASSIGNED_TO_LABEL}</div>
                  <Select
                    allowClear
                    size="small"
                    style={{ width: '100%' }}
                    placeholder={UNASSIGNED_LABEL}
                    value={draft.assignedToUserId ?? undefined}
                    options={assignees.map((u) => ({ value: u.id, label: u.displayName }))}
                    onChange={(value) => setDraft({ ...draft, assignedToUserId: value ?? null })}
                  />
                </div>
                <WorkPresenceMarks workItemId={item.id} assignedToUserId={draft.assignedToUserId} />
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
                  <Typography.Text type="secondary">Total time {item.hoursLabel || '0m'}</Typography.Text>
                </Space>
                {item.canSetPriority && draft.isPriority && (
                  <>
                    <div>
                      <div className="detail-field-label">{NEEDED_BY_LABEL}</div>
                      <DatePicker
                        size="small"
                        style={{ width: '100%' }}
                        value={draft.priorityNeededBy}
                        onChange={(value) => setDraft((current) => (current ? changeNeededBy(current, value) : current))}
                      />
                    </div>
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
                  <div>
                    <div className="detail-field-label">{WORKED_LABEL}</div>
                    <DatePicker
                      size="small"
                      style={{ width: '100%' }}
                      value={draft.workedOn}
                      onChange={(value) => patchDraft({ workedOn: value }, 'workedOn')}
                    />
                  </div>
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
                <span>
                  {ASSIGNED_TO_LABEL} {item.assignedToName || UNASSIGNED_LABEL}
                  <WorkPresenceMarks workItemId={item.id} assignedToUserId={item.assignedToUserId} />
                </span>
                {item.isPriority && neededByBadgeText({ neededBy: item.priorityNeededBy, workedOn: item.workedOn }) ? (
                  <span>{isNeededByOverdue(item.isPriority, item.priorityNeededBy) ? 'Overdue · ' : ''}{neededByBadgeText({ neededBy: item.priorityNeededBy, workedOn: item.workedOn })}</span>
                ) : null}
                <span>
                  Split? {item.isSplit ? 'Yes' : 'No'} · Sketch? {item.isSketch ? 'Yes' : 'No'} · Reviewed {item.isReviewed ? 'Yes' : 'No'} · Total time {item.hoursLabel || '0m'}
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
              {' · '}Total time {item.hoursLabel || '0m'}
            </div>
          </div>
        </div>
        )}

        {showViewer && (
        <Card
          className="viewer-pane bis-theme-panel"
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
      <AiFillHelpModal open={aiFillHelpOpen} onClose={() => setAiFillHelpOpen(false)} />
    </div>
  )
}
