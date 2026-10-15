<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import IconAlertTriangle from '@/components/icons/IconAlertTriangle.vue'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import { useLogSettingsStore } from '@/stores/logSettings'
import { useAppSettingEncryptionStore } from '@/stores/appSettingEncryption'
import { useProfilesStore } from '@/stores/profiles'
import { BrowserType } from '@/services/browsersApi'

const encryption = useEncryptionStore()
const targets = useTargetsStore()
const zipSettings = useZipSettingsStore()
const browserProfiles = useBrowserProfilesStore()
const logSettings = useLogSettingsStore()
const appSettingEncryption = useAppSettingEncryptionStore()
const profiles = useProfilesStore()

onMounted(() => {
  encryption.fetchStatus()
  targets.fetchTargets()
  zipSettings.fetchZipSettings()
  logSettings.fetchLogSetting()
  appSettingEncryption.fetchAppSettingEncryption()
  // Fetched here (not lazily in BrowserProfileCard alone) so the Home dashboard's
  // "configuration snapshot" has real selection data ready without requiring a Settings visit first.
  browserProfiles.fetchProfiles(BrowserType.Edge)
  browserProfiles.fetchProfiles(BrowserType.Chrome)
  // Fetched here (not lazily in ProfileSwitcher alone) so the sidebar's current-profile indicator
  // is populated the moment the app loads, matching the pattern every other app-wide store uses.
  profiles.fetchProfiles()
})
</script>

<template>
  <div class="relative flex h-screen overflow-hidden bg-neutral-950">
    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_80%_50%_at_15%_-10%,rgba(45,212,191,0.16),transparent)]"
    />
    <AppSidebar class="relative" />
    <main class="relative min-w-0 flex-1 overflow-y-auto px-8 py-10 lg:px-12">
      <!--
        Visible on every page (not just Home, where the toggle is started) since closing the app
        is a risk regardless of which page is open when a toggle is in flight — the backend has no
        resume/rollback for a run interrupted mid-zip/mid-restore. Sticky so scrolling a long page
        (e.g. Settings) can't scroll it out of view.
      -->
      <Transition
        enter-active-class="transition duration-200 ease-out"
        enter-from-class="opacity-0 -translate-y-2"
        enter-to-class="opacity-100 translate-y-0"
        leave-active-class="transition duration-150 ease-in"
        leave-from-class="opacity-100 translate-y-0"
        leave-to-class="opacity-0 -translate-y-2"
      >
        <div
          v-if="encryption.toggling"
          class="sticky top-0 z-40 mb-6 flex items-center justify-center gap-2 rounded-xl border border-amber-400/30 bg-amber-400/15 px-4 py-2.5 text-center text-sm font-semibold text-amber-200 shadow-[0_8px_24px_-8px_rgba(0,0,0,0.6)] backdrop-blur"
        >
          <IconAlertTriangle class="h-4 w-4 shrink-0" />
          Do not close the app — protection is being applied. Closing now can corrupt your files or
          cause permanent data loss.
        </div>
      </Transition>

      <RouterView v-slot="{ Component }">
        <Transition
          mode="out-in"
          enter-active-class="transition duration-200 ease-out"
          enter-from-class="opacity-0 translate-y-1"
          enter-to-class="opacity-100 translate-y-0"
          leave-active-class="transition duration-150 ease-in"
          leave-from-class="opacity-100 translate-y-0"
          leave-to-class="opacity-0 -translate-y-1"
        >
          <component :is="Component" />
        </Transition>
      </RouterView>
    </main>
  </div>
</template>
