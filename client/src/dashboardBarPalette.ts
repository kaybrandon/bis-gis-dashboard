/** GIS-UI-03 — distinct Dashboard bar colors for each displayed category. */

/**
 * Ant Design / Mask F hues, darkened where the -6 swatch washes out on white
 * chart cards (`#ffffff`) or the layout (`#f0f2f5`). Adjacent hues stay far apart
 * so CAD / technician / assignee / client bars are distinguishable.
 */
export const DASHBOARD_BAR_PALETTE = [
  '#1890ff', // colorPrimary
  '#d46b08', // orange-7
  '#389e0d', // green-7
  '#722ed1', // purple-6
  '#08979c', // cyan-7
  '#f5222d', // colorError
  '#2f54eb', // geekblue-6
  '#ad6800', // gold-8
  '#eb2f96', // magenta-6
  '#d4380d', // volcano-7
  '#096dd9', // blue-7
  '#531dab', // purple-7
] as const

export type DashboardBarColor = (typeof DASHBOARD_BAR_PALETTE)[number]

export function dashboardBarColor(index: number): DashboardBarColor {
  const length = DASHBOARD_BAR_PALETTE.length
  const safe = ((index % length) + length) % length
  return DASHBOARD_BAR_PALETTE[safe]
}

export function dashboardBarColors(count: number): DashboardBarColor[] {
  const n = Math.max(0, Math.floor(count))
  return Array.from({ length: n }, (_, index) => dashboardBarColor(index))
}

/** Ordinal color scale: displayed category names, in chart order, map 1:1 to the palette. */
export function dashboardBarColorScale(categoryNames: readonly string[]) {
  return {
    color: {
      type: 'ordinal' as const,
      domain: [...categoryNames],
      range: dashboardBarColors(categoryNames.length),
    },
  }
}

/**
 * Shared Column encoding for the four Dashboard bar charts.
 * Axis category labels stay; the color legend is off so users are not color-only.
 */
export function dashboardCategoryBarPlot(categoryNames: readonly string[]) {
  return {
    colorField: 'name' as const,
    scale: dashboardBarColorScale(categoryNames),
    legend: false as const,
  }
}
