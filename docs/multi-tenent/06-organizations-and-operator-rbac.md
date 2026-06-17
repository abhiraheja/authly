# Organizations + Employees + Operator RBAC

> Requirement: ek company apne **employees** ko Authly console operators ke roop mein
> manage kar paaye — company se scoped, custom roles + permissions ke saath — aur yeh
> **app end-users (`User`) se bilkul alag** rahe.
>
> Yeh doc **[02-decision-and-plan.md](02-decision-and-plan.md) ko REVISE karta hai**:
> flat `Account` + `ProjectMembership` ki jagah ab **Organization-centric** model aata
> hai. Doc-only — abhi koi code nahi.

---

## 1. Do populations — kabhi mat milao

| Population | Entity | Kaun | Kahan login |
|---|---|---|---|
| **App end-users** | `User` (tenant-scoped, RLS) | Customer ke apps ke users (public self-signup) | Authly hosted login → OAuth token apps ke liye. **Unchanged.** |
| **Workspace operators (employees)** | `Account` (global) | Company ke staff jo console operate karte hain | Authly **console** (TenantAdmin surface). OAuth token **kabhi nahi** milta. |

Yeh requirement **sirf operators (Account layer)** ke baare mein hai. End-user `User` +
uska RBAC (`Role`/`Permission`/`UserRole` → tokens) jaisa hai waisa rahega.

---

## 2. Confirmed decisions

1. **Operators = Account layer** (console login), `User` table NAHI.
2. **Organization (Company) entity** — naya top-level box jo company ke **projects ko
   group** karta hai + **employee directory** rakhta hai. Access **org-level** pe grant.
3. **Custom operator roles + granular permissions** — ek **alag operator-side RBAC**
   (end-user RBAC se separate, conflation se bachne ke liye).

---

## 3. Revised hierarchy

```
Organization (Company)              ← NAYA global entity
  ├── Projects:  Tenant.OrganizationId   (sibling projects = "environments": dev/beta/prod)
  ├── Members:   Account ──(OrganizationMembership)── Organization,  with operator role(s)
  └── Operator RBAC: OperatorRole / OperatorPermission / OperatorRolePermission (org-scoped)

Account (global employee login) ── ek ya zyada Organizations ka member
```

Pehle (doc 02) tha: `Account → ProjectMembership → Tenant` (flat). **Ab:** `Organization`
upar, projects org ke andar, employees org ke members. `ProjectMembership` **replace** ho
gaya `OrganizationMembership` se.

---

## 4. Entities

> Saare naye identity/grouping/operator-RBAC tables **GLOBAL / RLS-exempt** hain (jaise
> `super_admins`) — login/switch ke waqt jab koi tenant resolve nahi hua tab read hote
> hain. Operator RBAC org-scoped hai `organization_id` column + app-level filter se, RLS
> se nahi.

### `Account` — global login (mirror `SuperAdmin`)
`Id`, `Email` (GLOBAL unique), `PasswordHash?` (**nullable** — invite pending = null,
login nahi kar sakta), `FirstName?`, `LastName?`, `EmailVerified`, `Status` (Active|Disabled),
`CreatedAt`, `LastLoginAt?`, `Memberships` nav.

### `Organization` (Company)
`Id`, `Name`, `Slug` (GLOBAL unique, slugified), `OwnerAccountId` (FK → accounts, Restrict),
`Settings` (jsonb), `CreatedAt`, `UpdatedAt`. (Branding per-project rahega `Tenant` pe;
billing v1 mein nahi.)

### `OrganizationMembership` — Account ↔ Organization (REPLACES `ProjectMembership`)
`Id`, `AccountId` (FK Cascade), `OrganizationId` (FK Cascade), `Status` (Invited|Active|
Disabled), `InvitedByAccountId?`, `CreatedAt`, `MemberRoles` nav. Unique `(AccountId,
OrganizationId)`; index `AccountId`.
- **Role scope = ORG-LEVEL** (decided): operator role grant org ke **saare projects** pe
  apply hota hai. Per-project narrowing future ke liye (nullable `MemberRole.TenantId`) —
  **v1 mein nahi**, par schema room chhodta hai (no breaking change later).

### `Tenant.OrganizationId` (naya required FK)
Project ab hamesha exactly ek org ke andar. FK Restrict (live projects wale org ko delete
block). Index `idx_tenants_organization`. Pre-prod: NOT NULL, koi backfill nahi.
`Tenant.Slug` global-unique rahega (custom-domain/hosted-login resolution untouched).

### Operator RBAC (end-user RBAC se SEPARATE)
Structure end-user `Role`/`Permission`/`RolePermission`/`UserRole` jaisa, par org-scoped
aur distinct catalogue:
- `OperatorRole` (`Id, OrganizationId, Name, Description?, IsSystem, CreatedAt`; unique `(OrganizationId, Name)`)
- `OperatorPermission` (`Id, OrganizationId, Resource, Action`; `Name = "{Resource}.{Action}"`)
- `OperatorRolePermission` (composite PK)
- `MemberRole` — `UserRole` ka operator analogue, **membership pe keyed** (`OrganizationMembershipId, OperatorRoleId, OrganizationId, GrantedByAccountId?, GrantedAt`). Member hatao → membership + member_roles cascade.
- `AccountInviteToken` — naya table jo `accounts.id` FK kare (existing reset/verification tokens `users.id` FK karte hain, isliye reuse nahi ho sakte).

