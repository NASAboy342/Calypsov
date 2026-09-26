---
name: front-end-enhance
description: Use whenever the user wants to implement a new feature or enhance/modify anything in Calypsov's frontend (the Vue 3 + Vite + TypeScript app in Calypsov/UserInterface) — new views, components, pages, forms, sidebar items, or changes to existing UI behavior. Triggers on requests like "add a way to X in the UI", "build a settings panel for Y", "enhance the sidebar", or "the home page needs Z", even if the user doesn't say "frontend" explicitly — anything about what the user sees or clicks counts. Always read (or create) Calypsov/UserInterface/PROJECT_README.md before planning, and hand off to the back-end-enhance skill if the feature needs new backend support.
---

# front-end-enhance

Codifies a repeatable way to add or enhance Calypsov frontend features so every iteration follows the same convention and flow: minimal diff, consistent with existing code, and easy for the user to review and hand-tune afterward. Don't skip steps even for a small-sounding ask — the point of this skill is that *every* change goes through the same loop, so the codebase and the project doc never drift apart.

## Workflow

1. **Read the project doc**: `Calypsov/UserInterface/PROJECT_README.md`. It has the current stack, folder structure, conventions (how API calls are made, state pattern, routing, styling), UI theme, and a running feature log.
   - If it doesn't exist yet, create it first — see "Creating the project doc" below. Don't skip this by guessing at conventions from general Vue knowledge; this project has specific patterns (setup-store Pinia, hash-history routing, a shared `requestJson` wrapper) that a generic approach would miss.
   - If it exists but looks stale against the actual code (references a file or pattern that's gone), refresh the relevant section before relying on it — a stale doc is worse than no doc, since it actively misleads the plan.

2. **Plan** the feature against what the doc says. If the prompt is thin on detail (e.g. "add a way to exclude certain file types"), make the placement/UX call yourself, consistent with existing patterns, rather than stopping to ask — the user specifically wants this skill to move implementation forward on its own and expects to adjust the result afterward. Only pause to ask if the request is ambiguous about *what* to build, not *how*.

3. **Implement**:
   - Make the smallest diff that fully satisfies the request. Don't refactor unrelated code, rename things in passing, or introduce a new pattern/library where an existing one already covers the need (e.g. extend `requestJson`/an existing `*Api.ts` file rather than reaching for axios or raw `fetch`; add a Pinia setup-store like the existing ones rather than inventing new state machinery).
   - Match existing naming, file placement, and style exactly as documented in the project doc (Tailwind utility classes and dark theme already in use, `<script setup>` components, the try/catch/finally shape in store actions).
   - Optimize for a human reading and maintaining this later, not for cleverness — this is exactly the code the user asked for so they can go back in and customize it by hand.

4. **Backend needs** — if the feature needs data or an action from the backend:
   - First check whether an existing endpoint (see the matching `src/services/*Api.ts` file and its counterpart in `Calypsov/Api/*Endpoints.cs`) already covers it, or could reasonably be extended (e.g. an extra optional field on an existing request). Reuse over adding new surface area — a smaller backend footprint is easier for the user to maintain.
   - If nothing existing fits, decide the contract you need (route, method, request/response shape) and wire the frontend against it: add the service function and types as if the endpoint already existed. Then invoke the `back-end-enhance` skill directly (via the Skill tool) with a precise spec of that contract, so the backend gets built to match what the frontend expects rather than the other way around.
   - Until that backend work lands, make the frontend usable and demoable on its own by mocking the new service function's response — return realistic fixture data, and mark it unmistakably (e.g. a `// TODO(back-end-enhance): replace mock once <endpoint> exists` comment right at the mock). Never let a mocked response look indistinguishable from a real one; the user needs to be able to find and rip it out later.

5. **Update the project doc** — after implementing, go back to `Calypsov/UserInterface/PROJECT_README.md` and append an entry to the Feature log: what was added, where it lives, and its state (fully wired, or pending/mocked pending backend work). Keep entries short and factual, one per feature — this log is what makes the *next* iteration fast, so don't skip it even when the change felt small.

## Creating the project doc (if missing)

Explore the actual current code rather than assuming — conventions may have shifted since this skill was last used. At minimum read `package.json`, `src/main.ts`, `src/App.vue`, `src/router/index.ts`, the Tailwind entry in `src/assets`, and skim one file each from `src/views`, `src/components`, `src/stores`, `src/services`. Structure the doc as:

```markdown
# Frontend Project Doc

## Stack
(Vue/Vite/TS versions, Pinia, vue-router, Tailwind version, any UI kit in use)

## Structure
(what belongs in views/ vs components/ vs stores/ vs services/ vs router/, and how each is named)

## Conventions
(how API calls are made, the state-management pattern, the routing pattern and why, component authoring style)

## UI style / theme
(color palette, dark/light mode, font, spacing, any recurring UI motifs)

## Feature log
(empty to start — this skill appends one entry per feature going forward)
```

Cite real file paths and concrete examples pulled from the code, not generic Vue advice — the doc's whole value is being specific to *this* codebase.
