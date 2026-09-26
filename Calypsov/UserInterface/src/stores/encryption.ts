import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getStatus, getProgress, toggleStatus, type EncryptionProgress } from '@/services/encryptionApi'

/** How often to poll /progress while a toggle is in flight. */
const PROGRESS_POLL_INTERVAL_MS = 300

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
  /** Latest snapshot from /progress while toggling; null once the toggle finishes. */
  const progress = ref<EncryptionProgress | null>(null)

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
    progress.value = null

    // /toggle blocks on the backend until the whole zip/unzip finishes, so this polls a
    // separate, cheap endpoint concurrently to show real progress while that request is
    // still in flight — best-effort: a poll failing shouldn't interrupt the toggle itself.
    const pollHandle = window.setInterval(async () => {
      try {
        progress.value = await getProgress()
      } catch {
        // ignore — next tick will try again
      }
    }, PROGRESS_POLL_INTERVAL_MS)

    try {
      enabled.value = (await toggleStatus()).enabled
    } catch (err) {
      error.value = toMessage(err, 'Could not update encryption status.')
    } finally {
      window.clearInterval(pollHandle)
      toggling.value = false
      progress.value = null
    }
  }

  return { enabled, loading, toggling, error, progress, fetchStatus, toggle }
})
