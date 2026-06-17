# Authly Public Marketing + Documentation Website

> On approval, this plan file is copied to `docs/website/00-website-plan.md` (the user asked for it under `docs\website`). Plan mode only permits editing the plan file, so the copy happens as execution step 0.

## Context

Authly (Saarvix IDaaS) is a finished, **open-source, self-hosted** multi-tenant Identity-as-a-Service platform (ASP.NET Core 10 MVC + Razor, OpenIddict OAuth2/OIDC, PostgreSQL, Redis, Hangfire). It currently has only a placeholder landing (`Views/Home/Index.cshtml`) and a stub `/docs`. The user wants a **complete public-facing website built into the product itself** (Razor + the existing compiled SAARVIX Tailwind design system — **not** React) that:

- Explains the product thoroughly so any visitor "gets everything from here, nothing left out."
- Documents how to **integrate** (OAuth/OIDC + REST API), what the **admin panel** does, what the **account/end-user portal** does.
- Documents **self-hosting via Docker** end-to-end (install Docker, pull/run, env vars, first-admin, observability).
- Positions it as **free & open source**, **"Developed by Abhishek Raheja"**.
- Leaves **labeled screenshot placeholders** (user uploads real product screenshots later) and uses **inline SVG / gradients / lucide icons** for decoration — fully offline, Docker-safe.

The reference Lovable/React export at `C:\Users\rahej\Downloads\SAARVIX Auth_ Secure & Simple` defines the *visual direction* (dark hero with grid bg + glow, mono eyebrow labels, code-sample tabs, feature-card grid, architecture diagram, security strip, FAQ accordion). We recreate that look in Razor, but **reorient SaaS content → open-source/self-host**.

### Confirmed decisions
- **Pricing → "Open Source / Self-Host"** page (no $ tiers; GitHub + Docker CTAs, "no MAU limits, no auth tax").
- **Images → local placeholders + SVG/icons** (`/wwwroot/img/screenshots/`).
- **Docs → multi-page hub** with sidebar nav, also surfacing existing `docs/` markdown.

## Key existing facts to reuse (do NOT rebuild)
- Routing: no global auth fallback — pages are public by default. Default route `{controller=Home}/{action=Index}`. `Program.cs:205`.
- Layout: `src/Authly.Web/Views/Shared/_Layout.cshtml` already loads `~/css/saarvix.css` (compiled Tailwind), Inter font, lucide UMD, `~/js/saarvix-app.js`. `_ViewStart` sets it globally.
- Public controllers already exist: `HomeController` (`/`, `/privacy`), `DocsController` (`[AllowAnonymous]`, `/docs`), `SignupController` (`/signup`).
- SAARVIX component classes already compiled: `.btn`/`.btn-primary`/`.btn-outline`/`.btn-ghost`/`.btn-sm`, `.card`, `.badge`, `.input`, `.tabs`, `.modal`, semantic tokens (`bg-primary`, `text-muted-foreground`, `border-border`, etc.). Tailwind is **compiled** (`tailwind.config.js` + `Styles/saarvix.input.css` → `wwwroot/css/saarvix.css`).
- **Critical build note:** any *new* utility class not already in `saarvix.css` requires `npm run build:css` (run from `src/Authly.Web`). The Dockerfile already runs `npm ci && npm run build:css` at image build, so production is covered — but locally we must rebuild CSS after authoring views. **Mitigation:** prefer classes already used in existing views; the config `content` globs include `./Views/**/*.cshtml` and `./Areas/**/*.cshtml`, so a single `npm run build:css` after all views are written picks up everything.

## Approach

### Step 0 — Place the plan
Create `docs/website/00-website-plan.md` containing this plan (so it lives under `docs\website` as requested).

### Step 1 — Public marketing layout
Add a dedicated public layout `Views/Shared/_PublicLayout.cshtml` (cloned from `_Layout` but with the full marketing nav + rich footer), so marketing pages get the proper nav/footer without disturbing the app `_Layout`.
- **Nav:** Authly logo · Features · Docs · Self-Host · GitHub (external) · "Admin sign in" (TenantAdmin/Account/Login) · "Get started" (`/signup`). Theme toggle (dark/light) reuses existing `authly-theme` localStorage logic.
- **Footer:** product links, docs links, "Open source · Developed by **Abhishek Raheja**", GitHub link, license.
- Marketing views opt in via `@{ Layout = "_PublicLayout"; }`.

