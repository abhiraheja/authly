# SAARVIX UI — Component & API Cheat-sheet

Copy-paste snippets. All classes are defined in `assets/theme.css`; behaviours in `assets/app.js`.

## Buttons
```html
<button class="btn btn-primary">Primary</button>
<button class="btn btn-secondary">Secondary</button>
<button class="btn btn-outline">Outline</button>
<button class="btn btn-ghost">Ghost</button>
<button class="btn btn-danger">Danger</button>
<button class="btn btn-success">Success</button>
<button class="btn btn-primary btn-sm">Small</button>
<button class="btn btn-primary btn-lg">Large</button>
<button class="btn btn-outline btn-icon"><i data-lucide="settings" class="size-5"></i></button>
<button class="btn btn-primary"><i data-lucide="plus" class="size-4"></i>With icon</button>
```

## Social login buttons (Google / Microsoft / LinkedIn)
Lucide has no brand logos — use inline brand SVGs (genuine brand colors are allowed). Define each SVG once in a `<template>` and clone it into `[data-brand]` spans. Build on `.btn btn-outline btn-lg` (recommended) — neutral surface keeps it on-system; the logo carries recognition.
```html
<div class="grid gap-3 max-w-md">
  <button class="btn btn-outline btn-lg w-full justify-center gap-2.5 font-semibold"><span data-brand="google"></span>Continue with Google</button>
  <button class="btn btn-outline btn-lg w-full justify-center gap-2.5 font-semibold"><span data-brand="microsoft"></span>Continue with Microsoft</button>
  <button class="btn btn-outline btn-lg w-full justify-center gap-2.5 font-semibold"><span data-brand="linkedin"></span>Continue with LinkedIn</button>
</div>
<!-- icon-only variant: <button class="btn btn-outline btn-lg btn-icon" aria-label="Google"><span data-brand="google"></span></button> -->

<!-- Brand SVGs — paste once per page -->
<template id="brand-google"><svg class="size-5" viewBox="0 0 48 48" aria-hidden="true"><path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3c-1.6 4.7-6.1 8-11.3 8-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.9 1.2 8 3.1l5.7-5.7C34.1 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.3-.4-3.5z"/><path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.9 1.2 8 3.1l5.7-5.7C34.1 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z"/><path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2c-2 1.5-4.6 2.4-7.2 2.4-5.2 0-9.6-3.3-11.3-7.9l-6.5 5C9.6 39.6 16.2 44 24 44z"/><path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C39.9 35.8 44 30.4 44 24c0-1.3-.1-2.3-.4-3.5z"/></svg></template>
<template id="brand-microsoft"><svg class="size-5" viewBox="0 0 23 23" aria-hidden="true"><path fill="#F25022" d="M1 1h10v10H1z"/><path fill="#7FBA00" d="M12 1h10v10H12z"/><path fill="#00A4EF" d="M1 12h10v10H1z"/><path fill="#FFB900" d="M12 12h10v10H12z"/></svg></template>
<template id="brand-linkedin"><svg class="size-5" viewBox="0 0 24 24" aria-hidden="true"><path fill="#0A66C2" d="M20.45 20.45h-3.56v-5.57c0-1.33-.02-3.04-1.85-3.04-1.85 0-2.14 1.45-2.14 2.94v5.67H9.34V9h3.42v1.56h.05c.48-.9 1.64-1.85 3.37-1.85 3.6 0 4.27 2.37 4.27 5.46v6.28zM5.34 7.43a2.07 2.07 0 1 1 0-4.14 2.07 2.07 0 0 1 0 4.14zM7.12 20.45H3.56V9h3.56v11.45zM22.22 0H1.77C.8 0 0 .78 0 1.74v20.52C0 23.22.8 24 1.77 24h20.45c.98 0 1.78-.78 1.78-1.74V1.74C24 .78 23.2 0 22.22 0z"/></svg></template>
<!-- white LinkedIn glyph for a solid blue fill: same path, fill="#fff" -->
```
```js
// clone brand SVGs into placeholders (run once, after the DOM is ready / shell mounts)
document.querySelectorAll("[data-brand]").forEach(function (el) {
  var tpl = document.getElementById("brand-" + el.getAttribute("data-brand"));
  if (tpl) el.appendChild(tpl.content.cloneNode(true));
});
```

