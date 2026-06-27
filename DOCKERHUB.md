# Authly — Open-source Identity-as-a-Service (IDaaS)

A free, open-source, **multi-tenant identity platform** you can self-host in minutes — a drop-in
alternative to **Auth0, Microsoft Entra, Google Identity, and Supabase Auth**, with differentiators
they lack: **WhatsApp OTP, BYOK messaging/email, white-label login, and cloud or self-host from one codebase.**

## Features
- 🔑 **OAuth2 / OIDC** server (OpenIddict): Auth Code + PKCE, refresh-token rotation, client credentials, discovery/JWKS
- 🛡️ **MFA & passwordless**: TOTP, passkeys (WebAuthn), magic links, WhatsApp/email OTP
- 🌐 **Social & enterprise login**: Google, Microsoft, GitHub, Facebook + generic OAuth2/OIDC
- 🏢 **Multi-tenant** with per-org RBAC, conditional access, impersonation, device management, audit logs
- 🎨 **White-label hosted login** — fully brandable (logo, colors, layouts, backgrounds, copy)
- 📈 **Pluggable observability** (OpenTelemetry → OTLP / Grafana / Azure Monitor)
- 🔒 Argon2id password hashing, AES-256-GCM encryption at rest, PostgreSQL row-level security

## Quick start

Authly needs **PostgreSQL** and **Redis** alongside it, so the supported way to run is Docker Compose.
Save the file below as `docker-compose.yml`, then run it — Postgres and Redis are included.

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: authly
      POSTGRES_PASSWORD: authly
      POSTGRES_DB: authly
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U authly"]
      interval: 5s
      timeout: 5s
      retries: 10

  redis:
    image: redis:7

  authly:
    image: abhiraheja/authly:latest
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_started
    environment:
      # Postgres connection string (Npgsql format) — points at the bundled service below
      - DATABASE_URL=Host=postgres;Database=authly;Username=authly;Password=authly
      # Redis host:port — points at the bundled service below
      - REDIS_URL=redis:6379
      # REQUIRED. 32-byte base64 key for AES-256-GCM secret encryption.
      # Generate one with:  openssl rand -base64 32
      - ENCRYPTION_KEY=CHANGE_ME_openssl_rand_base64_32
      # Serve the public marketing website + docs at "/". If left false (the default),
      # "/" redirects to the admin console and only the IdP/login/OIDC routes are served.
      - Website__Enabled=true
    ports:
      - "8080:8080"
    restart: unless-stopped

volumes:
  pgdata:
```

```bash
# 1. Generate an encryption key and paste it into ENCRYPTION_KEY above
openssl rand -base64 32

# 2. Start everything (pulls Postgres, Redis and the Authly image)
docker compose up -d

# 3. Watch the app apply EF Core migrations on first boot
docker compose logs -f authly
```

Then open **http://localhost:8080** and click **Sign up** — the account you create becomes the
first administrator of its workspace. Your OIDC discovery document is at
`http://localhost:8080/.well-known/openid-configuration`.

> **Production:** generate a fresh `ENCRYPTION_KEY`, use strong DB/Redis passwords, and put a TLS
> reverse proxy (Traefik / nginx / Caddy) in front. See the in-product
> [Production deployment guide](https://github.com/abhiraheja/authly).

### Required environment variables

| Variable | Required | What it is |
|---|---|---|
| `ENCRYPTION_KEY` | **Yes** | 32-byte base64 key (AES-256-GCM). App refuses to start without it. `openssl rand -base64 32`. |
| `DATABASE_URL` | Yes | PostgreSQL connection string (Npgsql format), e.g. `Host=postgres;Database=authly;Username=authly;Password=authly`. |
| `REDIS_URL` | Yes | Redis `host:port` (cache/sessions, rate limiting, login lockout — shared across instances). |
| `Website__Enabled` | No | `true` serves the public marketing website + docs; default `false` redirects `/` to the admin console. |
| `CORS_ALLOWED_ORIGINS` | No | Extra trusted browser origins for SPA CORS (app redirect URIs are allowed automatically). |
| `RETENTION_AUDIT_DAYS` / `RETENTION_LOGIN_HISTORY_DAYS` | No | Retention windows (default 365 / 90). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No | OpenTelemetry collector endpoint for traces/metrics/logs. |

## Tech stack
ASP.NET Core 10 (MVC + Razor) · OpenIddict (OAuth2/OIDC) · PostgreSQL + EF Core · Redis · Hangfire · Docker.

**Tags:** identity, authentication, authorization, oauth2, oidc, sso, mfa, passkeys, iam, idaas, self-hosted, auth0-alternative
