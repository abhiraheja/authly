# Access Policies (ABAC) + App-to-App (Machine-to-Machine) — Guide

> Do cheezein samjhane ke liye, examples ke saath:
> 1. **Access Policies** (`/tenantadmin/policies`) — "kaun, kis cheez pe, kya kar sakta hai"
>    ka fine-grained control. Kya hai, kyu hai, kaise kaam aayega.
> 2. **App-to-App** — meri microservice A jab microservice B se baat kare, to apna token
>    kaise leke share kare (client-credentials flow).

---

# PART A — Access Policies (ABAC)

## A1. Yeh hai kya?

Aam taur pe permissions **role** se hoti hain (RBAC): "admin sab kar sakta hai, viewer sirf
padh sakta hai". Lekin real zindagi me rules role se zyada barik hote hain, jaise:

- "Engineering department ka banda hi engineering documents padh sakta hai."
- "Apni hi team ka record edit kar sakta hai, doosri team ka nahi."
- "Office IP se hi delete allowed hai."

In sabko role me ghusana mushkil ho jaata hai. **ABAC (Attribute-Based Access Control)**
isi ke liye hai — decision **attributes** pe hota hai:

- **Subject** = jo request kar raha hai (user/service) ke attributes — department, role, level…
- **Resource** = jis cheez pe action ho raha hai ke attributes — owner, classification…
- **Environment** = context — IP, time…

Aap **policies** likhte ho ("allow/deny … in conditions me"), aur Authly har request pe
**Allow/Deny** ka faisla deta hai.

## A2. Kyu chahiye / kaise help karega

- **Centralized authorization:** har microservice me apna-apna `if (user.dept == ...)` likhne
  ke bajaye, rules ek jagah (Authly) me. Code se rules nikal gaye → badalna aasaan.
- **Audit + test:** policies UI me dikhti hain, "Test a decision" se bina deploy kiye check
  kar sakte ho.
- **Default-deny security:** jab tak explicit Allow na ho, sab deny — yani galti se kuch
  open nahi rehta.
- **Fine-grained:** role se aage — department, ownership, classification, IP, time, kuch bhi.

## A3. Ek policy ke parts

(`AccessPolicy` entity — [src/Authly.Core/Entities/AccessPolicy.cs](../../src/Authly.Core/Entities/AccessPolicy.cs);
admin form [AccessPoliciesController](../../src/Authly.Web/Areas/TenantAdmin/Controllers/AccessPoliciesController.cs))

| Field | Matlab |
|---|---|
| **Name** | Pehchaan ke liye, e.g. `engineers-read-docs`. |
| **Description** | Optional note. |
| **Effect** | `Allow` ya `Deny`. |
| **Action** | Kaunsa action — exact (`document.read`), prefix (`document.*`), ya sab (`*`). |
| **Resource type** | Kis cheez pe — exact (`document`), prefix (`doc*`), ya `*`. |
| **Conditions** | JSON array of attribute-tests. **Saari conditions sach honi chahiye** tabhi policy match. Khaali `[]` = sirf action+resource pe match. |
| **Priority** | Integer. Allow policies me se sabse **highest priority** wala jeetता hai. |
| **Enabled** | Off policy ignore hoti hai. |

## A4. Faisla kaise hota hai (combining rules)

Engine ([AbacEngine.cs](../../src/Authly.Modules/Abac/AbacEngine.cs)) ka logic — **deny-overrides,
default-deny**:

1. Pehle wahi policies chuni jaati hain jinka **Action + Resource type match** kare AUR jinki
   **saari conditions hold** karein.
2. In me se koi bhi **Deny** hai → **DENY** (reason `explicit_deny`). Deny sabse upar — chahe
   Allow ho ya priority kuch bhi ho.
3. Deny nahi, par koi **Allow** hai → sabse **highest-priority Allow** se **ALLOW** (reason `allow`).
4. Kuch match nahi hua → **DENY** (reason `default_deny`).

