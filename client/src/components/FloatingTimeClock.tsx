import { ClockCircleOutlined, CloseOutlined, PlayCircleOutlined, PlusOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button, Input, InputNumber, Space, Typography } from 'antd'
import dayjs from 'dayjs'
import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { api } from '../api'
import type { WorkItemListItem } from '../api'
import { useAuth } from '../auth'
import { TitleWithHelp } from './HelpTip'
import { useIsMobile } from '../layout/useIsMobile'
import {
  clampClockPos,
  defaultClockPos,
  documentIdFromPath,
  elapsedMinutes,
  emptyClock,
  formatElapsed,
  loadClock,
  loadClockPos,
  notifyTimeLogged,
  saveClock,
  saveClockPos,
  type ClockPosition,
  type TimeClockTarget,
} from '../timeClock'

function toTarget(item: { id: string; fileName: string; organizationName: string; statusName?: string }): TimeClockTarget {
  return {
    id: item.id,
    fileName: item.fileName,
    organizationName: item.organizationName,
    statusName: item.statusName,
  }
}

export function FloatingTimeClock() {
  const { message } = App.useApp()
  const { user } = useAuth()
  const location = useLocation()
  const isMobile = useIsMobile()
  const [open, setOpen] = useState(false)
  const [clock, setClock] = useState(loadClock)
  const [now, setNow] = useState(Date.now())
  const [search, setSearch] = useState('')
  const [results, setResults] = useState<WorkItemListItem[]>([])
  const [mine, setMine] = useState<WorkItemListItem[]>([])
  const [searching, setSearching] = useState(false)
  const [hours, setHours] = useState(0)
  const [minutes, setMinutes] = useState(15)
  const [saving, setSaving] = useState(false)
  const [pos, setPos] = useState<ClockPosition | null>(null)
  const [dragging, setDragging] = useState(false)
  const fabRef = useRef<HTMLButtonElement>(null)
  const drag = useRef({
    active: false,
    moved: false,
    pointerId: -1,
    startX: 0,
    startY: 0,
    origX: 0,
    origY: 0,
  })

  const running = clock.startedAt != null
  const routeId = documentIdFromPath(location.pathname)
  const userId = user?.id ?? 'anon'

  const measureFab = () => {
    const rect = fabRef.current?.getBoundingClientRect()
    return { width: rect?.width || 148, height: rect?.height || 40 }
  }

  useEffect(() => {
    const fab = measureFab()
    const saved = loadClockPos(userId)
    setPos(clampClockPos(saved ?? defaultClockPos(fab.width, fab.height), fab.width, fab.height))
  }, [userId])

  useEffect(() => {
    const onResize = () => {
      const fab = measureFab()
      setPos((current) => current
        ? clampClockPos(current, fab.width, fab.height)
        : clampClockPos(defaultClockPos(fab.width, fab.height), fab.width, fab.height))
    }
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  const persist = useCallback((next: typeof clock) => {
    setClock(next)
    saveClock(next)
  }, [])

  useEffect(() => {
    if (!running) return
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [running])

  useEffect(() => {
    if (!routeId || clock.startedAt != null) return
    if (clock.target?.id === routeId) return
    let cancelled = false
    api.workItem(routeId)
      .then((item) => {
        if (cancelled) return
        setClock((current) => {
          if (current.startedAt != null || current.target?.id === item.id) return current
          const next = { ...current, target: toTarget(item) }
          saveClock(next)
          return next
        })
      })
      .catch(() => undefined)
    return () => { cancelled = true }
  }, [routeId, clock.startedAt, clock.target?.id])

  const loadChoices = useCallback(async (query: string) => {
    setSearching(true)
    try {
      const [recent, assigned] = await Promise.all([
        api.workItems({ search: query || undefined, pageSize: 8, sortBy: 'updatedAt', sortDir: 'desc' }),
        query ? Promise.resolve({ items: [] as WorkItemListItem[] }) : api.workItems({ bucket: 'mine', pageSize: 8, sortBy: 'updatedAt', sortDir: 'desc' }),
      ])
      setResults(recent.items)
      setMine(assigned.items)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not load work items.')
    } finally {
      setSearching(false)
    }
  }, [message])

  useEffect(() => {
    if (!open || running) return
    const handle = window.setTimeout(() => void loadChoices(search.trim()), search.trim() ? 250 : 0)
    return () => window.clearTimeout(handle)
  }, [open, running, search, loadChoices])

  const choices = useMemo(() => {
    const seen = new Set<string>()
    const rows: { heading?: string; item: WorkItemListItem }[] = []
    if (!search.trim()) {
      mine.forEach((item) => {
        if (seen.has(item.id)) return
        seen.add(item.id)
        rows.push({ heading: rows.length === 0 ? 'My work items' : undefined, item })
      })
      let recentHeading = false
      results.forEach((item) => {
        if (seen.has(item.id)) return
        seen.add(item.id)
        if (!recentHeading) {
          rows.push({ heading: 'Recently updated', item })
          recentHeading = true
        } else {
          rows.push({ item })
        }
      })
    } else {
      results.forEach((item) => {
        if (seen.has(item.id)) return
        seen.add(item.id)
        rows.push({ item })
      })
    }
    return rows
  }, [mine, results, search])

  if (!user?.canLogTime) return null

  const select = (item: TimeClockTarget) => {
    persist({ ...clock, target: item })
    setSearch('')
  }

  const start = () => {
    if (!clock.target) {
      message.warning('Select a GIS work item before starting the clock.')
      setOpen(true)
      return
    }
    persist({ ...clock, startedAt: Date.now() })
    setNow(Date.now())
    setOpen(true)
  }

  const logEntry = async (body: { hours: number; minutes: number; note?: string }) => {
    if (!clock.target?.id) {
      message.warning('Select a GIS work item to log time against.')
      setOpen(true)
      return
    }
    setSaving(true)
    try {
      const target = clock.target
      await api.createTimeEntry(target.id, {
        hours: body.hours,
        minutes: body.minutes,
        workedOn: dayjs().startOf('day').toISOString(),
        note: body.note,
      })
      notifyTimeLogged(target.id)
      message.success(`Logged ${body.hours ? `${body.hours}h ` : ''}${body.minutes ? `${body.minutes}m` : ''} on ${target.fileName}.`.replace(/\s+/g, ' ').trim())
      persist({ ...clock, startedAt: null, note: '' })
      setHours(0)
      setMinutes(15)
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Could not log time.')
    } finally {
      setSaving(false)
    }
  }

  const stop = async () => {
    if (!clock.startedAt || !clock.target) return
    const total = elapsedMinutes(clock.startedAt)
    const startLabel = dayjs(clock.startedAt).format('h:mm A')
    const endLabel = dayjs().format('h:mm A')
    await logEntry({
      hours: Math.floor(total / 60),
      minutes: total % 60,
      note: clock.note.trim() || `Clock ${startLabel}–${endLabel}`,
    })
  }

  const addManual = async () => {
    await logEntry({
      hours,
      minutes,
      note: clock.note.trim() || undefined,
    })
  }

  const clearTarget = () => {
    if (running) return
    persist({ ...emptyClock, note: clock.note })
  }

  const onFabPointerDown = (event: PointerEvent<HTMLButtonElement>) => {
    if (event.button !== 0) return
    const current = pos ?? defaultClockPos()
    drag.current = {
      active: true,
      moved: false,
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      origX: current.x,
      origY: current.y,
    }
    event.currentTarget.setPointerCapture(event.pointerId)
  }

  const onFabPointerMove = (event: PointerEvent<HTMLButtonElement>) => {
    if (!drag.current.active || event.pointerId !== drag.current.pointerId) return
    const dx = event.clientX - drag.current.startX
    const dy = event.clientY - drag.current.startY
    if (!drag.current.moved && (dx * dx + dy * dy) < 64) return
    drag.current.moved = true
    setDragging(true)
    const fab = measureFab()
    setPos(clampClockPos({
      x: drag.current.origX + dx,
      y: drag.current.origY + dy,
    }, fab.width, fab.height))
  }

  const onFabPointerUp = (event: PointerEvent<HTMLButtonElement>) => {
    if (!drag.current.active || event.pointerId !== drag.current.pointerId) return
    drag.current.active = false
    try {
      event.currentTarget.releasePointerCapture(event.pointerId)
    } catch {
      /* already released */
    }
    if (drag.current.moved) {
      const fab = measureFab()
      setPos((current) => {
        const next = clampClockPos(current ?? defaultClockPos(fab.width, fab.height), fab.width, fab.height)
        saveClockPos(userId, next)
        return next
      })
      setDragging(false)
      return
    }
    setDragging(false)
    setOpen((value) => !value)
  }

  const fabLabel = running && clock.target
    ? formatElapsed(clock.startedAt!, now)
    : 'Clock'
  const alignLeft = (pos?.x ?? 0) < (typeof window === 'undefined' ? 0 : window.innerWidth / 2)
  const openBelow = (pos?.y ?? 0) < 280

  return (
    <div
      className={`floating-time-clock${open ? ' is-open' : ''}${running ? ' is-running' : ''}${alignLeft ? ' is-left' : ''}${openBelow ? ' is-below' : ''}${dragging ? ' is-dragging' : ''}`}
      style={pos ? { left: pos.x, top: pos.y, right: 'auto', bottom: 'auto' } : undefined}
    >
      {open && (
        <div className="floating-time-clock-panel" role="dialog" aria-label="Time clock">
          <div className="floating-time-clock-head">
            <Typography.Text strong>
              <TitleWithHelp help="Not a global timesheet. Hours go on the GIS work item you are working — the open document, or one you pick — and show on that item’s time log and on time report cards.">
                Time clock
              </TitleWithHelp>
            </Typography.Text>
            <Button type="text" size="small" icon={<CloseOutlined />} aria-label="Close time clock" onClick={() => setOpen(false)} />
          </div>

          {clock.target ? (
            <div className="floating-time-clock-target">
              <Link to={`/documents/${clock.target.id}`}>{clock.target.fileName}</Link>
              <Typography.Text type="secondary">
                {clock.target.organizationName}
                {clock.target.statusName ? ` · ${clock.target.statusName}` : ''}
              </Typography.Text>
              {!running && (
                <Button type="link" size="small" onClick={clearTarget} style={{ paddingInline: 0 }}>
                  Change work item
                </Button>
              )}
            </div>
          ) : (
            <div className="floating-time-clock-picker">
              <Input
                allowClear
                placeholder="Search work items"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                aria-label="Search work items"
              />
              <div className="floating-time-clock-choices">
                {searching && <Typography.Text type="secondary">Searching…</Typography.Text>}
                {!searching && choices.length === 0 && (
                  <Typography.Text type="secondary">No matching work items. Search by file name or pick one from Manage Documents.</Typography.Text>
                )}
                {choices.map(({ heading, item }) => (
                  <div key={item.id}>
                    {heading && <Typography.Text type="secondary" className="floating-time-clock-heading">{heading}</Typography.Text>}
                    <button type="button" className="floating-time-clock-choice" onClick={() => select(toTarget(item))}>
                      <span>{item.fileName}</span>
                      <Typography.Text type="secondary">{item.organizationName} · {item.statusName}</Typography.Text>
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          <Input
            placeholder="Optional note"
            value={clock.note}
            maxLength={500}
            onChange={(event) => persist({ ...clock, note: event.target.value })}
            style={{ marginTop: 8 }}
          />

          {running ? (
            <div className="floating-time-clock-run">
              <Typography.Title level={3} style={{ margin: 0 }}>{formatElapsed(clock.startedAt!, now)}</Typography.Title>
              <Button type="primary" danger icon={<StopOutlined />} loading={saving} onClick={() => void stop()} block={isMobile}>
                Stop and log
              </Button>
            </div>
          ) : (
            <Space direction="vertical" size={8} style={{ width: '100%', marginTop: 8 }}>
              <Button type="primary" icon={<PlayCircleOutlined />} disabled={!clock.target} onClick={start} block>
                Start clock
              </Button>
              <Space wrap size={8} style={{ width: '100%' }}>
                <InputNumber min={0} max={24} value={hours} onChange={(value) => setHours(value ?? 0)} addonBefore="h" style={{ width: 96 }} />
                <InputNumber min={0} max={59} value={minutes} onChange={(value) => setMinutes(value ?? 0)} addonBefore="m" style={{ width: 96 }} />
                <Button icon={<PlusOutlined />} loading={saving} disabled={!clock.target} onClick={() => void addManual()}>
                  Add time
                </Button>
              </Space>
            </Space>
          )}
        </div>
      )}

      <button
        type="button"
        ref={fabRef}
        className="floating-time-clock-fab"
        aria-label={clock.target
          ? (running
            ? `Time clock running on ${clock.target.fileName} ${formatElapsed(clock.startedAt!, now)}. Drag to move.`
            : `Time clock for ${clock.target.fileName}. Drag to move.`)
          : 'Open time clock. Drag to move.'}
        onPointerDown={onFabPointerDown}
        onPointerMove={onFabPointerMove}
        onPointerUp={onFabPointerUp}
        onPointerCancel={onFabPointerUp}
        onClick={(event) => event.preventDefault()}
      >
        <ClockCircleOutlined />
        <span>{fabLabel}</span>
      </button>
    </div>
  )
}
