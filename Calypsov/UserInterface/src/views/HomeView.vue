<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import EncryptionProgressBar from '@/components/EncryptionProgressBar.vue'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconFile from '@/components/icons/IconFile.vue'
import IconLayers from '@/components/icons/IconLayers.vue'
import IconShield from '@/components/icons/IconShield.vue'
import IconBolt from '@/components/icons/IconBolt.vue'
import IconLock from '@/components/icons/IconLock.vue'
import IconGlobe from '@/components/icons/IconGlobe.vue'

const encryption = useEncryptionStore()
const targets = useTargetsStore()
const zipSettings = useZipSettingsStore()
const browserProfiles = useBrowserProfilesStore()

const connectedBrowserCount = computed(
  () => Object.values(browserProfiles.byBrowser).filter((b) => b.selectedId !== null).length,
)

const ringClass = computed(() => {
  if (encryption.toggling) {
    return 'animate-pulse border-amber-400/70 shadow-[0_0_30px_-8px_rgba(251,191,36,0.6)]'
  }
  return encryption.enabled
    ? 'border-teal-400 shadow-[0_0_40px_-6px_rgba(45,212,191,0.65)]'
    : 'border-neutral-700 shadow-[0_0_25px_-10px_rgba(255,255,255,0.12)]'
})

const handWrapperClass = computed(() =>
  encryption.toggling ? 'animate-spin' : encryption.enabled ? 'rotate-0' : 'rotate-180',
)

const handClass = computed(() => {
  if (encryption.toggling) return 'bg-amber-300 shadow-[0_0_10px_rgba(251,191,36,0.9)]'
  return encryption.enabled ? 'bg-teal-400 shadow-[0_0_10px_rgba(45,212,191,0.9)]' : 'bg-neutral-600'
})

const dotClass = computed(() => {
  if (encryption.toggling) return 'bg-amber-300'
  return encryption.enabled ? 'bg-teal-400' : 'bg-neutral-700'
})

// enabled still holds the pre-toggle value while toggling is true (the store only flips it
// once the request resolves), so this reflects the direction of the run in progress.
const progressActionLabel = computed(() => (encryption.enabled ? 'Restoring' : 'Zipping'))

// A soft glow behind the dial, color-matched to its current ring color — ties the hero card's
// backdrop to the same on/off/toggling state instead of a static decoration.
const heroGlowStyle = computed(() => {
  if (encryption.toggling) {
    return { background: 'radial-gradient(circle, rgba(251,191,36,0.22), transparent 70%)' }
  }
  return {
    background: encryption.enabled
      ? 'radial-gradient(circle, rgba(45,212,191,0.22), transparent 70%)'
      : 'radial-gradient(circle, rgba(115,115,115,0.12), transparent 70%)',
  }
})
</script>

