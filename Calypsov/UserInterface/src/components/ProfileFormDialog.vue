<script setup lang="ts">
import { ref, watch } from 'vue'
import { PROFILE_COLOR_OPTIONS, PROFILE_ICON_OPTIONS } from '@/utils/profileVisuals'

const props = defineProps<{
  open: boolean
  mode: 'create' | 'edit'
  initialName?: string
  initialColor?: string
  initialIcon?: string
  /** An error from the create/update request itself (e.g. a backend validation failure) — shown
   * alongside the dialog's own local "name is required" check. */
  serverError?: string | null
}>()

const emit = defineEmits<{
  submit: [{ name: string; color: string; icon: string }]
  cancel: []
}>()

const name = ref('')
const color = ref(PROFILE_COLOR_OPTIONS[0].key)
const icon = ref(PROFILE_ICON_OPTIONS[0].key)
const error = ref('')

// Reset to the right starting values every time the dialog opens, rather than on every prop
// change — avoids the fields flashing/resetting while the dialog is already open and visible.
watch(
  () => props.open,
  (isOpen) => {
    if (!isOpen) return
    name.value = props.initialName ?? ''
    color.value = props.initialColor ?? PROFILE_COLOR_OPTIONS[0].key
    icon.value = props.initialIcon ?? PROFILE_ICON_OPTIONS[0].key
    error.value = ''
  },
)

function handleSubmit() {
  if (!name.value.trim()) {
    error.value = 'Give this profile a name.'
    return
  }
  emit('submit', { name: name.value.trim(), color: color.value, icon: icon.value })
}
</script>

<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="opacity-0"
      enter-to-class="opacity-100"
      leave-active-class="transition duration-100 ease-in"
      leave-from-class="opacity-100"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 px-4"
        @click.self="emit('cancel')"
      >
        <div class="w-full max-w-sm rounded-2xl border border-white/10 bg-neutral-900 p-5 shadow-xl">
          <h2 class="text-sm font-semibold text-neutral-100">
            {{ mode === 'create' ? 'Create Profile' : 'Edit Profile' }}
          </h2>

          <div class="mt-4 flex flex-col gap-4">
            <div>
              <label class="mb-1.5 block text-xs font-semibold text-neutral-400">Name</label>
              <input
                v-model="name"
                type="text"
                placeholder="e.g. Work"
                class="h-10 w-full rounded-lg border border-white/10 bg-neutral-950 px-3 text-sm text-neutral-100 placeholder:text-neutral-600 focus:border-teal-400 focus:ring-2 focus:ring-teal-400/40 focus:outline-none"
                @keyup.enter="handleSubmit"
              />
            </div>

            <div>
              <label class="mb-1.5 block text-xs font-semibold text-neutral-400">Color</label>
              <div class="flex flex-wrap gap-2">
                <button
                  v-for="option in PROFILE_COLOR_OPTIONS"
                  :key="option.key"
                  type="button"
                  :aria-label="option.key"
                  class="h-8 w-8 rounded-full transition-transform"
                  :class="[
                    option.swatchClass,
                    color === option.key
                      ? `ring-2 ring-offset-2 ring-offset-neutral-900 ${option.ringClass}`
                      : 'hover:scale-110',
                  ]"
                  @click="color = option.key"
                />
              </div>
            </div>

            <div>
              <label class="mb-1.5 block text-xs font-semibold text-neutral-400">Icon</label>
              <div class="flex max-h-40 flex-wrap gap-2 overflow-y-auto">
                <button
                  v-for="option in PROFILE_ICON_OPTIONS"
                  :key="option.key"
                  type="button"
                  :aria-label="option.key"
                  class="flex h-9 w-9 items-center justify-center rounded-lg border transition-colors"
                  :class="
                    icon === option.key
                      ? 'border-teal-400 bg-teal-400/10 text-teal-300'
                      : 'border-white/10 text-neutral-400 hover:bg-white/5'
                  "
                  @click="icon = option.key"
                >
                  <component :is="option.icon" class="h-4.5 w-4.5" />
                </button>
              </div>
            </div>

            <p v-if="error || serverError" class="text-sm text-red-400">{{ error || serverError }}</p>
          </div>

          <div class="mt-5 flex justify-end gap-2.5">
            <button
              type="button"
              class="h-9 rounded-lg px-4 text-sm font-medium text-neutral-300 transition-colors hover:bg-white/5"
              @click="emit('cancel')"
            >
              Cancel
            </button>
            <button
              type="button"
              class="h-9 rounded-lg bg-teal-400 px-4 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
              @click="handleSubmit"
            >
              {{ mode === 'create' ? 'Create' : 'Save' }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>
