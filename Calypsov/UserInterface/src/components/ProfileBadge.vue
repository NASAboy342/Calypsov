<script setup lang="ts">
import { computed } from 'vue'
import { resolveProfileColor, resolveProfileIcon } from '@/utils/profileVisuals'

const props = withDefaults(
  defineProps<{
    color: string
    icon: string
    size?: 'sm' | 'md'
  }>(),
  { size: 'md' },
)

const colorOption = computed(() => resolveProfileColor(props.color))
const IconComponent = computed(() => resolveProfileIcon(props.icon))
const sizeClass = computed(() => (props.size === 'sm' ? 'h-6 w-6' : 'h-9 w-9'))
const iconSizeClass = computed(() => (props.size === 'sm' ? 'h-3.5 w-3.5' : 'h-4.5 w-4.5'))
</script>

<template>
  <span class="flex shrink-0 items-center justify-center rounded-full" :class="[sizeClass, colorOption.bgClass]">
    <component :is="IconComponent" :class="[iconSizeClass, colorOption.textClass]" />
  </span>
</template>
