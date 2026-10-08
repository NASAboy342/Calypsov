<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'
import { useZipSettingsStore } from '@/stores/zipSettings'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import { BrowserType } from '@/services/browsersApi'

const encryption = useEncryptionStore()
const targets = useTargetsStore()
const zipSettings = useZipSettingsStore()
const browserProfiles = useBrowserProfilesStore()

onMounted(() => {
  encryption.fetchStatus()
  targets.fetchTargets()
  zipSettings.fetchZipSettings()
  // Fetched here (not lazily in BrowserProfileCard alone) so the Home dashboard's
  // "configuration snapshot" has real selection data ready without requiring a Settings visit first.
  browserProfiles.fetchProfiles(BrowserType.Edge)
  browserProfiles.fetchProfiles(BrowserType.Chrome)
})
</script>

<template>
  <div class="relative flex h-screen overflow-hidden bg-neutral-950">
    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_80%_50%_at_15%_-10%,rgba(45,212,191,0.16),transparent)]"
    />
    <AppSidebar class="relative" />
    <main class="relative min-w-0 flex-1 overflow-y-auto px-8 py-10 lg:px-12">
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
