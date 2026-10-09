import { requestJson } from '@/services/httpClient'

export interface LogSetting {
  isRecordLog: boolean
}

export interface LogFileSummary {
  fileName: string
  modifiedAtUtc: string
}

export interface LogFileContent {
  fileName: string
  content: string
}

const BASE_URL = '/api/logs'

export function getLogSetting() {
  return requestJson<LogSetting>(`${BASE_URL}/setting`)
}

export function updateLogSetting(isRecordLog: boolean) {
  return requestJson<LogSetting>(`${BASE_URL}/setting`, {
    method: 'PUT',
    body: JSON.stringify({ isRecordLog }),
  })
}

/** Newest-modified first — matches the backend's own ordering. */
export function getLogFiles() {
  return requestJson<LogFileSummary[]>(`${BASE_URL}/files`)
}

export function getLogFileContent(fileName: string) {
  return requestJson<LogFileContent>(`${BASE_URL}/files/${encodeURIComponent(fileName)}`)
}