## Badges / status
```html
<span class="badge badge-primary">Primary</span>
<span class="badge badge-success"><span class="dot"></span>Active</span>
<span class="badge badge-warning"><span class="dot"></span>Pending</span>
<span class="badge badge-danger"><span class="dot"></span>Failed</span>
<span class="badge badge-info">Info</span>  <span class="badge badge-neutral">Neutral</span>
```

## Card
```html
<div class="card">
  <div class="card-header"><div class="card-title">Title</div>
    <button class="btn btn-ghost btn-icon btn-sm"><i data-lucide="more-horizontal" class="size-4"></i></button></div>
  <div class="card-body">…</div>
  <div class="card-footer">…</div>
</div>
<!-- simple: <div class="card card-body">…</div> -->
```

## Stat card
```html
<div class="card card-body">
  <div class="flex items-start justify-between">
    <div class="grid size-11 place-items-center rounded-xl bg-primary/10 text-primary"><i data-lucide="user-plus" class="size-5"></i></div>
    <span class="badge badge-success"><i data-lucide="trending-up" class="size-3"></i>12.4%</span>
  </div>
  <div class="mt-4 text-sm text-muted-foreground">Total Leads</div>
  <div class="mt-1 text-2xl font-semibold tracking-tight">8,642</div>
</div>
```

## Form controls
```html
<div><label class="label">Email</label><input class="input" placeholder="you@co.com"></div>
<select class="select"><option>One</option></select>
<textarea class="textarea" rows="3"></textarea>
<input class="input input-sm" placeholder="small">
<!-- checkbox/radio use native + accent --> <input type="checkbox" class="accent-[rgb(var(--primary))] size-4">
<!-- switch --> <span class="switch"><input type="checkbox" checked><span class="track"></span></span>
<!-- search input with icon -->
<div class="relative"><i data-lucide="search" class="size-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"></i><input class="input input-sm pl-9" placeholder="Search"></div>
```

## Avatar
```html
<div class="avatar size-9 bg-gradient-to-br from-primary to-teal text-xs">AS</div>
<!-- with status --> <div class="relative"><div class="avatar size-9 bg-primary text-xs">PN</div><span class="absolute -bottom-0.5 -right-0.5 size-3 rounded-full bg-success ring-2 ring-card"></span></div>
<!-- stack --> <div class="flex -space-x-2"><div class="avatar size-8 bg-primary text-xs ring-2 ring-card">A</div><div class="avatar size-8 bg-teal text-xs ring-2 ring-card">B</div></div>
```

## Table
```html
<div class="card overflow-hidden"><div class="overflow-x-auto scroll-thin"><table class="table">
  <thead><tr><th>Name</th><th>Status</th><th class="text-right">Value</th></tr></thead>
  <tbody>
    <tr><td class="font-medium">Acme</td><td><span class="badge badge-success"><span class="dot"></span>Won</span></td><td class="text-right">$48k</td></tr>
  </tbody>
</table></div></div>
```
Select-all: header `<input type="checkbox" data-check-all>`, rows `<input type="checkbox" data-row-check>`.

## Tabs (wired)
```html
<div data-tabs-root>
  <div class="tabs" data-tabs>
    <button class="tab active" data-target="a">Overview</button>
    <button class="tab" data-target="b">Activity</button>
  </div>
  <div data-panel="a">…</div>
  <div data-panel="b" class="hidden">…</div>
</div>
```

## Segmented (wired)
```html
<div class="segmented" data-segmented><button class="active">Day</button><button>Week</button><button>Month</button></div>
```

## Accordion (wired)
```html
<div class="card divide-rows" data-accordion>
  <div data-acc-item>
    <button data-acc-trigger class="w-full flex items-center justify-between p-4 text-left text-sm font-medium">
      Question<i data-lucide="chevron-down" data-acc-chev class="size-4 text-muted-foreground transition-transform"></i></button>
    <div data-acc-panel class="hidden px-4 pb-4 text-sm text-muted-foreground">Answer.</div>
  </div>
</div>
```
First item open: remove `hidden` from its panel and add `rotate-180` to its chevron.

## Dropdown (wired)
```html
<div class="relative">
  <button class="btn btn-outline" data-dropdown="menu1">Menu <i data-lucide="chevron-down" class="size-4"></i></button>
  <div id="menu1" data-dropdown-menu class="menu hidden absolute left-0 mt-2 w-48 z-50">
    <button class="menu-item"><i data-lucide="pencil" class="size-4"></i>Edit</button>
    <div class="menu-sep"></div>
    <button class="menu-item text-coral"><i data-lucide="trash-2" class="size-4"></i>Delete</button>
  </div>
</div>
```