Naya `src/Authly.Core/Authorization/OperatorRbac.cs` (mirrors `SystemRbac.cs`). **System
operator roles** (per-org seeded): `org_owner` (sab, protected), `org_admin` (`org.manage`/
`billing.manage` ke alawa sab), `project_admin` (project + client + enduser + role), `viewer`
(`*.read`).

**Operator permission catalogue:**
| Resource | Actions |
|---|---|
| `project` | read, write, create, delete |
| `client` | read, manage (OAuth `Application` + secrets) |
| `enduser` | read, manage (project ke `User` admin karna — end-user ke apne `user.*` RBAC se alag) |
| `member` | read, invite, manage (operator directory) |
| `role` | read, manage (operator roles) |
| `observability` | read, manage |
| `org` | read, manage (rename/delete org) |
| `billing` | read, manage (self-host v1 mein reserved no-op) |

> **`PermissionEvaluator.Satisfies` as-is reuse** karenge (wahi wildcard semantics jo
> end-user RBAC use karta hai) — duplicate logic nahi.

---

## 5. Auth / cookie / guard

Cookie `AuthSchemes.TenantAdmin` reuse. Claims: `NameIdentifier` = account id,
`TenantAdminClaims.AccountId`, **`OrgId`** (active org), `TenantId` (active project).
Org/project switch = naye claims ke saath `SignInAsync` re-issue (ek cookie, ek source).

**Guard — `TenantAdminControllerBase`:** equality-check ki jagah do-part authorization:
1. Active project active org ka ho, AUR account ka uss org mein `Active` membership ho —
   naya `IConsoleAccessService.ResolveAsync(accountId, orgId, projectId)` (mirrors
   `RbacService.GetUserAuthorizationAsync`) effective operator permission set deta hai
   (null → sign-out + redirect). Result `HttpContext.Items` mein cache.
2. **Per-action permission**: `[RequireOperatorPermission("client.manage")]` action filter
   jo cached set pe `PermissionEvaluator.Satisfies` chalaye (extra DB hit nahi).

`ITenantContext` set-once-per-request + RLS untouched. ~16 derived controllers ke
constructor mein ek dependency thread hogi (mechanical).

---

## 6. Selector — two-level (org → project), Google-style

Account multiple orgs mein ho sakta hai, isliye **two-level**: topbar breadcrumb **active
org › active project**. `ConsoleSelectorViewComponent` (doc-02 ke `ProjectSelectorViewComponent`
ko replace karta hai): org chuno (sirf >1 hone pe dikhe) → org ke projects (active checked)
→ "+ New project" (`project.create` gated). Single-org account ek-level project list pe collapse.

Naya `WorkspaceController` (base se derive NAHI; apna membership check): `switch-org`,
`switch-project`, `new-project` (project active org mein bane → auto-switch, via
`IConsoleProvisioningService.CreateProjectAsync` jo slug-dedup `CreateWorkspaceAsync` wrap kare).

---

## 7. Signup + invite

### Revised self-serve signup
`SignupController` + `TenantSignupService` ab provision karenge:
1. `Account` (Argon2id, global email-unique)
2. `Organization` (slugified company name, `OwnerAccountId`)
3. Operator RBAC seed: `IOperatorRbacService.EnsureSystemRolesAsync(orgId)` (mirrors
   `RbacService.EnsureSystemRolesAsync`)
4. First `Project` (`Tenant`) org mein + uske **end-user** system roles (existing
   `IRbacService.EnsureSystemRolesAsync(tenant.Id)`)
5. `OrganizationMembership(Active)` + `MemberRole(org_owner)`
6. Audit: `account.created`, `organization.created`, `tenant.signup`

