import { neededByLabel } from './neededBy'

/** CR02 — exact BA-locked strings. Persistent labels, not placeholders. */
export const WORKED_LABEL = 'Worked'
export const NEEDED_BY_LABEL = 'Needed by'

export type WorkItemDateFields<TDate> = {
  workedOn: TDate
  priorityNeededBy: TDate
}

/** CR01 — changing Worked must not rewrite Needed by. */
export function changeWorkedOn<T extends WorkItemDateFields<unknown>>(
  draft: T,
  workedOn: T['workedOn'],
): T {
  return { ...draft, workedOn }
}

/** CR01 — changing Needed by must not rewrite Worked. */
export function changeNeededBy<T extends WorkItemDateFields<unknown>>(
  draft: T,
  priorityNeededBy: T['priorityNeededBy'],
): T {
  return { ...draft, priorityNeededBy }
}

export function workItemDateLabels(
  _state?: 'empty' | 'populated' | 'cleared' | 'edited' | 'reloaded',
) {
  return { worked: WORKED_LABEL, neededBy: NEEDED_BY_LABEL }
}

export function toDateSavePayload(draft: {
  workedOn: string | null
  priorityNeededBy: string | null
}) {
  return {
    workedOn: draft.workedOn,
    clearWorkedOn: draft.workedOn == null,
    priorityNeededBy: draft.priorityNeededBy,
    clearPriorityNeededBy: draft.priorityNeededBy == null,
  }
}

/** CR01 — badge/display uses saved Needed by only. Worked is ignored. */
export function neededByBadgeText(input: {
  neededBy?: string | null
  workedOn?: string | null
}) {
  return neededByLabel(input.neededBy)
}
