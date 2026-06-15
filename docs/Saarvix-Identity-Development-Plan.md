# Saarvix Identity Platform — Development Plan (Phase & Task)

> **Source of truth:** `Saarvix-Identity-Development-Handoff.md` (technical spec) + `saarvix-identity-master-plan-v2.md` (product).
> **Locked architecture:** ASP.NET Core **MVC + Razor + REST** `/api/v1`, OpenIddict, PostgreSQL + EF Core, Redis, Hangfire, modular monolith, Docker. **Vona** Bootstrap for UI. (No GraphQL.)
> **Legend:** `[ ]` not started · `[~]` in progress · `[x]` done. Each phase ends with an **Acceptance** gate — do not start the next phase until it passes.

---

## Roadmap at a Glance

| # | Phase | Build-Order Step | Outcome |
|---|---|---|---|
| 0 | Foundation & Infrastructure | Step 1 | App boots in Docker; DB/Redis/Hangfire live; base entities migrated |
| 1 | Tenancy & Super Admin | Step 2 | Tenants CRUD; super admin login; isolation + RLS |
| 2 | Core Auth (no OAuth) | Step 3 | Register / login / verify / reset; sessions; audit |
| 3 | OAuth / OIDC Engine | Step 4 | OpenIddict flows: Auth Code+PKCE, Refresh+rotation, Client Credentials |
| 4 | Authorization (RBAC) | Step 5 | Roles, permissions, claims in tokens |
| 5 | Management REST API | Step 6 | `/api/v1` for users/roles/apps/etc.; API-key + client-cred auth |
| 6 | Multi-Factor Auth | Step 7 | TOTP, backup codes, Email OTP, wired into login |
| 7 | Messaging & Templates | Step 8 | Email + WhatsApp providers (BYOK), template engine, WhatsApp OTP |
| 8 | Social Login | Step 9 | Google + others; account linking |
| 9 | Webhooks & Pipeline Hooks | Step 10 | Event system, HMAC webhooks, hooks, custom claims |
| 10 | Branding & Login UI | Step 11 | Visual branding, custom domain, end-user portal |
| 11 | Advanced Auth | Step 12 | Passkeys/WebAuthn, magic link, recovery, email/phone change |
| 12 | Security Hardening | Step 13 | Rate limiting, lockout, CAPTCHA, breached-pw, block/allow lists |
| 13 | Self-Host & Compliance | Step 14 | Self-host Docker artifact, sync, GDPR/DPDP export/erasure/retention |
| 14 | Polish & Launch-Ready | Step 15 | Onboarding wizard, sandbox, super-admin monitoring, docs |

Phases 0–14 = master-plan **Phase 1 (Foundation)**. Master-plan Phases 2–4 are tracked in **Future Work** at the end.

---

## Phase 0 — Foundation & Infrastructure  *(Step 1)*

**Goal:** A running ASP.NET Core MVC app in Docker Compose with PostgreSQL, Redis, Hangfire, base entities migrated, and the security primitives (Argon2id, AES) in place.

- [x] Create solution (`Authly.slnx`) and projects: `Saarvix.Identity.Web`, `.Core`, `.Modules`, `.Infrastructure`, `tests/Saarvix.Identity.Tests`
- [x] Enforce dependency direction: Web → Modules → Core ← Infrastructure → Core
- [x] Add `docker-compose.yml` (app + postgres:16 + redis:7) and `Dockerfile`
- [x] Wire EF Core (Npgsql) + `AppDbContext` reading `DATABASE_URL`
- [x] Wire Redis connection (`REDIS_URL`) — `IConnectionMultiplexer` + distributed cache
- [x] Wire Hangfire (Postgres storage) + dashboard at `/hangfire` (dev-open, locked down later)
- [x] Core entities + first EF migration (`InitialCreate`): `tenants`, `users` (per schema §4.1, §4.2)
- [x] Implement custom **Argon2id** `IPasswordHasher` in Infrastructure
- [x] Implement **AES-256-GCM** encryption service (key from `ENCRYPTION_KEY` env)
- [x] env-var config binding; secrets via env (not committed)
- [~] Base `_Layout` renders + rebranded landing — *full Vona theme assets deferred to Phase 1 panels*
- [~] **Acceptance:** unit tests prove Argon2id hash/verify + AES round-trip (**11/11 pass**); build green; migration SQL validated. *Runtime `docker compose up` check pending — requires Docker (not available in dev shell).*

