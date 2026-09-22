<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { useEncryptionStore } from '@/stores/encryption'
import { useTargetsStore } from '@/stores/targets'

const encryption = useEncryptionStore()
const targets = useTargetsStore()
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <div class="flex flex-col gap-12 lg:flex-row lg:items-center lg:justify-between">
      <div class="max-w-xl">
        <p class="mb-4 text-xs font-semibold tracking-[0.2em] text-teal-400 uppercase">
          Protection status
        </p>

        <h1 class="text-4xl font-extrabold tracking-tight text-neutral-50 sm:text-5xl">
          Encryption is
          <span
            class="bg-gradient-to-r bg-clip-text text-transparent"
            :class="encryption.enabled ? 'from-teal-300 to-indigo-400' : 'from-neutral-500 to-neutral-600'"
          >
            {{ encryption.enabled ? 'on' : 'off' }}
          </span>
        </h1>

        <p class="mt-6 text-base leading-relaxed text-neutral-400">
          <template v-if="targets.count === 0">
            No files or folders are configured yet. Add some in Settings to start protecting them.
          </template>
          <template v-else>
            {{ encryption.enabled ? 'Actively protecting' : 'Ready to protect' }}
            {{ targets.count }} {{ targets.count === 1 ? 'item' : 'items' }} configured in Settings.
          </template>
        </p>

        <div class="mt-8 flex flex-wrap items-center gap-4">
          <RouterLink
            v-if="targets.count === 0"
            to="/settings"
            class="rounded-full bg-teal-400 px-6 py-3 text-sm font-semibold text-neutral-950 transition-colors hover:bg-teal-300"
          >
            Add files or folders
          </RouterLink>
          <RouterLink
            v-else
            to="/settings"
            class="rounded-full border border-white/15 px-6 py-3 text-sm font-semibold text-neutral-200 transition-colors hover:bg-white/5"
          >
            Manage in Settings
          </RouterLink>
        </div>
      </div>

      <div class="flex flex-col items-center gap-4">
        <button
          type="button"
          role="switch"
          :aria-checked="encryption.enabled"
          aria-label="Toggle encryption"
          class="relative h-24 w-24 shrink-0 rounded-full border-2 bg-neutral-900 transition-all duration-300"
          :class="
            encryption.enabled
              ? 'border-teal-400 shadow-[0_0_40px_-6px_rgba(45,212,191,0.65)]'
              : 'border-neutral-700'
          "
          @click="encryption.toggle()"
        >
          <span
            class="absolute inset-0 flex justify-center transition-transform duration-300"
            :class="encryption.enabled ? 'rotate-0' : 'rotate-180'"
          >
            <span
              class="mt-3 h-7 w-1 rounded-full transition-colors duration-300"
              :class="encryption.enabled ? 'bg-teal-400 shadow-[0_0_10px_rgba(45,212,191,0.9)]' : 'bg-neutral-600'"
            />
          </span>
          <span
            class="absolute inset-0 m-auto h-3 w-3 rounded-full transition-colors duration-300"
            :class="encryption.enabled ? 'bg-teal-400' : 'bg-neutral-700'"
          />
        </button>

        <p class="text-sm text-neutral-400">
          Encrypt:
          <strong class="font-semibold" :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-200'">
            {{ encryption.enabled ? 'On' : 'Off' }}
          </strong>
        </p>
      </div>
    </div>

    <div class="mt-16 grid grid-cols-2 gap-8 border-t border-white/10 pt-8 sm:grid-cols-4">
      <div>
        <p class="text-3xl font-bold text-neutral-50">{{ targets.folders.length }}</p>
        <p class="mt-1 text-sm text-neutral-500">Folders protected</p>
      </div>
      <div>
        <p class="text-3xl font-bold text-neutral-50">{{ targets.files.length }}</p>
        <p class="mt-1 text-sm text-neutral-500">Files protected</p>
      </div>
      <div>
        <p class="text-3xl font-bold text-neutral-50">{{ targets.count }}</p>
        <p class="mt-1 text-sm text-neutral-500">Total items configured</p>
      </div>
      <div>
        <p class="text-3xl font-bold" :class="encryption.enabled ? 'text-teal-300' : 'text-neutral-50'">
          {{ encryption.enabled ? 'ON' : 'OFF' }}
        </p>
        <p class="mt-1 text-sm text-neutral-500">Current status</p>
      </div>
    </div>
  </div>
</template>
