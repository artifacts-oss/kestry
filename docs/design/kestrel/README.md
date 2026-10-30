# Kestrel

Calm, light, modern UI drawn from the American kestrel: slate-blue crown and wings, rufous back, cream cheeks, black wing spots and a small orange cere. The plumage gives the whole system its logic — large quiet fields of cream and slate, one warm accent, and a single bright signal used sparingly.

## Principles

- **Quiet ground, sharp detail.** Most of every screen is `surface-000`/`surface-100`. Color appears where the eye should land, like the kestrel's face markings against soft feathers.
- **One cool, one warm.** `slate-600` is the primary (actions, links, selection). `rufous-*` is the accent (secondary emphasis, illustration). Never put both as equal-weight buttons side by side.
- **Signal is rare.** `cere` marks only what is new or needs attention — a dot, a badge. If more than one cere element is visible, one is too many.
- **Lines, not shadows.** Cards separate with `line` hairlines on `surface-100`. `shadow-soft` is only for things that float (popovers, modals).
- **Soft geometry.** `radius-md` on buttons and cards, `radius-lg` on sheets; pills for tags and status.

## Content

Plain, calm, short. Sentence case everywhere ("Save changes", not "Save Changes"). Say what happens, not how the system feels: "Report sent" beats "Success!". No exclamation marks in UI copy.

## Color

| Role | Token | On it |
| --- | --- | --- |
| Page | `surface-000` | `ink`, `ink-muted` |
| Card / panel | `surface-100` + `line` border | `ink`, `ink-muted` |
| Input / well | `surface-200` + `line-strong` border | `ink`, `ink-muted` |
| Primary action | `slate-600` (hover `slate-700`) | `on-slate` |
| Accent action / accent text | `rufous-700` | `on-rufous` |
| Selection, info | `slate-100` | `ink` |
| Warm callout | `rufous-100` | `ink` |
| Success / error | `moss` / `danger` text on surfaces | — |
| New / attention dot | `cere` | no text |

All text pairs above meet 4.5:1 in both themes; `line-strong` and `rufous-500` meet 3:1 for borders and large shapes. `rufous-500`, `sand` and `cere` are never used for text.

## Typography

Fraunces (display, soft optical serif) for titles; Manrope (geometric sans) for everything else; JetBrains Mono (`mono`) for URLs, paths and checksums. All load from Google Fonts. Use `display` once per page at most; UI runs on `body`, `body-sm`, `label`.

## Spacing and shape

4px base: `space-1` 4 · `space-2` 8 · `space-4` 16 · `space-6` 24 · `space-8` 32 · `space-12` 48. Card padding is `space-6`; section rhythm `space-12`. Radii: `radius-sm` inputs, `radius-md` buttons and cards, `radius-lg` sheets, `radius-full` pills.

## Iconography and imagery

`Icon` ships a small outline set (1.75 stroke, 24px grid) covering download-manager needs; see `Kestrel.iconNames`. Draw it in `ink` or `ink-muted`, 20px in UI; `slate-600` only for an active state. Photography: natural light, shallow depth of field, soft warm-gray backgrounds, one clear subject — like the source images this system was built from. Pattern accents use `mark` dots over slate (the wing spots), never more than one pattern per view.

## Focus and states

Focus: 2px `focus` ring, 2px offset, on every interactive element. Disabled: 40% opacity, no hover change. Error fields: `danger` border plus a text message — never color alone.

## Components (download manager)

React 18 components on `window.Kestrel`, styled by `components/bundle.css`.

- **App shell:** a 240px sidebar on `surface-100` with `NavItem` filters (All, Downloading, Queued, Completed, Failed) and `SpeedStat` totals; main area on `surface-000` with `Toolbar` on top and a stack of `DownloadItem` rows.
- **Status language:** `StatusBadge` and `ProgressBar` share five states: downloading (slate), queued (neutral), paused (rufous), completed (moss), failed (danger). Every state also has a word, never color alone.
- **Flows:** *Add download* opens a `Dialog` (URL in a `mono` `TextField`, destination, options); results arrive as a `Toast`; an empty filter shows an `EmptyState`.
- **Settings:** `Switch` for live toggles, `Checkbox` for options applied on save, `Select` for short fixed choices.

The consumer provides data (sizes in bytes, speed in bytes/s, ETA in seconds) and handlers; components hold no download logic.
