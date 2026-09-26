# Backend Project Doc (Calypsov)

Maintained by the `back-end-enhance` skill. Read this before planning any backend change; update the Feature log after implementing one. This is a living architecture/convention doc, separate from `ReadMe.md` (which only covers build/run setup).

## Stack
- .NET 8, C#, `OutputType: Exe` — this runs as a native apphost (`bin/Debug/net8.0/Calypsov`), not `dotnet Calypsov.dll`.
- Photino.NET + Photino.NET.Server — hosts an ASP.NET minimal-API backend and a native OS window in one process.
- Newtonsoft.Json for settings persistence (`JsonConvert`); minimal-API results elsewhere use `Results.*`.
- Two run modes wired in `Program.cs` via `Program.IsDebugMode` (`#if DEBUG`):
  - Debug: window loads `http://localhost:5173` (the Vite dev server — see the `run-test` skill).
  - Release: window loads the built `wwwroot` bundle (embedded via `Resources/wwwroot`), with a cache-busting query string on load.

## Structure
- `Program.cs` — composition root. Creates the Photino static file server, constructs services, calls each feature's `Map<Feature>Endpoints(...)` extension method, then creates and shows the `PhotinoWindow`.
- `Api/` — one static class per feature (e.g. `EncryptionEndpoints.cs`, `DialogEndpoints.cs`). Each exposes a single `Map<Feature>Endpoints(this WebApplication app, ...)` extension method that groups its routes under `/api/<feature>` via `app.MapGroup(...)`.
- `Services/` — one interface per feature (`I<Feature>Service`) plus one or more implementations (e.g. `StorageEncryptionSettingsService` persists to a JSON file under the OS app-data folder; `MemoryEncryptionSettingsService` is an in-memory stand-in). `Program.cs` is the only place that decides which implementation is live — swapping implementations never touches the API surface.
- `Models/` — plain C# records/classes: response DTOs (`EncryptionStatusResponse`, `PickPathsResponse`, `ErrorResponse`), request DTOs (`AddTargetRequest`), domain types (`EncryptionTarget`, `AppSetting`, `EnumTargetCategory` + its JSON converter).
- `Helpers/` — small stateless utilities used by services (e.g. `Zip.cs` zips/unzips target folders and files).

## Conventions
- **Endpoint pattern**: `app.MapGroup("/api/<feature>")`, then `MapGet/MapPost/MapDelete` with lambda bodies. Bodies that can fail are wrapped in a local `Try(Func<IResult> body)` helper (see `EncryptionEndpoints.cs`), which turns `ArgumentException` → 400, `InvalidOperationException` → 409, anything else → 500, all as a JSON `ErrorResponse { message }`. Keep new endpoint files consistent with this — the frontend's `httpClient.requestJson` reads `body.message` on any non-2xx response, so an endpoint that doesn't follow this shape breaks that error handling.
- **Service pattern**: define the interface first, with XML doc comments explaining what each thrown exception means (since `Try` maps them to status codes). Put implementation(s) in `Services/`. `Program.cs` picks which implementation is constructed — don't hardwire a concrete service type into an endpoint file.
- **DTOs**: small immutable records where possible. Enums that need to serialize as strings (not ints) get an explicit `EnumXxxJsonConverter`, as with `EnumTargetCategoryJsonConverter`.
- **Concurrency**: services doing file I/O guard state with `lock` (see the three separate locks in `StorageEncryptionSettingsService` — one for in-memory state, one for the settings file, one for the actual encryption operation). Follow this pattern rather than introducing async/await plus separate concurrency primitives unless there's a real need.

## Feature log
- *(baseline)* Encryption on/off + target folder/file management (`EncryptionEndpoints`, `IEncryptionSettingsService`, `StorageEncryptionSettingsService`), backed by zipping targets and deleting originals (reversed on toggle-off). Native file/folder picker dialogs (`DialogEndpoints`).
- Browser profile selection for Edge/Chrome (`BrowserEndpoints`, `IBrowserProfileService`, `StorageBrowserProfileService`): `GET/POST /api/browsers/{browser}/selection` persist which profile id is picked per browser (`browserProfiles.json`, default `null` = "None") — fully implemented.
  - `GetMsEdgeBrowserProfiles()` is now implemented: reads Edge's `Local State` file (`profile.info_cache`) for each profile's folder name + display name, and — if the profile has a cached account picture (`Google Profile Picture.png` / `Edge Profile Picture.png` in the profile's own folder) — inlines it as a base64 `data:` URI in `AvatarUrl` so the frontend needs no extra endpoint to serve it. `GetEdgeUserDataDirectory()` covers Windows/macOS/Linux paths. `BrowserProfile` gained a `folderPath` field (the profile's absolute folder path) for anything that needs to read more from the profile later. Note: a full-resolution cached picture can be several hundred KB as base64 — fine for a local single-user settings page, but worth downsizing the image before encoding if this ever needs to scale to many profiles.
  - `GetDisplayName()` (used by `GetMsEdgeBrowserProfiles`): prefers the signed-in account's email (`user_name` in `info_cache`) over Edge's own generic `name` field ("Profile 1"/"Profile 2"/…), since the generic name doesn't tell a user which account a profile actually is — and two profiles signed into different accounts can even share the same real name, so email is what actually disambiguates them. Falls back to the generic name for a profile that isn't signed into any account.
  - `GetChromeBrowserProfiles()` is still a stub (returns an empty list) — same `Local State`/profile-folder approach should apply to Chrome's user-data directory, just not implemented yet.
