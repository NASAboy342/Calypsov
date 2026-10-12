<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLogsStore } from '@/stores/logs'
import { fuzzyMatchIndices } from '@/utils/fuzzyMatch'
import IconLogs from '@/components/icons/IconLogs.vue'
import IconClipboard from '@/components/icons/IconClipboard.vue'
import IconTrash from '@/components/icons/IconTrash.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

interface LineSegment {
  text: string
  highlighted: boolean
}

interface DisplayLine {
  text: string
  segments: LineSegment[]
}

const logs = useLogsStore()
const search = ref('')
const copied = ref(false)
const showClearConfirm = ref(false)
const clearing = ref(false)
const clearError = ref('')

onMounted(() => {
  logs.fetchFiles()
})

const lines = computed(() => (logs.content.length ? logs.content.split('\n') : []))

/**
 * Filters to matching lines and, for each, splits it into highlighted/plain segments in one pass
 * (rather than filtering with fuzzyMatch and then separately re-running the match per line to find
 * what to highlight). A fuzzy match isn't one contiguous substring, so consecutive matched
 * character indices are merged into a single highlighted run instead of wrapping every character
 * in its own span.
 */
const displayLines = computed<DisplayLine[]>(() =>
  lines.value
    .map((text) => ({ text, indices: fuzzyMatchIndices(search.value, text) }))
    .filter((line): line is { text: string; indices: number[] } => line.indices !== null)
    .map(({ text, indices }) => ({ text, segments: toSegments(text, indices) })),
)

function toSegments(line: string, matchedIndices: number[]): LineSegment[] {
  if (matchedIndices.length === 0) return [{ text: line, highlighted: false }]

  const segments: LineSegment[] = []
  let cursor = 0

  for (let i = 0; i < matchedIndices.length; i++) {
    const start = matchedIndices[i]
    let end = start
    while (i + 1 < matchedIndices.length && matchedIndices[i + 1] === end + 1) {
      end = matchedIndices[++i]
    }

    if (start > cursor) segments.push({ text: line.slice(cursor, start), highlighted: false })
    segments.push({ text: line.slice(start, end + 1), highlighted: true })
    cursor = end + 1
  }

  if (cursor < line.length) segments.push({ text: line.slice(cursor), highlighted: false })

  return segments
}