> **Yaad rakho:** koi policy hi nahi = sab deny. Kuch allow karne ke liye explicit Allow
> policy likhni padti hai. Aur ek bhi matching Deny poori request rok deta hai.

**Action/Resource matching (case-insensitive):**
- `*` → kuch bhi match.
- `document.*` → `document.read`, `document.write`, `document.delete`…
- `doc*` → `document`, `docs`…
- `document.read` → bilkul wahi.

## A5. Conditions — operators aur attributes

Condition ek JSON object: `{ "attribute": "...", "operator": "...", "value": "..." }`.
Attribute hamesha **scope.key** hota hai:

- `subject.*` — request karne wale ke attributes
- `resource.*` — resource ke attributes
- `environment.*` (ya `env.*`) — context

**Operators:**

| Operator | Kaam | Example |
|---|---|---|
| `equals` | barabar (case-insensitive) | `subject.role equals "admin"` |
| `notEquals` | barabar nahi | `subject.status notEquals "suspended"` |
| `contains` | substring | `subject.name contains "john"` |
| `in` | comma-list me se koi ek | `subject.dept in "eng,ops,security"` |
| `greaterThan` | number se bada | `subject.level greaterThan "3"` |
| `lessThan` | number se chhota | `environment.hour lessThan "18"` |
| `exists` | attribute maujood hai (value ignore) | `subject.phone exists` |

## A6. Example policies

**Example 1 — Engineering department documents padh sake:**
- Effect: `Allow`, Action: `document.read`, Resource type: `document`, Priority: `10`
- Conditions:
```json
[
  { "attribute": "subject.department", "operator": "equals", "value": "eng" }
]
```

**Example 2 — Sirf apna hi record edit kar sake (ownership):**
- Effect: `Allow`, Action: `record.update`, Resource type: `record`, Priority: `10`
- Conditions:
```json
[
  { "attribute": "subject.userId", "operator": "equals", "value": "owner-placeholder" }
]
```
> Note: `value` static hota hai — "subject == resource.owner" jaisा dynamic compare engine
> abhi support nahi karta. Iske liye relying app `subject.userId` aur `resource.owner` ko
> compare karke ek attribute (e.g. `subject.isOwner = "true"`) bhej sakta hai, ya owner ki id
> ko `value` me daal de.

**Example 3 — Suspended user ko sab deny (override):**
- Effect: `Deny`, Action: `*`, Resource type: `*`, Priority: `0`
- Conditions:
```json
[
  { "attribute": "subject.status", "operator": "equals", "value": "suspended" }
]
```
Yeh Deny baaki saare Allows pe haavi rahega.

## A7. "Test a decision" (admin) — bina deploy ke check

`/tenantadmin/policies` ke right panel me Action + Resource type + teen JSON bags
(Subject/Resource/Environment) daalo → **Evaluate** → result `Allowed / PolicyName / Reason`.
(Route: `POST /tenantadmin/policies/test`.)

E.g. Action `document.read`, Resource type `document`, Subject `{"department":"eng"}` →
Example-1 policy se **Allowed = true, reason = allow**.

## A8. Apni app/microservice se runtime decision API

Admin test ke alawa ek **public REST API** hai jise aapki services call kar sakti hain
([Api/AccessController.cs](../../src/Authly.Web/Controllers/Api/AccessController.cs)):

```
POST /api/v1/access/evaluate
Authorization: <API key>        (tenant API credential se — tenant API key se resolve)
Content-Type: application/json
```

**Request** (action + resourceType **required**; teeno bags string→string):
```json
{
  "action": "document.read",
  "resourceType": "document",
  "subject":     { "department": "eng", "role": "admin" },
  "resource":    { "owner": "u_123", "classification": "internal" },
  "environment": { "ipAddress": "10.0.0.5" }
}
```

**Response:**
```json
{ "allowed": true, "policy": "engineers-read-docs", "reason": "allow" }
```

