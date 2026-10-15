---
name: release
description: Cuts and publishes a new Calypsov release — commits/tags/pushes a version, then watches the GitHub Actions pipeline build the macOS .dmg + Windows installer and publish the GitHub Release. Use when the user says things like "let's release", "publish a new version", "cut a release", "ship it", or asks how to release.
---

# release

Walks Calypsov from a clean `main` branch to a published, non-draft GitHub Release that the in-app "Check for Updates" feature (`Calypsov/Services/AppUpdateService.cs`) will actually detect. Pushing a version tag triggers `.github/workflows/release.yml`, which builds the macOS `.dmg` (on a real `macos-latest` runner) and the Windows installer (on a real `windows-latest` runner) in parallel, then publishes both as one GitHub Release automatically — there's no local packaging step in this flow. See `Calypsov/PROJECT_README.md`'s "Versioning & Releases" convention for the background.

**Pushing the tag is the one real, public, hard-to-reverse step** — it kicks off the entire pipeline, including a public release at the end. Treat step 3 below as a checkpoint: confirm with the user before running it. Steps 1–2 and 4–5 are local/read-only and fine to run without stopping.

## Steps

1. **Preflight checks** — don't start a release on a dirty or unverified tree:
   ```bash
   git status --short --branch
   ```
   - Must be on `main` (or whatever branch the user actually releases from — ask if unclear) with a clean working tree. If there are uncommitted changes, stop and ask the user whether to commit, stash, or abort — don't decide for them.
   - Confirm the backend builds clean and the frontend type-checks clean before going further (the CI pipeline runs the equivalent build steps, but catching a break locally first is faster than waiting on two parallel CI runners to fail):
     ```bash
     dotnet build Calypsov/Calypsov.csproj
     (cd Calypsov/UserInterface && npx vue-tsc --noEmit)
     ```

2. **Confirm the version number**. Read the current version:
   ```bash
   dotnet msbuild Calypsov/Calypsov.csproj -getProperty:Version -nologo
   ```
   Ask the user to confirm this is the version they want to ship, or what to bump it to (MAJOR for breaking/incompatible changes, MINOR for new features, PATCH for fixes — this is a judgment call about the actual changes since the last release, so ask rather than guess). If it needs to change, edit the `<Version>` property in `Calypsov/Calypsov.csproj`, commit that change, and re-run the command above to confirm it took.

   Also check `git tag -l` doesn't already have a tag for this version — a duplicate tag push will fail (and the `release` job in CI only runs for an actual tag push, so a bad tag can't be easily "redone" without deleting it first).

3. **Checkpoint — tag and push.** Show the user the exact tag name (`vX.Y.Z`, matching the csproj version) and ask them to confirm before running:
   ```bash
   git push origin main   # only if step 2 made a commit (e.g. a version bump)
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```
   This is what starts the whole pipeline — both builds and the eventual public release. Worth a real confirmation, not a rhetorical one.

4. **Watch the CI run**:
   ```bash
   gh run list --workflow=release.yml --limit 1
   gh run watch <run-id>
   ```
   If it fails, don't blindly re-run — fetch the failing job's log and diagnose:
   ```bash
   gh run view <run-id> --log-failed
   ```
   A failed run after a tag push leaves no release behind (the `release` job only runs once both builds succeed) — fix the underlying issue, then either delete and re-push the tag, or push a new patch tag, whichever the user prefers.

5. **Verify the release**:
   ```bash
   gh release view vX.Y.Z
   ```
   Confirm both `Calypsov-X.Y.Z-osx-arm64.dmg` and `Calypsov-X.Y.Z-win-x64-setup.exe` are attached and the release is non-draft/non-prerelease (the workflow's `gh release create` always produces both, per the rule below). The release notes are auto-generated from commits/PRs (`--generate-notes`) — offer to refine them if the user wants more descriptive notes:
   ```bash
   gh release edit vX.Y.Z --notes "<better notes>"
   ```

6. **Verify in-app**: tell the user to open the app's Settings → "About & Updates" → "Check for Updates" on a build running an *older* version than what was just published, and confirm it reports the new version with a working "View Release" link. (A build already at the just-released version will correctly report "You're on the latest version" instead — that's not a failure, just not the update-available path.)

## Testing the pipeline without cutting a release

The workflow also has a `workflow_dispatch` trigger, so the two build jobs can be exercised on demand without pushing a tag or creating a release (the `release` job only runs `if: startsWith(github.ref, 'refs/tags/')`, so a manual dispatch run never publishes anything):
```bash
gh workflow run release.yml
gh run watch <run-id>
```
Use this to validate a change to the workflow, `scripts/package-windows.ps1`, or `scripts/windows-installer.iss` before trusting a real tag push to it. The built artifacts can be downloaded from the run for inspection via `gh run download <run-id>`.

## Notes

- The single source of truth for the version is `Calypsov.csproj`'s `<Version>` property — never hand-edit a version anywhere else. `scripts/package-macos.sh`, `scripts/package-windows.ps1`, and `AppUpdateService` all read it live.
- `UserInterface/package.json`'s `version` field is intentionally never touched by this process — nothing in the app reads it.
- Neither build is code-signed (no Apple Developer Program or Windows code-signing certificate configured) — the `.dmg` shows an "unidentified developer" Gatekeeper warning, and the `.exe` installer triggers Windows SmartScreen. Both are known, already-discussed limitations, not bugs to silently try to fix in this skill.
- `Deployed/` is gitignored — CI builds fresh artifacts every run and uploads them directly as release assets without ever touching the repo. Don't commit anything under `Deployed/` by hand.
- If a release needs to be pulled after publishing, that's a `gh release delete vX.Y.Z` (and likely `git push --delete origin vX.Y.Z` for the tag) — destructive, so confirm explicitly with the user and don't fold it into a routine release run.
