import { DownOutlined, UpOutlined } from '@ant-design/icons'
import { Button, Dropdown, Space, Typography } from 'antd'
import {
  MANAGE_DOCUMENTS_COLUMN_LABELS,
  type ManageDocumentsColumnKey,
  moveColumn,
} from './manageDocumentsColumnPrefs'

export function ManageDocumentsColumnsControl({
  order,
  onChange,
  onReset,
}: {
  order: readonly ManageDocumentsColumnKey[]
  onChange: (next: ManageDocumentsColumnKey[]) => void
  onReset: () => void
}) {
  return (
    <Space wrap>
      <Dropdown
        trigger={['click']}
        popupRender={() => (
          <div className="manage-documents-columns-panel" role="dialog" aria-label="Reorder columns">
            <Typography.Text type="secondary">
              Drag a header handle, or move columns here. Order is saved for your account only.
            </Typography.Text>
            <ol className="manage-documents-columns-list">
              {order.map((key, index) => (
                <li key={key} className="manage-documents-columns-item">
                  <span>{MANAGE_DOCUMENTS_COLUMN_LABELS[key]}</span>
                  <Space size={4}>
                    <Button
                      size="small"
                      icon={<UpOutlined />}
                      aria-label={`Move ${MANAGE_DOCUMENTS_COLUMN_LABELS[key]} earlier`}
                      disabled={index === 0}
                      onClick={() => onChange(moveColumn(order, key, index - 1))}
                    />
                    <Button
                      size="small"
                      icon={<DownOutlined />}
                      aria-label={`Move ${MANAGE_DOCUMENTS_COLUMN_LABELS[key]} later`}
                      disabled={index === order.length - 1}
                      onClick={() => onChange(moveColumn(order, key, index + 1))}
                    />
                  </Space>
                </li>
              ))}
            </ol>
            <Button size="small" onClick={onReset}>
              Reset columns
            </Button>
          </div>
        )}
      >
        <Button aria-haspopup="dialog" aria-label="Reorder columns">
          Columns
        </Button>
      </Dropdown>
      <Button onClick={onReset} aria-label="Reset columns to the default order">
        Reset columns
      </Button>
    </Space>
  )
}
