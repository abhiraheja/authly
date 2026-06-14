# 12 — Icons

Three icon sets ship (`icons-lucide`, `icons-tabler`, `icons-flags`). **Lucide** is the primary set for app chrome (sidebar, topbar, buttons).

## Lucide (primary)
Placeholder elements hydrated by a global call:
```html
<i data-lucide="home"></i>
<i data-lucide="settings" class="text-muted"></i>
```
```js
lucide.createIcons();   // runs in app.js on load
```
- The placeholder is replaced by an inline `<svg>` that inherits `currentColor` and font-size, so style it with `text-{color}` and `fs-*`/inline size on the parent.
- **After injecting dynamic DOM** (AJAX, partial swap, modal/toast content, DataTables draw), call `lucide.createIcons()` again — new placeholders are not auto-rendered.
- Find icon names in the Lucide catalog (the `data-lucide` value = the kebab-case icon name).

## Tabler icons
Icon-font / SVG set demoed on `icons-tabler`. Usage is class-based:
```html
<i class="ti ti-home"></i>      <!-- confirm prefix (e.g. ti / tabler-) in your build -->
```
Color/size via `text-{color}` + `fs-*`. Use when an icon isn't in Lucide.

## Flags
`icons-flags` demos country flag icons (flag-icon style):
```html
<span class="flag fi fi-us"></span>   <!-- confirm exact prefix in your build -->
```
Use for language switchers (topbar) and locale lists.

## Rules
- Standardize app chrome on **Lucide**; only reach for Tabler/flags when needed.
- Never paste raw SVG when a `data-lucide`/class reference works — keeps markup small and themeable.
- Size/color via utilities + `currentColor`, never hardcoded fills, so icons follow the skin + light/dark mode.
- Always re-init Lucide after dynamic DOM changes.

Next: [13 — Theming & customizer](13-theming-and-customizer.md).
