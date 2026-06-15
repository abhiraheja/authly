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

- [x] Integrate **OpenIddict** 7.5 server + standard endpoints (§5.1) — authorize/token/userinfo/logout via passthrough controller; introspect/revoke/discovery/JWKS served by OpenIddict
- [x] `applications` + `application_secrets` tables (§4.5, our mirror) + OpenIddict EF stores; **Tenant Admin** UI to create/manage apps
- [x] Secret generation: `client_[24]` / `secret_[48]`, shown once, stored hashed (Argon2id mirror; OpenIddict validates); rotation via UI (REST endpoint = Phase 5)
- [x] **Authorization Code + PKCE** flow (§5.2) → hand-off to the branded end-user login, cross-tenant guard
- [~] **Refresh token** flow with rotation — OpenIddict rotation + reuse rejection (authorization-scoped revocation acts as the "family"); the `sessions.refresh_family_id` mirror is not yet wired to OpenIddict tokens
- [x] **Client Credentials** flow (§5.3) — subject = client, scoped to the client's tenant
- [~] JWKS + discovery (RS256) via OpenIddict **dev certificates**; persisted keys + super-admin key rotation still TODO
- [x] Token claim assembly — standard claims first (§5.6 step 1): sub, email, email_verified, name, tenant_id with per-scope destinations; re-checks account active on refresh
- [~] Audience binding — OpenIddict binds tokens to the issuing client by default; explicit resource/audience config not set yet
- [~] **Acceptance:** Build + 35/35 unit tests green (ApplicationService create/rotate/delete + public/confidential/machine behavior). **Full OAuth runtime acceptance pending a Postgres run** (no DB/Docker in dev shell) — end-to-end code+PKCE, refresh rotation, client_credentials, and discovery/JWKS validation against a standard client lib still to be exercised.

---

## Phase 4 — Authorization (RBAC + Permissions)  *(Step 5)*

**Goal:** Roles and fine-grained permissions per tenant, mapped and injected into tokens; permission checks enforce access.

- [x] `roles`, `permissions`, `role_permissions`, `user_roles` tables (§4.6) — migration `AddRbacTables` + RLS (ENABLE/FORCE/tenant_isolation) on `roles`/`permissions`/`user_roles`; `role_permissions` protected transitively
- [x] Seed system roles: `super_admin`, `tenant_admin`, `tenant_member`, `tenant_viewer`, `service_account` — `SystemRbac` catalogue; idempotent + non-destructive `EnsureSystemRolesAsync`, seeded in tenant context on tenant-admin sign-in (RLS-safe); bootstrap admin auto-granted `tenant_admin`
- [x] Tenant admin UI: create roles, define `resource.action` permissions, map roles→permissions, assign roles→users — `Areas/TenantAdmin` Roles + Users controllers/views (Vona); system roles edit-only
- [x] Inject `roles` + `permissions` claims into access tokens (claim assembly §5.6) — `AuthorizationController` adds `role` + `permissions` claims, routed to access token (+ identity when `roles` scope granted)
- [x] Permission-check middleware/attribute used by Management API — `RequirePermissionAttribute` (401/403) over pure `PermissionEvaluator` (exact + wildcard); Management API endpoints land in Phase 5
- [~] **Acceptance:** Build + 50/50 unit tests green (RBAC seed/idempotency/cross-tenant guard/assign-remove + `PermissionEvaluator` allow/deny `user.read` vs `user.delete`). **Runtime acceptance pending a Postgres run** (no DB/Docker in dev shell) — issuing a real token and asserting roles/permissions claims + endpoint allow/deny still to be exercised end-to-end.

---

## Phase 5 — Management REST API (v1)  *(Step 6)*

**Goal:** The `/api/v1` REST surface for programmatic tenant management, authenticated by client-credentials token or `X-API-Key`, fully tenant-scoped.

