<script setup lang="ts">
import { ref } from 'vue'
import { useTargetsStore, EnumTargetCategory } from '@/stores/targets'
import { pickFiles, pickFolders } from '@/services/dialogApi'
import { BrowserType } from '@/services/browsersApi'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconFile from '@/components/icons/IconFile.vue'
import IconTrash from '@/components/icons/IconTrash.vue'
import IconClipboard from '@/components/icons/IconClipboard.vue'
import IconFolderOpen from '@/components/icons/IconFolderOpen.vue'
import BrowserProfileCard from '@/components/BrowserProfileCard.vue'
import SettingsSection from '@/components/SettingsSection.vue'
import ToggleSwitch from '@/components/ToggleSwitch.vue'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useLogSettingsStore } from '@/stores/logSettings'

const targets = useTargetsStore()
const zipSettings = useZipSettingsStore()
const logSettings = useLogSettingsStore()

const category = ref<EnumTargetCategory>(EnumTargetCategory.Folder)
const path = ref('')
const error = ref('')
const browsing = ref(false)

async function handleAdd() {
  const result = await targets.add(category.value, path.value)
  if (result) {
    error.value = result
    return
  }
  error.value = ''
  path.value = ''
}

async function handlePaste() {
  try {
    const text = (await navigator.clipboard.readText()).trim()
    if (text) {
      path.value = text
      error.value = ''
    }
  } catch {
    error.value = 'Could not read from the clipboard.'
  }
}

/**
 * Browsing lets the user multi-select folders/files in one go, so each picked path is
 * added directly (like the manual Add button, just run once per path) rather than
 * being funneled through the single-path text input.
 */
