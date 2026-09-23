import { requestJson } from '@/services/httpClient'

interface PickPathsResult {
  paths: string[]
}

const BASE_URL = '/api/dialog'

export function pickFolders() {
  return requestJson<PickPathsResult>(`${BASE_URL}/pick-folder`, { method: 'POST' })
}

export function pickFiles() {
  return requestJson<PickPathsResult>(`${BASE_URL}/pick-file`, { method: 'POST' })
}
