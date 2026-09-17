import { Card, Empty, Spin, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import type { WorkItemListItem } from '../api'
import { isNeededByOverdue, neededByLabel } from '../neededBy'
import { reviewLabel, statusLabel } from '../statusLabels'

type Props = {
  items: WorkItemListItem[]
  loading?: boolean
  emptyText: string
  onOpen: (id: string) => void
  showUploadDate?: boolean
  showReview?: boolean
}

export function WorkItemCards({ items, loading, emptyText, onOpen, showUploadDate, showReview }: Props) {
  if (loading && items.length === 0) {
    return (
      <div style={{ textAlign: 'center', padding: 24 }}>
        <Spin />
      </div>
    )
  }

  if (items.length === 0) {
    return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText} />
  }

  return (
    <div className="work-item-cards">
      {items.map((item) => (
        <Card
          key={item.id}
          size="small"
          hoverable
          className="work-item-card"
          onClick={() => onOpen(item.id)}
        >
          <Typography.Text strong ellipsis style={{ display: 'block' }}>
            {item.fileName}
          </Typography.Text>
          <div className="work-item-card-meta">
            <span>{item.organizationName}</span>
            <Tag color={item.statusColor} style={{ marginInlineEnd: 0 }}>{statusLabel(item.statusName)}</Tag>
            {item.isPriority && <Tag color="red">Priority</Tag>}
            {item.isPriority && neededByLabel(item.priorityNeededBy) && (
              <Tag color={isNeededByOverdue(item.isPriority, item.priorityNeededBy) ? 'volcano' : 'gold'}>
                {isNeededByOverdue(item.isPriority, item.priorityNeededBy)
                  ? `Overdue · ${neededByLabel(item.priorityNeededBy)}`
                  : neededByLabel(item.priorityNeededBy)}
              </Tag>
            )}
          </div>
          <Typography.Text type="secondary" className="work-item-card-line">
            Assigned to {item.assignedToName ?? '—'}
          </Typography.Text>
          <Typography.Text type="secondary" className="work-item-card-line">
            {showUploadDate ? `Uploaded ${dayjs(item.uploadedAt).format('YYYY-MM-DD')}` : null}
            {showUploadDate ? ' · ' : null}
            Worked {item.workedOn ? dayjs(item.workedOn).format('YYYY-MM-DD') : '—'}
            {' · '}
            {showReview ? `Review ${reviewLabel(item.isReviewed)}` : item.hoursLabel}
          </Typography.Text>
        </Card>
      ))}
    </div>
  )
}
