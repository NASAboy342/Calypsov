---
name: release
description: Cuts and publishes a new Calypsov release — from building the signed .dmg through tagging and creating the GitHub Release. Use when the user says things like "let's release", "publish a new version", "cut a release", "ship it", or asks how to release.
---

# release

Walks Calypsov from a clean `main` branch all the way to a published, non-draft GitHub Release that the in-app "Check for Updates" feature (`Calypsov/Services/AppUpdateService.cs`) will actually detect. See `Calypsov/PROJECT_README.md`'s "Versioning & Releases" convention for the background this skill implements.

**This process has real, public, hard-to-reverse steps** (pushing a git tag, publishing a GitHub Release). Treat steps 4 and 6 below as checkpoints: confirm with the user before running them rather than barreling through. Steps 1–3 and 5 are local/reversible and fine to run without stopping.

## Steps

1. **Preflight checks** — don't start a release on a dirty or unverified tree:
   ```bash
   git status --short --branch
   ```
   - Must be on `main` (or whatever branch the user actually releases from — ask if unclear) with a clean working tree. If there are uncommitted changes, stop and ask the user whether to commit, stash, or abort — don't decide for them.
   - Confirm the backend builds clean and the frontend type-checks clean before going further:
     ```bash
     dotnet build Calypsov/Calypsov.csproj
     (cd Calypsov/UserInterface && npx vue-tsc --noEmit)
     ```

2. **Confirm the version number**. Read the current version:
   ```bash
   dotnet msbuild Calypsov/Calypsov.csproj -getProperty:Version -nologo
   ```
   Ask the user to confirm this is the version they want to ship, or what to bump it to (MAJOR for breaking/incompatible changes, MINOR for new features, PATCH for fixes — this is a judgment call about the actual changes since the last release, so ask rather than guess). If it needs to change, edit the `<Version>` property in `Calypsov/Calypsov.csproj` and re-run the command above to confirm it took.

   Also check `git tag -l` doesn't already have a tag for this version — a duplicate tag push will fail.

3. **Build the `.dmg`**:
   ```bash
   scripts/package-macos.sh osx-arm64
   ```
   This reads the version live from the csproj (no separate version argument needed), builds the frontend, publishes a self-contained backend, assembles the `.app`, ad-hoc signs it, and produces a `.dmg` under `Deployed/MacOs/`. Takes a few minutes. Note what it did NOT do: proper Developer ID signing or notarization aren't set up for this project (see the script's own header comment) — the `.dmg` will show an "unidentified developer" Gatekeeper warning for anyone who downloads it. That's a known, already-discussed limitation, not something to silently fix here.

   If the user also needs an Intel build, repeat with `scripts/package-macos.sh osx-x64`.

   Confirm the output file exists and note its exact path/name before continuing:
   ```bash
   ls -la Deployed/MacOs/*.dmg
   ```

4. **Checkpoint — tag and push.** Show the user the exact tag name (`vX.Y.Z`, matching the csproj version) and ask them to confirm before running:
   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```
   A pushed tag is visible to anyone watching the public repo and is awkward (not impossible) to take back — this is worth a real confirmation, not a rhetorical one.

5. **Check `gh` auth** before trying to use it:
   ```bash
   gh auth status
   ```
   If not authenticated, tell the user to run `gh auth login` themselves (interactive browser flow — not something to drive non-interactively on their behalf) and wait for them to confirm it's done.

6. **Checkpoint — publish the GitHub Release.** Confirm the release notes/title with the user, then run:
   ```bash
   gh release create vX.Y.Z Deployed/MacOs/Calypsov-X.Y.Z-osx-arm64.dmg \
     --title "Calypsov X.Y.Z" \
     --notes "<release notes>"
   ```
   `gh release create` defaults to a real, non-draft, non-prerelease release — correct, since GitHub's "latest release" API (what `AppUpdateService.CheckForUpdateAsync()` calls) deliberately excludes drafts and pre-releases. **Never** pass `--draft` or `--prerelease` for a release meant to be picked up by the in-app update checker. If the user wants a draft first to review before going fully live, that's fine — just make sure the final publish step removes draft status, and don't treat a draft as "done" for this skill's purpose.

   If this is a second/later architecture (e.g. adding an Intel `.dmg` to an existing release), use `gh release upload vX.Y.Z <file>` instead of `create`.

7. **Verify**: tell the user to open the app's Settings → "About & Updates" → "Check for Updates" on a build running an *older* version than what was just published, and confirm it reports the new version with a working "View Release" link. (A build already at the just-released version will correctly report "You're on the latest version" instead — that's not a failure, just not the update-available path.)

## Notes

- The single source of truth for the version is `Calypsov.csproj`'s `<Version>` property — never hand-edit a version anywhere else (there is nowhere else; `scripts/package-macos.sh` and `AppUpdateService` both read it live).
- `UserInterface/package.json`'s `version` field is intentionally never touched by this process — nothing in the app reads it.
- If a release needs to be pulled after publishing, that's a `gh release delete vX.Y.Z` (and likely `git push --delete origin vX.Y.Z` for the tag) — destructive, so confirm explicitly with the user and don't fold it into a routine release run.
