# Authly — Developer Quickstart

Authly is a multi-tenant, standards-based OpenID Connect / OAuth 2.0 identity provider.
If your stack has an OIDC client library (almost all do), integration is a few lines of config:
point it at the **discovery document** and it wires up the rest.

---

## 1. Concepts in 60 seconds

| Term | What it is |
|------|------------|
| **Tenant** | Your organization/workspace. Every user, app, and key belongs to exactly one tenant — data is isolated at the database level. |
| **Application** | An OAuth client (your web app, SPA, or mobile app). Has a `client_id`; confidential clients also get a `client_secret`. |
| **API key** | A server-to-server credential for the management API. Shown once, stored hashed. |
| **Scopes** | What a token may access: `openid profile email offline_access roles`. |

Two client types:

- **Web** — confidential. Has a `client_secret`. Use the Authorization Code flow with PKCE.
- **SPA / Native** — public. No secret. Use Authorization Code flow with PKCE (mandatory).

---

## 2. Create your first app (2 min)

1. Sign in to your **tenant admin** console.
2. Open **Get started** and follow the wizard, or go to **Applications → New application**.
3. Choose the type, set your **redirect URI** (e.g. `https://myapp.com/callback`), and create it.
4. Copy the **Client ID** and (for Web apps) the **Client secret** — the secret is shown only once.
5. Use the **Sandbox** page to run a test login and copy your integration endpoints.

---

## 3. Endpoints

Authly publishes a standard OIDC discovery document. Everything else is advertised there.

```
GET https://<your-authly-host>/.well-known/openid-configuration
```

| Endpoint | Path |
|----------|------|
| Discovery | `/.well-known/openid-configuration` |
| Authorization | `/connect/authorize` |
| Token | `/connect/token` |
| UserInfo | `/connect/userinfo` |
| Introspection | `/connect/introspect` |
| Revocation | `/connect/revoke` |
| End session (logout) | `/connect/logout` |

**Supported:** Authorization Code + PKCE, Refresh Token (with rotation), Client Credentials.
Access tokens live 1 hour; refresh tokens 14 days (rotated on every use — reuse is detected and the token family is revoked).

---

## 4. Authorization Code + PKCE flow

```
1. Generate a PKCE code_verifier + code_challenge (S256).
2. Redirect the user to:
     /connect/authorize
       ?response_type=code
       &client_id=YOUR_CLIENT_ID
       &redirect_uri=https://myapp.com/callback
       &scope=openid profile email offline_access
       &code_challenge=CHALLENGE
       &code_challenge_method=S256
       &state=RANDOM
3. User authenticates on Authly's hosted login page.
4. Authly redirects back to redirect_uri with ?code=...&state=...
5. Exchange the code at the token endpoint:
     POST /connect/token
       grant_type=authorization_code
       code=THE_CODE
       redirect_uri=https://myapp.com/callback
       client_id=YOUR_CLIENT_ID
       client_secret=YOUR_SECRET        (confidential clients only)
       code_verifier=THE_VERIFIER
6. Receive { access_token, id_token, refresh_token, expires_in }.
7. Call /connect/userinfo with `Authorization: Bearer <access_token>` for profile claims.
```

> PKCE is **required** for the authorization-code flow — for both public and confidential clients.

> **Browser (SPA) apps:** the `/connect/authorize` redirect is a top-level navigation, but the
> discovery, token, and userinfo calls are XHRs subject to CORS. Authly allows these automatically
> for the **origin of any registered redirect URI** — so a browser app at `https://myapp.com` whose
> redirect URI is registered just works, with no CORS configuration. Use a **SPA (public)** client,
> not Web/confidential: a browser cannot hold a `client_secret`, so a confidential client fails at
> the token exchange.

### Refresh

```
POST /connect/token
  grant_type=refresh_token
  refresh_token=THE_REFRESH_TOKEN
  client_id=YOUR_CLIENT_ID
  client_secret=YOUR_SECRET    (confidential clients only)
```

A new refresh token is returned each time; discard the old one.

### Client Credentials (machine-to-machine)

```
POST /connect/token
  grant_type=client_credentials
  client_id=YOUR_CLIENT_ID
  client_secret=YOUR_SECRET
  scope=...
```

### Logout (RP-initiated end-session)

Clearing tokens **in your app only** is not a full logout: Authly keeps its own SSO session
cookie, so the next trip to `/connect/authorize` silently re-authenticates (no login page). To log
the user out of Authly too, send them to the end-session endpoint:

```
GET /connect/logout
  ?id_token_hint=THE_ID_TOKEN
  &post_logout_redirect_uri=https://myapp.com/
```

Authly clears the SSO cookie and redirects back to your `post_logout_redirect_uri`.

> **Allowed post-logout target:** your app's **origin root** (`scheme://host[:port]/`) is allowed
> automatically — it's derived from the redirect URIs you registered (e.g. registering
> `https://myapp.com/callback` permits `post_logout_redirect_uri=https://myapp.com/`). No separate
> configuration. Send `id_token_hint` — it's how Authly validates the request and skips a confirm
> prompt.

---

## 5. Example — Next.js (next-auth)

```ts
providers: [
  {
    id: "authly",
    name: "Authly",
    type: "oidc",
    issuer: "https://<your-authly-host>",
    clientId: process.env.AUTHLY_CLIENT_ID,
    clientSecret: process.env.AUTHLY_CLIENT_SECRET,
    authorization: { params: { scope: "openid profile email offline_access" } },
  },
]
```

## 6. Example — generic backend (cURL)

```bash
# After receiving ?code= on your callback:
curl -X POST https://<your-authly-host>/connect/token \
  -d grant_type=authorization_code \
  -d code="$CODE" \
  -d redirect_uri="https://myapp.com/callback" \
  -d client_id="$CLIENT_ID" \
  -d client_secret="$CLIENT_SECRET" \
  -d code_verifier="$VERIFIER"
```

---

## 7. Verifying tokens

- **id_token / access_token** are JWTs signed by Authly. Fetch the signing keys from the `jwks_uri`
  in the discovery document and validate `iss`, `aud`, `exp`, and the signature with any standard JWT library.
- Or call `/connect/introspect` (server-side) for opaque validation.

---

## 8. Self-service & branding

- Customize the hosted login page (logo, brand color) under **Branding**.
- End users manage their own profile, sessions, MFA, and privacy from the user portal.
- Enable social login, MFA policy, and security controls per tenant from the admin console.

---

## 9. Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| `invalid_redirect_uri` | The `redirect_uri` isn't registered on the application (exact match required). |
| `invalid_client` | Wrong `client_id`/`client_secret`, or a public client sending a secret. |
| `invalid_grant` on refresh | The refresh token was already used (rotation) or expired. |
| PKCE error | Missing/incorrect `code_verifier` for the `code_challenge` you sent. |
| Re-login skips the login page (silent auto-login) | App-only logout left Authly's SSO cookie. Use the [end-session endpoint](#logout-rp-initiated-end-session) to log out of Authly too. |
| `invalid_request` on logout (post-logout redirect) | `post_logout_redirect_uri` must be your app's origin root (`scheme://host[:port]/`) and an origin you registered a redirect URI for. |
| CORS / status 0 on token or userinfo | The browser origin isn't a registered redirect-URI origin, or you're calling from a Web/confidential client instead of a SPA (public) one. |
| `invalid_request` "different workspace" on authorize | The `tenant` (or host) resolves to a different tenant than the one that owns the `client_id`. Use the client's own tenant. |

Use the **Sandbox** page to confirm credentials resolve and to copy the exact endpoint URLs for your host.
