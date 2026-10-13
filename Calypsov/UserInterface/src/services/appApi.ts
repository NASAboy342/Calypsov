import { requestJson } from '@/services/httpClient'

export interface AppVersion {
  version: string
}

export interface UpdateCheckResult {
  currentVersion: string
  latestVersion: string | null
  updateAvailable: boolean
  releaseUrl: string | null
  error: string | null
}

const BASE_URL = '/api/app'

export function getAppVersion() {
  return requestJson<AppVersion>(`${BASE_URL}/version`)
}

export function checkForUpdate() {
  return requestJson<UpdateCheckResult>(`${BASE_URL}/update-check`)
}

export function openReleasePage(url: string) {
  return requestJson<void>(`${BASE_URL}/open-release-page`, {
    method: 'POST',
    body: JSON.stringify({ url }),
  })
}
