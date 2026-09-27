import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getZipSettings, updateZipSettings, type ZipSettings } from '@/services/encryptionApi'

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useZipSettingsStore = defineStore('zipSettings', () => {
  const isUseTurboZip = ref(false)
  const isEncryptZippedFile = ref(false)
  const loading = ref(true)
  /** True while a change is being saved — drives the disabled state on the switches. */
  const saving = ref(false)
  const error = ref<string | null>(null)

  async function fetchZipSettings() {
    loading.value = true
    error.value = null
    try {
      const settings = await getZipSettings()
      isUseTurboZip.value = settings.isUseTurboZip
      isEncryptZippedFile.value = settings.isEncryptZippedFile
    } catch (err) {
      error.value = toMessage(err, 'Could not load zip settings.')
    } finally {
      loading.value = false
    }
  }

  /** Applies the change immediately, rolling back if the save fails. */
  async function update(next: ZipSettings) {
    const previous: ZipSettings = {
      isUseTurboZip: isUseTurboZip.value,
      isEncryptZippedFile: isEncryptZippedFile.value,
    }
    isUseTurboZip.value = next.isUseTurboZip
    isEncryptZippedFile.value = next.isEncryptZippedFile

    saving.value = true
    error.value = null
    try {
      const settings = await updateZipSettings(next)
      isUseTurboZip.value = settings.isUseTurboZip
      isEncryptZippedFile.value = settings.isEncryptZippedFile
    } catch (err) {
      isUseTurboZip.value = previous.isUseTurboZip
      isEncryptZippedFile.value = previous.isEncryptZippedFile
      error.value = toMessage(err, 'Could not update zip settings.')
    } finally {
      saving.value = false
    }
  }

  function setTurboZip(value: boolean) {
    return update({ isUseTurboZip: value, isEncryptZippedFile: isEncryptZippedFile.value })
  }

  function setEncryptZippedFile(value: boolean) {
    return update({ isUseTurboZip: isUseTurboZip.value, isEncryptZippedFile: value })
  }

  return {
    isUseTurboZip,
    isEncryptZippedFile,
    loading,
    saving,
    error,
    fetchZipSettings,
    setTurboZip,
    setEncryptZippedFile,
  }
})
