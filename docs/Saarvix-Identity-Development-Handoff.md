# Saarvix Identity Platform — Development Handoff & Technical Architecture

> **Purpose of this document:** This is a complete, self-contained technical specification to build the Saarvix Identity Platform from scratch. It is written so a developer (or an AI coding assistant) can read it and begin implementation immediately, in the correct order, with all decisions already made.
>
> **Read this first, then build in the order given in Section 13 (Build Order).**

---

## 0. Context for the Developer

You are building a **free, open-source, multi-tenant Identity-as-a-Service (IDaaS) platform** — an alternative to Auth0, Microsoft Entra, Google Identity, and Supabase Auth.

Companies ("tenants") sign up, register their applications, and use this platform to handle login, security, and user management for *their own* products. Their end-users log in through branded pages powered by this platform.

**The builder is a solo developer.** Every decision in this document favors simplicity, minimal moving parts, one language/framework, and shipping over enterprise complexity.

### Non-Negotiable Principles
1. **Standards-based** — implement OAuth 2.0 + OpenID Connect via OpenIddict. Never invent a custom auth protocol.
2. **Multi-tenant isolation** — a tenant must NEVER access data outside its own tenant. Enforced at query level + PostgreSQL Row Level Security.
3. **Sensitive data protected** — passwords (Argon2id), secrets/tokens (hashed or AES-256 encrypted). Unreadable even to the platform owner.
4. **Modular monolith** — one deployable app with clean internal module boundaries. Not microservices.
5. **Everything runs in Docker** — local dev and production are the same Docker Compose stack.

---

## 1. Technology Stack (Locked)

| Layer | Technology | Notes |
|---|---|---|
| Runtime / Framework | **ASP.NET Core (latest LTS)** | One framework for everything |
| UI | **ASP.NET Core MVC + Razor** | Server-rendered; all panels + login + portal |
| OAuth/OIDC engine | **OpenIddict** | Certified standards-compliant server |
| Identity foundation | **ASP.NET Core Identity** | User store base, password hashing hooks |
| Database | **PostgreSQL** | Primary data store |
| ORM | **Entity Framework Core (Npgsql)** | Code-first migrations |
| Cache / sessions / rate limits | **Redis** | Fast, distributed |
| Background jobs | **Hangfire** | In-process, stores jobs in PostgreSQL, built-in dashboard, auto-retries |
| Password hashing | **Argon2id** | Override Identity's default hasher |
| MFA (TOTP) | **Otp.NET** (or equivalent) | Build our own TOTP, RFC 6238 |
| JWT signing | **RS256 / ES256** | Asymmetric — clients verify with public key |
| Email (BYOK) | Zepto / SMTP / SendGrid / SES | Pluggable provider interface |
| WhatsApp (BYOK) | MSG91 / Gupshup / Meta | Pluggable provider interface |
| Light client-side JS | Alpine.js or vanilla | Only where interactivity is needed |
| Containerization | **Docker + Docker Compose** | 3 containers: app, PostgreSQL, Redis |

**Do not add** other frameworks, message brokers, or a separate frontend SPA. Keep the surface small.

---

