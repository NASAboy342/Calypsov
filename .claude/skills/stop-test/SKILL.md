---
name: stop-test
description: Stops Calypsov's local dev environment (the Vite dev server and the dotnet backend/window started by /run-test), killing the actual port listeners and the dotnet process cleanly rather than just backgrounding Ctrl+C. Use this whenever the user wants to stop, kill, shut down, or clean up the running dev servers, or before restarting them to avoid port conflicts.
---

# stop-test

Cleanly stops both processes started by `/run-test`. This matters more than it sounds like it should, because of two easy-to-miss gotchas:

- `npm run dev &` backgrounds the `npm` wrapper, not the actual Vite server it spawns — `npm` doesn't forward signals to its child, so killing the wrapper leaves the port bound and the next `/run-test` fails with `EADDRINUSE`. The reliable fix is to kill whatever process is actually *listening* on the port, not the process you happened to background.
- Killing by a broad name pattern (e.g. `pkill -f dotnet`, or even `pkill -f Calypsov`) is risky — it can match unrelated dotnet processes on the machine, or even match this agent session's own command line and kill it. Always match on something specific enough to only ever be this app.

## Steps

1. **Free the Vite dev server port:**
   ```bash
   lsof -ti:5173 -sTCP:LISTEN | xargs -r kill
   ```

2. **Free the backend/API port:**
   ```bash
   lsof -ti:8000 -sTCP:LISTEN | xargs -r kill
   ```

3. **Kill the dotnet process itself** (the one hosting the Photino window). `dotnet run` on this project launches the native apphost directly (`bin/Debug/net8.0/Calypsov`, not `dotnet Calypsov.dll` — this project's `OutputType` is `Exe`), so match on that build output path specifically:
   ```bash
   pkill -9 -f "bin/Debug/net8.0/Calypsov" 2>/dev/null
   ```
   Use `-9` (SIGKILL) directly rather than a plain `pkill`/`kill` — this process runs a native window event loop (Photino's `WaitForClose`) that does not reliably respond to a graceful SIGTERM; a plain kill can appear to succeed but leave the process running.

   Match on the dev build path (`bin/Debug/net8.0/Calypsov`) precisely — **not** a bare `pkill -f Calypsov` or `pkill -f dotnet`. Those broader patterns can also match a completely unrelated, already-installed copy of the app (e.g. `/Applications/Calypsov.app/Contents/MacOS/Calypsov`, if the user has built and installed a packaged version) or the agent's own session. Only the dev instance started by `/run-test` should ever be touched here.

4. **Verify the cleanup actually worked** — don't assume it did:
   ```bash
   lsof -ti:5173 -sTCP:LISTEN; lsof -ti:8000 -sTCP:LISTEN; ps aux | grep -i calypsov | grep -v grep
   ```
   The first two should come back empty. The process list may still show an installed `/Applications/Calypsov.app/...` instance if one happens to be running separately — that's not this skill's concern and should be left alone; only a `bin/Debug/net8.0/Calypsov` entry indicates the dev instance didn't die and needs a retry.

5. **Report back** that both processes were stopped and the ports are free, so the user knows it's safe to run `/run-test` again without hitting a port conflict.
