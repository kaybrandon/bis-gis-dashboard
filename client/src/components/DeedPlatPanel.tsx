import { Button, Card, Input, Space, Typography, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import type { AiFillResponse, WorkItemDetail } from '../api'
import { api } from '../api'
import { applyAiFill, applyAutoAiScan, type AiFillDraft, type AiFieldKey, type AiHint } from '../aiScan'
import { TitleWithHelp } from './HelpTip'

type DeedPlatDraft = {
  survey: string
  abstract: string
  lotBlock: string
  subdivision: string
  legalDescription: string
}

function fromItem(item: WorkItemDetail): DeedPlatDraft {
  return {
    survey: item.survey ?? '',
    abstract: item.abstract ?? '',
    lotBlock: item.lotBlock ?? '',
    subdivision: item.subdivision ?? '',
    legalDescription: item.legalDescription ?? '',
  }
}

function toAiDraft(item: WorkItemDetail, draft: DeedPlatDraft): AiFillDraft {
  return {
    title: item.title,
    documentTypeId: item.documentTypeId,
    propertyIds: item.propertyIds ?? '',
    annexationCount: item.annexationCount,
    correctionCount: item.correctionCount,
    deedCount: item.deedCount,
    platCount: item.platCount,
    workedOn: item.workedOn ? item.workedOn.slice(0, 10) : null,
    ...draft,
    deedPlatManual: item.deedPlatManual,
  }
}

function pickDeedPlat(next: AiFillDraft): DeedPlatDraft {
  return {
    survey: next.survey,
    abstract: next.abstract,
    lotBlock: next.lotBlock,
    subdivision: next.subdivision,
    legalDescription: next.legalDescription,
  }
}

export function DeedPlatPanel({
  item,
  onUpdated,
  aiFill,
  aiFillKey,
}: {
  item: WorkItemDetail
  onUpdated: (next: WorkItemDetail) => void
  aiFill?: AiFillResponse | null
  aiFillKey?: string | null
}) {
  const [draft, setDraft] = useState<DeedPlatDraft>(() => fromItem(item))
  const [saved, setSaved] = useState<DeedPlatDraft>(() => fromItem(item))
  const [saving, setSaving] = useState(false)
  const [hints, setHints] = useState<Partial<Record<AiFieldKey, AiHint>>>({})
  const [appliedKey, setAppliedKey] = useState<string | null>(null)

  useEffect(() => {
    const next = fromItem(item)
    setDraft(next)
    setSaved(next)
    setHints({})
    setAppliedKey(null)
  }, [item.id])

  useEffect(() => {
    const next = fromItem(item)
    setDraft((current) => (JSON.stringify(current) === JSON.stringify(saved) ? next : current))
    setSaved(next)
  }, [item.survey, item.abstract, item.lotBlock, item.subdivision, item.legalDescription])

  useEffect(() => {
    const scan = item.aiScan
    if (!scan || scan.status !== 'succeeded' || !scan.result) return
    const key = `scan:${item.id}:${scan.completedAt ?? 'done'}`
    if (appliedKey === key) return
    const applied = applyAutoAiScan(toAiDraft(item, draft), scan.result, scan.baseline)
    setAppliedKey(key)
    const next = pickDeedPlat(applied.next)
    if (JSON.stringify(next) === JSON.stringify(draft)) return
    setDraft(next)
    setHints((current) => ({ ...current, ...applied.hints }))
  }, [appliedKey, draft, item])

  useEffect(() => {
    if (!aiFill || !aiFillKey) return
    if (appliedKey === aiFillKey) return
    const applied = applyAiFill(toAiDraft(item, draft), aiFill)
    setAppliedKey(aiFillKey)
    const next = pickDeedPlat(applied.next)
    if (JSON.stringify(next) === JSON.stringify(draft)) return
    setDraft(next)
    setHints((current) => ({ ...current, ...applied.hints }))
  }, [aiFill, aiFillKey, appliedKey, draft, item])

  const dirty = useMemo(() => JSON.stringify(draft) !== JSON.stringify(saved), [draft, saved])

  async function save() {
    setSaving(true)
    try {
      const updated = await api.updateWorkItem(item.id, {
        survey: draft.survey,
        abstract: draft.abstract,
        lotBlock: draft.lotBlock,
        subdivision: draft.subdivision,
        legalDescription: draft.legalDescription,
      })
      onUpdated(updated)
      const snap = fromItem(updated)
      setDraft(snap)
      setSaved(snap)
      setHints({})
      message.success('Deed / plat fields saved.')
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not save deed / plat fields.')
    } finally {
      setSaving(false)
    }
  }

  const hint = (field: AiFieldKey) => {
    const value = hints[field]
    if (!value || value.approved) return null
    return (
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        AI {Math.round(value.confidence * 100)}% — review, then Save
      </Typography.Text>
    )
  }

  return (
    <Card
      size="small"
      className="bis-theme-panel deed-plat-panel"
      title={(
        <TitleWithHelp help="Survey, Abstract, Lot/Block, Subdivision, and Legal Description extracted from the deed or plat when the source supports them. Missing stays blank. AI fill will not overwrite values you have saved.">
          Deed / plat data
        </TitleWithHelp>
      )}
    >
      {item.canMutate ? (
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          <label className="deed-plat-field">
            <span>Survey</span>
            <Input
              value={draft.survey}
              onChange={(e) => setDraft((current) => ({ ...current, survey: e.target.value }))}
              placeholder="Blank if not on the document"
              aria-label="Survey"
            />
            {hint('survey')}
          </label>
          <label className="deed-plat-field">
            <span>Abstract</span>
            <Input
              value={draft.abstract}
              onChange={(e) => setDraft((current) => ({ ...current, abstract: e.target.value }))}
              placeholder="Blank if not on the document"
              aria-label="Abstract"
            />
            {hint('abstract')}
          </label>
          <label className="deed-plat-field">
            <span>Lot/Block</span>
            <Input
              value={draft.lotBlock}
              onChange={(e) => setDraft((current) => ({ ...current, lotBlock: e.target.value }))}
              placeholder="Blank if not on the document"
              aria-label="Lot/Block"
            />
            {hint('lotBlock')}
          </label>
          <label className="deed-plat-field">
            <span>Subdivision</span>
            <Input
              value={draft.subdivision}
              onChange={(e) => setDraft((current) => ({ ...current, subdivision: e.target.value }))}
              placeholder="Blank if not on the document"
              aria-label="Subdivision"
            />
            {hint('subdivision')}
          </label>
          <label className="deed-plat-field">
            <span>Legal Description</span>
            <Input.TextArea
              rows={4}
              value={draft.legalDescription}
              onChange={(e) => setDraft((current) => ({ ...current, legalDescription: e.target.value }))}
              placeholder="Multiline legal description — blank if not on the document"
              aria-label="Legal Description"
            />
            {hint('legalDescription')}
          </label>
          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <Button type="primary" onClick={() => void save()} loading={saving} disabled={!dirty}>
              Save deed / plat
            </Button>
          </div>
        </Space>
      ) : (
        <div className="detail-readonly">
          <span>Survey {item.survey || '—'}</span>
          <span>Abstract {item.abstract || '—'}</span>
          <span>Lot/Block {item.lotBlock || '—'}</span>
          <span>Subdivision {item.subdivision || '—'}</span>
          <span style={{ whiteSpace: 'pre-wrap' }}>Legal Description {item.legalDescription || '—'}</span>
        </div>
      )}
    </Card>
  )
}
