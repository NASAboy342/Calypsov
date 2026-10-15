<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useProfilesStore } from '@/stores/profiles'
import { useEncryptionStore } from '@/stores/encryption'
import ProfileBadge from '@/components/ProfileBadge.vue'
import IconChevronDown from '@/components/icons/IconChevronDown.vue'

const profiles = useProfilesStore()
const encryption = useEncryptionStore()
const router = useRouter()

const open = ref(false)
const root = ref<HTMLElement | null>(null)

// Mirrors SettingsView's own isLocked concept: protection must be off before the active profile
// can change, since only the current profile can ever be the one that's encrypted.
const isLocked = computed(() => encryption.enabled || encryption.toggling)

function toggle() {
  open.value = !open.value
}

async function selectProfile(id: string) {
  if (isLocked.value || id === profiles.currentProfileId) return
  open.value = false
  await profiles.switchProfile(id)
}

function openManage() {
  open.value = false
  router.push('/profiles')
}

function handleClickOutside(event: MouseEvent) {
  if (root.value?.contains(event.target as Node)) return
  open.value = false
}

onMounted(() => document.addEventListener('mousedown', handleClickOutside))
onBeforeUnmount(() => document.removeEventListener('mousedown', handleClickOutside))
</script>

<template>
  <div ref="root" class="relative">
    <button
      type="button"
      class="flex w-full items-center gap-2.5 rounded-lg border border-white/10 bg-neutral-900/60 px-2.5 py-2 text-left transition-colors hover:bg-white/5"
      @click="toggle"
    >
      <ProfileBadge
        v-if="profiles.currentProfile"
        :color="profiles.currentProfile.color"
        :icon="profiles.currentProfile.icon"
        size="sm"
      />
      <span class="min-w-0 flex-1 truncate text-sm font-medium text-neutral-100">
        {{ profiles.currentProfile?.name ?? 'Loading…' }}
      </span>
      <IconChevronDown class="h-4 w-4 shrink-0 text-neutral-500" />
    </button>

    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="origin-top scale-95 opacity-0"
      enter-to-class="origin-top scale-100 opacity-100"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="origin-top scale-100 opacity-100"
      leave-to-class="origin-top scale-95 opacity-0"
    >
      <div
        v-if="open"
        class="absolute top-full left-0 z-30 mt-2 w-full min-w-56 rounded-lg border border-white/10 bg-neutral-900 p-1.5 shadow-xl"
      >
        <p v-if="isLocked" class="px-2.5 py-1.5 text-xs text-amber-300">Turn off protection to switch profiles.</p>
        <ul class="flex max-h-56 flex-col gap-0.5 overflow-y-auto">
          <li v-for="p in profiles.items" :key="p.id">
            <button
              type="button"
              :disabled="isLocked"
              class="flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-sm transition-colors hover:bg-white/5 disabled:cursor-not-allowed disabled:opacity-50"
              :class="p.id === profiles.currentProfileId ? 'bg-teal-400/10 text-teal-300' : 'text-neutral-200'"
              @click="selectProfile(p.id)"
            >
              <ProfileBadge :color="p.color" :icon="p.icon" size="sm" />
              <span class="truncate">{{ p.name }}</span>
            </button>
          </li>
        </ul>
        <div class="mt-1 border-t border-white/10 pt-1">
          <button
            type="button"
            class="flex w-full items-center rounded-md px-2.5 py-2 text-left text-sm font-medium text-neutral-300 transition-colors hover:bg-white/5 hover:text-neutral-100"
            @click="openManage"
          >
            Manage Profiles…
          </button>
        </div>
      </div>
    </Transition>
  </div>
</template>
