import { Column, Line, Pie } from '@ant-design/plots'
import { Card, Col, Empty, Row } from 'antd'
import type { DashboardResponse } from '../api'
import { TitleWithHelp } from './HelpTip'
import { dashboardCategoryBarPlot } from '../dashboardBarPalette'
import {
  DOCUMENTS_BY_CAD_HELP,
  DOCUMENTS_BY_CAD_TITLE,
  DOCUMENTS_BY_TECHNICIAN_HELP,
  DOCUMENTS_BY_TECHNICIAN_TITLE,
  documentsByCadQuery,
  documentsByTechnicianQuery,
} from '../dashboardVolume'
import type { DocumentsHrefQuery } from '../documentsHref'
import { endOfDayIso, startOfDayIso } from '../documentsHref'
import { useIsMobile } from '../layout/useIsMobile'
import { chartStatusCounts, statusLabel } from '../statusLabels'

type Props = {
  data: DashboardResponse | null
  loading: boolean
  filters: { organizationId?: string; statusId?: string; assignedToUserId?: string; from?: string; to?: string }
  onOpen: (query: DocumentsHrefQuery) => void
}

function plotDatum(event: { type?: string; data?: unknown }): Record<string, unknown> | null {
  const type = event.type ?? ''
  if (type !== 'click' && !type.endsWith(':click')) return null
  if (type.includes('legend') || type.includes('axis')) return null
  const payload = event.data as { data?: unknown } | Record<string, unknown> | undefined
  if (!payload) return null
  const inner = (payload as { data?: unknown }).data
  if (inner && typeof inner === 'object' && !Array.isArray(inner)) return inner as Record<string, unknown>
  if (typeof payload === 'object') return payload as Record<string, unknown>
  return null
}

