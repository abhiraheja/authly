# Firebase ka Multi-Tenancy / Project Model — gehraai se samjho

> **Maqsad:** Pehle Firebase apne users ko environments (dev/beta/prod) aur
> organizations kaise deta hai, usko theek se samajhna. Phir is model ko Authly
> (Saarvix Identity) ke current model ke saath map karna, taaki hum decide kar
> saken ki "environment" feature kaise banayein.
>
> Yeh document **sirf samajhne** ke liye hai — implementation ka faisla agle
> document mein hoga. Yahan hum sirf Firebase ko decode karte hain aur do-teen
> raaste (options) saamne rakhte hain.

---

## 1. Sabse pehle: Firebase ke teen alag-alag "boxes"

Log aksar Firebase ke "project", "tenant" aur "app" ko mix kar dete hain. Yeh
teen **bilkul alag** cheezein hain. Inhe alag samajhna zaroori hai:

```
┌──────────────────────────────────────────────────────────────────┐
│  GOOGLE CLOUD / FIREBASE PROJECT   (sabse upar — top-level box)     │
│  e.g.  "myapp-prod"                                                 │
│                                                                    │
│   • Yahi wo cheez hai jo login ke baad console aapse PUCHHTA hai    │
│   • Project ID globally unique hota hai (myapp-prod-3f9a2)          │
│   • ISKE andar SAB kuch aata hai: Auth, Firestore, Storage,        │
│     Functions, Hosting, billing, quotas — sab isolated             │
│                                                                    │
│   ┌────────────────────────────────────────────────────────┐      │
│   │  AUTHENTICATION (end-user pool)                          │      │
│   │   • Default: ek hi user pool poore project ka            │      │
│   │   • Email project ke andar unique                        │      │
│   │                                                          │      │
│   │   ┌──────────────┐  ┌──────────────┐  (OPTIONAL —        │      │
│   │   │ Tenant: acme │  │ Tenant: globex│   Identity Platform │      │
│   │   │  apne users  │  │  apne users   │   ka paid feature)  │      │
│   │   └──────────────┘  └──────────────┘                     │      │
│   └────────────────────────────────────────────────────────┘      │
│                                                                    │
│   ┌──────────┐ ┌──────────┐ ┌──────────┐                           │
│   │ Web App  │ │ iOS App  │ │Android App│   ← "Apps" = client       │
│   │ (config) │ │ (config) │ │ (config)  │     registrations         │
│   └──────────┘ └──────────┘ └──────────┘                           │
└──────────────────────────────────────────────────────────────────┘
```

| Firebase box | Yeh kya hai | Kiska isolation | Authly mein iska match |
|---|---|---|---|
| **Project** | Sabse upar ka container. Login ke baad jo aap chunte ho. | **Sab kuch** — users, data, config, billing, quota | ❌ Abhi Authly mein **nahi hai** (poora Authly = ek hi project) |
| **Tenant** (Identity Platform) | Project ke *andar* end-users ko alag-alag organizations mein baantna | Sirf **end-users + providers** | ✅ Authly ka `Tenant` (acme) — yahi hai |
| **App** | Ek platform ka client registration (web/iOS/Android) | Kuch nahi — sirf client config | ✅ Authly ka `Application` (OAuth client) |

**Sabse important insight:** Jise aap "environment / workspace" bol rahe ho —
Firebase mein wo **Project** hai, **Tenant nahi**. Yeh confusion ka asli point
hai. Aaiye dono ko detail mein dekhte hain.

---

## 2. Firebase PROJECT — top-level box (environment yahan banta hai)

### Project kya hota hai
- Ek Firebase project = ek Google Cloud project. 1:1 mapping.
- Iska ek **Project ID** hota hai jo **globally unique** hai (poori duniya mein),
  jaise `myapp-prod-3f9a2`. Aur ek display name.
- Iske andar **saari** Firebase services rehti hain — Authentication, Firestore,
  Realtime DB, Cloud Storage, Functions, Hosting — aur har project ka apna
  **billing, quota, aur config** hota hai. Ek project ka data dusre se 100%
  isolated hai.

### Login ke baad "project chuno" wala flow
- Jab aap Firebase Console mein login karte ho, aapko **un saare projects ki list**
  dikhti hai jinpe aapke Google account ko access hai (IAM ke through).
- Aap ek project chunte ho → uske baad poora console us project ke context mein
  chalta hai. Yeh wahi behaviour hai jo aapne describe kiya.
- **Dhyaan do:** Yahan jo "users" project pe access rakhte hain (Owner / Editor /
  Viewer) — yeh **team members / admins** hain (IAM roles). Yeh wo end-users
  **nahi** hain jinhe aapki app Firebase Auth se login karwati hai. Do alag duniya:
  - **IAM members** = aap aur aapki team (project manage karne wale)
  - **Auth users** = aapki app ke customers (login karne wale)

