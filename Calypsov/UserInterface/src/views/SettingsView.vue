<script setup lang="ts">
import { ref } from 'vue'
import { useTargetsStore, type TargetCategory } from '@/stores/targets'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconFile from '@/components/icons/IconFile.vue'
import IconTrash from '@/components/icons/IconTrash.vue'

const targets = useTargetsStore()

const category = ref<TargetCategory>('folder')
const path = ref('')
const error = ref('')

async function handleAdd() {
  const result = await targets.add(category.value, path.value)
  if (result) {
    error.value = result
    return
  }
  error.value = ''
  path.value = ''
}
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <header class="mb-8">
      <p class="mb-2 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">Configuration</p>
      <h1 class="text-2xl font-bold text-neutral-50">Settings</h1>
      <p class="mt-1 text-sm text-neutral-400">Choose which folders and files should be encrypted.</p>
    </header>

    <form
      class="mb-3 flex flex-wrap items-end gap-4 rounded-2xl border border-white/10 bg-neutral-900 p-5"
      @submit.prevent="handleAdd"
    >
      <div class="flex flex-col gap-1.5">
        <label for="category" class="text-xs font-semibold text-neutral-400">Category</label>
        <select
          id="category"
          v-model="category"
          class="rounded-lg border border-white/10 bg-neutral-950 px-3 py-2.5 text-sm text-neutral-100 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
        >
          <option value="folder">Folder</option>
          <option value="file">File</option>
        </select>
      </div>

      <div class="flex min-w-55 flex-1 flex-col gap-1.5">
        <label for="path" class="text-xs font-semibold text-neutral-400">
          {{ category === 'folder' ? 'Folder path' : 'File path' }}
        </label>
        <input
          id="path"
          v-model="path"
          type="text"
          :placeholder="
            category === 'folder'
              ? '/Users/you/Documents/Projects'
              : '/Users/you/Documents/report.pdf'
          "
          class="rounded-lg border border-white/10 bg-neutral-950 px-3 py-2.5 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
        />
      </div>

      <button
        type="submit"
        class="rounded-full bg-teal-400 px-6 py-2.5 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
      >
        Add
      </button>
    </form>
    <p v-if="error" class="mb-5 text-sm text-red-400">{{ error }}</p>

    <div class="mt-6 grid gap-5">
      <section class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <h2 class="mb-3 flex items-center gap-2 text-[15px] font-semibold text-neutral-100">
          Folders
          <span class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300">
            {{ targets.folders.length }}
          </span>
        </h2>
        <ul v-if="targets.folders.length" class="flex flex-col gap-2">
          <li
            v-for="t in targets.folders"
            :key="t.id"
            class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
          >
            <IconFolder class="h-[18px] w-[18px] shrink-0 text-neutral-500" />
            <span class="flex-1 text-sm break-all text-neutral-200">{{ t.path }}</span>
            <button
              type="button"
              class="flex h-7 w-7 items-center justify-center rounded-lg text-neutral-500 hover:bg-red-400/10 hover:text-red-400"
              aria-label="Remove"
              @click="targets.remove(t.id)"
            >
              <IconTrash class="h-4 w-4" />
            </button>
          </li>
        </ul>
        <p v-else class="text-sm text-neutral-500">No folders added yet.</p>
      </section>

      <section class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <h2 class="mb-3 flex items-center gap-2 text-[15px] font-semibold text-neutral-100">
          Files
          <span class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300">
            {{ targets.files.length }}
          </span>
        </h2>
        <ul v-if="targets.files.length" class="flex flex-col gap-2">
          <li
            v-for="t in targets.files"
            :key="t.id"
            class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
          >
            <IconFile class="h-[18px] w-[18px] shrink-0 text-neutral-500" />
            <span class="flex-1 text-sm break-all text-neutral-200">{{ t.path }}</span>
            <button
              type="button"
              class="flex h-7 w-7 items-center justify-center rounded-lg text-neutral-500 hover:bg-red-400/10 hover:text-red-400"
              aria-label="Remove"
              @click="targets.remove(t.id)"
            >
              <IconTrash class="h-4 w-4" />
            </button>
          </li>
        </ul>
        <p v-else class="text-sm text-neutral-500">No files added yet.</p>
      </section>
    </div>
  </div>
</template>