---

## Phase 1 — Tenancy & Super Admin  *(Step 2)*

**Goal:** Tenants exist and are CRUD-able; the platform owner can log into a Super Admin panel; tenant isolation is enforced at query level + Postgres RLS.

- [x] `Tenant` entity + repository + `Tenants` module service (create/suspend/reactivate/soft-delete) with slug generation
- [x] `super_admins` table + entity (§4.15); super admin bootstrap from `SUPERADMIN_EMAIL/PASSWORD` (force change on first login)
- [x] Super Admin auth (isolated cookie scheme, separate from tenant users) + `Areas/SuperAdmin` panel shell (replicated Vona theme)
- [x] Tenant list / create / suspend / reactivate / delete UI in Super Admin panel
- [x] **Tenant isolation middleware** (`TenantResolutionMiddleware`) + `TenantConnectionInterceptor` sets `app.current_tenant` per connection
- [x] **Row Level Security** on `users` (canonical tenant-scoped table) — `ENABLE` + `FORCE` + `tenant_isolation` policy (backstop); app also filters by tenant_id
- [x] Audit-log scaffolding (`IAuditLogger`/`audit_logs`) invoked on every tenant state change
- [~] **Acceptance:** build green, 19/19 unit tests pass (incl. tenant create/suspend/slug + audit), full migration SQL validated. *Runtime check (login → create Tenant A/B → cross-tenant read denied) pending `docker compose up` — no Docker in dev shell.*
- *Note:* RLS applied to `users` only so far (the tenant-scoped table with data); `applications`, `sessions`, etc. get the same policy as those tables land in later phases.

---

## Phase 2 — Core Auth (no OAuth yet)  *(Step 3)*

**Goal:** Server-rendered registration, email/password login, email verification, and password reset — with sessions, login history, and audit logging. Email goes through a stubbed provider for now.

- [x] User registration (Razor pages, Vona) → creates `users` row (Argon2id hash)
- [x] Email/password login + logout; isolated end-user cookie scheme (`authly.user`)
- [x] `sessions` table (§4.7) + session lifecycle (create on login, revoke on logout / password reset)
- [x] `verification_tokens` + email verification flow (§4.9) — SHA-256 token hash, single-use, 24h expiry
- [x] `password_reset_tokens` + reset flow (§4.9) — single-use, 1h expiry, anti-enumeration request
- [x] Queue all emails via Hangfire (`HangfireEmailQueue` → `EmailDispatchJob`) with a **stub email provider** (`StubEmailSender`, logs payload; link only at Debug)
- [x] `login_history` recording (success/failed/blocked, IP, UA, device) (§4.8)
- [x] `audit_logs` append-only writes on all state changes (user.registered/login/email_verified/password_reset_requested/password_reset)
- [~] **Acceptance:** A user registers, verifies email, logs in, resets password; `login_history` and `audit_logs` rows appear; reset/verify tokens are single-use and expire. *(Build + 29/29 unit tests green, incl. single-use/expiry/anti-enumeration coverage; full runtime acceptance pending Postgres run — no Docker/DB in dev shell.)*

---

## Phase 3 — OAuth / OIDC Engine  *(Step 4)*

**Goal:** OpenIddict issues standards-compliant tokens. Tenants create Applications (client id/secret). Auth Code + PKCE, Refresh (rotation + reuse detection), and Client Credentials all work.

