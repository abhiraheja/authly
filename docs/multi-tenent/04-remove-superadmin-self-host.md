# SuperAdmin hatao — Authly ab pure self-host

> Yeh [03-superadmin-role.md](03-superadmin-role.md) ko **reverse** karta hai. 03 mein
> humne maana tha ki SuperAdmin Saarvix-cloud ka operator console hai isliye rahega.
> Ab product direction confirm ho gaya: **pure self-host Docker image, koi Saarvix
> cloud nahi.** Is wajah se SuperAdmin ka koi matlab hi nahi bachta — **poori tarah
> hata rahe hain.** Doc-only — abhi koi code nahi.

---

## 1. Faisla aur kyun

**Product = pure self-host.** Har company Docker image pull karke apna instance chalati
hai. Us company ke paas apne box pe **100% control already hai** (DB, env, container).
Uske upar ek "platform super-admin" rakhna nonsensical hai — woh kiska boss banega? Woh
khud hi owner hai.

- Koi **Saarvix-operated multi-customer cloud nahi** hai. Toh "platform operator" naam
  ka role exist hi nahi karta.
- Aapka existing `SUPERADMIN_ENABLED` gate already self-host mode mein `/superadmin` ko
  **404** kar deta tha — yani design ne yeh anticipate kiya tha. Ab hum ek kadam aage
  jaake usse **delete** kar rahe hain (gate rakhne ke bajaye).
- Self-host owner == platform owner. Sab management uske **Account + projects** (Google
  model, dekho [02-decision-and-plan.md](02-decision-and-plan.md)) se ho jaata hai.

---

## 2. Kya HATAO (pre-prod hai, clean drop theek hai)

### A. SuperAdmin identity + surface (poora delete)
- `SuperAdmin` entity, `ISuperAdminService`/`SuperAdminService`, `ISuperAdminRepository`/repo
- `src/Authly.Web/Areas/SuperAdmin/**` (saare controllers + views)
- `AuthSchemes.SuperAdmin`, `AuthPolicies.SuperAdmin`, `SuperAdminClaims` ([AuthConstants.cs](../../src/Authly.Web/Infrastructure/AuthConstants.cs))
- [Program.cs](../../src/Authly.Web/Program.cs) se: super-admin cookie registration,
  bootstrap seeding (`SUPERADMIN_EMAIL`/`SUPERADMIN_PASSWORD`), `SUPERADMIN_ENABLED`
  gate (~lines 172-175, 231-242), aur `SuperAdminIpAllowlistMiddleware` ([SuperAdminIpAllowlistMiddleware.cs](../../src/Authly.Web/Infrastructure/Security/SuperAdminIpAllowlistMiddleware.cs))
- Migration se `super_admins` table drop

### B. Cloud-only features (cloud ke bina dead — drop)
| Feature | Kyun dead | Files |
|---|---|---|
| **Announcements** (Saarvix → tenant admins broadcast) | Koi Saarvix nahi jo broadcast kare | `Announcement` entity, service, [AnnouncementsController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/AnnouncementsController.cs), tenant-admin banner |
| **SelfHostedInstance + sync/telemetry** | Yeh cloud ka phone-home receiver tha; ab har box hi standalone hai | `SelfHostedInstance`, `SelfHostSyncService`, [InstancesController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/InstancesController.cs), sync ingest endpoint |
| **`DEPLOYMENT_MODE` / `SYNC_*` config** | "cloud vs self_hosted" distinction khatam — sab self-host | Program.cs / config |
| **Tenant suspend / reactivate** | Self-host mein koi platform-authority nahi jo abuse pe suspend kare | [TenantsController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/TenantsController.cs) |

---

## 3. Kya MOVE karo (delete mat karo)

### Monitoring / health → Account-owner (admin) surface, read-only
Yeh genuinely useful hai self-host operator ke liye bhi (DB/Redis up hai? kitne users?).
Isliye SuperAdmin se nikaal ke **admin panel pe ek read-only page** banao.

- Reuse karo: `IPlatformHealthProbe` (DB/Redis probes), `InstanceMetricsCollector`
  (counts), `LoginAnalyticsStore` (login trends) — logic wahi, sirf surface badlega.
- **Scope:** Box-level health (DB/Redis) **global** hai (poora instance). Per-tenant
  metrics (users/apps/sessions) ko **account ke projects** tak scope karo, na ki saare
  tenants (kyunki ab cross-tenant platform view nahi hai).
- Source file jise reference karo: [MonitoringController.cs](../../src/Authly.Web/Areas/SuperAdmin/Controllers/MonitoringController.cs).

---

## 4. Kya FOLD karo

### Tenant **Delete** → project settings mein owner-action
Project delete karna ab platform ka kaam nahi, **owner** ka kaam hai. Project settings
mein ek "Delete project" action do jo existing `TenantService.DeleteAsync` reuse kare
(soft-delete → status `Deleted`, grace period). Suspend/reactivate drop (upar #2-B).

---

## 5. Audit ka kya?
Audit logging **rahega** — woh tenant/project-scoped events ke liye hai (login,
config-change, etc.), platform-only nahi. Sirf `actor_type = "super_admin"` wale code
paths hatenge. Audit-log **streaming** ka config ab observability feature mein move ho
raha hai — dekho [05-pluggable-observability.md](05-pluggable-observability.md).

---

## 6. Plan pe asar
Yeh implementation plan mein ek **naya phase (Phase 5 — Remove SuperAdmin)** add karta
hai, jo Account model (Phases 1-4) ke baad chalega. Sequence: pehle Account+projects aa
jaaye (taaki admin surface khada rahe), phir SuperAdmin nikaalo.

## 7. TL;DR
- Pure self-host → SuperAdmin ka koi role nahi → **delete**.
- Cloud-only cheezein (announcements, instance-sync, deployment-mode, tenant-suspend) → **drop**.
- Monitoring/health → **admin panel pe read-only move**.
- Tenant delete → **project settings mein owner action**.
- Audit logging → **rahega**; uska streaming config observability feature (05) mein.
```
