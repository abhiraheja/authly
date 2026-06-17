# Decision & Implementation Plan — Google-style multi-project admin

> Yeh doc [01-firebase-model-explained.md](01-firebase-model-explained.md) ka follow-up
> hai. Wahan humne Firebase/Google ka model samjha aur 3 options rakhe the. Yahan
> **final faisla** aur **implementation plan** hai.
>
> Status: **Design approved — implementation pending** (abhi koi code nahi likha gaya).

---

## 1. Faisla (kya banana hai)

Google Cloud ko **exactly copy** karna hai. Confirmed decisions:

1. **Full Google model** — ek admin **account** multiple **projects** own/access karega
   aur ek **selector dropdown** se switch karega.
2. **Environments = bas alag projects** — koi alag "environment" type/property NAHI.
   dev/beta/prod sirf alag projects hain (jaise Google `myapp-dev` / `myapp-prod`).
3. **Self-serve** — koi bhi logged-in admin turant **"New project"** bana sakta hai.

### Sabse zaroori realization
**Authly ka `Tenant` already = Google ka `Project`.** Tenant ke paas pehle se branding,
OAuth clients (`Application`), scopes, aur users hain — bilkul Google Auth Platform ke
nav (Branding / Audience / Clients / Data Access) jaisa. Toh humein naya "project" entity
banane ki zaroorat nahi — **Tenant hi project hai**.

### Asli gap
Abhi admin ek tenant se bandha hai: `User.TenantId` ek single FK hai, email `(tenant_id,
email)` pe unique hai, aur `User.IsTenantAdmin` flag hai. **Ek insaan ek se zyada tenant
mein nahi ho sakta.** Google jaisa banane ke liye humein ek **cross-project admin
identity** chahiye.

---

## 2. Approach (decided)

Ek naya **global `Account` identity + `ProjectMembership` join table** banao — jo
existing **`SuperAdmin`** pattern ko mirror karega (global unique email, Argon2id
`PasswordHash`, apna cookie scheme, RLS-exempt). Tenant-scoped `User` ko **sirf
end-users** ke liye rakho.

```
Account (global login — Google account jaisa)
  │  email globally unique, PasswordHash (Argon2id)
  │
  └── ProjectMembership (join)        ← ek account ↔ kai projects
        ├── (Account A, Tenant acme,   Role=Owner)
        ├── (Account A, Tenant acme-dev, Role=Owner)   ← "environment" = bas dusra project
        └── (Account A, Tenant globex, Role=Owner)

Tenant (= Project)  ──>  Users (end-users, per-tenant)  +  Applications  +  Branding
```

### Kyun yeh, aur kyun NAHI "User ko global karna"
Doosra raasta tha: `User` ki email global karo + ek `UserTenant` join. **Reject kiya**
kyunki:
- `users` table **RLS-protected** hai (`app.current_tenant` pe depend karti hai). Login
  ke waqt tenant resolve nahi hua hota — toh "yeh user kis-kis tenant mein hai" yeh
  query karna chicken-and-egg ban jaata (RLS lock-out).
- Admin login end-user sessions / MFA / social-login / password-reset ke saath uljh
  jaata — bada aur risky refactor.

`SuperAdmin` pehle se ek **global, non-tenant, RLS-exempt** identity hai (apna cookie
`authly.superadmin`). `Account` usi ko mirror karta hai — sabse kam-risk raasta, aur
Google ka mental model (global account ↔ project memberships ↔ alag end-users) se exact
match.

---

## 3. Naye entities

| Entity | File | Fields |
|---|---|---|
| `Account` | `src/Authly.Core/Entities/Account.cs` | `Id`, `Email` (GLOBAL unique), `PasswordHash` (Argon2id), `FirstName?`, `LastName?`, `EmailVerified`, `CreatedAt`, `LastLoginAt?`, `Memberships` nav |
| `ProjectMembership` | `src/Authly.Core/Entities/ProjectMembership.cs` | `Id`, `AccountId` FK, `TenantId` FK (Tenant = project), `Role` (`ProjectRole`), `CreatedAt`. Unique `(AccountId, TenantId)`; index on `AccountId`; cascade-delete dono FK |
| `ProjectRole` enum | `src/Authly.Core/Enums/ProjectRole.cs` | `Owner, Admin, Viewer` (abhi sirf `Owner`; column aage Google-style IAM roles ke liye future-proof) |

> **RLS note:** `accounts` aur `project_memberships` **global** tables hain (jaise
> `super_admins`). Inpe RLS **mat lagao** — `project_memberships` tab read hoti hai jab
> koi tenant resolve hi nahi hua (`app.current_tenant` empty), toh RLS policy lock-out
> kar degi.

---

## 4. Changes by area (kahan-kahan badlega)

**Data** — [AppDbContext.cs](../../src/Authly.Infrastructure/Data/AppDbContext.cs):
`SuperAdmin` block jaisa hi config (snake_case columns, `gen_random_uuid()`/`NOW()`
defaults, `Role` ko `HasConversion<string>()`). Nayi migration
`AddAccountsAndProjectMemberships`. In tables ko RLS migration mein **add nahi karna**.

**Auth cookie** — wahi `AuthSchemes.TenantAdmin` (`authly.tenantadmin`) reuse karo.
Cookie ab carry karega: account id + **active project** id. [AuthConstants.cs](../../src/Authly.Web/Infrastructure/AuthConstants.cs)
mein `TenantAdminClaims.AccountId` (`"authly:account_id"`) add karo; `TenantAdminClaims.TenantId`
= active project id rahega. `NameIdentifier` = account id. **Switch = `SignInAsync` ko
naye `TenantId` claim ke saath dobara call** (ek hi cookie, ek source of truth).

