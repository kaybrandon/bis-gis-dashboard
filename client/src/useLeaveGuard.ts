import { useEffect } from 'react'

/** Warn when the tab is closed or refreshed while uploads are still in flight. */
export function useLeaveWhileUploading(inFlight: boolean) {
  useEffect(() => {
    if (!inFlight) return
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', onBeforeUnload)
    return () => window.removeEventListener('beforeunload', onBeforeUnload)
  }, [inFlight])
}