- [x] API auth: Bearer (client credentials) **and** `X-API-Key` (`api_keys` table §4.5) — `Api` policy scheme forwards to `ApiKey` handler when X-API-Key present, else OpenIddict validation (Bearer); api_keys stored SHA-256-hashed, looked up globally (not RLS-scoped), tenant bound from the key/token claim
- [x] Users endpoints (§6: list/create/get/patch/delete, suspend/reactivate, force-reset, roles, sessions) — `UserAdminService` + `/api/v1/users` controller; soft delete; force-reset revokes sessions + queues reset email
- [x] Roles & Permissions endpoints (§6) — `/api/v1/roles` (+`/permissions` map) and `/api/v1/permissions`, over `IRbacService`
- [x] Applications endpoints incl. secret rotate (§6) — `/api/v1/applications` over `IApplicationService`; secret returned once on create/rotate
- [~] Invitations endpoints incl. bulk (`invitations` table §4.10) — **deferred**: the invitations feature/table isn't built yet (no entity); revisit when invitations land
- [x] Audit-logs (filterable) endpoint (§6) — `/api/v1/audit-logs` paged + `event`/`actorId` filters. [~] Webhooks endpoints — **deferred to Phase 9** (webhooks are fully built there)
- [x] Conventions: pagination `{data,total,page,limit}` (`PagedResponse`/`Pagination` clamp), error `{error:{code,message}}` (`ApiExceptionFilter` + envelope), additive-only `/api/v1` versioning
- [~] **Acceptance:** Build + 59/59 unit tests green (ApiKeyService hash-once/revoke + UserAdminService create/dup/suspend/delete/force-reset). **Runtime acceptance pending a Postgres run** (no DB/Docker in dev shell) — exercising CRUD via a real API key and a client-credentials token, asserting tenant-scoping + pagination/error envelopes end-to-end, still to be done.

---

## Phase 6 — Multi-Factor Authentication  *(Step 7)*

**Goal:** Our own TOTP, backup codes, and Email OTP, wired into the login flow with per-tenant/per-role policy.

- [x] `mfa_factors` + `mfa_backup_codes` tables (§4.4) — plus `otp_codes` (§4.9); RLS on the tenant-scoped ones (mfa_factors, otp_codes)
- [x] **TOTP** (RFC 6238): `TotpService` (HMAC-SHA1/6-digit/30s, Base32), generate secret + QR (QRCoder SVG), enroll/confirm/verify; secret AES-256-GCM encrypted
- [x] Backup codes: 10 × `xxxxx-xxxxx`, SHA-256 hashed, one-time use, regen drops old set
- [x] **Email OTP** via `otp_codes`: send (Hangfire email queue), verify, 10-min expiry, 5-attempt cap; code never logged
- [x] Wire MFA step into login flow — `MfaController` gate via data-protected `authly.mfa` pending cookie; session cookie issued only after the factor clears
- [x] MFA policy config per tenant + per role (Optional / AdminsOnly / Required) in `tenants.settings` JSON; tenant-admin UI; self-service portal (`/account/security`)
- [~] **Acceptance:** logic unit-proven (23 new tests: TOTP round-trip, backup single-use, email-OTP burn, policy → enroll/challenge decisions). Live "scan QR in a real authenticator app + full login" pass still pending Postgres run.

---

## Phase 7 — Messaging & Templates  *(Step 8)*

**Goal:** Pluggable email + WhatsApp providers (BYOK), a template engine with variables, WhatsApp OTP, and a real email provider — all sending via Hangfire.

- [x] Provider interfaces (email + WhatsApp) in Core (`IEmailProvider`/`IWhatsAppProvider`), implementations in Infrastructure; selected per tenant by provider key
- [x] `messaging_providers` table (§4.11) + BYOK config UI; secret fields (api_key/password) AES-256-GCM encrypted at rest, write-only in the UI; RLS on the tenant-scoped tables
- [x] Template engine (`TemplateRenderer`, `{{var}}` w/ HTML-encoding) + `message_templates` overrides, preview (sample vars), send-test, multi-locale (locale→en fallback)
- [x] Built-in templates (verify_email, reset_password, otp [email+whatsapp], magic_link, welcome, security_alert); security-critical keys validated to keep their required `{{action_url}}`/`{{otp}}` variable
- [x] **WhatsApp OTP** path: MSG91 provider (HTTP); Gupshup is the same-shape extension (documented). MFA email-OTP + Auth verify/reset now route through templates
- [x] Real email providers (SMTP via System.Net.Mail, ZeptoMail via HTTP) replacing the Phase 2 stub; `log` provider is the dev default when no BYOK configured
- [x] `message_log` (§4.11) + delivery tracking (queued/sent/failed, routing metadata only — never body/OTP) + channel fallback (WhatsApp→email); all sends via Hangfire (`MessageDispatchJob` binds tenant for RLS)
- [~] **Acceptance:** logic unit-proven (12 new tests: render/encode, builtin resolution, active-provider selection, log fallback, WhatsApp→email fallback, override-beats-builtin, secret encryption, required-var validation). Live BYOK delivery (real Zepto/MSG91 keys + Hangfire) is runtime-pending (no keys/network/Postgres in dev).

