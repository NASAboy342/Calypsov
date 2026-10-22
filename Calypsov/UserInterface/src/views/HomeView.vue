<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import EncryptionProgressBar from '@/components/EncryptionProgressBar.vue'
import SettingsSection from '@/components/SettingsSection.vue'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconFile from '@/components/icons/IconFile.vue'
import IconLayers from '@/components/icons/IconLayers.vue'
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

const hasTargets = computed(() => targets.count > 0)

// Labels reflect real state only, never an assumed one — "protected" once the toggle has actually
// succeeded, "configured" otherwise — so the dashboard can't imply something is protected before
// the operation finishes.
const itemsLabel = computed(() => (encryption.enabled ? 'protected' : 'configured'))
const itemsAccent = computed(() => (encryption.enabled ? 'text-teal-300' : 'text-neutral-500'))

// enabled still holds the pre-toggle value while toggling is true (the store only flips it once
// the request resolves), so this reflects the direction of the run in progress.
const progressActionLabel = computed(() => (encryption.enabled ? 'Restoring' : 'Zipping'))

// The iconic dial toggle, brought back after the dashboard redesign replaced it with a plain
// button — kept, just restyled to sit within the new, simpler hero layout.
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

// Replaces the old passive "Protection: On/Off" readout with an action-oriented hint — the dial
// had no sign anywhere that it was something to click, so pairing it with what clicking *does*
// (rather than just restating the state already shown in the heading above) is the label doing
// real work instead of just decorating the control.
const dialHintLabel = computed(() => (encryption.enabled ? 'Click to turn off' : 'Click to turn on'))

const statusDescription = computed(() => {
  if (!hasTargets.value && !encryption.enabled) {
    return "You haven't added anything to protect yet. Add folders or files in Settings to get started."
  }
  const itemWord = targets.count === 1 ? 'item' : 'items'
  const itemVerb = targets.count === 1 ? 'is' : 'are'
  return encryption.enabled
    ? `Your ${targets.count} ${itemWord} ${itemVerb} zipped up and hidden from their original location.`
    : `Turning protection on will zip up and hide the ${targets.count} ${itemWord} you've configured.`
})
</script>

