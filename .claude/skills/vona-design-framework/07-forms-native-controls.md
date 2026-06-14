# 07 — Forms: Native Controls

From `form-elements` + `form-validation`. Standard Bootstrap 5 form controls, restyled. Always pair a `.form-label` with each control and use `.form-text` for help.

## Text inputs & textarea
```html
<label for="name" class="form-label">Name</label>
<input type="text" class="form-control" id="name" placeholder="…">
<div class="form-text">Helper text.</div>

<textarea class="form-control" rows="3"></textarea>
```
- Sizes: `form-control-sm`, `form-control-lg`.
- States: `disabled`, `readonly`, plaintext (`form-control-plaintext`).
- File: `<input type="file" class="form-control">`.

## Selects
```html
<select class="form-select">
  <option>One</option>
</select>
```
- Sizes: `form-select-sm`, `form-select-lg`. Multiple: `multiple` attr.
- For searchable/tag selects use **Choices.js** (see file 08).

## Checkboxes, radios, switches
```html
<div class="form-check">
  <input class="form-check-input" type="checkbox" id="c1">
  <label class="form-check-label" for="c1">Remember me</label>
</div>

<div class="form-check form-switch">
  <input class="form-check-input" type="checkbox" role="switch" id="s1">
  <label class="form-check-label" for="s1">Enable</label>
</div>

<div class="form-check">
  <input class="form-check-input" type="radio" name="r" id="r1">
  <label class="form-check-label" for="r1">Option</label>
</div>
```
- Inline: add `form-check-inline`.
- **Colored** checks/switches are a theme extension (e.g. `form-check-{color}` / a custom class) — confirm in `app.css`.
- Switch sizes (16px/20px) are theme extensions — confirm.
- Toggle buttons: `btn-check` + `<label class="btn btn-outline-primary">` inside a `btn-group`.

## Input groups
```html
<div class="input-group">
  <span class="input-group-text">@</span>
  <input type="text" class="form-control" placeholder="username">
  <button class="btn btn-outline-secondary">Go</button>
</div>
```
- Addons: `input-group-text` (text/icon/checkbox). Sizes: `input-group-sm|lg`.
- Can contain dropdowns, buttons, multiple addons.

## Floating labels
```html
<div class="form-floating">
  <input type="email" class="form-control" id="fl" placeholder="name@x.com">
  <label for="fl">Email address</label>
</div>
```
Works with inputs, textarea, and selects.

## Range
```html
<label for="rg" class="form-label">Volume</label>
<input type="range" class="form-range" id="rg" min="0" max="100">
```

## Validation
Two patterns:
- **Server/explicit state:** add `is-valid` / `is-invalid` to the control + sibling `valid-feedback` / `invalid-feedback`.
```html
<input class="form-control is-invalid">
<div class="invalid-feedback">Please enter a value.</div>
```
- **Bootstrap client validation:** `<form class="needs-validation" novalidate>` + JS that adds `was-validated` on submit. The `form-validation` page demos this and the wizard (`form-wizard`).

> Confirm theme-extension classes for colored checks/switches and switch sizes. Everything else here is stock Bootstrap 5.3.

Next: [08 — Forms: plugin widgets](08-forms-plugins.md).