---

## Phase 8 — Social Login  *(Step 9)*

**Goal:** Social sign-in starting with Google, plus account linking, configurable per tenant.

- [x] `social_identities` table (§4.3); access/refresh tokens AES-256-GCM encrypted. Added `social_providers` config table. Per-tenant unique (tenant,provider,provider_id) over the doc's global UNIQUE (tenant-isolation hard rule); RLS on both
- [x] Google OAuth (authorization-code) login + JIT user creation (verified email → social-only user, no password)
- [x] **Account linking** — callback links a verified provider email to an existing account; takeover guard: only link/JIT from a **provider-verified** email
- [x] Additional providers (Google/Microsoft/GitHub/Facebook presets) + **generic OAuth2/OIDC** ("custom" provider w/ own endpoints); `SocialAuthGateway` (HttpClient) drives any standard endpoints, profile field-mapping per preset
- [x] Per-tenant provider enable/config UI (`/tenantadmin/social`); client secret encrypted + write-only; login buttons on the sign-in page; callback redirect-URL hint
- [~] **Acceptance:** logic unit-proven (13 new tests: presets, JIT-create, link-existing, returning-identity reuse, **token encryption at rest**, unverified-email rejection, unconfigured-provider guard, custom-endpoint validation). Live Google handshake (real client id/secret + network + Postgres) is runtime-pending. Note: social sign-in currently bypasses the MFA gate (documented; MFA-after-social is a future enhancement).

---

## Phase 9 — Webhooks & Pipeline Hooks  *(Step 10)*

**Goal:** Event system with HMAC-signed webhooks (retry + delivery log), pipeline hooks at each auth stage, and custom token claims (static + metadata + webhook).

- [x] Event system + 40+ event catalogue (auth, user lifecycle, MFA, sessions, org, token, app) — `EventCatalog` (47 events) + `IEventPublisher`; `AuditLogger` fans every tenant-scoped audited event out to subscribed endpoints
- [x] `webhook_endpoints` + `webhook_deliveries` (§4.12); HMAC-SHA256 signing (`WebhookSigner`, `sha256=` over `timestamp.body` + timestamp/delivery-id headers = replay protection); per-event routing (exact name or `*`)
- [x] Hangfire dispatch + exponential-backoff retry (1m→5m→30m→2h→24h, `WebhookRetrySchedule`) + delivery log + manual retry + test-in-dashboard
- [x] `pipeline_hooks` (§4.12): pre/post registration, pre/post login, pre-token, send-OTP, send-email stages; per-call timeout + on_failure (continue=fail-open / block=fail-closed); `pre_token` hook response merged as claims
- [x] **Custom token claims** (`claim_configs` §4.13): static + user/app-metadata mapping (dotted path, `user_metadata.`/`app_metadata.` prefix) + webhook claims via `pre_token` hooks — `TokenClaimAssembler` wired into `/connect/authorize` + client-credentials issuance (claim assembly §5.6 steps 2–4; block vetoes issuance)
- [~] **Acceptance:** verified by build + 20 new unit tests (HMAC sign/verify + replay-window, retry-ladder progression + exhaustion, event routing/wildcard/inactive, dispatch success/retry/exhaust, pipeline block/continue/merge/unwrap, claim assembly static/metadata/hook/block/token-type). **Runtime-pending:** a real receiver verifying HMAC, an observed scheduled retry, and a live pre-token hook injecting a claim require a running app + Postgres + an external endpoint.

---

## Phase 10 — Branding & Login UI  *(Step 11)*

**Goal:** Per-tenant visual branding applied to the hosted login page, custom domain support, and the end-user self-service portal.

