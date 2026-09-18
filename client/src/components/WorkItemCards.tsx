import { Card, Empty, Spin, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import type { WorkItemListItem } from '../api'
import { ASSIGNED_TO_LABEL, UNASSIGNED_LABEL } from '../assignmentLabels'
import { isNeededByOverdue } from '../neededBy'
import { neededByBadgeText } from '../workItemDates'
import { WorkPresenceMarks } from './PresencePeople'
import { statusLabel } from '../statusLabels'
import { PENDING_HIGHLIGHT_CARD_CLASS, isPendingStatus } from '../theme/pendingHighlight'
import '../theme/pendingHighlight.css'
import { workItemTimeLine } from '../workItemDisplay'

type Props = {
  items: WorkItemListItem[]
  loading?: boolean
  emptyText: string
  onOpen: (id: string) => void
  showUploadDate?: boolean
  showReview?: boolean
  showTotalTime?: boolean
}

export function WorkItemCards({ items, loading, emptyText, onOpen, showUploadDate, showReview, showTotalTime }: Props) {
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
      {items.map((item) => {
        const due = neededByBadgeText({ neededBy: item.priorityNeededBy, workedOn: item.workedOn })
        const timeLine = workItemTimeLine(item, { showReview, showTotalTime })
        return (
        <Card
          key={item.id}
          size="small"
          hoverable
          className={isPendingStatus(item.statusName) ? `work-item-card bis-theme-panel ${PENDING_HIGHLIGHT_CARD_CLASS}` : 'work-item-card bis-theme-panel'}
          title={<Typography.Text strong ellipsis style={{ color: 'inherit', maxWidth: '100%' }}>{item.fileName}</Typography.Text>}
          onClick={() => onOpen(item.id)}
        >
          <div className="work-item-card-meta">
            <span>{item.organizationName}</span>
            <Tag color={item.statusColor} style={{ marginInlineEnd: 0 }}>{statusLabel(item.statusName)}</Tag>
            {item.isPriority && <Tag color="red">Priority</Tag>}
            {item.isPriority && due && (
              <Tag color={isNeededByOverdue(item.isPriority, item.priorityNeededBy) ? 'volcano' : 'gold'}>
                {isNeededByOverdue(item.isPriority, item.priorityNeededBy) ? `Overdue · ${due}` : due}
              </Tag>
            )}
          </div>
          <Typography.Text type="secondary" className="work-item-card-line">
            {ASSIGNED_TO_LABEL} {item.assignedToName ?? UNASSIGNED_LABEL}
            <WorkPresenceMarks workItemId={item.id} assignedToUserId={item.assignedToUserId} />
          </Typography.Text>
          <Typography.Text type="secondary" className="work-item-card-line">
            {showUploadDate ? `Uploaded ${dayjs(item.uploadedAt).format('YYYY-MM-DD')}` : null}
            {showUploadDate ? ' · ' : null}
            Worked {item.workedOn ? dayjs(item.workedOn).format('YYYY-MM-DD') : '—'}
            {timeLine ? ` · ${timeLine}` : ''}
          </Typography.Text>
        </Card>
        )
      })}
    </div>
  )
}
