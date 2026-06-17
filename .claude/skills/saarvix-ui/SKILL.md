---
name: saarvix-ui
description: Build and edit admin/dashboard/SaaS UIs the SAARVIX way — HTML + Tailwind (CDN) on a portable token + component layer (theme.css, tw-config.js, shell.js, app.js). Use whenever creating or modifying any admin panel, dashboard, CRM/CMS, settings, table, form, modal, chart, or component page; scaffolding a new UI project or page; or matching the SAARVIX design system (Inter/SF-Pro type, ≤8px corners, purple #6A5ACD brand, sidebar+topbar shell). Triggers on requests like "build a dashboard", "add a page", "make a UI", "create a component", "admin panel", "design system".
---

# SAARVIX UI System

A reusable, framework-agnostic admin-UI system: **static HTML + Tailwind (Play CDN)** layered on a
portable design-token stylesheet and a tiny JS shell + interaction layer. No build step. Works in any
project by copying four files from `assets/` in this skill.

## When you build UI, follow THIS system — do not invent a new one.

---

## 1. Setup (once per project)
Copy these four files from this skill's `assets/` into the project's `assets/` folder:
- `theme.css` — design tokens (light+dark) + the component library (`.btn`, `.card`, `.badge`, `.input`, `.table`, `.timeline`, `.progress`, `.drawer`, `.modal`, `.tt` tooltip, `.skeleton`, …).
- `tw-config.js` — maps tokens onto Tailwind utilities (`bg-primary`, `text-muted-foreground`, opacity variants, fonts, ≤8px radius scale).
- `shell.js` — renders sidebar + top header from a `NAV` array; exposes `mountShell({active,title})`.
- `app.js` — global interactions (toasts, dropdowns, dismiss, tabs, segmented, accordion, ratings, table select-all) + `openOverlay`/`closeOverlay`.

Then edit the `NAV` array near the top of `shell.js` to match the project's modules.

## 2. Every page follows this exact skeleton
```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" /><meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>PAGE — APP</title>
  <script>try{if(localStorage.getItem("saarvix-theme")==="dark")document.documentElement.classList.add("dark")}catch(e){}</script>
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap" rel="stylesheet">
  <link rel="stylesheet" href="assets/theme.css" />
  <script src="https://cdn.tailwindcss.com"></script>
  <script src="assets/tw-config.js"></script>
  <script src="https://unpkg.com/lucide@latest"></script>
  <!-- add Chart.js ONLY on chart pages:
  <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script> -->
</head>
<body>
<template id="page-content">
  <!-- PAGE CONTENT ONLY — the shell injects sidebar + header around this -->
</template>
<script src="assets/shell.js"></script>
<script src="assets/app.js"></script>
<script>
  mountShell({ active: "KEY", title: "TITLE" });   // KEY must match a NAV item key
  // page-specific JS here
  lucide.createIcons();
</script>
</body>
</html>
```
- Put **only the main content** inside `<template id="page-content">`. Never hand-write the sidebar/header.
- `active` = the `key` of the current NAV item (drives highlight). `title` = header text.
- Always end the inline script with `lucide.createIcons();` (and call it again after injecting dynamic HTML that contains icons).
- A copy-paste starting point is in `templates/page.html`. Full-screen pages (login, 404) use `templates/auth.html` — they do NOT use the shell (`<template>`, shell.js, app.js, mountShell are omitted).

## 3. Design rules (non-negotiable)
- **Type:** Inter, with a native SF-Pro system stack as first fallback (already set in theme.css / tw-config.js). Headings use `.font-display` + tight tracking.
- **Corners:** tight — 8px cards, 6px controls, 4px chips. Never exceed 8px. Use the component classes or `rounded-lg`/`rounded-xl` (capped ≤8px by tw-config).
- **Color:** use semantic tokens only — `primary, teal, mint, coral, success, warning, danger, info, muted, accent, border, ink, slate` + `background/card/foreground/muted-foreground`. Opacity variants work (`bg-primary/10`). NEVER hard-code hex except gradient utilities (`from-primary to-teal`) and genuine brand colors (e.g. platform logos).
- **Theme:** everything must look right in light AND dark. Drive all color from tokens so dark mode is automatic.
- **Spacing:** generous, consistent. Page wrappers use `space-y-4/5/6`; component galleries use `max-w-5xl space-y-8`.
- **Icons:** Lucide only — `<i data-lucide="name" class="size-4"></i>`. Don't add other icon fonts.
- **Re-theme** a whole product by editing the RGB channel tokens in `:root` of `theme.css`.

## 4. Reuse components — don't rebuild them
Use the classes from `theme.css` (full list + examples in `reference/cheatsheet.md`):
`.btn` (+ `btn-primary/secondary/outline/ghost/danger/success`, `btn-sm`, `btn-lg`, `btn-icon`),
`.card`/`.card-header`/`.card-title`/`.card-body`/`.card-footer`, `.badge` (+ variants, `.dot`),
`.input`/`.select`/`.textarea`/`.label`/`.input-sm`, `.switch`, `.avatar`, `.table`, `.tabs`/`.tab`,
`.segmented`, `.progress`, `.timeline`/`.timeline-item`, `.menu`/`.menu-item`/`.menu-sep`,
`.tt`+`.tt-bubble` (tooltip), `.skeleton`, `.kbd`, `.divide-rows`, `.overlay`+`.modal`/`.drawer`.

## 5. Interactions are wired globally — use the data-attribute API (no custom JS needed)
- **Dropdown:** `<button data-dropdown="menuId">` + `<div id="menuId" data-dropdown-menu class="menu hidden absolute …">`. Opens/closes, outside-click + Esc close it.
- **Dismiss:** `<button data-dismiss>` removes the closest `[data-alert]` (or `data-dismiss="#sel"`).
- **Tabs:** wrap in `[data-tabs-root]`; tabs `<button class="tab" data-target="x">` inside `[data-tabs]`; panels `<div data-panel="x" class="hidden">`.
- **Segmented:** `<div class="segmented" data-segmented>` with `<button>`s (one `.active`).
- **Accordion:** `[data-accordion] > [data-acc-item] > ([data-acc-trigger] + [data-acc-panel])`, chevron `[data-acc-chev]`.
- **Table select-all:** master `[data-check-all]`, rows `[data-row-check]`.
- **Ratings:** `<span data-rating data-value="N">` containing 5 `<i data-star>` stars.
- **Toast (anywhere):** `toast(message, "success"|"error"|"info"|"warning")`.
- **Modal/Drawer:** `openOverlay('id')` / `closeOverlay('id')`; needs `#id` (`.modal`/`.drawer`) + `#id-overlay` (`.overlay`). Esc + overlay-click close.

## 6. Charts (Chart.js, themed with tokens)
Add the Chart.js CDN to the head, then:
```js
const css = v => getComputedStyle(document.documentElement).getPropertyValue(v).trim();
const rgb = (v, a = 1) => `rgb(${css(v)} / ${a})`;
Chart.defaults.font.family = "Inter";
Chart.defaults.color = rgb("--muted-foreground");
Chart.defaults.borderColor = rgb("--border");
new Chart(canvas, { /* use rgb('--primary'), rgb('--teal'), … ; maintainAspectRatio:false in an h-64 wrapper */ });
```

## 7. Adding a page or module
- **New page:** copy `templates/page.html`, set `mountShell({active,title})`, build content in the template.
- **New nav item / module:** add an entry to the `NAV` array in `shell.js` (`{ key, label, icon, href }`; supports `children:[…]` for nested/collapsible sub-menus). The `key` must equal the page's `mountShell` `active`.

## 8. Quality bar
- Match the surrounding code's density and idioms. Realistic placeholder data, not lorem dumps.
- Responsive: desktop-first, must work down to ~1024 and tablet; sidebar collapses on mobile (handled by shell).
- Don't introduce new dependencies beyond Tailwind CDN, Lucide, and (when charting) Chart.js.
- When unsure how a control looks/behaves, open `reference/cheatsheet.md` or mirror an existing page.

## 9. Deep references (read the relevant file when you need detail)
- `reference/cheatsheet.md` — copy-paste snippet for every component (start here when building).
- `reference/components.md` — per-component variants, states & rules.
- `reference/design-tokens.md` — all color/radius/shadow/type tokens + light/dark values.
- `reference/layout-shell.md` — `mountShell`, the `NAV` array shape, header features, collapse/responsive, theme persistence.
- `reference/interactions.md` — full data-attribute API + `toast`/`openOverlay`/`closeOverlay`, and re-rendering gotchas.
- `reference/patterns.md` — ready page recipes (dashboard, list+drawer, inbox, kanban, settings, auth, pricing…).
- `reference/charts.md` — themed Chart.js setup and chart recipes.
- `reference/pages-index.md` — catalog of ~100 example pages to mirror.
- `templates/page.html` (shell page) and `templates/auth.html` (full-screen, no shell) — scaffolds.
- `assets/` — the four core files to copy into a project (`theme.css`, `tw-config.js`, `shell.js`, `app.js`).
