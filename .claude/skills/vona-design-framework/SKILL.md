---
name: Vona HTML Design Framework — Component Library
description: Reusable knowledge of the CoderThemes "Vona" Bootstrap 5.3.8 admin template's design system and HTML components (layout shell, buttons, cards, forms, tables, charts, modals, theming, etc.). Use whenever building, styling, or matching UI markup for a project that uses the Vona theme — to reproduce a component correctly with the right class names and structure.
type: skill
---

# Vona HTML Design Framework — Component Library

## When to use this skill

Use this skill whenever you are **producing or matching HTML/CSS markup** for a project that uses the CoderThemes **Vona** admin theme:

- Building a new page that must match the Vona look (layout shell, page-title, cards).
- Reproducing a specific component (button, badge, table, modal, form control, chart) with the **correct theme class names and structure**.
- Knowing **which JS plugin** backs a widget (date picker, select, datatable, chart) and how it's initialized.
- Theming: switching skins, light/dark, and sidebar/topbar/layout variants.
- This is a **front-end/design skill**. For backend (.NET, GraphQL, Mongo, service bus) use the separate *Saarvix .NET GraphQL Backend* skill — keep concerns separate.

> **Licensing note.** Vona is a **commercial, paid template** by CoderThemes (ThemeForest). This skill documents *structure, class taxonomy, and which plugin backs each widget* — functional facts to help you author matching markup. The proprietary CSS/SCSS/JS and full page source must come from **your licensed copy**. Verify exact class names against your package before relying on them; this skill notes where to confirm.

## Tech facts (locked)

| Concern | Choice |
|---|---|
| CSS framework | **Bootstrap 5.3.8** (vanilla JS, no jQuery for BS itself) |
| Icons | **Lucide** (app chrome) + **Tabler** + **flags**; demoed via `<i data-lucide="…">` hydrated by `lucide.createIcons()` |
| Color mode | Bootstrap native `data-bs-theme="light|dark"` on `<html>` |
| Skins | 14 prebuilt (Shadcn=default, Corporate, Spotify, SaaS, Nature, Vintage, Leafline, Ghibli, Slack, Material, Flat, Pastel Pop, Caffeine, Redshift) via `data-skin` |
| Layout variants | Default / Sidebar-Dark / Topbar-Dark / Monochrome via `<html>` `data-*` |
| Charts | **Chart.js** |
| Tables | static Bootstrap tables + **DataTables** for interactive |
| Form plugins | **Choices.js** (select), **Flatpickr** (date), **Timepickr** (time), **Typeahead** (autocomplete), **TouchSpin** (number spinner) |
| Scrollbars | **SimpleBar** (sidebar / scroll regions) |
| Maps | Vector maps + **Leaflet** |
| Editor | **Quill** (rich text) |

## Demo page map (real, from the static build)

App shell lives under `…/layouts/`. Pages observed:

- **Apps:** `ton-ai` (AI chat), `calendar`, `directory`
- **UI:** `ui-core` (buttons, badges, alerts, cards, avatars, accordions, typography), `ui-interactive` (modals, offcanvas, dropdowns, tabs, tooltips, popovers, collapse, toasts), `ui-visual-feedback` (progress, spinners, carousels, placeholders), `ui-menu-links`, `ui-utilities`
- **Forms:** `form-elements`, `form-plugins`, `form-validation`, `form-wizard`, `form-fileuploads`, `form-quill-editor`
- **Tables:** `tables-static`, `tables-datatables-*` (basic, export, select, ajax, javascript, rendering, columns, child-rows, checkbox-select)
- **Charts:** `charts`
- **Icons:** `icons-tabler`, `icons-lucide`, `icons-flags`
- **Maps:** `maps-vector`, `maps-leaflet`
- **Pages:** `pages-pricing`, `pages-empty`, `pages-timeline`, `pages-terms-conditions`, `pages-invoice`, `404`
- **Auth:** `auth-sign-in`, `auth-sign-up`, `auth-reset-pass`, `auth-new-pass`, `auth-two-factor`, `auth-lock-screen`

