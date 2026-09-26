import { reactive } from 'vue'
import { defineStore } from 'pinia'
import {
  BrowserType,
  getProfiles,
  getSelection,
  setSelection,
  type BrowserProfile,
} from '@/services/browsersApi'

interface BrowserState {
  profiles: BrowserProfile[]
  selectedId: string | null
  loading: boolean
  error: string | null
}

function createState(): BrowserState {
  return { profiles: [], selectedId: null, loading: true, error: null }
}

function toMessage(err: unknown, fallback: string): string {
  return err instanceof Error ? err.message : fallback
}

export const useBrowserProfilesStore = defineStore('browserProfiles', () => {
  const byBrowser = reactive<Record<BrowserType, BrowserState>>({
    [BrowserType.Edge]: createState(),
    [BrowserType.Chrome]: createState(),
  })

  /**
   * Fetches the profile list and the saved selection independently (not Promise.all) so a
   * failure in one — e.g. profile detection not being implemented yet — doesn't also hide a
   * successfully loaded selection.
   */
  async function fetchProfiles(browser: BrowserType) {
    const state = byBrowser[browser]
    state.loading = true
    state.error = null

    const [profilesResult, selectionResult] = await Promise.allSettled([
      getProfiles(browser),
      getSelection(browser),
    ])

    if (profilesResult.status === 'fulfilled') {
      state.profiles = profilesResult.value
    } else {
      state.error = toMessage(profilesResult.reason, `Could not load ${browser} profiles.`)
    }

    if (selectionResult.status === 'fulfilled') {
      state.selectedId = selectionResult.value.profileId
    } else if (!state.error) {
      state.error = toMessage(selectionResult.reason, `Could not load the selected ${browser} profile.`)
    }

    state.loading = false
  }

  /** Optimistically applies the selection, rolling back if the save fails. */
  async function select(browser: BrowserType, profileId: string | null) {
    const state = byBrowser[browser]
    const previous = state.selectedId
    state.selectedId = profileId
    state.error = null
    try {
      await setSelection(browser, profileId)
    } catch (err) {
      state.selectedId = previous
      state.error = toMessage(err, `Could not update the selected ${browser} profile.`)
    }
  }

  return { byBrowser, fetchProfiles, select }
})
