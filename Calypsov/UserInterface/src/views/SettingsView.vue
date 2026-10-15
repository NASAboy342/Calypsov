<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useTargetsStore, EnumTargetCategory } from '@/stores/targets'
import { useEncryptionStore } from '@/stores/encryption'
import { pickFiles, pickFolders } from '@/services/dialogApi'
import { BrowserType } from '@/services/browsersApi'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconFile from '@/components/icons/IconFile.vue'
import IconTrash from '@/components/icons/IconTrash.vue'
import IconClipboard from '@/components/icons/IconClipboard.vue'
import IconFolderOpen from '@/components/icons/IconFolderOpen.vue'
import IconLock from '@/components/icons/IconLock.vue'
import BrowserProfileCard from '@/components/BrowserProfileCard.vue'
import SettingsSection from '@/components/SettingsSection.vue'
import ToggleSwitch from '@/components/ToggleSwitch.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import Checkbox from '@/components/Checkbox.vue'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useLogSettingsStore } from '@/stores/logSettings'
import { useAppSettingEncryptionStore } from '@/stores/appSettingEncryption'
import { useAppUpdateStore } from '@/stores/appUpdate'
import { fuzzyMatch } from '@/utils/fuzzyMatch'

const targets = useTargetsStore()
const zipSettings = useZipSettingsStore()
const logSettings = useLogSettingsStore()
const appSettingEncryption = useAppSettingEncryptionStore()
const appUpdate = useAppUpdateStore()
const encryption = useEncryptionStore()

onMounted(() => {
  appUpdate.checkForUpdate()
})

const folderSearch = ref('')
const fileSearch = ref('')
const filteredFolders = computed(() => targets.folders.filter((t) => fuzzyMatch(folderSearch.value, t.path)))
const filteredFiles = computed(() => targets.files.filter((t) => fuzzyMatch(fileSearch.value, t.path)))

// Multi-select for bulk delete — folders and files each get their own independent selection.
const selectedFolderIds = ref<Set<string>>(new Set())
const selectedFileIds = ref<Set<string>>(new Set())
const bulkDeleteError = ref('')

/** "Select all" only ever applies to the currently search-filtered items, not the whole list. */
const selectedVisibleFolderCount = computed(
  () => filteredFolders.value.filter((t) => selectedFolderIds.value.has(t.id)).length,
)
const allFilteredFoldersSelected = computed(
  () => filteredFolders.value.length > 0 && selectedVisibleFolderCount.value === filteredFolders.value.length,
)
const someFilteredFoldersSelected = computed(
  () => selectedVisibleFolderCount.value > 0 && !allFilteredFoldersSelected.value,
)

const selectedVisibleFileCount = computed(
  () => filteredFiles.value.filter((t) => selectedFileIds.value.has(t.id)).length,
)
const allFilteredFilesSelected = computed(
  () => filteredFiles.value.length > 0 && selectedVisibleFileCount.value === filteredFiles.value.length,
)
const someFilteredFilesSelected = computed(
  () => selectedVisibleFileCount.value > 0 && !allFilteredFilesSelected.value,
)

function toggleFolderSelection(id: string) {
  if (selectedFolderIds.value.has(id)) selectedFolderIds.value.delete(id)
  else selectedFolderIds.value.add(id)
}

function toggleSelectAllFolders() {
  if (allFilteredFoldersSelected.value) {
    for (const t of filteredFolders.value) selectedFolderIds.value.delete(t.id)
  } else {
    for (const t of filteredFolders.value) selectedFolderIds.value.add(t.id)
  }
}

function toggleFileSelection(id: string) {
  if (selectedFileIds.value.has(id)) selectedFileIds.value.delete(id)
  else selectedFileIds.value.add(id)
}

function toggleSelectAllFiles() {
  if (allFilteredFilesSelected.value) {
    for (const t of filteredFiles.value) selectedFileIds.value.delete(t.id)
  } else {
    for (const t of filteredFiles.value) selectedFileIds.value.add(t.id)
  }
}

/** Single-item removal (the existing per-row trash button) also drops the id from whichever
 * selection set it might be in, so a stale checkmark can't linger after the row is gone. */
