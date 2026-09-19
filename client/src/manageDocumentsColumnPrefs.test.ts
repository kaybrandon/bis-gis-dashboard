import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  MANAGE_DOCUMENTS_COLUMN_KEYS,
  MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX,
  MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER,
  MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH,
  manageDocumentsColumnPrefsKey,
  manageDocumentsTableScrollX,
  moveColumn,
  normalizeColumnOrder,
  orderColumns,
  readColumnOrder,
  resetColumnOrder,
  writeColumnOrder,
} from './manageDocumentsColumnPrefs.ts'

class MemoryStorage {
  private readonly data = new Map<string, string>()
  getItem(key: string) {
    return this.data.get(key) ?? null
  }
  setItem(key: string, value: string) {
    this.data.set(key, value)
  }
  removeItem(key: string) {
    this.data.delete(key)
  }
}

describe('QC4-03 Manage Documents column order prefs', () => {
  it('keeps File name in the default order and a usable min width', () => {
    assert.equal(MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER[0], 'filename')
    assert.deepEqual([...MANAGE_DOCUMENTS_COLUMN_KEYS], [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER])
    assert.ok(MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH >= 220)
    assert.ok(manageDocumentsTableScrollX(false) >= MANAGE_DOCUMENTS_FILENAME_MIN_WIDTH + 980)
    assert.ok(manageDocumentsTableScrollX(true) > manageDocumentsTableScrollX(false))
  })

  it('normalizes unknown, duplicate, and partial saved orders', () => {
    assert.deepEqual(
      normalizeColumnOrder(['hours', 'filename', 'hours', 'nope', 12]),
      ['hours', 'filename', 'client', 'status', 'assignedto', 'uploadedAt', 'workedon', 'difficulty', 'reviewed'],
    )
    assert.deepEqual(normalizeColumnOrder(null), [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER])
  })

  it('moves a column without mutating the source and without dropping File name', () => {
    const start = [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER]
    const moved = moveColumn(start, 'status', 0)
    assert.deepEqual(start, [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER])
    assert.equal(moved[0], 'status')
    assert.ok(moved.includes('filename'))
    assert.equal(moved.length, start.length)
    assert.deepEqual(moveColumn(start, 'filename', 3)[3], 'filename')
  })

  it('pins the Group column first when ordering table columns', () => {
    const columns = [
      { key: 'filename' },
      { key: 'client' },
      { key: 'group' },
      { key: 'status' },
    ]
    assert.deepEqual(
      orderColumns(columns, ['status', 'filename', 'client']).map((column) => column.key),
      ['group', 'status', 'filename', 'client'],
    )
  })

  it('persists per user and Reset only clears that user', () => {
    const storage = new MemoryStorage()
    writeColumnOrder('user-a', ['hours', 'filename'], storage)
    writeColumnOrder('user-b', ['client', 'filename'], storage)
    assert.equal(readColumnOrder('user-a', storage)[0], 'hours')
    assert.equal(readColumnOrder('user-b', storage)[0], 'client')
    assert.equal(storage.getItem(manageDocumentsColumnPrefsKey('user-a'))?.includes('hours'), true)
    assert.notEqual(
      manageDocumentsColumnPrefsKey('user-a'),
      manageDocumentsColumnPrefsKey('user-b'),
    )
    assert.match(manageDocumentsColumnPrefsKey('user-a'), new RegExp(`^${MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX}\\.`))

    const restored = resetColumnOrder('user-a', storage)
    assert.deepEqual(restored, [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER])
    assert.equal(storage.getItem(manageDocumentsColumnPrefsKey('user-a')), null)
    assert.equal(readColumnOrder('user-b', storage)[0], 'client')
  })

  it('does not write a shared key when the user id is missing', () => {
    const storage = new MemoryStorage()
    writeColumnOrder(undefined, ['hours'], storage)
    writeColumnOrder(null, ['hours'], storage)
    assert.equal(storage.getItem(MANAGE_DOCUMENTS_COLUMNS_STORAGE_PREFIX), null)
    assert.deepEqual(readColumnOrder(undefined, storage), [...MANAGE_DOCUMENTS_DEFAULT_COLUMN_ORDER])
  })
})
