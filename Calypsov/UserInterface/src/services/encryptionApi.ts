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

const BASE_URL = '/api/encryption'

export function getStatus() {
  return requestJson<EncryptionStatus>(`${BASE_URL}/status`)
}

export function toggleStatus() {
  return requestJson<EncryptionStatus>(`${BASE_URL}/toggle`, { method: 'POST' })
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
