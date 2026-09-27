<script setup lang="ts">
defineProps<{
  modelValue: boolean
  disabled?: boolean
}>()

defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>

<template>
  <button
    type="button"
    role="switch"
    :aria-checked="modelValue"
    :disabled="disabled"
    class="relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors duration-200 focus:ring-2 focus:ring-teal-400/40 focus:outline-none disabled:cursor-wait disabled:opacity-60"
    :class="modelValue ? 'bg-teal-400' : 'bg-neutral-700'"
    @click="$emit('update:modelValue', !modelValue)"
  >
    <span
      class="inline-block h-4 w-4 transform rounded-full bg-neutral-50 transition-transform duration-200"
      :class="modelValue ? 'translate-x-6' : 'translate-x-1'"
    />
  </button>
</template>
