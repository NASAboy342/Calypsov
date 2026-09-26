<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useBrowserProfilesStore } from '@/stores/browserProfiles'
import { BrowserType } from '@/services/browsersApi'
import BrowserProfileSelect from '@/components/BrowserProfileSelect.vue'
import IconEdge from '@/components/icons/IconEdge.vue'
import IconChrome from '@/components/icons/IconChrome.vue'

const props = defineProps<{
  browser: BrowserType
}>()

const icons = { [BrowserType.Edge]: IconEdge, [BrowserType.Chrome]: IconChrome }
const labels = { [BrowserType.Edge]: 'Microsoft Edge', [BrowserType.Chrome]: 'Google Chrome' }

const store = useBrowserProfilesStore()
const state = computed(() => store.byBrowser[props.browser])
const icon = icons[props.browser]
const label = labels[props.browser]

onMounted(() => store.fetchProfiles(props.browser))

function handleSelect(profileId: string | null) {
  store.select(props.browser, profileId)
}
</script>

<template>
  <section class="rounded-2xl border border-white/10 bg-neutral-900 p-5">
    <h2 class="mb-3 flex items-center gap-2.5 text-[15px] font-semibold text-neutral-100">
      <component :is="icon" class="h-6 w-6 shrink-0" />
      {{ label }}
    </h2>

    <div class="flex flex-col gap-1.5">
      <label class="text-xs font-semibold text-neutral-400">Profile</label>
      <BrowserProfileSelect
        :profiles="state.profiles"
        :model-value="state.selectedId"
        :disabled="state.loading"
        @update:model-value="handleSelect"
      />
    </div>

    <p v-if="state.error" class="mt-3 text-xs text-red-400">{{ state.error }}</p>
  </section>
</template>
