# 01 — Design System Overview

Vona is a **Bootstrap 5.3.8** template. Its visual language is "minimal admin": generous whitespace, soft card shadows, rounded corners, muted neutral surfaces, a single accent color per skin, and Lucide line icons. Build with Bootstrap utilities + CSS custom properties first; only add bespoke CSS when a utility genuinely doesn't exist.

## Foundation

- **Bootstrap 5.3.8** provides the grid, components, and the `data-bs-theme` light/dark mechanism (Bootstrap's native color-mode feature). Vona layers its own SCSS theme on top.
- **No jQuery dependency for Bootstrap itself** (BS5 is vanilla JS). Some vendor plugins may still need their own runtime — load only what a page uses.
- **CSS custom properties** drive theming. Skins and light/dark are switched by changing CSS variables / `data-*` attributes on `<html>`, not by swapping whole stylesheets per skin.

## Color & theme tokens

Vona/Bootstrap expose colors as CSS variables. Prefer these over hardcoded hex:

- Bootstrap theme colors: `--bs-primary`, `--bs-secondary`, `--bs-success`, `--bs-danger`, `--bs-warning`, `--bs-info`, `--bs-light`, `--bs-dark` (each with `-rgb` and subtle/border/text variants in BS 5.3).
- Surface/text: `--bs-body-bg`, `--bs-body-color`, `--bs-border-color`, `--bs-secondary-bg`, `--bs-tertiary-bg`.
- The **accent (primary) color changes per skin** — never hardcode the brand color in a view; reference `var(--bs-primary)` (or Vona's own custom property if the package defines one, e.g. a `--vz-*`/`--ct-*` prefixed variable — confirm the prefix in your licensed `app.css`).

**Light/dark** is controlled by `data-bs-theme="light|dark"` on `<html>`. Components automatically adapt because they read the BS 5.3 color-mode variables. Don't write `@media (prefers-color-scheme)` overrides — toggle the attribute instead (see file 06).

## Typography

- A single sans-serif UI font (confirm the exact family in the package's font CSS) loaded from `wwwroot` fonts or a CDN.
- Type scale follows Bootstrap (`.fs-1`…`.fs-6`, `.display-*`, `.lead`, `.small`, `.text-muted`).
- Headings in page-title rows are typically `h4`/`h5` weight-semibold; use `.fw-semibold`/`.fw-medium` utilities rather than custom rules.

## Spacing, radius, shadows

- Use Bootstrap spacing utilities (`m-*`, `p-*`, `gap-*`, `g-*`) on the 0.25rem scale.
- Cards use a consistent border-radius and soft shadow — use `.card` (Vona restyles it) rather than custom boxes.
- Section rhythm: page content sits in `.container-fluid` with consistent vertical gaps between cards (`row g-3`/`g-4`).

## Icons — Lucide

Vona's primary icon set is **Lucide**. Typical usage patterns (verify which the package uses):

- Inline SVG sprite, **or**
- `<i data-lucide="home"></i>` placeholder elements hydrated by `lucide.createIcons()`.

Rules:
- After injecting markup dynamically (AJAX/partial swap, modal open with new icons), call the Lucide initializer again so new placeholders render.
- Size/color icons with utility classes / `currentColor` so they follow theme + skin automatically.
- Other icon fonts may be demoed (e.g. for icon-gallery pages) but standardize on Lucide for app chrome.

## Practical do/don't

- ✅ `class="btn btn-primary"`, `class="text-muted"`, `style="color:var(--bs-primary)"`
- ✅ Lay out with the BS grid (`row`/`col-*`) and flex/grid utilities.
- ❌ Hardcoded brand hex, custom shadow/radius values, per-skin stylesheet swaps, `prefers-color-scheme` overrides.

See [02 — Layout shell](02-layout-shell-and-dom-taxonomy.md) for the structural classes and [13 — Theming](13-theming-and-customizer.md) for switching skins/modes.