Result `(Account, Organization, Tenant)`. Login **account-based** (`IAccountService.
ValidateCredentialsAsync`, mirror `SuperAdminService`) + **tenant-agnostic** ("tenant
required" gate hatao). Active project default = recent membership ka pehla project.

> **Doc 02 replace:** purana `IAccountProjectService`/`ProjectMembership` → `IConsoleProvisioningService`
> + `OrganizationMembership` + `MemberRole`.

### NEW employee invite flow
`IInvitationService.InviteAsync(orgId, projectIdForEmail, email, operatorRoleIds, actor)` /
`AcceptAsync(rawToken, newPassword)`:
1. Email se global `Account` find-or-create (`PasswordHash=null` if naya)
2. `OrganizationMembership(Invited, InvitedByAccountId)` + chosen `MemberRole`s (org ke
   roles validate)
3. Single-use `AccountInviteToken` (SHA-256 hash store, raw token link mein) — pattern
   `PasswordResetToken` jaisa par `accounts.id` FK
4. Email existing `IMessagingService.DeliverAsync` se, naya `operator_invite` template.
   **Constraint:** messaging tenant-scoped hai (`MessageSendRequest.TenantId`), isliye email
   org ke active/first project provider se jaata hai. (Org-level provider config v1 mein nahi.)
5. `AcceptAsync`: token validate → `Account.PasswordHash` set, `EmailVerified=true`,
   membership `Status=Active`, token used. Public `InviteController` (`/invite/accept`,
   `[AllowAnonymous]`, tenant-less; `TenantResolutionMiddleware` exclusions mein `/invite` add).

### End-user invite se alag
End-user banana = Management API `POST /api/v1/users` → `IUserAdminService.CreateAsync`
(tenant-scoped `User`, `users`-FK tokens). Operator invite = global `accounts` +
`organization_memberships` + naya `accounts`-FK token. Alag tables/routes/cookies — dono
populations kabhi touch nahi karte.

---

## 8. Admin UI surfaces (naye)
`TenantAdmin` area mein, `RequireOperatorPermission` se gated:
- **Organization settings** — `OrganizationController` (rename/delete org), `org.manage`.
- **Members** — `MembersController` (list / invite / role-assign / remove); existing
  [UsersController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/UsersController.cs)
  + Users views mirror. "Last org owner remove nahi" guard. `member.*` gated.
- **Operator Roles** — `OperatorRolesController` + views, existing
  [RolesController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/RolesController.cs)
  ka near-copy par `IOperatorRbacService` drive kare. `role.manage` gated. System roles
  edit/delete-protected (`IsSystem` + `SystemRoleProtectedException` reuse).

---

## 9. Migration, tests, phasing

**Migration** `AddOrganizationsAccountsAndOperatorRbac`: `accounts`, `organizations`,
`organization_memberships`, `operator_roles`, `operator_permissions`,
`operator_role_permissions`, `member_roles`, `account_invite_tokens` banao; `tenants.
organization_id` NOT NULL + FK + index add. Koi backfill nahi (no data). **In tables pe RLS nahi.**

**Tests:** `TenantSignupTests` rewrite (Account + Organization + Tenant(OrgId) +
Membership(Active) + MemberRole(org_owner); slug dedup; operator + end-user system roles;
audits). Naye: `ConsoleAccessService.ResolveAsync` (member/non-member/project-not-in-org/
disabled), `OperatorRbacService.EnsureSystemRolesAsync` idempotency, `RequireOperatorPermission`
allow/deny, `InvitationService` invite+accept (token single-use, membership flips Active),
selector switch rejects non-member. `Tenant` banane wale fixtures ab `OrganizationId` set karein.

**Phasing (doc-02 ke phases 1-4 ko revise):**
1. **Org + Account identity** — entities, migration, repos (model `SuperAdminRepository`),
   `Tenant.OrganizationId`; signup Account+Org+Project+Membership; login account-based + tenant-agnostic.
2. **Operator RBAC** — `OperatorRbac` catalogue + entities, `IOperatorRbacService`,
   `IConsoleAccessService`, `RequireOperatorPermission`, guard rewrite; org-create pe system roles seed.
3. **Selector + new project** — `ConsoleSelectorViewComponent`, `WorkspaceController`,
   `IConsoleProvisioningService`.
4. **Members UI + invite** — `MembersController`, `OperatorRolesController`,
   `OrganizationController`, `InvitationService` + `InviteController` + `account_invite_tokens`
   + `operator_invite` template.
5. **Cleanup** — `users.is_tenant_admin`, `ITenantAdminService`, first-admin bootstrap hatao.

Uske baad: **SuperAdmin removal** (doc [04](04-remove-superadmin-self-host.md)) aur
**observability** (doc [05](05-pluggable-observability.md)) phases.

**Doc 02 ko update karna** — §3 entities (ProjectMembership/ProjectRole → Organization/
OrganizationMembership/MemberRole + operator RBAC + `Tenant.OrganizationId`), §4 cookie
(`OrgId` add), §4 guard (org-membership + project-in-org + operator-permission), §4 selector
(two-level), §4 signup/DI (`IConsoleProvisioningService`/`IConsoleAccessService`/
`IOperatorRbacService`/`IInvitationService`), §5 phases.

---

## 10. TL;DR
- Employees = **operators** = **Account** layer (console), end-user `User` se alag.
- Naya **Organization** entity company ke projects group kare + employee directory rakhe;
  access **org-level** grant. `ProjectMembership` → `OrganizationMembership`.
- `Tenant` ab org ke andar (`OrganizationId`). Environments = org ke sibling projects.
- **Custom operator RBAC** (alag from end-user RBAC), `PermissionEvaluator` reuse;
  system roles `org_owner`/`org_admin`/`project_admin`/`viewer`.
- **Two-level selector** (org → project). **Invite flow** for employees (global Account +
  org membership + `accounts`-FK token + email).
- Yeh doc 02 ko revise karta hai; implementation 5 phases (phir 04 + 05).
```
