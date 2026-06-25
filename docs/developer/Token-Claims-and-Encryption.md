# Token Claims Routing + Access-Token Encryption — Guide

> Do generic features jo token issuance ko control karte hain. Dono **config-driven** hain
> (koi per-integration fork nahi), aur standard extension points (pre-token pipeline hook +
> custom claim config) ke saath kaam karte hain.
>
> 1. **Readable access tokens** — OpenIddict default me access token **encrypted (JWE)** deta
>    hai; ek toggle se use plain **signed JWT** bana sakte ho taaki resource servers (APIs)
>    sirf JWKS se claims padh sakein.
> 2. **Hook claims → id_token** — pre-token pipeline hook ke claims default me sirf **access
>    token** me jaate hain; naya **`hook`** claim-source se unhe **id_token** me bhi bhej
>    sakte ho — bina hook dobara chalaaye, bina frontend ko access token decode karwaaye.

---

# PART A — Readable (un-encrypted) Access Tokens

## A1. Yeh hai kya / kyu chahiye

OpenIddict by default access token ko **encrypt** karta hai (JWE — header me
`"alg":"RSA-OAEP","enc":"A256CBC-HS512"`). Iska matlab koi external **resource server**
(microservice / API) us token ke claims ko sirf **JWKS public key** se **padh nahi sakta** —
JWKS sirf signature verify karne ki key deta hai, decrypt karne ki nahi. Decrypt karne ke
liye chahiye:

- Authly ki **private encryption key** (expose karna unsafe), **ya**
- har request pe Authly ka **introspection** endpoint call (extra round-trip).

Jab aapke downstream services token se seedha `tenant_id` / custom claims (`company_id`,
permissions, etc.) padhna chahte hain — jaisa typical JWT-based microservice auth me hota hai —
to access token ka **readable signed JWT** hona zaroori hai.

> **Note:** id_token kabhi encrypt nahi hota (hamesha signed-only readable). Yeh feature sirf
> **access token** ko affect karta hai.

## A2. Toggle kaise use kare

Config key (boolean):

```
Authly:Tokens:DisableAccessTokenEncryption
```

| Value | Behaviour |
|---|---|
| `true` | Access token = plain **signed** JWT (RS256, JWKS se verify + read). |
| `false` | Access token = **encrypted** JWE (default OpenIddict behaviour). |
| *(not set)* | **Default**: Development me `true` (readable), Production me `false` (encrypted). Explicit config hamesha jeetता hai. |

**appsettings.json me:**

```json
{
  "Authly": {
    "Tokens": {
      "DisableAccessTokenEncryption": true
    }
  }
}
```

**Environment variable se (Docker / k8s):**

```
Authly__Tokens__DisableAccessTokenEncryption=true
```

Change ke baad **app restart** zaroori hai (yeh server-startup setting hai).

## A3. Kahan implement hua

[src/Authly.Web/Infrastructure/OpenIddictRegistration.cs](../../src/Authly.Web/Infrastructure/OpenIddictRegistration.cs)

```csharp
var disableAccessTokenEncryption =
    configuration.GetValue<bool?>("Authly:Tokens:DisableAccessTokenEncryption") ?? isDevelopment;
...
if (disableAccessTokenEncryption)
    options.DisableAccessTokenEncryption();
```

## A4. Limitations / security

- **Server-wide hai, per-tenant nahi.** OpenIddict me token encryption ek global server
  setting hai — kisi ek tenant ke liye on/off nahi ho sakta. (Per-tenant chahiye to woh bada
  architectural kaam hai.)
- **Trade-off:** Encrypted token zyada private hota hai (claims sirf authorized parties padh
  sakte hain). Disable tabhi karo jab resource servers JWKS se verify karte hain aur token ke
  claims padhna unka legit kaam hai. Token abhi bhi **signed** rehta hai — tamper-proof hai,
  bas readable ho jaata hai.
- Agar future me "encrypted rakho par trusted backends ko decrypt karne do" chahiye, to woh
  ek alag feature hai (symmetric shared encryption key distribute karna) — yeh toggle woh nahi
  karta.

---

# PART B — Pre-Token Hook Claims ko id_token me bhejna (`hook` claim source)

## B1. Problem

Pre-token **pipeline hook** (`/tenantadmin/hooks`, stage `PreToken`) jo claims lautata hai, woh
**sirf access token** me jaate hain. Claim assembly do pass me hoti hai:

- **Access pass** → pipeline hook chalti hai, uske claims access token me.
- **Id pass** → hook **nahi** chalti (taaki ek hi request me hook do baar na fire ho); id_token
  me sirf `static` + `metadata` claims jaate hain.

To dynamic hook claims (jaise per-user `company_id`, `sid`, `subscription`) **config se id_token
me nahi jaa paate the**. id_token me daalne ka pehle ek hi (bhaari) raasta tha: value ko user
ke **metadata** me likho, phir `metadata → id` claim banao — jo har change pe metadata sync +
re-auth maangता hai.

## B2. Solution — naya `hook` claim source

`/tenantadmin/claims` me ab **`hook`** type ka claim config bana sakte ho. Yeh **koi value
fetch nahi karta** — yeh sirf **declare** karta hai ki "pre-token hook ne jo claim banaya hai,
use is token (e.g. `id`) me bhi likho". Value pipeline hook ke output se aati hai.

### Kaise configure kare

