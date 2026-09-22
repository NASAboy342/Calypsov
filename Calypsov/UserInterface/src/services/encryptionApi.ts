export type TargetCategory = 'folder' | 'file'

export interface EncryptionTarget {
  id: string
  category: TargetCategory
  path: string
}

interface EncryptionStatus {
  enabled: boolean
}

interface ErrorResponse {
  message: string
}

const BASE_URL = '/api/encryption'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as ErrorResponse | null
    throw new Error(body?.message ?? `Request failed with status ${response.status}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export function getStatus() {
  return request<EncryptionStatus>('/status')
}

export function toggleStatus() {
  return request<EncryptionStatus>('/toggle', { method: 'POST' })
}

export function getTargets() {
  return request<EncryptionTarget[]>('/targets')
}

export function addTarget(category: TargetCategory, path: string) {
  return request<EncryptionTarget>('/targets', {
    method: 'POST',
    body: JSON.stringify({ category, path }),
  })
}

export function removeTarget(id: string) {
  return request<void>(`/targets/${id}`, { method: 'DELETE' })
}