- [ ] Integrate **OpenIddict** server + standard endpoints (§5.1: authorize, token, userinfo, introspect, revoke, logout, discovery, JWKS)
- [ ] `applications` + `application_secrets` tables (§4.5); tenant admin UI to create apps
- [ ] Secret generation: `client_[24]` / `secret_[48]`, shown once, stored hashed; rotation endpoint
- [ ] **Authorization Code + PKCE** flow (§5.2) → branded login page hand-off
- [ ] **Refresh token** flow with rotation + `refresh_family_id` **reuse detection** (§5.4)
- [ ] **Client Credentials** flow (§5.3)
- [ ] JWKS + discovery serving asymmetric keys (RS256/ES256); key rotation via super admin
- [ ] Token claim assembly — standard claims first (§5.6 step 1)
- [ ] Audience binding (token for app A rejected at app B)
- [ ] **Acceptance:** A confidential web app completes Auth Code+PKCE end-to-end; refresh rotates and a reused refresh token kills the family; client_credentials returns an access token; `/.well-known/openid-configuration` + JWKS validate with a standard client lib.

---

## Phase 4 — Authorization (RBAC + Permissions)  *(Step 5)*

**Goal:** Roles and fine-grained permissions per tenant, mapped and injected into tokens; permission checks enforce access.

- [ ] `roles`, `permissions`, `role_permissions`, `user_roles` tables (§4.6)
- [ ] Seed system roles: `super_admin`, `tenant_admin`, `tenant_member`, `tenant_viewer`, `service_account`
- [ ] Tenant admin UI: create roles, define `resource.action` permissions, map roles→permissions, assign roles→users
- [ ] Inject `roles` + `permissions` claims into access tokens (claim assembly §5.6)
- [ ] Permission-check middleware/attribute used by Management API
- [ ] **Acceptance:** A user with `user.read` but not `user.delete` is allowed/denied accordingly; roles+permissions appear in the issued access token.

---

## Phase 5 — Management REST API (v1)  *(Step 6)*

**Goal:** The `/api/v1` REST surface for programmatic tenant management, authenticated by client-credentials token or `X-API-Key`, fully tenant-scoped.

- [ ] API auth: Bearer (client credentials) **and** `X-API-Key` (`api_keys` table §4.5)
- [ ] Users endpoints (§6: list/create/get/patch/delete, suspend/reactivate, force-reset, roles, sessions)
- [ ] Roles & Permissions endpoints (§6)
- [ ] Applications endpoints incl. secret rotate (§6)
- [ ] Invitations endpoints incl. bulk (`invitations` table §4.10)
- [ ] Audit-logs (filterable) + Webhooks endpoints (§6)
- [ ] Conventions: pagination `{data,total,page,limit}`, error `{error:{code,message}}`, additive-only versioning
- [ ] **Acceptance:** Full CRUD on users/roles/apps via API key and via client-credentials token; every response is tenant-scoped; pagination + error envelope conform.

---

## Phase 6 — Multi-Factor Authentication  *(Step 7)*

**Goal:** Our own TOTP, backup codes, and Email OTP, wired into the login flow with per-tenant/per-role policy.

- [ ] `mfa_factors` + `mfa_backup_codes` tables (§4.4)
- [ ] **TOTP** (RFC 6238): generate secret + QR, enroll, verify; secret AES-encrypted
- [ ] Backup codes: generate, hash, one-time use
- [ ] **Email OTP** via `otp_codes` (§4.9)
- [ ] Wire MFA step into login flow (post-password challenge)
- [ ] MFA policy config per tenant + per role (required-all / admins-only / optional)
- [ ] **Acceptance:** User enrolls TOTP (scans QR in a real authenticator app), logs in with a 6-digit code, falls back to a backup code; policy forces MFA for admins.

---

## Phase 7 — Messaging & Templates  *(Step 8)*

**Goal:** Pluggable email + WhatsApp providers (BYOK), a template engine with variables, WhatsApp OTP, and a real email provider — all sending via Hangfire.

