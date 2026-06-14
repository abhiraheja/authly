# 05 — Visual Feedback

From `ui-visual-feedback` + `ui-core`. Loading states, status messages, progress, and slideshows.

## Alerts
```html
<div class="alert alert-success alert-dismissible fade show" role="alert">
  <strong>Done!</strong> Saved successfully. <a href="#" class="alert-link">View</a>.
  <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
</div>
```
- Color: `alert-{color}`.
- Dismissible: `alert-dismissible fade show` + `.btn-close[data-bs-dismiss="alert"]`.
- With icon: prepend a Lucide icon; use `d-flex align-items-center`.
- Theme may add soft/bordered alert variants — confirm in `app.css`.

## Toasts (notifications)
Container positions a toast in one of **9 placements** (top/middle/bottom × start/center/end) via utilities:
```html
<div class="toast-container position-fixed top-0 end-0 p-3">
  <div class="toast" role="alert" data-bs-autohide="true" data-bs-delay="4000">
    <div class="toast-header">
      <i data-lucide="bell" class="me-2"></i>
      <strong class="me-auto">{AppName}</strong><small>now</small>
      <button class="btn-close" data-bs-dismiss="toast"></button>
    </div>
    <div class="toast-body">Your message.</div>
  </div>
</div>
```
Init/show via JS: `bootstrap.Toast.getOrCreateInstance(el).show()`. Variants: translucent, stacked, custom content.

## Progress bars
```html
<div class="progress" role="progressbar" aria-valuenow="65" aria-valuemin="0" aria-valuemax="100" style="height:6px">
  <div class="progress-bar bg-success" style="width:65%"></div>
</div>
```
- Striped: `progress-bar-striped`; animated: add `progress-bar-animated`.
- Multiple/stacked: several `.progress-bar` inside one `.progress`.
- Labels, height variants, steps — set height via inline style/utility.

## Spinners
```html
<div class="spinner-border text-primary" role="status"><span class="visually-hidden">Loading…</span></div>
<div class="spinner-grow text-primary" role="status"></div>
```
- Types: `spinner-border`, `spinner-grow`.
- Color via `text-{color}`. Size: `spinner-border-sm`.
- In buttons: place spinner before label and disable the button while loading.

## Placeholders (skeletons)
```html
<p class="placeholder-glow"><span class="placeholder col-6"></span></p>
```
- Animation: `placeholder-glow` or `placeholder-wave` on the wrapper.
- Width via grid cols (`col-6`) or sizing utilities; color via `bg-*`.
- Use for skeleton loading of cards/tables before data arrives.

## Carousels
```html
<div id="carouselX" class="carousel slide" data-bs-ride="carousel">
  <div class="carousel-indicators">…</div>
  <div class="carousel-inner">
    <div class="carousel-item active"><img class="d-block w-100" src="..."></div>
    <div class="carousel-item"><img class="d-block w-100" src="..."></div>
  </div>
  <button class="carousel-control-prev" data-bs-target="#carouselX" data-bs-slide="prev">…</button>
  <button class="carousel-control-next" data-bs-target="#carouselX" data-bs-slide="next">…</button>
</div>
```
- Variants: controls, indicators, captions (`carousel-caption`), `carousel-fade` crossfade, per-slide `data-bs-interval`, dark (`carousel-dark`).

Next: [06 — Interactive components](06-interactive-components.md).
