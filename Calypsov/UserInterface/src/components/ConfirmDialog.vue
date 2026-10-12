<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'

withDefaults(
  defineProps<{
    open: boolean
    title: string
    message: string
    confirmLabel?: string
    cancelLabel?: string
    /** Styles the confirm button red instead of teal — for destructive actions. */
    danger?: boolean
  }>(),
  { confirmLabel: 'Confirm', cancelLabel: 'Cancel', danger: false },
)

const emit = defineEmits<{ confirm: []; cancel: [] }>()

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') emit('cancel')
}

onMounted(() => document.addEventListener('keydown', handleKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', handleKeydown))
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
        @click.self="emit('cancel')"
      >
        <div class="w-full max-w-sm rounded-2xl border border-white/10 bg-neutral-900 p-5 shadow-xl">
          <h2 class="text-sm font-semibold text-neutral-100">{{ title }}</h2>
          <p class="mt-2 text-sm text-neutral-400">{{ message }}</p>
          <div class="mt-5 flex justify-end gap-2.5">
            <button
              type="button"
              class="h-9 rounded-lg px-4 text-sm font-medium text-neutral-300 transition-colors hover:bg-white/5"
              @click="emit('cancel')"
            >
              {{ cancelLabel }}
            </button>
            <button
              type="button"
              class="h-9 rounded-lg px-4 text-sm font-semibold transition-colors"
              :class="
                danger
                  ? 'bg-red-500 text-white hover:bg-red-400'
                  : 'bg-teal-400 text-neutral-950 hover:bg-teal-300'
              "
              @click="emit('confirm')"
            >
              {{ confirmLabel }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
