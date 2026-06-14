# 02 — Layout Shell & DOM Taxonomy

Every Vona page shares one shell. Understanding its containers is what lets you map it cleanly to a single `_Layout.cshtml`. Class names below reflect CoderThemes' conventional taxonomy — **confirm exact names against your licensed `app.css`/HTML** before relying on them, then keep your Razor markup byte-identical to the template so its CSS/JS hooks keep working.

## The shell, top to bottom

```html
<html lang="en" data-bs-theme="light"
      data-sidenav-color="..." data-sidenav-size="..."
      data-topbar-color="..." data-layout="...">     ← layout config (see file 06)
<head> … core css, vendor css, page css … </head>
<body>
  <div class="wrapper">                ← outermost app frame

    <!-- LEFT MENU -->
    <div class="sidenav-menu">          ← sidebar container (name may be .sidenav / .leftside-menu / .app-menu — verify)
      <a class="logo">…logo + logo-sm…</a>
      <button class="button-sm-hover">…collapse pin…</button>
      <div data-simplebar>             ← scrollable region
        <ul class="side-nav">          ← the menu list
          <li class="side-nav-title">Workspace Tools</li>     ← menu GROUP label
          <li class="side-nav-item">
            <a class="side-nav-link" href="…">
              <span class="menu-icon"><i data-lucide="…"></i></span>
              <span class="menu-text">Dashboard</span>
              <span class="badge …">  (optional)
            </a>
          </li>
          <li class="side-nav-item">    ← item WITH children (collapsible)
            <a class="side-nav-link" data-bs-toggle="collapse" href="#sub">…<span class="menu-arrow"></span></a>
            <div class="collapse" id="sub">
              <ul class="sub-menu"> <li class="side-nav-item"><a class="side-nav-link">…</a></li> </ul>
            </div>
          </li>
        </ul>
      </div>
    </div>

    <!-- TOPBAR -->
    <header class="app-topbar">         ← (name may be .topbar / .navbar-custom — verify)
      <div class="page-container">
        <div class="topbar-item"> brand toggle / menu toggle </div>
        <div class="topbar-item"> search </div>
        <div class="topbar-item ms-auto"> theme toggle · language · apps · notifications · user dropdown </div>
      </div>
    </header>

    <!-- MAIN CONTENT -->
    <div class="page-content">
      <div class="container-fluid">
        <!-- page title row -->
        <div class="page-title-head … d-flex align-items-center">
          <h4 class="…">Page Title</h4>
          <ol class="breadcrumb …"> … </ol>
        </div>
        <!-- YOUR VIEW CONTENT (rows of cards) -->
      </div>

      <!-- FOOTER -->
      <footer class="footer">
        <div class="container-fluid">© {AppName} … · links/storage</div>
      </footer>
    </div>

  </div><!-- /.wrapper -->

  <!-- CUSTOMIZER (theme settings offcanvas) -->
  <div class="offcanvas offcanvas-end" id="theme-settings-offcanvas"> … </div>

  … core js, vendor js, page js …
</body>
</html>
```

## Layout config lives in `<html>` `data-*` attributes

CoderThemes templates store layout/theme state as data-attributes on the root element, read by the config JS and the customizer. Typical set (verify exact names/values in `config.js`):

| Attribute | Purpose | Example values |
|---|---|---|
| `data-bs-theme` | light/dark color mode | `light`, `dark` |
| `data-sidenav-color` / `data-menu-color` | sidebar skin | `light`, `dark`, `brand`, `gradient` |
| `data-sidenav-size` | sidebar width/state | `default`, `compact`, `condensed`, `offcanvas`, `full` |
| `data-topbar-color` | topbar skin | `light`, `dark` |
| `data-layout` / `data-layout-mode` | container/layout style | `fluid`, `boxed`, `detached` |
| `data-skin` | active prebuilt skin | `shadcn`, `corporate`, … |

These are what the customizer toggles and what you set from the user's saved preference (see file 13). The collapse/pin button in the sidebar header just flips `data-sidenav-size`.

## Authoring rules for the shell

- Render the shell **once** (sidebar, topbar, footer, customizer); individual pages emit only the inner content inside `.page-content > .container-fluid`.
- The **`<html>` tag with its `data-*`** carries the theme/layout config; set it from the saved preference, not hardcoded per page. See file 13.
- The **menu `<ul class="side-nav">`** should be generated from a single source of truth (a config/data structure), not copied per page — so the active highlight and groups stay consistent.
- Keep the wrapper/sidenav/topbar class names **exactly** as the package ships them; the template's `app.js` binds the toggles, SimpleBar scroll, active-state, and the customizer to those selectors. Inventing or renaming a class silently breaks behavior.

Next: [03 — Buttons & badges](03-buttons-and-badges.md).
