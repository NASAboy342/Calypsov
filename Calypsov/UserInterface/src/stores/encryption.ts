import { ref, watch } from 'vue'
import { defineStore } from 'pinia'

const STORAGE_KEY = 'calypsov:encryption-enabled'

function loadEnabled(): boolean {
  if (typeof localStorage === 'undefined') return false
  return localStorage.getItem(STORAGE_KEY) === 'true'
}

export const useEncryptionStore = defineStore('encryption', () => {
  const enabled = ref(loadEnabled())

  watch(enabled, (value) => {
    localStorage.setItem(STORAGE_KEY, String(value))
  })

  function toggle() {
    enabled.value = !enabled.value
  }

  return { enabled, toggle }
})
