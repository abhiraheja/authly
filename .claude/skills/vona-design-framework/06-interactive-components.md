# 06 — Interactive Components

From `ui-interactive`. All are Bootstrap 5 JS components driven by `data-bs-*` attributes (no jQuery). Some (tooltips, popovers) require explicit JS init.

## Modals
```html
<button class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#m1">Open</button>

<div class="modal fade" id="m1" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-dialog-centered">
    <div class="modal-content">
      <div class="modal-header">
        <h5 class="modal-title">Title</h5>
        <button class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body">…</div>
      <div class="modal-footer">
        <button class="btn btn-light" data-bs-dismiss="modal">Close</button>
        <button class="btn btn-primary">Save</button>
      </div>
    </div>
  </div>
</div>
```
Dialog modifiers: size `modal-sm|lg|xl`, `modal-fullscreen{-sm|md|lg|xl|xxl-down}`, `modal-dialog-centered`, `modal-dialog-scrollable`. Static backdrop: `data-bs-backdrop="static" data-bs-keyboard="false"`.

## Offcanvas
```html
<button data-bs-toggle="offcanvas" data-bs-target="#oc1">Open</button>
<div class="offcanvas offcanvas-end" tabindex="-1" id="oc1">
  <div class="offcanvas-header">
    <h5 class="offcanvas-title">Panel</h5>
    <button class="btn-close" data-bs-dismiss="offcanvas"></button>
  </div>
  <div class="offcanvas-body">…</div>
</div>
```
Placement: `offcanvas-start|end|top|bottom`. Options: `data-bs-scroll="true"`, `data-bs-backdrop="false"`. The customizer panel is an offcanvas (see file 13).

## Dropdowns
```html
<div class="dropdown">
  <button class="btn btn-soft-primary dropdown-toggle" data-bs-toggle="dropdown">Menu</button>
  <ul class="dropdown-menu">
    <li><a class="dropdown-item" href="#">Action</a></li>
    <li><hr class="dropdown-divider"></li>
    <li><a class="dropdown-item" href="#">Other</a></li>
  </ul>
</div>
```
Alignment: `dropdown-menu-end`. Directions: wrap with `dropup`/`dropend`/`dropstart`. Headers: `dropdown-header`. Used heavily in topbar (user, notifications) and card actions.

## Tabs & pills
```html
<ul class="nav nav-tabs" role="tablist">
  <li class="nav-item"><button class="nav-link active" data-bs-toggle="tab" data-bs-target="#t1">One</button></li>
  <li class="nav-item"><button class="nav-link" data-bs-toggle="tab" data-bs-target="#t2">Two</button></li>
</ul>
<div class="tab-content">
  <div class="tab-pane fade show active" id="t1">…</div>
  <div class="tab-pane fade" id="t2">…</div>
</div>
```
Variants: `nav-pills`, `nav-justified`/`nav-fill`, vertical (`flex-column` + `nav` in a col), bordered/colored/icon tabs (theme extensions — confirm classes).

## Collapse / accordion
```html
<a class="btn" data-bs-toggle="collapse" href="#c1">Toggle</a>
<div class="collapse" id="c1">…</div>

<div class="accordion" id="acc">
  <div class="accordion-item">
    <h2 class="accordion-header">
      <button class="accordion-button collapsed" data-bs-toggle="collapse" data-bs-target="#a1">Header</button>
    </h2>
    <div id="a1" class="accordion-collapse collapse" data-bs-parent="#acc">
      <div class="accordion-body">…</div>
    </div>
  </div>
</div>
```
Variants: `accordion-flush`, custom-icon, no-arrow. Horizontal collapse: `collapse-horizontal`. The sidebar sub-menus use collapse (see file 02).

## Tooltips & popovers (require JS init)
```html
<button data-bs-toggle="tooltip" data-bs-placement="top" title="Hi">Hover</button>
<button data-bs-toggle="popover" data-bs-trigger="focus" title="T" data-bs-content="Body">Click</button>
```
Init once after render:
```js
document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));
document.querySelectorAll('[data-bs-toggle="popover"]').forEach(el => new bootstrap.Popover(el));
```
Placements: top/bottom/left/right. Color variants are theme extensions (confirm). **Re-init after injecting dynamic DOM.**

Next: [07 — Forms (native controls)](07-forms-native-controls.md).
