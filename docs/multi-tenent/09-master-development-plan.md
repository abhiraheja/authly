# Master Development Plan — Authly (consolidated)

> Yeh `docs/multi-tenent/` ke saare 8 docs ko **ek executable plan** mein baandhta hai:
> kya banana hai, **pehle kya**, saare phases + tasks, migrations, tests, aur done ki
> definition. Detail ke liye har phase apne source doc ko point karta hai.
>
> **Pre-condition:** pre-production (244 tests green, no real data) → clean refactors +
> drop/recreate migrations OK. Stack locked: **ASP.NET Core MVC + Razor + OpenIddict +
> EF/PostgreSQL** (doc 07). UI = **SAARVIX (compiled Tailwind)** (doc 08).

---

## 0. Source docs (kis phase ka detail kahan)
| Doc | Topic |
|---|---|
| [01](01-firebase-model-explained.md) | Firebase/Google model — concept |
| [02](02-decision-and-plan.md) | Account model (⚠ **06 ne revise kiya** → org-centric) |
| [03](03-superadmin-role.md) | SuperAdmin analysis (⚠ **04 ne reverse kiya**) |
| [04](04-remove-superadmin-self-host.md) | SuperAdmin removal + self-host cleanup |
| [05](05-pluggable-observability.md) | Pluggable OpenTelemetry observability |
| [06](06-organizations-and-operator-rbac.md) | **Organizations + employees + operator RBAC** (authoritative identity model) |
| [07](07-framework-choice.md) | Stack stays MVC+Razor (no rewrite) |
| [08](08-ui-revamp-saarvix.md) | SAARVIX UI revamp |

---

## 1. End-state (kya ban raha hai — ek paragraph)
Authly ek **pure self-host** IDaaS hoga. Ek global **Account** (employee login) ek ya
zyada **Organizations** ka member hota hai; har Organization apne **Projects (= Tenants,
= environments)** group karta hai aur ek **employee directory** rakhta hai. Operators ko
**custom operator-RBAC roles** (org-scoped) milte hain jo console actions gate karte hain —
yeh **app end-users (`User`) + unke RBAC se bilkul alag** hai. Console mein **org→project
selector**, self-serve **new project**, aur **employee invite** flow hai. **SuperAdmin
hata diya** (self-host owner hi platform owner). Logging **pluggable OpenTelemetry**
(opt-in, admin-configured). Poora UI **SAARVIX (compiled Tailwind)** design system pe.

Do populations (kabhi mat milao):
- **Operators / employees** → global **`Account`** (console). OAuth token kabhi nahi.
- **App end-users** → tenant-scoped **`User`** (hosted login → OAuth tokens). Unchanged.

---

## 2. Pehle kya? — recommended order (dependency-driven)

```
Phase 0  UI Foundation (SAARVIX)         ← pehle: baaki sab isi pe bane, no rework
Phase 1  Identity: Org + Account         ← backbone (sab features ispe khade)
Phase 2  Operator RBAC + guard
Phase 3  Org→Project selector + new-project
Phase 4  Members UI + employee invite
Phase 5  Cleanup (IsTenantAdmin / legacy admin)
Phase 6  Remove SuperAdmin + self-host cleanup (monitoring → account surface)
Phase 7  Pluggable observability (OpenTelemetry)
```
- **UI re-skin** ek **parallel track** hai: Phase 0 foundation ke baad existing screens
  area-by-area re-skin; saare NAYE feature screens (P3/P4/P6/P7) **seedha SAARVIX** mein.
- Har phase **independently green ship** hota hai (`dotnet build` + `dotnet test` pass).
- **Skill ready** ✅ — `saarvix-ui` repo ke `.claude/skills/` mein copy ho chuka.

> Kyun yeh order: Phase 0 pehle taaki har naya screen SAARVIX mein bane (do baar na banana
> pade). Phase 1 identity backbone hai — selector/members/operator-RBAC sab ispe depend
> karte. SuperAdmin removal (6) Account surface ke baad, taaki monitoring wahan move ho
> sake. Observability (7) sabse independent → last.

---

## Phase 0 — UI Foundation (SAARVIX)
**Goal:** ek baar SAARVIX foundation khada karo; aage sab isi pe bane. **Source:** doc 08.

**Tasks**
- [ ] `saarvix-ui` skill repo mein available (done ✅).
- [ ] Tailwind build: `package.json` + `tailwind.config.js` (SAARVIX `tw-config.js` token
      mappings + ≤8px radius remap port). `content` = `**/*.cshtml`.
- [ ] Input CSS = SAARVIX `theme.css` tokens (`:root` light+dark) + `@layer components`.
      Output → `wwwroot/css/saarvix.css`. MSBuild pre-build `npm run build:css` + Dockerfile build stage.