### Environment (dev / beta / prod) — Firebase ka official tarika
Firebase ki **official guidance**: har environment ke liye ek **alag project**
banao:

```
myapp-dev        ← developers ka khel, test data
myapp-staging    ← QA / beta testing
myapp-prod       ← real users, real data
```

- Har environment ka apna user pool, apna data, apna config, apni billing.
- Dev ka test data **kabhi** prod ko touch nahi karta — kyunki physically alag
  projects hain.
- **Firebase CLI** isko aliases se manage karta hai. `.firebaserc` file mein:
  ```json
  {
    "projects": {
      "dev":     "myapp-dev",
      "staging": "myapp-staging",
      "prod":    "myapp-prod"
    }
  }
  ```
  Aur `firebase use prod` / `firebase use dev` se switch karte ho.

**Trade-off:**
- ✅ Poora isolation, koi risk nahi, billing alag.
- ❌ Dev ka user prod mein nahi hai (alag pools). Config har project mein duplicate.
  Ek customer ko teeno env mein chahiye to teen jagah banana padta hai.

---

## 3. Firebase TENANT (Identity Platform) — project ke andar ka box

- Yeh Firebase Auth ka **paid upgrade** hai, jiska naam **Google Cloud Identity
  Platform (GCIP)** hai.
- Isse aap **ek hi project ke andar** kai "tenants" bana sakte ho (max ~1000).
- Har tenant ke paas:
  - **Apne isolated end-users** (tenant A ka user tenant B se alag, bhale email
    same ho)
  - **Apne identity providers** ka config (alag Google/SAML/OIDC setup)
  - **Apni settings**
- Client side pe authenticate karte waqt tenant batana padta hai:
  `auth.tenantId = "acme-xxxx"`. User ko `(tenantId, uid)` se address karte hain.
- Yeh **B2B2C** ke liye hai: ek app, par har customer organization ke users alag.
  Jaise ek SaaS jo 50 companies ko bechta hai, har company ke employees alag pool.

> **Yahi Authly ke `Tenant` (acme) jaisa hai.** Authly ka multi-tenancy bilkul
> GCIP tenant model jaisa banaya gaya hai (per-tenant users, per-tenant apps,
> per-tenant branding). Niche section 5 mein side-by-side dekhenge.

---

## 4. Quick recap — Firebase mein "isolation" ke 3 levels

| Aap kya alag karna chahte ho | Firebase ka tool | Kyun |
|---|---|---|
| Pura **environment** (dev/beta/prod) | **Alag Project** | Data + users + config sab alag rakhna |
| Ek product ke andar alag **organizations** (B2B customers) | **Tenant (GCIP)** | Sirf users alag, baaki project shared |
| Alag **platforms** (web/iOS/Android) ek hi backend pe | **App** | Sirf client config, backend shared |

---

## 5. Authly ka current model — "project0"

Aapne jo bola "abhi jo system hai use project0 samjho" — bilkul sahi analogy hai.
**Poora Authly deployment abhi Firebase ke ek single project jaisa hai.** Iske
andar Authly ka apna tenant-system pehle se hai (jo GCIP tenant jaisa hai):

```
AUTHLY DEPLOYMENT  ( = Firebase ka 1 Project = aapka "project0" )
│
├── Tenant "acme"     ← Firebase GCIP Tenant jaisa
│     ├── Users        (User.TenantId = acme,  email per-tenant unique)
│     ├── Applications (OAuth clients,  Application.TenantId = acme)
│     ├── Branding / Settings (per-tenant JSONB)
│     └── CustomDomain  (auth.acme.com)
│
├── Tenant "globex"
│     └── ... apne users, apps ...
│
└── (koi "environment" / project-level box NAHI hai)
```

**Authly ke actual building blocks** (concrete, code se):

| Authly cheez | File | Firebase equivalent |
|---|---|---|
| `Tenant` (Slug="acme", CustomDomain, Branding, ParentId) | [Tenant.cs](../../src/Authly.Core/Entities/Tenant.cs) | GCIP **Tenant** |
| `User` (TenantId FK, email unique per `(tenant_id, email)`) | [User.cs](../../src/Authly.Core/Entities/User.cs) | Tenant ka **end-user** |
| `Application` (TenantId FK, ClientId, RedirectUris) | [Application.cs](../../src/Authly.Core/Entities/Application.cs) | Firebase **App** (client) |
| Tenant resolution (custom domain → slug → ReturnUrl → cookie) | [TenantResolutionMiddleware.cs](../../src/Authly.Web/Infrastructure/TenantResolutionMiddleware.cs) | `auth.tenantId` set karna |
| RLS backstop (`app.current_tenant` Postgres var) | [TenantConnectionInterceptor.cs](../../src/Authly.Infrastructure/Tenancy/TenantConnectionInterceptor.cs) | Tenant data isolation |

