# Interactions API

`assets/app.js` provides a document-level layer so common patterns work with **zero per-page JS**.
`mountShell` (and a DOMContentLoaded fallback) call `wireApp()`, which runs `globalInit()` once and
`wire()` after each content injection.

## Declarative (data-attributes)
| Pattern | Markup |
|---|---|
| Dropdown | trigger `data-dropdown="ID"`; panel `<div id="ID" data-dropdown-menu class="menu hidden absolute …">` |
| Dismiss | `<button data-dismiss>` removes closest `[data-alert]` (or `data-dismiss="#sel"`) |
| Tabs | `[data-tabs-root]` › `[data-tabs]` with `.tab[data-target]` › panels `[data-panel]` (`hidden` to hide) |
| Segmented | `<div class="segmented" data-segmented>` with `<button>`s (one `.active`) |
| Accordion | `[data-accordion]`›`[data-acc-item]`›`[data-acc-trigger]`+`[data-acc-panel]` (+`[data-acc-chev]`) |
| Table select-all | header `[data-check-all]`, rows `[data-row-check]` |
| Rating | `<span data-rating data-value="N">` containing 5 `<i data-star>` |

Behaviour notes:
- Dropdowns: clicking the trigger toggles; outside-click and **Esc** close all open menus.
- Tabs/segmented/accordion/rating bind idempotently (safe to re-run after injecting HTML).

## Imperative (JS globals)
```js
toast(message, type)          // type: "success" | "error" | "info" | "warning" — top-right, auto-dismiss 4s
openOverlay(id)               // adds .open to #id and #id-overlay
closeOverlay(id)              // removes .open
```
Modal/drawer require `#id` (`.modal` or `.drawer`) AND `#id-overlay` (`.overlay`). Esc and
overlay-click also close them.

## Re-rendering dynamic content
After you set `innerHTML` that contains:
- **Lucide icons** → call `lucide.createIcons()`.
- **New data-attribute widgets** → call `wireApp()` (binds tabs/segmented/accordion/rating/check-all on the new nodes).

## Gotchas
- Don't put unescaped apostrophes inside `onclick="toast('...')"` — they break the JS string. Reword or use `&#39;`.
- `${...}` only works inside JS template literals (backtick strings in `<script>`), NOT in static HTML inside `<template>`. Generate such markup from JS, or hard-code it.
- Give dropdown menus `z-50` so they sit above content.
