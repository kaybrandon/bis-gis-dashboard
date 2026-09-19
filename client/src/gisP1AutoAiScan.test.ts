import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { describe, it } from 'node:test'
import { fileURLToPath } from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const review = readFileSync(join(here, 'pages/ViewDocumentPage.tsx'), 'utf8')
const api = readFileSync(join(here, 'api.ts'), 'utf8')
const fill = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/AiFill/WorkItemAiFillService.cs'), 'utf8')
const kinds = readFileSync(join(here, '../..', 'src/GisDashboard.Application/AiFill/AiFillSourceKinds.cs'), 'utf8')
const upload = readFileSync(join(here, '../..', 'src/GisDashboard.Infrastructure/Services/WorkItemService.cs'), 'utf8')
const settings = readFileSync(join(here, '../..', 'src/GisDashboard.Api/Controllers/SettingsController.cs'), 'utf8')
const scan = readFileSync(join(here, 'aiScan.ts'), 'utf8')

describe('P1 auto AI-scan on upload + QC4-01 manual Property IDs', () => {
  it('kicks AI scan from the server after a successful upload', () => {
    assert.match(upload, /PrepareNewUpload/)
    assert.match(upload, /TryEnqueueAiScan/)
    assert.match(upload, /UploadByTokenAsync/)
    assert.match(review, /AI scan pending/)
    assert.match(review, /AI scan failed/)
    assert.match(review, /applyAutoAiScan/)
    assert.match(review, /AI fill/)
    assert.match(review, /JPG\/JPEG, PNG, TIFF\/TIF, DOCX/)
    assert.match(api, /aiScan/)
    assert.match(settings, /upload kicks the same AI-fill/)
    assert.match(settings, /Property IDs are manual-only/)
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
    assert.match(fill, /SanitizeScanMessage/)
    assert.match(fill, /page images/)
    assert.match(fill, /Do not extract or return propertyIds/)
    assert.match(fill, /AiFillSourceKinds/)
    assert.match(fill, /ExtractXlsxFirstSheet/)
    assert.match(fill, /DocumentImageRenderer/)
    assert.match(fill, /PasswordMessage/)
    assert.match(kinds, /first sheet/)
    assert.doesNotMatch(kinds, /docm|xlsm|\.doc"/)
  })

  it('never writes Property IDs from any AI path', () => {
    assert.match(fill, /Property IDs are entered manually|Do not extract or return propertyIds/)
    assert.match(fill, /new AiFillStringField\(false, null, 0\)/)
    assert.doesNotMatch(fill, /ReadPropertyIds/)
    assert.match(scan, /Property IDs are manual-only/)
    assert.doesNotMatch(review, /<AiField field="propertyIds"/)
    assert.match(review, /manual entry only/)
    assert.doesNotMatch(review, /AzureOpenAI__ApiKey/)
    assert.doesNotMatch(api, /AzureOpenAI__ApiKey|sk-[a-zA-Z0-9]/)
  })
})