<template>
  <div class="mx-auto flex max-w-5xl flex-col gap-6">
    <!-- Hero card -->
    <div class="relative overflow-hidden rounded-3xl border border-white/10 bg-neutral-900 p-8 sm:p-10">
      <div
        class="pointer-events-none absolute -top-24 right-0 h-96 w-96 transition-[background] duration-500"
        :style="heroGlowStyle"
      />

      <div class="relative flex flex-col gap-12 md:flex-row md:items-center md:justify-between">
        <div class="max-w-xl">
          <p class="mb-4 flex items-center gap-2 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">
            <span class="relative flex h-2 w-2">
              <span
                v-if="encryption.enabled && !encryption.toggling"
                class="absolute inline-flex h-full w-full animate-ping rounded-full bg-teal-400 opacity-75"
              />
              <span
                class="relative inline-flex h-2 w-2 rounded-full"
                :class="encryption.toggling ? 'bg-amber-400' : encryption.enabled ? 'bg-teal-400' : 'bg-neutral-600'"
              />
            </span>
            Protection status
          </p>

          <h1 class="text-4xl font-extrabold tracking-tight text-neutral-50 sm:text-5xl">
            Encryption is
            <span
              class="bg-linear-to-r bg-clip-text text-transparent"
              :class="encryption.enabled ? 'from-teal-300 to-indigo-400' : 'from-neutral-500 to-neutral-600'"
            >
              {{ encryption.enabled ? 'on' : 'off' }}
            </span>
          </h1>

          <p class="mt-6 text-base leading-relaxed text-neutral-400">
            <template v-if="targets.count === 0">
              No files or folders are configured yet. Add some in Settings to start protecting them.
            </template>
            <template v-else>
              {{ encryption.enabled ? 'Actively protecting' : 'Ready to protect' }}
              {{ targets.count }} {{ targets.count === 1 ? 'item' : 'items' }} configured in Settings.
            </template>
          </p>

          <div class="mt-8 flex flex-wrap items-center gap-4">
            <RouterLink
              v-if="targets.count === 0"
              to="/settings"
              class="rounded-full bg-teal-400 px-6 py-3 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
            >
              Add files or folders
            </RouterLink>
            <RouterLink
              v-else
              to="/settings"
              class="rounded-full border border-white/15 px-6 py-3 text-sm font-semibold text-neutral-200 transition-colors hover:bg-white/5"
            >
              Manage in Settings
            </RouterLink>
          </div>
        </div>

        <div class="flex flex-col items-center gap-4">
          <div class="relative flex h-32 w-32 shrink-0 items-center justify-center">
            <span class="absolute inset-0 rounded-full border border-dashed border-white/10" />
            <button
              type="button"
              role="switch"
              :aria-checked="encryption.enabled"
              :aria-busy="encryption.toggling"
              aria-label="Toggle encryption"
              :disabled="encryption.toggling"
              class="relative h-24 w-24 shrink-0 rounded-full border-2 bg-neutral-900 transition-all duration-300 disabled:cursor-wait"
              :class="ringClass"
              @click="encryption.toggle()"
            >
            <span
              class="absolute inset-0 flex justify-center"
              :class="[handWrapperClass, !encryption.toggling && 'transition-transform duration-300']"
            >
              <span class="mt-3 h-7 w-1 rounded-full transition-colors duration-300" :class="handClass" />
            </span>
            <span class="absolute inset-0 m-auto h-3 w-3 rounded-full transition-colors duration-300" :class="dotClass" />
            </button>
          </div>

          <Transition
            mode="out-in"
            enter-active-class="transition duration-200 ease-out"
            enter-from-class="opacity-0 scale-95"
            enter-to-class="opacity-100 scale-100"
            leave-active-class="transition duration-150 ease-in"
            leave-from-class="opacity-100 scale-100"
            leave-to-class="opacity-0 scale-95"
          >
            <EncryptionProgressBar
              v-if="encryption.toggling"
              :progress="encryption.progress"
              :action-label="progressActionLabel"
            />
            <p v-else class="text-sm text-neutral-400">
              Encrypt:
              <strong class="font-semibold" :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-200'">
                {{ encryption.enabled ? 'On' : 'Off' }}
              </strong>
            </p>
          </Transition>

          <Transition
            enter-active-class="transition duration-200 ease-out"
            enter-from-class="opacity-0"
            enter-to-class="opacity-100"
            leave-active-class="transition duration-150 ease-in"
            leave-from-class="opacity-100"
            leave-to-class="opacity-0"
          >
            <p v-if="encryption.error" class="max-w-56 text-center text-sm text-red-400">
              {{ encryption.error }}
            </p>
          </Transition>
        </div>
      </div>
    </div>

    <!-- Stat cards -->
    <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <span class="flex h-9 w-9 items-center justify-center rounded-lg bg-teal-400/10">
          <IconFolder class="h-4.5 w-4.5 text-teal-300" />
        </span>
        <p class="mt-3 text-3xl font-bold text-neutral-50">{{ targets.folders.length }}</p>
        <p class="mt-1 text-sm text-neutral-500">Folders protected</p>
      </div>
      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <span class="flex h-9 w-9 items-center justify-center rounded-lg bg-teal-400/10">
          <IconFile class="h-4.5 w-4.5 text-teal-300" />
        </span>
        <p class="mt-3 text-3xl font-bold text-neutral-50">{{ targets.files.length }}</p>
        <p class="mt-1 text-sm text-neutral-500">Files protected</p>
      </div>
      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <span class="flex h-9 w-9 items-center justify-center rounded-lg bg-teal-400/10">
          <IconLayers class="h-4.5 w-4.5 text-teal-300" />
        </span>
        <p class="mt-3 text-3xl font-bold text-neutral-50">{{ targets.count }}</p>
        <p class="mt-1 text-sm text-neutral-500">Total items configured</p>
      </div>
      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <span
          class="flex h-9 w-9 items-center justify-center rounded-lg"
          :class="encryption.enabled ? 'bg-teal-400/10' : 'bg-white/5'"
        >
          <IconShield class="h-4.5 w-4.5" :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-500'" />
        </span>
        <p class="mt-3 text-3xl font-bold" :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-50'">
          {{ encryption.enabled ? 'ON' : 'OFF' }}
        </p>
        <p class="mt-1 text-sm text-neutral-500">Current status</p>
      </div>
    </div>

    <!-- Configuration snapshot -->
    <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
      <h2 class="mb-4 text-[15px] font-semibold text-neutral-100">Configuration snapshot</h2>
      <div class="grid gap-5 sm:grid-cols-3">
        <div class="flex items-center gap-3">
          <IconBolt class="h-5 w-5 shrink-0 text-amber-300" />
          <div>
            <p class="text-sm text-neutral-300">Turbo Zip</p>
            <span
              class="mt-0.5 inline-block rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="zipSettings.isUseTurboZip ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'"
            >
              {{ zipSettings.isUseTurboZip ? 'On' : 'Off' }}
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <IconLock class="h-5 w-5 shrink-0 text-amber-300" />
          <div>
            <p class="text-sm text-neutral-300">Zip encryption</p>
            <span
              class="mt-0.5 inline-block rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="zipSettings.isEncryptZippedFile ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'"
            >
              {{ zipSettings.isEncryptZippedFile ? 'On' : 'Off' }}
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <IconGlobe class="h-5 w-5 shrink-0 text-amber-300" />
          <div>
            <p class="text-sm text-neutral-300">Browser profiles</p>
            <span
              class="mt-0.5 inline-block rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="connectedBrowserCount > 0 ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'"
            >
              {{ connectedBrowserCount }} of 2 connected
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
