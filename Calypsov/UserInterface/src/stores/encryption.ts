import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getStatus, toggleStatus } from '@/services/encryptionApi'

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useEncryptionStore = defineStore('encryption', () => {
  const enabled = ref(false)
  const loading = ref(true)
  /** True while a toggle request is in flight — drives the "processing" UI. */
  const toggling = ref(false)
  /** Exact message from the last failed status fetch or toggle, if any. */
  const error = ref<string | null>(null)

  async function fetchStatus() {
    loading.value = true
    error.value = null
    try {
      enabled.value = (await getStatus()).enabled
    } catch (err) {
      error.value = toMessage(err, 'Could not load encryption status.')
    } finally {
      loading.value = false
    }
  }

  async function toggle() {
    if (toggling.value) return

    toggling.value = true
    error.value = null
    try {
      enabled.value = (await toggleStatus()).enabled
    } catch (err) {
      error.value = toMessage(err, 'Could not update encryption status.')
    } finally {
      toggling.value = false
    }
  }

  return { enabled, loading, toggling, error, fetchStatus, toggle }
})
