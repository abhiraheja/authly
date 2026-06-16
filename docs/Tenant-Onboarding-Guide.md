# Authly — Tenant Onboarding & Usage Guide

How an organization signs up for Authly, provisions its workspace, integrates its apps, and runs
authentication for its own users — start to finish.

This is the **product/operations** view (the journey). For the OIDC integration details a developer
needs, see [developer/Quickstart.md](developer/Quickstart.md). For the verification matrix, see
[Runtime-Acceptance-Checklist.md](Runtime-Acceptance-Checklist.md).

---

## 1. The mental model

Authly is a multi-tenant, standards-based **OpenID Connect / OAuth 2.0** identity provider. One Authly
deployment hosts many independent **tenants**; everything below belongs to exactly one tenant and is
isolated at the database level (Postgres row-level security keyed on the tenant).

| Concept | What it is |
|---------|-----------|
| **Tenant** | An organization / workspace. The unit of isolation. Has a `slug` (e.g. `acme`) and optionally a custom domain. |
| **Tenant admin** | A person who manages the workspace — apps, users, roles, branding, security. The signup creator is the first one. |
| **End user** | A person who logs into the tenant's apps. Lives only inside that tenant. |
| **Application** | An OAuth client (web app, SPA, mobile, or machine). Has a `client_id`; confidential clients also get a `client_secret`. |
| **API key** | A server-to-server credential for the management API. Shown once, stored hashed. |
| **Super admin** | The platform operator (Saarvix), not a customer. Manages tenants and platform health. Gated by `SUPERADMIN_ENABLED` and **absent from customer/self-hosted builds**. |

### The four surfaces

| Surface | Base path | Who uses it | Cookie scheme |
|---------|-----------|-------------|---------------|
| **Signup** | `/signup` | A brand-new prospect (anonymous, tenant-less) | issues the TenantAdmin cookie on success |
| **Tenant Admin** | `/tenantadmin/*` | Tenant admins | TenantAdmin |
| **End-user auth + Portal** | `/account/*`, `/portal/*` | The tenant's end users | User |
| **OIDC endpoints** | `/connect/*`, `/.well-known/*` | The tenant's apps (machines) | tokens, not cookies |
| **Super Admin** | `/superadmin/*` | Platform operator only | SuperAdmin (404s when `SUPERADMIN_ENABLED=false`) |

---

## 2. Step 1 — Self-service signup (`/signup`)

This is the Supabase / Google-Console model: a visitor creates a workspace and becomes its first
administrator in one step. **No invitation, no super-admin involvement.**

1. Visitor opens `/signup` (linked from the home page as **"Create your workspace"**). The page is
   anonymous and deliberately tenant-less.
2. They submit: **Company name, email, password, first/last name.**
3. `TenantSignupService` provisions everything atomically:
   - Creates the **tenant**, deriving a unique slug from the company name (`Acme` → `acme`, with
     de-duplication: `acme` → `acme-2` → `acme-3`…).
   - Binds the new tenant into scope so the RLS-protected user insert is permitted.
   - Registers the **first user**, promotes them to `IsTenantAdmin`, seeds the system roles, and grants
     the `tenant_admin` role.
   - Writes a `tenant.signup` audit row.
4. The new admin is **signed straight into the tenant-admin surface** (TenantAdmin cookie) and redirected
   to the onboarding wizard — no separate login required.

> Source: [SignupController.cs](../src/Authly.Web/Controllers/SignupController.cs) →
> `ITenantSignupService`.

---

## 3. Step 2 — The onboarding wizard (`/tenantadmin/onboarding`)

A brand-new tenant lands here automatically; the dashboard also shows a **"Get started"** nudge until
it's done. Four guided steps, each reusing the same services as the standalone admin pages:

| Step | What happens |
|------|--------------|
| **1. Create your first application** | Name, type (Web / SPA / Native / Machine), redirect URI(s), scopes → produces a `client_id` (+ `client_secret` for confidential clients). |
| **2. Grab credentials** | Client ID + secret shown (**secret shown once**); optionally mint a backend **API key** for the management API. |
| **3. Set branding** | Logo URL + primary color, applied to the hosted login page. |
| **4. Test login + finish** | Pointer to a working test login; **Finish** marks the tenant onboarded and the banner disappears. |

