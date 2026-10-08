<script setup lang="ts">
import { computed } from 'vue'
import type { EncryptionProgress, ZipThreadProgress } from '@/services/encryptionApi'
import IconSpinner from '@/components/icons/IconSpinner.vue'

const props = defineProps<{
  progress: EncryptionProgress | null
  /** e.g. "Zipping" or "Restoring" — shown as the card's status label. */
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

// currentItem is a full path, which can easily be far longer than this card is wide. Splitting
// off the filename and rendering it on its own line — never truncated — means the one piece of
// text that actually identifies "what's happening" is always fully readable without hovering;
// the (less critical, often much longer) directory is truncated separately below it instead of
// fighting the filename for space.
const fileName = computed(() => {
  const path = props.progress?.currentItem
  if (!path) return null
  return path.slice(path.lastIndexOf('/') + 1)
})

const directoryPath = computed(() => {
  const path = props.progress?.currentItem
  const lastSlash = path?.lastIndexOf('/') ?? -1
  return lastSlash > 0 ? path!.slice(0, lastSlash) : null
})

// Non-empty only while Turbo Zip is splitting a category across multiple worker threads.
const threads = computed(() => props.progress?.threads ?? [])

function threadPercent(thread: ZipThreadProgress): number {
  if (thread.total <= 0) return 0
  return Math.min(100, Math.round((thread.completed / thread.total) * 100))
}
</script>

<template>
  <div
    class="w-80 max-w-full rounded-2xl border border-amber-400/25 bg-gradient-to-b from-neutral-900 to-neutral-900/60 p-4 shadow-[0_0_35px_-12px_rgba(251,191,36,0.5)]"
  >
    <div class="flex items-center justify-between gap-2">
      <span class="flex items-center gap-1.5 text-[11px] font-bold tracking-wider text-amber-400 uppercase">
        <IconSpinner class="h-3 w-3 animate-spin" />
        {{ actionLabel }}
      </span>
      <span v-if="isDeterminate" class="text-sm font-bold text-amber-300">{{ percent }}%</span>
    </div>

    <p v-if="fileName" class="mt-2 leading-snug font-semibold break-words text-[15px] text-neutral-50">
      {{ fileName }}
    </p>
    <p v-else class="mt-2 leading-snug font-semibold text-[15px] text-neutral-400">Preparing…</p>

    <p
      v-if="directoryPath"
      dir="rtl"
      class="truncate text-left text-xs text-neutral-500"
      :title="progress?.currentItem ?? undefined"
    >
      {{ directoryPath }}
    </p>

    <div
      class="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-neutral-800"
      role="progressbar"
      :aria-valuenow="isDeterminate ? percent : undefined"
      aria-valuemin="0"
      aria-valuemax="100"
      aria-label="Encryption progress"
    >
      <div
        class="h-full rounded-full bg-gradient-to-r from-amber-500 to-amber-300 shadow-[0_0_12px_rgba(251,191,36,0.7)] transition-[width] duration-200"
        :class="{ 'animate-pulse': !isDeterminate }"
        :style="{ width: `${isDeterminate ? percent : 30}%` }"
      />
    </div>

    <p v-if="isDeterminate" class="mt-1.5 text-right text-[11px] font-medium text-neutral-500">
      {{ progress!.completed }} / {{ progress!.total }} items
    </p>

    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div v-if="threads.length" class="mt-3 flex flex-col gap-1.5 border-t border-white/10 pt-3">
        <p class="text-[10px] font-semibold tracking-wide text-teal-400 uppercase">
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
    </Transition>
  </div>
</template>
