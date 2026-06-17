# UI Revamp — Authly → SAARVIX design system

> Faisla: Authly ke UI ko **SAARVIX design system** pe le jaana. Stack wahi rahega
> (ASP.NET MVC + Razor, dekho [07-framework-choice.md](07-framework-choice.md)) — sirf
> screens behtar honge. Doc-only — abhi koi code nahi.

---

## 1. Kya hai kya

| | Current (Authly) | Target (SAARVIX) |
|---|---|---|
| CSS framework | **Bootstrap 5.3.8** + custom `authly-theme.css` | **Tailwind** (compiled) + SAARVIX `theme.css` token/component layer |
| Font / icons | Inter + Lucide ✅ | Inter + Lucide ✅ (already match!) |
| Brand | — | Purple `#6A5ACD`, ≤8px corners, light+dark via tokens |
| Shell | Razor `_AdminLayout` etc. (server-rendered) | sidebar+topbar; SAARVIX ships `shell.js` (client) — hum Razor mein rakhenge |
| JS | Bootstrap bundle + jQuery + validation | SAARVIX `app.js` (data-attr API: toasts/dropdowns/tabs/overlays) + jquery-validation |
| Views | ~95 Razor views, 4 areas | same views, re-skinned |

**SAARVIX source:** `D:\AVDesignWorks\CaludeUI design` (HTML design system + `saarvix-ui`
Claude skill). Files: `theme.css` (tokens + component library), `tw-config.js` (token→Tailwind
mapping, ≤8px radius remap), `app.js` (interactions), `shell.js` (chrome), `*.html` (~100 example
pages), skill `reference/` (cheatsheet, components, patterns, charts, layout-shell, pages-index).

Already match (good news): Authly **already uses Inter + Lucide**, aur shell structure
(sidebar `.side-nav` + `IsActive()`) conceptually SAARVIX jaisa hai — toh migration mostly
**class system Bootstrap → Tailwind/SAARVIX** ka hai.

**Confirmed decisions:** (1) **compiled Tailwind build** (Play CDN nahi), (2) **foundation-first,
phir incremental re-skin**.

---

## 2. Enable the SAARVIX skill in this repo (pehla kadam)
- `…\CaludeUI design\.claude\skills\saarvix-ui` ko copy karo →
  `d:\Personal\Authly\.claude\skills\saarvix-ui`. Isse skill is repo mein trigger karega
  ("build a page / component / dashboard" pe).
- Purana **`vona-design-framework` skill retire** karo (ab obsolete — Authly Vona se SAARVIX pe ja raha).
- Design HTML (`cheatsheet.md`, `patterns.md`, `pages-index.md`, aur `*.html` pages) ko
  **component markup ka source-of-truth** maano — naya invent mat karo.

---

## 3. Foundation (ek baar banao — sab pe apply hota hai)

### 3.1 Compiled Tailwind build
- `package.json` + `tailwind.config.js` — SAARVIX `tw-config.js` ke token mappings + ≤8px
  radius remap port karo. `content` mein Razor views (`Views/**/*.cshtml`, `Areas/**/*.cshtml`) scan.
- Input CSS: SAARVIX `theme.css` ke **tokens (`:root` light+dark)** + **`@layer components`**
  (.btn/.card/.input/.table/.badge/.modal/.drawer/.timeline…) import karo.
- Output → `wwwroot/css/saarvix.css` (committed). MSBuild target `npm run build:css` (pre-build)
  + **Dockerfile build stage** mein Tailwind compile (taaki self-host image mein offline ship ho).
- Production-grade: chhota CSS, no FOUC, internet nahi chahiye (Play CDN ki problem solve).

### 3.2 Assets laao
- `theme.css` (tokens + components) → Tailwind input (3.1).
- `app.js` (toasts, dropdowns, tabs, segmented, accordion, table select-all, `toast()`,
  `openOverlay`/`closeOverlay`) → `wwwroot/js/saarvix-app.js`.
- **`shell.js` drop** — chrome Razor layouts mein server-render hoga (MVC best practice; SEO/auth safe).
- Inter + Lucide already present — reuse.

