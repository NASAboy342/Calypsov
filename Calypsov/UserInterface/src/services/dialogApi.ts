import { requestJson } from '@/services/httpClient'

interface PickPathResult {
  path: string | null
}

const BASE_URL = '/api/dialog'

export function pickFolder() {
  return requestJson<PickPathResult>(`${BASE_URL}/pick-folder`, { method: 'POST' })
}

export function pickFile() {
  return requestJson<PickPathResult>(`${BASE_URL}/pick-file`, { method: 'POST' })
}