## 2. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              Docker Compose Stack (local + server)          │
│                                                             │
│  ┌───────────────────────────────────────────────────┐    │
│  │   Saarvix.Identity  (ASP.NET Core MVC monolith)   │    │
│  │                                                   │    │
│  │   ┌─────────────────────────────────────────┐   │    │
│  │   │  PRESENTATION (MVC + Razor)             │   │    │
│  │   │  • Hosted Login UI                      │   │    │
│  │   │  • Super Admin Panel                    │   │    │
│  │   │  • Tenant Admin Panel                   │   │    │
│  │   │  • End-User Portal                      │   │    │
│  │   │  • Management REST API (controllers)    │   │    │
│  │   │  • OAuth/OIDC endpoints (OpenIddict)    │   │    │
│  │   └─────────────────────────────────────────┘   │    │
│  │                      │                          │    │
│  │   ┌─────────────────────────────────────────┐   │    │
│  │   │  MODULES (internal boundaries)          │   │    │
│  │   │  Auth · Users · Tenants · Tokens ·      │   │    │
│  │   │  Authorization · MFA · Messaging ·      │   │    │
│  │   │  Webhooks · Hooks · Audit · Sync        │   │    │
│  │   └─────────────────────────────────────────┘   │    │
│  │                      │                          │    │
│  │   ┌─────────────────────────────────────────┐   │    │
│  │   │  INFRASTRUCTURE                         │   │    │
│  │   │  EF Core · Redis · Hangfire · Providers │   │    │
│  │   └─────────────────────────────────────────┘   │    │
│  └───────────────────────────────────────────────────┘    │
│              │                          │                   │
│       ┌──────▼──────┐           ┌──────▼──────┐           │
│       │ PostgreSQL  │           │   Redis     │           │
│       │ (data +     │           │ (cache,     │           │
│       │  Hangfire)  │           │  sessions)  │           │
│       └─────────────┘           └─────────────┘           │
└─────────────────────────────────────────────────────────────┘
```

### Modules (Internal Boundaries Within the Monolith)

Each module is a folder/namespace with its own services, not a separate deployable. They communicate via in-process service interfaces.

| Module | Responsibility |
|---|---|
| **Tenants** | Organizations, settings, branding, isolation, onboarding/offboarding |
| **Auth** | Login flows, sessions, social login, magic link, password reset |
| **Users** | User lifecycle, profile, metadata, invitations, impersonation |
| **Tokens** | Token issuance/verification, refresh rotation, claims assembly, client/secret, API keys |
| **Authorization** | Roles, permissions, role-permission mapping, policy checks |
| **MFA** | TOTP, email/WhatsApp OTP, passkeys, backup codes |
| **Messaging** | Email + WhatsApp providers (managed + BYOK), template engine |
| **Webhooks** | Event dispatch, signing, retry, delivery log |
| **Hooks** | Pipeline hooks (pre/post registration, login, token issuance, send-OTP) |
| **Audit** | Immutable audit log, login history, security events |
| **Sync** | Self-hosted instance telemetry (push to cloud) |

---

## 3. Solution / Folder Structure

A clean layout for a solo dev. One solution, a few projects.

```
SaarvixIdentity.sln
│
├── src/
│   ├── Saarvix.Identity.Web/            ← MVC app (entry point, controllers, Razor views, API)
│   │   ├── Areas/
│   │   │   ├── SuperAdmin/              ← Super admin panel (controllers + views)
│   │   │   ├── Admin/                   ← Tenant admin panel
│   │   │   ├── Account/                 ← Hosted login, register, reset, MFA
│   │   │   └── Portal/                  ← End-user self-service portal
│   │   ├── Controllers/Api/             ← Management REST API (v1)
│   │   ├── OAuth/                       ← OpenIddict endpoints (authorize, token, userinfo, etc.)
│   │   ├── Views/
│   │   ├── wwwroot/                     ← CSS, JS (Alpine), images
│   │   └── Program.cs
│   │
│   ├── Saarvix.Identity.Core/           ← Domain: entities, interfaces, enums, value objects
│   │   ├── Entities/
│   │   ├── Interfaces/
│   │   └── Enums/
│   │
│   ├── Saarvix.Identity.Modules/        ← Business logic per module
│   │   ├── Tenants/
│   │   ├── Auth/
│   │   ├── Users/
│   │   ├── Tokens/
│   │   ├── Authorization/
│   │   ├── Mfa/
│   │   ├── Messaging/
│   │   ├── Webhooks/
│   │   ├── Hooks/
│   │   ├── Audit/
│   │   └── Sync/
│   │
│   └── Saarvix.Identity.Infrastructure/ ← EF Core, Redis, Hangfire, provider implementations
│       ├── Data/                        ← DbContext, configurations, migrations
│       ├── Caching/
│       ├── Jobs/                        ← Hangfire job definitions
│       ├── Email/                       ← Email provider implementations
│       └── Whatsapp/                    ← WhatsApp provider implementations
│
├── tests/
│   └── Saarvix.Identity.Tests/
│
├── docker-compose.yml
├── Dockerfile
└── README.md
```

**Dependency direction:** Web → Modules → Core ← Infrastructure → Core. Core depends on nothing. This keeps it clean and testable.

---

## 4. Complete Database Schema

PostgreSQL. All tables use UUID primary keys (`gen_random_uuid()`). All tenant-scoped tables have `tenant_id` with Row Level Security enabled. Timestamps are `TIMESTAMPTZ`.

### 4.1 Tenants & Organizations

```sql
CREATE TABLE tenants (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug            TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'active',     -- active|suspended|deleted
    parent_id       UUID REFERENCES tenants(id),        -- for sub-organizations
    settings        JSONB NOT NULL DEFAULT '{}',        -- feature flags, policies
    branding        JSONB NOT NULL DEFAULT '{}',        -- logo_url, colors, fonts, layout
    custom_domain   TEXT,                               -- auth.theircompany.com
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_tenants_slug ON tenants(slug);
CREATE INDEX idx_tenants_parent ON tenants(parent_id);
```

### 4.2 Users

```sql
CREATE TABLE users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email           TEXT NOT NULL,
    email_verified  BOOLEAN NOT NULL DEFAULT FALSE,
    username        TEXT,
    phone           TEXT,
    phone_verified  BOOLEAN NOT NULL DEFAULT FALSE,
    password_hash   TEXT,                               -- Argon2id; NULL if social-only
    status          TEXT NOT NULL DEFAULT 'active',     -- active|suspended|pending|deleted
    is_anonymous    BOOLEAN NOT NULL DEFAULT FALSE,     -- guest auth
    first_name      TEXT,
    last_name       TEXT,
    avatar_url      TEXT,
    timezone        TEXT DEFAULT 'UTC',
    locale          TEXT DEFAULT 'en',
    user_metadata   JSONB NOT NULL DEFAULT '{}',        -- user CAN edit
    app_metadata    JSONB NOT NULL DEFAULT '{}',        -- only backend CAN edit (plan, etc.)
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at   TIMESTAMPTZ,
    UNIQUE (tenant_id, email)                           -- email unique PER tenant
);
CREATE INDEX idx_users_tenant ON users(tenant_id);
CREATE INDEX idx_users_email ON users(tenant_id, email);
```

### 4.3 Social Identities

```sql
CREATE TABLE social_identities (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    provider        TEXT NOT NULL,                      -- google|facebook|instagram|github|...
    provider_id     TEXT NOT NULL,
    provider_email  TEXT,
    access_token    TEXT,                               -- AES-256 encrypted
    refresh_token   TEXT,                               -- AES-256 encrypted
    expires_at      TIMESTAMPTZ,
    raw_profile     JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (provider, provider_id)
);
CREATE INDEX idx_social_user ON social_identities(user_id);
```

### 4.4 MFA Factors

```sql
CREATE TABLE mfa_factors (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    type            TEXT NOT NULL,                      -- totp|email_otp|whatsapp_otp|passkey
    secret          TEXT,                               -- AES-256 encrypted (TOTP); passkey cred for webauthn
    status          TEXT NOT NULL DEFAULT 'pending',    -- pending|active|revoked
    friendly_name   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_used_at    TIMESTAMPTZ
);
CREATE INDEX idx_mfa_user ON mfa_factors(user_id);

