# 13 — Theming & Customizer

Theme/layout state is **declarative** — a set of `data-*` attributes on `<html>` (see file 02). The customizer offcanvas and the topbar theme toggle just flip these attributes; theme CSS reacts via CSS variables. **Never** swap whole stylesheets or use `prefers-color-scheme`.

## The config attributes (on `<html>`)
| Attribute | Purpose | Typical values |
|---|---|---|
| `data-bs-theme` | light/dark color mode | `light`, `dark` |
| `data-skin` | active prebuilt skin | `shadcn` (default), `corporate`, `spotify`, `saas`, `nature`, `vintage`, `leafline`, `ghibli`, `slack`, `material`, `flat`, `pastel`, `caffieine`, `redshift` |
| `data-sidenav-color` / `data-menu-color` | sidebar skin | `light`, `dark`, `brand`, `gradient` |
| `data-sidenav-size` | sidebar state | `default`, `compact`, `condensed`, `offcanvas`, `full` |
| `data-topbar-color` | topbar skin | `light`, `dark` |
| `data-layout` / `data-layout-mode` | container style | `fluid`, `boxed`, `detached` |

> Confirm the exact attribute names and value vocabulary in your package's `config.js` — vendors vary the spelling. The 4 marketing "layouts" (Default / Sidebar-Dark / Topbar-Dark / Monochrome) are just presets of these attributes.

## 14 skins
Skins are palette/style swaps applied by `data-skin` (Shadcn is the default). They re-map the CSS color variables (primary accent, surfaces). Because every component references `var(--bs-*)`, switching `data-skin` restyles the whole app with no markup change. Each skin supports light **and** dark.

## Avoiding the theme flash (FOUC)
Apply the saved config **before first paint**. The template ships a tiny `config.js` loaded **early in `<head>`** (before the CSS) that reads the persisted choice (localStorage/cookie) and sets the `<html>` attributes synchronously. Keep that script first; don't move it to the bottom.

## The customizer offcanvas
An `offcanvas offcanvas-end` panel (e.g. `#theme-settings-offcanvas`) with controls for: color mode, skin, sidebar color/size, topbar color, layout. Each control writes the corresponding `data-*` attribute on `<html>` and persists it (localStorage). There's usually a "reset" button restoring defaults.

```html
<!-- trigger (often in topbar) -->
<button class="btn btn-icon" data-bs-toggle="offcanvas" data-bs-target="#theme-settings-offcanvas">
  <i data-lucide="settings"></i>
</button>
```

## Persisting per user
- **Client-only:** the template's localStorage default — simplest; survives reloads on that device.
- **Server-side (recommended for an app):** store the chosen attributes against the user (cookie/DB), emit them onto `<html>` when rendering the page, and let the customizer continue to update both the DOM and the store. This gives consistent theming across devices and avoids the flash without relying on JS.

## When the theme changes at runtime
Components using CSS variables update automatically. **Canvas charts do not** — re-read CSS variables and `chart.update()`/recreate on theme change (see file 10). Re-run `lucide.createIcons()` only if you re-rendered icon markup.

Next: [14 — Page templates](14-page-templates.md).