**Tenant resolution** — [TenantResolutionMiddleware.cs](../../src/Authly.Web/Infrastructure/TenantResolutionMiddleware.cs)
(`/tenantadmin` fallback, ~lines 78-83): trivial — abhi bhi `TenantAdminClaims.TenantId`
(ab = active project) padh ke `SetTenant(...)`. RLS / `ITenantContext` set-once-per-request
waisa hi rahega.

**Guard** — [TenantAdminControllerBase.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/TenantAdminControllerBase.cs):
equality check (`cookieTenant != _tenant.TenantId`) ko **membership check** se replace
karo — `IProjectMembershipService.IsMemberAsync(accountId, activeProject)`; member nahi
to sign-out + redirect. **Yeh core behaviour change hai:** admin apni membership ke
KISI bhi project pe act kar sakta hai; forged/stale claim membership lookup pe fail ho
jaata hai. `CurrentUserId` → `CurrentAccountId`, `CurrentAudit()` actor type `"account"`.
Yeh naya dependency ~12 derived controllers ke constructor mein thread karna padega.

**Selector + New project** — naya [ProjectsController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/ProjectsController.cs)
(`TenantAdminControllerBase` se derive NAHI karega; apna membership check karega):
`POST /tenantadmin/projects/switch` (membership verify → cookie re-issue) aur
`GET/POST /tenantadmin/projects/new` (create → auto-switch). Ek
`ProjectSelectorViewComponent` + Bootstrap dropdown
[_AdminLayout.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml)
ke topbar (~lines 140-159) mein — Google ki "Select a project" modal jaisa: projects ki
list, active wala checked, neeche "+ New project". Existing logout dropdown rahega.

**Signup / onboarding** — [TenantSignupService.cs](../../src/Authly.Modules/Signup/TenantSignupService.cs):
pehla signup ab `Account` + `Tenant` + `ProjectMembership(Owner)` banayega (admin-`User`
ki jagah). Existing slug-dedup `CreateWorkspaceAsync` ko ek shared
`IAccountProjectService.CreateProjectForAccountAsync(accountId, name)` mein nikaalo, jise
"New project" bhi use kare. [SignupController.cs](../../src/Authly.Web/Controllers/SignupController.cs)
aur [TenantAdmin/AccountController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/AccountController.cs)
ab Account ke roop mein sign-in karenge (credential check `SuperAdminService.ValidateCredentialsAsync`
jaisa). Login **tenant-agnostic** ho jaayega ("tenant required" gate hatao); active
project default = pehli / most-recent membership.

**IsTenantAdmin / RBAC** — admin-panel access ab `ProjectMembership` pe gate hoga, na ki
`User` row pe. `tenant_admin` RBAC role sirf **end-user tokens** ke liye matter karta
hai, aur `Account` ko kabhi token nahi milta. Toh owner ke liye `IsTenantAdmin` set
karna / `tenant_admin` assign karna band. `EnsureSystemRolesAsync(tenantId)` ko
project-create pe rakho taaki har project ke end-user roles seed rahein. `User.IsTenantAdmin`
+ `ITenantAdminService` Phase 4 mein hatao.

**DI** — `IAccountRepository`, `IProjectMembershipRepository` (model `SuperAdminRepository`)
register karo `Infrastructure/DependencyInjection.cs` mein; `IAccountSignupService`,
`IAccountProjectService`, `IProjectMembershipService` register karo
`Modules/DependencyInjection.cs` mein.

---

## 5. Phases (har phase apne aap mein green ship hota hai)

| Phase | Kya | Result |
|---|---|---|
| **1 — Identity + auth** | entities, migration, repositories, `IsMemberAsync`; admin login + signup ko Account+Tenant+Membership + naya cookie; middleware + membership guard; dependency derived controllers mein thread; `TenantSignupTests` rewrite | Aaj jaisa hi chalega (har admin ka ek project) |
| **2 — Selector + switch** | `ProjectsController.Switch`, view component, layout dropdown; switch pe cookie re-issue | Multiple membership wale admin switch kar sakte |
| **3 — Self-serve New project** | `CreateProjectForAccountAsync`, `ProjectsController.New` + view, auto-switch | **Full Google model live** |
| **4 — Cleanup** | `User.IsTenantAdmin`, `AnyTenantAdminAsync`, `ITenantAdminService`, first-admin bootstrap hatao; `is_tenant_admin` column drop | Legacy path saaf |

Pre-production hai (244 tests green, koi real data nahi) — clean refactor/migration theek hai.

---

## 6. Verification (kaise test karenge)

- Har phase pe `dotnet build` + `dotnet test` green.
- [TenantSignupTests.cs](../../tests/Authly.Tests/Signup/TenantSignupTests.cs) rewrite:
  assert ki `Account` + `ProjectMembership(Owner)` bana, slug dedup chalta hai,
  `account.created`/`tenant.signup` audited (`IsTenantAdmin`/RBAC asserts hatao).
- Naye unit tests: `IsMemberAsync` (member / non-member / wrong account),
  `CreateProjectForAccountAsync` (tenant+membership banata, slug dedup),
  switch-rejects-non-member.
- Manual end-to-end (Web app chala ke): signup → project A panel → "New project" → B bana
  aur auto-switch → selector se A↔B switch → same account se login pe dono projects dikhe
  → jis project ka member nahi uspe access reject.
- Confirm: `accounts` / `project_memberships` pe koi RLS policy nahi (resolution inhe
  empty `app.current_tenant` ke saath read karta hai).
```