- [ ] `app.js` → `wwwroot/js/saarvix-app.js` (toasts/dropdowns/tabs/overlays data-attr API). **`shell.js` drop.**
- [ ] Razor shell layouts re-implement (SAARVIX sidebar+topbar, server-side nav + `IsActive()`):
      `_AdminLayout.cshtml` (TenantAdmin), `_PortalLayout.cshtml` (Portal),
      `_AuthLayout.cshtml` (full-screen, no shell). Topbar mein selector + theme toggle + profile (placeholder selector; P3 mein wire).
- [ ] Reusable Razor partials/tag-helpers: button, card, input, table, badge, modal, drawer, tabs.
- [ ] **Bootstrap remove** (CDN + `wwwroot/lib/bootstrap`). **jquery-validation-unobtrusive rakho**, message classes re-style.
- [ ] Re-skin existing **TenantAdmin** screens (Applications, Users, Roles, Branding, Messaging, Webhooks, ApiKeys, Onboarding, Sandbox, Security, AccessPolicies…). **SuperAdmin re-skin mat karo** (P6 mein delete).
- [ ] Re-skin **Connect/Account** (login, register, consent, MFA, forgot/reset, magic-link) + **Portal**.

**Verify:** `npm run build:css` clean; app runs; light+dark; responsive ≤1024; koi `data-bs-`/`btn-`/`col-`残 nahi; Lucide render.

---

## Phase 1 — Identity: Organization + Account
**Goal:** global Account + Organization + membership; projects org ke andar; account-based
signup/login. **Source:** doc 06 §4–§7 (authoritative), doc 02 (context).

**Entities (new)** — `src/Authly.Core/Entities/`
- [ ] `Account` (global; `Email` unique, `PasswordHash?` nullable, `Status`, names, `EmailVerified`, timestamps).
- [ ] `Organization` (`Name`, `Slug` global-unique, `OwnerAccountId`, `Settings` jsonb).
- [ ] `OrganizationMembership` (`AccountId`, `OrganizationId`, `Status` Invited|Active|Disabled, `InvitedByAccountId?`). Unique `(AccountId, OrganizationId)`.
- [ ] `Tenant.OrganizationId` (required FK, Restrict, index). `Tenant.Slug` global-unique rahe.
- [ ] Enums: `AccountStatus`, `MembershipStatus`.

**Data / DI**
- [ ] AppDbContext: DbSets + config (mirror `SuperAdmin` block; snake_case, `gen_random_uuid()`/`NOW()`, enums `HasConversion<string>()`). **In tables pe RLS NAHI.**
- [ ] Migration `AddOrganizationsAndAccounts` (+ `tenants.organization_id` NOT NULL, no backfill).
- [ ] Repos: `AccountRepository`, `OrganizationRepository`, `OrganizationMembershipRepository` (mirror `SuperAdminRepository`); register in `Infrastructure/DependencyInjection.cs`.

**Auth / signup / login**
- [ ] `AuthConstants`: add `TenantAdminClaims.AccountId`, `OrgId` (keep `TenantId` = active project).
- [ ] `IAccountService.ValidateCredentialsAsync` (mirror `SuperAdminService`). Argon2id via existing `IPasswordHasher`.
- [ ] `TenantSignupService` → create Account + Organization + first Tenant(OrgId) + `OrganizationMembership(Active)` (+ owner role in P2). Result `(Account, Organization, Tenant)`.
- [ ] `SignupController` + `TenantAdmin/AccountController` → sign in as Account (claims AccountId/OrgId/TenantId); login **tenant-agnostic** (remove "tenant required" gate).
- [ ] `TenantResolutionMiddleware` /tenantadmin fallback → read active-project `TenantId` claim (trivial; already does).

**Tests:** rewrite `TenantSignupTests` (Account+Org+Tenant+Membership created; slug dedup; audits). Build+test green (behaves like today: one org/one project each).

---

## Phase 2 — Operator RBAC + guard
**Goal:** custom operator roles/permissions gate console; membership+permission guard. **Source:** doc 06 §4.5, §5.

**Tasks**
- [ ] Entities: `OperatorRole`, `OperatorPermission`, `OperatorRolePermission`, `MemberRole` (keyed on `OrganizationMembershipId`). Org-scoped (`organization_id` column), **no RLS**.
- [ ] `src/Authly.Core/Authorization/OperatorRbac.cs` (mirror `SystemRbac.cs`): catalogue
      `project.{read,write,create,delete}`, `client.manage`, `enduser.manage`,
      `member.{read,invite,manage}`, `role.manage`, `observability.{read,manage}`, `org.manage`, `billing.manage`.
      System roles: `org_owner`, `org_admin`, `project_admin`, `viewer`.
