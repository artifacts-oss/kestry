# AGENTS.md

Guidance for AI coding agents (and humans) working on Kestry: a web download manager (.NET 10 + React) driving `aria2c`.

## Ground rules

- **Language**: code, comments, docs, commit messages and CLI/README text are in **English**. The *UI* is translated (en, es, de, fr; modelled on artifacts-oss/daylog's next-intl setup). Every visible string lives in `src/kestry-web/src/i18n/messages/<locale>.json`; never hard-code UI text in components. Adding or changing a string means updating **all four files** (a test enforces identical keys and placeholders).
- **Do not edit `Kestry/`.** It is the original plan (`PLAN.md`) and the exported Kestrel design system, kept as read-only reference. The project lives at the repo root. `design/kestrel/` is a copy of the export used by tests.
- Every feature ships with tests. Stage commits separately (one logical change each) and keep the maintainer as author; never add AI co-author trailers.
- Smallest change that fully solves the task; no speculative options or abstractions.

## Layout

```
src/Kestry.Server/        ASP.NET Core minimal API + background services
  Aria2/                  Locator (find/download aria2c), ProcessManager (child process), Client (JSON-RPC), Models
  Downloads/              DownloadService, LinkParser, Snapshot{Mapper,Builder,Hub}, DownloadMonitor, endpoints (REST + SSE)
  Settings/               AppSettings, SettingsService (validate/persist/hot-apply), endpoints (+ /api/system)
  Shutdown/               ShutdownService (countdown), ISystemPower   <- not "System/": a namespace named System breaks `System.*`
  Security/               RequestGuard (Host/Origin checks on /api)
  Health/                 `--healthcheck` flag for container probes
src/kestry-web/           Vite + React + TS + Tailwind v4 (+ Radix for Dialog/Drawer/DropdownMenu/Switch)
  src/components/ui/      Kestrel components re-implemented on Tailwind
  src/i18n/               config.ts (locales, Accept-Language resolution), index.ts (locale store, `useTranslations(ns)`), messages/*.json
  src/styles/tokens.css   Kestrel tokens (light `:root`, dark `.dark`) exposed via `@theme inline`
tests/Kestry.Server.Tests xUnit; `Smoke/` runs against a real aria2c
docker/  deploy/  scripts/  .github/workflows/
```

Data flow: aria2 is the single source of truth. `DownloadMonitor` polls it once per second (one `system.multicall`) while a browser is connected or shutdown is armed, publishes a snapshot through `SnapshotHub`, and `/api/events` streams it as SSE. The UI derives toasts by diffing snapshots.

## Commands

```bash
dotnet test                                   # backend (smoke tests need aria2c on PATH, else they are skipped)
cd src/kestry-web && npm ci && npm test       # frontend (vitest)
cd src/kestry-web && npm run typecheck && npm run build   # build writes to src/Kestry.Server/wwwroot
scripts/publish.sh [rid...]                   # self-contained zips into artifacts/

# dev: backend and Vite dev server (Vite rewrites Host, so allow its origin)
Kestry__AllowedOrigins__0=http://localhost:5173 dotnet run --project src/Kestry.Server
cd src/kestry-web && npm run dev              # http://localhost:5173, proxies /api to :8080
```

Toolchain: .NET SDK 10, Node 22, `aria2c` 1.37+. Docker image build: `docker build -f docker/Dockerfile -t kestry .`

## Conventions

- Backend: file-scoped namespaces, primary constructors, `Nullable` on. JSON is camelCase (`JsonSerializerDefaults.Web`). aria2 sends every number as a string: parse with `Aria2Json.Options`.
- i18n: `const t = useTranslations('Namespace'); t('key', { name })`; messages use the ICU subset `{name}` and `{count, plural, one {# x} other {# xs}}`. Outside components use `createTranslator(locale, ns)`. The locale store (`setLocale`) persists to `localStorage['kestry-locale']`, sets `<html lang>` and re-renders every hook user; no provider needed. Default `en`, otherwise the browser's languages. Server-provided error text (API `error` fields, aria2 messages) is not translated. Tests run in `es` (`test-setup.ts`).
- Frontend: only Tailwind classes that map to Kestrel tokens (no `bg-[#...]`, no arbitrary sizes). Tailwind's default palette is disabled (`--color-*: initial`). Components follow the Kestrel README rules (`cere` never for text, status always has a word, sentence case, no exclamation marks).
- Time-dependent code takes `TimeProvider`; the real power-off command is behind `ISystemPower` and must never run in tests.
- Tests that start the host use `KestryFactory` (isolated temp data/downloads, free aria2 ports). Never start aria2 on fixed ports in tests.

## Gotchas learned the hard way

- **aria2 RPC**: send bodies with `Content-Length` (it cannot parse chunked, so no `PostAsJsonAsync`). `system.multicall` takes **no** top-level token; put the token inside each sub-call. A bare `string[]` passed to `params object[]` is treated as the params array itself (see `Aria2Client.TellActive`).
- **Seeding**: `--seed-ratio=0` means *seed forever*. "Seeding off" is `seed-time=0` (`SettingsService.ToAria2Options`, shared by startup args and hot-apply).
- **`Urls`** in `appsettings.json` beats `ASPNETCORE_URLS`; override with the `Urls` env var (the Dockerfile does).
- **Content root** is `AppContext.BaseDirectory` (so `wwwroot/`, `appsettings.json` and the relative `DataDir` resolve next to the executable, also as a service). The csproj copies `wwwroot/` to the build output.
- `RequestGuard` allows `/api` only for loopback/IP/`AllowedOrigins` hosts and, on mutating methods, same-origin or allowed `Origin`. Reads are not origin-checked. Kestrel body limits are not enforced by `TestServer`; they are asserted via configuration and were verified against real Kestrel.
- Docker: use the TARGETARCH→RID mapping in the Dockerfile (`-a amd64` did not restore); keep `Dockerfile.dockerignore` or stale `obj/` breaks the in-image restore.
- Failed/stale snapshot reads are logged as warnings (`SnapshotBuilder`); an always-empty list in the UI means check the log first.

## Definition of done

`dotnet test`, `npm test`, `npm run typecheck` and `npm run build` pass; new behavior has tests; user-visible text is in all four `i18n/messages/*.json` files; README/AGENTS updated if commands, config or behavior changed.
