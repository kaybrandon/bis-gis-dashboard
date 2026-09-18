import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  DownloadOutlined,
  FileOutlined,
  InboxOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ThunderboltOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { App, Button, Card, ConfigProvider, DatePicker, Flex, Input, Menu, Pagination, Select, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import type { AssignableUser, LookupItem, OrgOption, StatusActions, WorkItemListItem, WorkItemQuery } from '../api'
import { api } from '../api'
import {
  ASSIGNED_TO_FILTER_HELP,
  ASSIGNED_TO_HELP,
  ASSIGNED_TO_LABEL,
  UNASSIGNED_LABEL,
} from '../assignmentLabels'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { WorkPresenceMarks } from '../components/PresencePeople'
import { WorkItemCards } from '../components/WorkItemCards'
import { useAuth } from '../auth'
import { useIsMobile } from '../layout/useIsMobile'
import { isNeededByOverdue } from '../neededBy'
import { neededByBadgeText } from '../workItemDates'
import { statusSelectOptions } from '../statusSelectOptions'
import { reviewLabel, statusLabel } from '../statusLabels'
import { canSeeDashboardAssignee } from '../roles'
import {
  QUEUE_SORT_BY,
  QUEUE_SORT_DIR,
  UNASSIGNED,
  defaultDocumentsBucket,
  resolveAssigneeFilter,
} from '../staffQueue'
import { manageDocumentsRowClassName, manageDocumentsTableTheme } from '../theme/bisManageDocuments'
import '../theme/bisManageDocuments.css'

type Bucket = 'all' | 'pending' | 'mine' | 'unassigned' | 'hold' | 'completed' | 'firstdeadline' | 'finaldeadline' | 'priority' | 'duethisweek'
type QueuePreset = 'mine' | 'unassigned' | 'priority' | 'duethisweek' | ''

const QUEUE_KEY = 'gis.myQueue'

const bucketKeys: Bucket[] = ['all', 'pending', 'mine', 'unassigned', 'hold', 'completed', 'firstdeadline', 'finaldeadline', 'priority', 'duethisweek']

const bucketItems: { key: Bucket; label: string; countKey: keyof ReturnType<typeof emptyCounts> | null; icon: ReactNode }[] = [
  { key: 'all', label: 'All items', countKey: null, icon: <FileOutlined /> },
  { key: 'pending', label: 'All Pending', countKey: 'pending', icon: <ClockCircleOutlined /> },
  { key: 'mine', label: 'My Work Items', countKey: 'mine', icon: <UserOutlined /> },
  { key: 'unassigned', label: UNASSIGNED_LABEL, countKey: 'unassigned', icon: <InboxOutlined /> },
  { key: 'priority', label: 'Priority', countKey: 'priority', icon: <ThunderboltOutlined /> },
  { key: 'duethisweek', label: 'Due this week', countKey: 'dueThisWeek', icon: <ThunderboltOutlined /> },
  { key: 'hold', label: 'On Hold', countKey: 'onHold', icon: <PauseCircleOutlined /> },
  { key: 'completed', label: 'Completed', countKey: 'completed', icon: <CheckCircleOutlined /> },
  { key: 'firstdeadline', label: 'First Deadline', countKey: 'firstDeadline', icon: <ThunderboltOutlined /> },
  { key: 'finaldeadline', label: 'Final Deadline', countKey: 'finalDeadline', icon: <FileOutlined /> },
]

function emptyCounts() {
  return { pending: 0, mine: 0, onHold: 0, completed: 0, firstDeadline: 0, finalDeadline: 0, priority: 0, dueThisWeek: 0, unassigned: 0 }
}

function readQueuePreset(): QueuePreset {
  try {
    const value = localStorage.getItem(QUEUE_KEY)
    if (value === 'mine' || value === 'unassigned' || value === 'priority' || value === 'duethisweek') return value
  } catch {
    /* ignore */
  }
  return ''
}

function writeQueuePreset(value: QueuePreset) {
  try {
    if (value) localStorage.setItem(QUEUE_KEY, value)
    else localStorage.removeItem(QUEUE_KEY)
  } catch {
    /* ignore */
  }
}

function stopRowClick(event: { stopPropagation: () => void }) {
  event.stopPropagation()
}

function groupValue(item: WorkItemListItem, groupBy?: string) {
  switch (groupBy) {
    case 'status':
      return statusLabel(item.statusName)
    case 'client':
      return item.organizationName
    case 'assignedTo':
      return item.assignedToName ?? 'Unassigned'
    case 'documentType':
      return item.documentTypeName
    default:
      return ''
  }
}

function parseDay(value: string | null): Dayjs | null {
  if (!value) return null
  const day = dayjs(value)
  return day.isValid() ? day : null
}

export function ManageDocumentsPage() {
  const { message } = App.useApp()
  const { user } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const isMobile = useIsMobile()
  const incomingFilters = !!(
    params.get('statusId') ||
    params.get('organizationId') ||
    params.get('assignedToUserId') ||
    params.get('uploadedFrom') ||
    params.get('workedFrom')
  )
  const [items, setItems] = useState<WorkItemListItem[]>([])
  const [total, setTotal] = useState(0)
  const [counts, setCounts] = useState(emptyCounts())
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(25)
  const [sortBy, setSortBy] = useState(QUEUE_SORT_BY)
  const [sortDir, setSortDir] = useState(QUEUE_SORT_DIR)
  const [bucket, setBucket] = useState<Bucket>(() => {
    const fromUrl = params.get('bucket')
    if (fromUrl && bucketKeys.includes(fromUrl as Bucket)) return fromUrl as Bucket
    return defaultDocumentsBucket(null, readQueuePreset(), user, incomingFilters) as Bucket
  })
  const [queuePreset, setQueuePreset] = useState<QueuePreset>(() => {
    const fromUrl = params.get('bucket')
    if (fromUrl === 'mine' || fromUrl === 'unassigned' || fromUrl === 'priority' || fromUrl === 'duethisweek') return fromUrl
    return readQueuePreset()
  })
  const [search, setSearch] = useState('')
  const [orgId, setOrgId] = useState<string | undefined>(() => params.get('organizationId') ?? undefined)
  const [statusId, setStatusId] = useState<string | undefined>(() => params.get('statusId') ?? undefined)
  const [assignedTo, setAssignedTo] = useState<string | undefined>(() =>
    resolveAssigneeFilter(params.get('assignedToUserId'), user),
  )
  const [docTypeId, setDocTypeId] = useState<string | undefined>(() => params.get('documentTypeId') ?? undefined)
  const [uploaded, setUploaded] = useState<[Dayjs | null, Dayjs | null] | null>(() => {
    const from = parseDay(params.get('uploadedFrom'))
    const to = parseDay(params.get('uploadedTo'))
    return from || to ? [from, to] : null
  })
  const [worked, setWorked] = useState<[Dayjs | null, Dayjs | null] | null>(() => {
    const from = parseDay(params.get('workedFrom'))
    const to = parseDay(params.get('workedTo'))
    return from || to ? [from, to] : null
  })
  const [groupBy, setGroupBy] = useState<string | undefined>()
  const [orgs, setOrgs] = useState<OrgOption[]>([])
  const [statuses, setStatuses] = useState<LookupItem[]>([])
  const [assignees, setAssignees] = useState<AssignableUser[]>([])
  const [docTypes, setDocTypes] = useState<LookupItem[]>([])
  const [actions, setActions] = useState<StatusActions | null>(null)
  const [loading, setLoading] = useState(true)
  const [savingId, setSavingId] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const ignoreRowClick = useRef(false)

  const swallowRowClick = useCallback(() => {
    ignoreRowClick.current = true
    window.setTimeout(() => {
      ignoreRowClick.current = false
    }, 400)
  }, [])
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const showAssignee = canSeeDashboardAssignee(user)

  const loadLookups = useCallback(async () => {
    try {
      const [o, s, a, t, act] = await Promise.all([
        api.organizations(),
        api.statuses(),
        showAssignee ? api.assignees() : Promise.resolve([] as AssignableUser[]),
        api.documentTypes(),
        api.statusActions(),
      ])
      setOrgs(o)
      setStatuses(s)
      setAssignees(a)
      setDocTypes(t)
      setActions(act)
    } catch {
      /* keep empty */
    }
  }, [showAssignee])

  const query = useCallback((): WorkItemQuery => ({
    page,
    pageSize,
    sortBy,
    sortDir,
    bucket: bucket === 'all' ? undefined : bucket,
    search: search || undefined,
    organizationId: orgId,
    statusId,
    assignedToUserId: showAssignee ? assignedTo : undefined,
    documentTypeId: docTypeId,
    uploadedFrom: uploaded?.[0]?.toISOString(),
    uploadedTo: uploaded?.[1]?.endOf('day').toISOString(),
    workedFrom: worked?.[0]?.toISOString(),
    workedTo: worked?.[1]?.endOf('day').toISOString(),
    groupBy,
  }), [assignedTo, bucket, docTypeId, groupBy, orgId, page, pageSize, search, showAssignee, sortBy, sortDir, statusId, uploaded, worked])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await api.workItems(query())
      setItems(result.items)
      setTotal(result.total)
      setCounts(result.buckets ?? emptyCounts())
    } catch (err) {
      const text = err instanceof Error ? err.message : 'Work items failed to load.'
      setError(text)
    } finally {
      setLoading(false)
    }
  }, [message, query])

  useEffect(() => {
    const hasFilter = !!(
      params.get('statusId') ||
      params.get('organizationId') ||
      params.get('assignedToUserId') ||
      params.get('uploadedFrom') ||
      params.get('workedFrom')
    )
    const nextBucket = params.get('bucket')
    const resolvedBucket = nextBucket && bucketKeys.includes(nextBucket as Bucket)
      ? nextBucket
      : defaultDocumentsBucket(null, readQueuePreset(), user, hasFilter)
    if (nextBucket && bucketKeys.includes(nextBucket as Bucket)) {
      setBucket(nextBucket as Bucket)
      setQueuePreset(nextBucket === 'mine' || nextBucket === 'unassigned' || nextBucket === 'priority' || nextBucket === 'duethisweek' ? nextBucket : '')
    } else {
      setBucket(resolvedBucket as Bucket)
    }
    setOrgId(params.get('organizationId') ?? undefined)
    setStatusId(params.get('statusId') ?? undefined)
    const nextAssigned = resolveAssigneeFilter(params.get('assignedToUserId'), user)
    setAssignedTo(resolvedBucket === 'unassigned' && !params.get('assignedToUserId') ? UNASSIGNED : nextAssigned)
    setDocTypeId(params.get('documentTypeId') ?? undefined)
    const uploadedFrom = parseDay(params.get('uploadedFrom'))
    const uploadedTo = parseDay(params.get('uploadedTo'))
    setUploaded(uploadedFrom || uploadedTo ? [uploadedFrom, uploadedTo] : null)
    const workedFrom = parseDay(params.get('workedFrom'))
    const workedTo = parseDay(params.get('workedTo'))
    setWorked(workedFrom || workedTo ? [workedFrom, workedTo] : null)
    setPage(1)
  }, [params, user])

  useEffect(() => {
    void loadLookups()
  }, [loadLookups])

  useEffect(() => {
    void load()
  }, [load])

  const applyBucket = (next: Bucket) => {
    setPage(1)
    setBucket(next)
    const preset = next === 'mine' || next === 'unassigned' || next === 'priority' || next === 'duethisweek' ? next : ''
    setQueuePreset(preset)
    writeQueuePreset(preset)
    if (next === 'unassigned' && showAssignee) setAssignedTo(UNASSIGNED)
    if (next === 'mine' && showAssignee && user?.id) setAssignedTo(user.id)
  }

  const applyPreset = (preset: QueuePreset) => {
    setQueuePreset(preset)
    writeQueuePreset(preset)
    setPage(1)
    if (preset === 'mine') {
      if (showAssignee && user?.id) setAssignedTo(user.id)
      setBucket('mine')
      return
    }
    if (preset === 'unassigned') {
      if (showAssignee) setAssignedTo(UNASSIGNED)
      setBucket('unassigned')
      return
    }
    if (preset === 'priority' || preset === 'duethisweek') {
      setBucket(preset)
      return
    }
    if (showAssignee && user?.id) {
      setAssignedTo(user.id)
      setBucket('all')
      setSortBy(QUEUE_SORT_BY)
      setSortDir(QUEUE_SORT_DIR)
      return
    }
    setBucket(incomingFilters ? 'all' : 'pending')
  }

  const patchRow = async (row: WorkItemListItem, body: Record<string, unknown>, patch: Partial<WorkItemListItem>) => {
    setSavingId(row.id)
    setItems((current) => current.map((item) => (item.id === row.id ? { ...item, ...patch } : item)))
    try {
      const updated = await api.updateWorkItem(row.id, body)
      setItems((current) => current.map((item) => (item.id === row.id ? { ...item, ...updated } : item)))
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not update the work item.')
      await load()
    } finally {
      setSavingId(null)
    }
  }

  const onExport = async () => {
    setExporting(true)
    try {
      await api.exportWorkItems(query())
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Export failed.')
    } finally {
      setExporting(false)
    }
  }

  const menuItems = bucketItems.map((b) => ({
    key: b.key,
    icon: b.icon,
    label: (
      <Flex justify="space-between" gap={12}>
        <span>{b.label}</span>
        {b.countKey ? <Typography.Text type="secondary">{counts[b.countKey] ?? 0}</Typography.Text> : null}
      </Flex>
    ),
  }))

  const columns = useMemo(() => {
    const cols: ColumnsType<WorkItemListItem> = [
      {
        title: 'File name',
        dataIndex: 'fileName',
        key: 'filename',
        sorter: true,
        ellipsis: true,
        render: (name: string, row) => {
          const due = neededByBadgeText({ neededBy: row.priorityNeededBy, workedOn: row.workedOn })
          const overdue = isNeededByOverdue(row.isPriority, row.priorityNeededBy)
          return (
            <Space size={6} wrap>
              <span>{name}</span>
              {row.isPriority && <Tag color="red">Priority</Tag>}
              {due && <Tag color={overdue ? 'volcano' : 'gold'}>{overdue ? `Overdue · ${due}` : due}</Tag>}
            </Space>
          )
        },
      },
      { title: 'Client name', dataIndex: 'organizationName', key: 'client', sorter: true, width: 160 },
      {
        title: 'Status',
        dataIndex: 'statusName',
        key: 'status',
        sorter: true,
        width: 160,
        render: (name: string, row) => {
          if (!user?.canMutateWorkItems || !actions) return statusLabel(name)
          return (
            <div onClick={stopRowClick} onMouseDown={stopRowClick}>
            <Select
              size="small"
              style={{ width: '100%' }}
              value={row.statusId}
              disabled={savingId === row.id}
              onClick={stopRowClick}
              onMouseDown={stopRowClick}
              onDropdownVisibleChange={(open) => {
                swallowRowClick()
                if (open) setSelectedId(row.id)
              }}
              onChange={(value) => {
                swallowRowClick()
                setSelectedId(row.id)
                const next = statuses.find((s) => s.id === value)
                void patchRow(row, { statusId: value }, {
                  statusId: value,
                  statusName: next?.name ?? row.statusName,
                  statusColor: next?.color ?? row.statusColor,
                })
              }}
              options={statusSelectOptions(actions, { id: row.statusId, name: row.statusName })}
            />
            </div>
          )
        },
      },
      {
        title: <TitleWithHelp help={ASSIGNED_TO_HELP}>{ASSIGNED_TO_LABEL}</TitleWithHelp>,
        dataIndex: 'assignedToName',
        key: 'assignedto',
        sorter: true,
        width: 180,
        render: (v: string | null, row) => {
          if (!user?.canMutateWorkItems) {
            return (
              <span>
                {v ?? '—'}
                <WorkPresenceMarks workItemId={row.id} assignedToUserId={row.assignedToUserId} />
              </span>
            )
          }
          return (
            <div onClick={stopRowClick} onMouseDown={stopRowClick}>
            <WorkPresenceMarks workItemId={row.id} assignedToUserId={row.assignedToUserId} />
            <Select
              allowClear
              size="small"
              style={{ width: '100%' }}
              placeholder="Unassigned"
              value={row.assignedToUserId ?? undefined}
              disabled={savingId === row.id}
              onClick={stopRowClick}
              onMouseDown={stopRowClick}
              onDropdownVisibleChange={(open) => {
                swallowRowClick()
                if (open) setSelectedId(row.id)
              }}
              options={assignees.map((a) => ({ value: a.id, label: a.displayName }))}
              onChange={(value) => {
                swallowRowClick()
                setSelectedId(row.id)
                const next = assignees.find((a) => a.id === value)
                void patchRow(row, {
                  assignedToUserId: value ?? null,
                  clearAssignment: !value,
                }, {
                  assignedToUserId: value ?? null,
                  assignedToName: next?.displayName ?? null,
                })
              }}
            />
            </div>
          )
        },
      },
      {
        title: 'Upload date',
        dataIndex: 'uploadedAt',
        key: 'uploadedAt',
        sorter: true,
        width: 170,
        render: (v: string) => dayjs(v).format('YYYY-MM-DD HH:mm'),
      },
      {
        title: 'Worked date',
        dataIndex: 'workedOn',
        key: 'workedon',
        sorter: true,
        width: 140,
        render: (v: string | null) => (v ? dayjs(v).format('YYYY-MM-DD') : '—'),
      },
      {
        title: 'Review',
        dataIndex: 'isReviewed',
        key: 'reviewed',
        sorter: true,
        width: 90,
        render: (value: boolean | undefined) => reviewLabel(value),
      },
    ]

    if (!groupBy) return cols

    const spans = items.map((item, index) => {
      if (index > 0 && groupValue(item, groupBy) === groupValue(items[index - 1], groupBy)) return 0
      let span = 1
      for (let i = index + 1; i < items.length; i += 1) {
        if (groupValue(items[i], groupBy) !== groupValue(item, groupBy)) break
        span += 1
      }
      return span
    })

    return [
      {
        title: 'Group',
        key: 'group',
        width: 160,
        render: (_: unknown, item: WorkItemListItem, index: number) => ({
          children: groupValue(item, groupBy),
          props: { rowSpan: spans[index] },
        }),
      },
      ...cols,
    ]
  }, [actions, assignees, groupBy, items, savingId, statuses, swallowRowClick, user?.canMutateWorkItems])

  const filterSelects = (
    <>
      <Select
        allowClear
        placeholder="Status"
        className="filter-field"
        value={statusId}
        onChange={(v) => {
          setPage(1)
          setStatusId(v)
        }}
        options={statuses.map((s) => ({ value: s.id, label: statusLabel(s.name) }))}
      />
      {showAssignee && (
      <Select
        allowClear
        placeholder="All Assigned to"
        className="filter-field"
        title={ASSIGNED_TO_FILTER_HELP}
        value={assignedTo}
        onChange={(v) => {
          setPage(1)
          setAssignedTo(v)
          if (v === UNASSIGNED) setBucket('unassigned')
          else if (bucket === 'unassigned') setBucket('all')
        }}
        options={[
          { value: UNASSIGNED, label: UNASSIGNED_LABEL },
          ...assignees.map((a) => ({ value: a.id, label: a.displayName })),
        ]}
      />
      )}
      <Select
        allowClear
        placeholder="Client"
        className="filter-field"
        value={orgId}
        onChange={(v) => {
          setPage(1)
          setOrgId(v)
        }}
        options={orgs.map((o) => ({ value: o.id, label: o.name }))}
      />
      <Select
        allowClear
        placeholder="Document type"
        className="filter-field"
        value={docTypeId}
        onChange={(v) => {
          setPage(1)
          setDocTypeId(v)
        }}
        options={docTypes.map((t) => ({ value: t.id, label: t.name }))}
      />
      <DatePicker.RangePicker
        className="filter-field-lg"
        placeholder={['Uploaded from', 'Uploaded to']}
        value={uploaded}
        onChange={(v) => {
          setPage(1)
          setUploaded(v)
        }}
      />
      <DatePicker.RangePicker
        className="filter-field-lg"
        placeholder={['Worked from', 'Worked to']}
        value={worked}
        onChange={(v) => {
          setPage(1)
          setWorked(v)
        }}
      />
      <Select
        allowClear
        placeholder="Group by"
        className="filter-field"
        value={groupBy}
        onChange={(v) => {
          setPage(1)
          setGroupBy(v)
        }}
        options={[
          { value: 'status', label: 'Status' },
          { value: 'client', label: 'Client' },
          { value: 'assignedTo', label: ASSIGNED_TO_LABEL },
          { value: 'documentType', label: 'Document type' },
        ]}
      />
    </>
  )

  return (
    <Space direction="vertical" size={8} style={{ width: '100%' }}>
      <Flex justify="space-between" align="flex-start" wrap="wrap" gap={8}>
        <div>
          <Typography.Title level={3} className="page-title" style={{ margin: 0 }}>
            <TitleWithHelp help="Staff land on items Assigned to you, Pending first then oldest upload. Switch Assigned to — all, Unassigned, or another person. Assigned to is the work-item assignee (queues/reports). Assigned technician is the org default used only to auto-assign new uploads. Viewer and Uploader do not see Assigned to and stay in assigned organizations. Editors can change status and Assigned to in the grid. My queue presets stay in this browser.">
              Manage Documents
            </TitleWithHelp>
          </Typography.Title>
        </div>
        <Space wrap>
          <Button icon={<DownloadOutlined />} loading={exporting} onClick={() => void onExport()}>
            {isMobile ? 'Export' : 'Export to Excel'}
          </Button>
          {user?.canUpload ? (
            <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate('/upload-documents')}>
              Upload
            </Button>
          ) : null}
        </Space>
      </Flex>

      {error && <LoadError message={error} onRetry={() => void load()} />}

      <Flex gap={12} align="flex-start" className="documents-layout">
        {isMobile ? (
          <Select
            className="bucket-select"
            value={bucket}
            style={{ width: '100%' }}
            onChange={(key) => {
              applyBucket(key as Bucket)
            }}
            options={bucketItems.map((b) => ({
              value: b.key,
              label: b.countKey ? `${b.label} (${counts[b.countKey] ?? 0})` : b.label,
            }))}
          />
        ) : (
          <Card size="small" className="bucket-sider" styles={{ body: { padding: 8 } }}>
            <Menu
              mode="inline"
              selectedKeys={[bucket]}
              items={menuItems}
              onClick={({ key }) => {
                applyBucket(key as Bucket)
              }}
            />
          </Card>
        )}

        <Space direction="vertical" size={8} className="documents-main">
          <div className="filter-toolbar">
            <Space wrap size={6}>
              <Typography.Text type="secondary">My queue</Typography.Text>
              <Button size="small" type={queuePreset === 'mine' ? 'primary' : 'default'} onClick={() => applyPreset('mine')}>
                Assigned to me
              </Button>
              {showAssignee ? (
                <Button size="small" type={queuePreset === 'unassigned' ? 'primary' : 'default'} onClick={() => applyPreset('unassigned')}>
                  {UNASSIGNED_LABEL}
                </Button>
              ) : null}
              <Button size="small" type={queuePreset === 'priority' ? 'primary' : 'default'} onClick={() => applyPreset('priority')}>
                Priority
              </Button>
              <Button size="small" type={queuePreset === 'duethisweek' ? 'primary' : 'default'} onClick={() => applyPreset('duethisweek')}>
                Due this week
              </Button>
              <Button size="small" onClick={() => applyPreset('')}>Clear</Button>
            </Space>
            <Input.Search
              allowClear
              placeholder="Search file, client, or assignee"
              className="filter-field-lg"
              onSearch={(value) => {
                setPage(1)
                setSearch(value)
              }}
            />
            {filterSelects}
          </div>

          {isMobile ? (
            <>
              <WorkItemCards
                items={items}
                loading={loading}
                emptyText="No work items in this bucket."
                showUploadDate
                showReview
                onOpen={(id) => navigate(`/documents/${id}`)}
              />
              <Pagination
                current={page}
                pageSize={pageSize}
                total={total}
                showSizeChanger
                size="small"
                align="center"
                onChange={(p, ps) => {
                  setPage(p)
                  setPageSize(ps)
                }}
              />
            </>
          ) : (
            <div className="manage-documents-grid">
            <ConfigProvider theme={manageDocumentsTableTheme}>
            <Table<WorkItemListItem>
              rowKey="id"
              className="manage-documents-grid"
              loading={loading}
              dataSource={items}
              columns={columns}
              scroll={{ x: 980 }}
              rowClassName={(row, index) => manageDocumentsRowClassName(index, selectedId === row.id)}
              pagination={{
                current: page,
                pageSize,
                total,
                showSizeChanger: true,
                onChange: (p, ps) => {
                  setPage(p)
                  setPageSize(ps)
                },
              }}
              onChange={(_p, _f, sorter) => {
                const s = Array.isArray(sorter) ? sorter[0] : sorter
                if (s?.columnKey && s.order) {
                  setSortBy(String(s.columnKey))
                  setSortDir(s.order === 'ascend' ? 'asc' : 'desc')
                }
              }}
              onRow={(row) => ({
                tabIndex: 0,
                onClick: () => {
                  if (ignoreRowClick.current) return
                  setSelectedId(row.id)
                  navigate(`/documents/${row.id}`)
                },
                onFocus: () => setSelectedId(row.id),
                style: { cursor: 'pointer' },
              })}
              locale={{ emptyText: 'No work items in this bucket.' }}
            />
            </ConfigProvider>
            </div>
          )}
        </Space>
      </Flex>

    </Space>
  )
}