async function handleBrowse() {
  browsing.value = true
  try {
    const result =
      category.value === EnumTargetCategory.Folder ? await pickFolders() : await pickFiles()
    if (result.paths.length === 0) return

    const failures: string[] = []
    for (const picked of result.paths) {
      const addError = await targets.add(category.value, picked)
      if (addError) failures.push(`${picked} — ${addError}`)
    }
    error.value = failures.join(' ')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not open the picker.'
  } finally {
    browsing.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <header class="mb-8">
      <p class="mb-2 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">Configuration</p>
      <h1 class="text-2xl font-bold text-neutral-50">Settings</h1>
      <p class="mt-1 text-sm text-neutral-400">Choose which folders and files should be encrypted.</p>
    </header>

    <div class="flex flex-col gap-5">
      <SettingsSection
        title="Target Folders & Files"
        description="Choose which folders and files should be encrypted."
        :badge="targets.count"
        storage-key="targets"
      >
        <form
          class="mb-5 flex flex-wrap items-end gap-4 rounded-xl border border-white/10 bg-neutral-950/60 p-4"
          @submit.prevent="handleAdd"
        >
          <div class="flex w-36 flex-col gap-1.5">
            <label for="category" class="text-xs font-semibold text-neutral-400">Category</label>
            <select
              id="category"
              v-model="category"
              class="h-10.5 rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
            >
              <option :value="EnumTargetCategory.Folder">Folder</option>
              <option :value="EnumTargetCategory.File">File</option>
            </select>
          </div>

          <div class="flex min-w-70 flex-1 flex-col gap-1.5">
            <label for="path" class="text-xs font-semibold text-neutral-400">
              {{ category === EnumTargetCategory.Folder ? 'Folder path' : 'File path' }}
            </label>
            <div class="relative">
              <input
                id="path"
                v-model="path"
                type="text"
                :placeholder="
                  category === EnumTargetCategory.Folder
                    ? '/Users/you/Documents/Projects'
                    : '/Users/you/Documents/report.pdf'
                "
                class="h-10.5 w-full rounded-lg border border-white/10 bg-neutral-950 px-3 pr-20 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
              />
              <div class="absolute inset-y-0 right-1.5 flex items-center gap-1">
                <button
                  type="button"
                  title="Paste from clipboard"
                  aria-label="Paste from clipboard"
                  class="flex h-7 w-7 items-center justify-center rounded-md text-neutral-500 transition-colors hover:bg-white/10 hover:text-teal-300"
                  @click="handlePaste"
                >
                  <IconClipboard class="h-4 w-4" />
                </button>
                <button
                  type="button"
                  title="Browse… (pick multiple, added instantly)"
                  aria-label="Browse for one or more paths"
                  :disabled="browsing"
                  class="flex h-7 w-7 items-center justify-center rounded-md text-neutral-500 transition-colors hover:bg-white/10 hover:text-teal-300 disabled:opacity-50"
                  @click="handleBrowse"
                >
                  <IconFolderOpen class="h-4 w-4" />
                </button>
              </div>
            </div>
          </div>

          <button
            type="submit"
            class="h-10.5 rounded-lg bg-teal-400 px-6 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
          >
            Add
          </button>

          <p v-if="error" class="w-full text-sm text-red-400">{{ error }}</p>
        </form>

        <div class="grid gap-5 sm:grid-cols-2">
          <section class="rounded-xl border border-white/10 bg-neutral-950/60 p-4">
            <h3 class="mb-3 flex items-center gap-2 text-sm font-semibold text-neutral-100">
              Folders
              <span class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300">
                {{ targets.folders.length }}
              </span>
            </h3>
            <TransitionGroup
              v-if="targets.folders.length"
              tag="ul"
              class="flex max-h-96 flex-col gap-2 overflow-y-auto pr-1"
              enter-active-class="transition duration-200 ease-out"
              enter-from-class="opacity-0 -translate-x-2"
              enter-to-class="opacity-100 translate-x-0"
              leave-active-class="transition duration-150 ease-in"
              leave-from-class="opacity-100"
              leave-to-class="opacity-0"
              move-class="transition-transform duration-200 ease-out"
            >
              <li
                v-for="t in targets.folders"
                :key="t.id"
                class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
              >
                <IconFolder class="h-4.5 w-4.5 shrink-0 text-neutral-500" />
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
            </TransitionGroup>
            <p v-else class="text-sm text-neutral-500">No folders added yet.</p>
          </section>

          <section class="rounded-xl border border-white/10 bg-neutral-950/60 p-4">
            <h3 class="mb-3 flex items-center gap-2 text-sm font-semibold text-neutral-100">
              Files
              <span class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300">
                {{ targets.files.length }}
              </span>
            </h3>
            <TransitionGroup
              v-if="targets.files.length"
              tag="ul"
              class="flex max-h-96 flex-col gap-2 overflow-y-auto pr-1"
              enter-active-class="transition duration-200 ease-out"
              enter-from-class="opacity-0 -translate-x-2"
              enter-to-class="opacity-100 translate-x-0"
              leave-active-class="transition duration-150 ease-in"
              leave-from-class="opacity-100"
              leave-to-class="opacity-0"
              move-class="transition-transform duration-200 ease-out"
            >
              <li
                v-for="t in targets.files"
                :key="t.id"
                class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
              >
                <IconFile class="h-4.5 w-4.5 shrink-0 text-neutral-500" />
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
            </TransitionGroup>
            <p v-else class="text-sm text-neutral-500">No files added yet.</p>
          </section>
        </div>
      </SettingsSection>

      <SettingsSection
        title="Encryption Options"
        description="Fine-tune how your zip archives are built."
        storage-key="zip-settings"
      >
        <div class="flex flex-col divide-y divide-white/10">
          <div class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0">
            <div>
              <p class="text-sm font-semibold text-neutral-100">Turbo Zip</p>
              <p class="mt-1 max-w-lg text-xs text-neutral-500">
                Split the zip job across your CPU's available threads once 20+ folders are targeted —
                finishes much faster, but runs the CPU hotter and uses more power.
              </p>
            </div>
            <ToggleSwitch
              :model-value="zipSettings.isUseTurboZip"
              :disabled="zipSettings.saving"
              aria-label="Toggle Turbo Zip"
              @update:model-value="zipSettings.setTurboZip"
            />
          </div>

          <div class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0">
            <div>
              <p class="text-sm font-semibold text-neutral-100">Encrypt Zipped Files</p>
              <p class="mt-1 max-w-lg text-xs text-neutral-500">
                Adds a further layer of encryption to the zip output, making it unreadable without this
                app — more secure, at the cost of a longer zip process.
              </p>
            </div>
            <ToggleSwitch
              :model-value="zipSettings.isEncryptZippedFile"
              :disabled="zipSettings.saving"
              aria-label="Toggle Encrypt Zipped Files"
              @update:model-value="zipSettings.setEncryptZippedFile"
            />
          </div>
        </div>

        <p v-if="zipSettings.error" class="mt-3 text-xs text-red-400">{{ zipSettings.error }}</p>
      </SettingsSection>

      <SettingsSection
        title="Browser Profiles"
        description="Pick which browser profile's data should be included."
        storage-key="browser-profiles"
      >
        <div class="grid gap-5 sm:grid-cols-2">
          <BrowserProfileCard :browser="BrowserType.Edge" />
          <BrowserProfileCard :browser="BrowserType.Chrome" />
        </div>
      </SettingsSection>

      <SettingsSection
        title="Logging"
        description="Record app activity and errors to a log file you can review on the Logs page."
        storage-key="logging"
      >
        <div class="flex items-start justify-between gap-4">
          <div>
            <p class="text-sm font-semibold text-neutral-100">Record Logs</p>
            <p class="mt-1 max-w-lg text-xs text-neutral-500">
              When on, API activity and errors are queued and written to a dated file under Logs — kept
              for the last 4 days. When off, nothing is recorded and no background work runs.
            </p>
          </div>
          <ToggleSwitch
            :model-value="logSettings.isRecordLog"
            :disabled="logSettings.saving"
            aria-label="Toggle log recording"
            @update:model-value="logSettings.setRecordLog"
          />
        </div>

        <p v-if="logSettings.error" class="mt-3 text-xs text-red-400">{{ logSettings.error }}</p>
      </SettingsSection>
    </div>
  </div>
</template>
