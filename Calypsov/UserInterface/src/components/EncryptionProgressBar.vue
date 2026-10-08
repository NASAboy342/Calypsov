<script setup lang="ts">
import { computed } from 'vue'
import type { EncryptionProgress, ZipThreadProgress } from '@/services/encryptionApi'

const props = defineProps<{
  progress: EncryptionProgress | null
  /** e.g. "Zipping" or "Restoring" — prefixed to the current item's path. */
  actionLabel: string
}>()

// The very first poll or two can land before the backend has computed a total (or between
// toggles, before the next run's first progress update lands) — treat that as indeterminate
// rather than showing a misleading 0%.
const isDeterminate = computed(() => (props.progress?.total ?? 0) > 0)

const percent = computed(() => {
  if (!isDeterminate.value) return 0
  return Math.min(100, Math.round((props.progress!.completed / props.progress!.total) * 100))
})

const message = computed(() => {
  if (!props.progress?.currentItem) return 'Preparing…'
  return `${props.actionLabel} ${props.progress.currentItem}`
})

// Non-empty only while Turbo Zip is splitting a category across multiple worker threads.
const threads = computed(() => props.progress?.threads ?? [])

function threadPercent(thread: ZipThreadProgress): number {
  if (thread.total <= 0) return 0
  return Math.min(100, Math.round((thread.completed / thread.total) * 100))
}
</script>

<template>
  <div class="w-64 max-w-full">
    <p class="mb-2 truncate text-xs text-neutral-400" :title="progress?.currentItem ?? undefined">
      {{ message }}
    </p>

    <div
      class="h-2 w-full overflow-hidden rounded-full bg-neutral-800"
      role="progressbar"
      :aria-valuenow="isDeterminate ? percent : undefined"
      aria-valuemin="0"
      aria-valuemax="100"
      aria-label="Encryption progress"
    >
      <div
        class="h-full rounded-full bg-amber-400 shadow-[0_0_10px_rgba(251,191,36,0.6)] transition-[width] duration-200"
        :class="{ 'animate-pulse': !isDeterminate }"
        :style="{ width: `${isDeterminate ? percent : 30}%` }"
      />
    </div>

    <p v-if="isDeterminate" class="mt-1.5 text-right text-xs font-semibold text-amber-300">
      {{ percent }}% · {{ progress!.completed }}/{{ progress!.total }}
    </p>

    <div v-if="threads.length" class="mt-3 flex flex-col gap-1.5 border-t border-white/10 pt-3">
      <p class="text-[10px] font-semibold tracking-wide text-neutral-500 uppercase">
        Turbo Zip · {{ threads.length }} threads
      </p>
      <div v-for="thread in threads" :key="thread.threadIndex" class="flex items-center gap-2">
        <span class="w-6 shrink-0 text-[10px] font-semibold text-neutral-500">#{{ thread.threadIndex + 1 }}</span>
        <div class="h-1.5 flex-1 overflow-hidden rounded-full bg-neutral-800">
          <div
            class="h-full rounded-full bg-teal-400/80 transition-[width] duration-200"
            :style="{ width: `${threadPercent(thread)}%` }"
          />
        </div>
        <span class="w-8 shrink-0 text-right text-[10px] text-neutral-500">{{ threadPercent(thread) }}%</span>
      </div>
    </div>
  </div>
</template>