### Zaroori baat: attributes **aap** bhejte ho (PDP model)
Engine khud user ke attributes DB se nahi nikalta — **calling app** subject/resource/
environment banakar bhejti hai. Yani:
1. App ke paas user ka access token aata hai (login se).
2. App token ke claims (ya `/connect/userinfo`) se user ke attributes nikaalti hai.
3. Resource ke attributes apne DB se leti hai.
4. `POST /api/v1/access/evaluate` call karke `allowed` check karti hai.

Isse Authly ek **Policy Decision Point (PDP)** ban jaata hai — rules ek jagah, services bas
"haan/na" poochti hain.

---

# PART B — App-to-App (Machine-to-Machine)

## B1. Problem

Microservice **A** ko microservice **B** ka API call karna hai. Yahan koi user nahi hai
(login screen nahi aa sakti). To A apni pehchaan kaise sabit kare aur token kaise paaye?

## B2. Solution: Client-Credentials grant

OAuth2 ka **client_credentials** flow exactly isi ke liye hai. A ek **Machine application**
ban jaata hai (apna `client_id` + `client_secret`), token endpoint se token leta hai, aur
wahi token B ko bhejta hai. B token verify karke request maan leta hai.

## B3. Step 1 — Machine app banao

TenantAdmin → **Applications** → New application → **Type = Machine**:
- **Name** do (e.g. `order-service`).
- **Scopes** (optional, space-separated) — fine-grained access ke liye, e.g. `api inventory:read`.
- Redirect URIs ki **zaroorat nahi** (machine ke liye ignore).

Bante hi **Client ID** (`client_…`) aur **Client secret** (`secret_…`) **ek hi baar** dikhte
hain — turant copy karke safe jagah (env/secret manager) me rakho. Secret sirf **hashed**
store hota hai, dobara nahi milega
([Applications/Details.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Applications/Details.cshtml)).

> Machine client confidential hota hai (secret ke saath), grant sirf `client_credentials`,
> aur `openid`/`offline_access` auto-add nahi hote
> ([ApplicationService.cs](../../src/Authly.Modules/Applications/ApplicationService.cs)).

API se bhi bana sakte ho: `POST /api/v1/applications` body `{ "name": "...", "type": "Machine",
"scopes": ["api"] }` → response me `application` + ek-baar ka `clientSecret`
([Api/ApplicationsController.cs](../../src/Authly.Web/Controllers/Api/ApplicationsController.cs)).

## B4. Step 2 — Service A token mange

```bash
curl -X POST https://auth.example.com/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=client_aaabbbccc111222333444" \
  -d "client_secret=secret_xxxxxxxxxxxxxxxxxxxxxxxx" \
  -d "scope=api inventory:read"
```

**Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "api inventory:read"
}
```

Token endpoint: `POST /connect/token`
([Connect/AuthorizationController.cs](../../src/Authly.Web/Controllers/Connect/AuthorizationController.cs)).
Access token ek **JWT** hai, lifetime **1 ghanta**
([OpenIddictRegistration.cs](../../src/Authly.Web/Infrastructure/OpenIddictRegistration.cs)).

**Token ke andar (decoded) kuch aise claims:**
```json
{
  "sub": "client_aaabbbccc111222333444",   // machine ki pehchaan (user nahi)
  "client_id": "client_aaabbbccc111222333444",
  "tenant_id": "550e8400-e29b-41d4-a716-446655440000",
  "scope": "api inventory:read",
  "iss": "https://auth.example.com",
  "exp": 1719245400
}
```

## B5. Step 3 — A → B call

Service A wahi token **Authorization header** me bhejta hai:
```
GET https://inventory.internal/api/stock/42
Authorization: Bearer eyJhbGciOiJSUzI1NiI...
```

A token ko **cache** kare (expiry tak reuse) — har call pe naya token mat lo. ~1 min pehle
refresh kar lo (skew ke liye).

## B6. Step 4 — Service B token verify kare

B ko **secret share karne ki zaroorat nahi** — wo bas JWT ki signature aur claims verify
karta hai, Authly ki **public keys** se. Authly OpenIddict ye endpoints deta hai:

- Discovery: `GET /.well-known/openid-configuration`
- Public keys (JWKS): `jwks_uri` (`/.well-known/jwks`)

B ka check:
1. JWT signature verify (JWKS se public key, `kid` ke hisaab se).
2. `iss` = Authly issuer.
3. `exp` expire to nahi.
4. `scope` me zaroori scope hai (e.g. `inventory:read`).
5. `tenant_id` sahi tenant ka hai.

.NET me B aise validate karega (concept):
```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = "https://auth.example.com";   // discovery + JWKS auto
        o.TokenValidationParameters.ValidateAudience = false; // ya apna aud set karo
    });
