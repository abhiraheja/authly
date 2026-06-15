# Authly — Runtime Acceptance Checklist (Phases 2–14)

Everything through Phase 14 is **build-verified + unit-tested (203/203)** but has **never run against a live
stack**. This checklist is the end-to-end pass to do once Docker + a browser are available. Work top-to-bottom;
each phase's gate mirrors the Acceptance line in `Saarvix-Identity-Development-Plan.md`.

Legend: ☐ = to verify · record PASS/FAIL + notes inline.

---

## 0. Bring up the stack

```bash
cd d:/Personal/Authly
docker compose up --build -d
docker compose logs -f app          # watch migrations auto-apply + super-admin seed
```

Expected on first boot:
- Postgres healthy, app reachable at **http://localhost:8080**.
- All EF migrations apply (InitialCreate … AddAnnouncements) — no errors in log.
- Super admin seeded from `SUPERADMIN_EMAIL=admin@authly.local` / `SUPERADMIN_PASSWORD=ChangeMe!123` (dev only).

Smoke:
- ☐ `GET /` renders the landing page (Super Admin Console + Developer Docs buttons).
- ☐ `GET /docs` renders with live endpoint URLs.
- ☐ `GET /.well-known/openid-configuration` returns valid OIDC discovery JSON.
- ☐ `GET /hangfire` redirects to super-admin login when unauthenticated.

**Dev tenant resolution:** tenant surfaces resolve by custom domain in prod; in dev use `?tenant=<slug>`
(remembered in the `authly.dev_tenant` cookie) or the `X-Tenant-Slug` header.

---

## Phase 1 — Tenancy & Super Admin
1. ☐ Sign in at `/superadmin/account/login` with the seed creds → forced password change on first login.
2. ☐ Create a tenant (e.g. "Acme", slug `acme`) → appears in Tenants list.
3. ☐ Suspend then reactivate the tenant → status flips; audit-log rows written for each.

## Phase 2 — Core Auth (end-user)
Use the tenant surface: `http://localhost:8080/?tenant=acme` then the user routes.
1. ☐ Register a user → verification email payload appears in app logs (StubEmailSender).
2. ☐ Verify email via the logged link → account becomes verified.
3. ☐ Log in → session cookie issued; `login_history` row with result=success.
4. ☐ Wrong password → result=failed row; no session.
5. ☐ Forgot-password → reset link in logs → set new password → log in with it.
6. ☐ Confirm anti-enumeration: forgot-password for an unknown email returns the same response as a known one.

## Phase 3 — OAuth/OIDC (OpenIddict)
Create an application in Tenant Admin first (or via the onboarding wizard, Phase 14).
1. ☐ Authorization Code + PKCE: drive `/connect/authorize` → login → callback with `code`.
2. ☐ Exchange code at `/connect/token` → `access_token` + `id_token` (+ `refresh_token` with `offline_access`).
3. ☐ `GET /connect/userinfo` with the bearer token → profile claims.
4. ☐ Refresh: redeem the refresh token → new token pair; **reuse the old one → rejected** (rotation/theft detection).
5. ☐ Client credentials grant for a confidential client → access token.

## Phase 4–9 — RBAC, API keys, MFA, messaging, social, webhooks/hooks/claims
1. ☐ RBAC: create a role + permissions, assign to a user, confirm `roles` claim in the issued token.
2. ☐ API key: create in Tenant Admin (raw shown once) → call a management endpoint with it → revoke → call fails.
3. ☐ MFA (TOTP): enrol with an authenticator, log out, log in → TOTP challenge → backup code also works once.
4. ☐ Messaging: configure a provider (BYOK), send a test → `message_logs` row; secret stored encrypted (not plaintext).
5. ☐ Social login: configure a provider, run the redirect round-trip → `social_identities` row linked.
6. ☐ Webhook: register an endpoint → trigger an event → delivery attempt logged (with signature); retry on failure.
7. ☐ Custom claim config → appears in the issued token.

## Phase 10 — Branding & user portal
1. ☐ Set branding (logo, primary color) in Tenant Admin → hosted login page reflects it.
2. ☐ Custom domain mapping resolves the right tenant (or via `X-Tenant-Slug` in dev).
3. ☐ End-user portal: edit profile, view/revoke sessions, manage MFA.

