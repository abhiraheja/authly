# Saarvix Identity Platform — Plan Summary

A free, open-source, multi-tenant Identity-as-a-Service (IDaaS) platform. Companies sign up, get authentication for their products, and configure everything themselves. Alternative to Auth0, Microsoft Entra, Google Identity, and Supabase Auth — with features they lack.

---

## 1. What We Are Building

A standalone identity platform that other companies plug into their apps for login, security, and user management.

- **Multi-tenant** — many companies use one platform, fully isolated from each other
- **Open source + free** — no pricing for now; everything available
- **Cloud + self-hostable** — same product, two deployment options
- **Standards-based** — OAuth 2.0 + OpenID Connect + SAML + WebAuthn (no proprietary protocol)
- **Built on** — ASP.NET Core + OpenIddict + PostgreSQL + Entity Framework Core + Redis

---

## 2. Three Surfaces (Who Uses What)

| Surface | Who | What they control |
|---|---|---|
| **Super Admin Panel** | You (platform owner) | Entire platform, all tenants, system config, health |
| **Tenant Admin Panel** | Companies who sign up | Only their own tenant — users, apps, branding, auth |
| **End-User Portal** | The companies' customers | Their own profile, password, MFA, sessions |

**Hard rule:** a tenant can never see or affect anything outside their own tenant.

---

## 3. Authentication

**Local:** email + password, username + password, magic link (passwordless).

**Mobile OTP:** WhatsApp OTP (our key differentiator), optional voice call. No SMS.

**Social:** Google, Facebook, Instagram, GitHub, Microsoft, Apple, Twitter/X, LinkedIn, any custom OAuth2/OIDC.

**Enterprise SSO:** SAML 2.0, OIDC federation, Azure AD, Google Workspace, Okta, with just-in-time provisioning.

**Special modes:** anonymous/guest auth (upgrade later), account linking (same person via email + Google = one account).

---

## 4. Multi-Factor Authentication

We build our own TOTP system (RFC 6238) — no Microsoft/Google dependency. Works with any authenticator app.

- TOTP (any authenticator app)
- Email OTP
- WhatsApp OTP
- Passkeys / WebAuthn (Face ID, Touch ID, hardware keys)
- Backup codes

Policy per-tenant + per-role: required for all / admins only / optional.

---

## 5. Authorization

Hybrid model: roles + fine-grained permissions + policies.

- **Roles** — system + custom per tenant
- **Permissions** — `resource.action` (e.g. `user.read`, `project.delete`)
- **ABAC policies** — attribute-based rules (Phase 2)
- **Risk-based / conditional access** — e.g. require MFA from new device, block outside India (Phase 2)

---

## 6. App-to-App Authentication

- **Client Credentials** — service acts as itself (cron jobs, microservices)
- **On-Behalf-Of (OBO)** — service calls another service AS the user, preserving identity through the chain
- **Client auth methods** — secret (basic/post), private_key_jwt, mTLS certificate
- **Workload identity federation** — external systems get tokens without storing secrets (Phase 3)

---

## 7. Client & Secret System

Each tenant creates Applications, each with a Client ID + Secret.

| App Type | Use | Secret |
|---|---|---|
| Web App | Next.js, .NET, Laravel | Yes |
| SPA / Mobile | React, Vue, Flutter | No (PKCE instead) |
| Machine | Cron, microservice | Yes |

- Secret shown once, stored hashed, never recoverable
- Zero-downtime rotation
- Scopes per app (least privilege)
- API Keys (separate) for direct API access

Tenants use Client + Secret to: integrate login, manage users via API, verify tokens, trigger actions (suspend, reset, revoke).

---

## 8. Token Strategy

| Token | Format | Lifetime | Purpose |
|---|---|---|---|
| Access | JWT (RS256) | 15 min – 1 hr | API authorization |
| ID | JWT | Login session | User identity |
| Refresh | Opaque (hashed) | 30 days sliding | Session continuity |
| API | Opaque (hashed) | Optional | Automation |
| Service | JWT | Short | Internal comms |

