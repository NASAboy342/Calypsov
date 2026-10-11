<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import type { BrowserProfile } from '@/services/browsersApi'
import IconChevronDown from '@/components/icons/IconChevronDown.vue'

const props = defineProps<{
  profiles: BrowserProfile[]
  modelValue: string | null
  disabled?: boolean
}>()

const emit = defineEmits<{ 'update:modelValue': [value: string | null] }>()

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const panel = ref<HTMLElement | null>(null)

// The panel is teleported to <body> (see template) so it's no longer a descendant of
// SettingsSection's overflow-hidden collapse wrapper, which was clipping its bottom edge
// whenever the list of profiles ran past the card's boundary. Since it's no longer positioned
// relative to the trigger via normal flow, its position has to be computed from the trigger's
// own bounding rect instead.
const panelStyle = ref({ top: '0px', left: '0px', width: '0px' })

const selected = computed(() => props.profiles.find((p) => p.id === props.modelValue) ?? null)

function initials(name: string): string {
  return name.trim().slice(0, 1).toUpperCase()
}

function updatePanelPosition() {
  if (!trigger.value) return
  const rect = trigger.value.getBoundingClientRect()
  panelStyle.value = {
    top: `${rect.bottom + 6}px`,
    left: `${rect.left}px`,
    width: `${rect.width}px`,
  }
}

async function toggle() {
  if (props.disabled) return
  open.value = !open.value
  if (open.value) {
    // Position is computed on open rather than kept in sync continuously — cheap since this
    // only runs once per open, and the scroll/resize listeners below keep it accurate afterward.
    await nextTick()
    updatePanelPosition()
  }
}

function choose(id: string | null) {
  emit('update:modelValue', id)
  open.value = false
}

function handleClickOutside(event: MouseEvent) {
  const target = event.target as Node
  // The panel is teleported outside `root`, so it needs its own containment check — otherwise
  // every click on a list item would register as "outside" and close the panel before `choose()`
  // could fire its own click handler.
  if (root.value?.contains(target) || panel.value?.contains(target)) return
  open.value = false
}

function handleReposition() {
  if (open.value) updatePanelPosition()
}

onMounted(() => {
  document.addEventListener('mousedown', handleClickOutside)
  // Capture phase so this also fires for scrolling inside a nested scroll container (e.g.
  // SettingsView's main area), not just the window itself.
  window.addEventListener('scroll', handleReposition, true)
  window.addEventListener('resize', handleReposition)
})
onBeforeUnmount(() => {
  document.removeEventListener('mousedown', handleClickOutside)
  window.removeEventListener('scroll', handleReposition, true)
  window.removeEventListener('resize', handleReposition)
})
</script>

<template>
  <div ref="root" class="relative">
    <button
      ref="trigger"
      type="button"
      :disabled="disabled"
      class="flex h-11 w-full items-center gap-2.5 rounded-lg border border-white/10 bg-neutral-950 px-3 text-left text-sm text-neutral-100 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none disabled:opacity-50"
      @click="toggle"
    >
      <template v-if="selected">
        <img
          v-if="selected.avatarUrl"
          :src="selected.avatarUrl"
          :alt="selected.name"
          class="h-6 w-6 shrink-0 rounded-full object-cover"
        />
        <span
          v-else
          class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-neutral-800 text-[11px] font-semibold text-neutral-400"
        >
          {{ initials(selected.name) }}
        </span>
        <span class="flex-1 truncate">{{ selected.name }}</span>
      </template>
      <span v-else class="flex-1 text-neutral-500">None</span>
      <IconChevronDown class="h-4 w-4 shrink-0 text-neutral-500" />
    </button>

    <Teleport to="body">
      <Transition
        enter-active-class="transition duration-150 ease-out"
        enter-from-class="origin-top scale-95 opacity-0"
        enter-to-class="origin-top scale-100 opacity-100"
        leave-active-class="transition duration-100 ease-in"
        leave-from-class="origin-top scale-100 opacity-100"
        leave-to-class="origin-top scale-95 opacity-0"
      >
        <ul
          v-if="open"
          ref="panel"
          :style="panelStyle"
          class="fixed z-50 max-h-64 overflow-y-auto rounded-lg border border-white/10 bg-neutral-900 p-1 shadow-lg"
        >
          <li
            class="flex cursor-pointer items-center gap-2.5 rounded-md px-2.5 py-2 text-sm text-neutral-300 hover:bg-white/5"
            @click="choose(null)"
          >
            <span class="flex h-6 w-6 shrink-0 items-center justify-center text-neutral-600">—</span>
            <span>None</span>
          </li>
          <li
            v-for="profile in profiles"
            :key="profile.id"
            class="flex cursor-pointer items-center gap-2.5 rounded-md px-2.5 py-2 text-sm text-neutral-200 hover:bg-white/5"
            @click="choose(profile.id)"
          >
            <img
              v-if="profile.avatarUrl"
              :src="profile.avatarUrl"
              :alt="profile.name"
              class="h-6 w-6 shrink-0 rounded-full object-cover"
            />
            <span
              v-else
              class="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-neutral-800 text-[11px] font-semibold text-neutral-400"
            >
              {{ initials(profile.name) }}
            </span>
            <span class="truncate">{{ profile.name }}</span>
          </li>
          <li v-if="profiles.length === 0" class="px-2.5 py-2 text-sm text-neutral-500">No profiles detected.</li>
        </ul>
      </Transition>
    </Teleport>
  </div>
</template>
