# Kestry — Project plan

Web download manager (torrents, magnets and direct links) built on .NET, with aria2 as the download engine and a UI built with the **Kestrel** design system. Meant to run on a home PC or server and be managed from the browser, locally or remotely.

---

## 1. Goals

| # | Goal | Acceptance criterion |
|---|------|----------------------|
| 1 | Download torrents, magnets and HTTP/HTTPS/FTP links | All three types are added and completed using aria2 |
| 2 | Simultaneous downloads | Several downloads active at once; the rest stay queued |
| 3 | Bulk import by copy/paste | Paste N links in a textarea (one per line) and add them in one click |
| 4 | Visual download panel | Real-time list with progress, speed, ETA and actions |
| 5 | Settings | Max concurrent downloads, speed limit, download folder, shutdown when finished |
| 6 | Web and remote access | The UI works from any browser; ready to be published behind a tunnel/VPN |
| 7 | Simplicity | One-step install (Docker or binary) and a UI usable without reading documentation |

### Out of scope (v1)
- Login / user management (delegated to Cloudflare Zero Trust or a VPN, see §8).
- Multi-user, permissions or quotas.
- Torrent search, RSS or indexer integration.
- Streaming / file preview.
- Native mobile app (the web UI will be responsive).
- Queue reordering, multi-select with bulk actions and a per-download detail panel (see §15).
- `sftp://` links (depends on how aria2 was compiled).
- English UI (texts are centralized so they can be translated later).

---

## 2. Tech stack

