import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  getProfiles,
  createProfile as apiCreateProfile,
  updateProfile as apiUpdateProfile,
  switchProfile as apiSwitchProfile,
  deleteProfile as apiDeleteProfile,
  type ProtectionProfile,
} from '@/services/profilesApi'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import { BrowserType } from '@/services/browsersApi'

export type { ProtectionProfile }

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useProfilesStore = defineStore('profiles', () => {
  const items = ref<ProtectionProfile[]>([])
  const currentProfileId = ref<string | null>(null)
  const loading = ref(true)
  /** True while a create/switch/delete request is in flight — drives disabled states in the UI. */
  const mutating = ref(false)
  const error = ref<string | null>(null)

  const currentProfile = computed(() => items.value.find((p) => p.id === currentProfileId.value) ?? null)

  async function fetchProfiles() {
    loading.value = true
    error.value = null
    try {
      const result = await getProfiles()
      items.value = result.profiles
      currentProfileId.value = result.currentProfileId
    } catch (err) {
      error.value = toMessage(err, 'Could not load profiles.')
    } finally {
      loading.value = false
    }
  }

  /**
   * Re-fetches everything whose data is scoped to the current protection profile — targets,
   * browser-profile selections, and encryption status (IsEncrypted now lives on the profile, not
   * globally) — so switching the active profile never leaves stale data from the previous one
   * showing on the Settings/Home pages.
   */
  async function refreshProfileScopedData() {
    const encryption = useEncryptionStore()
    const targets = useTargetsStore()
    const browserProfiles = useBrowserProfilesStore()
    await Promise.all([
      encryption.fetchStatus(),
      targets.fetchTargets(),
      browserProfiles.fetchProfiles(BrowserType.Edge),
      browserProfiles.fetchProfiles(BrowserType.Chrome),
    ])
  }

  /** Returns an error message on failure, or null on success. */
  async function createProfile(name: string, color: string, icon: string): Promise<string | null> {
    mutating.value = true
    error.value = null
    try {
      const profile = await apiCreateProfile(name, color, icon)
      items.value.push(profile)
      currentProfileId.value = profile.id
      await refreshProfileScopedData()
      return null
    } catch (err) {
      return toMessage(err, 'Could not create the profile.')
    } finally {
      mutating.value = false
    }
  }

  /** Returns an error message on failure, or null on success. Not gated by the protected lock on
   * the backend — renaming/recoloring/re-iconing never touches what's actually protected. */
  async function updateProfile(id: string, name: string, color: string, icon: string): Promise<string | null> {
    mutating.value = true
    error.value = null
    try {
      const updated = await apiUpdateProfile(id, name, color, icon)
      const index = items.value.findIndex((p) => p.id === id)
      if (index !== -1) items.value[index] = updated
      return null
    } catch (err) {
      return toMessage(err, 'Could not update the profile.')
    } finally {
      mutating.value = false
    }
  }

  /** Returns an error message on failure, or null on success. */
  async function switchProfile(id: string): Promise<string | null> {
    if (id === currentProfileId.value) return null
    mutating.value = true
    error.value = null
    try {
      await apiSwitchProfile(id)
      currentProfileId.value = id
      await refreshProfileScopedData()
      return null
    } catch (err) {
      return toMessage(err, 'Could not switch profiles.')
    } finally {
      mutating.value = false
    }
  }

  /** Returns an error message on failure, or null on success. */
  async function deleteProfile(id: string): Promise<string | null> {
    mutating.value = true
    error.value = null
    try {
      await apiDeleteProfile(id)
      const wasCurrent = currentProfileId.value === id
      items.value = items.value.filter((p) => p.id !== id)
      if (wasCurrent) {
        // Mirrors the backend's own fallback: the first remaining profile becomes current.
        currentProfileId.value = items.value[0]?.id ?? null
        await refreshProfileScopedData()
      }
      return null
    } catch (err) {
      return toMessage(err, 'Could not delete the profile.')
    } finally {
      mutating.value = false
    }
  }

  return {
    items,
    currentProfileId,
    currentProfile,
    loading,
    mutating,
    error,
    fetchProfiles,
    createProfile,
    updateProfile,
    switchProfile,
    deleteProfile,
  }
})
