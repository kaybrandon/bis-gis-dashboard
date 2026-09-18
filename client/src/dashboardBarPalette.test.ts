import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  DASHBOARD_BAR_PALETTE,
  dashboardBarColor,
  dashboardBarColorScale,
  dashboardBarColors,
  dashboardCategoryBarPlot,
} from './dashboardBarPalette.ts'

const here = dirname(fileURLToPath(import.meta.url))
const charts = readFileSync(join(here, 'components/DashboardCharts.tsx'), 'utf8')

const CHART_CARD_BG = '#ffffff'
const LAYOUT_BG = '#f0f2f5'
const MIN_CONTRAST = 3

function channel(hex: string, offset: number) {
  return parseInt(hex.slice(offset, offset + 2), 16) / 255
}

function relativeLuminance(hex: string) {
  const value = hex.replace('#', '').toLowerCase()
  assert.match(value, /^[0-9a-f]{6}$/)
  const linear = (c: number) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4)
  const r = linear(channel(value, 0))
  const g = linear(channel(value, 2))
  const b = linear(channel(value, 4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrastRatio(foreground: string, background: string) {
  const a = relativeLuminance(foreground)
  const b = relativeLuminance(background)
  const [hi, lo] = a > b ? [a, b] : [b, a]
  return (hi + 0.05) / (lo + 0.05)
}

function rgbDistance(a: string, b: string) {
  const av = a.replace('#', '')
  const bv = b.replace('#', '')
  const dr = parseInt(av.slice(0, 2), 16) - parseInt(bv.slice(0, 2), 16)
  const dg = parseInt(av.slice(2, 4), 16) - parseInt(bv.slice(2, 4), 16)
  const db = parseInt(av.slice(4, 6), 16) - parseInt(bv.slice(4, 6), 16)
  return Math.sqrt(dr * dr + dg * dg + db * db)
}

describe('GIS-UI-03 Dashboard bar palette', () => {
  it('keeps twelve unique on-theme colors and includes Mask F primary / error', () => {
    assert.equal(DASHBOARD_BAR_PALETTE.length, 12)
    assert.equal(new Set(DASHBOARD_BAR_PALETTE.map((c) => c.toLowerCase())).size, 12)
    assert.ok(DASHBOARD_BAR_PALETTE.includes('#1890ff'))
    assert.ok(DASHBOARD_BAR_PALETTE.includes('#f5222d'))
  })

  it('assigns a distinct color per displayed category and wraps the palette', () => {
    const colors = dashboardBarColors(14)
    assert.equal(colors.length, 14)
    assert.equal(new Set(colors.slice(0, 12)).size, 12)
    assert.equal(colors[0], DASHBOARD_BAR_PALETTE[0])
    assert.equal(colors[12], DASHBOARD_BAR_PALETTE[0])
    assert.equal(colors[13], DASHBOARD_BAR_PALETTE[1])
    assert.equal(dashboardBarColor(-1), DASHBOARD_BAR_PALETTE[11])
    assert.deepEqual(dashboardBarColors(0), [])
  })

  it('maps category names in display order without remapping values', () => {
    const names = ['Alex CAD', 'Bee CAD', 'Camp CAD']
    const scale = dashboardBarColorScale(names)
    assert.deepEqual(scale.color.domain, names)
    assert.deepEqual(scale.color.range, ['#1890ff', '#d46b08', '#389e0d'])
    assert.equal(scale.color.type, 'ordinal')
  })

  it('stays readable on the chart card and layout backgrounds', () => {
    for (const color of DASHBOARD_BAR_PALETTE) {
      assert.ok(
        contrastRatio(color, CHART_CARD_BG) >= MIN_CONTRAST,
        `${color} washes out on ${CHART_CARD_BG} (${contrastRatio(color, CHART_CARD_BG).toFixed(2)})`,
      )
      assert.ok(
        contrastRatio(color, LAYOUT_BG) >= 2.8,
        `${color} washes out on ${LAYOUT_BG} (${contrastRatio(color, LAYOUT_BG).toFixed(2)})`,
      )
    }
  })

  it('keeps adjacent palette hues distinguishable', () => {
    for (let i = 0; i < DASHBOARD_BAR_PALETTE.length; i += 1) {
      const a = DASHBOARD_BAR_PALETTE[i]
      const b = DASHBOARD_BAR_PALETTE[(i + 1) % DASHBOARD_BAR_PALETTE.length]
      assert.ok(rgbDistance(a, b) > 80, `${a} and ${b} are too close`)
    }
  })

  it('does not reuse Manage Documents QC07 tokens', () => {
    const forbidden = ['#142642', '#00BA8C', '#E8F8F3', '#FDE68A', '#AED20F']
    for (const hex of forbidden) {
      assert.equal(
        DASHBOARD_BAR_PALETTE.some((c) => c.toLowerCase() === hex.toLowerCase()),
        false,
      )
    }
  })
})

describe('GIS-UI-03 DashboardCharts wiring', () => {
  it('colors all four in-scope bar charts from the shared helper', () => {
    assert.match(charts, /dashboardCategoryBarPlot/)
    assert.match(charts, /Documents by CAD|DOCUMENTS_BY_CAD_TITLE/)
    assert.match(charts, /Documents by technician|DOCUMENTS_BY_TECHNICIAN_TITLE/)
    assert.match(charts, /Hours by assignee/)
    assert.match(charts, /Hours by client/)

    const columns = [...charts.matchAll(/<Column\b[\s\S]*?\/>/g)].map((m) => m[0])
    assert.equal(columns.length, 4, 'Fail if: a bar chart was added or removed.')
    for (const column of columns) {
      assert.match(column, /colorField="name"|dashboardCategoryBarPlot\(/)
      assert.match(column, /dashboardCategoryBarPlot\(/)
      assert.doesNotMatch(column, /\bcolor="#/)
      assert.match(column, /xField="name"/)
      assert.match(column, /axis=\{\{ x: \{ title: false/)
      assert.match(column, /onEvent=/)
    }
  })

  it('keeps category labels, click filters, and does not import the MD theme', () => {
    const plot = dashboardCategoryBarPlot(['Alex CAD', 'Bee CAD'])
    assert.equal(plot.colorField, 'name')
    assert.equal(plot.legend, false)
    assert.deepEqual(plot.scale.color.domain, ['Alex CAD', 'Bee CAD'])

    assert.match(charts, /documentsByCadQuery\(filters, id\)/)
    assert.match(charts, /documentsByTechnicianQuery\(filters, id\)/)
    assert.match(charts, /assignedToUserId: id/)
    assert.match(charts, /organizationId: id/)
    assert.doesNotMatch(charts, /bisManageDocuments/)
  })
})
