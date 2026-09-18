import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'
import {
  PREVIEW_UNAVAILABLE_MESSAGE,
  TIFF_UNAVAILABLE_MESSAGE,
  isBrowserImage,
  isTiff,
  tiffMorePagesNote,
  unavailablePreviewMessage,
  viewerFilePath,
  viewerKind,
} from './documentPreview.ts'

const here = dirname(fileURLToPath(import.meta.url))
const viewer = readFileSync(join(here, 'components/DocumentViewer.tsx'), 'utf8')

describe('CR04 TIFF preview', () => {
  it('treats .tif, .tiff, and image/tiff as TIFF — not a native browser image', () => {
    assert.equal(isTiff('image/tiff', 'scan.tif'), true)
    assert.equal(isTiff('image/tiff', 'scan.tiff'), true)
    assert.equal(isTiff('application/octet-stream', 'plat.TIF'), true)
    assert.equal(isTiff('', 'deed.tiff'), true)
    assert.equal(isTiff('image/x-tiff', 'scan'), true)
    assert.equal(viewerKind('image/tiff', 'scan.tif'), 'tiff')
    assert.equal(isBrowserImage('image/tiff', 'scan.tif'), false)
    assert.equal(isBrowserImage('image/tif', 'scan.tiff'), false)
  })

  it('keeps JPEG and other already-working images on the native /file viewer path', () => {
    assert.equal(viewerKind('image/jpeg', 'photo.jpg'), 'image')
    assert.equal(viewerKind('image/png', 'scan.png'), 'image')
    assert.equal(viewerKind('image/gif', 'anim.gif'), 'image')
    assert.equal(viewerKind('image/webp', 'shot.webp'), 'image')
    assert.equal(isBrowserImage('image/jpeg', 'photo.jpg'), true)
    assert.equal(isTiff('image/jpeg', 'photo.jpg'), false)
    assert.equal(viewerFilePath('item-1', 'image/jpeg', 'photo.jpg'), '/api/work-items/item-1/file')
    assert.equal(viewerFilePath('item-1', 'image/png', 'scan.png'), '/api/work-items/item-1/file')
    assert.equal(viewerFilePath('item-2', 'image/tiff', 'scan.tif'), '/api/work-items/item-2/preview')
    assert.equal(viewerFilePath('item-3', 'application/octet-stream', 'plat.tiff'), '/api/work-items/item-3/preview')
  })

  it('shows a clear unavailable message and a first-page note when more TIFF pages exist', () => {
    assert.equal(tiffMorePagesNote(1), null)
    assert.equal(tiffMorePagesNote(0), null)
    assert.equal(tiffMorePagesNote(4), 'This TIFF has 4 pages. Showing the first page.')
    assert.equal(viewerKind('application/msword', 'lease.doc'), 'unavailable')
    assert.equal(
      unavailablePreviewMessage('lease.doc'),
      `lease.doc: ${PREVIEW_UNAVAILABLE_MESSAGE}`,
    )
    assert.equal(PREVIEW_UNAVAILABLE_MESSAGE.includes('Preview is unavailable'), true)
    assert.equal(TIFF_UNAVAILABLE_MESSAGE.includes('Preview is unavailable'), true)
  })

  it('DocumentViewer fetches TIFF preview and still uses /file for JPEG', () => {
    assert.equal(viewer.includes("viewerFilePath(workItemId, contentType, fileName)"), true)
    assert.equal(viewer.includes('tiffMorePagesNote'), true)
    assert.equal(viewer.includes('unavailablePreviewMessage'), true)
    assert.equal(viewer.includes('PREVIEW_UNAVAILABLE_MESSAGE'), true)
    assert.equal(viewer.includes("kind === 'tiff'"), true)
    assert.equal(viewer.includes("kind === 'image'"), true)
    assert.match(viewer, /onError=\{\(\) => setError\(PREVIEW_UNAVAILABLE_MESSAGE\)\}/)
  })
})
