# 04 — Cards, Avatars & List Groups

Cards are the primary content container in Vona — almost every page section is a `.card`. From `ui-core`.

## Card anatomy

```html
<div class="card">
  <div class="card-header d-flex align-items-center justify-content-between">
    <h5 class="card-title mb-0">Title</h5>
    <div class="card-action"><!-- dropdown / buttons --></div>
  </div>
  <div class="card-body">
    <h5 class="card-title">…</h5>
    <p class="card-text text-muted">…</p>
    <a href="#" class="btn btn-primary">Action</a>
  </div>
  <div class="card-footer">…</div>
</div>
```

Pieces: `card-header`, `card-body`, `card-footer`, `card-title`, `card-subtitle`, `card-text`, `card-img-top`/`card-img-bottom`/`card-img-overlay`.

### Colored & bordered cards
- Background: `card text-bg-{color}` (BS 5.3 helper — sets bg + readable text).
- Border accent: `card border border-{color}` (+ `border-2` for thicker).
- Gradient: theme gradient bg utility (confirm class, e.g. `bg-gradient`).
- Whole-card clickable: put `<a class="stretched-link">` inside.

### Card groups & layout
- `card-group` for equal-width attached cards.
- Use the grid for spacing: `<div class="row g-3"><div class="col-md-4"><div class="card">…`.

### Widget/stat card pattern (dashboards)
A stat tile is a `.card` with a label, a big number, a delta badge, and a Lucide icon in a soft avatar circle. Compose with utilities (`d-flex`, `justify-content-between`, `fs-*`, `text-muted`) + an avatar (below) — there's no special "stat" component, just card + utilities.

## Avatars (*theme extension*)

Sizing classes wrap an `<img>` or an icon/initials span:
| Class | Use |
|---|---|
| `avatar-xs` | tiny (table rows, menus) |
| `avatar-sm` | small |
| `avatar-md` | default |
| `avatar-lg` | large |
| `avatar-xl` | extra large (profiles) |

Patterns:
```html
<!-- image avatar -->
<div class="avatar-md"><img src="..." class="img-fluid rounded-circle" alt=""></div>
<!-- icon/initials avatar in a soft tile -->
<div class="avatar-sm">
  <span class="avatar-title bg-soft-primary text-primary rounded-circle">
    <i data-lucide="user"></i>
  </span>
</div>
```
- `avatar-title` centers the inner content; combine with `bg-soft-*`/`text-*` + `rounded`/`rounded-circle`.
- **Avatar group** (overlapping stack): a wrapper (confirm `avatar-group`) with negative-margin children.

## List groups

Standard Bootstrap, restyled:
```html
<div class="list-group">
  <a href="#" class="list-group-item list-group-item-action active">Item</a>
  <a href="#" class="list-group-item list-group-item-action">Item</a>
  <a href="#" class="list-group-item list-group-item-action disabled">Item</a>
</div>
```
- Flush (no outer border): `list-group-flush`.
- Contextual: `list-group-item-{color}`.
- With badge: `d-flex justify-content-between align-items-center` + `<span class="badge ...">`.
- Numbered: `list-group-numbered`.

> Confirm `avatar-*`, `avatar-title`, `avatar-group`, `bg-soft-*` against your package.

Next: [05 — Visual feedback](05-visual-feedback.md).