### 3.3 Shell ko Razor layouts mein re-implement karo
SAARVIX sidebar+topbar markup ko in layouts mein translate karo (server-side nav + existing
`IsActive()` pattern rakho):
- `Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml` — console shell (sidebar groups + topbar).
- `Areas/Portal/Views/Shared/_PortalLayout.cshtml` — end-user portal shell.
- `Views/Shared/_AuthLayout.cshtml` — login/consent = **full-screen SAARVIX auth template**
  (no shell), `templates/auth.html` jaisa.
- Topbar mein **doc-06 ka two-level org→project selector** + theme toggle + profile menu.

### 3.4 Reusable Razor component partials
Common SAARVIX components ke liye partials / tag-helpers banao (button, card, input, table,
badge, modal, drawer, tabs) taaki views compose karein, har jagah classes repeat na ho.

### 3.5 Bootstrap hatao
- **Bootstrap CSS/JS remove** karo (CDN link + `wwwroot/lib/bootstrap`).
- **jquery-validation-unobtrusive rakho** (markup-driven client validation) — bas uske
  message classes ko SAARVIX styling do. Bootstrap JS interactions → SAARVIX `app.js`.
- jQuery sirf validation ke liye reh sakta hai (chhota); baaki interactions `app.js` se.

---

## 4. Incremental re-skin (foundation ke baad, area-by-area)

Order (har area independently ship + visually verify):
1. **TenantAdmin console** — sabse zyada screens + yahin naye features aayenge. (Applications,
   Users, Roles, Branding, Messaging, Webhooks, ApiKeys, Onboarding, Sandbox, Security,
   AccessPolicies, etc.)
2. **Connect / Account** — login, register, consent, MFA, forgot/reset, magic-link, recover.
3. **Portal** — end-user self-service (profile, sessions, security, devices, passkeys, privacy).

**Skip: SuperAdmin** — woh delete ho raha hai ([04-remove-superadmin-self-host.md](04-remove-superadmin-self-host.md)).

**Naye feature screens seedha SAARVIX mein banao** (kabhi Bootstrap mein nahi): project/org
selector, members (invite/role-assign), operator roles, org settings, observability config
(docs [02](02-decision-and-plan.md)/[06](06-organizations-and-operator-rbac.md)/[05](05-pluggable-observability.md)).

---

## 5. Sequencing — feature-work ke saath
- **Foundation pehle** (section 3) — taaki doc-06 ke feature screens shuru se SAARVIX mein banein.
- Phir feature phases (Org+Account, selector, members, observability) apni UI directly SAARVIX
  mein laayein, aur purane screens area-by-area re-skin hote rahein.
- Yani UI ek **alag track** hai jo feature phases ke saath chalta hai — foundation un screens
  se pehle jo nayi banengi.

---

## 6. Verification
- `npm run build:css` clean; `dotnet build` + app run.
- Har re-skinned area ko SAARVIX reference HTML (`pages-index.md`, relevant `*.html`) se
  visually compare karo — spacing, components, density match.
- **Light + dark dono** sahi (sirf tokens use karo, fixed colors nahi).
- Responsive: desktop-first, ~1024 tak + tablet theek; sidebar mobile pe collapse.
- Lucide icons render (`lucide.createIcons()` dynamic HTML ke baad bhi).
- Bootstrap-specific markup/JS kahin reh to nahi gaya (grep `btn-` / `col-` / `data-bs-`).

---

## 7. TL;DR
- Stack same (MVC+Razor); sirf **Bootstrap → SAARVIX Tailwind** UI.
- **Skill copy** karo repo mein; Vona skill retire.
- **Foundation once:** compiled Tailwind (`wwwroot/css/saarvix.css`) + SAARVIX tokens/components
  + `app.js` + **Razor shell layouts** (shell.js nahi) + component partials; Bootstrap hatao,
  validation rakho.
- **Incremental re-skin:** TenantAdmin → Account/Connect → Portal; SuperAdmin skip; naye feature
  screens seedha SAARVIX.
- Light+dark + responsive verify.
```
