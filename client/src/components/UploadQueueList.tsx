import { List, Tag, Typography } from 'antd'
import type { QueueStatus, UploadQueueItem } from '../uploadQueue'
import { formatFileSize } from '../uploadQueue'

const statusTag: Record<QueueStatus, { color: string; label: string }> = {
  pending: { color: 'default', label: 'Pending' },
  uploading: { color: 'processing', label: 'Uploading' },
  done: { color: 'success', label: 'Done' },
  failed: { color: 'error', label: 'Failed' },
  skipped: { color: 'warning', label: 'Skipped' },
}

export function UploadQueueList({ items, emptyText }: { items: UploadQueueItem[]; emptyText?: string }) {
  if (items.length === 0) {
    return emptyText ? (
      <Typography.Paragraph type="secondary" className="upload-queue-empty">
        {emptyText}
      </Typography.Paragraph>
    ) : null
  }
  return (
    <List
      size="small"
      className="upload-queue"
      dataSource={items}
      renderItem={(item) => {
        const tag = statusTag[item.status]
        return (
          <List.Item>
            <div className="upload-queue-row">
              <div>
                <Typography.Text>{item.file.name}</Typography.Text>
                <Typography.Text type="secondary"> · {formatFileSize(item.file.size)}</Typography.Text>
              </div>
              <Tag color={tag.color}>{tag.label}</Tag>
              {item.error && (
                <Typography.Text type="danger" className="upload-queue-error">
                  {item.error}
                </Typography.Text>
              )}
            </div>
          </List.Item>
        )
      }}
    />
  )
}
