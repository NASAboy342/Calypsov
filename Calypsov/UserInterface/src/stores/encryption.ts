import { ref } from 'vue'
import { defineStore } from 'pinia'
import { getStatus, toggleStatus } from '@/services/encryptionApi'

export const useEncryptionStore = defineStore('encryption', () => {
  const enabled = ref(false)
  const loading = ref(true)

  async function fetchStatus() {
    loading.value = true
    try {
      enabled.value = (await getStatus()).enabled
    } finally {
      loading.value = false
    }
  }

  async function toggle() {
    enabled.value = (await toggleStatus()).enabled
  }

  return { enabled, loading, fetchStatus, toggle }
})
