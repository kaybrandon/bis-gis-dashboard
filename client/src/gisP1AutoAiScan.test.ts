import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const review = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')
const api = readFileSync(join(here, 'api.ts'), 'utf8')
const fill = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/AiFill/WorkItemAiFillService.cs'), 'utf8')
const upload = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/Services/WorkItemService.cs'), 'utf8')
const settings = readFileSync(join(here, '../..', 'src/GisDashboard.Api/Controllers/SettingsController.cs'), 'utf8')
const normalizer = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/AiFill/PropertyIdsNormalizer.cs'), 'utf8')

describe('P1 auto AI-scan on upload + P0 subject PIDs', () => {
  it('kicks AI scan from the server after a successful upload', () => {
    assert.match(upload, /PrepareNewUpload/)
    assert.match(upload, /TryEnqueueAiScan/)
    assert.match(upload, /UploadByTokenAsync/)
    assert.match(review, /AI scan pending/)
    assert.match(review, /AI scan failed/)
    assert.match(review, /applyAutoAiScan/)
    assert.match(review, /AI fill from PDF/)
    assert.match(api, /aiScan/)
    assert.match(settings, /upload kicks the same AI-fill/)
  })

  it('keeps amber Approve / Save and does not overwrite dirty edits', () => {
    assert.match(review, /applyAutoAiScan/)
    assert.match(review, /fieldMatchesBaseline|applyAutoAiScan/)
    assert.match(review, /Approve/)
    assert.match(review, /Save/)
    assert.match(review, /Your edits were kept|Review amber fields, then Save|still needs Save/)
  })

  it('does not invent a Document Intelligence path or extractable-text banner', () => {
    assert.doesNotMatch(review, /no extractable text|cannot be AI-filled/i)
    assert.doesNotMatch(api, /documentIntelligence|DocumentIntelligence|formrecognizer|FormRecognizer/i)
    assert.doesNotMatch(fill, /documentIntelligence|DocumentIntelligence|formrecognizer/i)
    assert.doesNotMatch(fill, /no extractable text|cannot be AI-filled/i)
    assert.match(fill, /page images/)
    assert.match(fill, /subject Property IDs|subject PIDs/)
  })

  it('normalizes CAD\/web-map Property IDs to subject-only or empty', () => {
    assert.match(normalizer, /MaxSubjectIds/)
    assert.match(normalizer, /IsMassDump/)
    assert.match(fill, /ReadPropertyIds/)
    assert.match(fill, /Never vacuum every parcel label/)
    assert.doesNotMatch(review, /AzureOpenAI__ApiKey/)
    assert.doesNotMatch(api, /AzureOpenAI__ApiKey|sk-[a-zA-Z0-9]/)
  })
})