## Phase 11 — Advanced auth
1. ☐ Magic-link login → link in logs → one-time use.
2. ☐ Secure contact change (email/phone) → verification on both old/new as designed.
3. ☐ Recovery contact flow.
4. ☐ Passkey (WebAuthn) register + login (needs a platform/roaming authenticator).

## Phase 12 — Security hardening
1. ☐ Brute-force a login → lockout with exponential backoff; unlock on backoff expiry / successful sign-in.
2. ☐ Rate limit: hammer a throttled POST → `429` with `Retry-After`.
3. ☐ Breached password (HIBP) → register/reset with a known-pwned password is rejected (needs network).
4. ☐ CAPTCHA: enable per tenant → challenge appears and is verified server-side.
5. ☐ Block list: blocked email/domain/IP denied at register/login.
6. ☐ Security headers present (CSP/HSTS/X-Frame-Options); super-admin IP allowlist 404s off-list (if `SUPERADMIN_IP_ALLOWLIST` set).
7. ☐ Suspicious login (new IP + new device) → `SuspiciousLoginJob` queues a `security_alert`.

## Phase 13 — Self-host & compliance
**Compliance (cloud):**
1. ☐ Consent captured at signup (`consent_records` rows for terms + privacy).
2. ☐ `/portal/privacy`: export → JSON download with profile/sessions/etc and **no secrets/hashes/tokens**.
3. ☐ Erase: retype-email confirm → user + child rows hard-deleted (cascade) → signed out; `user.erased` audit has ids only.
4. ☐ Retention jobs (Hangfire): expired verification/reset tokens + OTPs purged hourly; old login_history/audit purged daily.

**Self-host telemetry:**
5. ☐ In Super Admin → Self-hosted, register an instance → copy the one-time sync key.
6. ☐ Run a second instance with `docker-compose.self-host.yml` (`DEPLOYMENT_MODE=self_hosted`, `SYNC_ENDPOINT`, `SYNC_KEY`).
7. ☐ Capture the sync POST on the wire → body is **aggregate counts + version + health only, zero PII**.
8. ☐ Kill the cloud endpoint → self-hosted instance keeps authenticating (offline grace, failure never blocks auth).

## Phase 14 — Polish & launch-ready  *(primary gate for this pass)*
1. ☐ A brand-new tenant admin sees the "Get started" nudge, runs the **onboarding wizard**
   (create app → copy credentials → set branding → reach test login) → wizard marks onboarded, banner disappears.
2. ☐ **Sandbox**: run a test login against a real tenant user → correct outcome shown; **no admin cookie is issued**;
   endpoints listed match discovery.
3. ☐ **Monitoring** (`/superadmin/monitoring`): PostgreSQL + Redis show Healthy with latency; aggregate metric cards
   match reality; 14-day login chart reflects the logins generated above; self-hosted telemetry table lists the instance.
4. ☐ Stop Redis (`docker compose stop redis`) → Monitoring shows Redis **Down** and the dashboard still renders (probe never throws).
5. ☐ **Announcements**: create one → it appears as a banner to tenant admins; set it inactive/expired → it disappears.
6. ☐ Platform ops panel shows version/deployment mode; set `BACKUP_STATUS`/`BACKUP_LAST_AT`/`DEPRECATION_NOTICE` env → reflected.
7. ☐ **Docs**: an external dev can integrate using only `/docs` + `docs/developer/Quickstart.md` (do a clean-room OIDC client setup).

---

## Cross-cutting checks (spot-verify throughout)
- ☐ **Tenant isolation**: data created under tenant A is never visible from tenant B (try `?tenant=` swap + direct ID access).
- ☐ **RLS backstop**: with `app.current_tenant` unset, RLS-protected tables return nothing (query via psql without the setting).
- ☐ **Secrets**: client secrets / API keys / sync keys shown once, stored hashed/encrypted — confirm no plaintext in DB.
- ☐ **Audit**: every state change writes an `audit_logs` row; no secrets/passwords/tokens/OTPs in any log line.
- ☐ **Background work**: email/WhatsApp/webhooks/hooks/telemetry run via Hangfire, not inline (check `/hangfire`).

## Teardown
```bash
docker compose down            # keep data
docker compose down -v         # also drop the pgdata volume (clean slate)
```