`/tenantadmin/claims` → Add claim:

| Field | Value |
|---|---|
| **Claim name** | hook ke output claim ka exact naam, e.g. `company_id` |
| **Type** | **`hook`** |
| **Value / path** | *(khaali chhod do — Hook ke liye Source nahi chahiye)* |
| **Token** | **`id`** (access token me to woh hook se already aata hai) |
| **Application** | All (tenant-wide) ya specific app |

Bas. Ab login pe `company_id` **access token + id_token dono** me aayega. Frontend id_token se
seedha padh lega — access token decode karne ki zaroorat nahi.

> Har hook-claim ke liye ek row banao (e.g. `company_id`, `sid`, `subscription`...). Jo claim
> id_token me chahiye, sirf usi ke liye `hook → id` config banao.

## B3. Internally kaise kaam karta hai

1. **Access pass** pipeline hook chala kar uske claims (`access.Claims`) jodता hai.
2. Issuer **id pass** ko `access.Claims` ko `HookClaims` ke roop me deta hai (hook dobara nahi
   chalti).
3. Id pass me `hook`-type config dekh kar, agar `HookClaims` me us claim ka naam mila to value
   copy kar leता hai → id_token claim ban jaata hai.
4. Destination routing us claim ko **dono** tokens (access + id) me bhej deta hai.

Files:
- [src/Authly.Core/Enums/WebhookEnums.cs](../../src/Authly.Core/Enums/WebhookEnums.cs) — `ClaimSourceType.Hook` naya value.
- [src/Authly.Modules/Claims/ClaimModels.cs](../../src/Authly.Modules/Claims/ClaimModels.cs) — `ClaimAssemblyRequest.HookClaims`.
- [src/Authly.Modules/Claims/TokenClaimAssembler.cs](../../src/Authly.Modules/Claims/TokenClaimAssembler.cs) — `Hook` case.
- [src/Authly.Web/Controllers/Connect/AuthorizationController.cs](../../src/Authly.Web/Controllers/Connect/AuthorizationController.cs) — id pass ko `HookClaims: access.Claims` deta hai.
- [src/Authly.Modules/Claims/ClaimConfigService.cs](../../src/Authly.Modules/Claims/ClaimConfigService.cs) — `Hook` allow (Source optional).
- [src/Authly.Infrastructure/Data/AppDbContext.cs](../../src/Authly.Infrastructure/Data/AppDbContext.cs) — `"hook"` parse (text column, **no migration**).
- [src/Authly.Web/Areas/TenantAdmin/Views/ClaimConfigs/Index.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/ClaimConfigs/Index.cshtml) — dropdown me `hook`.

## B4. Notes / limitations

- **Reserved claims override nahi kar sakte:** `sub, iss, aud, exp, iat, nbf, jti, email,
  email_verified, name, tenant_id, roles, role, permissions, scope`. Inn naamo se custom/hook
  claim nahi banega (Authly inhe khud manage karta hai). Yani hook ka `permissions` claim isi
  list ki wajah se token me **nahi aayega** — agar app-specific permissions chahiye to use
  reserved-list se alag naam dena (e.g. `app_permissions`).
- `hook` config **value fetch nahi karता** — agar pipeline hook ne woh claim nahi lautaya to
  woh token me nahi aayega (chupchaap skip).
- `webhook` type abhi bhi UI se invalid hai (per-claim URL支持 nahi) — hook **values** pipeline
  hook se hi aati hain; `hook` type sirf **routing** ke liye hai.

---

# PART C — Combined example (SPA + microservices)

1. **Login** → Authly token issue karte waqt **PreToken pipeline hook** aapke service ko POST
   karta hai → aap `company_id`/`sid` jaise claims lautate ho.
2. `Authly:Tokens:DisableAccessTokenEncryption=true` → **access token readable signed JWT**.
   Downstream microservices JWKS se verify karke `company_id` seedha padh lete hain.
3. `/tenantadmin/claims` me `company_id` + `sid` ke liye **`hook → id`** configs → ye claims
   **id_token** me bhi aate hain. **SPA** id_token se seedha padh leता hai, access token decode
   karne ki zaroorat nahi.

```
                ┌──────────── Authly ────────────┐
  login ───►    │  PreToken pipeline hook  ──────┼──► your service returns
                │      ↓ claims (company_id,…)    │      { company_id, sid, … }
                │  access pass  → access_token    │
                │  id pass (hook→id configs)      │
                │      → id_token (company_id,sid) │
                └────────────────────────────────┘
   access_token (signed JWT)  → microservices read company_id via JWKS
   id_token     (signed JWT)  → SPA reads company_id directly
```

---

# Changelog (is feature ke saath kya badla)

| Area | Change |
|---|---|
| OpenIddict server | `Authly:Tokens:DisableAccessTokenEncryption` flag (default dev=on, prod=off) → `DisableAccessTokenEncryption()`. |
| Claims | Naya `ClaimSourceType.Hook` — pipeline-hook claim ko chosen token (id) me route karta hai; Source optional. |
| Claim assembly | `ClaimAssemblyRequest.HookClaims`; id pass ko access pass ke claims diye jaate hain. |
| Admin UI | `/tenantadmin/claims` Type dropdown me `hook`; help text updated. |
| DB | Koi migration nahi (`claim_configs.type` text column hai; `"hook"` naya allowed value). |