CREATE TABLE mfa_backup_codes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    code_hash       TEXT NOT NULL,                      -- hashed
    used            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.5 Applications (Client ID / Secret) — managed via OpenIddict but extended

```sql
CREATE TABLE applications (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    client_id       TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    type            TEXT NOT NULL,                      -- web|spa|native|machine
    grant_types     TEXT[] NOT NULL,
    redirect_uris   TEXT[] NOT NULL DEFAULT '{}',
    allowed_scopes  TEXT[] NOT NULL DEFAULT '{}',
    token_lifetime  INT NOT NULL DEFAULT 3600,
    is_first_party  BOOLEAN NOT NULL DEFAULT FALSE,
    settings        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_apps_tenant ON applications(tenant_id);

CREATE TABLE application_secrets (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    application_id  UUID NOT NULL REFERENCES applications(id) ON DELETE CASCADE,
    secret_hash     TEXT NOT NULL,                      -- bcrypt/argon2 hash, shown once
    label           TEXT,
    expires_at      TIMESTAMPTZ,
    revoked         BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE api_keys (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id         UUID REFERENCES users(id) ON DELETE CASCADE,  -- NULL = tenant-level key
    key_hash        TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    scopes          TEXT[] NOT NULL DEFAULT '{}',
    expires_at      TIMESTAMPTZ,
    revoked         BOOLEAN NOT NULL DEFAULT FALSE,
    last_used_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.6 Roles & Permissions

```sql
CREATE TABLE roles (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    description     TEXT,
    is_system       BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, name)
);

