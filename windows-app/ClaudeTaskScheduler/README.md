# Claude Task Scheduler (Windows)

A Windows tray app that lets you create, run, and monitor scheduled
[Claude Code](https://claude.com/claude-code) CLI jobs — controlled from a
phone (or any browser) on your local network.

- Scheduling is delegated to the real **Windows Task Scheduler** (via
  `schtasks.exe`), so jobs keep running even if this app isn't open, and
  survive reboots (as long as the machine is on and you're logged in).
- The app itself just hosts a small local REST API + a mobile-friendly web
  page for creating/running/deleting those jobs, and lives in the system
  tray.
- Each scheduled job runs `claude` non-interactively, piping the saved
  prompt into it and appending stdout/stderr to a per-task log file you can
  view from your phone.

## How it works

```
Phone browser  --HTTP + X-Api-Key-->  Windows tray app (Kestrel, localhost:5055)
                                            |
                                            v
                                     schtasks.exe (creates/runs/deletes)
                                            |
                                            v
                              Windows Task Scheduler runs a generated
                              wrapper .cmd -> pipes prompt.txt into `claude -p`
                                            |
                                            v
                                    per-task log file (tail-able from the phone)
```

Task metadata (name, schedule, working directory, last-run time) is kept in
`%LOCALAPPDATA%\ClaudeTaskScheduler\tasks.json`. The Windows Task Scheduler
entry is the actual source of truth for *when* the job runs; this app just
remembers what it asked Task Scheduler to do and gives you a UI for it.

## Building & running

Requires **.NET 8 SDK** and **Windows** (uses WinForms for the tray icon and
`schtasks.exe` for scheduling — this will not run on Linux/macOS).

```powershell
cd windows-app\ClaudeTaskScheduler
dotnet build
dotnet run
```

On first launch it:
1. Generates a random API key and writes it, along with the port (default
   `5055`), to `%LOCALAPPDATA%\ClaudeTaskScheduler\config.json`.
2. Shows a tray balloon with the local URL.
3. Right-click the tray icon → **Copy API key** to get the key for your
   phone. **Open dashboard** opens the page in your default browser.

> **Not build-tested in this session.** This code was written and reviewed
> for correctness against .NET 8 / ASP.NET Core / WinForms APIs, but the
> sandbox this was authored in is Linux with no .NET SDK installed, and the
> app's core dependencies (WinForms, `schtasks.exe`, Windows Task Scheduler)
> only exist on Windows. Please build and smoke-test it on a real Windows
> machine before relying on it, and open an issue with the exact compiler
> error if `dotnet build` fails.

### Publish a single-file exe (optional)

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Making it reachable from your phone

The app binds to `0.0.0.0:<port>`, so it listens on all local network
interfaces, not just `localhost`. To reach it from a phone:

1. **Same Wi-Fi/LAN**: on the PC, run `ipconfig` to find its LAN IPv4
   address, then browse to `http://<that-ip>:5055/` from the phone. Windows
   Firewall will prompt to allow the app through on first run — allow it for
   Private networks at least.
2. **From anywhere**: install [Tailscale](https://tailscale.com) (or
   equivalent) on both the PC and the phone, then use the PC's Tailscale
   address instead of its LAN IP. This avoids exposing the API to the
   public internet.
3. Either way, treat the API key like a password — anyone with it and
   network access to the port can create/run tasks (i.e. run arbitrary
   Claude CLI prompts) on your PC. Don't port-forward this app to the public
   internet.

### Configuring the Claude CLI path

By default the wrapper script invokes `claude` and expects it to be on the
`PATH` for the same Windows account the Task Scheduler job runs as. If the
scheduled task can't find it (a common Task Scheduler gotcha — its default
environment can differ from an interactive shell's), edit
`%LOCALAPPDATA%\ClaudeTaskScheduler\config.json` and set
`"ClaudeExecutablePath"` to the full path, e.g.
`"C:\\Users\\you\\AppData\\Roaming\\npm\\claude.cmd"`, then restart the app
and re-create any existing tasks so their wrapper scripts pick up the
change.

## API

All endpoints below (except `/api/ping`) require an `X-Api-Key: <key>`
header. Bodies/responses are JSON.

| Method | Path                        | Description                                   |
|--------|-----------------------------|------------------------------------------------|
| GET    | `/api/ping`                 | Unauthenticated health check                   |
| GET    | `/api/tasks`                | List all scheduled tasks                       |
| POST   | `/api/tasks`                | Create a task (see fields below)               |
| DELETE | `/api/tasks/{id}`           | Delete a task (and its Task Scheduler job)     |
| POST   | `/api/tasks/{id}/run`       | Run the task immediately                       |
| POST   | `/api/tasks/{id}/toggle`    | Body `{ "enabled": true|false }`               |
| GET    | `/api/tasks/{id}/log`       | Tail (last ~200 lines) of the task's log file  |

`POST /api/tasks` body:

```json
{
  "name": "Morning summary",
  "prompt": "Summarize yesterday's commits in this repo.",
  "workingDirectory": "C:\\Users\\you\\projects\\my-repo",
  "scheduleType": "Daily",
  "startTime": "2026-01-01T08:00:00",
  "intervalMinutes": null,
  "daysOfWeek": null
}
```

- `scheduleType`: `"Once" | "Daily" | "Weekly" | "Interval"`.
- `startTime`: for `Once`, the full date+time to run; for `Daily`/`Weekly`,
  only the time-of-day is used; ignored for `Interval`.
- `intervalMinutes`: required (1–1440) when `scheduleType` is `Interval`.
- `daysOfWeek`: required (subset of `MON,TUE,WED,THU,FRI,SAT,SUN`) when
  `scheduleType` is `Weekly`.

## Security notes

- The API key is generated with `RandomNumberGenerator` (24 random bytes),
  stored locally, and required on every state-changing call.
- User-supplied text (the prompt, in particular) is never interpolated into
  a shell command line — it's written to its own file and streamed into the
  Claude CLI over stdin, so it cannot inject additional commands into the
  generated wrapper script or into the `schtasks.exe` invocation (which
  itself uses `ProcessStartInfo.ArgumentList`, not a shell string, for every
  call).
- `workingDirectory` is validated to be an absolute path containing no `"`
  characters before being embedded in the wrapper script.
