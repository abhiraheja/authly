# Saarvix Identity Platform — Master Plan (Product Definition)

> **A free, open-source, multi-tenant Identity-as-a-Service (IDaaS) platform.**
> An alternative to Auth0, Microsoft Entra, Google Identity, and Supabase Auth —
> with features they lack: WhatsApp OTP, BYOK everything, beautiful white-label login,
> fully open source, cloud + self-hostable.

> **Status:** Product definition complete — ready for technical architecture phase
> **Version:** 2.0
> **Pricing:** Deferred (free for now)
> **Standards:** OAuth 2.0 + OpenID Connect + SAML 2.0 + WebAuthn

---

## Table of Contents

1. [Vision & Positioning](#1-vision--positioning)
2. [What We Build vs Competitors](#2-what-we-build-vs-competitors)
3. [Standards We Implement](#3-standards-we-implement)
4. [The Three Surfaces (Super Admin / Tenant Admin / End User)](#4-the-three-surfaces)
5. [Authentication](#5-authentication)
6. [Authorization](#6-authorization)
7. [App-to-App Authentication](#7-app-to-app-authentication)
8. [Client & Secret System](#8-client--secret-system)
9. [Custom Token Claims](#9-custom-token-claims)
10. [Token Strategy](#10-token-strategy)
11. [Multi-Factor Authentication](#11-multi-factor-authentication)
12. [Messaging & Template System](#12-messaging--template-system)
13. [Login UI & Branding](#13-login-ui--branding)
14. [Pipeline Hooks & Webhooks](#14-pipeline-hooks--webhooks)
15. [Multi-Tenancy Model](#15-multi-tenancy-model)
16. [Data Storage & Retention](#16-data-storage--retention)
17. [Security Architecture](#17-security-architecture)
18. [Performance Architecture](#18-performance-architecture)
19. [Account Recovery & Identity Changes](#19-account-recovery--identity-changes)
20. [Hosting Model: Cloud + Self-Hosted](#20-hosting-model-cloud--self-hosted)
21. [Compliance (GDPR + DPDP)](#21-compliance-gdpr--dpdp)
22. [Tech Stack](#22-tech-stack)
23. [Phased Delivery Plan](#23-phased-delivery-plan)
24. [Remaining Build-Time Decisions](#24-remaining-build-time-decisions)

---

## 1. Vision & Positioning

Companies today choose Microsoft, Google, Auth0, or Supabase for authentication. Each forces tradeoffs — Auth0 is expensive and cloud-locked, Microsoft B2C has terrible DX, Supabase lacks enterprise features, Keycloak is painful to operate.

**Saarvix Identity is the platform that removes those tradeoffs:** every authentication method, real authorization, full white-label control, developer-first DX, enterprise-ready by default — all free and open source, available as cloud or self-hosted.

### The Five Product Pillars

1. **Universal Auth** — every login method, every enterprise SSO protocol, in one place
2. **Real Authorization** — not just roles, but fine-grained permissions and policies, built in
3. **Your Brand, Your Control** — full white-label, custom domain, self-host option
4. **Developer-First** — SDKs, CLI, webhooks, great docs, local dev in minutes
5. **Enterprise-Ready by Default** — SAML, SCIM, audit logs, compliance — not locked behind pricing

---

## 2. What We Build vs Competitors

| Feature | Auth0 | Microsoft | Google | Supabase | Clerk | **Saarvix** |
|---|---|---|---|---|---|---|
| Open Source | No | No | No | Yes | No | **Yes** |
| Self-hostable | No | No | No | Yes | No | **Yes** |
| Cloud + Self-host same product | No | No | No | Yes | No | **Yes** |
| WhatsApp OTP | No | No | No | No | No | **Yes** |
| BYOK Messaging (your provider keys) | No | No | No | No | No | **Yes** |
| BYOK Email (your domain) | Partial | No | No | No | No | **Yes** |
| Custom Token Claims (visual + webhook) | Paid | Yes | Yes | Hook | No | **Yes** |
| Instagram / any-OAuth social | No | No | No | Partial | No | **Yes** |
| Beautiful login UI | Okay | Poor | Basic | Basic | Best | **Best + white-label** |
| Visual template builder | No | No | No | No | No | **Yes** |
| SAML on all plans | Paid | Yes | Yes | No | Paid | **Yes** |
| Sub-organizations | No | No | No | No | No | **Yes** |
| Fine-grained authz included | Paid | Yes | No | DIY | No | **Yes** |
| Risk-based access | Paid | Best | Partial | No | Partial | **Yes (Ph2)** |
| SCIM | Paid | Yes | Yes | No | Paid | **Yes (Ph3)** |
| Free | No | No | Partial | Partial | No | **Yes** |

---

## 3. Standards We Implement

We do **not** invent a proprietary protocol. We implement industry standards so any standard client library works with us instantly.

```
Saarvix Identity = OAuth 2.0 + OpenID Connect (core)
│
├── OAuth 2.0 (RFC 6749)
│     ├── Authorization Code flow + PKCE (RFC 7636)
│     ├── Client Credentials flow          ← app-to-app
│     └── Refresh Token flow                ← rotation + reuse detection
│
├── OpenID Connect (OIDC)
│     ├── ID Token, UserInfo endpoint
│     ├── Discovery (.well-known/openid-configuration)
│     └── JWKS endpoint (public keys)
│
├── Token Exchange (RFC 8693)             ← powers On-Behalf-Of
│
├── SAML 2.0                               ← enterprise SSO (Phase 3)
│
└── WebAuthn / FIDO2                       ← passkeys (Phase 1)
```

**Device Code flow: skipped** (not needed — our tenants don't build TV/CLI auth).

The OAuth/OIDC engine is built on **OpenIddict** (certified, standards-compliant .NET library) — we build our product features on top of it rather than writing security-critical protocol code from scratch.

---

## 4. The Three Surfaces

The system has three distinct UI surfaces for three distinct audiences.

```
┌─────────────────────────────────────────────────────────┐
│ SUPER ADMIN PANEL — Platform Owner (you)                │
│ Controls entire platform, all tenants, system config    │
└────────────────────────┬────────────────────────────────┘
                         │ governs
                         ▼
┌─────────────────────────────────────────────────────────┐
│ TENANT ADMIN PANEL — Companies who sign up               │
│ Each controls only their own tenant (users, apps,       │
│ branding, auth config) — fully isolated from others     │
└────────────────────────┬────────────────────────────────┘
                         │ serves
                         ▼
┌─────────────────────────────────────────────────────────┐
│ END-USER PORTAL — The tenants' customers                 │
│ Self-service: profile, password, MFA, sessions, history │
│ Branded as the tenant's product                         │
└─────────────────────────────────────────────────────────┘
```

### Super Admin Panel (You)

Tenant management (view/create/suspend/delete all tenants, usage stats, impersonate admin for support, set tenant limits). Platform-wide user oversight (search any user across tenants, global ban list). System config (global defaults, managed email/WhatsApp providers, available social providers, master signing keys, feature flags). Monitoring & health (system dashboard, real-time health, error monitoring, performance metrics, login analytics, self-hosted instance telemetry). Security & abuse (platform-wide audit log, security alerts, abuse detection, incident response kill-switch). Platform operations (announcements, version/deprecation management, background job monitoring, backup status). Super admin team roles: Owner, Platform Operator, Support Agent, Security Analyst.

### Tenant Admin Panel (Companies)

Scoped entirely to their own tenant — can never see anything outside it. Manages: their applications (Client ID/Secret), their users, authentication config, branding & login UI, messaging & templates, authorization (roles/permissions), custom token claims, webhooks & pipeline hooks, security settings, their audit log & analytics. Dashboard team roles within a tenant: Owner, Admin, User Manager, Developer, Support, Read-Only.

### End-User Portal (Tenant's Customers)

Profile management, password change, MFA management, active session list + revoke, login history, API key management, data export, account deletion. Hosted and branded as the tenant's product.

### Hard Isolation Rule

> A tenant admin must NEVER access, see, or affect anything outside their own tenant. Enforced at every layer: separate auth surfaces, every query hard-scoped to tenant_id, PostgreSQL Row Level Security backstop, super admin panel on an isolated surface with stricter protection (IP allowlist, mandatory MFA).

---

## 5. Authentication

### Local
- Email + Password
- Username + Password
- Magic Link (passwordless email)

### Mobile OTP
- **WhatsApp OTP** (managed or BYOK) — our key differentiator
- Voice Call OTP (optional)
- *(SMS removed — WhatsApp is the mobile channel)*

### Social Login
- Google, Facebook, Instagram, GitHub, Microsoft, Apple, Twitter/X, LinkedIn
- Any custom OAuth 2.0 / OIDC provider

### Enterprise SSO
- SAML 2.0 (Phase 3)
- OIDC Federation
- Azure AD / Entra ID, Google Workspace, Okta
- Just-in-Time (JIT) provisioning on first SSO login

### Special Auth Modes
- **Anonymous / Guest auth** (Phase 2) — temporary identity, upgrade to full account later
- **Account linking** (Phase 1) — same person via email + Google = one account

---

## 6. Authorization

**Hybrid model: RBAC + fine-grained permissions + policies.**

- **Roles** — coarse-grained groupings (system + custom per tenant)
- **Permissions** — fine-grained `resource.action` (e.g. `user.read`, `project.delete`)
- **Roles → Permissions** mapping
- **ABAC policies** — attribute-based rules (Phase 2)
- **Conditional / Risk-Based Access** (Phase 2) — "require MFA from new device", "block outside India", "step-up auth for sensitive actions"

System roles: `super_admin`, `tenant_admin`, `tenant_member`, `tenant_viewer`, `service_account`.

---

## 7. App-to-App Authentication

Two distinct kinds of machine-to-machine auth:

```
├── Client Credentials Flow
│     Service authenticates as ITSELF.
│     Use: cron jobs, microservices, backend tasks.
│
├── On-Behalf-Of (OBO) Flow  [Token Exchange, RFC 8693]
│     Service A receives a user's token, exchanges it to call
│     Service B AS THAT USER — user identity preserved through
│     the chain. Use: API gateways, service meshes.
│
├── Client Authentication Methods
│     ├── client_secret_basic   (secret in header)
│     ├── client_secret_post    (secret in body)
│     ├── private_key_jwt        (signed JWT, no secret sent — more secure)
│     └── tls_client_auth        (mTLS certificate — most secure)
│
└── Workload Identity Federation (Phase 3)
      External systems (GitHub Actions, AWS, K8s) get tokens
      without storing any secret.
```

---

## 8. Client & Secret System

Each tenant creates **Applications**, each with its own Client ID + Secret.

### Application Types

| Type | Use | Secret | Allowed Flows |
|---|---|---|---|
| **Web App** (confidential) | Next.js, Laravel, .NET MVC | Yes | Auth Code + PKCE, Refresh, Client Credentials |
| **SPA / Mobile** (public) | React, Vue, Flutter | No (PKCE replaces it) | Auth Code + PKCE only |
| **Machine** (service) | Cron, microservice | Yes | Client Credentials only |

### Secret Rules
- Format: `client_[24 chars]`, `secret_[48 chars]`
- Secret shown **once** at creation, stored hashed, never recoverable
- Zero-downtime rotation (old + new valid during window, then old expires)
- Multiple secrets per app, optional expiry dates
- Never in URLs — header or body only

### Scopes
User-facing: `openid`, `profile`, `email`, `phone`, `offline_access`.
Management API: `users:read/write/delete`, `roles:read/write`, `sessions:read/write`, `audit:read`, `org:read/write`. Least-privilege — each app gets only what it needs.

### API Keys (separate from client secrets)
Direct API access via `X-API-Key` header, scoped to specific permissions, optional expiry. For developer scripts, CI/CD, integrations.

### What Tenants Do With Client + Secret
Integrate hosted login, manage users programmatically (Management API), verify tokens (JWKS), trigger actions (force reset, suspend, revoke sessions, assign roles).

---

## 9. Custom Token Claims

Companies inject their own data into tokens. Three layers:

1. **Static claims** — fixed key-value pairs added to every token (e.g. `region: "IN"`)
2. **User metadata claims** — map stored profile fields into the token (e.g. `user.metadata.plan → plan`)
3. **Webhook claims** — before issuing a token, call the tenant's API; they return data to inject (full dynamic control)

Per-token-type config (different claims on ID token vs Access token). Claim namespacing to avoid collisions. Visual dashboard configuration, no code on our side. Webhook claims have HMAC secret, timeout, and configurable failure behavior.

**`user_metadata` vs `app_metadata` distinction (security-critical):**
- `user_metadata` — user CAN edit (display name, avatar preference)
- `app_metadata` — only backend CAN edit (plan, roles, permissions)
- A user must never be able to edit their own `plan: "enterprise"`.

---

## 10. Token Strategy

| Token | Format | Lifetime | Purpose |
|---|---|---|---|
| Access Token | JWT, RS256/ES256 | 15 min – 1 hr (configurable) | API authorization |
| ID Token | JWT | Login session | User identity info |
| Refresh Token | Opaque, hashed in DB | 30 days sliding | Session continuity |
| API Token | Opaque, hashed | Optional expiry | Automation/integrations |
| Service Token | JWT | Short | Internal service comms |

- **Asymmetric signing** — services verify with public key (JWKS), never hold the signing secret
- **Refresh token rotation** — single-use, each use issues a new one
- **Reuse detection** — reusing a consumed refresh token kills the whole session family
- **Audience binding** — a token for Service A can't be used on Service B
- **Sender-constrained tokens (DPoP/mTLS)** — Phase 3
- **Automatic key rotation** — multiple `kid` values valid via JWKS

---

## 11. Multi-Factor Authentication

**We build our own TOTP system (RFC 6238) — no Microsoft/Google dependency.** We generate the secret + QR code; the user scans with any authenticator app (Google, Microsoft, Authy, 1Password).

- **TOTP** (any authenticator app) — Phase 1
- **Email OTP** — Phase 1
- **WhatsApp OTP** — Phase 1
- **Passkeys / WebAuthn** (Face ID, Touch ID, hardware keys) — Phase 1
- **Backup codes** (one-time, hashed) — Phase 1

MFA policy per-tenant + per-role: require for all / admins only / optional.

---

## 12. Messaging & Template System

### Channels & Modes
- **Email** — Zepto Mail / SMTP / SendGrid / SES / Mailgun. Managed (our key) or BYOK (their key, their domain).
- **WhatsApp** — MSG91 / Gupshup / Meta. Managed or BYOK.
- **Generic HTTP adapter** — connect any provider by defining the API call.

### Built-in Email Templates (customizable)
Welcome, email verification, magic link, OTP, password reset, password changed, suspicious login alert, account locked, invitation, MFA enrolled/removed, session revoked, API key created/expiring.

### Template Features
Visual editor, variable system (`{{otp}}`, `{{user_name}}`, `{{reset_link}}`, `{{app_name}}`, `{{expires_in}}`, `{{ip_address}}`, `{{device}}`, `{{location}}`), multi-language versions, plain-text fallback, preview, send-test. Security-critical templates can be styled but not broken (OTP value always injected; security alerts can't be disabled). Channel fallback (WhatsApp fails → email). Delivery status tracking. Managed-mode sending caps to prevent cost/abuse.

---

## 13. Login UI & Branding

- **Hosted login page** — on our domain or the tenant's custom domain (`auth.theircompany.com`)
- **Visual builder** — logo, background image/video, colors, fonts, layout themes (card / split / full-screen), dark mode
- **Embeddable components** — React, Vue, vanilla JS (login form inside their app)
- **Full HTML/CSS override** for complete control
- Mobile responsive, fast, accessible
- Branded email templates and end-user portal to match

---

## 14. Pipeline Hooks & Webhooks

### Pipeline Hooks (custom code/webhook at each stage)
```
Pre-Registration     → validate / block / enrich before user created
Post-Registration    → sync to their DB
Pre-Login            → block / check before login
Post-Login           → add claims, force MFA
Pre-Token-Issuance   → customize claims (= webhook claims)
Send-OTP             → customize delivery (= WhatsApp integration point)
Send-Email           → customize email delivery
Post-Password-Change → notify
MFA-Challenge        → custom MFA
```
This single architecture powers custom claims, WhatsApp OTP, BYOK email, and external integrations consistently.

### Webhooks (40+ events)
Auth events (login success/failed/blocked, new device/location, MFA), user lifecycle (created/updated/deleted/suspended/verified/invited), MFA, sessions, organization, token, application events.

Payload structure: consistent envelope (`id`, `event`, `timestamp`, `tenant_id`, `version`, `data`, `signature`). HMAC-SHA256 signed. Replay protection. Retry with exponential backoff (1min → 24hr). Per-event endpoint routing. Delivery log + manual retry + test in dashboard.

---

## 15. Multi-Tenancy Model

- **Unlimited organizations**, each fully isolated
- **Email unique per-tenant**, not globally (same person can have separate accounts across different tenants)
- Per-org branding + custom domain, SSO config, messaging provider (BYOK per org), token claim config
- **Sub-organizations** (nested — for enterprises with divisions/branches)
- Invitation system (single + bulk + via API), pending invite management
- Tenant onboarding wizard (first app, get keys, configure branding, test login)
- Tenant offboarding: 30-day grace, full export, then hard delete (configurable retention)

### Isolation Strategy
Shared database, `tenant_id` on every table, enforced at repository layer + PostgreSQL Row Level Security backstop. Tokens carry `tenant_id`; cross-tenant access rejected.

---

## 16. Data Storage & Retention

**Approach: capture comprehensive security + profile data, each with a defined purpose and configurable retention. Sensitive credentials always hashed/encrypted.**

### Captured Data

**Profile / Identity:** avatar/image, email + verification, phone + verification, username, full name, timezone, locale, language, unlimited custom metadata.

**Security & Forensics:** IP address (every event), full login history (time, IP, device, browser, OS, geo-location, success/failure), device fingerprinting + trusted-device tracking, all active sessions, failed login attempts, location / impossible-travel data, suspicious activity flags, full immutable audit trail.

**Credentials (protected — unreadable even to platform owner):**

| Data | Storage | Readable? |
|---|---|---|
| Password | Argon2id hash | No — impossible |
| MFA secret | AES-256-GCM encrypted | Only at verification |
| Backup codes | Hashed | No |
| Refresh tokens | SHA-256 hashed | No |
| Social tokens | AES-256 encrypted | Only when calling provider |

**Authorization:** roles, permissions, org memberships, linked social identities.

### Retention (configurable per tenant, within platform limits)

| Data | Default retention |
|---|---|
| Login history + IPs | 90 days – 1 year |
| Audit logs | 1 year+ (compliance) |
| Active sessions | Until expired/revoked |
| Failed attempts | 30 days |
| Profile + image | Until account deleted |

Rich data captured for forensics, used during its useful window, then auto-expired — full investigative power without becoming an unbounded liability. Supports data export + erasure for GDPR/DPDP.

---

## 17. Security Architecture (Defense in Depth)

> Note: no system is "100% secure." The goal is the highest practical bar — defense in depth, no known weaknesses, matching or exceeding the major players.

1. **Credentials** — Argon2id passwords, client secrets hashed, TOTP/social tokens AES-256 encrypted, refresh tokens hashed, breached-password detection (HaveIBeenPwned k-anonymity).
2. **Tokens** — short-lived access tokens, refresh rotation + reuse detection, asymmetric signing, audience binding, DPoP (Ph3), key rotation.
3. **Login attacks** — multi-level rate limiting, account lockout with backoff, brute-force/credential-stuffing detection, bot detection + CAPTCHA (hCaptcha/Turnstile), suspicious login detection, mandatory PKCE for public clients, state+nonce validation.
4. **Transport/infra** — TLS 1.3, HSTS preload, no secrets in URLs, secrets in vault, WAF + DDoS, security headers (CSP etc.).
5. **Tenant isolation** — query-level tenant scoping + Postgres RLS, tokens carry tenant_id, cross-tenant rejected.
6. **Code security** — input validation, parameterized queries (EF Core), output encoding, no sensitive data in logs, dependency scanning, least privilege.
7. **Detection/response** — immutable tamper-evident audit logs, anomaly detection, real-time alerts, session management.
8. **Process** — security code review, third-party pen testing, responsible disclosure / bug bounty, OWASP ASVS checklist.

**Blocklist:** email, domain (disposable detection), IP/range, country. **Allowlist:** restrict to specific email domains. **IP allowlist** per org.

---

## 18. Performance Architecture

| Operation | Target |
|---|---|
| Token verification (by client service) | < 1ms (local, cached JWKS) |
| Login page load | < 500ms |
| Token issuance | < 100ms |
| Management API call | < 200ms |
| Webhook delivery | async (non-blocking) |

- **Stateless JWT verification** — services verify locally with cached public key, zero round-trip per request (the key scaling decision)
- **Redis** — sessions, rate-limit counters, hot data, tenant config cache
- **PostgreSQL** — proper indexing, connection pooling (PgBouncer), read replicas, compiled queries
- **ASP.NET Core** — top-tier framework performance, async everywhere
- **Stateless services** — horizontal autoscaling behind load balancer
- **CDN** — static login assets at the edge

**Security/speed balance:** deliberately slow where it happens once (password hashing, MFA — per login), instant where it happens constantly (token verification — per request).

---

## 19. Account Recovery & Identity Changes

### Account Recovery (tiered, tenant-configurable)
Backup codes → recovery email/phone → admin-assisted recovery → identity-verification recovery. Critical rules: every recovery notifies all the user's contacts, optional cooldown/delay (24–72hr) defeats instant takeover, fully audit-logged, strict tenants can disable self-service recovery (admin-only).

### Email / Phone Change (secure flow)
Verify current identity (password + MFA) → verify the NEW contact before switching → notify the OLD contact with a cancellation link → optional cooldown → old contact retains cancellation window. Defeats hijack-then-lockout.

### Session & Throttle Controls
Configurable max concurrent sessions + max active refresh tokens per user. Account lockout with self-service unlock via verified email.

---

## 20. Hosting Model: Cloud + Self-Hosted

### Cloud (default)
You host. You're the super admin. Tenants optionally use managed email/WhatsApp sending (your keys) as a convenience.

### Self-Hosted (Docker)
1. User signs up on **your cloud** first
2. Cloud provides the **Docker image** + a unique **sync key**
3. User runs Docker on their server, provides **their own PostgreSQL** connection string (their data stays in their database)
4. User enters the sync key into Docker config
5. Instance syncs **required aggregate info** to your super admin
6. **Disclosed upfront and consented** — the user is told what syncs and why
7. The self-hoster becomes their own super admin for their instance (sees everything locally)
8. **All external services are BYOK** — email, WhatsApp, CAPTCHA, social — the self-hoster supplies their own keys, never the platform's

### What Syncs From Self-Hosted Instances

| Synced (aggregate/operational) | NEVER synced (personal data) |
|---|---|
| Instance ID, tenant/owner | User records, names, emails, phones |
| Version, uptime, health | Passwords, hashes, tokens, secrets, MFA seeds |
| Counts (users, apps, logins/day) | Database contents |
| License/key status | Any end-user PII |

### Two Critical Guardrails
1. **Sync is aggregate-only, never PII** — keeps it compliant and trustworthy. ("500 users exist" yes; *who* they are, never.)
2. **Sync failure must never break auth** — the instance runs fully even if it can't reach you; sync is best-effort/background; the key validates occasionally with a long offline grace period (30+ days), never per-request. An auth system that breaks when it can't reach the mothership is a dealbreaker.

### Why This Is Good
Cuts your cloud cost (they bear compute/storage), wins privacy-conscious + enterprise customers, and the key + disclosure model keeps it trustworthy (no secret phone-home scandal). Same codebase, cloud and self-host; self-host is fully feature-complete, not crippled.

### Central Super Admin View
Sees: your cloud tenants (full) + self-hosted instances (anonymous aggregate only). Does NOT control or peer into self-hosted deployment data.

---

## 21. Compliance (GDPR + DPDP)

Build to both EU (GDPR) and India (DPDP — Digital Personal Data Protection Act) from the start:

- **Data minimization** — store only what has a defined purpose (built into tenant-controlled + retention model)
- **Data export** — give a user all data stored about them
- **Right to erasure** — full deletion on request
- **Retention policies** — auto-expire old data
- **Purpose limitation** — data used only for auth
- **Consent tracking** — record what users agreed to, timestamp + version
- **Consent screens** — when a third-party app requests user data (skipped for first-party)
- **Breach notification** readiness
- **Data residency** — EU/India/APAC region options; self-host = full control

SOC 2, HIPAA, ISO 27001 — later, for enterprise.

---

## 22. Tech Stack

| Component | Choice | Notes |
|---|---|---|
| Runtime | ASP.NET Core | Top-tier performance, LTS |
| OAuth/OIDC engine | OpenIddict | Certified, standards-compliant, native .NET |
| Database | PostgreSQL | Free, JSON support, RLS, scales |
| ORM | Entity Framework Core | + EF Core Identity foundation |
| Session/cache | Redis | Sessions, rate limits, cache |
| Password hashing | Argon2id | Memory-hard, GPU-resistant |
| MFA | Own TOTP (RFC 6238) | No external dependency |
| JWT signing | RS256 / ES256 | Asymmetric — local verification |
| Key storage | Vault / secrets manager | Never in config |
| Email | Zepto/SMTP/SendGrid/SES (BYOK) | Swappable |
| WhatsApp | MSG91/Gupshup/Meta (BYOK) | Generic HTTP adapter |
| Container | Docker (+ K8s for cloud) | Self-host = Docker image |
| Migrations | EF Core Migrations | Version-controlled |
| Observability | OpenTelemetry → Grafana/Prometheus | Tracing + metrics |
| Logging | Structured JSON → ELK/Loki | Tenant-scoped |

---

## 23. Phased Delivery Plan

### Phase 1 — Foundation
Core: PostgreSQL schema, Redis, multi-tenancy (tenant model + isolation), OAuth 2.0 + OIDC engine (Auth Code + PKCE, Client Credentials, Refresh + rotation), tokens (Access/ID/Refresh), email + WhatsApp OTP (BYOK), social login (Google/Facebook/etc.), MFA (TOTP, Email OTP, WhatsApp OTP, Passkeys, backup codes), account linking, hybrid RBAC + permissions, custom token claims (static + metadata + webhook), client & secret system, app-to-app (Client Credentials + OBO), user management, both admin panels (super + tenant), hosted login + visual branding, template system, webhooks + pipeline hooks, security baseline (Argon2id, rate limiting, lockout, breached passwords), account recovery + email/phone change, GDPR/DPDP basics (export, erasure, consent), sandbox environment, onboarding wizard, first SDKs.

### Phase 2 — Advanced
Risk-based / conditional access, ABAC policies, anonymous/guest auth, end-user self-service portal (full), API tokens, device management, impersonation, anomaly detection, audit log streaming (Datadog/Splunk), more social providers, full white-label (custom domain), migration tools (Auth0/Firebase importers + lazy password migration), more SDKs, CLI, self-hosted Docker + sync + telemetry.

### Phase 3 — Enterprise
SAML 2.0 SSO, SCIM 2.0 provisioning, enterprise IdP federation (Azure AD, Okta, Google Workspace), workload identity federation, sender-constrained tokens (DPoP/mTLS), private_key_jwt + mTLS client auth, full data residency, advanced compliance (SOC 2 etc.), sub-organizations (full), B2B onboarding.

### Phase 4 — Future
Identity analytics, SAML IdP (us as provider), B2C self-registration, federation hub, advanced risk scoring.

---

## 24. Remaining Build-Time Decisions

These are small and can be decided during architecture, not blockers:

1. **Login delivery** — hosted page vs embeddable widget vs both (leaning: both)
2. **WhatsApp path** — direct Meta Business API vs provider-only (MSG91/Gupshup) first
3. **First SDKs** — which 2–3 at launch (suggested: Next.js/React, Node, .NET)
4. **First target customer** — startup-first (fast/easy) vs enterprise-first (sets priority order)
5. **Open-source license** — exact license + contribution governance
6. **Dashboard embeddable** — whether tenants can give their own customers an embedded user-management UI

---

*This document is the single source of truth for the Saarvix Identity Platform product definition. The next phase is the detailed technical architecture: system architecture, full database schema, OAuth/OIDC flow specs, API specifications, deployment architecture, and the development roadmap derived from the phased plan above.*