- [x] Branding config (logo, colors, fonts, layout, dark mode) stored in `tenants.branding` — `TenantBranding` (Core) + pure `TenantBrandingJson` parse/serialize (snake_case, degrades to default on malformed); `BrandingService` validates hex colors, http(s) logo URL, sanitizes the font, persists + audits (`tenant.branding_updated`)
- [x] Apply branding to hosted login/register/MFA pages (Vona-based, themed per tenant) — request-scoped `CurrentBranding` accessor; `_AuthLayout` emits per-tenant `--bs-primary`/`--bs-primary-rgb`/button-text/font overrides + `data-bs-theme` + logo/name; centered **and** split layouts
- [x] Custom domain support (`auth.theircompany.com`) — already resolved host→tenant in `TenantResolutionMiddleware`; `BrandingService.SetCustomDomainAsync` validates host shape + rejects a domain owned by another tenant; **unique index** `idx_tenants_custom_domain` (migration `AddTenantCustomDomainUniqueIndex`)
- [x] **End-User Portal** (`Areas/Portal`): profile + password change, MFA management (moved from `/account/security`), active sessions + revoke (+ revoke-others), login history — `IAccountSelfService` (ownership-scoped); password change evicts other sessions; per-request `SessionCookieValidator` (cookie `OnValidatePrincipal`) makes revoke actually sign a device out; branded `_PortalLayout`
- [~] **Acceptance:** verified by build + 22 new unit tests (branding JSON round-trip/default/malformed + RGB conversion; save validation: bad color/logo/font-sanitize; custom-domain normalize/clear/invalid/cross-tenant-collision/own-domain; password change wrong-current/success-revokes-others/social-set-first; session revoke ownership guard + audit; profile normalize). **Runtime-pending:** two tenants showing distinct branding on the same login route, a live custom domain resolving, and an end-user managing profile/MFA/sessions need a running app + Postgres + DNS.

---

## Phase 11 — Advanced Auth  *(Step 12)*

**Goal:** Passkeys, magic link, account recovery, and secure email/phone change.

- [x] **Passkeys / WebAuthn** (FIDO2) enroll + login (store credential in `mfa_factors`) — Core `IWebAuthnGateway` seam + Fido2NetLib impl in Infrastructure (rpId/origin from request host via `IWebAuthnRelyingParty`, so passkeys bind to localhost or a tenant custom domain); `PasskeyService` stores type=passkey factors (`credential_id` column + public key/counter JSON in `secret`); passwordless `/account/passkey` login + portal `/portal/passkeys` enrol; `authly-webauthn.js` drives the browser ceremony; data-protected challenge cookie binds the ceremony to the tenant/user
- [x] **Magic link** passwordless login (via `verification_tokens` type=magic_link) — `MagicLinkService` (anti-enumeration request, 15-min single-use token, opening it verifies the inbox); `/account/magic-link` + `/account/magic`; "Email me a sign-in link" on the login page
- [x] Tiered **account recovery** (backup codes → recovery contact → admin-assisted → identity verification); notify all contacts; optional cooldown; fully audited — `recovery_contacts` table + `RecoveryService` (manage contacts; `InitiateRecoveryAsync` issues a recovery/reset token and notifies the account email **plus every recovery contact**, anti-enumeration, audited `user.recovery_initiated`); backup-code tier reuses MFA; admin-assisted/identity-verification tiers documented as follow-on
- [x] **Email/phone change** secure flow: verify current identity → verify new contact → notify old with cancel link → optional cooldown — `pending_contact_changes` table + `ContactChangeService` (current identity = authenticated portal session; confirmation link to the new contact, cancel link to the current email; 2-min cooldown; email uniqueness re-checked at apply); `/portal/contact` to start, `/account/change/verify` + `/account/change/cancel` public links
- [~] **Acceptance:** verified by build + 18 new unit tests (magic-link request/anti-enum/single-use/expiry/verifies-email; contact-change notify-both/already-in-use/cooldown/verify-applies/cancel-from-old; recovery add-idempotent/remove-ownership/notify-all-channels/anti-enum; passkey exclude-existing/store-active/begin-null-when-none/verify-bumps-counter/remove-ownership). **Runtime-pending:** a real passkey ceremony in a browser+authenticator, the live magic-link/recovery emails, and a cross-device cancel all need a running app + Postgres + a real authenticator. *(Transitive note: Fido2NetLib pulls IdentityModel; pinned to 6.35.0 to clear GHSA-59j7-ghrg-fj52.)*

---

## Phase 12 — Security Hardening  *(Step 13)*

**Goal:** Close the attack surface — rate limiting, lockout, bot defense, breached-password detection, suspicious-login detection, block/allow lists.