## Index of component files

| # | File | Covers |
|---|---|---|
| 00 | [Quick reference (cheat-sheet)](00-quick-reference.md) | One-page class lookup across every component — start here, then drill into a numbered file |
| 01 | [Design system overview](01-design-system-overview.md) | Bootstrap 5.3.8 base, CSS variables, color/spacing/typography tokens, Lucide icons, do/don't |
| 02 | [Layout shell & DOM taxonomy](02-layout-shell-and-dom-taxonomy.md) | `.wrapper`/sidenav/topbar/`.page-content`/footer structure, sidebar menu markup, `<html>` `data-*` config |
| 03 | [Buttons & badges](03-buttons-and-badges.md) | btn variants (soft/ghost/outline/rounded), sizes, icon buttons, groups; badge variants (lighten/outline/square/circle/label) |
| 04 | [Cards, avatars & list groups](04-cards-avatars-listgroups.md) | card anatomy, bg/border/gradient cards, stretched-link, avatar sizes/groups, list groups |
| 05 | [Visual feedback](05-visual-feedback.md) | alerts, toasts (9 placements), progress bars, spinners, placeholders, carousels |
| 06 | [Interactive components](06-interactive-components.md) | modals, offcanvas, dropdowns, tabs, tooltips, popovers, collapse/accordion |
| 07 | [Forms — native controls](07-forms-native-controls.md) | form-control/label/select/check/switch/range, input groups, floating labels, validation, sizes |
| 08 | [Forms — plugin widgets](08-forms-plugins.md) | Choices.js, Flatpickr, Timepickr, Typeahead, TouchSpin, Quill, file uploads — data-attribute init |
| 09 | [Tables](09-tables.md) | static table variants + DataTables setups (export, select, ajax, child-rows) |
| 10 | [Charts (Chart.js)](10-charts.md) | chart types, canvas containers, init pattern, theme-aware colors |
| 11 | [Typography & utilities](11-typography-and-utilities.md) | type scale, text-bg, opacity, borders/radius, shadows, sizing, object-fit, position |
| 12 | [Icons](12-icons.md) | Lucide / Tabler / flags usage and re-init after dynamic DOM |
| 13 | [Theming & customizer](13-theming-and-customizer.md) | light/dark, 14 skins, sidebar/topbar/monochrome variants, customizer offcanvas, persistence |
| 14 | [Page templates](14-page-templates.md) | auth, pricing, invoice, timeline, empty/404, and the AI-chat/calendar/directory apps |

## Authoring rules (non-negotiable)

1. **Match the template's class names exactly.** Theme JS (`app.js`) and CSS bind to selectors like `.wrapper`, `.side-nav`, `.btn-soft-*`. Verify against your licensed files; never invent class names.
2. **Utility-first.** Compose with Bootstrap utilities + theme classes; add bespoke CSS only as a last resort.
3. **Theme via attributes, not stylesheets.** Light/dark/skin/layout are `<html>` `data-*` toggles — never swap whole stylesheets or write `prefers-color-scheme` overrides. See 13.
4. **Reference colors via CSS variables** (`var(--bs-primary)`, `--bs-body-bg`) so components follow the active skin. Never hardcode brand hex. See 01.
5. **Load plugin JS/CSS only on pages that use it** (charts, datatables, choices), not globally. See 08–10.
6. **Re-init plugins after dynamic DOM** (Lucide icons, tooltips, choices) when injecting markup via AJAX/partials. See 12.
7. **One layout shell**; pages render only inner content. The shell (sidebar/topbar/footer/customizer) is shared. See 02.

## Portability placeholders

- `{AppName}` → product name (logo alt, footer, `<title>`)
- `assets/` (template paths) → wherever you host assets (e.g. `wwwroot/`)
