<script setup lang="ts">
import IconCheck from '@/components/icons/IconCheck.vue'

defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue: boolean
    /** Tri-state "some but not all" look, e.g. for a "select all" control. */
    indeterminate?: boolean
    disabled?: boolean
  }>(),
  { indeterminate: false, disabled: false },
)

defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>

<template>
  <!--
    Native checkboxes render via the OS's own control on a Photino webview (e.g. WebKit on macOS),
    which ignores author `background-color`/`border` entirely — that's why a plain Tailwind-classed
    checkbox was still showing a stark white box regardless of the dark theme. `appearance-none`
    drops the native box so our own background/border actually apply, and the checkmark/dash are
    drawn ourselves (absolutely positioned, toggled via `peer-checked`/`peer-indeterminate`) since
    appearance-none also removes the native checkmark.
  -->
  <span class="relative inline-flex h-4 w-4 shrink-0 items-center justify-center">
    <input
      type="checkbox"
      :checked="modelValue"
      :indeterminate="indeterminate"
      :disabled="disabled"
      class="peer h-4 w-4 shrink-0 appearance-none rounded border border-white/20 bg-neutral-950 transition-colors checked:border-teal-400 checked:bg-teal-400 indeterminate:border-teal-400 indeterminate:bg-teal-400 focus-visible:ring-2 focus-visible:ring-teal-400/40 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
      v-bind="$attrs"
      @change="$emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <IconCheck
      class="pointer-events-none absolute h-3 w-3 text-neutral-950 opacity-0 peer-checked:opacity-100 peer-indeterminate:opacity-0"
    />
    <span
      class="pointer-events-none absolute h-0.5 w-2 rounded-full bg-neutral-950 opacity-0 peer-indeterminate:opacity-100"
    />
  </span>
</template>
