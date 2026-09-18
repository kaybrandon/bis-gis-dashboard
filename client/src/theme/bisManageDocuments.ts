/** QC07 tokens. GIS-UI-02 reuses these for app-wide panels, cards, and grids. Do not invent new hex values. */
export const BIS_MANAGE_DOCUMENTS = {
  navy: '#142642',
  teal: '#00BA8C',
  rowGreen: '#E8F8F3',
  rowGray: '#F3F4F6',
  select: '#FDE68A',
  selectBorder: '#F59E0B',
  hover: '#FEF3C7',
  textOnNavy: '#FFFFFF',
  textOnRow: '#111827',
} as const

export const FORBIDDEN_SELECTION_LIME = ['#AED20F', '#D4FF00', '#D7FF01', '#D8FF00'] as const

/** Shared class tokens — same MD treatments, used on any panel or grid. */
export const BIS_THEME_PANEL_CLASS = 'bis-theme-panel'
export const BIS_THEME_GRID_CLASS = 'bis-theme-grid'

export const manageDocumentsTableTheme = {
  components: {
    Table: {
      headerBg: BIS_MANAGE_DOCUMENTS.navy,
      headerColor: BIS_MANAGE_DOCUMENTS.textOnNavy,
      headerSortActiveBg: BIS_MANAGE_DOCUMENTS.navy,
      headerSortHoverBg: '#1c3558',
      headerSplitColor: BIS_MANAGE_DOCUMENTS.navy,
      rowHoverBg: BIS_MANAGE_DOCUMENTS.hover,
      rowSelectedBg: BIS_MANAGE_DOCUMENTS.select,
      rowSelectedHoverBg: BIS_MANAGE_DOCUMENTS.select,
      rowSelectedBorderColor: BIS_MANAGE_DOCUMENTS.selectBorder,
      borderColor: BIS_MANAGE_DOCUMENTS.navy,
      headerBorderRadius: 0,
    },
  },
}

export function manageDocumentsRowClassName(index: number, selected: boolean) {
  const zebra = index % 2 === 0 ? 'manage-documents-row-a' : 'manage-documents-row-b'
  return selected ? `${zebra} manage-documents-row-selected` : zebra
}
