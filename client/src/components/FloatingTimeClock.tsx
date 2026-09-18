import { ClockCircleOutlined, CloseOutlined, LoginOutlined, LogoutOutlined } from '@ant-design/icons'
import { Button, Typography } from 'antd'
import { useCallback, useEffect, useRef, useState, type PointerEvent } from 'react'
import { useAuth } from '../auth'
import { TitleWithHelp } from './HelpTip'
import {
  clampClockPos,
  defaultClockPos,
  emptyClock,
  formatElapsed,
  loadClock,
  loadClockPos,
  saveClock,
  saveClockPos,
  type ClockPosition,
} from '../timeClock'

export function FloatingTimeClock() {
  const { user } = useAuth()
  const [open, setOpen] = useState(false)
  const [clock, setClock] = useState(loadClock)
  const [now, setNow] = useState(Date.now())
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
  const userId = user?.id ?? 'anon'

  const measureFab = () => {
    const rect = fabRef.current?.getBoundingClientRect()
    return { width: rect?.width || 132, height: rect?.height || 40 }
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

  if (!user?.canSeePresence) return null

  const clockIn = () => {
    persist({ startedAt: Date.now() })
    setNow(Date.now())
    setOpen(false)
  }

  const clockOut = () => {
    persist(emptyClock)
    setOpen(false)
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

  const fabLabel = running ? `Clocked in ${formatElapsed(clock.startedAt!, now)}` : 'Clock in'
  const alignLeft = (pos?.x ?? 0) < (typeof window === 'undefined' ? 0 : window.innerWidth / 2)
  const openBelow = (pos?.y ?? 0) < 220

  return (
    <div
      className={`floating-time-clock${open ? ' is-open' : ''}${running ? ' is-running' : ''}${alignLeft ? ' is-left' : ''}${openBelow ? ' is-below' : ''}${dragging ? ' is-dragging' : ''}`}
      style={pos ? { left: pos.x, top: pos.y, right: 'auto', bottom: 'auto' } : undefined}
    >
      {open && (
        <div className="floating-time-clock-panel" role="dialog" aria-label="Staff attendance clock">
          <div className="floating-time-clock-head">
            <Typography.Text strong>
              <TitleWithHelp help="Staff attendance clock-in for the GIS Dashboard. Clock in and clock out here from any screen. This is not a work-item or document timer and does not log hours or payroll punches.">
                Attendance
              </TitleWithHelp>
            </Typography.Text>
            <Button type="text" size="small" icon={<CloseOutlined />} aria-label="Collapse attendance clock" onClick={() => setOpen(false)} />
          </div>
          <Typography.Text type="secondary" className="floating-time-clock-help">
            Clock in or clock out for staff attendance. Same control on every GIS screen.
          </Typography.Text>
          {running ? (
            <div className="floating-time-clock-run">
              <Typography.Title level={4} style={{ margin: 0 }}>{formatElapsed(clock.startedAt!, now)}</Typography.Title>
              <Button type="primary" danger icon={<LogoutOutlined />} onClick={clockOut}>
                Clock out
              </Button>
            </div>
          ) : (
            <Button type="primary" icon={<LoginOutlined />} onClick={clockIn} block>
              Clock in
            </Button>
          )}
        </div>
      )}

      <button
        type="button"
        ref={fabRef}
        className="floating-time-clock-fab"
        title={running
          ? 'Staff attendance: clocked in. Open to clock out. Drag to move.'
          : 'Staff attendance: clock in. Drag to move so it does not cover Save or the viewer.'}
        aria-label={running
          ? `Staff attendance clocked in ${formatElapsed(clock.startedAt!, now)}. Open to clock out. Drag to move.`
          : 'Staff attendance clock in. Drag to move so it does not cover Save or the viewer.'}
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
