import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getAppVersion, checkForUpdate as checkForUpdateApi, openReleasePage } from '@/services/appApi'

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useAppUpdateStore = defineStore('appUpdate', () => {
  const currentVersion = ref<string | null>(null)
  const latestVersion = ref<string | null>(null)
  const updateAvailable = ref(false)
  const releaseUrl = ref<string | null>(null)
  const checking = ref(false)
  const error = ref<string | null>(null)
  /** True once a check has completed at least once — drives the "up to date" message. */
  const checked = ref(false)

  async function checkForUpdate() {
    checking.value = true
    error.value = null
    try {
      const result = await checkForUpdateApi()
      currentVersion.value = result.currentVersion
      latestVersion.value = result.latestVersion
      updateAvailable.value = result.updateAvailable
      releaseUrl.value = result.releaseUrl
      error.value = result.error
      checked.value = true
    } catch (err) {
      error.value = toMessage(err, 'Could not check for updates.')
    } finally {
      checking.value = false
    }
  }

  async function fetchCurrentVersion() {
    try {
      currentVersion.value = (await getAppVersion()).version
    } catch (err) {
      error.value = toMessage(err, 'Could not load the app version.')
    }
  }

  async function openRelease() {
    if (!releaseUrl.value) return
    try {
      await openReleasePage(releaseUrl.value)
    } catch (err) {
      error.value = toMessage(err, 'Could not open the release page.')
    }
  }

  return {
    currentVersion,
    latestVersion,
    updateAvailable,
    releaseUrl,
    checking,
    checked,
    error,
    checkForUpdate,
    fetchCurrentVersion,
    openRelease,
  }
})