### Step 2 — Reusable Razor partials (recreate reference components)
Under `Views/Shared/Marketing/`:
- `_CodeTabs.cshtml` — tabbed code block (uses existing `.tabs` from `saarvix-app.js`) for JS / cURL / Python / C# samples.
- `_FeatureCard.cshtml` — icon + title + desc + mono hint (model-driven).
- `_ArchitectureDiagram.cshtml` — inline SVG: "Operator/Admin identity" vs "End-user identity", OAuth/OIDC core in the middle.
- `_Screenshot.cshtml` — labeled placeholder block (aspect-ratio box, dashed border, caption + intended filename like `screenshots/admin-applications.png`) the user later swaps for a real image in `/wwwroot/img/screenshots/`.
- `_DocsSidebar.cshtml` — left nav for the docs hub (active-section aware).

### Step 3 — Landing page (`Views/Home/Index.cshtml`, rewrite)
Recreate reference hero → reorient to open-source:
1. **Hero** — grid-bg + glow, badge "Open source · Self-hostable · Passkeys included", H1 "Authentication infrastructure you can self-host.", subcopy, CTAs: "Get started" (`/signup`) + "Read the docs" (`/docs`) + "Star on GitHub". Code-tab sample (OIDC discovery + token exchange in JS/cURL/C#).
2. **Feature grid** — 6 `_FeatureCard`s mapped to REAL features: OAuth2/OIDC + Social, Passkeys/WebAuthn, MFA (TOTP/Email OTP/backup codes), RBAC + ABAC access policies, Multi-tenant isolation, Webhooks & pipeline hooks.
3. **Architecture** — `_ArchitectureDiagram` + "Two identity layers" copy (operator vs end-user).
4. **Admin panel showcase** — `_Screenshot` placeholder (dashboard) + bullets of admin capabilities.
5. **Developer section** — code tabs (verify token / webhook signature) + bullet list (OIDC discovery, JWKS rotation, refresh-token rotation w/ reuse detection, Management REST API, ABAC evaluate).
6. **Security/compliance strip** — AES-256-GCM at rest, Argon2id hashing, HIBP breached-password, audit logs, GDPR/DPDP data export & erasure.
7. **Open-source CTA** — "Free, forever. Self-host in minutes." → Docker one-liner + GitHub + Developed by Abhishek Raheja.

### Step 4 — Features page (`/features`)
New action `HomeController.Features()` → `Views/Home/Features.cshtml`. Three deep sections, each with `_Screenshot` placeholders + capability tables sourced from the real inventory:
- **Admin Console** (`/tenantadmin/*`) — use the **real route slugs** verified in `Tenant-Onboarding-Guide.md` §4: Applications (`/applications`), Users + impersonation (`/users`), Roles (`/roles`), API Keys (`/apikeys`), MFA Policy (`/mfapolicy`), Social Providers (`/socialproviders`), Messaging BYOK (`/messaging`), Branding (`/branding`), Webhooks (`/webhooks`), Pipeline Hooks (`/pipelinehooks`), Claim Configs (`/claimconfigs`), Security (`/security`), Access Policies / ABAC (`/accesspolicies`), Sandbox (`/sandbox`), plus Members/Operator roles, Observability, Monitoring. Tenant-admin login is `/tenantadmin/account/login`; onboarding wizard `/tenantadmin/onboarding`.
- **End-user Portal** (`/portal/*`): Profile, Security (TOTP/Email OTP/backup codes), Passkeys, Sessions, Devices, Recovery, Contact change, Activity/login history, Privacy (data export + account erasure).
- **Developer/API**: OIDC endpoints, Management REST API, webhooks/events.

### Step 5 — Self-Host / Open Source page (`/self-host`)
New action `HomeController.SelfHost()` → `Views/Home/SelfHost.cshtml`. The "pricing replacement":
- "Free & open source — no MAU caps, no auth tax." GitHub star CTA, license note, Developed by Abhishek Raheja.
- **Install Docker** (tabbed: Windows / macOS / Linux) — links + the few real commands.
- **Run Authly** — `docker compose up --build -d` (dev) and the self-host flow with `docker-compose.self-host.yml`.
- **Required env vars table** — `ENCRYPTION_KEY` (with the .NET one-liner to generate a 32-byte base64 key), `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`, `DATABASE_URL`, `REDIS_URL` + optional retention/observability vars. All quoted from `docker-compose.self-host.yml` / `appsettings.json`.
- **First run** — open `http://localhost:8080`, sign in as super-admin (forced password change), create workspace, health checks (`/hangfire`, psql, redis ping).
- **Optional observability overlay** — `docker compose -f docker-compose.yml -f docker-compose.observability.yml up` (Grafana :3000, Prometheus :9090, Loki :3100, Tempo, OTel collector :4317/4318).
- **Prerequisites** — only Docker Desktop / Docker Engine; Postgres 16 + Redis 7 ship in compose.

### Step 6 — Docs hub (multi-page) — extend `DocsController`
Keep `[AllowAnonymous] [Route("docs")]`. Add actions + `Views/Docs/*.cshtml`, all using `_PublicLayout` + `_DocsSidebar`:
- `/docs` (Index) — overview + section cards + quick links to the in-repo `docs/` guides.
- `/docs/quickstart` — Integrate an app: create workspace → register application (Web/SPA/Native/Machine) → redirect URIs/scopes → discovery doc → Auth Code + PKCE → token exchange → userinfo. Code tabs (JS / C# / cURL).
- `/docs/oauth` — OIDC endpoints reference table: `/.well-known/openid-configuration`, `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/introspect`, `/connect/revoke`, `/connect/logout`, JWKS; grants (Auth Code+PKCE, Refresh w/ rotation, Client Credentials); token lifetimes.
- `/docs/api` — Management REST API (`/api/v1/*`): users, roles, permissions, applications, auditlogs, access/evaluate; API-key auth + scopes.
- `/docs/webhooks` — events, HMAC signature verification (code sample), retries/replay, pipeline hooks.
- `/docs/admin` — admin console guide (links to Features admin section, deeper how-to).
- `/docs/portal` — end-user account guide.
- `/docs/self-host` — links to / mirrors the Self-Host page deep content.

### Step 7 — Controllers wiring
- `HomeController`: add `Features()`, `SelfHost()` (keep `Index`, `Privacy`). All public by default; add `[AllowAnonymous]` for clarity.
- `DocsController`: add the doc sub-actions with `[HttpGet("quickstart")]` etc.
- Update nav links across `_PublicLayout`. Set the **GitHub URL** as a single shared constant/ViewData (placeholder `https://github.com/abhiraheja/authly` — confirm during execution).

### Step 8 — Assets
- Create `wwwroot/img/screenshots/` with a short `README.md` listing expected filenames + recommended dimensions so the user knows what to drop in.
- Any extra marketing-only CSS goes into `Styles/saarvix.input.css` (e.g. `.grid-bg`, `.text-gradient`, glow) so it compiles into `saarvix.css`. Then run `npm run build:css`.

### Step 9 — Build CSS
From `src/Authly.Web`: `npm run build:css` (regenerates `wwwroot/css/saarvix.css` picking up all new view classes + marketing utilities).

## Critical files
- New layout: `src/Authly.Web/Views/Shared/_PublicLayout.cshtml`
- New partials: `src/Authly.Web/Views/Shared/Marketing/{_CodeTabs,_FeatureCard,_ArchitectureDiagram,_Screenshot,_DocsSidebar}.cshtml`
- Rewrite: `src/Authly.Web/Views/Home/Index.cshtml`
- New views: `src/Authly.Web/Views/Home/{Features,SelfHost}.cshtml`
- Docs views: `src/Authly.Web/Views/Docs/{Index,Quickstart,OAuth,Api,Webhooks,Admin,Portal,SelfHost}.cshtml`
- Controllers: `src/Authly.Web/Controllers/HomeController.cs`, `src/Authly.Web/Controllers/DocsController.cs`
- Styles: `src/Authly.Web/Styles/saarvix.input.css` (marketing utilities) → rebuild `wwwroot/css/saarvix.css`
- Assets: `src/Authly.Web/wwwroot/img/screenshots/` (+ `README.md`)
- Plan copy: `docs/website/00-website-plan.md`

## Content sourcing (keep it real, not fictional)
All feature/endpoint/env-var content is drawn from the verified inventory: `Areas/TenantAdmin/Controllers`, `Areas/Portal/Controllers`, `Controllers/Connect`, `Controllers/Api`, `docs/Tenant-Onboarding-Guide.md`, `docs/developer/Quickstart.md`, `Dockerfile`, `docker-compose*.yml`, `appsettings*.json`. No invented SaaS pricing, SLAs, SOC2, or fake customer logos (drop the reference's "Trusted by Northwind/Acme" strip or replace with a neutral "Built with" tech strip: .NET 10, OpenIddict, PostgreSQL, Redis).

**Two authoritative docs to mirror closely** (`docs/developer/Quickstart.md` + `docs/Tenant-Onboarding-Guide.md`) give exact, real content — reuse their wording, route table, and worked example rather than paraphrasing:
- The **"four surfaces"** model (Signup `/signup`, Tenant Admin `/tenantadmin/*`, End-user auth+Portal `/account/*` + `/portal/*`, OIDC `/connect/*` + `/.well-known/*`) — good framing for the Architecture + Docs overview.
- OIDC specifics to state precisely: PKCE mandatory for **all** clients; access token **1h**, refresh **14d** with rotation + reuse-detection family revocation; client types **Web (confidential)** vs **SPA/Native (public)**; automatic CORS + post-logout-redirect allow for any registered redirect-URI origin; RP-initiated logout via `/connect/logout` with `id_token_hint`. The Quickstart's troubleshooting table → a `/docs/oauth` "Common errors" section.
- Worked "Acme zero-to-login" example (Onboarding Guide §8) → a "Walkthrough" panel on `/docs/quickstart`.
- Tenant resolution: prod by custom domain, dev via `?tenant=<slug>` / `X-Tenant-Slug` header → mention on `/docs/quickstart` + `/docs/self-host`.

**SuperAdmin is excluded from open-source/self-host builds** (Phase 6 removed it; gated by `SUPERADMIN_ENABLED`, 404s when off). **Do NOT advertise a super-admin surface** on the public site — the public-facing identity surfaces are only Signup, Tenant Admin, End-user Portal, and the OIDC endpoints.

### Pure self-host — no cloud phone-home (read all 16 docs/*.md to confirm)
The early specs (`Saarvix-Identity-Development-Handoff.md`, `Saarvix-Identity-Development-Plan.md`, `saarvix-identity-master-plan-v2.md`) describe a "sign up on Saarvix cloud → receive a Docker image + sync key → instance phones aggregate metrics home" model. **`docs/multi-tenent/04` + Phase 6 deleted all of that** (`SelfHostedInstance`, `SelfHostSyncService`, `DEPLOYMENT_MODE`/`SYNC_*`, telemetry). The website's Docker story must therefore be: **just pull the image and run — no cloud account, no sync key, no telemetry, runs fully offline.** Do not repeat the old cloud-sync narrative.

### SHIPPED — safe to advertise (verified across all docs + the org-centric master plan, all 7 phases DONE)
- **Auth:** email + password, magic link (passwordless), social login (Google / Microsoft / GitHub / Facebook presets + any custom OAuth2/OIDC), account linking.
- **MFA:** TOTP (own RFC 6238 + QR), Email OTP, **WhatsApp OTP**, Passkeys/WebAuthn (FIDO2), backup codes; per-tenant/per-role policy.
- **OAuth2/OIDC:** Authorization Code + PKCE (mandatory), Client Credentials, Refresh w/ rotation + reuse-detection family revocation; discovery, JWKS, userinfo, introspection, revocation, end-session.
- **Authorization:** RBAC + fine-grained `resource.action` permissions in tokens, **ABAC** (`AccessPolicy` + `POST /api/v1/access/evaluate`), conditional/risk-based access (new-device/unverified-email → block or step-up MFA).
- **Org-centric multi-tenancy:** global **Accounts** (operators) vs tenant-scoped **end-users**; **Organizations** group **Projects (= tenants = environments)**; operator RBAC (`org_owner`/`org_admin`/`project_admin`/`viewer`); two-level org→project selector; self-serve new project; **employee invite** flow; Postgres RLS isolation; custom domain per project.
- **User mgmt:** CRUD, suspend/reactivate, force password reset, **bulk import (Auth0/Firebase/generic JSON)**, **impersonation** (audited), **device management** (trust/rename/forget), session list+revoke.
- **End-user portal:** profile, password change, MFA mgmt, passkeys, sessions, devices, recovery contacts, secure email/phone change, login history, **GDPR/DPDP data export + account erasure**, consent records.
- **Messaging (BYOK):** Email (SMTP / ZeptoMail), **WhatsApp (MSG91 / Gupshup)**; template engine w/ `{{vars}}`, built-in templates, multi-locale, preview, test send, delivery log, channel fallback; all via Hangfire.
- **Webhooks & hooks:** 47-event catalogue, HMAC-SHA256 signing + replay protection, exponential-backoff retry, delivery log, test-in-dashboard; pipeline hooks (pre/post registration, pre/post login, pre-token, send-OTP/email); custom claims (static / metadata / webhook).
- **Security:** Argon2id, AES-256-GCM, Redis rate limiting (429+Retry-After), lockout w/ backoff, CAPTCHA (hCaptcha/Turnstile), HIBP breached-password (k-anonymity), suspicious-login detection, block/allow lists (email/domain/IP/country), security headers (CSP/HSTS/…).
- **Branding & DX:** white-label hosted login (logo/colors/fonts/layout/dark mode), custom domain, branded portal, onboarding wizard, sandbox, `/docs`.
- **Management API `/api/v1`:** users, roles, permissions, applications (secret rotate), audit-logs, access/evaluate; API-key (`X-API-Key`) or client-credentials Bearer; permission-gated, tenant-scoped, paginated.
- **Ops:** Docker self-host, opt-in OpenTelemetry observability (OTLP / Azure Monitor) + local Grafana/Loki/Tempo/Prometheus overlay, monitoring/health page, retention purge jobs.

### ROADMAP — do NOT advertise (specced but not in code)
SAML 2.0, SCIM 2.0, enterprise IdP federation (Azure AD/Okta/Workspace), On-Behalf-Of / Token Exchange (RFC 8693), `private_key_jwt` + mTLS client auth, DPoP, workload identity federation, voice-call OTP, SMS OTP, anonymous/guest auth, sub-organizations, SDKs (Next.js/Node/.NET), CLI, embeddable login widget / full HTML-CSS override, multi-region/HA, managed cloud + pricing tiers. (A small, honestly-labeled "Roadmap" section is OK if desired, but the main feature claims must stay within SHIPPED.)

## Verification
1. `cd src/Authly.Web && npm run build:css` — confirm no Tailwind errors and `wwwroot/css/saarvix.css` regenerates.
2. `dotnet build` (or `docker compose up --build`) — confirm the Web project compiles with new actions/views.
3. Run locally (`dotnet run` in `src/Authly.Web` or `docker compose up`) and visit each route: `/`, `/features`, `/self-host`, `/docs`, `/docs/quickstart`, `/docs/oauth`, `/docs/api`, `/docs/webhooks`, `/docs/admin`, `/docs/portal`. Verify: pages load anonymously (no login redirect), nav/footer render, dark/light toggle works, code-tab partials switch tabs, screenshot placeholders show captions, links resolve.
4. Confirm "Developed by Abhishek Raheja" + open-source/GitHub appears in footer and CTA.
5. Spot-check responsiveness at mobile width.

## Open items to confirm during execution
- Exact public **GitHub repository URL** (used in nav/footer/CTAs).
- **License** name to display (README says "TBD") — show "Open source" generically until confirmed.
