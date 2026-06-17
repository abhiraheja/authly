# SuperAdmin ka role — Account model ke baad

> Sawaal (user): "Account model ke baad SuperAdmin ab kisi kaam ka nahi — usme bas yeh
> dekh sakte hai ki kaun mera system use kar raha hai. Correct?"
>
> Chhota jawab: **Partly sahi, par mostly nahi.** SuperAdmin bekaar nahi hota — uska
> role bas saaf ho jaata hai. Sirf **ek** cheez redundant hoti hai. Neeche poora
> breakdown.
>
> Yeh [02-decision-and-plan.md](02-decision-and-plan.md) ka companion hai. Doc-only —
> koi code change nahi.

---

## 1. Pehle confusion door karo: 3 alag consoles hain

| Surface | Kiske liye | Kya manage karta hai |
|---|---|---|
| **TenantAdmin** (`/tenantadmin`) | **Customer** (project owner) | Apne project ke clients, branding, users, scopes |
| **SuperAdmin** (`/superadmin`) | **Saarvix (aap)** — platform operator | Poore platform ki governance: saare tenants, health, abuse, instances |
| **User** (`/`) | **End-user** | Apna login/profile |

> **SuperAdmin kabhi customer ka tenant-manager tha hi nahi.** Customer apna kaam
> hamesha TenantAdmin se karta tha. Toh Account model "SuperAdmin ko replace" nahi
> karta — woh **TenantAdmin** ko upgrade karta hai (ek account → multiple projects +
> selector). SuperAdmin alag layer hai, alag cookie (`authly.superadmin`), alag MFA,
> alag IP-allowlist.

---

## 2. Account model ke baad kya badalta hai

### Sirf 1 cheez redundant: tenant **Create**
Abhi naya tenant banane ka eklauta raasta `SuperAdmin → TenantsController.Create` hai
([SuperAdmin/Controllers/TenantsController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/TenantsController.cs)).
Self-serve signup + "New project" aa jaane ke baad customer khud project banayega —
toh yeh primary path ke liye redundant ho jaata hai.

- **Recommendation:** Create ko **hatao mat**, rakho — manual onboarding / support
  cases ke liye useful (jab Saarvix khud kisi customer ke liye project bana de).
  Bas ab woh "the only way" nahi, "operator convenience" ban jaata hai.

### Baaki sab **essential** hai — koi per-customer Account inhe replace nahi kar sakta
| Capability | File | Kyun platform-only |
|---|---|---|
| Tenant **suspend / reactivate / delete** | [TenantsController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/TenantsController.cs) | Abuse, non-payment, compliance hold — customer khud ko suspend nahi karega |
| **Monitoring / health / login analytics** | [MonitoringController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/MonitoringController.cs) (DB/Redis probes, aggregate metrics via `InstanceMetricsCollector`, `LoginAnalyticsStore`) | SaaS ops — pure platform-wide, kisi single customer ka nahi |
| **Self-hosted instances** (sync key issue + telemetry) | [InstancesController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/InstancesController.cs) | Hybrid/self-host deployments platform-level — Account nahi de sakta |
| **Announcements** (sab tenant admins ko notices) | [AnnouncementsController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/AnnouncementsController.cs) | Maintenance/deprecation/marketing — platform broadcast |
| **Dashboard** (aggregate tenant counts) | [DashboardController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/DashboardController.cs) | Platform overview |
| **Audit log** (har super-admin action) | `audit_logs` table | Compliance / accountability |
| **Bootstrap + security** (SUPERADMIN_EMAIL/PASSWORD seed, MFA, IP allowlist, SUPERADMIN_ENABLED gate) | [Program.cs](../../src/Authly.Web/Program.cs), [SuperAdminIpAllowlistMiddleware.cs](../../src/Authly.Web/Infrastructure/Security/SuperAdminIpAllowlistMiddleware.cs) | Operator-only security |

**Nichod:** "Kaun mera system use kar raha hai" dekhna SuperAdmin ke kaamon mein se sirf
**ek** hai (dashboard + monitoring). Suspend/abuse, ops health, self-host, announcements,
audit — yeh sab alag, zaroori platform-owner functions hain.

---

## 3. Naya gap jo Account model paida karta hai

Ab tak SuperAdmin "kaun use kar raha hai" = **Tenants** ki list dikhata tha
(`TenantsController.Index`). Account model ke baad asli "users of the system" =
**Accounts** (ek account ke kai projects ho sakte hain). Lekin SuperAdmin **Accounts ko
dekhta hi nahi** — abhi sirf tenants.

- **Recommendation (future task, is plan ke 4 phases mein NAHI):** SuperAdmin pe ek
  **read-only Accounts view** add karo —
  - Saare accounts ki list (email, created, last login)
  - Har account ke project memberships (kaun-kaun se tenants, role)
  - Tenant detail pe "is project ke owners/members" dikhana
- Yeh tab banega jab `Account` / `ProjectMembership` entities aa jayenge (Phase 1 ke
  baad). Abhi sirf note kar rahe hain.

---

## 4. Summary

- SuperAdmin **bekaar nahi** — woh Saarvix ka platform-operator console hai, jo customer
  ke TenantAdmin se hamesha alag tha.
- Account model **TenantAdmin** ko upgrade karta hai (multi-project + selector), SuperAdmin
  ko nahi.
- Account model ke baad **sirf tenant Create redundant** hota hai (signup le leta hai);
  rakho support ke liye. Suspend/delete, monitoring, self-host, announcements, audit,
  security — sab essential.
- **Naya kaam:** SuperAdmin mein **Accounts visibility** add karni chahiye (read-only),
  taaki "kaun mera system use kar raha hai" ka asli jawab (accounts + memberships) mile.
  Yeh future task hai, abhi documented hai.
```
