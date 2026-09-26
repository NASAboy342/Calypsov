import { requestJson } from '@/services/httpClient'

export enum BrowserType {
  Edge = 'edge',
  Chrome = 'chrome',
}

export interface BrowserProfile {
  id: string
  name: string
  avatarUrl: string | null
}

interface BrowserProfileSelection {
  profileId: string | null
}

const BASE_URL = '/api/browsers'

export function getProfiles(browser: BrowserType) {
  return requestJson<BrowserProfile[]>(`${BASE_URL}/${browser}/profiles`)
}

export function getSelection(browser: BrowserType) {
  return requestJson<BrowserProfileSelection>(`${BASE_URL}/${browser}/selection`)
}

export function setSelection(browser: BrowserType, profileId: string | null) {
  return requestJson<BrowserProfileSelection>(`${BASE_URL}/${browser}/selection`, {
    method: 'POST',
    body: JSON.stringify({ profileId }),
  })
}
