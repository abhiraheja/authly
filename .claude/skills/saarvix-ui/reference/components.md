# Components (detailed)

Defined in `assets/theme.css`. For ready snippets see `cheatsheet.md`; this file documents variants
and rules. Combine with Tailwind utilities for layout.

## Button `.btn`
- Variants: `btn-primary`, `btn-secondary`, `btn-outline`, `btn-ghost`, `btn-danger`, `btn-success`.
- Sizes: default, `btn-sm`, `btn-lg`. Icon-only: `btn-icon` (square; pair with `btn-sm` for compact).
- Put icons inline: `<i data-lucide="x" class="size-4"></i>` before/after the label.
- Loading: replace icon with `<span class="size-4 rounded-full border-2 border-white/40 border-t-white animate-spin"></span>`.
- Joined group: wrap buttons, add `rounded-r-none` / `rounded-l-none -ml-px`.
- Disabled: `disabled` attribute (auto-dims, blocks pointer).

## Card `.card`
- Parts: `card-header` (flex, space-between), `card-title`, `card-body`, `card-footer`.
- Shorthand: `<div class="card card-body">`. Cover image: a gradient `<div class="h-28 bg-gradient-to-br from-primary to-teal">` + `overflow-hidden` on the card.

## Badge `.badge`
- Variants: `badge-neutral/primary/success/warning/danger/info`.
- Status dot: add `<span class="dot"></span>` (inherits text color).
- Count pill: `class="badge badge-primary px-1.5 min-w-5 justify-center"`.

## Inputs
- `.input`, `.select`, `.textarea`, label `.label`. Compact: `.input-sm`.
- States: focus ring is automatic. Error: add `!border-coral` + helper `<p class="text-xs text-coral mt-1">`. Valid: `!border-success`.
- Checkbox/radio: native + `class="accent-[rgb(var(--primary))] size-4"`.
- Switch: `<span class="switch"><input type="checkbox"><span class="track"></span></span>`.
- Icon input: relative wrapper + absolutely-positioned `<i data-lucide="search">` + `pl-9` on input.

## Avatar `.avatar`
- Size via `size-8/9/10/12`. Background: gradient `bg-gradient-to-br from-primary to-teal` + initials, or solid `bg-primary`.
- Status dot: absolute `-bottom-0.5 -right-0.5 size-3 rounded-full bg-success ring-2 ring-card`.
- Stack: parent `flex -space-x-2`, each avatar `ring-2 ring-card`.

## Table `.table`
- Wrap in `<div class="card overflow-hidden"><div class="overflow-x-auto scroll-thin">…`.
- Header auto-styles (uppercase, muted). Rows hover-highlight. Right-align numerics with `text-right`.
- Select-all: `data-check-all` on header checkbox, `data-row-check` on row checkboxes (wired).

## Tabs `.tabs`/`.tab` + Segmented `.segmented`
- Tabs need `[data-tabs-root]` ancestor, `[data-tabs]` on the bar, `.tab[data-target="x"]`, panels `[data-panel="x"]` (inactive get `hidden`). One tab `.active`.
- Segmented: `.segmented[data-segmented]` with `<button>`s, one `.active`.

## Accordion (wired)
`[data-accordion] > [data-acc-item] > ([data-acc-trigger button] + [data-acc-panel])`, chevron `[data-acc-chev]` (rotates). Open by default: remove `hidden` from panel, add `rotate-180` to chevron.

## Menu / Dropdown `.menu`
- Items `.menu-item`, separators `.menu-sep`. Trigger pattern uses `data-dropdown="id"` + panel `id` with `data-dropdown-menu class="menu hidden absolute …"` (wired open/close/Esc/outside-click).

## Tooltip `.tt` + `.tt-bubble`
CSS-only: `<span class="tt">TRIGGER<span class="tt-bubble">text</span></span>` (shows on hover, top by default).

## Overlays: Modal `.modal` / Drawer `.drawer` + `.overlay`
- Pair `#id` (modal/drawer) with `#id-overlay` (overlay). Open `openOverlay('id')`, close `closeOverlay('id')`.
- Modal width: `!w-[400px]` (sm), default, `!w-[720px]` (lg). Drawer slides from right by default.

## Feedback: Progress / Skeleton / Spinner / Timeline
- Progress: `<div class="progress"><span style="width:62%"></span></div>` (recolor span: `bg-teal` etc).
- Skeleton: `<div class="skeleton h-3 w-2/3"></div>` (pulsing).
- Spinner: `<span class="size-5 rounded-full border-2 border-muted border-t-primary animate-spin"></span>`.
- Timeline: `.timeline` wrapper, `.timeline-item` per entry (dot + connector auto-drawn).

## Misc
- `.kbd` keyboard hint, `.divide-rows` (adds top borders between children — list rows), `.scroll-thin` (slim scrollbar), `.glass` (blurred translucent bg).