<template>
  <div class="mx-auto flex max-w-5xl flex-col gap-5">
    <!-- Protection status: the dashboard's primary focal point — a clear state, a plain-language
         explanation of what that state means, and the iconic dial as the one actionable control. -->
    <div class="rounded-3xl border border-white/10 bg-neutral-900 p-8 sm:p-10">
      <div class="flex flex-col gap-10 md:flex-row md:items-center md:justify-between">
        <div class="max-w-xl">
          <div class="mb-3 flex items-center gap-2">
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
            <span class="text-xs font-semibold tracking-[0.2em] text-neutral-400 uppercase">Protection Status</span>
          </div>

          <h1 class="text-4xl font-bold tracking-tight text-neutral-50 sm:text-5xl">
            Protection is
            <span :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-500'">
              {{ encryption.enabled ? 'on' : 'off' }}
            </span>
          </h1>

          <p class="mt-4 text-base leading-relaxed text-neutral-400">
            {{ statusDescription }}
          </p>

          <div class="mt-8 flex flex-wrap items-center gap-4">
            <RouterLink
              v-if="!hasTargets"
              to="/settings"
              class="rounded-full bg-teal-400 px-6 py-3 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
            >
              Add Files or Folders
            </RouterLink>
            <RouterLink
              v-else
              to="/settings"
              class="rounded-full border border-white/15 px-6 py-3 text-sm font-semibold text-neutral-200 transition-colors hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
            >
              Manage in Settings
            </RouterLink>
          </div>
        </div>

        <div class="flex flex-col items-center gap-3">
          <button
            type="button"
            role="switch"
            :aria-checked="encryption.enabled"
            :aria-busy="encryption.toggling"
            aria-label="Toggle protection"
            :disabled="encryption.toggling"
            class="group relative flex h-32 w-32 shrink-0 items-center justify-center rounded-full transition-transform duration-200 hover:scale-105 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none active:scale-95 disabled:cursor-wait disabled:hover:scale-100"
            @click="encryption.toggle()"
          >
            <span
              class="absolute inset-0 rounded-full border border-dashed border-white/10 transition-colors duration-200 group-hover:border-teal-400/40"
              :class="{ 'animate-pulse': !encryption.enabled && !encryption.toggling }"
            />
            <span
              class="relative h-24 w-24 shrink-0 rounded-full border-2 bg-neutral-900 transition-all duration-300"
              :class="ringClass"
            >
              <span
                class="absolute inset-0 flex justify-center"
                :class="[handWrapperClass, !encryption.toggling && 'transition-transform duration-300']"
              >
                <span class="mt-3 h-7 w-1 rounded-full transition-colors duration-300" :class="handClass" />
              </span>
              <span
                class="absolute inset-0 m-auto h-3 w-3 rounded-full transition-colors duration-300"
                :class="dotClass"
              />
            </span>
          </button>

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
              :cancelling="encryption.cancelling"
              @cancel="encryption.cancel()"
            />
            <p v-else class="text-sm font-medium text-neutral-400">
              {{ dialHintLabel }}
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

    <!-- How it works: collapsed by default so it doesn't compete with the status card above for a
         returning user — SettingsSection persists the collapse state per storage-key, so a user
         who expands or collapses it keeps that choice on every future visit. Reused purely as a
         generic collapsible card; nothing about it is Settings-specific. -->
    <SettingsSection
      title="How Calypsov Works"
      description="New here? A quick 3-step overview."
      storage-key="how-it-works"
      :default-open="false"
    >
      <div class="grid gap-5 sm:grid-cols-3">
        <div class="flex items-start gap-3">
          <span
            class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-teal-400/10 text-sm font-bold text-teal-300"
          >
            1
          </span>
          <div>
            <p class="text-sm font-semibold text-neutral-100">Choose what to protect</p>
            <p class="mt-1 text-xs text-neutral-500">
              Add folders or files you want kept out of sight, in Settings.
            </p>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <span
            class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-teal-400/10 text-sm font-bold text-teal-300"
          >
            2
          </span>
          <div>
            <p class="text-sm font-semibold text-neutral-100">Turn protection on</p>
            <p class="mt-1 text-xs text-neutral-500">
              Calypsov zips everything up and removes it from its original location.
            </p>
          </div>
        </div>
        <div class="flex items-start gap-3">
          <span
            class="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-teal-400/10 text-sm font-bold text-teal-300"
          >
            3
          </span>
          <div>
            <p class="text-sm font-semibold text-neutral-100">Turn it off anytime</p>
            <p class="mt-1 text-xs text-neutral-500">
              Flip the switch back and everything returns exactly where it was.
            </p>
          </div>
        </div>
      </div>
    </SettingsSection>

    <!-- Protected items + configuration: two consistently-styled, side-by-side cards rather than
         four separate boxed stat tiles plus an unrelated-feeling snapshot card below them — reads
         as one cohesive dashboard section instead of a grid of disconnected widgets. Every tile
         and row links to Settings, since that's where all of this is actually managed. -->
    <div class="grid gap-5 lg:grid-cols-2">
      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <h2 class="mb-4 text-[15px] font-semibold text-neutral-100">Protected Items</h2>
        <div class="grid grid-cols-3 divide-x divide-white/10">
          <RouterLink
            to="/settings"
            class="flex flex-col items-center gap-2 rounded-lg px-2 py-1 text-center transition-colors hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <IconFolder class="h-5 w-5 transition-colors duration-300" :class="itemsAccent" />
            <span class="text-2xl font-bold text-neutral-50">{{ targets.folders.length }}</span>
            <span class="text-xs text-neutral-500">Folders {{ itemsLabel }}</span>
          </RouterLink>
          <RouterLink
            to="/settings"
            class="flex flex-col items-center gap-2 rounded-lg px-2 py-1 text-center transition-colors hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <IconFile class="h-5 w-5 transition-colors duration-300" :class="itemsAccent" />
            <span class="text-2xl font-bold text-neutral-50">{{ targets.files.length }}</span>
            <span class="text-xs text-neutral-500">Files {{ itemsLabel }}</span>
          </RouterLink>
          <RouterLink
            to="/settings"
            class="flex flex-col items-center gap-2 rounded-lg px-2 py-1 text-center transition-colors hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <IconLayers class="h-5 w-5 transition-colors duration-300" :class="itemsAccent" />
            <span class="text-2xl font-bold text-neutral-50">{{ targets.count }}</span>
            <span class="text-xs text-neutral-500">Total {{ itemsLabel }}</span>
          </RouterLink>
        </div>
      </div>

      <div class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
        <h2 class="mb-4 text-[15px] font-semibold text-neutral-100">Configuration</h2>
        <div class="flex flex-col divide-y divide-white/10">
          <RouterLink
            to="/settings"
            class="flex items-center justify-between gap-3 rounded-lg px-2 py-2.5 transition-colors first:pt-1 last:pb-1 hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <span class="flex items-center gap-2.5 text-sm text-neutral-300">
              <IconBolt
                class="h-4 w-4 shrink-0"
                :class="zipSettings.isUseTurboZip ? 'text-teal-300' : 'text-neutral-600'"
              />
              Turbo Zip
            </span>
            <span
              class="rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="zipSettings.isUseTurboZip ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'"
            >
              {{ zipSettings.isUseTurboZip ? 'On' : 'Off' }}
            </span>
          </RouterLink>

          <RouterLink
            to="/settings"
            class="flex items-center justify-between gap-3 rounded-lg px-2 py-2.5 transition-colors first:pt-1 last:pb-1 hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <span class="flex items-center gap-2.5 text-sm text-neutral-300">
              <IconLock
                class="h-4 w-4 shrink-0"
                :class="zipSettings.isEncryptZippedFile ? 'text-teal-300' : 'text-neutral-600'"
              />
              Zip Encryption
            </span>
            <span
              class="rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="
                zipSettings.isEncryptZippedFile ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'
              "
            >
              {{ zipSettings.isEncryptZippedFile ? 'On' : 'Off' }}
            </span>
          </RouterLink>

          <RouterLink
            to="/settings"
            class="flex items-center justify-between gap-3 rounded-lg px-2 py-2.5 transition-colors first:pt-1 last:pb-1 hover:bg-white/5 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none"
          >
            <span class="flex items-center gap-2.5 text-sm text-neutral-300">
              <IconGlobe
                class="h-4 w-4 shrink-0"
                :class="connectedBrowserCount > 0 ? 'text-teal-300' : 'text-neutral-600'"
              />
              Browser Profiles
            </span>
            <span
              class="rounded-full px-2.5 py-0.5 text-xs font-bold"
              :class="connectedBrowserCount > 0 ? 'bg-teal-400/10 text-teal-300' : 'bg-white/5 text-neutral-500'"
            >
              {{ connectedBrowserCount }} of 2
            </span>
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>