**Khaas baat:** Authly mein abhi **environment ka concept nahi hai**. Ek "Sandbox"
testing UI zaroor hai ([SandboxController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/SandboxController.cs)),
par wo sirf login flow test karne ke liye hai — alag data/user pool nahi deta.

Toh aapka sawaal yeh hai: **"environment (dev/beta/prod) ka box kaise banayein?"**
Firebase ne yeh kaam **Project level** pe kiya. Authly mein Project level abhi
hai hi nahi. Toh humare paas teen raaste hain.

---

## 6. Authly mein environment banane ke 3 raaste (options)

> Yeh sirf options hain — faisla aapka. Har ek ka Firebase analogy aur trade-off
> diya hai.

### Option A — Firebase jaisa exact: alag deployment per environment
Har environment = bilkul alag Authly deployment (alag database/connection).
`myapp-dev`, `myapp-prod` ki tarah.

- **Firebase match:** 💯 Exactly Firebase ka recommended model (alag project per env).
- ✅ Total isolation, simplest mental model, code mein **zero change**.
- ❌ Har env mein tenant/app/config dobara banana padega. Ek shared control panel
  nahi. Operationally bhaari.

### Option B — Naya top-level box: `Environment` / `Project` entity Tenant ke upar
Authly mein ek naya **Project/Environment** entity introduce karo jo Tenant ke
*upar* baithe. Har environment ke apne tenants, users, apps.

```
Project "prod"  → Tenant acme → users/apps
Project "dev"   → Tenant acme → users/apps   (alag data)
```

- **Firebase match:** Yeh Firebase ke Project concept ko Authly ke andar laata hai
  (multi-project ek hi deployment mein).
- ✅ Ek hi deployment, ek control panel, proper isolation, scalable.
- ❌ Sabse bada code change — har entity, query, resolution, RLS mein ek aur
  dimension (`EnvironmentId`) add karna padega.

### Option C — Tenant pe property add karo, ya sibling/child tenants
Aapne khud bola "acme me kuch add kar sakte hai yaa ek new property introduce kar
sakte hai" — yeh wahi hai. Do sub-tarike:

- **C1 — `Environment` property Tenant pe:** Tenant mein ek enum field
  `Environment = Dev | Beta | Prod`. Phir `acme-dev`, `acme-prod` alag tenant rows.
- **C2 — `ParentId` use karke (Authly mein PEHLE SE hai):** Ek parent tenant `acme`,
  uske niche child tenants `acme-dev`, `acme-beta`, `acme-prod`. Resolution mein
  child chunna.

- **Firebase match:** Yeh thoda hybrid hai — Firebase mein environment Tenant level
  pe nahi hota, par chhote setups ke liye yeh sabse kam-mehnat wala hack hai.
- ✅ Sabse kam code change (`ParentId` already exists). Ek deployment.
- ❌ "Environment" ko "organization" ke saath mix kar deta hai — conceptually saaf
  nahi. Aage chalke confusing ho sakta hai. RLS / isolation ko manually sambhalna
  padega.

---

## 7. Faisle ke liye sochne wale sawaal (agle doc mein)

1. **Environment kaun banayega** — Saarvix platform (super-admin), ya har customer
   apne liye (self-serve, Firebase jaisa)?
2. **Users env ke beech share hone chahiye?** Firebase mein nahi hote (alag pool).
   Aapko dev/prod mein same user chahiye ya nahi?
3. **Ek control panel se sab env dikhne chahiye** (Option B/C) ya alag-alag chalega
   (Option A)?
4. **Scale** — kitne customers × kitne environments? Zyada hai to Option B sahi,
   kam hai to Option C kaafi.

---

## 8. Ek line mein nichod (TL;DR)

- Firebase mein **"environment" = alag Project** (top-level box), aur **"organization"
  = Tenant** (project ke andar). Yeh do **alag** levels hain.
- Authly ke paas **Tenant** (= Firebase Tenant) hai, par **Project/Environment level
  nahi** hai — abhi poora Authly = "project0".
- Environment dene ke liye ya to (A) alag deployment, ya (B) naya Environment box
  Tenant ke upar, ya (C) Tenant pe property / `ParentId` se kaam chala lo.
- **Firebase ke sabse kareeb = Option B.** Sabse kam mehnat = Option C2 (`ParentId`).
```