- [ ] `IOperatorRbacService.EnsureSystemRolesAsync(orgId)` (mirror `RbacService`); seed on org-create (wire into Phase 1 signup → owner gets `org_owner`).
- [ ] `IConsoleAccessService.ResolveAsync(accountId, orgId, projectId)` → effective permission set (verify membership Active + project∈org). Mirror `RbacService.GetUserAuthorizationAsync`.
- [ ] `RequireOperatorPermissionAttribute(permission)` action filter — reuse **`PermissionEvaluator.Satisfies`** verbatim; reads set cached by base guard.
- [ ] `TenantAdminControllerBase`: equality-check → membership+project-in-org check via `IConsoleAccessService`; `CurrentUserId`→`CurrentAccountId`; add `OrgId`; thread dependency into ~16 derived controllers; annotate actions with `[RequireOperatorPermission(...)]`.
- [ ] Migration `AddOperatorRbac`.

**Tests:** `ResolveAsync` (member/non-member/project-not-in-org/disabled); `EnsureSystemRolesAsync` idempotency; attribute allow/deny.

---

## Phase 3 — Org→Project selector + new project
**Goal:** Google-style two-level selector + self-serve project create. **Source:** doc 06 §6, doc 02 §4.

**Tasks**
- [ ] `IConsoleProvisioningService.CreateProjectAsync(orgId, name)` — extract slug-dedup `CreateWorkspaceAsync` from `TenantSignupService`; seeds end-user system roles for the new project.
- [ ] `WorkspaceController` (NOT base-derived; own membership check): `POST switch-org`, `POST switch-project`, `GET/POST new-project` (gated `project.create`) → re-issue cookie (new OrgId/TenantId) → auto-switch.
- [ ] `ConsoleSelectorViewComponent` + wire into the SAARVIX topbar (Phase 0 placeholder → real): org (if >1) → project list (active checked) → "+ New project".
- [ ] **Built in SAARVIX** (mirror Google "Select a project" modal).

**Tests:** switch rejects non-member org/project; new-project creates Tenant in active org + auto-switch.

---

## Phase 4 — Members UI + employee invite
**Goal:** invite employees as operators with roles. **Source:** doc 06 §7 (invite), §8 (UI).

