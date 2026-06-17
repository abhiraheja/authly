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
Phase 0  UI Foundation (SAARVIX)         ✅ DONE (main 2c952a9)
Phase 1  Identity: Org + Account         ✅ DONE (feat/identity-org-account 01dfc2c)
Phase 2  Operator RBAC + guard           ✅ DONE (feat/operator-rbac)
Phase 3  Org→Project selector + new-project   ✅ DONE (feat/console-selector)
Phase 4  Members UI + employee invite    ✅ DONE (feat/members-invite)
Phase 5  Cleanup (IsTenantAdmin / legacy admin)   ✅ DONE (feat/cleanup-legacy-admin)
Phase 6  Remove SuperAdmin + self-host cleanup (monitoring → account surface)   ✅ DONE (feat/remove-superadmin)
Phase 7  Pluggable observability (OpenTelemetry)   ✅ DONE (feat/observability) — FINAL PHASE
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

**Status: ✅ DONE** (on `main`, commit `2c952a9`). One sub-task deferred.

**Tasks**
- [x] `saarvix-ui` skill repo mein available (done ✅).
- [x] Tailwind build: `package.json` + `tailwind.config.js` (SAARVIX `tw-config.js` token
      mappings + ≤8px radius remap port). `content` = `**/*.cshtml`.
- [x] Input CSS = SAARVIX `theme.css` tokens (`:root` light+dark) + `@layer components`.
      Output → `wwwroot/css/saarvix.css`. MSBuild pre-build `npm run build:css` + Dockerfile build stage.
- [x] `app.js` → `wwwroot/js/saarvix-app.js` (toasts/dropdowns/tabs/overlays data-attr API). **`shell.js` drop.**
- [x] Razor shell layouts re-implement (SAARVIX sidebar+topbar, server-side nav + `IsActive()`):
      `_AdminLayout.cshtml` (TenantAdmin), `_PortalLayout.cshtml` (Portal),
      `_AuthLayout.cshtml` (full-screen, no shell). Topbar mein selector placeholder + theme toggle + profile (P3 mein wire).
- [ ] Reusable Razor partials/tag-helpers: button, card, input, table, badge, modal, drawer, tabs. — **DEFERRED** (views use SAARVIX component classes directly; optional DRY pass later).
- [x] **Bootstrap remove** (CDN + `wwwroot/lib/bootstrap`). **jquery-validation-unobtrusive rakha** (`_ValidationScriptsPartial` ab jQuery khud load karta hai), message classes re-styled.
- [x] Re-skin existing **TenantAdmin** screens (Applications, Users, Roles, Branding, Messaging, Webhooks, ApiKeys, Onboarding, Sandbox, Security, AccessPolicies…). **SuperAdmin re-skin nahi kiya** (P6 mein delete).
- [x] Re-skin **Connect/Account** (login, register, consent, MFA, forgot/reset, magic-link) + **Portal**.

**Verify:** ✅ `npm run build:css` clean; app runs; saarvix.css served (200); light+dark via tokens; no `data-bs-`/`btn-soft`/`form-control` residue outside SuperAdmin; Lucide renders. *(Pixel-level pass of authenticated console screens pending tenant-admin creds.)*

---

## Phase 1 — Identity: Organization + Account
**Goal:** global Account + Organization + membership; projects org ke andar; account-based
signup/login. **Source:** doc 06 §4–§7 (authoritative), doc 02 (context).

**Status: ✅ DONE** (branch `feat/identity-org-account`, commit `01dfc2c`; 247 tests green; signup + account-login smoke-verified vs Postgres).

**Entities (new)** — `src/Authly.Core/Entities/`
- [x] `Account` (global; `Email` unique, `PasswordHash?` nullable, `Status`, names, `EmailVerified`, timestamps).
- [x] `Organization` (`Name`, `Slug` global-unique, `OwnerAccountId` **nullable** — platform-provisioned orgs ka owner null, `Settings` jsonb).
- [x] `OrganizationMembership` (`AccountId`, `OrganizationId`, `Status` Invited|Active|Disabled, `InvitedByAccountId?`). Unique `(AccountId, OrganizationId)`.
- [x] `Tenant.OrganizationId` (required FK, Restrict, index `idx_tenants_organization`). `Tenant.Slug` global-unique rahe.
- [x] Enums: `AccountStatus`, `MembershipStatus`.