async function removeSingleTarget(id: string) {
  await targets.remove(id)
  selectedFolderIds.value.delete(id)
  selectedFileIds.value.delete(id)
}

type PendingDelete = { noun: string; ids: string[]; onDone: () => void }
const pendingDelete = ref<PendingDelete | null>(null)

const pendingDeleteMessage = computed(() => {
  if (!pendingDelete.value) return ''
  const { noun, ids } = pendingDelete.value
  const label = ids.length === 1 ? noun : `${noun}s`
  return `This will remove ${ids.length} ${label} from your encryption targets. This can't be undone.`
})

function requestDeleteSelectedFolders() {
  if (isLocked.value || selectedFolderIds.value.size === 0) return
  pendingDelete.value = {
    noun: 'folder',
    ids: Array.from(selectedFolderIds.value),
    onDone: () => selectedFolderIds.value.clear(),
  }
}

function requestDeleteSelectedFiles() {
  if (isLocked.value || selectedFileIds.value.size === 0) return
  pendingDelete.value = {
    noun: 'file',
    ids: Array.from(selectedFileIds.value),
    onDone: () => selectedFileIds.value.clear(),
  }
}

async function confirmDeleteSelected() {
  if (!pendingDelete.value) return
  const { ids, onDone } = pendingDelete.value
  pendingDelete.value = null
  bulkDeleteError.value = ''

  const failedIds = await targets.removeMany(ids)
  onDone()
  if (failedIds.length > 0) {
    bulkDeleteError.value = `Could not remove ${failedIds.length} item${failedIds.length === 1 ? '' : 's'}. Try again.`
  }
}

function cancelDeleteSelected() {
  pendingDelete.value = null
}

// Mirrors the backend: AppSetting-backed settings (targets, zip options, log recording) can't be
// changed while files are protected — either already encrypted, or a toggle is still in progress.
const isLocked = computed(() => encryption.enabled || encryption.toggling)

const category = ref<EnumTargetCategory>(EnumTargetCategory.Folder)
const path = ref('')
const error = ref('')
const browsing = ref(false)

