# 00 — Quick Reference (cheat-sheet)

One-page class lookup across the whole library. Color suffix `{c}` = `primary secondary success danger warning info light dark` (+ active skin accent). Items marked ⚑ are theme extensions — confirm exact spelling in your licensed `app.css`. Deep dives are linked per row.

## Shell & theming → [02](02-layout-shell-and-dom-taxonomy.md) · [13](13-theming-and-customizer.md)
| Need | Hook |
|---|---|
| App frame | `.wrapper` > `.sidenav-menu`⚑ + `header`/topbar⚑ + `.page-content` + `footer` |
| Scroll region (sidebar) | `data-simplebar` |
| Sidebar menu | `ul.side-nav`⚑ > `li.side-nav-title` (group) / `li.side-nav-item > a.side-nav-link` |
| Sub-menu | `a[data-bs-toggle="collapse"]` + `.menu-arrow` → `.collapse > ul.sub-menu` |
| Page title row | `.page-title-head`⚑ + `h4` + `ol.breadcrumb` |
| Color mode | `<html data-bs-theme="light|dark">` |
| Skin (14) | `<html data-skin="shadcn|corporate|…">`⚑ |
| Sidebar/topbar/layout | `data-sidenav-color|size`, `data-topbar-color`, `data-layout`⚑ |
| Customizer | `.offcanvas.offcanvas-end#theme-settings-offcanvas` |

## Buttons & badges → [03](03-buttons-and-badges.md)
| Need | Hook |
|---|---|
| Button | `btn btn-{c}` · `btn-outline-{c}` · `btn-soft-{c}`⚑ · `btn-ghost-{c}`⚑ · `btn-rounded`⚑ |
| Size / block | `btn-sm` `btn-lg` · `d-grid`/`w-100` |
| Icon button | `btn btn-icon`⚑ + `<i data-lucide="…">` |
| Group | `.btn-group` / `.btn-group-vertical` / `.btn-toolbar` |
| Badge | `badge bg-{c}`/`text-bg-{c}` · `badge-lighten-{c}`⚑ · `badge-outline-{c}`⚑ · `+rounded-pill` |
| Badge shapes | `badge-label`⚑ `badge-square`⚑ `badge-circle`⚑ |

## Cards / avatars / lists → [04](04-cards-avatars-listgroups.md)
| Need | Hook |
|---|---|
| Card | `.card` > `.card-header`/`.card-body`/`.card-footer` + `.card-title`/`.card-text` |
| Colored / bordered | `card text-bg-{c}` · `card border border-{c} border-2` |
| Clickable card | inner `a.stretched-link` |
| Avatar size | `avatar-xs|sm|md|lg|xl`⚑ + `.avatar-title`⚑ + `bg-soft-{c}`⚑ |
| List group | `.list-group > .list-group-item(.-action/.active/.disabled)` · `-flush` · `-numbered` |

## Visual feedback → [05](05-visual-feedback.md)
| Need | Hook |
|---|---|
| Alert | `alert alert-{c} [alert-dismissible fade show]` + `.btn-close[data-bs-dismiss="alert"]` |
| Toast | `.toast-container.position-fixed` + `.toast` (`data-bs-autohide`/`-delay`); JS `bootstrap.Toast` |
| Progress | `.progress > .progress-bar.bg-{c}[.progress-bar-striped.progress-bar-animated]` |
| Spinner | `spinner-border`/`spinner-grow` + `text-{c}` (`-sm`) |
| Skeleton | wrapper `.placeholder-glow|wave` + `.placeholder.col-*` |
| Carousel | `.carousel.slide[data-bs-ride]` + `.carousel-inner > .carousel-item.active` |

