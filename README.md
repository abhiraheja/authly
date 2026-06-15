# Authly

A free, open-source, **multi-tenant Identity-as-a-Service (IDaaS)** platform — an alternative to
Auth0, Microsoft Entra, Google Identity, and Supabase Auth, with differentiators they lack:
WhatsApp OTP, BYOK messaging/email, white-label login, and cloud **or** self-host from one codebase.

> Product spec & roadmap live in [`docs/`](docs/). Build follows
> [`docs/Saarvix-Identity-Development-Plan.md`](docs/Saarvix-Identity-Development-Plan.md).

## Tech stack

ASP.NET Core 10 **MVC + Razor** · OpenIddict (OAuth2/OIDC) · PostgreSQL + EF Core · Redis ·
Hangfire · Argon2id · AES-256-GCM · Docker. Modular monolith.

```
src/Authly.Web/             MVC app — panels, hosted login, end-user portal, REST API, OAuth endpoints
src/Authly.Core/            Domain — entities, enums, interfaces (depends on nothing)
src/Authly.Modules/         Business logic per module (Tenants, Auth, Users, …)
src/Authly.Infrastructure/  EF Core, Redis, Hangfire, security primitives, providers
tests/Authly.Tests/         Unit tests
```

## Running with Docker (recommended)

**Prerequisite — install one thing:** [Docker Desktop](https://www.docker.com/products/docker-desktop/)
(includes Docker Engine + Compose). PostgreSQL, Redis, and the .NET runtime are pulled/built
automatically — you do **not** install them yourself.

```bash
# from the repo root
docker compose up --build           # foreground (Ctrl+C to stop)
docker compose up --build -d        # background
docker compose logs -f app          # tail app logs
docker compose down                 # stop (keeps the database volume)
docker compose down -v              # stop and WIPE the database
```

On startup the app **auto-applies EF Core migrations**, creating the schema.

| Check | URL / command |
|---|---|
| App | http://localhost:8080 |
| Hangfire dashboard | http://localhost:8080/hangfire |
| Postgres | `docker compose exec postgres psql -U authly -d authly -c "\dt"` |
| Redis | `docker compose exec redis redis-cli ping` → `PONG` |

### Configuration (env vars, set in `docker-compose.yml` for dev)

| Variable | Dev value | Purpose |
|---|---|---|
| `DATABASE_URL` | `Host=postgres;Database=authly;Username=authly;Password=authly` | EF Core + Hangfire |
| `REDIS_URL` | `redis:6379` | cache / sessions / rate limits |
| `DEPLOYMENT_MODE` | `cloud` | `cloud` or `self_hosted` |
| `ENCRYPTION_KEY` | (32-byte base64 dev key) | AES-256 secret encryption — **replace in production** |
| `SUPERADMIN_EMAIL` / `SUPERADMIN_PASSWORD` | `admin@authly.local` / `ChangeMe!123` | super-admin bootstrap (forced change on first login) |

> **Production:** generate a fresh `ENCRYPTION_KEY`, supply DB credentials via secrets (not the
> compose file), and terminate TLS with nginx/Caddy in front of the app.

## Running locally without Docker

Requires the **.NET 10 SDK**, plus a local PostgreSQL and Redis (or point `DATABASE_URL` /
`REDIS_URL` at running instances).

```bash
dotnet build Authly.slnx
dotnet test  Authly.slnx
dotnet run --project src/Authly.Web
```

## License

TBD (chosen before public release — see build-time decisions in the plan).
