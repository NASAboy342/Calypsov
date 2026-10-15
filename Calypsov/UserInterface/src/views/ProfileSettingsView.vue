<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useProfilesStore, type ProtectionProfile } from '@/stores/profiles'
import { useEncryptionStore } from '@/stores/encryption'
import ProfileBadge from '@/components/ProfileBadge.vue'
import ProfileFormDialog from '@/components/ProfileFormDialog.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import IconTrash from '@/components/icons/IconTrash.vue'
import IconLock from '@/components/icons/IconLock.vue'
import { fuzzyMatch } from '@/utils/fuzzyMatch'

const profiles = useProfilesStore()
const encryption = useEncryptionStore()

onMounted(() => {
  profiles.fetchProfiles()
})

const search = ref('')
const filtered = computed(() => profiles.items.filter((p) => fuzzyMatch(search.value, p.name)))

// Mirrors SettingsView's own isLocked concept: profile create/switch/delete are blocked while
// protection is on, since only the current profile can ever be encrypted. Renaming/recoloring/
// re-iconing (the edit dialog) is deliberately NOT gated by this, matching the backend.
const isLocked = computed(() => encryption.enabled || encryption.toggling)

const formOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const editingProfile = ref<ProtectionProfile | null>(null)
const formError = ref('')

function openCreateDialog() {
  if (isLocked.value) return
  formMode.value = 'create'
  editingProfile.value = null
  formError.value = ''
  formOpen.value = true
}

function openEditDialog(profile: ProtectionProfile) {
  formMode.value = 'edit'
  editingProfile.value = profile
  formError.value = ''
  formOpen.value = true
}

async function handleFormSubmit(payload: { name: string; color: string; icon: string }) {
  const result =
    formMode.value === 'create'
      ? await profiles.createProfile(payload.name, payload.color, payload.icon)
      : await profiles.updateProfile(editingProfile.value!.id, payload.name, payload.color, payload.icon)

  if (result) {
    formError.value = result
    return
  }
  formOpen.value = false
}

const pendingDeleteId = ref<string | null>(null)
const deleteError = ref('')

function requestDelete(id: string) {
  if (isLocked.value || profiles.items.length <= 1) return
  pendingDeleteId.value = id
}

async function confirmDelete() {
  if (!pendingDeleteId.value) return
  const id = pendingDeleteId.value
  pendingDeleteId.value = null
  deleteError.value = ''
  const result = await profiles.deleteProfile(id)
  if (result) deleteError.value = result
}

function cancelDelete() {
  pendingDeleteId.value = null
}

function switchTo(id: string) {
  if (isLocked.value || id === profiles.currentProfileId) return
  profiles.switchProfile(id)
}
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <header class="mb-8">
      <p class="mb-2 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">Configuration</p>
      <h1 class="text-2xl font-bold text-neutral-50">Profile Settings</h1>
      <p class="mt-1 text-sm text-neutral-400">
        Keep separate protection setups — like Personal and Work — and switch between them.
      </p>
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
        class="mb-5 flex items-start gap-3 rounded-xl border border-teal-400/25 bg-teal-400/10 px-4 py-3.5 text-teal-200"
      >
        <IconLock class="h-5 w-5 shrink-0 translate-y-0.5" />
        <div>
          <p class="text-sm font-semibold">Profiles are locked</p>
          <p class="mt-0.5 text-xs opacity-90">
            Turn off protection first before creating, switching, or deleting a profile. Renaming or
            restyling a profile is still fine.
          </p>
        </div>
      </div>
    </Transition>

    <div class="mb-5 flex flex-wrap items-center gap-3">
      <input
        v-model="search"
        type="text"
        placeholder="Search profiles…"
        class="h-10 min-w-56 flex-1 rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
      />
      <button
        type="button"
        :disabled="isLocked"
        class="h-10 shrink-0 rounded-lg bg-teal-400 px-4 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300 disabled:cursor-not-allowed disabled:opacity-50"
        @click="openCreateDialog"
      >
        Create Profile
      </button>
    </div>

    <TransitionGroup
      tag="ul"
      class="flex flex-col gap-3"
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="opacity-0 -translate-x-2"
      enter-to-class="opacity-100 translate-x-0"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
      move-class="transition-transform duration-200 ease-out"
    >
      <li
        v-for="p in filtered"
        :key="p.id"
        class="flex items-center gap-4 rounded-2xl border bg-neutral-900 p-4"
        :class="p.id === profiles.currentProfileId ? 'border-teal-400/40' : 'border-white/10'"
      >
        <ProfileBadge :color="p.color" :icon="p.icon" />
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2">
            <p class="truncate text-sm font-semibold text-neutral-100">{{ p.name }}</p>
            <span
              v-if="p.id === profiles.currentProfileId"
              class="shrink-0 rounded-full bg-teal-400/10 px-2 py-0.5 text-[11px] font-bold text-teal-300"
            >
              Current
            </span>
          </div>
          <p class="mt-0.5 text-xs text-neutral-500">
            {{ p.encryptionTargets.length }} item{{ p.encryptionTargets.length === 1 ? '' : 's' }} configured
          </p>
        </div>

        <button
          v-if="p.id !== profiles.currentProfileId"
          type="button"
          :disabled="isLocked"
          class="h-9 shrink-0 rounded-lg border border-white/10 px-3 text-sm font-medium text-neutral-300 transition-colors hover:border-teal-400/40 hover:text-teal-300 disabled:cursor-not-allowed disabled:opacity-50"
          @click="switchTo(p.id)"
        >
          Switch
        </button>

        <button
          type="button"
          class="h-9 shrink-0 rounded-lg px-3 text-sm font-medium text-neutral-400 transition-colors hover:bg-white/5 hover:text-neutral-200"
          @click="openEditDialog(p)"
        >
          Edit
        </button>

        <button
          type="button"
          aria-label="Delete profile"
          :disabled="isLocked || profiles.items.length <= 1"
          class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-neutral-500 hover:bg-red-400/10 hover:text-red-400 disabled:pointer-events-none disabled:opacity-30"
          @click="requestDelete(p.id)"
        >
          <IconTrash class="h-4 w-4" />
        </button>
      </li>
    </TransitionGroup>

    <p v-if="filtered.length === 0 && !profiles.loading" class="text-sm text-neutral-500">
      No profiles match "{{ search }}".
    </p>
    <p v-if="profiles.error" class="mt-3 text-sm text-red-400">{{ profiles.error }}</p>
    <p v-if="deleteError" class="mt-3 text-sm text-red-400">{{ deleteError }}</p>

    <ProfileFormDialog
      :open="formOpen"
      :mode="formMode"
      :initial-name="editingProfile?.name"
      :initial-color="editingProfile?.color"
      :initial-icon="editingProfile?.icon"
      :server-error="formError"
      @submit="handleFormSubmit"
      @cancel="formOpen = false"
    />

    <ConfirmDialog
      :open="pendingDeleteId !== null"
      title="Delete this profile?"
      message="This removes the profile and its configured targets. This can't be undone."
      confirm-label="Delete"
      danger
      @confirm="confirmDelete"
      @cancel="cancelDelete"
    />
  </div>
</template>