**Tasks**
- [ ] `AccountInviteToken` entity (FK `accounts.id`; SHA-256 hash) + migration. Mirror `PasswordResetToken` pattern.
- [ ] `IInvitationService.InviteAsync(orgId, projectIdForEmail, email, operatorRoleIds, actor)` / `AcceptAsync(rawToken, newPassword)`:
      find-or-create Account → `OrganizationMembership(Invited)` + `MemberRole`s → single-use token → email via existing `IMessagingService` (`operator_invite` template; sent through org's active project provider).
- [ ] Public `InviteController` (`/invite/accept`, `[AllowAnonymous]`, tenant-less; add `/invite` to `TenantResolutionMiddleware` exclusions). `AcceptAsync` sets PasswordHash, EmailVerified, membership→Active.
- [ ] `operator_invite` built-in template (`BuiltInTemplates`).
- [ ] UI (SAARVIX): `MembersController` (list/invite/role-assign/remove; mirror `UsersController` + Users views; "last owner can't be removed" guard); `OperatorRolesController` (near-copy of `RolesController`/Views, drives `IOperatorRbacService`; system roles protected); `OrganizationController` (rename/delete org; `org.manage`).
- [ ] Keep distinct from end-user invite (`POST /api/v1/users` → `IUserAdminService`).

**Tests:** invite+accept (token single-use, membership flips Active); member/role CRUD; operator-role permission management.

---

## Phase 5 — Cleanup (legacy admin)
**Goal:** remove the pre-Account admin path. **Source:** doc 06 §9-P5, doc 02 §IsTenantAdmin.

**Tasks**
- [ ] Remove `User.IsTenantAdmin`, `IUserRepository.AnyTenantAdminAsync`, `ITenantAdminService`/`TenantAdminService`, first-admin bootstrap.
- [ ] Migration: drop `users.is_tenant_admin`.
- [ ] Keep end-user `EnsureSystemRolesAsync(tenantId)` on project-create.

**Tests:** update any tests referencing the removed flag/service.

---

## Phase 6 — Remove SuperAdmin + self-host cleanup
**Goal:** delete the platform-operator surface; move monitoring to account surface. **Source:** doc 04.

**Tasks**
- [ ] Delete `src/Authly.Web/Areas/SuperAdmin/**`, `SuperAdmin` entity/service/repo, `AuthSchemes.SuperAdmin`/`AuthPolicies.SuperAdmin`/`SuperAdminClaims`; `Program.cs` super-admin cookie + bootstrap + `SUPERADMIN_ENABLED` gate + `SuperAdminIpAllowlistMiddleware`. Migration: drop `super_admins`.
- [ ] Drop cloud-only: **Announcements** (entity/service/controller + tenant-admin banner), **SelfHostedInstance** + `SelfHostSyncService` + sync ingest, `DEPLOYMENT_MODE`/`SYNC_*`, tenant **suspend/reactivate**.
- [ ] **Move** monitoring/health/login-analytics (`IPlatformHealthProbe`, `InstanceMetricsCollector`, `LoginAnalyticsStore`) → read-only **Account/admin** page (SAARVIX). Box-health global; per-tenant metrics scoped to the account's projects.
- [ ] **Fold** tenant **Delete** → project settings owner action (`TenantService.DeleteAsync`, `project.delete`).
- [ ] Keep audit logging (drop only `super_admin` actor paths).

**Tests:** remove SuperAdmin tests; add monitoring-page + project-delete tests.

---

## Phase 7 — Pluggable observability
**Goal:** opt-in OpenTelemetry, admin-configured. **Source:** doc 05.

**Tasks**
- [ ] `ObservabilityConfig` global singleton entity (no TenantId): `Enabled`, `Exporter` (otlp|azure_monitor), `OtlpEndpoint`/`OtlpHeaders` (enc), `AzureConnectionString` (enc), `Signals`, `SamplingRatio`. Secrets via `IEncryptionService`. Migration (no RLS).
- [ ] `Program.cs`: `AddOpenTelemetry().WithLogging/WithTracing/WithMetrics` + OTLP (+ Azure Monitor) exporter, **read stored config at startup; absent → no exporter**. Env fallback `OTEL_EXPORTER_OTLP_ENDPOINT`. (Changes apply on restart — document in UI.)
- [ ] Enrichment processor: stamp `tenant.id`/`project.id` (+account) from `ITenantContext`.
- [ ] Re-source `LogStreamJob` target from config (keep job).
- [ ] Admin surface (SAARVIX): instance-global **Observability** menu (`observability.manage`) — mirror `MessagingProvider` BYOK UX (write-only secrets, encrypt, audit).
- [ ] docker-compose: optional overlay `docker-compose.observability.yml` — OTel Collector → Loki + Tempo + Prometheus → Grafana (pre-provisioned). App OTLP → collector.

**Verify:** configure → generate activity → see logs/traces/metrics in Grafana, filter by project.

---

## 3. Migrations (sequence, pre-prod)
1. `AddOrganizationsAndAccounts` (+ `tenants.organization_id`) — P1
2. `AddOperatorRbac` — P2
3. `AddAccountInviteTokens` — P4
4. `DropIsTenantAdmin` — P5
5. `RemoveSuperAdminAndCloudTables` (drop `super_admins`, `announcements`, `self_hosted_instances`) — P6
6. `AddObservabilityConfig` — P7

> Pre-prod (no data): chaaho to recreate-from-scratch bhi kar sakte ho; warna additive migrations + targeted drops.

---

## 4. Cross-cutting invariants (har phase mein dhyaan)
- **Do populations alag:** Account (operators) ≠ User (end-users). Alag tables/cookies/tokens/routes.
- **Global vs RLS:** `accounts`, `organizations`, memberships, operator-RBAC, invite-tokens, observability = **global, NO RLS**. Tenant-scoped (`users` etc.) RLS unchanged. `ITenantContext` set-once-per-request.
- **Reuse, don't reinvent:** `PermissionEvaluator`, `IEncryptionService`, `IMessagingService`, `IRbacService` patterns, `SuperAdminRepository` shape, `MessagingProvider` BYOK UX.
- **New screens = SAARVIX** always; SuperAdmin never re-skinned (deleted P6).
- **Each phase ships green:** `dotnet build` + `dotnet test` pass before next.

---

## 5. Definition of Done (whole effort)
- [ ] Signup → Account+Organization+first Project; login account-based, tenant-agnostic.
- [ ] Console: org→project selector switches; "New project" self-serve; non-member access rejected.
- [ ] Employees invited as operators with custom operator roles; permissions gate console actions; end-user `User`/RBAC untouched.
- [ ] SuperAdmin gone; monitoring on account surface; tenant delete in project settings; cloud-only features removed.
- [ ] Observability opt-in (OpenTelemetry); nothing ships unconfigured; local Grafana stack works.
- [ ] Entire UI on SAARVIX (compiled Tailwind), light+dark, responsive; no Bootstrap residue.
- [ ] All tests green; docs 02/03 noted as superseded by 06/04.

---

## 6. First concrete step
**Phase 0, task 1:** Tailwind build set up karo (`package.json` + `tailwind.config.js` +
SAARVIX `theme.css` → `wwwroot/css/saarvix.css`) aur ek shell layout (`_AdminLayout.cshtml`)
SAARVIX mein convert karke ek screen (e.g. Applications/Index) re-skin karke verify karo.
Yeh foundation set karta hai; uske baad baaki Phase 0 + Phase 1 backbone.