## Interactive → [06](06-interactive-components.md)
| Need | Hook |
|---|---|
| Modal | trigger `data-bs-toggle="modal" data-bs-target` → `.modal > .modal-dialog(.modal-lg/-centered/-scrollable/-fullscreen) > .modal-content` |
| Offcanvas | `data-bs-toggle="offcanvas"` → `.offcanvas.offcanvas-{start|end|top|bottom}` |
| Dropdown | `.dropdown > .dropdown-toggle[data-bs-toggle="dropdown"]` + `.dropdown-menu(.-end) > .dropdown-item` |
| Tabs | `.nav.nav-tabs|nav-pills > .nav-link[data-bs-toggle="tab" data-bs-target]` + `.tab-content > .tab-pane.fade` |
| Collapse/accordion | `[data-bs-toggle="collapse" data-bs-target]` · `.accordion > .accordion-item > .accordion-button` |
| Tooltip/popover | `[data-bs-toggle="tooltip|popover"]` — **needs JS init**, re-init on dynamic DOM |

## Forms → [07](07-forms-native-controls.md) · plugins [08](08-forms-plugins.md)
| Need | Hook |
|---|---|
| Input / label / help | `.form-control` (`-sm`/`-lg`) + `.form-label` + `.form-text` |
| Select | `.form-select` (`-sm`/`-lg`) |
| Check / switch / radio | `.form-check[.form-switch] > .form-check-input + .form-check-label` (`form-check-inline`) |
| Input group | `.input-group > .input-group-text + .form-control` |
| Floating label | `.form-floating > input + label` |
| Range | `.form-range` |
| Validation | `.is-valid`/`.is-invalid` + `.valid-feedback`/`.invalid-feedback`; form `.needs-validation novalidate` → `.was-validated` |
| Plugin select | `data-choices [data-choices-multiple]` (Choices.js) |
| Date / time | `data-provider="flatpickr"` / `data-provider="timepickr"` |
| Number spinner | `data-provider="touchspin"` |
| Rich text | `new Quill('#editor',{theme:'snow'})` |

## Tables → [09](09-tables.md)
`.table-responsive > .table[.table-striped/.table-hover/.table-bordered/.table-sm].align-middle` · head `.table-light`/`.table-dark` · contextual `tr/td.table-{c}` · interactive: `new DataTable('#id',{responsive:true})` (load only the needed extensions; re-init Lucide in `drawCallback`).

## Charts → [10](10-charts.md)
`<div style="height:NNNpx"><canvas id="x"></canvas></div>` + `new Chart(el,{type:'line|bar|pie|doughnut|radar|polarArea|bubble|scatter', options:{responsive:true,maintainAspectRatio:false}})`. Colors from `getComputedStyle(html).getPropertyValue('--bs-primary')`; `chart.update()` on theme change.

## Icons → [12](12-icons.md)
Lucide `<i data-lucide="name">` + `lucide.createIcons()` (re-run after dynamic DOM). Tabler `<i class="ti ti-name">`⚑. Flags `.fi.fi-xx`⚑. Color/size via `text-{c}` + `fs-*`.

## Top utilities → [11](11-typography-and-utilities.md)
`text-{c}` · `bg-{c}` / `bg-{c}-subtle` / `text-bg-{c}` · `bg-soft-{c}`⚑ · `opacity-{0..100}` · `rounded(-{0..5}|-circle|-pill)` · `shadow(-sm|-lg)` · spacing `m*/p*/gap-/g*-{0..5}` · flex `d-flex justify-content-* align-items-* flex-grow-1` · sizing `w-/h-{25..100}` · `object-fit-cover` · `ratio ratio-16x9` · `position-* top-0 translate-middle`.

## Page templates → [14](14-page-templates.md)
Auth = standalone centered card (no shell): `auth-sign-in/-sign-up/-reset-pass/-new-pass/-two-factor/-lock-screen`. In-shell: `pages-pricing/-invoice/-timeline/-terms-conditions/-empty`, `404`. Apps: `ton-ai` (chat), `calendar`, `directory`.

← back to [SKILL.md](SKILL.md)
