# Layout & Shell

`assets/shell.js` renders the chrome (sidebar + top header) and mounts page content. Each page calls
`mountShell({ active, title })` once; it injects the layout around the `<template id="page-content">`.

## mountShell(opts)
- `active` (string): the `key` of the current NAV item → drives sidebar highlight + which group opens.
- `title` (string): text shown in the top header.
- Internally: injects chrome, runs `lucide.createIcons()`, calls `wireApp()` (so global + per-page interactions bind to injected DOM), wires theme toggle, mobile menu, collapse, and sub-menu toggles.

## NAV configuration
Edit the `NAV` array near the top of `shell.js`. Shape:
```js
const NAV = [
  { group: "Overview", items: [
    { key: "dashboard", label: "Dashboard", icon: "layout-dashboard", href: "index.html" },
  ]},
  { group: "CRM", items: [
    { key: "leads", label: "Leads", icon: "user-plus", href: "leads.html", badge: "128" },
    { key: "social", label: "Social", icon: "share-2", href: "social.html", children: [
      { key: "social-x", label: "X", href: "social-x.html" },   // nested = collapsible sub-menu
    ]},
  ]},
];
```
- `group`: section heading (uppercase label in the rail).
- `key`: unique; must equal the page's `mountShell` `active`.
- `icon`: a Lucide icon name.
- `href`: page file.
- `badge` (optional): small count pill on the row.
- `children` (optional): array of `{key,label,href}` → renders a collapsible sub-menu (one level).

## Header features (built-in, all interactive)
- Mobile hamburger (`#menuBtn`) opens the sidebar; backdrop closes it.
- Global search input (lg+), `⌘K` hint.
- Workspace switcher (dropdown), Quick-add (dropdown), Notifications (dropdown panel).
- Theme toggle (`#themeBtn`) — persists to `localStorage["saarvix-theme"]`.
- AI Assistant button → `toast(...)` by default (repoint as needed).
- Sidebar footer: profile dropdown with Profile / Settings / Toggle theme / Sign out (→ login.html).

## Collapse & responsive
- Desktop collapse button toggles a 72px rail (icons only) via `.sb-label` hiding.
- `< md`: sidebar is off-canvas (slides in over a backdrop).
- Main column auto-offsets (`md:pl-64` / `md:pl-[72px]`).

## Theme persistence
Each page's `<head>` has a tiny pre-paint script reading `localStorage["saarvix-theme"]` to add `.dark`
before first paint (prevents flash). The toggle updates that key.

## Full-screen pages (no shell)
Login / register / forgot / 404 / lock screens do NOT use the shell: omit `<template>`, `shell.js`,
`app.js`, and `mountShell`. Build markup directly in `<body>` and end with `lucide.createIcons()`.
Use `templates/auth.html` as the starting point.