## Tooltip (CSS-only)
```html
<span class="tt"><button class="btn btn-outline">Hover</button><span class="tt-bubble">Helpful tip</span></span>
```

## Alert (dismissible)
```html
<div data-alert class="card card-body !py-3 flex items-start gap-3 border-l-4" style="border-left-color:rgb(var(--success))">
  <i data-lucide="check-circle-2" class="size-5 text-success shrink-0"></i>
  <div class="text-sm flex-1"><div class="font-medium">Saved</div><div class="text-muted-foreground">Done.</div></div>
  <button data-dismiss class="btn btn-ghost btn-icon btn-sm"><i data-lucide="x" class="size-4"></i></button>
</div>
```

## Toast (JS)
```js
toast("Lead saved", "success");   // success | error | info | warning
```

## Modal (wired)
```html
<button onclick="openOverlay('m1')" class="btn btn-primary">Open</button>
<div id="m1-overlay" class="overlay" onclick="closeOverlay('m1')"></div>
<div id="m1" class="modal">
  <div class="p-5 border-b border-border flex items-center justify-between"><div class="font-semibold">Title</div>
    <button class="btn btn-ghost btn-icon btn-sm" onclick="closeOverlay('m1')"><i data-lucide="x" class="size-4"></i></button></div>
  <div class="p-5">…</div>
  <div class="p-5 border-t border-border flex justify-end gap-2">
    <button class="btn btn-outline" onclick="closeOverlay('m1')">Cancel</button>
    <button class="btn btn-primary" onclick="closeOverlay('m1');toast('Saved','success')">Save</button></div>
</div>
```
Sizes: add `!w-[400px]` (small) or `!w-[720px]` (large) to `.modal`.

## Drawer / Offcanvas (wired)
```html
<button onclick="openOverlay('d1')" class="btn btn-outline">Filters</button>
<div id="d1-overlay" class="overlay" onclick="closeOverlay('d1')"></div>
<aside id="d1" class="drawer">
  <div class="p-4 border-b border-border flex items-center justify-between"><div class="font-semibold">Filters</div>
    <button class="btn btn-ghost btn-icon" onclick="closeOverlay('d1')"><i data-lucide="x" class="size-5"></i></button></div>
  <div class="p-4 flex-1">…</div>
</aside>
```

## Progress / Skeleton / Spinner
```html
<div class="progress"><span style="width:62%"></span></div>
<div class="progress"><span class="bg-teal" style="width:40%"></span></div>
<div class="skeleton h-3 w-2/3"></div>
<span class="size-5 rounded-full border-2 border-muted border-t-primary animate-spin"></span>
```

## Timeline
```html
<div class="timeline">
  <div class="timeline-item"><div class="text-sm font-medium">Deal created</div><div class="text-xs text-muted-foreground">Today</div></div>
</div>
```

## Pagination
```html
<div class="flex items-center gap-1">
  <button class="btn btn-outline btn-icon btn-sm"><i data-lucide="chevron-left" class="size-4"></i></button>
  <button class="btn btn-primary btn-icon btn-sm">1</button>
  <button class="btn btn-ghost btn-icon btn-sm">2</button>
  <button class="btn btn-outline btn-icon btn-sm"><i data-lucide="chevron-right" class="size-4"></i></button>
</div>
```

## Color tokens (semantic)
`background, foreground, card, popover, primary (+foreground), secondary, muted (+foreground),
accent, success, warning, danger, info, border, input, ring, ink, teal, mint, coral, slate,
sidebar (+foreground/accent/border)`. Use as `bg-*`, `text-*`, `border-*`; opacity via `/NN`.

## Charts (Chart.js)
```js
const css=v=>getComputedStyle(document.documentElement).getPropertyValue(v).trim(), rgb=(v,a=1)=>`rgb(${css(v)} / ${a})`;
Chart.defaults.font.family="Inter"; Chart.defaults.color=rgb("--muted-foreground"); Chart.defaults.borderColor=rgb("--border");
new Chart(canvas,{type:"line",data:{labels:[…],datasets:[{borderColor:rgb("--primary"),backgroundColor:rgb("--primary",.12),fill:true,tension:.4,pointRadius:0,data:[…]}]},options:{maintainAspectRatio:false,plugins:{legend:{display:false}},scales:{x:{grid:{display:false}}}}});
```
Wrap each `<canvas>` in a fixed-height div, e.g. `<div class="h-64"><canvas id="c1"></canvas></div>`.
