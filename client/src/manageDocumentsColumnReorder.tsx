import { HolderOutlined } from '@ant-design/icons'
import type { ColumnsType, ColumnType } from 'antd/es/table'
import type { DragEvent, ReactNode } from 'react'
import {
  MANAGE_DOCUMENTS_COLUMN_LABELS,
  MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH,
  type ManageDocumentsColumnKey,
  isManageDocumentsColumnKey,
  moveColumn,
} from './manageDocumentsColumnPrefs'

function stopHeaderActivate(event: { stopPropagation: () => void }) {
  event.stopPropagation()
}

function DraggableColumnTitle({
  columnKey,
  label,
  children,
}: {
  columnKey: string
  label: string
  children: ReactNode
}) {
  return (
    <span className="manage-documents-col-title">
      <span
        className="manage-documents-col-drag"
        draggable
        role="button"
        tabIndex={0}
        aria-label={`Drag to reorder ${label} column`}
        title={`Drag to reorder ${label}`}
        onClick={stopHeaderActivate}
        onMouseDown={stopHeaderActivate}
        onDragStart={(event) => {
          event.dataTransfer.setData('text/plain', columnKey)
          event.dataTransfer.effectAllowed = 'move'
        }}
      >
        <HolderOutlined />
      </span>
      <span className="manage-documents-col-label">{children}</span>
    </span>
  )
}

export function decorateManageDocumentsColumns<T>(
  columns: ColumnsType<T>,
  order: readonly ManageDocumentsColumnKey[],
  onReorder: (next: ManageDocumentsColumnKey[]) => void,
): ColumnsType<T> {
  return columns.map((column) => {
    const typed = column as ColumnType<T>
    const key = String(typed.key ?? '')
    const isGroup = key === 'group'
    const isFilename = key === 'filename'
    const label = isManageDocumentsColumnKey(key) ? MANAGE_DOCUMENTS_COLUMN_LABELS[key] : key
    const previousHeader = typed.onHeaderCell

    const filenameClass = isFilename
      ? Array.from(new Set([typed.className, 'manage-documents-filename'].filter(Boolean))).join(' ')
      : typed.className

    const decorated: ColumnType<T> = {
      ...typed,
      ellipsis: isFilename ? typed.ellipsis ?? true : typed.ellipsis,
      minWidth: isFilename
        ? Math.max(Number(typed.minWidth ?? 0), MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH)
        : typed.minWidth,
      className: filenameClass,
      title: isGroup ? typed.title : (
        <DraggableColumnTitle columnKey={key} label={label}>
          {typed.title as ReactNode}
        </DraggableColumnTitle>
      ),
      onHeaderCell: (col) => {
        const prev = previousHeader?.(col) ?? {}
        const prevStyle = prev.style ?? {}
        return {
          ...prev,
          className: Array.from(new Set([
            prev.className,
            isFilename ? 'manage-documents-filename' : '',
            isGroup ? '' : 'manage-documents-col-drop',
          ].filter(Boolean))).join(' '),
          style: {
            ...prevStyle,
            ...(isFilename
              ? {
                  minWidth: Math.max(
                    typeof prevStyle.minWidth === 'number' ? prevStyle.minWidth : 0,
                    MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH,
                  ),
                }
              : {}),
          },
          'data-column-key': key,
          onDragOver: (event: DragEvent<HTMLElement>) => {
            if (isGroup) return
            event.preventDefault()
            event.dataTransfer.dropEffect = 'move'
            prev.onDragOver?.(event)
          },
          onDrop: (event: DragEvent<HTMLElement>) => {
            if (isGroup) return
            event.preventDefault()
            const from = event.dataTransfer.getData('text/plain')
            if (!isManageDocumentsColumnKey(from) || from === key) return
            const toIndex = order.indexOf(key as ManageDocumentsColumnKey)
            if (toIndex < 0) return
            onReorder(moveColumn(order, from, toIndex))
            prev.onDrop?.(event)
          },
        }
      },
    }
    return decorated
  })
}