- [ ] Provider interfaces (email + WhatsApp) in Infrastructure
- [ ] `messaging_providers` table (§4.11) + BYOK config UI (config encrypted)
- [ ] Template engine + `message_templates` (§4.11) with variables (`{{otp}}`, `{{user_name}}`, …), preview, send-test, multi-locale
- [ ] Built-in templates (verify, magic link, OTP, reset, welcome, alerts) — security-critical ones styleable but not breakable
- [ ] **WhatsApp OTP** via a provider (MSG91/Gupshup first, per decision)
- [ ] Real email provider (Zepto/SMTP) replacing the Phase 2 stub
- [ ] `message_log` (§4.11) + delivery tracking + channel fallback (WhatsApp→email)
- [ ] **Acceptance:** A tenant configures BYOK email + WhatsApp; verification email and WhatsApp OTP deliver via Hangfire; template variables render; failed channel falls back.

---

## Phase 8 — Social Login  *(Step 9)*

**Goal:** Social sign-in starting with Google, plus account linking, configurable per tenant.

- [ ] `social_identities` table (§4.3); tokens AES-encrypted
- [ ] Google OAuth login + JIT user creation
- [ ] **Account linking** (same email via password + Google → one account)
- [ ] Additional providers (Facebook, GitHub, Microsoft, …) + generic OAuth2/OIDC config
- [ ] Per-tenant provider enable/config UI
- [ ] **Acceptance:** A user signs in with Google; linking merges with an existing email account; provider tokens stored encrypted.

---

## Phase 9 — Webhooks & Pipeline Hooks  *(Step 10)*

**Goal:** Event system with HMAC-signed webhooks (retry + delivery log), pipeline hooks at each auth stage, and custom token claims (static + metadata + webhook).

- [ ] Event system + 40+ event catalogue (auth, user lifecycle, MFA, sessions, org, token, app)
- [ ] `webhook_endpoints` + `webhook_deliveries` (§4.12); HMAC-SHA256 signing; per-event routing
- [ ] Hangfire dispatch + exponential-backoff retry (1m→5m→30m→2h→24h) + delivery log + manual retry + test-in-dashboard
- [ ] `pipeline_hooks` (§4.12): pre/post registration, pre/post login, pre-token, send-OTP, send-email, etc.; timeout + on_failure (continue/block)
- [ ] **Custom token claims** (`claim_configs` §4.13): static + user/app-metadata mapping + webhook claims (claim assembly §5.6 steps 2–4)
- [ ] **Acceptance:** A test webhook fires with valid HMAC and replay protection; a failed delivery retries on schedule; a pre-token webhook injects a custom claim respecting timeout/on_failure.

---

## Phase 10 — Branding & Login UI  *(Step 11)*

**Goal:** Per-tenant visual branding applied to the hosted login page, custom domain support, and the end-user self-service portal.

- [ ] Branding config (logo, colors, fonts, layout, dark mode) stored in `tenants.branding`
- [ ] Apply branding to hosted login/register/MFA pages (Vona-based, themed per tenant)
- [ ] Custom domain support (`auth.theircompany.com`)
- [ ] **End-User Portal** (`Areas/Portal`): profile, password change, MFA management, active sessions + revoke, login history
- [ ] **Acceptance:** Two tenants show distinct branding on the same login route; a custom domain resolves to a tenant; an end user manages profile/MFA/sessions in the portal.

---

## Phase 11 — Advanced Auth  *(Step 12)*

**Goal:** Passkeys, magic link, account recovery, and secure email/phone change.

- [ ] **Passkeys / WebAuthn** (FIDO2) enroll + login (store credential in `mfa_factors`)
- [ ] **Magic link** passwordless login (via `verification_tokens` type=magic_link)
- [ ] Tiered **account recovery** (backup codes → recovery contact → admin-assisted → identity verification); notify all contacts; optional cooldown; fully audited
- [ ] **Email/phone change** secure flow: verify current identity → verify new contact → notify old with cancel link → optional cooldown
- [ ] **Acceptance:** A passkey logs a user in; a magic link logs in once and expires; recovery notifies contacts and is audited; email change is cancellable from the old address.

---

## Phase 12 — Security Hardening  *(Step 13)*