// phir endpoint pe [Authorize] + scope check
```

> **Important:** koi user-context nahi hai is token me — `sub` = client_id. B ko "kaunsi
> service" pata chalti hai (`client_id`), "kaunsa tenant" (`tenant_id`), aur "kya allowed"
> (`scope`). User-level decisions ke liye user ka token alag se chahiye.

## B7. Scopes se fine-grained control

Machine app ko sirf utne scopes do jitne chahiye (least privilege). E.g. `order-service` ko
`inventory:read` do, `inventory:write` nahi. B har endpoint pe required scope check kare.

## B8. ABAC + M2M saath me

Chaaho to B, request aane par Authly ka `POST /api/v1/access/evaluate` call karke aur barik
faisla le sakta hai (token ke `client_id`/`scope`/`tenant_id` ko subject attributes banakar).
Yani: token = "yeh kaun hai", ABAC = "yeh yeh kar sakta hai ya nahi".

## B9. Secret rotation aur dev note

- **Rotate:** secret leak/expire ho to TenantAdmin → Applications → app → **Rotate secret**
  (`POST /tenantadmin/applications/{id}/rotate-secret`). Naya secret ek baar dikhega; purana
  band. Services ko naya secret deploy karo.
- **Dev vs prod:** development me signing certificate **ephemeral** hota hai (har restart pe
  badalta hai) → JWKS bhi badalta hai, tokens invalidate ho sakte hain. **Production me
  persisted X.509 signing/encryption keys** configure karna zaroori hai (warna restart pe
  sab token toot jaayenge). Dekho [OpenIddictRegistration.cs](../../src/Authly.Web/Infrastructure/OpenIddictRegistration.cs).

## B10. Poora flow (sequence)

```
Service A                         Authly (/connect/token)          Service B
   |  client_id + secret + scope        |                              |
   |----------------------------------->|                              |
   |        access_token (JWT, 1h)      |                              |
   |<-----------------------------------|                              |
   |                                                                   |
   |  GET /api/...   Authorization: Bearer <JWT>                       |
   |------------------------------------------------------------------>|
   |                                    | JWKS (public keys)           |
   |                                    |<-----------------------------|  (verify sig/iss/exp/scope)
   |                         200 OK (token valid + scope ok)           |
   |<------------------------------------------------------------------|
```

---

## Quick reference

| Cheez | Endpoint / Jagah |
|---|---|
| Policies admin | `/tenantadmin/policies` |
| Policy test | `POST /tenantadmin/policies/test` |
| **Runtime decision API** | `POST /api/v1/access/evaluate` (API key) |
| Machine app banao | `/tenantadmin/applications` (Type=Machine) ya `POST /api/v1/applications` |
| **Token lo** | `POST /connect/token` (grant_type=client_credentials) |
| Secret rotate | `POST /tenantadmin/applications/{id}/rotate-secret` |
| Discovery / JWKS | `/.well-known/openid-configuration`, `/.well-known/jwks` |

**Core files:** ABAC engine [AbacEngine.cs](../../src/Authly.Modules/Abac/AbacEngine.cs),
services [AbacServices.cs](../../src/Authly.Modules/Abac/AbacServices.cs); OAuth/token
[Connect/AuthorizationController.cs](../../src/Authly.Web/Controllers/Connect/AuthorizationController.cs),
server config [OpenIddictRegistration.cs](../../src/Authly.Web/Infrastructure/OpenIddictRegistration.cs).
