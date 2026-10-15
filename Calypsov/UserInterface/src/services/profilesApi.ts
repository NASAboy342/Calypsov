import { requestJson } from '@/services/httpClient'
import type { EncryptionTarget } from '@/services/encryptionApi'

export interface ProtectionProfile {
  id: string
  name: string
  color: string
  icon: string
  isEncrypted: boolean
  encryptionTargets: EncryptionTarget[]
  browserProfileSelections: Record<string, string | null>
}

export interface ProfilesResult {
  profiles: ProtectionProfile[]
  currentProfileId: string
}

const BASE_URL = '/api/profiles'

export function getProfiles() {
  return requestJson<ProfilesResult>(BASE_URL)
}

export function createProfile(name: string, color: string, icon: string) {
  return requestJson<ProtectionProfile>(BASE_URL, {
    method: 'POST',
    body: JSON.stringify({ name, color, icon }),
  })
}

export function updateProfile(id: string, name: string, color: string, icon: string) {
  return requestJson<ProtectionProfile>(`${BASE_URL}/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ name, color, icon }),
  })
}

export function switchProfile(id: string) {
  return requestJson<ProtectionProfile>(`${BASE_URL}/${id}/switch`, { method: 'POST' })
}

export function deleteProfile(id: string) {
  return requestJson<void>(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