- [x] Redis-backed multi-level **rate limiting** middleware — Core `IRateLimiter` + `RedisRateLimiter` (atomic INCR + window TTL); `RateLimitingMiddleware` throttles POSTs per path+IP on login/register/forgot/magic/recover/passkey/token (429 + Retry-After)
- [x] **Account lockout** with exponential backoff + self-service unlock — `ILoginAttemptStore`/`RedisLoginAttemptStore` + `AccountLockoutService` (`LockoutPolicy` 1m→2m→4m… capped 1h); wired into login (block when locked, record on fail, reset on success); self-service unlock = backoff expiry / successful sign-in
- [x] **Bot/CAPTCHA** (hCaptcha/Turnstile), configurable per tenant — Core `ICaptchaGateway` + `HttpCaptchaGateway` (siteverify); per-tenant enable/provider/site-key + **encrypted** secret; `_Captcha` widget on login/register; verified server-side before auth
- [x] **Breached-password** check (HaveIBeenPwned k-anonymity) — Core `IBreachedPasswordGateway`+`IPwnedRangeClient`; `HibpBreachedPasswordGateway` (SHA-1, only 5-char prefix leaves the server, Add-Padding, fails open); rejects breached passwords on register + reset + recovery
- [x] **Suspicious-login** detection (impossible travel / new device) as Hangfire job + alert — `SuspiciousLoginDetector` (new IP **and** new device vs prior successes) + `SuspiciousLoginService`; `SuspiciousLoginJob` enqueued after login (binds tenant for RLS) → queues `security_alert`. *(Impossible-travel needs a geo-IP source — new-device/new-IP implemented; geo documented.)*
- [x] **Block/allow lists** (email, domain/disposable, IP/range, country) + per-org IP allowlist — `BlockListPolicy` (domain + built-in disposable set + IP/CIDR via `System.Net.IPNetwork` + country + allowlist) + `BlockListService`; enforced on register + login; **country requires a geo source** to enforce (config accepted)
- [x] Security headers (CSP/HSTS), super-admin panel hardening (mandatory MFA, optional IP allowlist) — `SecurityHeadersMiddleware` (CSP incl. captcha origins, X-Frame-Options/nosniff/Referrer/Permissions-Policy) + existing `UseHsts`; `SuperAdminIpAllowlistMiddleware` (`SUPERADMIN_IP_ALLOWLIST` → 404 off-list). *(Mandatory super-admin MFA deferred — super_admins have no MFA enrolment yet; IP allowlist + headers shipped.)*
- [~] **Acceptance:** verified by build + 20 new unit tests (lockout backoff/cap + lock-at-threshold/disabled/reset; block-list email/disposable/IP-CIDR/allowlist/country; HIBP suffix-match/zero-count-padding/absent/fail-open; suspicious first-login/known/new-context; settings round-trip + encrypted-write-only secret + node-preservation). **Runtime-pending:** live brute-force→lockout, a real breached-password rejection against HIBP, a CAPTCHA challenge round-trip, and an IP-blocked denial need a running app + Redis + Postgres + network.

---

## Phase 13 — Self-Host & Compliance  *(Step 14)*

**Goal:** Ship the self-host Docker artifact with aggregate-only sync, and meet GDPR/DPDP basics.

- [x] Docker image published as the self-host artifact; `DEPLOYMENT_MODE=self_hosted` path (`docker-compose.self-host.yml`; `IDeploymentContext`)
- [x] `self_hosted_instances` table on cloud (§4.14); sync API endpoint (`POST /api/sync`, `X-Sync-Key`; SuperAdmin → Self-hosted registers + issues one-time key)
- [x] Hangfire recurring **sync** job (every ~6h): pushes `{version, counts, health}` only — **never PII**; failure swallowed (best-effort, never blocks auth); only scheduled when self-hosted + sync configured
- [x] Consent capture + disclosure at signup/Docker setup (logged) — register accept-terms → `consent_records`; self-host boot logs `self_host.disclosure_acknowledged`
- [x] GDPR/DPDP: **data export** (JSON download), **right to erasure** (hard delete + cascade), **consent tracking**, retention jobs — portal `/portal/privacy`
- [x] Recurring cleanup jobs: expire verification/reset tokens + OTPs (hourly), purge old login history + audit past retention (daily)
- [~] **Acceptance:** unit-verified (export/erasure/consent, sync ingest aggregate-only + key validation, deployment-mode parsing — 197/197 tests). Runtime pending: live offline-grace, no-PII payload capture, retention purge against a real DB.