> Source: [OnboardingController.cs](../src/Authly.Web/Areas/TenantAdmin/Controllers/OnboardingController.cs).

---

## 4. Step 3 — Configure the workspace (Tenant Admin console)

Everything under `/tenantadmin/*` is gated by the tenant-admin policy, and a cross-tenant guard ensures
the signed-in admin's tenant matches the tenant resolved for the request. Available areas:

| Area | Path | Purpose |
|------|------|---------|
| **Applications** | `/tenantadmin/applications` | OAuth clients — create/edit, redirect URIs, scopes, rotate secrets. |
| **Users** | `/tenantadmin/users` | Manage end users; impersonate a user for support (audited). |
| **Roles** | `/tenantadmin/roles` | RBAC — roles + permissions; assignments surface as the `roles` claim in tokens. |
| **API Keys** | `/tenantadmin/apikeys` | Server-to-server keys for the management API (raw shown once, stored hashed). |
| **MFA Policy** | `/tenantadmin/mfapolicy` | Require/optional TOTP; enrolment and challenge behavior. |
| **Social Providers** | `/tenantadmin/socialproviders` | Configure Google/etc. login (BYOK client id/secret, stored encrypted). |
| **Messaging** | `/tenantadmin/messaging` | Email/SMS provider (BYOK); secrets encrypted; sends logged to `message_logs`. |
| **Branding** | `/tenantadmin/branding` | Logo, colors, fonts, layout, dark mode, tagline for the hosted login + portal. |
| **Webhooks** | `/tenantadmin/webhooks` | Register endpoints; signed delivery with retries on failure. |
| **Pipeline Hooks** | `/tenantadmin/pipelinehooks` | Custom logic injected into the auth pipeline. |
| **Claim Configs** | `/tenantadmin/claimconfigs` | Add custom claims to issued tokens. |
| **Security** | `/tenantadmin/security` | Lockout, rate limits, breached-password (HIBP), CAPTCHA, block lists. |
| **Access Policies** | `/tenantadmin/accesspolicies` | ABAC / conditional access — risk-based block or step-up MFA. |
| **Sandbox** | `/tenantadmin/sandbox` | Run a test login against a real tenant user; lists discovery endpoints (**no admin cookie issued**). |

---

## 5. Step 4 — Integrate your apps (OIDC)

Authly publishes a standard discovery document — point any OIDC client library at it:

```
GET https://<your-authly-host>/.well-known/openid-configuration
```

| Endpoint | Path |
|----------|------|
| Authorization | `/connect/authorize` |
| Token | `/connect/token` |
| UserInfo | `/connect/userinfo` |
| Introspection | `/connect/introspect` |
| Revocation | `/connect/revoke` |
| End session | `/connect/logout` |

**Supported grants:** Authorization Code + **PKCE** (mandatory, public and confidential), Refresh Token
(rotated — reuse is detected and the token family revoked), Client Credentials. Access tokens live 1
hour; refresh tokens 14 days.

Full request/response walkthroughs, a Next.js example, and a cURL example are in
[developer/Quickstart.md](developer/Quickstart.md).

---

## 6. Step 5 — Your end users

Once an app points at Authly, the tenant's users authenticate on Authly's **hosted pages** under
`/account/*` (branded per the tenant). The full end-user surface:

**Core auth** ([AccountController.cs](../src/Authly.Web/Controllers/AccountController.cs))
- **Register** (`/account/register`) → screened (CAPTCHA, block lists, breached-password) → email
  verification link → consent (terms + privacy) recorded.
- **Login** (`/account/login`) → lockout + block-list + CAPTCHA checks → conditional-access evaluation
  (block / step-up) → MFA gate if required → session cookie + `login_history` row.
- **Forgot/Reset password** — anti-enumeration (same response for known/unknown email); breached-password
  rejected on reset.
- **Magic link** (`/account/magic-link`) — passwordless, one-time-use sign-in link.
- **Account recovery** (`/account/recover-request`) and **secure contact change** (email/phone with
  verification on both old and new).

