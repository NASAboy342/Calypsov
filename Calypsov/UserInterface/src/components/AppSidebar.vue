<script setup lang="ts">
import { RouterLink } from 'vue-router'
import IconHome from './icons/IconHome.vue'
import IconSettings from './icons/IconSettings.vue'

const linkClass =
  'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-neutral-400 transition-colors hover:bg-white/5 hover:text-neutral-100'
const activeLinkClass = 'bg-teal-400/10 text-teal-300'
</script>

<template>
  <aside class="flex w-60 shrink-0 flex-col gap-8 border-r border-white/10 bg-neutral-950 px-4 py-7">
    <div class="flex items-center gap-2.5 px-2">
      <span
        class="flex h-8 w-8 items-center justify-center rounded-md bg-teal-400 text-sm font-bold text-neutral-950"
      >
        C
      </span>
      <span class="text-base font-semibold tracking-tight text-neutral-100">Calypsov</span>
    </div>

    <nav class="flex flex-col gap-1">
      <RouterLink to="/" :class="linkClass" :active-class="activeLinkClass">
        <IconHome class="h-[18px] w-[18px] shrink-0" />
        <span>Home</span>
      </RouterLink>
      <RouterLink to="/settings" :class="linkClass" :active-class="activeLinkClass">
        <IconSettings class="h-[18px] w-[18px] shrink-0" />
        <span>Settings</span>
      </RouterLink>
    </nav>
  </aside>
</template>