async function handleAdd() {
  if (isLocked.value) return
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
  if (isLocked.value) return
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

    <Transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0 -translate-y-1"
      enter-to-class="opacity-100 translate-y-0"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="isLocked"
        class="mb-5 flex items-start gap-3 rounded-xl border px-4 py-3.5"
        :class="
          encryption.toggling
            ? 'border-amber-400/25 bg-amber-400/10 text-amber-200'
            : 'border-teal-400/25 bg-teal-400/10 text-teal-200'
        "
      >
        <IconLock class="h-5 w-5 shrink-0 translate-y-0.5" />
        <div>
          <p class="text-sm font-semibold">
            {{ encryption.toggling ? 'Protection is being applied' : 'Settings are locked' }}
          </p>
          <p class="mt-0.5 text-xs opacity-90">
            Setting cannot be change while files are protected. Turn off the protection first before
            change any setting.
          </p>
        </div>
      </div>
    </Transition>

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
              :disabled="isLocked"
              class="h-10.5 rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none disabled:opacity-50"
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
                :disabled="isLocked"
                :placeholder="
                  category === EnumTargetCategory.Folder
                    ? '/Users/you/Documents/Projects'
                    : '/Users/you/Documents/report.pdf'
                "
                class="h-10.5 w-full rounded-lg border border-white/10 bg-neutral-950 px-3 pr-20 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none disabled:opacity-50"
              />
              <div class="absolute inset-y-0 right-1.5 flex items-center gap-1">
                <button
                  type="button"
                  title="Paste from clipboard"
                  aria-label="Paste from clipboard"
                  :disabled="isLocked"
                  class="flex h-7 w-7 items-center justify-center rounded-md text-neutral-500 transition-colors hover:bg-white/10 hover:text-teal-300 disabled:opacity-50"
                  @click="handlePaste"
                >
                  <IconClipboard class="h-4 w-4" />
                </button>
                <button
                  type="button"
                  title="Browse… (pick multiple, added instantly)"
                  aria-label="Browse for one or more paths"
                  :disabled="browsing || isLocked"
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
            :disabled="isLocked"
            class="h-10.5 rounded-lg bg-teal-400 px-6 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-teal-400"
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
            <input
              v-if="targets.folders.length"
              v-model="folderSearch"
              type="text"
              placeholder="Search folders…"
              class="mb-3 h-9 w-full rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
            />
            <div v-if="filteredFolders.length" class="mb-2.5 flex items-center justify-between gap-2">
              <label class="flex items-center gap-2 text-xs font-semibold text-neutral-400">
                <Checkbox
                  :model-value="allFilteredFoldersSelected"
                  :indeterminate="someFilteredFoldersSelected"
                  :disabled="isLocked"
                  @update:model-value="toggleSelectAllFolders"
                />
                Select all
              </label>
              <Transition
                enter-active-class="transition duration-150 ease-out"
                enter-from-class="opacity-0 scale-95"
                enter-to-class="opacity-100 scale-100"
                leave-active-class="transition duration-100 ease-in"
                leave-from-class="opacity-100 scale-100"
                leave-to-class="opacity-0 scale-95"
              >
                <button
                  v-if="selectedFolderIds.size > 0"
                  type="button"
                  :disabled="isLocked"
                  class="flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-semibold text-red-400 transition-colors hover:bg-red-400/10 disabled:pointer-events-none disabled:opacity-40"
                  @click="requestDeleteSelectedFolders"
                >
                  <IconTrash class="h-3.5 w-3.5" />
                  Delete ({{ selectedFolderIds.size }})
                </button>
              </Transition>
            </div>
            <TransitionGroup
              v-if="filteredFolders.length"
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
                v-for="t in filteredFolders"
                :key="t.id"
                class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
              >
                <Checkbox
                  :model-value="selectedFolderIds.has(t.id)"
                  :disabled="isLocked"
                  aria-label="Select folder"
                  @update:model-value="toggleFolderSelection(t.id)"
                />
                <IconFolder class="h-4.5 w-4.5 shrink-0 text-neutral-500" />
                <span class="flex-1 text-sm break-all text-neutral-200">{{ t.path }}</span>
                <button
                  type="button"
                  :disabled="isLocked"
                  class="flex h-7 w-7 items-center justify-center rounded-lg text-neutral-500 hover:bg-red-400/10 hover:text-red-400 disabled:pointer-events-none disabled:opacity-40"
                  aria-label="Remove"
                  @click="removeSingleTarget(t.id)"
                >
                  <IconTrash class="h-4 w-4" />
                </button>
              </li>
            </TransitionGroup>
            <p v-else-if="targets.folders.length" class="text-sm text-neutral-500">
              No folders match "{{ folderSearch }}".
            </p>
            <p v-else class="text-sm text-neutral-500">No folders added yet.</p>
          </section>

          <section class="rounded-xl border border-white/10 bg-neutral-950/60 p-4">
            <h3 class="mb-3 flex items-center gap-2 text-sm font-semibold text-neutral-100">
              Files
              <span class="rounded-full bg-teal-400/10 px-2.5 py-0.5 text-xs font-bold text-teal-300">
                {{ targets.files.length }}
              </span>
            </h3>
            <input
              v-if="targets.files.length"
              v-model="fileSearch"
              type="text"
              placeholder="Search files…"
              class="mb-3 h-9 w-full rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
            />
            <div v-if="filteredFiles.length" class="mb-2.5 flex items-center justify-between gap-2">
              <label class="flex items-center gap-2 text-xs font-semibold text-neutral-400">
                <Checkbox
                  :model-value="allFilteredFilesSelected"
                  :indeterminate="someFilteredFilesSelected"
                  :disabled="isLocked"
                  @update:model-value="toggleSelectAllFiles"
                />
                Select all
              </label>
              <Transition
                enter-active-class="transition duration-150 ease-out"
                enter-from-class="opacity-0 scale-95"
                enter-to-class="opacity-100 scale-100"
                leave-active-class="transition duration-100 ease-in"
                leave-from-class="opacity-100 scale-100"
                leave-to-class="opacity-0 scale-95"
              >
                <button
                  v-if="selectedFileIds.size > 0"
                  type="button"
                  :disabled="isLocked"
                  class="flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-semibold text-red-400 transition-colors hover:bg-red-400/10 disabled:pointer-events-none disabled:opacity-40"
                  @click="requestDeleteSelectedFiles"
                >
                  <IconTrash class="h-3.5 w-3.5" />
                  Delete ({{ selectedFileIds.size }})
                </button>
              </Transition>
            </div>
            <TransitionGroup
              v-if="filteredFiles.length"
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
                v-for="t in filteredFiles"
                :key="t.id"
                class="flex items-center gap-3 rounded-lg bg-neutral-950 px-3 py-2.5"
              >
                <Checkbox
                  :model-value="selectedFileIds.has(t.id)"
                  :disabled="isLocked"
                  aria-label="Select file"
                  @update:model-value="toggleFileSelection(t.id)"
                />
                <IconFile class="h-4.5 w-4.5 shrink-0 text-neutral-500" />
                <span class="flex-1 text-sm break-all text-neutral-200">{{ t.path }}</span>
                <button
                  type="button"
                  :disabled="isLocked"
                  class="flex h-7 w-7 items-center justify-center rounded-lg text-neutral-500 hover:bg-red-400/10 hover:text-red-400 disabled:pointer-events-none disabled:opacity-40"
                  aria-label="Remove"
                  @click="removeSingleTarget(t.id)"
                >
                  <IconTrash class="h-4 w-4" />
                </button>
              </li>
            </TransitionGroup>
            <p v-else-if="targets.files.length" class="text-sm text-neutral-500">
              No files match "{{ fileSearch }}".
            </p>
            <p v-else class="text-sm text-neutral-500">No files added yet.</p>
          </section>
        </div>

        <p v-if="bulkDeleteError" class="mt-3 text-sm text-red-400">{{ bulkDeleteError }}</p>
      </SettingsSection>

      <SettingsSection
        title="Encryption Options"
        description="Fine-tune how your files are packaged and protected."
        storage-key="zip-settings"
      >
        <div class="flex flex-col divide-y divide-white/10">
          <div class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0">
            <div>
              <p class="text-sm font-semibold text-neutral-100">Turbo Zip</p>
              <p class="mt-1 max-w-lg text-xs text-neutral-500">
                When protecting 20 or more folders at once, uses more of your computer's power to
                finish much faster. Your computer may run warmer and use more battery while it's
                working.
              </p>
            </div>
            <ToggleSwitch
              :model-value="zipSettings.isUseTurboZip"
              :disabled="zipSettings.saving || isLocked"
              aria-label="Toggle Turbo Zip"
              @update:model-value="zipSettings.setTurboZip"
            />
          </div>

          <div class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0">
            <div>
              <p class="text-sm font-semibold text-neutral-100">Encrypt Zipped Files</p>
              <p class="mt-1 max-w-lg text-xs text-neutral-500">
                Adds extra protection to your packaged files so they can only be opened with this
                app — more secure, but takes a bit longer to finish.
              </p>
            </div>
            <ToggleSwitch
              :model-value="zipSettings.isEncryptZippedFile"
              :disabled="zipSettings.saving || isLocked"
              aria-label="Toggle Encrypt Zipped Files"
              @update:model-value="zipSettings.setEncryptZippedFile"
            />
          </div>

          <div class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0">
            <div>
              <p class="text-sm font-semibold text-neutral-100">Encrypt App Settings</p>
              <p class="mt-1 max-w-lg text-xs text-neutral-500">
                Hides the list of folders and files you're protecting, so it can't just be opened and
                read by someone else using your computer. This is a light layer of protection, not
                the same as full encryption — but it stops your private list from being seen at a
                glance. Recommended to leave on.
              </p>
            </div>
            <ToggleSwitch
              :model-value="appSettingEncryption.isEncryptAppSetting"
              :disabled="appSettingEncryption.saving || isLocked"
              aria-label="Toggle Encrypt App Settings"
              @update:model-value="appSettingEncryption.setEncryptAppSetting"
            />
          </div>
        </div>

        <p v-if="zipSettings.error" class="mt-3 text-xs text-red-400">{{ zipSettings.error }}</p>
        <p v-if="appSettingEncryption.error" class="mt-3 text-xs text-red-400">{{ appSettingEncryption.error }}</p>
      </SettingsSection>

      <SettingsSection
        title="Browser Profiles"
        description="Pick which browser profile's saved data (like bookmarks and passwords) should be protected too."
        storage-key="browser-profiles"
      >
        <div class="grid gap-5 sm:grid-cols-2">
          <BrowserProfileCard :browser="BrowserType.Edge" />
          <BrowserProfileCard :browser="BrowserType.Chrome" />
        </div>
      </SettingsSection>

      <SettingsSection
        title="Logging"
        description="Keep a history of what the app has been doing, so you (or support) can look back if something goes wrong."
        storage-key="logging"
      >
        <div class="flex items-start justify-between gap-4">
          <div>
            <p class="text-sm font-semibold text-neutral-100">Record Logs</p>
            <p class="mt-1 max-w-lg text-xs text-neutral-500">
              When on, the app keeps a record of what it's been doing and any errors it runs into,
              which you can view on the Logs page — kept for the last 4 days, then deleted
              automatically. "Risking expose the file's name that you try to protect".
              Turn this on only if you're reporting a problem and want to share what
              happened; otherwise it's fine to leave off.
            </p>
          </div>
          <ToggleSwitch
            :model-value="logSettings.isRecordLog"
            :disabled="logSettings.saving || isLocked"
            aria-label="Toggle log recording"
            @update:model-value="logSettings.setRecordLog"
          />
        </div>

        <p v-if="logSettings.error" class="mt-3 text-xs text-red-400">{{ logSettings.error }}</p>
      </SettingsSection>

      <SettingsSection
        title="About & Updates"
        description="See which version of Calypsov you're running and check for a newer one."
        storage-key="about"
      >
        <div class="flex items-center justify-between gap-4">
          <div>
            <p class="text-sm font-semibold text-neutral-100">
              Version {{ appUpdate.currentVersion ?? '…' }}
            </p>
            <p class="mt-1 text-xs text-neutral-500">Calypsov checks GitHub for the latest release.</p>
          </div>
          <button
            type="button"
            :disabled="appUpdate.checking"
            class="h-9 shrink-0 rounded-lg border border-white/10 bg-neutral-950 px-4 text-sm font-semibold text-neutral-200 transition-colors hover:border-teal-400/40 hover:text-teal-300 disabled:cursor-not-allowed disabled:opacity-50"
            @click="appUpdate.checkForUpdate"
          >
            {{ appUpdate.checking ? 'Checking…' : 'Check for Updates' }}
          </button>
        </div>

        <div
          v-if="appUpdate.updateAvailable"
          class="mt-4 flex items-center justify-between gap-4 rounded-xl border border-teal-400/25 bg-teal-400/10 px-4 py-3.5"
        >
          <p class="text-sm text-teal-200">
            A new version is available — <span class="font-semibold">v{{ appUpdate.latestVersion }}</span>
          </p>
          <button
            type="button"
            class="h-9 shrink-0 rounded-lg bg-teal-400 px-4 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
            @click="appUpdate.openRelease"
          >
            View Release
          </button>
        </div>

        <p
          v-else-if="appUpdate.checked && !appUpdate.error"
          class="mt-4 text-sm text-neutral-500"
        >
          You're on the latest version.
        </p>

        <p v-if="appUpdate.error" class="mt-4 text-sm text-red-400">{{ appUpdate.error }}</p>
      </SettingsSection>
    </div>

    <ConfirmDialog
      :open="pendingDelete !== null"
      title="Remove selected?"
      :message="pendingDeleteMessage"
      confirm-label="Delete"
      danger
      @confirm="confirmDeleteSelected"
      @cancel="cancelDeleteSelected"
    />
  </div>
</template>
