import { requestJson } from '@/services/httpClient'

export enum EnumTargetCategory {
  Folder = 'folder',
  File = 'file',
}

export interface EncryptionTarget {
  id: string
  category: EnumTargetCategory
  path: string
}

interface EncryptionStatus {
  enabled: boolean
}

/** One Turbo Zip worker's progress through its own chunk of folders. */
export interface ZipThreadProgress {
  threadIndex: number
  completed: number
  total: number
  currentItem: string | null
}

export interface EncryptionProgress {
  isRunning: boolean
  completed: number
  total: number
  currentItem: string | null
  /** Per-worker breakdown while Turbo Zip is splitting a category across multiple zip parts;
   * empty when running single-threaded. */
  threads: ZipThreadProgress[]
}

export interface ZipSettings {
  isUseTurboZip: boolean
  isEncryptZippedFile: boolean
}

const BASE_URL = '/api/encryption'

export function getStatus() {
  return requestJson<EncryptionStatus>(`${BASE_URL}/status`)
}

export function toggleStatus() {
  return requestJson<EncryptionStatus>(`${BASE_URL}/toggle`, { method: 'POST' })
}

/** Safe to poll frequently — cheap, read-only snapshot of an in-flight toggle. */
export function getProgress() {
  return requestJson<EncryptionProgress>(`${BASE_URL}/progress`)
}

export function getTargets() {
  return requestJson<EncryptionTarget[]>(`${BASE_URL}/targets`)
}

export function addTarget(category: EnumTargetCategory, path: string) {
  return requestJson<EncryptionTarget>(`${BASE_URL}/targets`, {
    method: 'POST',
    body: JSON.stringify({ category, path }),
  })
}

export function removeTarget(id: string) {
  return requestJson<void>(`${BASE_URL}/targets/${id}`, { method: 'DELETE' })
}

export function getZipSettings() {
  return requestJson<ZipSettings>(`${BASE_URL}/zip-settings`)
}

export function updateZipSettings(settings: ZipSettings) {
  return requestJson<ZipSettings>(`${BASE_URL}/zip-settings`, {
    method: 'PUT',
    body: JSON.stringify(settings),
  })
}