**Goal:** Close the attack surface — rate limiting, lockout, bot defense, breached-password detection, suspicious-login detection, block/allow lists.

- [ ] Redis-backed multi-level **rate limiting** middleware
- [ ] **Account lockout** with exponential backoff + self-service unlock
- [ ] **Bot/CAPTCHA** (hCaptcha/Turnstile), configurable per tenant
- [ ] **Breached-password** check (HaveIBeenPwned k-anonymity)
- [ ] **Suspicious-login** detection (impossible travel / new device) as Hangfire job + alert
- [ ] **Block/allow lists** (email, domain/disposable, IP/range, country) + per-org IP allowlist
- [ ] Security headers (CSP/HSTS), super-admin panel hardening (mandatory MFA, optional IP allowlist)
- [ ] **Acceptance:** Brute-force triggers lockout; a breached password is rejected on signup; a blocked country/IP is denied; CAPTCHA gates the login form.

---

## Phase 13 — Self-Host & Compliance  *(Step 14)*

**Goal:** Ship the self-host Docker artifact with aggregate-only sync, and meet GDPR/DPDP basics.

- [ ] Docker image published as the self-host artifact; `DEPLOYMENT_MODE=self_hosted` path
- [ ] `self_hosted_instances` table on cloud (§4.14); sync API endpoint
- [ ] Hangfire recurring **sync** job (every ~6h): pushes `{version, counts, health}` only — **never PII**; failure never blocks auth; 30+ day offline grace
- [ ] Consent capture + disclosure at signup/Docker setup (logged)
- [ ] GDPR/DPDP: **data export**, **right to erasure**, **consent tracking**, retention jobs (login history, audit, failed attempts)
- [ ] Recurring cleanup jobs: expire tokens/OTPs (hourly), purge old login history (daily)
- [ ] **Acceptance:** A self-hosted instance syncs aggregate metrics (verified no PII in payload) and keeps authenticating while offline; a user export + erasure works; retention job purges expired rows.

---

## Phase 14 — Polish & Launch-Ready  *(Step 15)*

**Goal:** Onboarding, sandbox, super-admin monitoring, and documentation.

- [ ] Tenant **onboarding wizard** (first app → keys → branding → test login)
- [ ] **Sandbox** environment per tenant
- [ ] Super-admin **monitoring & health** dashboard (system metrics, login analytics, instance telemetry)
- [ ] Platform ops: announcements, version/deprecation, background-job monitoring, backup status
- [ ] Developer **documentation** + quickstart
- [ ] **Acceptance:** A new company signs up and reaches a working test login via the wizard; super admin sees health + analytics; docs let an external dev integrate.

---

## Cross-Cutting Conventions (apply in every phase)

- Async all the way; every tenant-scoped query filters `tenant_id` (never rely on RLS alone).
- Never log secrets/passwords/tokens/OTPs — redact.
- Every state change writes an audit-log entry.
- All external/slow calls (email, WhatsApp, webhooks, hooks) go through Hangfire — never inline.
- Validate input at the controller boundary; EF Core migrations for all schema changes.
- One module never touches another module's data directly — go through its service interface.

---

## Future Work (post-Foundation — master-plan Phases 2–4)

- **Phase 2 (Advanced):** risk-based/conditional access, ABAC, anonymous/guest auth, impersonation, device management, anomaly detection, log streaming, migration tools (Auth0/Firebase importers), more SDKs, CLI.
- **Phase 3 (Enterprise):** SAML 2.0, SCIM 2.0, enterprise IdP federation, workload identity federation, DPoP/mTLS, `private_key_jwt`, full data residency, sub-organizations, B2B onboarding, SOC 2/ISO.
- **Phase 4 (Future):** identity analytics, SAML IdP, B2C self-registration, federation hub, advanced risk scoring.
- **SDKs** (Next.js/React, Node, .NET) — after the v1 API is stable.

---

*Build sequentially; do not advance past a phase until its Acceptance gate passes. When ambiguous, prefer the simplest option that preserves tenant isolation and credential protection.*
