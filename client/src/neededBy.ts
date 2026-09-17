import dayjs from 'dayjs'

export function neededByLabel(value?: string | null) {
  if (!value) return null
  const day = dayjs(value)
  return day.isValid() ? `Needed by ${day.format('YYYY-MM-DD')}` : null
}

export function isNeededByOverdue(isPriority?: boolean, value?: string | null) {
  if (!isPriority || !value) return false
  const day = dayjs(value)
  if (!day.isValid()) return false
  return day.startOf('day').isBefore(dayjs().startOf('day'))
}
