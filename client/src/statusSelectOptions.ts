import type { StatusActions } from './api.ts'
import { statusLabel } from './statusLabels.ts'

export function statusSelectOptions(
  actions: StatusActions,
  current?: { id: string; name: string },
): { value: string; label: string }[] {
  const options = [
    { value: actions.activeId, label: 'Active' },
    { value: actions.pendingId, label: 'Pending' },
    { value: actions.completeId, label: 'Complete' },
    { value: actions.onHoldId, label: 'On-Hold' },
    { value: actions.needsReviewId, label: 'Needs Review' },
    { value: actions.cancelledId, label: 'Cancelled' },
  ]
  const known = new Set(options.map((option) => option.value))
  if (current && !known.has(current.id)) {
    options.push({ value: current.id, label: statusLabel(current.name) })
  }
  return options
}
