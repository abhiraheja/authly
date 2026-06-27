# Authly

**A free, open-source, multi-tenant Identity-as-a-Service (IDaaS) you can self-host in minutes.**

Authly is a complete, standards-based authentication and authorization platform — a drop-in
alternative to **Auth0, Microsoft Entra, Google Identity, and Supabase Auth** — with features
they either charge for or don't offer at all: **WhatsApp OTP, bring-your-own-keys (BYOK)
messaging/email, a fully white-label hosted login, fine-grained authorization built in, and the
*same product* whether you run it in the cloud or on your own box.**

> ASP.NET Core 10 (MVC + Razor) · OpenIddict (certified OAuth2/OIDC) · PostgreSQL + EF Core ·
> Redis · Hangfire · Argon2id · AES-256-GCM · Docker. Modular monolith — one image, run anywhere.

---

## Table of contents

- [Why Authly exists](#why-authly-exists)
- [How Authly is different](#how-authly-is-different)
- [Features](#features)
- [Architecture](#architecture)
- [Why PostgreSQL](#why-postgresql)
- [Why Redis](#why-redis)
- [Quick start (development)](#quick-start-development)
- [The public website (`Website__Enabled`)](#the-public-website)
- [Production deployment](#production-deployment)
- [Configuration reference](#configuration-reference)
- [Integrate your app (OAuth2 / OIDC)](#integrate-your-app-oauth2--oidc)
- [Management API](#management-api)
- [Authorization: RBAC, ABAC & app-to-app](#authorization-rbac-abac--app-to-app)
- [Security model](#security-model)
- [Observability](#observability)
- [Project structure](#project-structure)
- [Running without Docker](#running-without-docker)
- [Roadmap](#roadmap)
- [License](#license)

---

## Why Authly exists

Every team that ships software needs login. Today the realistic options all force a painful
trade-off:

- **Auth0 / Okta** — excellent, but expensive, priced per monthly-active-user, and cloud-locked. The
  features you actually need at scale (SSO, fine-grained authorization, custom claims) sit behind
  enterprise tiers.
- **Microsoft Entra (Azure AD B2C)** — powerful but notoriously hard to integrate and customize.
- **Google Identity** — fine if you live entirely inside Google; limited branding and portability.
- **Supabase Auth** — open-source and self-hostable, but auth is a feature of a database product, not
  a full IdP: no enterprise SSO, limited authorization, limited white-labeling.
- **Keycloak** — open-source and capable, but heavy to operate and dated to customize.

**Authly's bet:** you should be able to run a *complete*, beautiful, enterprise-grade identity
platform yourself, for free, with no per-user tax and no phone-home — and get the same product if you
ever want it hosted. Own your users, own your data, own your login page.

---

## How Authly is different

| Capability | Auth0 | Entra | Google | Supabase | **Authly** |
|---|:---:|:---:|:---:|:---:|:---:|
| **Open source** | ✗ | ✗ | ✗ | ✅ | ✅ |
| **Self-hostable (no MAU limits, no phone-home)** | ✗ | ✗ | ✗ | ✅ | ✅ |
| **Same product cloud *and* self-host** | ✗ | ✗ | ✗ | ✅ | ✅ |
| **WhatsApp OTP login** | ✗ | ✗ | ✗ | ✗ | ✅ |
| **BYOK email domain / WhatsApp / CAPTCHA / social** | partial | ✗ | ✗ | ✗ | ✅ |
| **Fully white-label hosted login + custom domain** | paid | basic | basic | basic | ✅ |
| **Fine-grained authorization (RBAC) built in** | paid | ✅ | ✗ | DIY | ✅ |
| **Attribute-based access (ABAC) + conditional access** | paid | ✅ | partial | ✗ | ✅ |
| **Custom token claims (visual + webhook-driven)** | paid | ✅ | ✅ | hook | ✅ |
| **Append-only audit log + webhooks (40+ events)** | paid | ✅ | partial | partial | ✅ |
| **Price** | $$$ | $$$ | partial-free | partial-free | **free** |

Enterprise SSO via **SAML 2.0 / SCIM** and **token exchange (on-behalf-of)** are on the
[roadmap](#roadmap); everything else in the table is shipped today.

---

## Features

Everything below is **implemented and shipping** (see [Architecture](#architecture) for where each
lives). Planned items are called out in [Roadmap](#roadmap).

### Authentication
- **Password** (email + password) with **Argon2id** hashing, email verification, and password reset.
- **Magic links** — passwordless, single-use, short-lived email sign-in.
- **Passkeys / WebAuthn (FIDO2)** — Face ID, Touch ID, hardware keys; cloned-key detection via the
  signature counter.
- **Social login** — Google, Microsoft, GitHub, Facebook presets plus **any custom OAuth2/OIDC
  provider**. BYOK (your client id/secret, encrypted at rest), just-in-time user creation, and
  account linking by verified email.
- **Phone / WhatsApp OTP** — sign up and log in with a mobile number, tied to a unified identity.
- Each tenant chooses which of the five sign-in methods are enabled (with an "at least one effective
  method" guard so you can never lock everyone out).

### Multi-factor authentication
- **TOTP** (RFC 6238) with QR enrollment — works with Google Authenticator, Authy, 1Password, etc.
- **Email OTP** and **WhatsApp OTP** as MFA challenges.
- **Passkeys** as a second factor.
- **Backup codes** — one-time, hashed.
- **Policy** per tenant and per role: Optional / Admins-only / Required.

### Authorization
- **RBAC** — system roles + custom per-tenant roles, each a set of fine-grained `resource.action`
  permissions; surfaced as a `roles` claim in tokens.
- **ABAC (attribute-based access)** — allow/deny policies with conditions over
  `subject.*` / `resource.*` / `environment.*`, deny-overrides + default-deny, priority ordering, and
  a **live "test a decision" console**. Evaluable at runtime via `POST /api/v1/access/evaluate`.
- **Conditional / risk-based access** — per-tenant actions for *new device* and *unverified email*
  (Allow / Require step-up MFA / Block).
- **App-to-app** — Client Credentials grant for machine clients, scoped to least privilege.

### Multi-tenancy
- **Organizations → Projects** — an organization groups projects, and each **project is an isolated
  tenant / environment** (run dev, staging and prod as sibling projects).
- **Operator RBAC** for your team: `org_owner` / `org_admin` / `project_admin` / `viewer`.
- **Member invites** — invite teammates as console operators with scoped roles.
- **PostgreSQL Row-Level Security** isolates tenant data at the database layer — see
  [Why PostgreSQL](#why-postgresql).
- **Custom domain per project** for the hosted login page.

### Hosted login & white-label branding
- A **hosted, fully brandable login page**: logo, colors, fonts, layout (centered / split),
  background images, card styling, dark mode, and custom copy — editable from the admin console.
- Brand carries into the **branded email templates** and the **end-user portal**.

### Applications, secrets & API keys
- Register OAuth clients of every type — **Web** (confidential), **SPA / Native** (public + PKCE),
  **Machine** (service).
- Client secrets shown once, stored **Argon2id-hashed**, with **zero-downtime rotation**.
- Separate **API keys** (`X-API-Key`) for the Management API — scoped, optional expiry, SHA-256
  hashed.

### Messaging (BYOK)
- **Email** via SMTP / Zepto Mail (pluggable), **WhatsApp** via MSG91 / Gupshup.
- **Bring your own keys** per project; provider secrets **AES-256-GCM encrypted at rest**.
- Editable, multi-locale message templates with `{{variables}}`, preview, test-send, and a delivery
  log. All sends go through Hangfire (never inline on the request path).

### Extensibility — webhooks, pipeline hooks & custom claims
- **Webhooks** — subscribe to **40+ identity events**; HMAC-SHA256 signed, timestamped (replay
  protection), wildcard routing, delivery log + manual retry, Hangfire dispatch with backoff.
- **Pipeline hooks** — inject your own HTTP logic at auth stages (pre/post login, pre-token,
  send-OTP, …) with per-call timeout and fail-open/fail-closed behavior.
- **Custom token claims** — static, user-metadata-mapped, or webhook-sourced; configured visually,
  per token type.

### End-user self-service portal (`/portal/*`)
Profile · password change (evicts other sessions) · MFA & passkeys · **known devices**
(trust / rename / forget) · **active sessions** (revoke one or all others) · login & security
**activity** · **secure email/phone change** (verify old + new, with a cancellation window) ·
**GDPR/DPDP data export** (JSON, no secrets) and **account erasure** (cascade delete + audit).

### Security & compliance
Rate limiting · account lockout with exponential backoff · **HaveIBeenPwned** breached-password
screening (k-anonymity) · **CAPTCHA** (hCaptcha / Turnstile) · disposable-email blocking ·
email/domain/IP/country block-and-allow lists · suspicious-login detection (new device + new IP) ·
strict security headers (CSP/HSTS/…) · **append-only audit log** · consent capture · configurable
retention. See [Security model](#security-model).

### Migration
Import users from **Auth0**, **Firebase**, or **generic JSON**. Passwords aren't migrated (foreign
hashes can't be verified) — imported users set a password via forgot-password on first sign-in.

---

## Architecture

Authly is a **modular monolith**: one deployable ASP.NET Core app, cleanly separated into a domain
core, business modules, and infrastructure. It exposes **four surfaces**:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                            Authly (one image)                              │
│                                                                            │
│  /signup            Self-serve: create Account + Organization + Project    │
│  /tenantadmin/*     Operator console — apps, users, roles, branding,       │
│                     security, messaging, webhooks, observability           │
│  /account/*  /portal/*   Hosted login (password/magic-link/passkey/        │
│                          social/phone OTP) + end-user self-service portal   │
│  /.well-known/*  /connect/*   Standards OAuth2/OIDC authorization server    │
│                                                                            │
│        ┌───────────────┐         ┌───────────────┐                         │
│        │  PostgreSQL    │         │     Redis     │                         │
│        │  data + RLS    │         │ cache · rate  │                         │
│        │  + Hangfire    │         │ limit · lock  │                         │
│        └───────────────┘         └───────────────┘                         │
└──────────────────────────────────────────────────────────────────────────┘
```

### Identity model: Account → Organization → Project (= tenant = environment)

- An **Account** is a *global* operator identity (your team) — one email, one password, used to sign
  in to the console regardless of tenant.
- An **Organization** groups **Projects**. Each **Project is a tenant**, i.e. an isolated environment
  with its own applications, end-users, roles and branding. Run `dev` / `staging` / `prod` as sibling
  projects under one organization.
- **End-users** belong to a project/tenant. Email is unique **per tenant**, so the same person can
  exist independently across tenants.
- **Operator RBAC** (`org_owner` / `org_admin` / `project_admin` / `viewer`) governs what each member
  of your team can do; **end-user RBAC** is a separate layer that governs the people who log in to
  *your* apps.

### Tenant resolution

In production, requests map to a tenant by **custom domain** (e.g. `login.acme.com → acme`). In
development you select the tenant with `?tenant=<slug>` (remembered in a cookie) or an
`X-Tenant-Slug` header. `/signup` is intentionally tenant-less.

---

## Why PostgreSQL

PostgreSQL is the system of record, and the choice is deliberate:

- **Row-Level Security (RLS) for true tenant isolation.** Every tenant-scoped table carries a
  `tenant_id`, and an RLS policy ties every query to `current_setting('app.current_tenant')`, which
  middleware sets per request. Isolation is enforced **at the database**, so even a bug in
  application code can't leak one tenant's data to another — it's a backstop, not just a convention.
  (Global tables — accounts, organizations, memberships, operator roles — are deliberately RLS-exempt.)
- **One dependency does double duty.** PostgreSQL is also the **Hangfire** job store, so background
  work (email/WhatsApp delivery, webhook dispatch, retention cleanup, suspicious-login checks) needs
  no extra broker.
- **EF Core 10 + Npgsql** give clean migrations (auto-applied on boot) and a productive data layer.
- **JSONB** stores flexible config (branding, settings, policy conditions, audit metadata) without
  schema churn.
- **It's boring and proven** — exactly what you want under an identity system.

What it stores: tenants/orgs/accounts/memberships, the per-tenant user pool, OAuth applications &
hashed secrets, credentials (Argon2id hashes, passkeys, verification/reset tokens), roles &
permissions (both operator and end-user), sessions & refresh-token families, webhooks & deliveries,
messaging config & templates, and the append-only audit log.

## Why Redis

Redis holds the **fast, shared, short-lived state** that must be consistent across every running
instance:

- **Distributed cache & sessions** — so the app scales horizontally without sticky sessions.
- **Rate limiting** — an atomic fixed-window counter per `path + IP`, returning `429` with
  `Retry-After`. Because the counter lives in Redis, a flood can't be spread thin across instances by
  a load balancer.
- **Login lockout** — failed-attempt counters and lockout deadlines with TTLs, keyed by tenant +
  identity. An attacker can't dodge lockout by hitting a different node.

In short: **PostgreSQL is the durable source of truth; Redis is the shared, ephemeral coordination
layer.** Behind a load balancer, a Postgres-only deployment would let attackers slip past rate limits
and lockout by hopping between instances — Redis is what keeps those checks global.

---

## Quick start (development)

**Prerequisite:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Docker Engine +
Compose). PostgreSQL and Redis are bundled in the Compose file — you don't install them yourself.

```bash
# from the repo root
docker compose up --build -d          # build + start app, Postgres, Redis (+ observability stack)
docker compose logs -f app            # watch EF Core migrations apply on first boot
```

Open **http://localhost:8080** and click **Sign up** — the account you create becomes the first
operator of its organization. EF Core migrations run automatically on startup.

| Check | URL / command |
|---|---|
| App | http://localhost:8080 |
| Hangfire dashboard | http://localhost:8080/hangfire |
| OIDC discovery | http://localhost:8080/.well-known/openid-configuration |
| Postgres | `docker compose exec postgres psql -U authly -d authly -c "\dt"` |
| Redis | `docker compose exec redis redis-cli ping` → `PONG` |
| Grafana (bundled) | http://localhost:3000 |

Stop and clean up:

```bash
docker compose down                   # stop, keep the database volume
docker compose down -v                # stop and WIPE the database
```

The dev Compose file ships safe defaults for everything, so it runs with no `.env` at all — but set
your own `ENCRYPTION_KEY` for anything real (see [Configuration](#configuration-reference)).

---

## The public website

Authly contains its **own marketing website + docs** (landing page, features, self-host guide, API
docs) built into the product. It is gated by a single flag:

- **`Website__Enabled=false` (default).** Only the IdP/console is served — login, OIDC, admin,
  portal. Any marketing route redirects to the admin console. This is the right setting when you
  self-host purely as an identity provider.
- **`Website__Enabled=true`.** The public site is served at `/` (with `/features`, `/self-host`,
  `/docs`, …). Use this if you want the full product experience, including browsable documentation.

The flag is read once at startup ([`WebsiteGateMiddleware`](src/Authly.Web/Infrastructure/Security/WebsiteGateMiddleware.cs)),
so recreate the container after changing it. In `docker-compose.yml` it's wired as
`Website__Enabled=${WEBSITE_ENABLED:-false}`.

---

## Production deployment

For a hardened, real-world setup — external/tuned PostgreSQL with a bind-mounted data directory,
Redis with a password + AOF persistence, Traefik TLS, resource limits, and per-container networking —
follow the in-product **Production deployment** guide ([`/docs/production`](src/Authly.Web/Views/Docs/Production.cshtml),
visible when `Website__Enabled=true`).

Production prerequisites at a glance:

- A Linux host with **Docker Engine 24+** and the **Compose v2** plugin (~6 GB RAM, 2+ vCPU).
- A **domain + DNS A record** for TLS.
- A **reverse proxy you provide** (Traefik / nginx / Caddy) — Authly's Compose emits Traefik labels
  but does not ship Traefik itself.
- A pre-created **external Docker network** shared with the proxy.
- **PostgreSQL** and **Redis** (the bundled containers, or your own managed instances — just point
  `DATABASE_URL` / `REDIS_URL` at them).
- Secrets: a fresh `ENCRYPTION_KEY`, strong `POSTGRES_PASSWORD` and `REDIS_PASSWORD`.

Updating is `docker compose pull && docker compose up -d`; data persists in the Postgres volume.

---

## Configuration reference

Set these as environment variables (e.g. in a `.env` next to your Compose file). `__` maps to nested
config keys (`Website__Enabled` → `Website:Enabled`).

| Variable | Required | What it is |
|---|:---:|---|
| `ENCRYPTION_KEY` | **Yes** | 32-byte base64 key for AES-256-GCM secret encryption. App refuses to start without it. Generate: `openssl rand -base64 32`. |
| `DATABASE_URL` | Yes | PostgreSQL connection string (Npgsql), e.g. `Host=postgres;Database=authly;Username=authly;Password=authly`. |
| `REDIS_URL` | Yes | Redis `host:port` (+ `password=…,ssl=…` options in production). |
| `Website__Enabled` | No | `true` serves the public marketing site + docs; default `false` (IdP/console only). |
| `CORS_ALLOWED_ORIGINS` | No | Extra trusted browser origins for SPA CORS (registered redirect-URI origins are allowed automatically). |
| `RETENTION_AUDIT_DAYS` | No | Audit-log retention (default 365). |
| `RETENTION_LOGIN_HISTORY_DAYS` | No | Login-history retention (default 90). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No | OpenTelemetry collector endpoint (startup fallback; the in-app Observability page can override). |
| `LOG_STREAM_ENDPOINT` / `LOG_STREAM_KEY` | No | Fallback audit-log streaming target (overridable in-app). |
| `Authly__Tokens__DisableAccessTokenEncryption` | No | `true` issues plain (signed, unencrypted) JWT access tokens — handy when downstream services need to read claims directly. |
| `APP_PORT`, `POSTGRES_*`, `REDIS_PASSWORD` | No | Compose-level host port and bundled-service credentials. |

> **Production:** always generate a fresh `ENCRYPTION_KEY`, supply DB/Redis credentials as secrets
> (not committed), and terminate TLS at a reverse proxy in front of the app.

---

## Integrate your app (OAuth2 / OIDC)

Authly is a certified **OpenID Connect / OAuth 2.0** provider (powered by OpenIddict). Point any
standards-compliant OIDC client at the discovery document and you're integrated:

```
GET /.well-known/openid-configuration     # discovery
GET /.well-known/jwks.json                # public signing keys (verify JWTs offline)
GET /connect/authorize                    # start Authorization Code + PKCE
POST /connect/token                       # exchange code / refresh / client credentials
GET /connect/userinfo                     # OIDC userinfo
POST /connect/introspect                  # token introspection
POST /connect/revoke                      # token revocation
POST /connect/logout                      # RP-initiated end-session
```

**Supported grants:** Authorization Code **+ PKCE** (web, SPA, native), **Refresh Token** (rotated,
single-use, with reuse detection that revokes the whole token family on replay), and **Client
Credentials** (machine-to-machine). Access tokens are JWTs signed with RS256/ES256; apps verify them
offline using the cached JWKS — no round-trip per request. Scopes:
`openid profile email offline_access roles`.

A minimal flow:

```
# 1. Send the user to authorize
GET /connect/authorize?response_type=code&client_id=<id>
    &redirect_uri=https://app.example.com/callback
    &scope=openid%20profile%20email%20offline_access&state=...&code_challenge=...&code_challenge_method=S256

# 2. Exchange the returned code for tokens
POST /connect/token
    grant_type=authorization_code&code=<code>&redirect_uri=https://app.example.com/callback
    &code_verifier=<verifier>&client_id=<id>&client_secret=<secret>
```

See [`docs/developer/Quickstart.md`](docs/developer/Quickstart.md) for an end-to-end integration.

---

## Management API

A REST API (authenticated with an `X-API-Key`, scoped per key) for automating everything the console
does — under `/api/v1/*`:

- **Users** — list/search, get, create, update, delete, suspend/reactivate, force password reset,
  role assignment, session listing/revocation.
- **Roles & permissions** — list/create roles, assign permissions.
- **Applications** — list/create/update/delete, rotate secrets.
- **Audit logs** — filterable by event and actor.
- **Access evaluation** — `POST /api/v1/access/evaluate` returns an ABAC allow/deny decision.

Responses are enveloped (`data`, `total`, `page`, `limit`); every endpoint is permission-gated. See
[`docs/developer/`](docs/developer/) for details.

---

## Authorization: RBAC, ABAC & app-to-app

- **RBAC** — roles are named bundles of `resource.action` permissions; assign roles to users, and the
  effective permissions surface as a `roles` claim.
- **ABAC** — for decisions that depend on *context*. Policies match by action (with `*` wildcards),
  resource type, and a set of conditions (`equals`, `notEquals`, `contains`, `in`, `greaterThan`,
  `lessThan`, `exists`) over `subject.*` / `resource.*` / `environment.*`. Evaluation is
  **deny-overrides with default-deny**; test decisions live in the console or via the API. Details in
  [`docs/developer/Access-Policies-and-App-to-App.md`](docs/developer/Access-Policies-and-App-to-App.md).
- **App-to-app** — create a **Machine** application, request a token with
  `grant_type=client_credentials`, and the receiving service verifies the JWT (signature, `iss`,
  `exp`, `tenant_id`, required scope) via JWKS. Give each service its own client and least-privilege
  scopes.

---

## Security model

- **Passwords:** Argon2id (memory-hard) hashing; HaveIBeenPwned breached-password screening via
  k-anonymity (only a 5-char SHA-1 prefix ever leaves the server) on register/reset/recovery.
- **Brute-force defense:** Redis-backed rate limiting (`429 + Retry-After`) and account lockout with
  exponential backoff — both global across instances.
- **Bots:** hCaptcha / Turnstile, per tenant, with the secret encrypted at rest.
- **Abuse controls:** disposable-email blocking and email / domain / IP(CIDR) / country
  block-and-allow lists.
- **Risk-based:** suspicious-login detection (new device + new IP) and conditional-access actions
  (allow / step-up MFA / block).
- **At rest:** AES-256-GCM for all provider/CAPTCHA/social secrets; tokens optionally encrypted.
- **Transport/headers:** CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Referrer-Policy,
  Permissions-Policy.
- **Auditability:** an **append-only** audit log (no UPDATE/DELETE from the app) records every state
  change with actor, result and metadata — never secrets, passwords, tokens or OTPs.
- **Token theft:** refresh-token rotation with family reuse-detection.
- **Privacy:** GDPR/DPDP data export and right-to-erasure from the end-user portal; consent captured
  at signup; configurable retention windows enforced by background jobs.

See [`docs/developer/Token-Claims-and-Encryption.md`](docs/developer/Token-Claims-and-Encryption.md).

---

## Observability

Pluggable **OpenTelemetry**: export traces, metrics and logs to any **OTLP** backend (Grafana,
Honeycomb, Datadog, …) or **Azure Monitor**. It's **opt-in** — nothing leaves the app until you
enable it in the console (**Observability**, requires `observability.manage`), where the exporter,
signals, sampling ratio and encrypted headers are configured. A telemetry processor enriches every
signal with `tenant.id` / `project.id` so you can slice dashboards per project. The audit log can be
streamed to the same sink. The dev `docker-compose.yml` bundles a full Grafana / Prometheus / Loki /
Tempo / OTel-Collector stack for local testing. See
[`docs/multi-tenent/05-pluggable-observability.md`](docs/multi-tenent/05-pluggable-observability.md).

---

## Project structure

```
src/Authly.Web/             MVC app — hosted login, admin console, end-user portal,
                            REST API, OAuth/OIDC endpoints, the public website
src/Authly.Core/            Domain — entities, enums, interfaces (depends on nothing)
src/Authly.Modules/         Business logic per module (Tenants, Auth, Users, Security, …)
src/Authly.Infrastructure/  EF Core, Redis, Hangfire, encryption, security primitives, providers
tests/Authly.Tests/         Unit tests
docs/                       Product spec, developer guides, architecture decisions
```

Why **MVC + Razor (server-rendered), not a SPA or GraphQL?** For an identity provider, server-rendered
login is the security-correct choice (no tokens in the browser, server-side PKCE, strict CSP) — the
same approach Google, Microsoft and Okta take. OpenIddict is deeply embedded and security-critical;
the architecture favors stability over a client-framework rewrite. Rationale in
[`docs/multi-tenent/07-framework-choice.md`](docs/multi-tenent/07-framework-choice.md).

---

## Running without Docker

Requires the **.NET 10 SDK** plus a local PostgreSQL and Redis (or point `DATABASE_URL` / `REDIS_URL`
at running instances).

```bash
dotnet build Authly.slnx
dotnet test  Authly.slnx
dotnet run --project src/Authly.Web
```

---

## Roadmap

Shipped today: everything in [Features](#features). Planned:

- **Enterprise SSO** — SAML 2.0 and SCIM provisioning.
- **Token exchange / on-behalf-of** (RFC 8693) for multi-hop service identity.
- **Sender-constrained tokens** (DPoP / mTLS) and workload identity federation.
- **Embeddable login components** (React / Vue / vanilla) for in-app login.
- **Tiered account recovery** and sub-organizations.

---

## License

Open source — the specific license is finalized before public release. Built and maintained by
**Abhishek Raheja**.
