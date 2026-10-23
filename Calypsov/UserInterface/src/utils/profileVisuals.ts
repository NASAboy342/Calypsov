import type { Component } from 'vue'
import IconShield from '@/components/icons/IconShield.vue'
import IconFolder from '@/components/icons/IconFolder.vue'
import IconHome from '@/components/icons/IconHome.vue'
import IconBolt from '@/components/icons/IconBolt.vue'
import IconGlobe from '@/components/icons/IconGlobe.vue'
import IconLock from '@/components/icons/IconLock.vue'
import { EXTRA_PROFILE_ICONS } from '@/utils/profileIconGlyphs'

/**
 * A protection profile's color/icon are opaque strings as far as the backend is concerned — it
 * just stores and returns whatever's sent. The actual fixed palette/icon set is owned here, on the
 * frontend, reusing existing icon components rather than adding new art just for this.
 */
export interface ProfileIconOption {
  key: string
  icon: Component
}

export const PROFILE_ICON_OPTIONS: ProfileIconOption[] = [
  { key: 'shield', icon: IconShield },
  { key: 'folder', icon: IconFolder },
  { key: 'home', icon: IconHome },
  { key: 'bolt', icon: IconBolt },
  { key: 'globe', icon: IconGlobe },
  { key: 'lock', icon: IconLock },
  ...EXTRA_PROFILE_ICONS,
]

const ICON_MAP = new Map(PROFILE_ICON_OPTIONS.map((option) => [option.key, option.icon]))

/** Falls back to the shield icon for an unrecognized key (e.g. the backend's own "shield" default,
 * or a key from a future version this build doesn't know about yet). */
export function resolveProfileIcon(key: string): Component {
  return ICON_MAP.get(key) ?? IconShield
}

export interface ProfileColorOption {
  key: string
  /** Solid swatch shown in the color picker. */
  swatchClass: string
  /** Badge/chip background. */
  bgClass: string
  /** Badge/chip icon + text color. */
  textClass: string
  /** Selected-swatch ring accent. */
  ringClass: string
}

export const PROFILE_COLOR_OPTIONS: ProfileColorOption[] = [
  { key: 'teal', swatchClass: 'bg-teal-400', bgClass: 'bg-teal-400/10', textClass: 'text-teal-300', ringClass: 'ring-teal-400' },
  { key: 'blue', swatchClass: 'bg-blue-400', bgClass: 'bg-blue-400/10', textClass: 'text-blue-300', ringClass: 'ring-blue-400' },
  { key: 'violet', swatchClass: 'bg-violet-400', bgClass: 'bg-violet-400/10', textClass: 'text-violet-300', ringClass: 'ring-violet-400' },
  { key: 'rose', swatchClass: 'bg-rose-400', bgClass: 'bg-rose-400/10', textClass: 'text-rose-300', ringClass: 'ring-rose-400' },
  { key: 'amber', swatchClass: 'bg-amber-400', bgClass: 'bg-amber-400/10', textClass: 'text-amber-300', ringClass: 'ring-amber-400' },
  { key: 'emerald', swatchClass: 'bg-emerald-400', bgClass: 'bg-emerald-400/10', textClass: 'text-emerald-300', ringClass: 'ring-emerald-400' },
]

const COLOR_MAP = new Map(PROFILE_COLOR_OPTIONS.map((option) => [option.key, option]))

/** Falls back to the first palette entry for an unrecognized key. */
export function resolveProfileColor(key: string): ProfileColorOption {
  return COLOR_MAP.get(key) ?? PROFILE_COLOR_OPTIONS[0]
}