export function DashboardCharts({ data, loading, filters, onOpen }: Props) {
  const isMobile = useIsMobile()
  const height = isMobile ? 220 : 260
  const status = chartStatusCounts(data?.statusCounts ?? []).map((x) => ({
    type: statusLabel(x.name),
    value: x.count,
    color: x.color ?? undefined,
    id: x.id,
  }))
  const volume = (data?.volumeOverTime ?? []).flatMap((day) => [
    { date: day.date.slice(5), fullDate: day.date, series: 'Uploaded', count: day.uploaded },
    { date: day.date.slice(5), fullDate: day.date, series: 'Completed', count: day.completed },
  ])
  const hoursAssignee = (data?.hoursByAssignee ?? []).map((x) => ({ name: x.name, hours: x.hours, id: x.id }))
  const hoursClient = (data?.hoursByClient ?? []).map((x) => ({ name: x.name, hours: x.hours, id: x.id }))
  const documentsByCad = (data?.organizationCounts ?? []).map((x) => ({ name: x.name, count: x.count, id: x.id }))
  const documentsByTechnician = (data?.assigneeCounts ?? []).map((x) => ({ name: x.name, count: x.count, id: x.id }))
  const palette = status.map((x) => x.color).filter(Boolean) as string[]

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={8}>
        <Card title="Volume by status" loading={loading} size="small" className="chart-card">
          {status.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No work items match these filters." />
          ) : (
            <Pie
              data={status}
              angleField="value"
              colorField="type"
              height={height}
              innerRadius={0.62}
              legend={{ color: { position: isMobile ? 'bottom' : 'right', layout: { justifyContent: 'center' } } }}
              scale={palette.length ? { color: { range: palette } } : undefined}
              tooltip={{ items: [{ field: 'value', name: 'Items' }] }}
              onEvent={(_chart, event) => {
                const row = plotDatum(event)
                const id = typeof row?.id === 'string' ? row.id : undefined
                if (!id) return
                onOpen({ ...filters, bucket: 'all', statusId: id })
              }}
            />
          )}
        </Card>
      </Col>
      <Col xs={24} lg={16}>
        <Card title={data?.rangeLabel ? `Work over ${data.rangeLabel}` : 'Work over the selected dates'} loading={loading} size="small" className="chart-card">
          <Line
            data={volume}
            xField="date"
            yField="count"
            colorField="series"
            height={height}
            legend={{ color: { position: 'bottom' } }}
            axis={{ x: { title: false, labelAutoRotate: false }, y: { title: false } }}
            scale={{ color: { range: ['#1890ff', '#52c41a'] } }}
            style={{ lineWidth: 2 }}
            onEvent={(_chart, event) => {
              const row = plotDatum(event)
              const fullDate = typeof row?.fullDate === 'string' ? row.fullDate : undefined
              const series = typeof row?.series === 'string' ? row.series : undefined
              if (!fullDate) return
              if (series === 'Completed') {
                onOpen({
                  ...filters,
                  bucket: 'completed',
                  workedFrom: startOfDayIso(fullDate),
                  workedTo: endOfDayIso(fullDate),
                })
                return
              }
              onOpen({
                ...filters,
                bucket: 'all',
                uploadedFrom: startOfDayIso(fullDate),
                uploadedTo: endOfDayIso(fullDate),
              })
            }}
          />
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card
          title={<TitleWithHelp help={DOCUMENTS_BY_CAD_HELP}>{DOCUMENTS_BY_CAD_TITLE}</TitleWithHelp>}
          loading={loading}
          size="small"
          className="chart-card"
        >
          {documentsByCad.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No documents uploaded in this range." />
          ) : (
            <Column
              data={documentsByCad}
              xField="name"
              yField="count"
              height={height}
              {...dashboardCategoryBarPlot(documentsByCad.map((x) => x.name))}
              axis={{ x: { title: false, labelAutoRotate: isMobile }, y: { title: false } }}
              tooltip={{ items: [{ field: 'count', name: 'Documents' }] }}
              onEvent={(_chart, event) => {
                const row = plotDatum(event)
                const id = typeof row?.id === 'string' ? row.id : undefined
                if (!id) return
                onOpen(documentsByCadQuery(filters, id))
              }}
            />
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card
          title={<TitleWithHelp help={DOCUMENTS_BY_TECHNICIAN_HELP}>{DOCUMENTS_BY_TECHNICIAN_TITLE}</TitleWithHelp>}
          loading={loading}
          size="small"
          className="chart-card"
        >
          {documentsByTechnician.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No documents uploaded in this range." />
          ) : (
            <Column
              data={documentsByTechnician}
              xField="name"
              yField="count"
              height={height}
              {...dashboardCategoryBarPlot(documentsByTechnician.map((x) => x.name))}
              axis={{ x: { title: false, labelAutoRotate: isMobile }, y: { title: false } }}
              tooltip={{ items: [{ field: 'count', name: 'Documents' }] }}
              onEvent={(_chart, event) => {
                const row = plotDatum(event)
                const id = typeof row?.id === 'string' ? row.id : undefined
                if (id === undefined) return
                onOpen(documentsByTechnicianQuery(filters, id))
              }}
            />
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card title="Hours by assignee" loading={loading} size="small" className="chart-card">
          {hoursAssignee.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No hours logged for these filters." />
          ) : (
            <Column
              data={hoursAssignee}
              xField="name"
              yField="hours"
              height={height}
              {...dashboardCategoryBarPlot(hoursAssignee.map((x) => x.name))}
              axis={{ x: { title: false, labelAutoRotate: isMobile }, y: { title: false } }}
              tooltip={{ items: [{ field: 'hours', name: 'Hours' }] }}
              onEvent={(_chart, event) => {
                const row = plotDatum(event)
                const id = typeof row?.id === 'string' ? row.id : undefined
                if (!id || id === '00000000-0000-0000-0000-000000000000') return
                onOpen({ ...filters, bucket: 'all', assignedToUserId: id })
              }}
            />
          )}
        </Card>
      </Col>
      <Col xs={24} lg={12}>
        <Card title="Hours by client" loading={loading} size="small" className="chart-card">
          {hoursClient.length === 0 ? (
            <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No hours logged for these filters." />
          ) : (
            <Column
              data={hoursClient}
              xField="name"
              yField="hours"
              height={height}
              {...dashboardCategoryBarPlot(hoursClient.map((x) => x.name))}
              axis={{ x: { title: false, labelAutoRotate: isMobile }, y: { title: false } }}
              tooltip={{ items: [{ field: 'hours', name: 'Hours' }] }}
              onEvent={(_chart, event) => {
                const row = plotDatum(event)
                const id = typeof row?.id === 'string' ? row.id : undefined
                if (!id) return
                onOpen({ ...filters, bucket: 'all', organizationId: id })
              }}
            />
          )}
        </Card>
      </Col>
    </Row>
  )
}
