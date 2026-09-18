import type { WorkItemListItem } from './api'
import { reviewLabel } from './statusLabels.ts'

export function totalTimeLabel(hoursLabel: string | undefined): string {
  return hoursLabel?.trim() ? hoursLabel : '0m'
}

export function workItemTimeLine(
  item: Pick<WorkItemListItem, 'isReviewed' | 'hoursLabel'>,
  options?: { showReview?: boolean; showTotalTime?: boolean },
): string {
  const parts: string[] = []
  if (options?.showReview) {
    parts.push(`Review ${reviewLabel(item.isReviewed)}`)
  }
  if (options?.showTotalTime !== false) {
    parts.push(`Total time ${totalTimeLabel(item.hoursLabel)}`)
  }
  return parts.join(' · ')
}
