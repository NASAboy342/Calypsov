import { computed, ref, watch } from 'vue'
import { defineStore } from 'pinia'

export type TargetCategory = 'folder' | 'file'

export interface EncryptionTarget {
  id: string
  category: TargetCategory
  path: string
}

const STORAGE_KEY = 'calypsov:encryption-targets'

function loadTargets(): EncryptionTarget[] {
  if (typeof localStorage === 'undefined') return []
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as EncryptionTarget[]) : []
  } catch {
    return []
  }
}

export const useTargetsStore = defineStore('targets', () => {
  const items = ref<EncryptionTarget[]>(loadTargets())

  watch(
    items,
    (value) => {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(value))
    },
    { deep: true },
  )

  const folders = computed(() => items.value.filter((t) => t.category === 'folder'))
  const files = computed(() => items.value.filter((t) => t.category === 'file'))
  const count = computed(() => items.value.length)

  /** Returns an error message on failure, or null on success. */
  function add(category: TargetCategory, path: string): string | null {
    const trimmed = path.trim()
    if (!trimmed) return 'Enter a path before adding.'

    const duplicate = items.value.some((t) => t.category === category && t.path === trimmed)
    if (duplicate) return 'That path is already in the list.'

    items.value.push({ id: crypto.randomUUID(), category, path: trimmed })
    return null
  }

  function remove(id: string) {
    items.value = items.value.filter((t) => t.id !== id)
  }

  return { items, folders, files, count, add, remove }
})