function formatModified(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

async function handleCopy() {
  const text = displayLines.value.map((line) => line.text).join('\n')
  try {
    await navigator.clipboard.writeText(text)
    copied.value = true
    window.setTimeout(() => (copied.value = false), 1500)
  } catch {
    // Clipboard access denied/unavailable — nothing more we can do here.
  }
}

function requestClearFile() {
  if (!logs.selectedFileName) return
  clearError.value = ''
  showClearConfirm.value = true
}

async function confirmClearFile() {
  showClearConfirm.value = false
  if (!logs.selectedFileName) return

  clearing.value = true
  clearError.value = ''
  try {
    await logs.clearFile(logs.selectedFileName)
  } catch (err) {
    clearError.value = err instanceof Error ? err.message : 'Could not clear this log file.'
  } finally {
    clearing.value = false
  }
}

function cancelClearFile() {
  showClearConfirm.value = false
}

const clearConfirmMessage = computed(
  () =>
    `This will permanently delete all contents of "${logs.selectedFileName}". This can't be undone.`,
)
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <header class="mb-8">
      <p class="mb-2 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">Diagnostics</p>
      <h1 class="text-2xl font-bold text-neutral-50">Logs</h1>
      <p class="mt-1 text-sm text-neutral-400">
        Review recorded app activity and errors. Up to 4 days of logs are kept.
      </p>
    </header>

    <div class="grid gap-5 md:grid-cols-[16rem_1fr]">
      <section class="rounded-2xl border border-white/10 bg-neutral-900 p-4">
        <h2 class="mb-3 text-sm font-semibold text-neutral-100">Log Files</h2>

        <p v-if="logs.loadingFiles" class="text-sm text-neutral-500">Loading…</p>
        <p v-else-if="logs.filesError" class="text-sm text-red-400">{{ logs.filesError }}</p>
        <p v-else-if="logs.files.length === 0" class="text-sm text-neutral-500">
          No log files yet. Turn on "Record Logs" in Settings to start recording.
        </p>
        <ul v-else class="flex max-h-[32rem] flex-col gap-1 overflow-y-auto pr-1">
          <li v-for="file in logs.files" :key="file.fileName">
            <button
              type="button"
              class="flex w-full flex-col items-start gap-0.5 rounded-lg px-3 py-2 text-left transition-colors hover:bg-white/5"
              :class="
                logs.selectedFileName === file.fileName
                  ? 'bg-teal-400/10 text-teal-300'
                  : 'text-neutral-300'
              "
              @click="logs.selectFile(file.fileName)"
            >
              <span class="text-sm font-medium">{{ file.fileName }}</span>
              <span class="text-xs text-neutral-500">{{ formatModified(file.modifiedAtUtc) }}</span>
            </button>
          </li>
        </ul>
      </section>

      <section class="flex min-w-0 flex-col rounded-2xl border border-white/10 bg-neutral-900 p-4">
        <template v-if="!logs.selectedFileName">
          <div class="flex flex-1 flex-col items-center justify-center gap-3 py-16 text-center">
            <IconLogs class="h-8 w-8 text-neutral-600" />
            <p class="text-sm text-neutral-500">Choose a log file to view its contents.</p>
          </div>
        </template>

        <template v-else>
          <div class="mb-3 flex flex-wrap items-center gap-3">
            <input
              v-model="search"
              type="text"
              placeholder="Fuzzy search within this file…"
              class="h-9 min-w-0 flex-1 rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
            />
            <span class="text-xs text-neutral-500">{{ displayLines.length }} / {{ lines.length }} lines</span>
            <button
              type="button"
              :disabled="logs.loadingContent || displayLines.length === 0"
              class="flex h-9 items-center gap-2 rounded-lg border border-white/10 px-3 text-sm text-neutral-300 transition-colors hover:bg-white/5 disabled:cursor-not-allowed disabled:opacity-50"
              @click="handleCopy"
            >
              <IconClipboard class="h-4 w-4" />
              {{ copied ? 'Copied!' : 'Copy' }}
            </button>
            <button
              type="button"
              :disabled="logs.loadingContent || clearing || lines.length === 0"
              class="flex h-9 items-center gap-2 rounded-lg border border-red-400/20 px-3 text-sm text-red-400 transition-colors hover:bg-red-400/10 disabled:cursor-not-allowed disabled:opacity-50"
              @click="requestClearFile"
            >
              <IconTrash class="h-4 w-4" />
              Clear
            </button>
          </div>

          <p v-if="clearError" class="mb-3 text-sm text-red-400">{{ clearError }}</p>

          <p v-if="logs.loadingContent" class="text-sm text-neutral-500">Loading…</p>
          <p v-else-if="logs.contentError" class="text-sm text-red-400">{{ logs.contentError }}</p>
          <p v-else-if="lines.length === 0" class="text-sm text-neutral-500">This log file is empty.</p>
          <p v-else-if="displayLines.length === 0" class="text-sm text-neutral-500">
            No lines match "{{ search }}".
          </p>
          <div v-else class="max-h-[32rem] overflow-auto rounded-lg bg-neutral-950 py-2 font-mono text-xs leading-relaxed">
            <div
              v-for="(line, index) in displayLines"
              :key="index"
              class="px-4 py-0.5 whitespace-pre-wrap"
              :class="index % 2 === 0 ? 'bg-transparent' : 'bg-white/[0.03]'"
            >
              <template v-for="(segment, segIndex) in line.segments" :key="segIndex">
                <mark
                  v-if="segment.highlighted"
                  class="rounded-sm bg-teal-400/25 text-teal-200"
                  >{{ segment.text }}</mark
                >
                <span v-else class="text-neutral-300">{{ segment.text }}</span>
              </template>
            </div>
          </div>
        </template>
      </section>
    </div>

    <ConfirmDialog
      :open="showClearConfirm"
      title="Clear this log file?"
      :message="clearConfirmMessage"
      confirm-label="Clear"
      danger
      @confirm="confirmClearFile"
      @cancel="cancelClearFile"
    />
  </div>
</template>
