import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getAppSettingEncryption, updateAppSettingEncryption } from '@/services/encryptionApi'

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useAppSettingEncryptionStore = defineStore('appSettingEncryption', () => {
  const isEncryptAppSetting = ref(true)
  const loading = ref(true)
  /** True while the toggle change is being saved — drives the switch's disabled state. */
  const saving = ref(false)
  const error = ref<string | null>(null)

  async function fetchAppSettingEncryption() {
    loading.value = true
    error.value = null
    try {
      isEncryptAppSetting.value = (await getAppSettingEncryption()).isEncryptAppSetting
    } catch (err) {
      error.value = toMessage(err, 'Could not load the app settings encryption setting.')
    } finally {
      loading.value = false
    }
  }

  /** Applies the change immediately, rolling back if the save fails. */
  async function setEncryptAppSetting(value: boolean) {
    const previous = isEncryptAppSetting.value
    isEncryptAppSetting.value = value

    saving.value = true
    error.value = null
    try {
      isEncryptAppSetting.value = (await updateAppSettingEncryption(value)).isEncryptAppSetting
    } catch (err) {
      isEncryptAppSetting.value = previous
      error.value = toMessage(err, 'Could not update the app settings encryption setting.')
    } finally {
      saving.value = false
    }
  }

  return { isEncryptAppSetting, loading, saving, error, fetchAppSettingEncryption, setEncryptAppSetting }
})
