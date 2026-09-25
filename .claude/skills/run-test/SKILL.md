---
name: run-test
description: Starts Calypsov's local dev environment (Vite frontend dev server + dotnet backend/window) so the app can be run and tested live with hot reload. Use this whenever the user wants to run, start, test, preview, or try out the Calypsov app, or says things like "run the app", "start the dev server", "let's test this", or "spin it up" — even if they don't mention the exact process names.
---

# run-test

Starts the two processes that make up Calypsov's dev environment. Both are long-running and must be started in the background — neither one exits on its own.

Because `Program.cs` wires `IsDebugMode` to `#if DEBUG`, a normal Debug build (the default for `dotnet run`) automatically points the app's native window at the live Vite dev server instead of the compiled `wwwroot` bundle. So starting both of these together, with no extra flags, is exactly the correct dev loop — changes to the Vue frontend hot-reload immediately, and C# changes just need a re-run.

## Steps

1. **If either port might already be in use** (e.g. a previous run wasn't stopped cleanly), run `/stop-test` first — starting on top of a stale process causes a confusing `EADDRINUSE` failure instead of a clean start.

2. **Start the frontend dev server** (Vite, hot reload, port 5173):
   ```bash
   (cd Calypsov/UserInterface && npm run dev > /tmp/calypsov-vite.log 2>&1 &)
   ```
   Then poll for readiness — never a blind `sleep`, since Vite's startup time varies:
   ```bash
   for i in $(seq 1 30); do curl -sf http://localhost:5173 >/dev/null && echo "Vite up after ${i}s" && break; sleep 1; done
   ```
   If it doesn't come up within 30s, show `/tmp/calypsov-vite.log` — it's almost always a stale port or a dependency error.

3. **Start the backend + native window** (dotnet, hosts the API and, in Debug, opens the Photino window pointed at the Vite server above):
   ```bash
   (cd Calypsov && dotnet run > /tmp/calypsov-dotnet.log 2>&1 &)
   ```
   Poll the API rather than sleeping, since the first build can take a few seconds:
   ```bash
   for i in $(seq 1 30); do curl -sf http://localhost:8000/api/encryption/status >/dev/null && echo "Backend up after ${i}s" && break; sleep 1; done
   ```
   If it doesn't come up, check `/tmp/calypsov-dotnet.log` for a build error or port conflict.

4. **Report back** both URLs (`http://localhost:5173` for the frontend, `http://localhost:8000` for the API) and that they're ready. Mention that `/stop-test` is how to shut them down cleanly afterward — don't just tell the user to Ctrl+C the session, since these were launched in the background and Ctrl+C won't reach them.

## Notes

- This only starts the processes; it doesn't drive a browser or take screenshots. If you need to visually inspect the UI, point headless Chromium/Playwright at `http://localhost:5173` separately (see this session's `run` skill for the general pattern).
- Logs are written to `/tmp/calypsov-vite.log` and `/tmp/calypsov-dotnet.log` — check these first if something fails to come up.
