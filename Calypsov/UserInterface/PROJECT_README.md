# Frontend Project Doc (Calypsov/UserInterface)

Maintained by the `front-end-enhance` skill. Read this before planning any frontend change; update the Feature log after implementing one. This is a living architecture/convention doc, separate from `README.md` (which only covers the default Vue/Vite scaffold commands).

## Stack
- Vue 3 (`<script setup>` + Composition API), TypeScript, Vite.
- Pinia for state, using the setup-store style (`defineStore('name', () => {...})`), not the options style.
- vue-router with **hash history** (`createWebHashHistory`) — required because Photino serves a static `index.html` rather than a server that can rewrite arbitrary paths, so history-mode routing would 404 on refresh or a deep link.
- Tailwind CSS v4 (`@import 'tailwindcss'` in `src/assets/main.css`), dark theme only (`bg-neutral-950` / `text-neutral-100` on `body`), Inter font stack.
- ESLint + Prettier configured (`.eslintrc.cjs`, `.prettierrc.json`); Vitest for unit tests (`npm run test:unit`).

## Structure
- `src/main.ts` — app bootstrap (Pinia + router installed here).
- `src/App.vue` — shell: fixed sidebar (`AppSidebar`) + `<RouterView>` main area, full-height flex layout.
- `src/views/` — one component per route (`HomeView.vue`, `SettingsView.vue`), registered in `src/router/index.ts`.
- `src/components/` — reusable UI pieces (e.g. `AppSidebar.vue`, `components/icons/`).
- `src/stores/` — one Pinia store per domain concern (`encryption.ts`, `targets.ts`), each wrapping the matching service module and exposing `loading`/`error` refs alongside the data.
- `src/services/` — one `*Api.ts` file per backend feature group (mirrors `Calypsov/Api/*Endpoints.cs` 1:1), plus `httpClient.ts` (the shared `requestJson<T>` wrapper) and `dialogApi.ts` for the native picker endpoints.
- `src/router/index.ts` — route table.

## Conventions
- **API calls**: never call `fetch` directly from a component or store. Add or extend a function in the matching `src/services/*Api.ts` file that calls `requestJson<T>(url, init)` from `httpClient.ts`. `requestJson` already handles JSON headers, turns a non-2xx response into a thrown `Error` carrying the backend's `message`, and returns `undefined` for a 204.
- **State**: one Pinia setup-store per domain, following `stores/encryption.ts` — plain `ref`s for data/`loading`/`error` (and any in-flight flag, e.g. `toggling`), async actions that set `loading`, clear `error`, call the service function, and populate the ref, with the same try/catch/finally shape in every action.
- **Components**: `<script setup lang="ts">`, Tailwind utility classes for styling (no separate CSS files per component), dark theme only — don't introduce light-mode variants or a new color system without discussion.
- **Routing**: add new pages as a route in `router/index.ts` pointing at a lazy `() => import(...)` for anything beyond the initial route, matching how `SettingsView` is registered.

## UI style / theme
- Background `neutral-950`, text `neutral-100`; a teal radial gradient (`rgba(45,212,191,...)`) glows behind the sidebar — see `App.vue`.
- Font: Inter, falling back through the standard system stack.
- No component library — everything is hand-built with Tailwind utilities.

## Feature log
- *(baseline)* Encryption toggle + folder/file target management (Home view), native file/folder picker integration, Settings view scaffold.
- Real progress bar + "what's being zipped" message on the Home screen while the encryption toggle is in flight (`EncryptionProgressBar.vue`). `POST /toggle` still blocks until done (unchanged), so `stores/encryption.ts`'s `toggle()` starts a `setInterval` polling the new `GET /api/encryption/progress` (`services/encryptionApi.ts`'s `getProgress()`) every 300ms *concurrently* with the still-pending toggle request, exposing the latest snapshot as a new `progress` ref; the interval is cleared and `progress` reset to `null` in `toggle()`'s `finally`. `EncryptionProgressBar` takes `progress: EncryptionProgress | null` + an `actionLabel` prop ("Zipping"/"Restoring", computed in `HomeView.vue` from `encryption.enabled`'s *pre*-toggle value, since the store doesn't flip it until the request resolves) — it shows a determinate width-based bar (amber, matching the toggle switch's existing "in progress" color) once a real `total` is known, an indeterminate pulse before that, and the current file's real path as the message. Verified live end-to-end (not just build-clean): added a real multi-file folder target, clicked the toggle, and screenshotted mid-flight — saw real percentages and real file paths advancing, not a canned animation.
- Browser profile cards on the Settings page (`BrowserProfileCard.vue` × 2, one per browser) showing a brand icon plus a profile selector. Since a native `<select>` can't render avatar images, the selector is a small hand-built listbox (`BrowserProfileSelect.vue` — button + absolutely-positioned panel, closes on outside click) rather than the usual native `<select>`; reuse it for any future "pick from a list of photo+name items" need instead of building another one. Backed by `services/browsersApi.ts` and `stores/browserProfiles.ts` (one Pinia store keyed by `BrowserType`, mirroring `stores/targets.ts`'s per-category-computed style but as a reactive map since there are two independent browsers). Selection persists via the new `/api/browsers/{browser}/selection` endpoint and defaults to "None". Profile *listing* (`/api/browsers/{browser}/profiles`) is backed by a real endpoint, but the backend's `GetProfiles` is a deliberate stub (throws `NotImplementedException`) pending manual implementation of OS-level Edge/Chrome profile detection — until then, each card shows that as an inline error and "No profiles detected." `IconEdge.vue`/`IconChrome.vue` are full-color brand marks (an intentional exception to the `currentColor` line-icon convention, since these need to be recognizable in their actual brand colors); `IconEdge` is a stylized approximation, not a pixel-accurate trace of the logo.
