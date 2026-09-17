import { Alert, Button, Progress, Space } from 'antd'
import type { ReactNode } from 'react'
import { completeSummaryMessage, queueProgress, type UploadQueueItem } from '../uploadQueue'
import { UploadQueueList } from './UploadQueueList'

type Props = {
  items: UploadQueueItem[]
  hideCompleted: boolean
  onToggleHideCompleted: () => void
  completeVisible?: boolean
  onDismissComplete?: () => void
  completeDescription?: string
  completeExtra?: ReactNode
}

export function UploadBatchProgress({
  items,
  hideCompleted,
  onToggleHideCompleted,
  completeVisible,
  onDismissComplete,
  completeDescription,
  completeExtra,
}: Props) {
  const p = queueProgress(items)
  if (p.total === 0) return null

  const completedCount = p.done + p.failed + p.skipped
  const visible = hideCompleted
    ? items.filter((item) => item.status === 'pending' || item.status === 'uploading')
    : items
  const completeType = p.failed > 0 && p.done === 0 ? 'error' : p.failed > 0 || p.skipped > 0 ? 'warning' : 'success'

  return (
    <div className="upload-batch">
      <div className={`upload-batch-progress${p.inFlight ? ' is-sticky' : ''}`}>
        {p.inFlight ? (
          <>
            <div className="upload-batch-title">
              Uploading {p.current} of {p.total}
            </div>
            <Progress percent={p.percent} status="active" showInfo />
            <div className="upload-batch-keep-open">Keep this window open until finished.</div>
          </>
        ) : (
          <>
            <div className="upload-batch-title">{p.failed > 0 ? 'Upload finished with errors' : 'Upload complete'}</div>
            <Progress percent={100} status={p.failed > 0 ? 'exception' : 'success'} showInfo />
          </>
        )}
        <div className="upload-batch-tallies">
          <span>
            <strong>{p.done}</strong> done
          </span>
          <span>
            <strong>{p.failed}</strong> failed
          </span>
          <span>
            <strong>{p.skipped}</strong> skipped
          </span>
          <span>
            <strong>{p.remaining}</strong> remaining
          </span>
        </div>
        {completedCount > 0 ? (
          <Button type="link" size="small" className="upload-batch-collapse" onClick={onToggleHideCompleted}>
            {hideCompleted ? `Show completed (${completedCount})` : `Hide completed (${completedCount})`}
          </Button>
        ) : null}
      </div>
      {!p.inFlight && completeVisible ? (
        <Alert
          className="upload-batch-complete"
          type={completeType}
          showIcon
          closable={!!onDismissComplete}
          onClose={(event) => {
            event?.preventDefault?.()
            event?.stopPropagation?.()
          }}
          afterClose={onDismissComplete}
          message={completeSummaryMessage(items)}
          description={
            completeDescription || completeExtra ? (
              <Space direction="vertical" size={8} style={{ width: '100%' }}>
                {completeDescription ? <span>{completeDescription}</span> : null}
                {completeExtra}
              </Space>
            ) : undefined
          }
        />
      ) : null}
      <UploadQueueList items={visible} emptyText={hideCompleted ? 'All files in this batch are completed.' : undefined} />
    </div>
  )
}
