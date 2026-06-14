# 08 — Forms: Plugin Widgets

From `form-plugins`, `form-fileuploads`, `form-quill-editor`. Vona wires these via a **`data-provider` / `data-*` attribute convention** — a global init script (in `app.js` or a page script) scans for the attribute and instantiates the plugin, so you mostly write markup, not JS. Confirm the exact attribute names/init in your package.

## Choices.js — enhanced selects
Searchable single/multi selects, tags, option groups.
```html
<select class="form-control" data-choices data-choices-multiple>
  <option value="1">One</option>
</select>
```
Modifiers seen: `data-choices`, `data-choices-groups`, `data-choices-removeItem` (remove button), `data-choices-multiple`, text-input variants, no-search, no-sort. Needs Choices CSS+JS on the page.

## Flatpickr — date / datetime picker
```html
<input type="text" class="form-control" data-provider="flatpickr" data-date-format="d M, Y">
<input type="text" class="form-control" data-provider="flatpickr" data-enable-time data-date-format="d.m.y, H:i">
<input type="text" class="form-control" data-provider="flatpickr" data-range-date>
```
Options seen: basic, datetime (`data-enable-time`), human-friendly, min/max date, default date, disabling dates, multiple dates, range (`data-range-date`), week numbers, inline.

## Timepickr — time picker
```html
<input type="text" class="form-control" data-provider="timepickr" data-time-basic>
```
Options seen: `data-time-basic`, 24h (`data-time-hrs`), limits (`data-min-time`/`data-max-time`), preloading, inline.

## Typeahead — autocomplete
Basic, Bloodhound engine, prefetch, default suggestions, custom template, multiple datasets. Init via JS with a dataset/source (jQuery typeahead-style) — see the package's page script.

## TouchSpin — number spinner
```html
<input type="text" value="0" data-provider="touchspin">
```
Variants seen: default, sizes, colors, readonly, disabled, styled, vertical.

## Quill — rich text editor
On `form-quill-editor`. A target `<div>` becomes the editor:
```html
<div id="snow-editor" style="height:300px"></div>
```
Init with `new Quill('#snow-editor', { theme: 'snow' /* or 'bubble' */, modules: { toolbar: [...] } })`. Sync content to a hidden input on submit.

## File uploads
On `form-fileuploads` — typically **Dropzone** (confirm) plus native `<input type="file" class="form-control">`. Dropzone pattern:
```html
<form class="dropzone" id="myDropzone" action="/upload"></form>
```

## Authoring rules
- **Load each plugin's CSS/JS only on pages that use it** (page `@section`/script tag), not globally.
- Prefer the **`data-*` attribute convention** so the global initializer wires the widget — keeps markup declarative.
- After AJAX/partial injection, **re-run the relevant init** for new elements (Choices/Flatpickr/tooltips don't auto-detect new DOM).
- Confirm exact attribute names (`data-provider`, `data-choices*`, etc.) and which file-upload lib ships, against your licensed copy.

Next: [09 — Tables](09-tables.md).