- Asymmetric signing — services verify locally with public key (fast)
- Refresh token rotation + reuse detection (reuse kills the session)
- Audience binding — Service A's token can't be used on Service B
- Automatic key rotation

---

## 9. Custom Token Claims

Companies inject their own data into tokens. Three ways:

1. **Static** — fixed key-value pairs in every token
2. **User metadata** — map stored profile fields into the token
3. **Webhook** — call the company's API before issuing the token; they return data to inject

Configurable per token type (ID vs Access). Visual dashboard, no code on our side.

**Key rule:** `user_metadata` (user can edit) vs `app_metadata` (only backend can edit — plan, roles). Users can't edit their own permissions.

---

## 10. Messaging & Templates

**Channels:** Email (Zepto/SMTP/SendGrid/SES), WhatsApp (MSG91/Gupshup/Meta), plus a generic HTTP adapter for any provider.

**Modes:** Managed (our keys, cloud only) or BYOK (their keys, their domain). Self-hosted = BYOK only.

**Templates:** built-in for all auth events (verify, reset, OTP, welcome, alerts). Visual editor, variables (`{{otp}}`, `{{user_name}}`, etc.), multi-language, preview, test send. Security-critical templates can be styled but not broken.

**Reliability:** channel fallback (WhatsApp fails → email), delivery tracking, managed-mode sending caps.

---

## 11. Login UI & Branding

- Hosted login page on our domain or the tenant's custom domain
- Visual builder — logo, background image/video, colors, fonts, layouts, dark mode
- Embeddable components (React, Vue, vanilla JS)
- Full HTML/CSS override
- Branded emails and end-user portal to match

---

## 12. Webhooks & Pipeline Hooks

**Pipeline hooks** — run custom code/webhook at each stage: pre/post registration, pre/post login, pre-token-issuance, send-OTP, send-email, password change, MFA. One system powers custom claims, WhatsApp OTP, and BYOK email.

**Webhooks** — 40+ events (login, user lifecycle, MFA, sessions, org, token, app). Consistent payload, HMAC-signed, replay protection, retry with backoff, delivery log, test in dashboard.

---

## 13. Multi-Tenancy

- Unlimited organizations, fully isolated
- Email unique **per tenant** (same person can have separate accounts across tenants)
- Per-org branding, custom domain, SSO, messaging provider, token claims
- Sub-organizations (nested — for divisions/branches)
- Invitations (single + bulk + API)
- Onboarding wizard; offboarding with 30-day grace + export + delete
- Isolation: `tenant_id` on every table + PostgreSQL Row Level Security

---

## 14. Data Storage & Retention

**Captured:** avatar/image, email, phone, name, timezone, locale, custom metadata. Plus security data: IP, full login history (time/IP/device/browser/OS/location), device fingerprints, sessions, failed attempts, suspicious-activity flags, full audit trail.

**Protected (unreadable even to platform owner):**

| Data | Storage |
|---|---|
| Password | Argon2id hash |
| MFA secret | AES-256 encrypted |
| Backup codes | Hashed |
| Refresh tokens | SHA-256 hashed |
| Social tokens | AES-256 encrypted |

**Retention (configurable per tenant):** login history 90d–1yr, audit logs 1yr+, failed attempts 30d, profile until deleted. Rich capture for forensics, auto-expiry to limit liability. Supports export + erasure.

---

## 15. Security (Defense in Depth)

- **Credentials** — Argon2id, encrypted secrets, breached-password detection
- **Tokens** — short-lived, rotation + reuse detection, asymmetric signing, audience binding
- **Login attacks** — rate limiting, lockout, bot detection + CAPTCHA, suspicious login detection, mandatory PKCE
- **Transport** — TLS 1.3, HSTS, secrets in vault, WAF + DDoS
- **Isolation** — tenant scoping + RLS
- **Detection** — immutable audit logs, anomaly detection, real-time alerts
- **Process** — security review, pen testing, bug bounty, OWASP ASVS
- **Block/allow lists** — email, domain, IP, country

> No system is "100% secure." Goal: highest practical bar, matching or exceeding the major players.

---

## 16. Performance

| Operation | Target |
|---|---|
| Token verification (client-side) | < 1ms |
| Login page load | < 500ms |
| Token issuance | < 100ms |
| Management API | < 200ms |

