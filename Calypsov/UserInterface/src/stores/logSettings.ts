import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getLogSetting, updateLogSetting } from '@/services/logsApi'

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useLogSettingsStore = defineStore('logSettings', () => {
  const isRecordLog = ref(false)
  const loading = ref(true)
  /** True while the toggle change is being saved — drives the switch's disabled state. */
  const saving = ref(false)
  const error = ref<string | null>(null)

  async function fetchLogSetting() {
    loading.value = true
    error.value = null
    try {
      isRecordLog.value = (await getLogSetting()).isRecordLog
    } catch (err) {
      error.value = toMessage(err, 'Could not load the logging setting.')
    } finally {
      loading.value = false
    }
  }

  /** Applies the change immediately, rolling back if the save fails. */
  async function setRecordLog(value: boolean) {
    const previous = isRecordLog.value
    isRecordLog.value = value

    saving.value = true
    error.value = null
    try {
      isRecordLog.value = (await updateLogSetting(value)).isRecordLog
    } catch (err) {
      isRecordLog.value = previous
      error.value = toMessage(err, 'Could not update the logging setting.')
    } finally {
      saving.value = false
    }
  }

  return { isRecordLog, loading, saving, error, fetchLogSetting, setRecordLog }
})
