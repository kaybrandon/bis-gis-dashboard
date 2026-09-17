import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  DownloadOutlined,
  FileOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  ThunderboltOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { App, Button, Card, DatePicker, Flex, Input, Menu, Pagination, Select, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { Dayjs } from 'dayjs'
import dayjs from 'dayjs'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import type { AssignableUser, LookupItem, OrgOption, StatusActions, WorkItemListItem, WorkItemQuery } from '../api'
import { api } from '../api'
import { TitleWithHelp } from '../components/HelpTip'
import { LoadError } from '../components/LoadError'
import { WorkItemCards } from '../components/WorkItemCards'
import { useAuth } from '../auth'
import { useIsMobile } from '../layout/useIsMobile'
import { isNeededByOverdue, neededByLabel } from '../neededBy'
import { reviewLabel, statusLabel } from '../statusLabels'

type Bucket = 'all' | 'pending' | 'mine' | 'hold' | 'completed' | 'firstdeadline' | 'finaldeadline' | 'priority' | 'duethisweek'
type QueuePreset = 'mine' | 'priority' | 'duethisweek' | ''

const QUEUE_KEY = 'gis.myQueue'

const bucketKeys: Bucket[] = ['all', 'pending', 'mine', 'hold', 'completed', 'firstdeadline', 'finaldeadline', 'priority', 'duethisweek']

const bucketItems: { key: Bucket; label: string; countKey: keyof ReturnType<typeof emptyCounts> | null; icon: ReactNode }[] = [
  { key: 'all', label: 'All items', countKey: null, icon: <FileOutlined /> },
  { key: 'pending', label: 'All Pending', countKey: 'pending', icon: <ClockCircleOutlined /> },
  { key: 'mine', label: 'My Work Items', countKey: 'mine', icon: <UserOutlined /> },
  { key: 'priority', label: 'Priority', countKey: 'priority', icon: <ThunderboltOutlined /> },
  { key: 'duethisweek', label: 'Due this week', countKey: 'dueThisWeek', icon: <ThunderboltOutlined /> },
  { key: 'hold', label: 'On Hold', countKey: 'onHold', icon: <PauseCircleOutlined /> },
  { key: 'completed', label: 'Completed', countKey: 'completed', icon: <CheckCircleOutlined /> },
  { key: 'firstdeadline', label: 'First Deadline', countKey: 'firstDeadline', icon: <ThunderboltOutlined /> },
  { key: 'finaldeadline', label: 'Final Deadline', countKey: 'finalDeadline', icon: <FileOutlined /> },
]

function emptyCounts() {
  return { pending: 0, mine: 0, onHold: 0, completed: 0, firstDeadline: 0, finalDeadline: 0, priority: 0, dueThisWeek: 0 }
}

function readQueuePreset(): QueuePreset {
  try {
    const value = localStorage.getItem(QUEUE_KEY)
    if (value === 'mine' || value === 'priority' || value === 'duethisweek') return value
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

function parseBucket(value: string | null, hasOtherFilter: boolean): Bucket {
  if (value && bucketKeys.includes(value as Bucket)) return value as Bucket
  return hasOtherFilter ? 'all' : 'pending'
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
  const [sortBy, setSortBy] = useState('uploadedAt')
  const [sortDir, setSortDir] = useState('desc')
  const [bucket, setBucket] = useState<Bucket>(() => {
    const fromUrl = params.get('bucket')
    if (fromUrl && bucketKeys.includes(fromUrl as Bucket)) return fromUrl as Bucket
    const preset = readQueuePreset()
    if (preset) return preset
    return parseBucket(fromUrl, incomingFilters)
  })
  const [queuePreset, setQueuePreset] = useState<QueuePreset>(() => {
    const fromUrl = params.get('bucket')
    if (fromUrl === 'mine' || fromUrl === 'priority' || fromUrl === 'duethisweek') return fromUrl
    return readQueuePreset()
  })
  const [search, setSearch] = useState('')
  const [orgId, setOrgId] = useState<string | undefined>(() => params.get('organizationId') ?? undefined)
  const [statusId, setStatusId] = useState<string | undefined>(() => params.get('statusId') ?? undefined)
  const [assignedTo, setAssignedTo] = useState<string | undefined>(() => params.get('assignedToUserId') ?? undefined)
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
  const ignoreRowClick = useRef(false)

  const swallowRowClick = useCallback(() => {
    ignoreRowClick.current = true
    window.setTimeout(() => {
      ignoreRowClick.current = false
    }, 400)
  }, [])
  const [exporting, setExporting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadLookups = useCallback(async () => {
    try {
      const [o, s, a, t, act] = await Promise.all([
        api.organizations(),
        api.statuses(),
        api.assignees(),
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
  }, [])

  const query = useCallback((): WorkItemQuery => ({
    page,
    pageSize,
    sortBy,
    sortDir,
    bucket: bucket === 'all' ? undefined : bucket,
    search: search || undefined,
    organizationId: orgId,
    statusId,
    assignedToUserId: assignedTo,
    documentTypeId: docTypeId,
    uploadedFrom: uploaded?.[0]?.toISOString(),
    uploadedTo: uploaded?.[1]?.endOf('day').toISOString(),
    workedFrom: worked?.[0]?.toISOString(),
    workedTo: worked?.[1]?.endOf('day').toISOString(),
    groupBy,
  }), [assignedTo, bucket, docTypeId, groupBy, orgId, page, pageSize, search, sortBy, sortDir, statusId, uploaded, worked])

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
    if (nextBucket && bucketKeys.includes(nextBucket as Bucket)) {
      setBucket(nextBucket as Bucket)
      setQueuePreset(nextBucket === 'mine' || nextBucket === 'priority' || nextBucket === 'duethisweek' ? nextBucket : '')
    } else {
      setBucket(parseBucket(nextBucket, hasFilter))
    }
    setOrgId(params.get('organizationId') ?? undefined)
    setStatusId(params.get('statusId') ?? undefined)
    setAssignedTo(params.get('assignedToUserId') ?? undefined)
    setDocTypeId(params.get('documentTypeId') ?? undefined)
    const uploadedFrom = parseDay(params.get('uploadedFrom'))
    const uploadedTo = parseDay(params.get('uploadedTo'))
    setUploaded(uploadedFrom || uploadedTo ? [uploadedFrom, uploadedTo] : null)
    const workedFrom = parseDay(params.get('workedFrom'))
    const workedTo = parseDay(params.get('workedTo'))
    setWorked(workedFrom || workedTo ? [workedFrom, workedTo] : null)
    setPage(1)
  }, [params])

  useEffect(() => {
    void loadLookups()
  }, [loadLookups])

  useEffect(() => {
    void load()
  }, [load])

  const applyPreset = (preset: QueuePreset) => {
    setQueuePreset(preset)
    writeQueuePreset(preset)
    setPage(1)
    if (preset === 'mine' || preset === 'priority' || preset === 'duethisweek') {
      setBucket(preset)
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
          const due = neededByLabel(row.priorityNeededBy)
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
              onDropdownVisibleChange={swallowRowClick}
              onChange={(value) => {
                swallowRowClick()
                const next = statuses.find((s) => s.id === value)
                void patchRow(row, { statusId: value }, {
                  statusId: value,
                  statusName: next?.name ?? row.statusName,
                  statusColor: next?.color ?? row.statusColor,
                })
              }}
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
                ].includes(row.statusId)
                  ? [{ value: row.statusId, label: statusLabel(row.statusName) }]
                  : []),
              ]}
            />
            </div>
          )
        },
      },
      {
        title: 'Assigned to',
        dataIndex: 'assignedToName',
        key: 'assignedto',
        sorter: true,
        width: 180,
        render: (v: string | null, row) => {
          if (!user?.canMutateWorkItems) return v ?? '—'
          return (
            <div onClick={stopRowClick} onMouseDown={stopRowClick}>
            <Select
              allowClear
              size="small"
              style={{ width: '100%' }}
              placeholder="Unassigned"
              value={row.assignedToUserId ?? undefined}
              disabled={savingId === row.id}
              onClick={stopRowClick}
              onMouseDown={stopRowClick}
              onDropdownVisibleChange={swallowRowClick}
              options={assignees.map((a) => ({ value: a.id, label: a.displayName }))}
              onChange={(value) => {
                swallowRowClick()
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
      <Select
        allowClear
        placeholder="Assigned to"
        className="filter-field"
        value={assignedTo}
        onChange={(v) => {
          setPage(1)
          setAssignedTo(v)
        }}
        options={assignees.map((a) => ({ value: a.id, label: a.displayName }))}
      />
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
          { value: 'assignedTo', label: 'Assigned to' },
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
            <TitleWithHelp help="Status buckets, assignment, and review for GIS work items. Assigned to shows the person's name only. Editors can change status and assignee in the grid. My queue presets stay in this browser.">
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
              setPage(1)
              const next = key as Bucket
              setBucket(next)
              const preset = next === 'mine' || next === 'priority' || next === 'duethisweek' ? next : ''
              setQueuePreset(preset)
              writeQueuePreset(preset)
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
                setPage(1)
                const next = key as Bucket
                setBucket(next)
                const preset = next === 'mine' || next === 'priority' || next === 'duethisweek' ? next : ''
                setQueuePreset(preset)
                writeQueuePreset(preset)
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
            <Table<WorkItemListItem>
              rowKey="id"
              loading={loading}
              dataSource={items}
              columns={columns}
              scroll={{ x: 980 }}
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
                onClick: () => {
                  if (ignoreRowClick.current) return
                  navigate(`/documents/${row.id}`)
                },
                style: { cursor: 'pointer' },
              })}
              locale={{ emptyText: 'No work items in this bucket.' }}
            />
          )}
        </Space>
      </Flex>

    </Space>
  )
}
