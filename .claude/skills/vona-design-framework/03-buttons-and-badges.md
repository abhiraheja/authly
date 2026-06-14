# 03 — Buttons & Badges

From `ui-core`. Vona extends Bootstrap buttons/badges with **soft**, **ghost**, **lighten**, and shape variants. Use the theme color suffixes everywhere: `primary secondary success danger warning info light dark` (+ skin accent).

## Buttons

### Fill / style variants (suffix with any color)
| Variant | Class pattern | Look |
|---|---|---|
| Solid | `btn btn-{color}` | filled |
| Outline | `btn btn-outline-{color}` | bordered, transparent fill |
| **Soft** | `btn btn-soft-{color}` | tinted (light bg + colored text) — *theme extension* |
| **Ghost** | `btn btn-ghost-{color}` | no bg until hover — *theme extension* |
| Rounded | add `btn-rounded` | pill-shaped |
| Link | `btn btn-link` | looks like a link |

### Sizes & states
- Sizes: `btn-sm`, (default), `btn-lg`.
- Block/full width: `d-grid` wrapper or `w-100`.
- Disabled: `disabled` class (on `<a>`) or `disabled` attr (on `<button>`).
- Toggle: `btn` + `data-bs-toggle="button"` + `active`.

### Icon buttons
Icon-only: square button containing a Lucide icon. Pattern:
```html
<button type="button" class="btn btn-soft-primary btn-icon">
  <i data-lucide="settings"></i>
</button>
```
Icon + text: place `<i data-lucide="…">` before the label inside the button. Confirm whether the package uses `btn-icon` for square sizing.

### Button groups
```html
<div class="btn-group" role="group" aria-label="...">
  <button class="btn btn-outline-primary">Left</button>
  <button class="btn btn-outline-primary">Mid</button>
  <button class="btn btn-outline-primary">Right</button>
</div>
```
Vertical: `btn-group-vertical`. Toolbar of groups: `btn-toolbar`.

## Badges

### Variants
| Variant | Class pattern | Notes |
|---|---|---|
| Solid | `badge bg-{color}` / `badge text-bg-{color}` | standard |
| **Lighten** | `badge badge-lighten-{color}` | tinted bg — *theme extension* |
| **Outline** | `badge badge-outline-{color}` | bordered — *theme extension* |
| Pill | add `rounded-pill` | rounded |
| **Label** | `badge badge-label` | label-style — *theme extension* |
| **Square** | `badge badge-square` | fixed square — *theme extension* |
| **Circle** | `badge badge-circle` | fixed circle (counts/dots) — *theme extension* |

### Common uses
- Count on a button/icon: position with `position-absolute top-0 start-100 translate-middle`.
- Inside headings: `<h5>Title <span class="badge bg-soft-... ">New</span></h5>`.

```html
<span class="badge badge-lighten-success">Active</span>
<span class="badge badge-outline-danger">Failed</span>
<span class="badge bg-primary rounded-pill">12</span>
```

> Confirm theme-extension class names (`btn-soft-*`, `btn-ghost-*`, `badge-lighten-*`, `badge-outline-*`, `badge-label/square/circle`) against your licensed `app.css`. The color list always tracks the active skin's palette.

Next: [04 — Cards, avatars & list groups](04-cards-avatars-listgroups.md).