**MFA & passkeys** — TOTP enrolment/challenge with backup codes; WebAuthn passkeys.

**Social login** — redirect round-trip to a configured provider, linked to a `social_identities` row.

**End-user portal** (`/portal/*`) — the signed-in landing:
- **Profile** — edit personal details.
- **Sessions** — view/revoke active sessions.
- **Security** — manage MFA factors; **Passkeys**; **Devices**; **Recovery** contacts.
- **Activity** — login/security history.
- **Contact** — change email/phone securely.
- **Privacy** — GDPR/DPDP: export data (JSON, no secrets) and erase account (cascade delete).

---

## 7. Tenant resolution — how a request finds its tenant

Every end-user and admin request must resolve to a tenant. `TenantResolutionMiddleware` does this:

- **Production:** by **custom domain** (e.g. `login.acme.com` → tenant `acme`). Map the domain under
  Branding/tenant settings.
- **Development (no custom domain):** use one of —
  - `?tenant=acme` query param (remembered in the `authly.dev_tenant` cookie), or
  - the `X-Tenant-Slug: acme` request header.
- `/signup` is **excluded** (it's tenant-less by design).
- For `/tenantadmin` paths where host resolution finds nothing, it falls back to the signed-in admin's
  own tenant claim — so a domain-less self-serve workspace can still reach its admin panel.

> So in local dev, the tenant login page is `http://localhost:8080/account/login?tenant=acme`.

---

## 8. Worked example — "Acme" from zero to first login

```
1.  Open  http://localhost:8080/            → click "Create your workspace"
2.  /signup → Company "Acme", admin email + password → submit
        → tenant `acme` created, you're signed into /tenantadmin and dropped on the wizard
3.  Wizard step 1 → create app "Acme Web", type Web,
        redirect URI https://app.acme.com/callback, scopes: openid profile email offline_access
4.  Wizard step 2 → copy Client ID + Client secret (secret shown once)
5.  Wizard step 3 → set logo + brand color
6.  Wizard step 4 → Finish  → banner gone, lands on Applications
7.  (Optional) Branding → map custom domain login.acme.com
8.  Point your app's OIDC client at  https://<authly-host>/.well-known/openid-configuration
9.  An Acme end user visits your app → redirected to Authly login
        (dev: http://localhost:8080/account/login?tenant=acme)
        → Register → verify email → log in → MFA if policy requires → back to your app with a code
10. Your app exchanges the code at /connect/token → access_token + id_token (+ refresh_token)
```

---

## 9. Security & compliance baked in

- **Tenant isolation** at the DB level (RLS); data created under tenant A is never visible to tenant B.
- **Secrets** (client secrets, API keys, sync keys) shown once, stored hashed/encrypted — never plaintext.
- **Audit log** — every state change writes an `audit_logs` row; no secrets/passwords/tokens/OTPs are logged.
- **Brute-force defence** — lockout with backoff; **rate limiting** with `429 + Retry-After`.
- **Breached-password** screening (HIBP), **CAPTCHA**, **block lists** (email/domain/IP).
- **Conditional access** — risk-based block or step-up MFA on suspicious logins (new IP + new device →
  security alert).
- **Compliance** — consent captured at signup; self-service data export and erase from the portal.
- **Background work** — email, webhooks, hooks, telemetry, retention purges run via Hangfire, not inline.

---

## 10. Route reference (cheat sheet)

| Goal | Route |
|------|-------|
| Create a workspace | `/signup` |
| Tenant admin sign-in | `/tenantadmin/account/login` |
| Onboarding wizard | `/tenantadmin/onboarding` |
| Manage apps / users / roles | `/tenantadmin/{applications,users,roles}` |
| End-user register / login | `/account/{register,login}` (dev: add `?tenant=<slug>`) |
| End-user portal | `/portal/profile` |
| OIDC discovery | `/.well-known/openid-configuration` |
| Developer docs (live) | `/docs` |

---

*Note: in development the hosted login/admin pages are reached with `?tenant=<slug>`; in production they
resolve by custom domain. The super-admin surface exists only when `SUPERADMIN_ENABLED=true` and is
removed entirely from customer-facing builds.*