- Stateless JWT verification (no round-trip per request — the key scaling decision)
- Redis cache, PostgreSQL indexing + pooling + read replicas
- ASP.NET Core (top-tier speed), stateless services, horizontal autoscaling, CDN

---

## 17. Account Recovery & Identity Changes

**Recovery (tiered, configurable):** backup codes → recovery contact → admin-assisted → identity verification. Every recovery notifies all contacts, optional cooldown, fully audited, strict tenants can disable self-service.

**Email/phone change:** verify current identity → verify new contact → notify old contact with cancel link → optional cooldown. Defeats hijack-then-lockout.

**Limits:** configurable max concurrent sessions and active refresh tokens per user.

---

## 18. Hosting: Cloud + Self-Hosted

**Cloud (default):** you host, you're super admin, managed sending available.

**Self-Hosted (Docker):**
1. User signs up on your cloud, gets Docker image + sync key
2. Runs Docker with their own PostgreSQL (their data stays with them)
3. Enters sync key; instance syncs aggregate info to your super admin
4. Disclosed + consented upfront
5. Self-hoster is their own super admin; all external services BYOK

**What syncs:** instance ID, version, health, counts, license status.
**What never syncs:** user records, PII, passwords, tokens, secrets, database contents.

**Two guardrails:**
1. Sync is aggregate-only, never PII (keeps it compliant + trustworthy)
2. Sync failure never breaks auth (instance runs fully offline; key validates occasionally with 30+ day grace)

**Why:** cuts your cloud cost, wins privacy/enterprise customers, stays trustworthy.

---

## 19. Compliance

Built to GDPR (EU) + DPDP (India) from the start: data minimization, export, right to erasure, retention policies, purpose limitation, consent tracking, consent screens, breach-notification readiness, data residency. SOC 2 / HIPAA / ISO later for enterprise.

---

## 20. Tech Stack

| Component | Choice |
|---|---|
| Runtime | ASP.NET Core |
| OAuth/OIDC engine | OpenIddict |
| Database | PostgreSQL |
| ORM | Entity Framework Core |
| Cache / sessions | Redis |
| Password hashing | Argon2id |
| MFA | Own TOTP (RFC 6238) |
| JWT signing | RS256 / ES256 |
| Email | Zepto/SMTP/SendGrid/SES (BYOK) |
| WhatsApp | MSG91/Gupshup/Meta (BYOK) |
| Container | Docker (+ K8s for cloud) |

---

## 21. Phased Delivery

**Phase 1 — Foundation:** multi-tenancy, OAuth/OIDC engine, all token types, email + WhatsApp OTP, social login, MFA (TOTP/Email/WhatsApp/Passkeys/backup), account linking, RBAC + permissions, custom claims, client/secret system, app-to-app (Client Credentials + OBO), user management, both admin panels, hosted login + branding, templates, webhooks + hooks, security baseline, recovery + identity changes, GDPR/DPDP basics, sandbox, onboarding.

**Phase 2 — Advanced:** risk-based access, ABAC, anonymous auth, end-user portal, API tokens, device management, impersonation, anomaly detection, log streaming, more social, custom domain, migration tools, more SDKs, CLI, self-hosted Docker + sync.

**Phase 3 — Enterprise:** SAML, SCIM, enterprise IdP federation, workload identity federation, DPoP/mTLS, full data residency, advanced compliance, sub-organizations, B2B onboarding.

**Phase 4 — Future:** identity analytics, SAML IdP, B2C self-registration, federation hub.

---

## 22. Still To Decide (Build-Time)

1. Login delivery — hosted vs embeddable vs both (leaning both)
2. WhatsApp — direct Meta API vs provider-only first
3. First SDKs — which 2–3 at launch (suggested: Next.js/React, Node, .NET)
4. First target customer — startup vs enterprise (sets priority)
5. Open-source license + contribution governance
6. Embeddable dashboard — let tenants give their customers a user-management UI

---

*Next phase: technical architecture — full database schema, OAuth/OIDC flow specs, API specifications, deployment design.*
