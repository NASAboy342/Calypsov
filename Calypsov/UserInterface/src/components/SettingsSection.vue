<script setup lang="ts">
import { ref, watch } from 'vue'
import IconChevronDown from '@/components/icons/IconChevronDown.vue'

const props = withDefaults(
  defineProps<{
    title: string
    description?: string
    badge?: string | number
    /** Unique key used to persist this section's open/closed state across reloads. */
    storageKey: string
    defaultOpen?: boolean
  }>(),
  { defaultOpen: true },
)

const STORAGE_PREFIX = 'calypsov:settings-section:'

function readStoredOpen(): boolean {
  try {
    const stored = localStorage.getItem(STORAGE_PREFIX + props.storageKey)
    return stored === null ? props.defaultOpen : stored === 'open'
  } catch {
    return props.defaultOpen
  }
}

const open = ref(readStoredOpen())

watch(open, (value) => {
  try {
    localStorage.setItem(STORAGE_PREFIX + props.storageKey, value ? 'open' : 'closed')
  } catch {
    // localStorage unavailable — collapse state just won't persist across reloads.
  }
})

function toggle() {
  open.value = !open.value
}
</script>

<template>
  <section class="rounded-2xl border border-white/10 bg-neutral-900">
    <button
      type="button"
      class="flex w-full items-center gap-3 rounded-2xl px-5 py-4 text-left transition-colors hover:bg-white/5"
      :aria-expanded="open"
      @click="toggle"
    >
      <slot name="icon" />
      <div class="flex-1">
        <h2 class="flex items-center gap-2 text-[15px] font-semibold text-neutral-100">
          {{ title }}
          <span
            v-if="badge !== undefined"
            class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300"
          >
            {{ badge }}
          </span>
        </h2>
        <p v-if="description" class="mt-1 text-xs text-neutral-500">{{ description }}</p>
      </div>
      <IconChevronDown
        class="h-4.5 w-4.5 shrink-0 text-neutral-500 transition-transform duration-200"
        :class="{ '-rotate-180': open }"
      />
    </button>

    <div v-if="open" class="border-t border-white/10 p-5">
      <slot />
    </div>
  </section>
</template>