| Layer | Technology | Rationale |
|-------|------------|-----------|
| Backend | **.NET 10 (LTS)** · ASP.NET Core Minimal APIs | Lightweight, cross-platform, single process |
| Real time | **Server-Sent Events** (.NET 10 `TypedResults.ServerSentEvents` + browser `EventSource`) | The flow is server → client only; no hub or client library |
| Download engine | **aria2c** (child process) via **JSON-RPC over HTTP** (`HttpClient`) | Supports HTTP/FTP/BitTorrent/Magnet, queues and limits; stateless requests, no reconnection |
| Persistence | `settings.json` + aria2 session file (`--save-session`) | No database: aria2 already stores its queue |
| Frontend | **React + TypeScript + Vite** | Lightweight SPA |
| UI/UX | **Kestrel design system** ([artifact](https://claude.ai/artifact/UvfFmAMTTYHUEUiTDM9uQC)): tokens, iconography, components and patterns | Requested design system; it is the design reference and is re-implemented on Tailwind (see §9) |
| Styles | **Tailwind CSS v4** (`@tailwindcss/vite`) with a `tokens.css` that translates the Kestrel tokens | Utilities with no ad hoc CSS; light/dark theme via CSS variables |
| Components | Native HTML + **Radix UI** (single `radix-ui` package) only for `Dialog`, `DropdownMenu` and `Switch` | Radix where accessibility is hard by hand (focus, portals, menus); everything else native, as in Kestrel |
| Packaging | Built frontend served as static files from `wwwroot` | One process, one port |
| Distribution | **Docker** image (aria2 installed with `apt` at build time) + zip with a self-contained binary and `wwwroot/` (Windows/Linux, without aria2: see §7) | One-step install; aria2 is never versioned or packaged in this repository |

---

## 3. Architecture

```
 Browser (React + Tailwind + Radix UI)
        │  HTTP (REST)  +  SSE (/api/events)
        ▼
┌──────────────────────────────────────────────┐
│ Kestry.Server (ASP.NET Core)                 │
│                                              │
│  REST API                                    │
│  /api/events (SSE) ◄── DownloadMonitor (1 s) │
│                                              │
│  Aria2Client (JSON-RPC/HTTP)                 │
│  Aria2ProcessManager (starts/watches aria2c) │
│  SettingsService (settings.json)             │
│  ShutdownService (shutdown when finished)    │
└──────────────┬───────────────────────────────┘
               │ http://127.0.0.1:6800/jsonrpc  (random rpc-secret)
               ▼
           aria2c  ──►  downloads folder
```

### Key decisions
- **aria2 is never exposed**: it listens only on `127.0.0.1`, with an `--rpc-secret` generated at startup. The browser only talks to Kestry.
- **Kestry manages aria2's lifecycle**: it launches it on start, restarts it if it dies, and shuts it down cleanly (`aria2.saveSession` + `aria2.shutdown`) on stop.
- **Single source of truth**: download state lives in aria2. Kestry does not duplicate the queue; it only queries and relays it.
- **Real-time push**: `DownloadMonitor` queries `tellActive`/`tellWaiting`/`tellStopped` + `getGlobalStat` every second in one `system.multicall` (only while clients are connected) and emits a snapshot over SSE.
- **No aria2 notifications**: since it is already polled every second, state changes (completed, error) are detected by comparing snapshots. The client raises the toasts; the server uses the same comparison for the shutdown condition.

---

## 4. Repository structure

```
Kestry/
├── README.md
├── AGENTS.md
├── Kestry.slnx
├── design/kestrel/                    # Kestrel design system export used by tests (see §9.1)
├── docs/                              # Original plan (read-only reference)
│   ├── PLAN-es.md
│   ├── PLAN-en.md
│   └── design/kestrel/                # Reference copy of the export
├── src/
│   ├── Kestry.Server/
│   │   ├── Program.cs                 # Composition, endpoints, static files
│   │   ├── KestryOptions.cs           # Kestry:* configuration (§10)
│   │   ├── ApiErrors.cs               # ErrorBody + ErrorCodes (only list of API error codes)
│   │   ├── Aria2/
│   │   │   ├── Aria2Locator.cs        # Finds aria2c or downloads it (Windows only, see §7)
│   │   │   ├── Aria2ProcessManager.cs # IHostedService: launches/watches aria2c
│   │   │   ├── Aria2Client.cs         # JSON-RPC over HTTP
│   │   │   └── Aria2Models.cs         # aria2 DTOs (status, files, bittorrent)
│   │   ├── Downloads/
│   │   │   ├── DownloadService.cs     # Add, pause, resume, cancel, remove
│   │   │   ├── DownloadEndpoints.cs   # REST endpoints
│   │   │   ├── LinkParser.cs          # Textarea parsing and validation
│   │   │   ├── PathGuard.cs           # AllowedRoots checks and path normalization
│   │   │   ├── Snapshot.cs            # Snapshot DTOs
│   │   │   ├── SnapshotMapper.cs      # aria2 status → snapshot row
│   │   │   ├── SnapshotBuilder.cs     # One multicall → Snapshot
│   │   │   ├── SnapshotHub.cs         # Latest snapshot + subscribers
│   │   │   ├── DownloadMonitor.cs     # BackgroundService: polling → hub
│   │   │   └── EventsEndpoint.cs      # SSE /api/events
│   │   ├── Settings/
│   │   │   ├── AppSettings.cs
│   │   │   ├── SettingsValidator.cs
│   │   │   ├── SettingsService.cs     # Reads/writes settings.json, applies to aria2
│   │   │   └── SettingsEndpoints.cs   # /api/settings, /api/system
│   │   ├── Shutdown/                  # Not "System/": that namespace breaks System.*
│   │   │   ├── ShutdownService.cs     # Countdown and condition
│   │   │   ├── SystemPower.cs         # ISystemPower: real power-off command per OS
│   │   │   └── ShutdownEndpoints.cs
│   │   ├── Security/
│   │   │   └── RequestGuard.cs        # Host/Origin checks on /api
│   │   ├── Health/
│   │   │   └── HealthCheck.cs         # --healthcheck flag for container probes
│   │   ├── appsettings.json
│   │   └── wwwroot/                   # Frontend build output (not versioned)
│   └── kestry-web/
│       ├── package.json
│       ├── vite.config.ts
│       └── src/
│           ├── main.tsx
│           ├── App.tsx                # Layout + theme class (light/dark) on <html>
│           ├── downloads.ts           # Filters, counters, snapshot diffing
│           ├── settings.ts            # Settings model and limit units
│           ├── theme.ts               # Light/dark/system preference
│           ├── api/                   # REST client, error translation, useSnapshot (EventSource)
│           ├── i18n/                  # config.ts, index.ts (useTranslations) and messages/{en,es,de,fr}.json
│           ├── styles/
│           │   └── tokens.css         # Hand-written from Kestrel's tokens.json: variables (light/dark) + @theme inline
│           ├── pages/
│           │   └── DownloadsPage.tsx
│           └── components/
│               ├── ui/                # Kestrel components (table in §9.4), Icon, Toaster
│               ├── Header.tsx         # Logo, quick switches, settings, LocaleSwitcher
│               ├── Sidebar.tsx        # NavItem filters, SpeedStat and aria2 status
│               ├── DownloadsList.tsx  # DownloadItem rows
│               ├── AddDownloadsModal.tsx
│               ├── ConfirmDialog.tsx
│               ├── SettingsDrawer.tsx
│               ├── LocaleSwitcher.tsx
│               └── ShutdownBanner.tsx
├── tests/
│   └── Kestry.Server.Tests/           # xUnit: unit, endpoints (KestryFactory), Smoke/ with real aria2
├── docker/
│   ├── Dockerfile
│   ├── Dockerfile.dockerignore
│   ├── docker-compose.yml             # Kestry + (optional) cloudflared
│   └── .env.example
├── deploy/                            # systemd unit + polkit rule for poweroff
├── scripts/
│   └── publish.sh                     # Self-contained zips into artifacts/
└── .github/workflows/                 # ci.yml, release.yml
```

---

## 5. Features in detail

### 5.1 Adding downloads (copy/paste)
**"Add downloads"** modal (`Dialog`), everything in a single view:

- **Links**: large `TextArea` in `mono` font. Accepted:
  - `http://`, `https://`, `ftp://`
  - `magnet:?xt=urn:btih:...`
  - URLs ending in `.torrent` (aria2 downloads them and continues with the content if `follow-torrent=true`)
- **.torrent files**: `FileDrop` drop zone below the textarea (multiple files) → `aria2.addTorrent` in base64.
- **Advanced options** (`<details>`, closed by default): destination subfolder (mono `TextField`), add paused (`Checkbox`).

Parsing (`LinkParser`):
1. Split on line breaks, spaces and commas; trim.
2. Ignore empty lines and comments (`#`).
3. Classify each line: `Http`, `Ftp`, `Magnet`, `TorrentUrl`, `Invalid`.
4. Remove duplicates (within the batch and against existing downloads, by URL or info-hash).

UX: below the textarea, a live summary with `StatusBadge`s: *"12 valid · 2 duplicates · 1 invalid"*, with the invalid ones listed. The **Add** button sends only the valid ones. Shortcut: pasting links with `Ctrl+V` anywhere on the page (outside text fields) opens the modal with the text already pasted.

### 5.2 Downloads panel
- **Action bar** (`Toolbar`): search by name on the left; on the right *Pause all* and *Resume all* (`Button` quiet) and, last, the primary *Add* button.
- **Filters** (`NavItem` with counter, in the sidebar, in Kestrel's order): All · Downloading · Queued · Completed · Error. *Queued* includes paused ones, like `aria2.tellWaiting`.
- **List**: one `DownloadItem` row per download (works the same on desktop and mobile). Each row shows:

  | Data | Presentation |
  |------|--------------|
  | Type | `Icon` by file type; the source (HTTPS / FTP / Torrent / Magnet) opens the detail line |
  | Name | Text with the path in the `title` attribute |
  | Status | `StatusBadge` (download states from §9.5) |
  | Progress | `ProgressBar` with the same state; indeterminate while a magnet fetches metadata |
  | Size, speed, ETA | Detail line: `X of Y GB · ↓ / ↑ (↑ torrents only) · N min left` |
  | Peers/Seeds | In the detail line, torrents only (`connections` / `numSeeders` from `tellStatus`) |
  | Actions | `IconButton` by state (like Kestrel's `DownloadItem`): downloading → Pause, Cancel · paused → Resume, Cancel · queued → Cancel · error → Retry, Remove · completed → Copy path, Remove. On mobile they go inside a `DropdownMenu` |

  - **Cancel** (`x` icon): asks for confirmation, stops the download (`aria2.forceRemove`), removes it from the list (`aria2.removeDownloadResult`) and Kestry deletes the partial files.
  - **Remove** (`trash` icon): removes a finished or failed download from the list (`aria2.removeDownloadResult`). Asks for confirmation, with a "also delete files" option.
  - File deletion is done by Kestry (aria2 deletes nothing), only inside `AllowedRoots` and with the path normalized.
  - **Copy path** (`folder` icon): copies the download's server-side path to the clipboard. A server folder cannot be opened from a remote browser.

- **Totals** (`SpeedStat` in the sidebar): total ↓/↑ speed and free disk space. The number of running and queued downloads goes in the filter counters and in a summary above the list.
- **Notifications** (`Toast` bottom right, one line and no exclamation marks), generated on the client by comparing snapshots:
  - `success` when a download completes, with the *Copy path* action.
  - `error` on failure, with the *Retry* action and `role="alert"`.
  - `info` for confirmations ("3 downloads added").
  - `info` and `success` close on their own after ~5 s; `error` ones stay until dismissed.

### 5.3 Settings
Side panel (`Drawer`, from the right) opened with the settings button in the header. Saved immediately and applied live via `aria2.changeGlobalOption` (without restarting aria2):

| Setting | Control | aria2 option | Default |
|---------|---------|--------------|---------|
| Concurrent downloads | Numeric `TextField` (1–20) | `max-concurrent-downloads` | 3 |
| Speed limit active | `Switch` | applies or sets to 0 the next two limits | Off |
| Download limit | Numeric `TextField` + `Select` KB/s / MB/s | `max-overall-download-limit` | 5 MB/s |
| Upload limit (torrents) | Same | `max-overall-upload-limit` | 1 MB/s |
| Download folder | Mono `TextField` + server-side validation | `dir` (affects new downloads only) | `~/Downloads/Kestry` (or `/downloads` in Docker) |
| Shut down when everything finishes | `Switch` | — (handled by Kestry) | Off |
| Seeding after completion | `Switch` + ratio | `seed-ratio` / `seed-time` | Off (ratio 0) |
| Theme | `Segmented` Light/Dark/System | — (stored in the browser) | System |

In `settings.json` the limit is stored as values (`DownloadLimit`, `UploadLimit`) plus a `SpeedLimitEnabled` boolean, so the switch knows which limit to go back to. The header has a **quick switch** (`Switch` with the `gauge` icon) that toggles that same boolean.

**Download folder**: the server checks that it exists (or can be created) and is writable. To prevent arbitrary writes when managed remotely, only paths inside a list of allowed roots (`AllowedRoots`) are permitted, defined in the configuration file or an environment variable, not editable from the UI.

### 5.4 Shutdown when finished
- Can be enabled from Settings and from a `Switch` visible in the header.
- Condition: enabled **and** no active or queued downloads remain **and** at least one download finished during the session (avoids shutting down right after startup).
- When met, a **60 s countdown** starts, visible on all clients (carried in the snapshot; `ShutdownBanner`: `Banner` on `rufous-100` + *Cancel shutdown* button). If cancelled or a download is added, it aborts.
- Before shutting down: `aria2.saveSession` and clean aria2 stop.
- Command per OS:
  - Windows: `shutdown /s /t 0`
  - Linux: `systemctl poweroff` (requires a polkit or sudoers rule documented in the README)
  - **Docker**: the container cannot shut down the host. The option is shown disabled with an explanation.
- `AllowShutdown` flag in the server configuration (not in the UI) to disable it entirely on remote installs.

---

## 6. API

### REST (`/api`)
| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/downloads` | Full snapshot (the same one delivered over SSE) |
| `POST` | `/api/downloads/links` | `{ text, subfolder?, paused? }` → per-line result (added/duplicate/invalid) |
| `POST` | `/api/downloads/torrents` | `multipart/form-data` with 1..N `.torrent` files |
| `POST` | `/api/downloads/{gid}/pause` | Pause |
| `POST` | `/api/downloads/{gid}/resume` | Resume |
| `POST` | `/api/downloads/{gid}/retry` | Retry (re-adds with the same URI) |
| `DELETE` | `/api/downloads/{gid}?deleteFiles=bool` | Cancel or remove |
| `POST` | `/api/downloads/pause-all` · `/resume-all` | Global actions (`aria2.pauseAll` / `unpauseAll`) |
| `POST` | `/api/downloads/purge` | Clear completed/errors from the list (`aria2.purgeDownloadResult`) |
| `GET` / `PUT` | `/api/settings` | Read/save settings |
| `POST` | `/api/shutdown/cancel` | Cancel countdown |
| `GET` | `/api/system` | Version, aria2 version, whether shutdown is available |
| `GET` | `/health` | Healthcheck (includes aria2 status) |

### SSE (`GET /api/events`)
A single `snapshot` event every second:

| Field | Content |
|-------|---------|
| `downloads` | List of downloads (minimal fields) |
| `stats` | Global ↓/↑ speed and free disk space |
| `aria2` | `installing` / `running` / `restarting` / `down` (with the reason if the binary is missing) |
| `shutdown` | `null` or remaining countdown seconds |
| `settingsVersion` | Changes when settings are saved; other clients re-request `/api/settings` |

The response carries `Cache-Control: no-cache` so proxies and tunnels do not buffer it.

---

## 7. aria2 integration

**Startup** (`Aria2ProcessManager`):
```
aria2c --enable-rpc --rpc-listen-all=false --rpc-listen-port=<Aria2:Port>
       --rpc-secret=<random> --dir=<dir>
       --input-file=<data>/aria2.session --save-session=<data>/aria2.session
       --save-session-interval=30 --continue=true
       --max-concurrent-downloads=<n> --max-overall-download-limit=<x>
       --follow-torrent=true --bt-save-metadata=true --seed-ratio=0
       --listen-port=<Aria2:BtPort> --dht-listen-port=<Aria2:BtPort>
       --enable-dht=true --bt-enable-lpd=true --enable-peer-exchange=true
```
**Obtaining the binary** (`Aria2Locator`). aria2 is not included in the repository or the zip; it is obtained at build time or downloaded by the application:
- **Docker**: `apt-get install aria2` in the final stage of the `Dockerfile` (Debian package, amd64 and arm64).
- **Binary search order**: `Aria2:Path` in configuration → `PATH` → `<DataDir>/aria2/aria2c[.exe]` (previously downloaded by Kestry).
- **Windows, if not found**: Kestry downloads in the background the official zip from the aria2 GitHub release (`aria2-<version>-win-64bit-build1.zip`), verifies its SHA-256 (version and hash pinned in the code), extracts `aria2c.exe` into `<DataDir>/aria2/` and starts it. Meanwhile the UI shows "Installing aria2…".
- **Linux, if not found**: aria2 publishes no official Linux binaries, so nothing is downloaded. It stays `down` with the message "Install aria2 (`apt install aria2`, `dnf install aria2`…)" in the UI and the log.
- **No Internet or wrong hash**: nothing is executed; it stays `down` with the reason, and instructions on how to copy `aria2c.exe` by hand or set `Aria2:Path`.
- Fixed, configurable RPC port (`Aria2:Port`); if busy, a clear error in the log.
- Supervision: if the process exits unexpectedly, it is restarted after 5 s; the state (`restarting` / `down`) reaches the UI in the snapshot.

**Client** (`Aria2Client`):
- `POST http://127.0.0.1:<port>/jsonrpc` with `HttpClient`; each call is independent (no persistent connection or reconnection).
- Methods used: `addUri`, `addTorrent`, `pause`, `unpause`, `pauseAll`, `unpauseAll`, `forceRemove`, `removeDownloadResult`, `purgeDownloadResult`, `tellStatus`, `tellActive`, `tellWaiting`, `tellStopped`, `getGlobalStat`, `changeGlobalOption`, `saveSession`, `shutdown`, `getVersion`, `system.multicall` (to batch the tick's queries).
- For magnets, the metadata GID's `followedBy` is followed so the user sees a single row.
- Request only the necessary fields (`keys`) in `tell*` to reduce load.

---

## 8. Remote access and security (no login)

Kestry **does not implement authentication** by design. Protection is delegated to the network layer:

1. **By default it listens only on `127.0.0.1:8080`**. Exposing it requires explicitly changing `Urls`. In Docker it listens on `http://+:8080` inside the container and the compose file publishes only `127.0.0.1:8080`.
2. Recommended options (documented in the README with examples):
   - **Cloudflare Tunnel + Cloudflare Access (Zero Trust)**: `cloudflared` as a service in `docker-compose.yml` (optional profile), and an Access policy by email/OTP or identity provider. Kestry never opens router ports. Published on its own subdomain (no subpath).
   - **Tailscale / WireGuard**: access only from the private network.
3. Mitigations even without login:
   - aria2 only on loopback and with `rpc-secret`; never exposed.
   - `Origin` header check on mutating requests (anti-CSRF / anti DNS-rebinding): the host itself and those in `Kestry:AllowedOrigins` are accepted.
   - Download folder and file deletion limited to `AllowedRoots`; path normalization to prevent `..`.
   - `AllowShutdown` can be disabled from the server.
   - Request validation and size limits (textarea and `.torrent`).

---

## 9. UI design

### 9.1 Source and scope
- **Where it lives**: the original is at https://claude.ai/artifact/UvfFmAMTTYHUEUiTDM9uQC. An exported copy is in `design/kestrel/` (version: the `version` field of `tokens.json`); it is re-exported when the original changes. It takes precedence over this plan on visual matters:
  - `README.md`: principles, color usage, typography, spacing and state rules.
  - `tokens.json`: **single source** for colors (light and dark themes), typography, spacing, radii and shadows.
  - `components/index.d.ts` and `components/<Comp>/README.md`: props, variants and states of each component.
  - `components/bundle.css` and `components/bundle.js`: reference implementation. Used to look up exact measurements (heights, paddings, icon sizes) and the icons' SVG paths. They are not loaded in the app.
- **Reference preview**: the agreed screens are at https://claude.ai/artifact/4uDV2ivwkbSYLJs9EQrZ6D (Downloads, Add downloads, Empty state, Shutdown when finished, Settings).
- **Why it is re-implemented**: Kestrel ships React components (`window.Kestrel` + `bundle.css`), but they are not used as is: they load as a global script (no npm/ESM package), their texts are fixed in English (`StatusBadge`, `DownloadItem`) and pieces the plan needs are missing (DropdownMenu, Drawer). They are re-implemented in `components/ui/` with **Tailwind CSS + Kestrel tokens**, native HTML as in the original and Radix only where indicated in §9.4, replicating their anatomy, variants and states (hover, focus, disabled, error).
- **Naming note**: not to be confused with *Kestrel*, the ASP.NET Core web server used by the backend; code and documentation say "Kestrel design system" to tell them apart.

### 9.2 Tokens, theme and typography
- **Tokens**: `styles/tokens.css` is hand-written from `design/kestrel/tokens.json` (few values that rarely change), with CSS variables of the same names (`--surface-000`, `--ink`, `--slate-600`, `--radius-md`…) exposed to Tailwind with `@theme inline`:
  ```css
  @import "tailwindcss";
  :root { --surface-000: #faf7f2; /* … */ }
  .dark { --surface-000: #1a1d21; /* … */ }
  @theme inline { --color-surface-000: var(--surface-000); /* colors, fonts, radii, shadows */ }
  ```
  Kestrel's spacing (4 px base: `space-1` 4 … `space-12` 48) matches Tailwind's default scale (`p-1` … `p-12`), so it is not redefined. No ad hoc styles: only Tailwind classes pointing to those tokens; no arbitrary colors or sizes (`bg-[#...]`, `p-[13px]`).
- **Theme**: the `light` values go in `:root` and the `dark` ones under the `.dark` class on `<html>`, according to the Light/Dark/System preference (`prefers-color-scheme`). Since colors are variables, the classes do not change between themes and the `dark:` variant is not needed.
- **Typography**: Fraunces (headings; the `display` style at most once per screen), Manrope (all UI) and JetBrains Mono (URLs, paths, info-hash). Unlike Kestrel, which loads them from Google Fonts, they are **self-hosted** with `@fontsource` inside the build: Kestry runs on home servers that may have no Internet access.

### 9.3 Kestrel rules that always apply
Summary; the Kestrel README is the reference:
- Calm backgrounds (`surface-000` / `surface-100`); `slate-600` is the primary action and `rufous-*` the accent. Never a primary and an accent button of the same weight together.
- Separation with lines (`line`), not shadows; `shadow-soft` only on floating elements (dialogs, drawer, toasts, menus).
- `cere` (signal orange) only for dots or "new"/attention badges, **never text**, and at most one visible at a time.
- Sentence case only ("Save changes"), no exclamation marks; say what happened ("Download added"), not how the system feels.
- Focus: 2 px ring on `focus` with 2 px offset on every interactive element. Disabled: 40 % opacity, no hover. Error: `danger` border + message, never color alone.
- Radii: `radius-sm` on fields, `radius-md` on buttons and cards, `radius-lg` on dialogs and drawer, `radius-full` on badges.

### 9.4 Components
| Plan concept | Kestrel component | Implementation |
|--------------|-------------------|----------------|
| Button | `Button` (primary · accent · secondary · quiet · danger; sm · md) | `<button>` + Tailwind |
| Icon button | `IconButton` (mandatory `aria-label`) | `<button>` + Tailwind |
| Icon | `Icon` (see §9.6) | Inline SVG |
| Text / number field | `TextField` (`mono` variant for URLs and paths) | `<input>` + `<label>` |
| Text area | — (`TextField` style) | `<textarea>` |
| Select | `Select` | Native `<select>` |
| Checkbox | `Checkbox` | `<input type="checkbox">` |
| Switch | `Switch` | Radix `Switch` |
| Status label | `StatusBadge` | HTML |
| Progress | `ProgressBar` | `<div role="progressbar">` (no `aria-valuenow` if indeterminate) |
| Download row | `DownloadItem` | HTML |
| Filter | `NavItem` (with counter) | `<button aria-current>` |
| Action bar | `Toolbar` | `<div role="toolbar">` |
| Totals | `SpeedStat` | HTML |
| Modal | `Dialog` | Radix `Dialog` |
| Notification | `Toast` (info · success · error) | List in an `aria-live` region + timer |
| Empty state | `EmptyState` | HTML |
| Side panel | — `Drawer` | Radix `Dialog` anchored to the right |
| Actions menu | — `DropdownMenu` | Radix `DropdownMenu` |
| Segmented selector | — `Segmented` | Group of `<input type="radio">` |
| Collapsible options | — | `<details>` / `<summary>` |
| Drop zone | — `FileDrop` | `<input type="file">` + drop events |
| Notice | — `Banner` | HTML (`rufous-100` for action notices, `slate-100` for info) |

"—" = Kestrel does not have it: designed using only its tokens and the rules in §9.3.

### 9.5 Download states
Kestrel defines five states shared by `StatusBadge` and `ProgressBar`; each always carries its word, never color alone:

| aria2 state | Kestrel state | Text | Color |
|-------------|---------------|------|-------|
| `active` | downloading | Downloading | slate |
| `waiting` | queued | Queued | neutral |
| `paused` | paused | Paused | rufous |
| `complete` | completed | Completed | moss |
| `error` | failed | Error | danger |

A magnet without metadata is shown as *downloading* with an indeterminate `ProgressBar` and the text "Fetching metadata…". `removed` downloads are not shown.

### 9.6 Iconography
- The 23 Kestrel icons are ported (`Kestrel.iconNames`: download, pause, play, x, plus, check, retry, folder, search, alert, info, file, video, archive, music, image, trash, link, clock, inbox, sliders, chevronDown, gauge).
- Added in the same style (1.75 stroke, 24 px grid, round caps): `upload` (`.torrent` zone) and `magnet`.
- 20 px in the UI, in `ink` or `ink-muted`; `slate-600` only for the active state.

### 9.7 Layout and screens
- **Header**: logo (for now a reserved 36 px slot until the logo exists), "Kestry" name, speed-limit `Switch`, shutdown-when-finished `Switch` and settings `IconButton`.
- **Sidebar** (240 px, `surface-100`): `NavItem` filters, `SpeedStat` (↓, ↑, free space) and, at the bottom, the connection indicator.
- **Content** (`surface-000`): `Toolbar`, shutdown banner, summary and `DownloadItem` list; `Toast` bottom right.
- **Views**: Downloads only. Settings is a 480 px `Drawer` from the right, not a page of its own.
- **Empty state**: `EmptyState` + large button *"Paste your links here"* that opens the modal.
- **Connection indicator** (`StatusBadge` at the bottom of the sidebar; version from `/api/system`, state from the snapshot):
  - Connected: *"aria2 {version} connected"*, `moss`.
  - Installing: *"Installing aria2…"*, `slate` (Windows only, first run).
  - Reconnecting: *"Reconnecting to aria2…"*, `rufous`.
  - Down: *"aria2 is not responding"*, `danger`.
- **Responsive**: Kestrel defines no breakpoints; Tailwind's defaults are used. Below `lg` (1024 px) the sidebar moves to the top and the filters sit in a horizontally scrolling row. Below `sm` (640 px), each row's actions go in a `DropdownMenu` and the drawer takes the full width.

### 9.8 Language and accessibility
- **Language**: Spanish. No visible text comes from the design system: all labels, including status ones, are in `strings.ts`, ready to add another language later.
- **Accessibility**: focus, keyboard and ARIA for dialogs, drawer and menus come from Radix; everything else uses native elements. `aria-label` on all icon buttons. Kestrel's color pairs meet 4.5:1 in both themes; `rufous-500`, `sand` and `cere` are never used for text.

---

## 10. Server configuration (`appsettings.json` / environment variables)

```jsonc
{
  "Urls": "http://127.0.0.1:8080",      // in Docker: http://+:8080 (see §11)
  "Kestry": {
    "DataDir": "./data",                 // settings.json, aria2.session, logs
    "AllowedRoots": ["/downloads"],     // allowed roots for the download folder
    "AllowShutdown": true,
    "AllowedOrigins": []                 // extra allowed origins (tunnel domain)
  },
  "Aria2": {
    "Path": null,                        // null = PATH → <DataDir>/aria2 → download (Windows only)
    "Port": 6800,                        // RPC, loopback only
    "BtPort": 6881,                      // BitTorrent and DHT (TCP/UDP)
    "ExtraArgs": []
  }
}
```
All keys can be overridden with variables such as `KESTRY__ALLOWEDROOTS__0=/downloads`.

---

## 11. Deployment

- **Docker** (recommended):
  - Multi-stage image: `node` (frontend build) → `mcr.microsoft.com/dotnet/sdk` (publish) → `mcr.microsoft.com/dotnet/aspnet` + `aria2` installed. Build stages use `--platform=$BUILDPLATFORM` and `dotnet publish -a $TARGETARCH` to avoid compiling under emulation.
  - Multi-architecture `linux/amd64` and `linux/arm64` (Raspberry Pi / NAS).
  - `ASPNETCORE_URLS=http://+:8080` in the image; the compose file publishes `127.0.0.1:8080:8080`.
  - Volumes: `/downloads` and `/data`. Runs as the .NET image's non-root `app` user; can be changed with `user:` in the compose file to match the volumes' owner.
  - `docker-compose.yml` with a `kestry` service, an optional `cloudflared` service (profile `tunnel`) and the BitTorrent port `6881` TCP/UDP mapped.
- **Self-contained binary**: `dotnet publish -c Release -r win-x64|linux-x64|linux-arm64 --self-contained -p:PublishSingleFile=true`. A zip is distributed with the executable and the `wwwroot/` folder (static files are not inside the executable). No zip includes aria2: on Windows Kestry downloads it on first start and on Linux it is installed with the package manager (§7).
- **System service**: example `systemd` unit and `UseWindowsService()` support to run as a Windows service.

---

## 12. Testing

| Type | What is tested | Tool |
|------|----------------|------|
| Unit | `LinkParser` (types, duplicates, invalid, separators), `SettingsService` (validation, paths outside `AllowedRoots`), `ShutdownService` logic (conditions and cancellation, with a fake clock) | xUnit + fake `TimeProvider` |
| Unit | `Aria2Locator`: search order and rejection of a zip with a different SHA-256 (no network, with a fake zip) | xUnit |
| Smoke | Start real aria2, add a locally served HTTP URL and see it complete | xUnit (same project; skipped if there is no `aria2c`; installed with `apt` in CI) |
| CI | Build, tests and Docker image build on every PR | GitHub Actions |

The real shutdown command is never run in tests: `ShutdownService` depends on an `ISystemPower` interface that is replaced by a double.

---

## 13. Development phases

### Phase 0 — Skeleton (1–2 days)
- [ ] .NET solution + Vite/React project with Tailwind CSS; the frontend build is copied to `wwwroot`.
- [ ] Write `styles/tokens.css` (variables + `@theme inline`) from Kestrel's `tokens.json` and configure the light/dark theme.
- [ ] Self-host the fonts (Fraunces, Manrope, JetBrains Mono) with `@fontsource`.
- [ ] Port the Kestrel icons and add the new ones (§9.6).
- [ ] Implement the components from the §9.4 table in `components/ui/`.
- [ ] `Program.cs` serving static files + `/health`.
- [ ] Basic CI.

### Phase 1 — aria2 core (2–3 days)
- [ ] `Aria2Locator`: binary search and verified download on Windows.
- [ ] `Aria2ProcessManager`: launch, watch and stop aria2c; persistent session.
- [ ] `Aria2Client`: JSON-RPC over HTTP.
- [ ] `DownloadService` + download REST endpoints (including file deletion inside `AllowedRoots`).
- [ ] Smoke test with real aria2.

### Phase 2 — Real-time panel (2–3 days)
- [ ] `DownloadMonitor` + SSE endpoint `/api/events`.
- [ ] `DownloadsList` (`DownloadItem` rows) with per-row actions and toasts derived from snapshots.
- [ ] `Sidebar`: `NavItem` filters, `SpeedStat` and aria2 connection indicator.

### Phase 3 — Import (2 days)
- [ ] `LinkParser` with tests.
- [ ] `AddDownloadsModal` with textarea, live summary and `.torrent` upload.
- [ ] Global `Ctrl+V` paste.

### Phase 4 — Settings and shutdown (2–3 days)
- [ ] `SettingsService` with live application to aria2.
- [ ] `SettingsDrawer` and quick speed toggle.
- [ ] `ShutdownService` (Windows/Linux) with countdown and cancellation.

### Phase 5 — Remote and security (1 day)
- [ ] `Origin` validation, `AllowedRoots`, request size limits.
- [ ] Cloudflare Tunnel + Access and Tailscale guide in the README; verify SSE arrives unbuffered through the tunnel.

### Phase 6 — Packaging and polish (2 days)
- [ ] Multi-arch Dockerfile + `docker-compose.yml` with a `cloudflared` profile.
- [ ] Self-contained binaries, systemd unit, Windows service.
- [ ] Mobile responsiveness and dark theme.

**Total estimate: ~2.5–3 weeks** of one person's work.

---

## 14. Risks and mitigations

| Risk | Mitigation |
|------|-----------|
| Accidental exposure to the Internet without protection | Listens on loopback by default (also the published port in Docker), clear tunnel/VPN documentation |
| Shutdown unavailable due to permissions or in Docker | Detection in `/api/system`, option disabled with an explanation |
| Magnets that never obtain metadata | Show "Fetching metadata" state and allow cancelling; DHT enabled |
| aria2 is not installed or cannot be downloaded | `down` state with the reason and instructions in the UI; `Aria2:Path` to set it by hand |
| The aria2 GitHub release changes or disappears | Version and SHA-256 pinned in the code; updated by hand in a new Kestry version |
| aria2 crashes or the session gets corrupted | Automatic restart; backup of the session file before each start |
| Many downloads → heavy snapshot | Minimal `keys` in aria2 and `system.multicall`; if needed, diff delivery (post v1) |
| A proxy or tunnel buffers SSE | `Cache-Control: no-cache`; test it with Cloudflare Tunnel in Phase 5 |
| BitTorrent ports blocked (NAT) | Single configurable port (`BtPort`), document its mapping; show peer count to diagnose |

---

## 15. Future improvements (post v1)
- Queue reordering (`aria2.changePosition`).
- Multi-select with bulk actions.
- Per-download detail panel (files, peers, info-hash, aria2 error).
- English UI.
- Shutdown hook for Docker (`ShutdownCommand` → host script or webhook).
- UI warning if Kestry detects it is listening on a public interface without a proxy in front.
- `PathBase` support (publish under a subpath) and `ForwardedHeaders`.
- `sftp://` links.
- Speed schedule (e.g. no limit at night).
- Categories with automatic destination folders.
- Browser extension / bookmarklet to send links to Kestry.
- Torrent RSS integration.
- Webhooks / push notifications (Telegram, ntfy) on completion.
- Installable PWA mode.
