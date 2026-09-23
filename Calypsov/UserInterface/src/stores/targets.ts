import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  addTarget as apiAddTarget,
  getTargets,
  removeTarget as apiRemoveTarget,
  EnumTargetCategory,
  type EncryptionTarget,
} from '@/services/encryptionApi'

export type { EncryptionTarget }
export { EnumTargetCategory }

export const useTargetsStore = defineStore('targets', () => {
  const items = ref<EncryptionTarget[]>([])
  const loading = ref(true)

  async function fetchTargets() {
    loading.value = true
    try {
      items.value = await getTargets()
    } finally {
      loading.value = false
    }
  }

  const folders = computed(() => items.value.filter((t) => t.category === EnumTargetCategory.Folder))
  const files = computed(() => items.value.filter((t) => t.category === EnumTargetCategory.File))
  const count = computed(() => items.value.length)

  /** Returns an error message on failure, or null on success. */
  async function add(category: EnumTargetCategory, path: string): Promise<string | null> {
    if (!path.trim()) return 'Enter a path before adding.'

    try {
      const target = await apiAddTarget(category, path)
      items.value.push(target)
      return null
    } catch (err) {
      return err instanceof Error ? err.message : 'Could not add that path.'
    }
  }

  async function remove(id: string) {
    await apiRemoveTarget(id)
    items.value = items.value.filter((t) => t.id !== id)
  }

  return { items, folders, files, count, loading, fetchTargets, add, remove }
})