CREATE TABLE permissions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    resource        TEXT NOT NULL,                      -- user|project|invoice...
    action          TEXT NOT NULL,                      -- read|write|delete...
    description     TEXT,
    UNIQUE (tenant_id, resource, action)
);

CREATE TABLE role_permissions (
    role_id         UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id   UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_roles (
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id         UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    granted_by      UUID REFERENCES users(id),
    granted_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, role_id)
);
```

### 4.7 Sessions

```sql
CREATE TABLE sessions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    tenant_id           UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    application_id      UUID REFERENCES applications(id),
    refresh_token_hash  TEXT NOT NULL UNIQUE,           -- SHA-256
    refresh_family_id   UUID NOT NULL,                  -- for rotation reuse detection
    ip_address          INET,
    user_agent          TEXT,
    device_fingerprint  TEXT,
    location            TEXT,
    trusted             BOOLEAN NOT NULL DEFAULT FALSE,
    last_active_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ NOT NULL,
    revoked             BOOLEAN NOT NULL DEFAULT FALSE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_sessions_user ON sessions(user_id, tenant_id);
CREATE INDEX idx_sessions_family ON sessions(refresh_family_id);
```

### 4.8 Login History & Audit

```sql
CREATE TABLE login_history (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID REFERENCES users(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    result          TEXT NOT NULL,                      -- success|failed|blocked|mfa_required
    method          TEXT,                               -- password|google|whatsapp_otp|...
    ip_address      INET,
    user_agent      TEXT,
    device          TEXT,
    location        TEXT,
    reason          TEXT,                               -- why failed/blocked
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_login_user ON login_history(user_id, created_at DESC);
CREATE INDEX idx_login_tenant ON login_history(tenant_id, created_at DESC);

CREATE TABLE audit_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID REFERENCES tenants(id),
    actor_id        UUID,
    actor_type      TEXT,                               -- user|service|system|super_admin
    event           TEXT NOT NULL,                      -- user.suspended|role.assigned|...
    resource_type   TEXT,
    resource_id     UUID,
    ip_address      INET,
    user_agent      TEXT,
    result          TEXT NOT NULL,                      -- success|failure
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_audit_tenant ON audit_logs(tenant_id, created_at DESC);
CREATE INDEX idx_audit_actor ON audit_logs(actor_id, created_at DESC);
-- audit_logs is append-only: no UPDATE or DELETE permitted in app code
```

### 4.9 Tokens & Verification

```sql
CREATE TABLE password_reset_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash      TEXT NOT NULL UNIQUE,
    expires_at      TIMESTAMPTZ NOT NULL,
    used            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE verification_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    type            TEXT NOT NULL,                      -- email|phone|magic_link
    target          TEXT NOT NULL,                      -- the email/phone being verified
    token_hash      TEXT NOT NULL UNIQUE,
    expires_at      TIMESTAMPTZ NOT NULL,
    used            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE otp_codes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID REFERENCES users(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel         TEXT NOT NULL,                      -- email|whatsapp
    code_hash       TEXT NOT NULL,
    attempts        INT NOT NULL DEFAULT 0,
    expires_at      TIMESTAMPTZ NOT NULL,
    used            BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.10 Invitations

```sql
CREATE TABLE invitations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email           TEXT NOT NULL,
    role_id         UUID REFERENCES roles(id),
    invited_by      UUID REFERENCES users(id),
    token_hash      TEXT NOT NULL UNIQUE,
    status          TEXT NOT NULL DEFAULT 'pending',    -- pending|accepted|revoked|expired
    expires_at      TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.11 Messaging — Provider Config & Templates

```sql
CREATE TABLE messaging_providers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel         TEXT NOT NULL,                      -- email|whatsapp
    provider        TEXT NOT NULL,                      -- zepto|smtp|sendgrid|msg91|gupshup|...
    mode            TEXT NOT NULL DEFAULT 'byok',       -- managed|byok
    config          JSONB NOT NULL DEFAULT '{}',        -- api_key (encrypted), sender, host...
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE message_templates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    key             TEXT NOT NULL,                      -- verify_email|reset_password|otp|...
    channel         TEXT NOT NULL,                      -- email|whatsapp
    locale          TEXT NOT NULL DEFAULT 'en',
    subject         TEXT,                               -- email only
    body            TEXT NOT NULL,                      -- with {{variables}}
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, key, channel, locale)
);

CREATE TABLE message_log (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    channel         TEXT NOT NULL,
    recipient       TEXT NOT NULL,
    template_key    TEXT,
    status          TEXT NOT NULL,                      -- queued|sent|failed
    error           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.12 Webhooks & Hooks

```sql
CREATE TABLE webhook_endpoints (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    url             TEXT NOT NULL,
    events          TEXT[] NOT NULL DEFAULT '{}',       -- which events route here
    secret          TEXT NOT NULL,                      -- HMAC secret (encrypted)
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE webhook_deliveries (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    endpoint_id     UUID NOT NULL REFERENCES webhook_endpoints(id) ON DELETE CASCADE,
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    event           TEXT NOT NULL,
    payload         JSONB NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',    -- pending|success|failed
    attempts        INT NOT NULL DEFAULT 0,
    response_code   INT,
    next_retry_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE pipeline_hooks (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    stage           TEXT NOT NULL,                      -- pre_registration|post_login|pre_token|send_otp|...
    url             TEXT NOT NULL,
    secret          TEXT NOT NULL,                      -- HMAC (encrypted)
    timeout_ms      INT NOT NULL DEFAULT 3000,
    on_failure      TEXT NOT NULL DEFAULT 'continue',   -- continue|block
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.13 Custom Token Claims Config

```sql
CREATE TABLE claim_configs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    application_id  UUID REFERENCES applications(id),   -- NULL = tenant-wide
    token_type      TEXT NOT NULL,                      -- id|access
    type            TEXT NOT NULL,                      -- static|metadata|webhook
    claim_name      TEXT NOT NULL,
    source          TEXT,                               -- static value | metadata path | webhook url
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.14 Self-Hosted Sync (lives on YOUR cloud only)

```sql
CREATE TABLE self_hosted_instances (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_tenant_id UUID REFERENCES tenants(id),        -- who registered it on cloud
    sync_key_hash   TEXT NOT NULL UNIQUE,
    version         TEXT,
    last_seen_at    TIMESTAMPTZ,
    -- aggregate metrics ONLY — never PII
    user_count      INT DEFAULT 0,
    app_count       INT DEFAULT 0,
    tenant_count    INT DEFAULT 0,
    health          JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### 4.15 Super Admins (platform-level, not tenant-scoped)

```sql
CREATE TABLE super_admins (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email           TEXT NOT NULL UNIQUE,
    password_hash   TEXT NOT NULL,                      -- Argon2id
    role            TEXT NOT NULL DEFAULT 'operator',   -- owner|operator|support|security
    mfa_enabled     BOOLEAN NOT NULL DEFAULT TRUE,      -- mandatory for super admins
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at   TIMESTAMPTZ
);
```

### 4.16 Row Level Security (apply to every tenant-scoped table)

```sql
-- Example for users; repeat pattern for all tenant-scoped tables
ALTER TABLE users ENABLE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON users
    USING (tenant_id = current_setting('app.current_tenant')::uuid);
-- The app sets app.current_tenant per request/connection.
-- This is a backstop; the app ALSO filters by tenant_id in every query.
```

---

## 5. OAuth / OIDC Flow Specifications

OpenIddict provides the endpoints; configure these flows.

### 5.1 Standard Endpoints (OpenIddict)
```
GET  /.well-known/openid-configuration   ← discovery
GET  /.well-known/jwks.json              ← public keys
GET  /connect/authorize                  ← start auth code flow
POST /connect/token                      ← token exchange (all grant types)
GET  /connect/userinfo                   ← OIDC userinfo
POST /connect/introspect                 ← token introspection
POST /connect/revoke                     ← revoke token
POST /connect/logout                     ← end session
```

### 5.2 Authorization Code + PKCE (web/SPA/mobile login)
```
1. App → GET /connect/authorize
        ?response_type=code
        &client_id=...
        &redirect_uri=...
        &scope=openid profile email offline_access
        &state=...
        &code_challenge=... &code_challenge_method=S256
2. Platform → show branded login page → authenticate → MFA if required
3. Platform → redirect to redirect_uri?code=...&state=...
4. App backend → POST /connect/token
        grant_type=authorization_code
        &code=... &redirect_uri=... &code_verifier=...
        &client_id=... &client_secret=... (confidential only)
5. Platform → { access_token, id_token, refresh_token, expires_in }
```

### 5.3 Client Credentials (app-to-app, service as itself)
```
POST /connect/token
  grant_type=client_credentials
  &client_id=... &client_secret=...
  &scope=users:read users:write
→ { access_token, expires_in }   (no refresh, no id_token)
```

### 5.4 Refresh Token (with rotation)
```
POST /connect/token
  grant_type=refresh_token
  &refresh_token=... &client_id=...
→ { access_token, refresh_token (new), expires_in }
Rules: old refresh token invalidated; if a consumed token is reused,
revoke the entire refresh_family_id (theft detected).
```

### 5.5 On-Behalf-Of / Token Exchange (RFC 8693)
```
POST /connect/token
  grant_type=urn:ietf:params:oauth:grant-type:token-exchange
  &subject_token=<user's access token>
  &subject_token_type=urn:ietf:params:oauth:token-type:access_token
  &client_id=... &client_secret=...
  &scope=...
→ new access_token AS the user, for calling another service
```

### 5.6 Token Claim Assembly Order
When issuing a token, assemble claims in this order:
```
1. Standard claims (sub, email, tenant_id, roles, permissions, iss, exp, iat, aud)
2. Static custom claims (from claim_configs)
3. User metadata claims (mapped from user_metadata/app_metadata)
4. Webhook claims (call pipeline_hooks stage=pre_token; merge response)
   - respect timeout_ms; on failure follow on_failure (continue = skip claims)
5. Sign with current active key (RS256/ES256)
```

---

## 6. Management REST API (v1)

Base: `/api/v1`. Auth: Bearer access token (client credentials) or `X-API-Key`. All requests tenant-scoped from the token.

### Users
```
GET    /api/v1/users                 list (paginated, filterable)
POST   /api/v1/users                 create
GET    /api/v1/users/{id}            get
PATCH  /api/v1/users/{id}            update
DELETE /api/v1/users/{id}            delete
POST   /api/v1/users/{id}/suspend
POST   /api/v1/users/{id}/reactivate
POST   /api/v1/users/{id}/force-password-reset
GET    /api/v1/users/{id}/roles
POST   /api/v1/users/{id}/roles
DELETE /api/v1/users/{id}/roles/{roleId}
GET    /api/v1/users/{id}/sessions
DELETE /api/v1/users/{id}/sessions   revoke all
```

### Roles & Permissions
```
GET/POST          /api/v1/roles
GET/PATCH/DELETE  /api/v1/roles/{id}
GET/POST/DELETE   /api/v1/roles/{id}/permissions
GET               /api/v1/permissions
```

### Applications
```
GET/POST          /api/v1/applications
GET/PATCH/DELETE  /api/v1/applications/{id}
POST              /api/v1/applications/{id}/secrets/rotate
```

### Invitations
```
GET/POST          /api/v1/invitations
POST              /api/v1/invitations/bulk
DELETE            /api/v1/invitations/{id}
```

### Audit & Webhooks
```
GET               /api/v1/audit-logs        (filterable)
GET/POST          /api/v1/webhooks
GET/PATCH/DELETE  /api/v1/webhooks/{id}
POST              /api/v1/webhooks/{id}/test
GET               /api/v1/webhooks/{id}/deliveries
```

### Standard API conventions
- JSON request/response, `application/json`
- Pagination: `?page=1&limit=50`, return `{ data, total, page, limit }`
- Errors: `{ error: { code, message } }` with proper HTTP status
- Never break v1 once released; additive changes only. Breaking → `/api/v2`.

---

## 7. Background Jobs (Hangfire)

Hangfire runs in-process, stores jobs in PostgreSQL, dashboard at `/hangfire` (super-admin protected).

| Job | Type | Purpose |
|---|---|---|
| Send email | Fire-and-forget | Deliver via tenant's email provider |
| Send WhatsApp OTP | Fire-and-forget | Deliver via tenant's WhatsApp provider |
| Dispatch webhook | Fire-and-forget + retry | POST event, HMAC-signed |
| Retry failed webhook | Scheduled | Exponential backoff: 1m, 5m, 30m, 2h, 24h |
| Expire tokens/OTPs | Recurring (hourly) | Clean up expired rows |
| Purge old login history | Recurring (daily) | Honor retention policy |
| Self-host sync receive | Triggered by API | Update aggregate metrics |
| Suspicious login analysis | Fire-and-forget | After login, flag anomalies |

**Rule:** anything that talks to an external service or can be slow runs as a Hangfire job, never inline in the request.

---

## 8. Security Implementation Map

| Control | Where it lives |
|---|---|
| Argon2id password hashing | Custom `IPasswordHasher` in Infrastructure |
| AES-256 encryption (secrets/TOTP/social tokens) | Encryption service in Infrastructure; key from env/vault |
| JWT signing keys | OpenIddict key management; rotate via super admin |
| Refresh rotation + reuse detection | Tokens module — check `refresh_family_id` |
| Rate limiting | Middleware backed by Redis counters |
| Account lockout | Auth module — track failed attempts, exponential backoff |
| Bot/CAPTCHA | Login page — hCaptcha/Turnstile, configurable per tenant |
| Tenant isolation | (1) every query filters tenant_id (2) Postgres RLS backstop |
| PKCE enforcement | OpenIddict config — required for public clients |
| Audit logging | Audit module — called from all state-changing operations |
| Breached password check | Auth module — HaveIBeenPwned k-anonymity API |
| Secrets in env/vault | Never in appsettings committed to git |
| TLS | Reverse proxy (nginx/Caddy) in front of the app |

**Super admin panel hardening:** separate area, mandatory MFA, optional IP allowlist, separate auth from tenant users.

---

## 9. Self-Hosted Sync Design

```
SELF-HOSTED INSTANCE (their server)              YOUR CLOUD
│                                                │
│  Hangfire recurring job (e.g. every 6h):       │
│    POST https://cloud.saarvix.com/api/sync     │
│    Header: X-Sync-Key: <their key>             │
│    Body: { version, user_count, app_count,     │
│            tenant_count, health }   ← NO PII   │
│                                  ───────────►  │  validate key →
│                                                │  update self_hosted_instances
│  If POST fails: log, retry later.              │
│  Instance keeps running normally regardless.   │
└────────────────────────────────────────────────┘
```

**Hard rules:**
1. Sync payload contains aggregate metrics only — never user records, emails, tokens, secrets, or DB contents.
2. Sync failure must NEVER block authentication. It's best-effort background.
3. Sync key validated occasionally; instance works offline 30+ days. Never a per-request check.
4. Disclosed and consented at signup + Docker setup; log acceptance.

---

## 10. Configuration (Environment Variables)

```
# Database
DATABASE_URL=Host=postgres;Database=saarvix;Username=...;Password=...

# Redis
REDIS_URL=redis:6379

# Encryption (generate strong random keys; store in env/vault)
ENCRYPTION_KEY=<32-byte base64>
SIGNING_KEY_PATH=/keys/signing.pfx   (or managed by OpenIddict)

# Deployment mode
DEPLOYMENT_MODE=cloud                 (cloud|self_hosted)
SYNC_ENDPOINT=https://cloud.saarvix.com/api/sync   (self_hosted only)
SYNC_KEY=<provided at signup>          (self_hosted only)

# Super admin bootstrap (first run)
SUPERADMIN_EMAIL=...
SUPERADMIN_PASSWORD=...                (force change on first login)
```

---

## 11. Docker Compose (Local + Server)

```yaml
version: "3.9"
services:
  app:
    build: .
    ports:
      - "8080:8080"
    environment:
      - DATABASE_URL=Host=postgres;Database=saarvix;Username=saarvix;Password=saarvix
      - REDIS_URL=redis:6379
      - DEPLOYMENT_MODE=cloud
    depends_on:
      - postgres
      - redis

  postgres:
    image: postgres:16
    environment:
      - POSTGRES_USER=saarvix
      - POSTGRES_PASSWORD=saarvix
      - POSTGRES_DB=saarvix
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  redis:
    image: redis:7
    ports:
      - "6379:6379"

volumes:
  pgdata:
```

Run locally: `docker compose up`. Same file deploys to your server. Put nginx/Caddy in front for TLS in production.

---

## 12. Coding Conventions for This Project

- **Async all the way** — every DB/IO call is `async`/`await`.
- **Every tenant-scoped query filters `tenant_id`** — never rely on RLS alone.
- **Never log secrets, passwords, tokens, OTPs** — redact.
- **All state changes write an audit log entry.**
- **External calls (email, WhatsApp, webhooks, hooks) go through Hangfire**, never inline.
- **Validate all input** at the controller boundary.
- **One module never reaches into another module's data directly** — go through its service interface.
- **EF Core migrations** for all schema changes — never hand-edit the DB.

---

## 13. Build Order (Follow This Sequence)

Build in this order. Each step produces something testable before moving on.

**Step 1 — Foundation**
Solution + projects + Docker Compose. EF Core + PostgreSQL connection. Redis connection. Hangfire setup (dashboard at `/hangfire`). Base entities + first migration (tenants, users). Argon2id password hasher. AES encryption service.

**Step 2 — Tenancy & Super Admin**
Tenant entity + CRUD. Super admin bootstrap + login + super admin panel shell. Tenant isolation middleware (sets `app.current_tenant`) + RLS policies.

**Step 3 — Core Auth (no OAuth yet)**
User registration + email/password login (server-rendered pages). Email verification + password reset (queued emails — stub provider first). Sessions. Login history + audit logging.

**Step 4 — OAuth/OIDC Engine**
Integrate OpenIddict. Applications (client id/secret) + tenant admin UI to create them. Authorization Code + PKCE flow. Refresh token flow + rotation + reuse detection. Client Credentials flow. JWKS + discovery endpoints. Token claim assembly (standard claims first).

**Step 5 — Authorization**
Roles, permissions, mappings. Assign roles to users. Inject roles/permissions into tokens. Permission checks in Management API.

**Step 6 — Management API**
REST endpoints for users, roles, applications, invitations, audit, webhooks. API key auth + client credentials auth.

**Step 7 — MFA**
Own TOTP (enroll + QR + verify). Backup codes. Email OTP. Wire MFA into the login flow + per-tenant/role policy.

**Step 8 — Messaging & Templates**
Provider interface (email + WhatsApp). BYOK config UI. Template engine + built-in templates + variables. WhatsApp OTP. Real email provider (Zepto/SMTP). All sending via Hangfire.

**Step 9 — Social Login**
Google first, then Facebook/GitHub/etc. Account linking. Per-tenant provider config.

**Step 10 — Webhooks & Hooks**
Event system + 40+ events. Webhook endpoints + HMAC signing + Hangfire dispatch + retry + delivery log. Pipeline hooks (pre/post stages). Custom token claims (static + metadata + webhook).

**Step 11 — Branding & Login UI**
Visual branding config (logo, colors, layout). Apply branding to hosted login page. Custom domain support. End-user portal (profile, MFA, sessions, login history).

**Step 12 — Advanced Auth**
Passkeys/WebAuthn. Magic link. Account recovery flows. Email/phone change (secure flow).

**Step 13 — Security Hardening**
Rate limiting, account lockout, bot/CAPTCHA, breached password check, suspicious login detection, block/allow lists.

**Step 14 — Self-Host & Compliance**
Docker image as self-host artifact. Sync (push from instance, aggregate-only). GDPR/DPDP: data export, erasure, consent tracking, retention jobs.

**Step 15 — Polish**
Onboarding wizard. Sandbox environment. Super admin monitoring + health. Documentation.

---

## 14. Deferred (Do Not Build Now)

SDKs (Next.js/React, Node, .NET) — after the API is stable. SAML, SCIM, enterprise IdP federation — Phase 3. Risk-based/conditional access, ABAC — Phase 2. Anonymous auth, impersonation, migration tools — Phase 2. Multi-region — later. DPoP/mTLS, workload identity federation — Phase 3.

---

## 15. Open Build-Time Decisions (Decide When You Reach Them)

1. Hosted login vs embeddable widget — start with **hosted** (simpler); add embeddable later.
2. WhatsApp — start with **a provider (MSG91/Gupshup)** rather than direct Meta API (faster, they handle compliance).
3. Open-source license — pick before public release (MIT is simplest).
4. Embeddable dashboard for tenants' customers — later.

---

*End of handoff document. Begin at Section 13, Step 1. Every decision needed to start is contained above. When something is ambiguous during implementation, prefer the simplest option that preserves tenant isolation and credential protection.*
