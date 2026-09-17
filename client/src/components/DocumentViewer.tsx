import { Alert, Button, Space, Spin, Typography } from 'antd'
import { useEffect, useRef, useState } from 'react'
import { authorizedBlob } from '../api'

type Props = {
  workItemId: string
  contentType?: string | null
  fileName: string
}

export function DocumentViewer({ workItemId, contentType, fileName }: Props) {
  const [url, setUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [page, setPage] = useState(1)
  const [pages, setPages] = useState(1)
  const [scale, setScale] = useState(1.15)
  const canvasRef = useRef<HTMLCanvasElement | null>(null)
  const highlightRef = useRef<HTMLCanvasElement | null>(null)
  const drag = useRef<{ x: number; y: number } | null>(null)
  const objectUrl = useRef<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    authorizedBlob(`/api/work-items/${workItemId}/file`)
      .then((blob) => {
        if (cancelled) return
        if (objectUrl.current) URL.revokeObjectURL(objectUrl.current)
        objectUrl.current = URL.createObjectURL(blob)
        setUrl(objectUrl.current)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Viewer failed.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
      if (objectUrl.current) URL.revokeObjectURL(objectUrl.current)
    }
  }, [workItemId])

  const isPdf = (contentType ?? '').includes('pdf') || fileName.toLowerCase().endsWith('.pdf')
  const isImage = (contentType ?? '').startsWith('image/')

  useEffect(() => {
    if (!url || !isPdf) return
    let destroyed = false
    const render = async () => {
      const pdfjs = await import('pdfjs-dist')
      pdfjs.GlobalWorkerOptions.workerSrc = new URL(
        'pdfjs-dist/build/pdf.worker.min.mjs',
        import.meta.url,
      ).toString()
      const pdf = await pdfjs.getDocument({ url }).promise
      if (destroyed) return
      setPages(pdf.numPages)
      const current = Math.min(page, pdf.numPages)
      const pdfPage = await pdf.getPage(current)
      const viewport = pdfPage.getViewport({ scale })
      const canvas = canvasRef.current
      if (!canvas) return
      const context = canvas.getContext('2d')
      if (!context) return
      canvas.width = viewport.width
      canvas.height = viewport.height
      await pdfPage.render({ canvas, canvasContext: context, viewport }).promise
      const overlay = highlightRef.current
      if (overlay) {
        overlay.width = viewport.width
        overlay.height = viewport.height
        overlay.getContext('2d')?.clearRect(0, 0, overlay.width, overlay.height)
      }
    }
    render().catch((err) => setError(err instanceof Error ? err.message : 'PDF render failed.'))
    return () => {
      destroyed = true
    }
  }, [url, isPdf, page, scale])

  function onPointerDown(event: React.PointerEvent<HTMLCanvasElement>) {
    const rect = event.currentTarget.getBoundingClientRect()
    drag.current = { x: event.clientX - rect.left, y: event.clientY - rect.top }
  }

  function onPointerUp(event: React.PointerEvent<HTMLCanvasElement>) {
    if (!drag.current || !highlightRef.current) return
    const rect = event.currentTarget.getBoundingClientRect()
    const ctx = highlightRef.current.getContext('2d')
    if (!ctx) return
    const x = Math.min(drag.current.x, event.clientX - rect.left)
    const y = Math.min(drag.current.y, event.clientY - rect.top)
    const w = Math.abs(event.clientX - rect.left - drag.current.x)
    const h = Math.abs(event.clientY - rect.top - drag.current.y)
    ctx.fillStyle = 'rgba(250, 173, 20, 0.28)'
    ctx.fillRect(x, y, w, h)
    drag.current = null
  }

  if (loading) return <div className="doc-viewer"><Spin /></div>
  if (error) return <Alert type="error" showIcon message={error} />
  if (!url) return <Alert type="warning" showIcon message="No file is attached to this work item." />

  if (isImage) {
    return (
      <div className="doc-viewer">
        <img src={url} alt={fileName} />
      </div>
    )
  }

  if (!isPdf) {
    return <Alert type="info" showIcon message={`${fileName} can be downloaded. Preview is for PDFs and images only.`} />
  }

  return (
    <div>
      <Space style={{ marginBottom: 8 }} wrap>
        <Button disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Previous page</Button>
        <Typography.Text>Page {page} / {pages}</Typography.Text>
        <Button disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Next page</Button>
        <Button onClick={() => setScale((s) => Math.max(0.5, s - 0.15))}>Zoom out</Button>
        <Button onClick={() => setScale((s) => Math.min(2.5, s + 0.15))}>Zoom in</Button>
        <Button onClick={() => highlightRef.current?.getContext('2d')?.clearRect(0, 0, highlightRef.current.width, highlightRef.current.height)}>
          Clear highlights
        </Button>
        <Typography.Text type="secondary">Highlights are ephemeral and are not saved.</Typography.Text>
      </Space>
      <div className="doc-viewer">
        <div style={{ position: 'relative' }}>
          <canvas ref={canvasRef} />
          <canvas
            ref={highlightRef}
            style={{ position: 'absolute', inset: 0, cursor: 'crosshair' }}
            onPointerDown={onPointerDown}
            onPointerUp={onPointerUp}
          />
        </div>
      </div>
    </div>
  )
}
