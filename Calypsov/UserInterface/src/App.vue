<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterView } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'

const encryption = useEncryptionStore()
const targets = useTargetsStore()

onMounted(() => {
  encryption.fetchStatus()
  targets.fetchTargets()
})
</script>

<template>
  <div class="relative flex h-screen overflow-hidden bg-neutral-950">
    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_80%_50%_at_15%_-10%,rgba(45,212,191,0.16),transparent)]"
    />
    <AppSidebar class="relative" />
    <main class="relative min-w-0 flex-1 overflow-y-auto px-8 py-10 lg:px-12">
      <RouterView />
    </main>
  </div>
</template>
