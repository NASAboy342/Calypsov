<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLogsStore } from '@/stores/logs'
import { fuzzyMatch } from '@/utils/fuzzyMatch'
import IconLogs from '@/components/icons/IconLogs.vue'
import IconClipboard from '@/components/icons/IconClipboard.vue'

const logs = useLogsStore()
const search = ref('')
const copied = ref(false)

onMounted(() => {
  logs.fetchFiles()
})

const lines = computed(() => (logs.content.length ? logs.content.split('\n') : []))
const filteredLines = computed(() => lines.value.filter((line) => fuzzyMatch(search.value, line)))

function formatModified(iso: string): string {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}

async function handleCopy() {
  const text = filteredLines.value.join('\n')
  try {
    await navigator.clipboard.writeText(text)
    copied.value = true
    window.setTimeout(() => (copied.value = false), 1500)
  } catch {
    // Clipboard access denied/unavailable — nothing more we can do here.
  }
}
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
            <span class="text-xs text-neutral-500">{{ filteredLines.length }} / {{ lines.length }} lines</span>
            <button
              type="button"
              :disabled="logs.loadingContent || filteredLines.length === 0"
              class="flex h-9 items-center gap-2 rounded-lg border border-white/10 px-3 text-sm text-neutral-300 transition-colors hover:bg-white/5 disabled:cursor-not-allowed disabled:opacity-50"
              @click="handleCopy"
            >
              <IconClipboard class="h-4 w-4" />
              {{ copied ? 'Copied!' : 'Copy' }}
            </button>
          </div>

          <p v-if="logs.loadingContent" class="text-sm text-neutral-500">Loading…</p>
          <p v-else-if="logs.contentError" class="text-sm text-red-400">{{ logs.contentError }}</p>
          <p v-else-if="lines.length === 0" class="text-sm text-neutral-500">This log file is empty.</p>
          <p v-else-if="filteredLines.length === 0" class="text-sm text-neutral-500">
            No lines match "{{ search }}".
          </p>
          <pre
            v-else
            class="max-h-[32rem] overflow-auto rounded-lg bg-neutral-950 p-4 font-mono text-xs leading-relaxed whitespace-pre-wrap text-neutral-300"
            >{{ filteredLines.join('\n') }}</pre
          >
        </template>
      </section>
    </div>
  </div>
</template>