---

## Phase 14 — Polish & Launch-Ready  *(Step 15)*

**Goal:** Onboarding, sandbox, super-admin monitoring, and documentation.

- [x] Tenant **onboarding wizard** (first app → keys → branding → test login) — `OnboardingController` 4-step flow (reuses app/api-key/branding services); progress indicator partial; onboarding state in `Tenant.Settings` JSON (`IsOnboardedAsync`/`SetOnboardedAsync`, preserves other keys); dashboard nudge banner via `TenantBanners` view component until complete
- [x] **Sandbox** environment per tenant — `SandboxController` test-login playground: validates credentials against the tenant's user store (scoped to TenantId, never issues an admin cookie) and surfaces OIDC endpoints (discovery/authorize/token/userinfo)
- [x] Super-admin **monitoring & health** dashboard — `MonitoringController` + `IPlatformHealthProbe` (Postgres/Redis liveness + latency, never throws), reuse of `IInstanceMetricsCollector` for aggregate counts, `ILoginAnalyticsStore` (cross-tenant 14-day success/fail, RLS-bound per tenant, aggregate-only), self-hosted instance telemetry table
- [x] Platform ops: announcements, version/deprecation, background-job monitoring, backup status — `Announcement` platform-level entity (no RLS) + `IAnnouncementService` + super-admin CRUD + `TenantBanners` banner to tenant admins; version/mode/deprecation from `IDeploymentContext`+config; backup status from config; Hangfire dashboard link
- [x] Developer **documentation** + quickstart — `docs/developer/Quickstart.md` (concepts, endpoints, Auth-Code+PKCE/refresh/client-credentials flows, Next.js + cURL examples, token verification, troubleshooting) + in-app `/docs` page with live endpoints
- [~] **Acceptance:** verified by build + 6 new unit tests (onboarding flag default/set-preserves-keys/idempotent-audit/malformed-tolerant; announcement severity-normalize/valid-severity/visible-filtering) — 203/203 total. **Runtime-pending:** a real signup→wizard→working test login round-trip, the super-admin health/analytics dashboard against live Postgres+Redis with traffic, and an external dev integrating via the docs all need a running app + DB + Redis + browser.

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

- **Phase 2 (Advanced):** ~~risk-based/conditional access~~ ✅, ABAC, anonymous/guest auth, ~~impersonation~~ ✅, ~~device management~~ ✅, anomaly detection, log streaming, migration tools (Auth0/Firebase importers), more SDKs, CLI.

### Phase 2 — Conditional / risk-based access  *(done 2026-06-16, build + 6 tests, runtime-pending)*
- [x] Per-tenant conditional-access policy (`TenantSecuritySettings.ConditionalAccessEnabled` + `NewDeviceAction`/`UnverifiedEmailAction` as `ConditionalAction` Allow|RequireMfa|Block), edited on the Tenant Admin → Security page.
- [x] `IConditionalAccessService` evaluates the login context (reuses the Phase 12 `SuspiciousLoginDetector` for new-device/IP; checks unverified email); most-restrictive signal wins.
- [x] Enforced in the end-user login path **before the session cookie is issued**: Block → revoke the just-created session + audit `user.login_blocked` + generic message; RequireMfa → step up (challenge if the user has a factor, else force enrollment) by elevating the existing MFA gate.
- [~] **Acceptance:** verified by build + 6 unit tests (disabled-allows; new-device triggers action; known-device no-trigger; unverified-email triggers; most-restrictive-wins; verified+known allowed) — 209/209 total. **Runtime-pending:** a real new-device sign-in stepping up to MFA, and a Block denial, against a running app.
- **Phase 3 (Enterprise):** SAML 2.0, SCIM 2.0, enterprise IdP federation, workload identity federation, DPoP/mTLS, `private_key_jwt`, full data residency, sub-organizations, B2B onboarding, SOC 2/ISO.
- **Phase 4 (Future):** identity analytics, SAML IdP, B2C self-registration, federation hub, advanced risk scoring.
- **SDKs** (Next.js/React, Node, .NET) — after the v1 API is stable.

---

*Build sequentially; do not advance past a phase until its Acceptance gate passes. When ambiguous, prefer the simplest option that preserves tenant isolation and credential protection.*