**Data / DI**
- [x] AppDbContext: DbSets + config (mirror `SuperAdmin` block; snake_case, `gen_random_uuid()`/`NOW()`, enums `HasConversion<string>()`). **In tables pe RLS NAHI.**
- [x] Migration `AddOrganizationsAndAccounts` (+ `tenants.organization_id` NOT NULL, no backfill; dev DB drop+recreate to apply).
- [x] Repos: `AccountRepository`, `OrganizationRepository`, `OrganizationMembershipRepository` (mirror `SuperAdminRepository`); registered in `Infrastructure/DependencyInjection.cs`. (+ `ITenantRepository.ListByOrganizationAsync`, `CreateTenantRequest.OrganizationId`.)

**Auth / signup / login**
- [x] `AuthConstants`: added `TenantAdminClaims.AccountId`, `OrgId` (keep `TenantId` = active project).
- [x] `IAccountService.ValidateCredentialsAsync` (mirror `SuperAdminService`). Argon2id via existing `IPasswordHasher`.
- [x] `TenantSignupService` → create Account + Organization + first Tenant(OrgId) + `OrganizationMembership(Active)` + end-user system roles (+ operator owner role in P2). Result `(Account, Organization, Tenant)`.
- [x] `SignupController` + `TenantAdmin/AccountController` → sign in as Account (claims AccountId/OrgId/TenantId); login **tenant-agnostic** ("tenant required" gate removed; active org→first project from membership). *(Legacy SuperAdmin create-tenant auto-provisions a platform-owned org for the new NOT-NULL FK.)*
- [x] `TenantResolutionMiddleware` /tenantadmin fallback → reads active-project `TenantId` claim (already did).

**Tests:** ✅ `TenantSignupTests` rewritten (Account+Org+Tenant(OrgId)+Membership(Active) created; org & project slug dedup; duplicate-email rejected; end-user roles seeded; audits). Build + 247 tests green.

> **Deferred to Phase 2 (operator RBAC):** operator `MemberRole(org_owner)` seeding on org-create and the `TenantAdminControllerBase` guard rewrite (still TenantId-equality; `CurrentUserId` now = account id). Selector = Phase 3.

---

## Phase 2 — Operator RBAC + guard
**Goal:** custom operator roles/permissions gate console; membership+permission guard. **Source:** doc 06 §4.5, §5.

**Status: ✅ DONE** (branch `feat/operator-rbac`; 252 tests green; org_owner allow + no-perms deny smoke-verified vs Postgres).

**Tasks**
- [x] Entities: `OperatorRole`, `OperatorPermission`, `OperatorRolePermission`, `MemberRole` (keyed on `OrganizationMembershipId`). Org-scoped (`organization_id` column), **no RLS**.
- [x] `src/Authly.Core/Authorization/OperatorRbac.cs` (mirrors `SystemRbac.cs`): catalogue
      `project.{read,write,create,delete}`, `client.{read,manage}`, `enduser.{read,manage}`,
      `member.{read,invite,manage}`, `role.{read,manage}`, `observability.{read,manage}`, `org.{read,manage}`, `billing.{read,manage}` (19 perms).
      System roles: `org_owner`, `org_admin`, `project_admin`, `viewer`.
- [x] `IOperatorRbacService.EnsureSystemRolesAsync(orgId)` (mirrors `RbacService`); seeded on org-create (wired into signup → owner gets `org_owner` via `AssignSystemRoleAsync`).
- [x] `IConsoleAccessService.ResolveAsync(accountId, orgId, projectId)` → effective permission set (verifies membership Active + project∈org).
- [x] `RequireOperatorPermissionAttribute(permission)` action filter — reuses **`PermissionEvaluator.Satisfies`** verbatim; reads set cached by base guard in `HttpContext.Items`.
- [x] `TenantAdminControllerBase`: equality-check → membership+project-in-org check via `IConsoleAccessService` (resolved from `RequestServices`, no ctor churn); `CurrentAccountId` + `OrgId` added (`CurrentUserId` kept as alias); all 15 derived controllers' actions annotated with `[RequireOperatorPermission(...)]`.
- [x] Migration `AddOperatorRbac` (4 tables, no RLS).

**Tests:** ✅ `ResolveAsync` (member/non-member/project-not-in-org/disabled); `EnsureSystemRolesAsync` seeding + idempotency; signup grants org_owner. Build + 252 tests green.

> Test/send diagnostic POSTs (AccessPolicies.Test, Messaging.SendTest, Webhooks.Test) annotated `project.write` (side-effecting); revisit if read-tier preferred. Full operator-role CRUD UI = Phase 4.

---

## Phase 3 — Org→Project selector + new project
**Goal:** Google-style two-level selector + self-serve project create. **Source:** doc 06 §6, doc 02 §4.

