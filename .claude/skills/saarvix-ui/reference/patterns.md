# Page Patterns (recipes)

Common screen layouts, all using the shell + components. Build the content inside
`<template id="page-content">`.

## Dashboard
- Row of 4 KPI stat cards (`grid sm:grid-cols-2 xl:grid-cols-4 gap-4`), each a `.card card-body` with an icon tile, label, big value, and a trend `.badge`.
- Charts row (`grid lg:grid-cols-3`): a wide chart card (`lg:col-span-2`) + a donut/side card.
- Lower row: a table card (`xl:col-span-2`) + an activity `.timeline` card.

## List + detail drawer (CRM)
- Toolbar: view tabs (Table/Kanban/List), search + filter buttons, primary "New".
- Bulk-action bar (hidden until selection) toggled by `[data-row-check]` count.
- `.table` with checkbox column, avatar+name, status `.badge`, owner avatar, actions.
- Row click → `openOverlay('detail')` drawer with tabs (Profile/Timeline/Notes/Tasks).

## Inbox (3-pane)
- One `.card` with `grid lg:grid-cols-[320px_1fr_300px] h-[calc(100vh-7rem)]`:
  conversation list (search + rows) | chat window (header, scrollable messages, composer) | details panel.
- Message bubbles: inbound `bg-card`, outbound `bg-primary text-white`, rounded with one squared corner.

## Kanban / Pipeline
- `grid md:grid-cols-2 xl:grid-cols-4 gap-4`; each column a `.card bg-muted/40` with header (dot + name + count badge) and stacked card-body items.
- For drag-drop: native HTML5 `draggable` + `dragover`/`drop` (see example page `adv-kanban.html`).

## Calling / softphone
- Metrics row, then `grid xl:grid-cols-3`: active-call card (gradient header + controls) + dialer; live transcript card with AI-summary footer; queue + agent-availability lists.

## Settings
- `grid lg:grid-cols-[220px_1fr]`: left vertical sub-nav (use `data-tabs` styled as a list) → right `[data-panel]`s (Profile/Workspace/Notifications/Security/Billing/API). Use `.switch` rows for toggles.

## Auth / full-screen
- `min-h-screen grid lg:grid-cols-2`: gradient brand panel (`from-primary via-primary to-teal`, white text, feature bullets) + centered form. No shell. See `templates/auth.html`.
- **Social login (Google / Microsoft / LinkedIn):** full-width `.btn btn-outline btn-lg` stack with inline brand-SVG logos, under an "or continue with" divider. Snippet + brand SVGs in `cheatsheet.md` → "Social login buttons"; `templates/auth.html` ships with it wired.

## Pricing
- `grid` of 4 plan `.card`s; mark the popular one with a `badge-primary`/ribbon + primary border + `btn-primary` CTA, others `btn-outline`.

## Empty state
- `.card card-body flex flex-col items-center text-center py-14`: muted icon tile, title, hint, optional primary action.

## Page header (reuse everywhere)
```html
<div class="flex flex-wrap items-end justify-between gap-3">
  <div><h2 class="font-display text-xl font-semibold">Title</h2>
    <p class="text-sm text-muted-foreground mt-0.5">Subtitle</p></div>
  <button class="btn btn-primary"><i data-lucide="plus" class="size-4"></i>Action</button>
</div>
```
