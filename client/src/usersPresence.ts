/** Live green dot uses the same Online signal as Who’s online (90s heartbeat). */
export function isLiveOnline(
  presenceStatus: string | null | undefined,
  isArchived?: boolean,
): boolean {
  if (isArchived) {
    return false
  }

  return presenceStatus === 'Online'
}