**Status: ✅ DONE** (branch `feat/console-selector`; 253 tests green; selector + new-project auto-switch + switch-project smoke-verified vs Postgres).

**Tasks**
- [x] `IConsoleProvisioningService.CreateProjectAsync(orgId, name, actor)` — extracted slug-dedup project creation from `TenantSignupService` (now reused by signup); seeds end-user system roles + binds tenant context only when the request is tenant-less.
- [x] `WorkspaceController` (NOT base-derived; own membership check): `POST switch-org`, `POST switch-project`, `GET/POST new-project` (gated `project.create` via `IConsoleAccessService`) → re-issue cookie (new OrgId/TenantId) → auto-switch.
- [x] `ConsoleSelectorViewComponent` + wired into the SAARVIX topbar (Phase 0 placeholder → real): org (if >1) → project list (active checked) → "+ New project".
- [x] **Built in SAARVIX** (topbar dropdown via the data-attr menu API).

**Tests:** ✅ provisioning (create/seed/bind, slug dedup, no-slug error); switch-org/switch-project reject non-members + new-project auto-switch verified at runtime (controller flows exercised vs Postgres). Build + 253 green.

> `TenantContext` is set-once-per-request, so provisioning only binds context when none is set (console requests already have one; role/permission tables aren't RLS-protected, so seeding carries an explicit tenant_id).

---

## Phase 4 — Members UI + employee invite
**Goal:** invite employees as operators with roles. **Source:** doc 06 §7 (invite), §8 (UI).

**Status: ✅ DONE** (branch `feat/members-invite`; 266 tests green; build clean. Runtime acceptance pending.)

**Tasks**
- [x] `AccountInviteToken` entity (FK `accounts.id`, + `organization_id`; SHA-256 hash, single-use) + migration `AddAccountInviteTokens` + `IAccountInviteTokenRepository`. Mirrors `PasswordResetToken`.
- [x] `IInvitationService.InviteAsync(orgId, projectIdForEmail, email, operatorRoleIds, actor)` / `AcceptAsync(rawToken, newPassword)` (+ `FindPendingAsync`):
      find-or-create Account → `OrganizationMembership(Invited)` + `MemberRole`s → single-use token (7-day) → email via `IMessageQueue` (`operator_invite` template; sent through org's active project provider). Accept consumes token, sets PasswordHash (when none), EmailVerified, membership→Active; returns workspace for sign-in. New invite URL `IAuthUrlBuilder.BuildInviteAcceptUrl`.
- [x] Public `InviteController` (`/invite/accept`, `[AllowAnonymous]`, tenant-less; `/invite` added to `TenantResolutionMiddleware` exclusions) → signs the operator into the console (same claim shape as signup). `Accept`/`Invalid` views on `_AuthLayout`.
- [x] `operator_invite` built-in template (`BuiltInTemplates` + `MessageTemplateKeys.OperatorInvite`; `action_url` required-variable guard).
- [x] UI (SAARVIX): `MembersController` (list/invite/role-assign/remove + remove-member; mirrors `UsersController`; "last owner can't be removed" guard via `IMemberDirectoryService` + `IOperatorRbacService`); `OperatorRolesController` (near-copy of `RolesController`/Views, drives extended `IOperatorRbacService` role CRUD; system roles protected); `OrganizationController` (rename always; delete guarded — refused while projects exist due to Restrict FK; `org.read`/`org.manage`). New "Organization" sidebar group.
- [x] Distinct from end-user invite (`IUserAdminService`); operates only on the global Account/Org layer.

**Tests:** ✅ invite creates account+membership(Invited)+role+token+email; accept sets password/verifies/activates + token single-use; re-invite active member rejected; invalid token → null; operator-role CRUD (create/dedup/set-perms-filtering/system-protected-delete); member-role assign/remove + last-owner guard (role strip + member removal); member directory listing. Build + 266 green.

> Org **delete** only succeeds for an org with zero projects (projects use a Restrict FK; project-delete lands in Phase 6) — surfaced as a clear guard, not a DB error. Removing a member sets membership→Disabled and clears role grants (ConsoleAccess already gates on Active), keeping the unique (account, org) row for clean re-invite + audit history.

---

## Phase 5 — Cleanup (legacy admin)
**Goal:** remove the pre-Account admin path. **Source:** doc 06 §9-P5, doc 02 §IsTenantAdmin.

**Status: ✅ DONE** (branch `feat/cleanup-legacy-admin`; 266 tests green; build clean.)

**Tasks**
- [x] Removed `User.IsTenantAdmin` (+ AppDbContext mapping), `IUserRepository.AnyTenantAdminAsync` (+ impl), the entire `Authly.Modules.TenantAdmins` module (`ITenantAdminService`/`TenantAdminService` + first-admin bootstrap) + DI registration. `ITenantAdminService` had no consumers — console login is account-based since Phase 1.
- [x] Migration `DropIsTenantAdmin` (drops `users.is_tenant_admin`).
- [x] Consumers updated: `MfaService.IsAdminAsync` now role-only (`tenant_admin`/`super_admin`); `UserResponse` API DTO drops the `IsTenantAdmin` field; TenantAdmin Users/Index view drops the "Admin" column. End-user `RbacService.EnsureSystemRolesAsync(tenantId)` (incl. the `tenant_admin` role) on project-create is **kept**.

**Tests:** ✅ MFA AdminsOnly test now marks the admin via the `tenant_admin` role (seedable `FakeUserRoleRepo`); removed `AnyTenantAdminAsync` from all in-memory user-repo fakes. Build + 266 green.

---

## Phase 6 — Remove SuperAdmin + self-host cleanup
**Goal:** delete the platform-operator surface; move monitoring to account surface. **Source:** doc 04.

**Status: ✅ DONE** (branch `feat/remove-superadmin`; 257 tests green; build clean.)

**Tasks**
- [x] Deleted `Areas/SuperAdmin/**`, `SuperAdmin` entity/enum/repo/service, `AuthSchemes.SuperAdmin`/`AuthPolicies.SuperAdmin`/`SuperAdminClaims`, `SuperAdminIpAllowlistMiddleware`; `Program.cs` super-admin cookie scheme + policy + `EnsureSeededAsync` bootstrap + `SUPERADMIN_ENABLED` gate + `/superadmin` 404 + IP-allowlist wiring (default auth scheme now `User`); Home page super-admin link. Migration `RemoveSuperAdminAndCloudTables` (drops `super_admins`, `announcements`, `self_hosted_instances`).
- [x] Dropped cloud-only: **Announcements** (entity/repo/service + tenant-admin banner — `TenantBanners` now onboarding-only), **SelfHostedInstance** + `SelfHostSyncService` + `/api` sync ingest (`SyncController`) + `SelfHostSyncJob` (Hangfire job removed), `IDeploymentContext`/`DeploymentContext` (+ `DEPLOYMENT_MODE`/`SYNC_*` reads + boot disclosure audit), tenant **suspend/reactivate** (`ITenantService`/`TenantService`). `SyncPayload`/`InstanceRegistration` contracts removed.
- [x] **Moved** monitoring to the console: `TenantAdmin/MonitoringController` (`observability.read`) reuses `IPlatformHealthProbe` + `IInstanceMetricsCollector` + `ILoginAnalyticsStore`; SAARVIX page (health, project-scoped totals + org project count, 14-day login analytics; version from entry assembly). Sidebar "Monitoring" entry. *(Runtime fix: the metrics collector + login-analytics store no longer rebind the set-once tenant context — they read under the request's already-bound project, RLS-safe — since the page now runs in a tenant-bound `/tenantadmin` request, not the old tenant-less SuperAdmin scope.)*
- [x] **Folded** tenant **Delete** → `TenantAdmin/SettingsController` (`project.delete`, slug-confirm) → `TenantService.DeleteAsync`; auto-switches to another project in the org or signs out. Sidebar "Project settings" entry.
- [x] Audit logging kept (only `super_admin` actor paths removed with the surface). `SystemRbac.SuperAdmin` end-user role retained (unrelated to the deleted platform surface).

**Tests:** ✅ removed SuperAdmin/Announcement/SelfHostSync/Deployment tests + dead fakes; `TenantService` suspend test → delete test; MFA admin check already role-based. Build + 257 green.

> `IInstanceMetricsCollector`/`PlatformHealthProbe`/`LoginAnalyticsStore` (Infrastructure) + their DI kept — they back the relocated page. Hangfire dashboard gate is now "any authenticated principal" in non-dev (was super-admin).

---

## Phase 7 — Pluggable observability
**Goal:** opt-in OpenTelemetry, admin-configured. **Source:** doc 05.

**Status: ✅ DONE** (branch `feat/observability`; 262 tests green; build clean. Runtime Grafana verify pending.)

**Tasks**
- [x] `ObservabilityConfig` global single-row entity (no TenantId, no RLS): `Enabled`, `Exporter` (otlp|azure_monitor), `OtlpEndpoint`, `OtlpHeadersEncrypted`, `AzureConnectionStringEncrypted`, `Signals`, `SamplingRatio`, `LogStreamEndpoint`, `LogStreamKeyEncrypted`. Secrets via `IEncryptionService`. Repo `IObservabilityConfigRepository` (upsert). Migration `AddObservabilityConfig`.
- [x] `Authly.Modules.Observability.IObservabilityConfigService`: `GetSettingsAsync` (decrypted runtime), `GetForEditAsync` (secret-free view + `Has*` flags), `SaveAsync` (encrypt-on-write / blank-keeps-existing, signal normalize, sampling clamp, audit `observability.config_saved`).
- [x] `Program.cs` `AddAuthlyObservability()` (`ObservabilityStartup`): reads stored config at startup (best-effort, env fallback `OTEL_EXPORTER_OTLP_ENDPOINT`), wires `AddOpenTelemetry().WithTracing/WithMetrics` (+ASP.NET Core/HttpClient/Runtime instrumentation, `TraceIdRatioBasedSampler`) and `builder.Logging.AddOpenTelemetry`; OTLP **or** Azure Monitor exporter per `Exporter`; absent/disabled → nothing exported. Packages added (OpenTelemetry 1.16, instrumentation, Azure.Monitor exporter).
- [x] `TelemetryEnrichmentProcessor` stamps `tenant.id`/`project.id`/`org.id`/`account.id` from the request principal's claims.
- [x] `LogStreamJob` re-sourced from config (endpoint/key from `ObservabilityConfig`, env `LOG_STREAM_*` fallback); recurring job always scheduled, self-guards (no endpoint → no-op).
- [x] SAARVIX admin: `TenantAdmin/ObservabilityController` (`observability.read`/`observability.manage`) — write-only encrypted secrets, "applies on restart" note, sidebar entry.
- [x] `docker-compose.observability.yml` overlay — OTel Collector → Tempo + Prometheus + Loki → Grafana (provisioned datasources); app OTLP → collector via env. Configs under `observability/`.

**Tests:** ✅ `ObservabilityConfigService` (encrypt-on-write, blank-keeps-secret, signal normalize/clamp, secret-presence view, decrypt for runtime, disabled-when-no-row). Build + 262 green.

**Verify:** ✅ Runtime acceptance (docker compose, current `main`): all migrations applied (account_invite_tokens + observability_config present; super_admins/announcements/self_hosted_instances dropped; users.is_tenant_admin gone); deleted `/superadmin/**` routes 404; signup → console cookie; all six new console pages render 200 authenticated (members, operator-roles, organization, settings, monitoring, observability); observability save persists with **encrypted** OTLP header (no plaintext leak) + `observability.config_saved` audit. Grafana trace/metric/log visualization via the compose overlay still to be eyeballed.

---

## 3. Migrations (sequence, pre-prod)
1. `AddOrganizationsAndAccounts` (+ `tenants.organization_id`) — P1
2. `AddOperatorRbac` — P2
3. `AddAccountInviteTokens` — P4 ✅
4. `DropIsTenantAdmin` — P5 ✅
5. `RemoveSuperAdminAndCloudTables` (drop `super_admins`, `announcements`, `self_hosted_instances`) — P6 ✅
6. `AddObservabilityConfig` — P7 ✅

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
- [x] Signup → Account+Organization+first Project; login account-based, tenant-agnostic. *(Phase 1)*
- [x] Console: org→project selector switches; "New project" self-serve; non-member access rejected. *(Phase 3)*
- [x] Employees invited as operators with custom operator roles; **permissions gate console actions ✅ (Phase 2)**; end-user `User`/RBAC untouched ✅; invite flow + member/role-CRUD UI ✅ *(Phase 4)*.
- [x] SuperAdmin gone; monitoring on account surface; tenant delete in project settings; cloud-only features removed. *(Phase 6)*
- [x] Observability opt-in (OpenTelemetry); nothing ships unconfigured; local Grafana stack overlay provided. *(Phase 7 — runtime Grafana verify pending)*
- [x] Entire UI on SAARVIX (compiled Tailwind), light+dark, responsive; no Bootstrap residue *(except SuperAdmin, deleted P6)*. *(Phase 0)*
- [x] All tests green *(262 green now)*; docs 02/03 noted as superseded by 06/04.

---

## 6. First concrete step
**Phase 0, task 1:** Tailwind build set up karo (`package.json` + `tailwind.config.js` +
SAARVIX `theme.css` → `wwwroot/css/saarvix.css`) aur ek shell layout (`_AdminLayout.cshtml`)
SAARVIX mein convert karke ek screen (e.g. Applications/Index) re-skin karke verify karo.
Yeh foundation set karta hai; uske baad baaki Phase 0 + Phase 1 backbone.
