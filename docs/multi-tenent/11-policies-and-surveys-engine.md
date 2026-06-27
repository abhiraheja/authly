# Policies & Surveys Engine — "Consent / Engagement" Platform

> Is doc ka kaam: Authly me ek **org-level Policies + Surveys engine** ka poora development
> plan capture karna — har woh decision jo user ke saath discuss hua, saari clarifying Q&A,
> teeno phases ke detailed tasks, data model, enforcement gate design, aur survey-system
> research. Yeh **single source of truth** hai is feature ke liye. Code abhi START nahi hua
> (branch `feat/policies-engine` bana hai); yeh doc development se PEHLE likha gaya hai.

---

## 0. Problem statement (kyun bana rahe hain)

Aaj Authly me:

- Signup par ek **hardcoded** `AcceptTerms` checkbox hai
  ([RegisterViewModel.AcceptTerms](../../src/Authly.Web/Models/AccountViewModels.cs)).
- Uske links `/legal/terms` aur `/legal/privacy`
  ([Register.cshtml](../../src/Authly.Web/Views/Account/Register.cshtml)) **kahin route hi
  nahi karte → 404**.
- Tenant/admin apni **khud ki** Terms & Conditions / Privacy Policy ya koi bhi norm
  **configure nahi kar sakta**, na enforce kar sakta hai. `/privacy`
  ([Home/Privacy.cshtml](../../src/Authly.Web/Views/Home/Privacy.cshtml)) sirf ek placeholder
  text hai.
- Consent **capture** ka mechanism hai ([ConsentRecord](../../src/Authly.Core/Entities/ConsentRecord.cs)
  + [ConsentService](../../src/Authly.Modules/Compliance/ConsentService.cs)) par jo **document**
  user accept karta hai uska content/version admin set nahi kar sakta.

**User ki demand (verbatim intent):**

1. Ek **new flow** chahiye Terms & Conditions aur "koi bhi norms" ke liye jo admin chahta hai
   ki uske users accept karein.
2. Example: agar user ne T&C accept nahi ki to signup na ho. Agar **social login** se aaya to
   bhi terms accept hone chahiye (bypass na ho).
3. Admin ke paas option ho ki wo **koi bhi ek policy** bana sake, jise user ke paas
   **accept / reject / skip** ka option ho.
4. Policy banate hi **draft** me jaye; **publish** karte hi admin se poocha jaye ki iska
   **kya impact** hai:
   - **(a)** Sign-in rokna hai — reject/accept-na-karne par login na ho, skip bhi na ho.
   - **(b)** Skip kar sakta hai, par **har baar sign-in** par poocha jaye.
   - **(c)** Reject karde tab bhi login ho jaye (sirf opinion lena hai).
5. **Bypass handling:** agar kisi tarah bypass ho gaya (e.g. social sign-in pe terms nahi
   liye), to **agli baar login par zaroor poocha jaye**, warna login na ho.
6. Norms ko simple banane ke 2 options: admin **HTML paste** kare, **ya PDF upload** kare.
   Sign-in par user use dekh sake.
7. Account portal (`/portal/privacy` jaisa) me ek **new menu** ho jahan user ki saari
   accept/reject history dikhe.
8. **Faida:** organization-level policies Authly handle karega — client orgs ko apne system me
   yeh banane ki zaroorat nahi.

**Aur (scope expansion — surveys):**

9. Issi tarah ek **survey system** bhi — company time-to-time apne employees/users se survey
   ya kuch bhi pooch sake. Skip allowed hai ya nahi, admin portal me decide kare.
10. Survey kuch bhi ho sakta hai: admin **Title, description**, aur jo bhi survey ke liye
    zaroori hota hai sab daale. Skip ki **end date** ho (kab tak skip hoga).
11. **Targeting fully customizable:** admin decide kare survey/norms **kin users** se lena hai —
    saare users, ya kisi **specific application** (multiple apps me se), ya **sirf social-login**
    wale, ya **sirf password** wale. "Fully customize system chahiye."
12. Survey **time-based** ho sakta hai (admin choose kare). Questions **randomly** aa sakte
    hain. "Jitni bhi possibility internet par hai sab" — comprehensive survey system chahiye.

---

## 1. Final decisions (user ke saath confirm — ek-ek baat)

Yeh decisions clarifying questions ke through finalize hue. Har ek implementation me bake hoga.

### 1.1 Architecture
- **Unified core, do item-types.** Policy aur Survey ek hi **delivery + targeting + gating +
  portal-history + lifecycle** engine share karenge. Sirf "content" alag hota hai:
  - **Policy** = ek document (HTML ya PDF) jise accept/reject/skip karna hai.
  - **Survey** = questions ka set jise answer/skip karna hai.

### 1.2 Phasing (user ne "Phased" choose kiya)
- **Phase 1** — Policies/Consent + enforcement + **basic** targeting (single-dimension) +
  portal history. *(Yeh core, sabse pehle ship hoga.)*
- **Phase 2** — Surveys (full multi-question builder + runner + reporting).
- **Phase 3** — Advanced targeting (AND/OR combos, audience preview, role/identity-based).

### 1.3 Enforcement modes (publish-time "impact" choice)
- **`Mandatory`** — jab tak user decide (accept) na kare, **block**. Skip/reject se aage nahi.
- **`SkippableUntil`** — skip-deadline tak skip allowed; **har naye sign-in par dobara
  poocha** jaye. Deadline ke baad → mandatory (block).
- **`Optional`** — kabhi block na ho. Accept/reject/skip sab allowed; sirf **opinion record**.

### 1.4 Do alag dates (research se bhi confirm — `StartDate`/`EndDate`/grace alag hote hain)
- **`SkipDeadline`** (grace window end) — iske baad skip band → mandatory ho jaata hai.
- **`CloseDate`** — iske baad item **poori tarah band**: na dikhega, na block karega.
- **`StartsAt`** (optional) — iske pehle dikhega hi nahi.

### 1.5 Post-deadline / post-close behavior (user ne explicitly confirm kiya)
- Skip-deadline ke baad, close se pehle → **block** (sirf accept/answer se aage).
- **Survey** close-date ke baad (user ne kabhi respond nahi kiya) → **chhod do**, normal login
  ho jaye; us user ko reporting me **"Missed / Not responded"** mark karo. *(Band survey
  dobara nahi poocha ja sakta.)*
- **Policy (legal)** ke liye → **koi auto-expiry nahi**. Accept na ho to block. Re-consent
  do tarah se trigger hota hai:
  1. **Automatic** — admin **nayi version publish** kare → purani version supersede → sab
     dobara accept karein.
  2. **Manual** — admin **"Re-request acceptance"** action chalaye → kisi user ya poore
     audience ki existing acceptance invalidate, agle login par dobara prompt. *(Bina kuch
     badle bhi fresh consent lena ho to.)*

  > User ka exact framing: "user block karde, or admin ke paas option ho use dobara wo policy
  > bhej kar accept karwa de." — yahi manual re-request + auto version-supersede dono cover
  > karte hain.

### 1.6 Targeting (fully customizable)
Dimensions:
- **All users** (default).
- **Specific Application(s)** — `client_id` se (tenant ki multiple apps me se chunno).
- **Auth method** — `password` / `social` / `passkey` / `phone` / `magic_link`.
- **Specific social provider** — `google`, `microsoft`, `facebook`, `github`, custom.

- **Phase 1** — single-dimension targeting (in me se ek choose).
- **Phase 3** — nested **AND/OR** combos + **audience preview** (kitne/kaunse users match).

### 1.7 Content authoring
- **Policy** — admin **HTML paste** kare **ya** **PDF upload** kare. (PDF DB me stored —
  BrandingAsset pattern, MIME-validated.)
- **Survey** — full question builder (Phase 2).

### 1.8 Survey scope (user: "jitni bhi possibility internet par hai sab")
- Full question-type taxonomy (section 5 me research), multi-question builder,
  **randomization**, **scheduling** (time-based), required/optional per question, **mandatory
  vs skippable**, anonymous option, image/video-rich questions, reporting. (Phase 2.)

---

## 2. Existing system — verified integration points

Yeh exploration se confirm hua (code padh ke). Engine inhi par tikega.

### 2.1 Single sign-in choke point
Saare end-user logins yahan converge: [UserSignIn.SignInAsync](../../src/Authly.Web/Infrastructure/UserSignIn.cs)
— cookie + claims set karta hai (`NameIdentifier`=userId, `ClaimTypes.Name`=email,
`UserClaims.TenantId`, `UserClaims.SessionId`, `UserClaims.EmailVerified`).

Callers (har sign-in path):
| Method | File |
|---|---|
| Password login | [AccountController.Login](../../src/Authly.Web/Controllers/AccountController.cs) |
| Magic link | AccountController.Magic |
| Passkey/WebAuthn | [PasskeyController](../../src/Authly.Web/Controllers/PasskeyController.cs) |
| Phone OTP | AccountController.PhoneOtp |
| Social/external | [SocialController](../../src/Authly.Web/Controllers/SocialController.cs) |
| MFA completion | [MfaController](../../src/Authly.Web/Controllers/MfaController.cs) |

### 2.2 Per-request validator (bypass backstop)
[SessionCookieValidator](../../src/Authly.Web/Infrastructure/SessionCookieValidator.cs) —
`OnValidatePrincipal` har request par chalta hai (wired in
[Program.cs](../../src/Authly.Web/Program.cs)). "Bypass" case (social se enter, terms na liye)
yahan/middleware me natural pakad hota hai.

### 2.3 OIDC provider (SSO client-app logins bhi covered)
[AuthorizationController](../../src/Authly.Web/Controllers/Connect/AuthorizationController.cs)
`/connect/authorize` par un-authenticated user ko `/account/login?ReturnUrl=/connect/authorize?...`
challenge karta hai — to **client-app logins bhi normal login paths se hi guzarte hain**.
`client_id` → [Application](../../src/Authly.Core/Entities/Application.cs) (tenant-scoped) yahin
resolve hota hai → **app-targeting** signal yahan milta hai.

### 2.4 Targeting signals (kahan se milenge)
- **Auth method** → [LoginHistory.Method](../../src/Authly.Core/Entities/LoginHistory.cs)
  (`"password"`,`"google"`,`"passkey"`,`"phone_otp"`,`"magic_link"`,…), har login par record
  (`AuthService.RecordLoginAsync`).
- **Linked providers** → [SocialIdentity](../../src/Authly.Core/Entities/SocialIdentity.cs)
  (`Provider`, `ProviderId`).
- **Tenant apps list** (admin targeting UI) → `IApplicationRepository.ListByTenantAsync`.

### 2.5 Reusable patterns (naya code likhne ki bajaye reuse)
- **Consent** — [ConsentRecord](../../src/Authly.Core/Entities/ConsentRecord.cs) +
  [ConsentService](../../src/Authly.Modules/Compliance/ConsentService.cs)
  (`Purpose/Granted/Version/IpAddress`, `RecordSignupConsentAsync`). Naya `PolicyDecision`
  isi shape se inspired.
- **File upload** (DB-stored, MIME-validated, 3MB) —
  [BrandingService.SaveImageAsync](../../src/Authly.Modules/Branding/BrandingService.cs) +
  [BrandingAsset](../../src/Authly.Core/Entities/BrandingAsset.cs) + `/branding/upload`. PDF
  upload **isi pattern** se (`PolicyAsset`).
- **Admin page** — [BrandingController](../../src/Authly.Web/Areas/TenantAdmin/Controllers/BrandingController.cs)
  + `Views/Branding/Index.cshtml` + [_AdminLayout.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml).
- **RBAC** — [OperatorRbac](../../src/Authly.Core/Authorization/OperatorRbac.cs) +
  [RequireOperatorPermissionAttribute](../../src/Authly.Web/Infrastructure/RequireOperatorPermissionAttribute.cs).
- **Portal page** — [PortalControllerBase](../../src/Authly.Web/Areas/Portal/Controllers/PortalControllerBase.cs)
  + [PrivacyController](../../src/Authly.Web/Areas/Portal/Controllers/PrivacyController.cs) +
  [_PortalLayout.cshtml](../../src/Authly.Web/Areas/Portal/Views/Shared/_PortalLayout.cshtml).
- **Data/EF** — [AppDbContext](../../src/Authly.Infrastructure/Data/AppDbContext.cs)
  (snake_case tables, jsonb columns, tenant FK cascade), migrations in
  `src/Authly.Infrastructure/Data/Migrations/`, DI in
  [DependencyInjection.cs](../../src/Authly.Modules/DependencyInjection.cs).
- **UI** — SAARVIX design system (`saarvix-ui` skill).

---

## 3. PHASE 1 — Policies / Consent engine (core)

### 3.1 Data model (new entities → `src/Authly.Core/Entities/`)

**`Policy`**
- `Id, TenantId, Title, Slug, Description`
- `Status` — `Draft | Published | Archived`
- `EnforcementMode` — `Mandatory | SkippableUntil | Optional`
- `SkipDeadline?`, `StartsAt?`, `CloseDate?` (policy ke liye normally null)
- `Targeting` (jsonb)
- `CurrentVersionId?`
- `CreatedAt, UpdatedAt, PublishedAt?`

**`PolicyVersion`** (consent har version se tied — re-consent enabler)
- `Id, PolicyId, TenantId, Version` (auto-increment string: "1","2",…)
- `ContentType` — `html | pdf`
- `HtmlContent?` (sanitized), `AssetId?` (PDF), `Notes?`
- `PublishedAt`

**`PolicyAsset`** (PDF storage — BrandingAsset clone)
- `Id, TenantId, PolicyId, FileName, ContentType` (`application/pdf`)
- `Data` (bytea), `SizeBytes, CreatedAt`

**`PolicyDecision`** (per-user, append-only / audit-friendly)
- `Id, TenantId, UserId, PolicyId, PolicyVersionId`
- `Decision` — `Accepted | Rejected | Skipped`
- `SessionId?` (skip-per-session ke liye), `ApplicationId?` (kis app context me dikha)
- `IpAddress?, UserAgent?, DecidedAt`

**Targeting JSON shape** (plain value object → `src/Authly.Core/Policies/PolicyTargeting.cs`,
jsonb me serialize, `TenantBranding` jaisa):
```jsonc
{
  "audience": "all" | "applications" | "authMethods" | "providers",
  "applicationIds": ["..."],
  "authMethods": ["password", "social", "passkey", "phone", "magic_link"],
  "providers": ["google", "microsoft", ...]
}
```
> Phase 3 me yeh nested AND/OR rule-groups me upgrade hoga (backward-compatible parse).

**DbContext** ([AppDbContext](../../src/Authly.Infrastructure/Data/AppDbContext.cs)) me:
- DbSets: `Policies, PolicyVersions, PolicyAssets, PolicyDecisions`.
- `OnModelCreating`: snake_case tables, `tenant_id` FK cascade, indexes on
  `policies(tenant_id, status)` aur `policy_decisions(tenant_id, user_id, policy_id)`.
- Ek migration: **`AddPoliciesEngine`**.

### 3.2 Module / services (`src/Authly.Modules/Policies/`)

**`IPolicyService` / `PolicyService`** — admin-facing:
- `CreateDraft`, `Update`, `UploadPdf`/`ReplacePdf`, `SaveHtml` (sanitize → save)
- `Publish` — naya `PolicyVersion` banaye + enforcement/targeting set + `Status=Published`
- `Archive`/`Close`, `List`, `GetWithVersions`
- `ListResponses`/report (accepted/rejected/skipped/pending counts + per-user)
- `ReRequestAcceptance(audience | userId)` — decisions invalidate karke "pending" bana de

**`IUserPromptService` / `UserPromptService`** — **gate ka brain**:
- Input: `(tenantId, userId, sessionId, currentAuthMethod?, currentApplicationId?, now)`
- Output: pending prompts list → `{ policyId, version, mode, blocking, actions[] }`
- Logic:
  - Published + active (`StartsAt..CloseDate` window) policies jo user ke context se **target
    match** karein.
  - User ne **current version** decide kiya? `Accepted` → satisfied (sab modes). `Mandatory`
    ko sirf `Accepted` satisfy karta hai.
  - `SkippableUntil` + abhi skip-window me + **is session** me already skipped → satisfied
    (sirf is session). Naya session → phir pending.
  - Skip-deadline nikal gaya → blocking. (Survey close ke baad → drop + "missed" — Phase 2.)

**HTML sanitization** — admin-pasted HTML render karne se pehle sanitize (e.g.
`HtmlSanitizer` / `Ganss.XSS`) → same-tenant stored-XSS se bachao. **Naya NuGet dependency.**

**DI** — [DependencyInjection.cs](../../src/Authly.Modules/DependencyInjection.cs) me
`AddScoped<IPolicyService,…>` + `AddScoped<IUserPromptService,…>`. Repositories →
`src/Authly.Infrastructure/Data/Repositories/` (interfaces `src/Authly.Core/Interfaces/`).

### 3.3 Enforcement gate (sabse critical — sab paths cover, bypass-proof)

**Naya middleware** `RequiredPromptsGateMiddleware`
(`src/Authly.Web/Infrastructure/Security/`), wired **after `UseAuthorization()`** in
[Program.cs](../../src/Authly.Web/Program.cs):
- Sirf authenticated end-user (`AuthSchemes.User`) requests; protected paths (`/portal/*`,
  `/connect/authorize`, app-facing).
- **Exclude:** prompt page khud (`/account/policies`), `/account/logout`, `/account/*` auth
  endpoints, static assets, `/api/*`.
- `IUserPromptService` se pending **blocking** prompts → agar hain to redirect:
  **`/account/policies?returnUrl=<original>`** (returnUrl `Url.IsLocalUrl` / `SafeReturnUrl`
  se validate).
- `/connect/authorize` par `client_id` se `currentApplicationId` resolve karke **app-targeted**
  prompts include — SSO logins bhi gated.
- Auth method `LoginHistory` (latest for session) se; ya login ke waqt session/claim me stamp.
  Yeh "social se bypass" case ko har subsequent login par pakad lega.

**Prompt/Consent page** `PoliciesController` (top-level `/account/policies`,
`[Authorize(Policy=User)]`):
- **GET**: pending policies dikhaye — HTML render **ya** PDF embed (`<object>`/iframe →
  `/account/policies/asset/{id}`). Har ek pe mode ke hisaab se allowed actions
  (Accept / Reject? / Skip?).
- **POST decision**: `PolicyDecision` record (SessionId, Ip, UA). Saare blocking satisfy hue →
  `returnUrl` pe continue; warna same page. `Mandatory` reject/!accept → aage nahi (logout
  option).

**Skip-per-session**: decision `SessionId` se tied; middleware "satisfied-this-session"
check karta hai. Naya login = naya `SessionId` = phir prompt (`SkippableUntil`).

### 3.4 Admin UI (TenantAdmin → "Policies")

**`Areas/TenantAdmin/Controllers/PoliciesController.cs`** —
`[Route("tenantadmin/consent-policies")]` *(`tenantadmin/policies` is already taken by the ABAC
AccessPolicies feature)*, base `TenantAdminControllerBase`,
`[RequireOperatorPermission("policy.read" / "policy.manage")]`:
- `Index` (list + status), `Create`/`Edit` (draft), `UploadPdf`
- `Publish` (modal: enforcement mode + skip-deadline + targeting — "publish karte hi impact"
  flow)
- `Archive`/`Close`, `Responses`/report, `ReRequestAcceptance`

Views `Areas/TenantAdmin/Views/Policies/*` (SAARVIX): Index, Edit (HTML editor + PDF upload +
live preview), Publish modal, Responses.

**RBAC**: [OperatorRbac](../../src/Authly.Core/Authorization/OperatorRbac.cs) me
`ResPolicy="policy"` + `policy.read`/`policy.manage`; role mappings
(org_owner/org_admin/project_admin → manage; viewer → read). Auto-seeded by
`EnsureSystemRolesAsync`.

**Nav**: [_AdminLayout.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml)
"Access control" section me "Policies" item.

### 3.5 End-user Portal ("My policies & consents")
- Naya `Areas/Portal/Controllers/ConsentsController.cs` (ya `PrivacyController` extend) + view:
  user ki saari `PolicyDecision` history (title, version, decision, date, view-content link).
  Mandatory pending bhi dikhe with "Review now".
- **Nav**: [_PortalLayout.cshtml](../../src/Authly.Web/Areas/Portal/Views/Shared/_PortalLayout.cshtml)
  "Privacy & data" ke paas item.

### 3.6 Signup links fix + integrate
- [Register.cshtml](../../src/Authly.Web/Views/Account/Register.cshtml) ke broken `/legal/terms`,
  `/legal/privacy` → tenant ke published Terms/Privacy policy ke real URLs par point (ya
  remove if none).
- Signup-time consent ko naye engine se record karein (purane `RecordSignupConsentAsync` ko
  bridge/replace).
- Optional: existing `ConsentRecord` ko seeded Policies me migrate — ya backward-compat ke
  liye parallel chhod dein (naya engine source-of-truth).

### 3.7 Phase 1 task checklist — ✅ DONE (branch `feat/policies-engine`, build 0 err, 322 tests green)
- [x] Branch `feat/policies-engine`
- [x] Entities: `Policy`, `PolicyVersion`, `PolicyAsset`, `PolicyDecision` + enums +
      `PolicyTargeting` value object (`Policy` also carries draft content + `ConsentResetAt`)
- [x] DbContext DbSets + `OnModelCreating` config
- [x] Repository interface `IPolicyRepository` + `PolicyRepository` + DI
- [x] `PolicyService` + `UserPromptService` + `PolicyHtmlSanitizer` + DI
      *(Ganss.Xss NuGet unavailable on the feed → in-house sanitizer + sandboxed-iframe render instead)*
- [x] `policy.read` / `policy.manage` RBAC (catalogue + project_admin grant)
- [x] EF migration `AddPoliciesEngine` (verified applied to live Postgres — 4 tables present)
- [x] `RequiredPromptsGateMiddleware` + Program.cs wiring (after `UseAuthorization`)
- [x] `/account/policies` consent page controller + view (HTML in sandboxed iframe, PDF embed)
- [x] TenantAdmin Policies controller + Index/Edit/Responses views + nav + publish/archive/re-request
- [x] Portal consents history page + nav
- [x] Signup links fix (removed 404 `/legal/*`; Privacy → public page)
- [x] `UserPromptServiceTests` (11 tests: mandatory/skippable/optional, per-session skip,
      version + consent-reset re-prompt, close-date, auth-method + application targeting)

> **Note (existing tenants):** `EnsureSystemRolesAsync` only grants new permissions on first role
> creation, so `policy.manage` auto-applies to **new** orgs; existing orgs need a manual grant (or
> re-seed). Fresh-DB verification uses a new org. Signup-time legacy `ConsentRecord` left as-is
> (engine is the enforcement source of truth); a designated-"terms" policy bridge is a follow-up.

---

## 4. PHASE 2 — Surveys (full builder)

Phase 1 ka **targeting + gate + portal + lifecycle reuse**; sirf "content" = questions, plus
survey-runner + reporting.

### 4.1 Entities (`src/Authly.Core/Entities/`)
- **`Survey`** — `Id, TenantId, Title, Description, Status, EnforcementMode, SkipDeadline?,
  StartsAt?, CloseDate?, Targeting (jsonb), Settings (jsonb), CreatedAt…`
  - `Settings`: `randomizeQuestions, anonymous, oneResponsePerUser | recurring,
    showProgressBar, thankYouMessage`
- **`SurveyQuestion`** — `Id, SurveyId, TenantId, Order, Type, Title, HelpText?, Required,
  MediaUrl?` (image/video question), `Settings (jsonb)`
  - `Settings`: `scale min/max, rows/cols (matrix), allowOther, maxChoices, randomizeOptions…`
- **`SurveyQuestionOption`** — `Id, QuestionId, Order, Label, ImageUrl?, Value`
- **`SurveyResponse`** — `Id, SurveyId, TenantId, UserId?, SessionId?, Status
  (Completed | Partial | Missed), StartedAt, SubmittedAt?` (`UserId` null when anonymous)
- **`SurveyAnswer`** — `Id, ResponseId, QuestionId, TextValue?, NumberValue?, OptionIds (jsonb
  array), ExtraValue (jsonb — matrix/ranking)`

> Generic answer storage (text vs number vs optionIds vs jsonb) — Redgate-style model
> (section 5.2).

### 4.2 Survey behavior
- **Builder UI** (TenantAdmin → "Surveys"): drag-order questions, per-question type+options,
  required toggle, randomize, scheduling (start/end + skip-deadline), enforcement mode,
  targeting (Phase-1 reuse), preview, publish.
- **Runner** (`/account/survey/{id}` via same gate): questions render (randomized if set),
  required validation, partial-save, submit → `SurveyResponse` = `Completed`.
- **Close-date ke baad** never-responded → `SurveyResponse` = `Missed`, login allowed.
- **Reporting** (admin): per-question aggregation/charts, response rate,
  completed/partial/missed, CSV export.
- **Portal**: user apni submitted responses + pending surveys dekhe.

### 4.3 Phase 2 task checklist — ✅ DONE (331 tests green; 9 new survey tests)
- [x] Survey entities (`Survey`, `SurveyQuestion`, `SurveyQuestionOption`, `SurveyResponse`,
      `SurveyAnswer`) + enums (`SurveyQuestionType`, `SurveyResponseStatus`) + DbContext +
      migration `AddSurveysEngine` (5 tables)
- [x] `ISurveyRepository` + `SurveyRepository` + DI
- [x] `SurveyService` (CRUD, question builder add/delete/reorder, publish/archive/re-request,
      pending evaluation, runner prep w/ randomization, submit/skip/decline, report aggregation)
- [x] Shared `TargetingEvaluator` extracted (policies + surveys both use it; UserPromptService refactored)
- [x] Gate integration — `RequiredPromptsGateMiddleware` checks policies first, then surveys →
      redirects to `/account/survey/{id}`
- [x] Survey runner page `/account/survey/{id}` (`SurveyController`) — renders 9 question types,
      submit/skip/decline, randomized order
- [x] TenantAdmin **Surveys** (`/tenantadmin/surveys`) — list, builder (meta + enforcement +
      targeting + settings + question add/reorder/delete), publish/archive/re-request, responses report
- [x] `survey.read` / `survey.manage` RBAC
- [x] Portal **Surveys** history page + nav
- [x] 9 `SurveyServiceTests` (pending/optional/mandatory/skippable-per-session/closed, submit +
      required validation, publish-needs-question, report option-count aggregation)

**Question types shipped (9):** SingleChoice · MultipleChoice · Dropdown · ShortText · LongText ·
Number · Rating (scale) · YesNo · Date. *(Matrix, Ranking, NPS/Star, image-choice, file-upload,
skip/branch logic = future — the `Type` enum + jsonb answer model are extensible.)*

> **"Missed" status:** modeled in `SurveyResponseStatus`; full population-wide missed-marking at
> close (for users who never log in) is a follow-up (needs the targeted-user set / a background job).
> Closed surveys simply stop prompting (no block) — the confirmed behavior.
> **Anonymous:** `UserId` is still stored for enforce-once/skip dedup; the *reporting* view hides
> identity. True unlinkable anonymity is a follow-up.

---

## 5. Research — survey systems (Google Forms / SurveyMonkey / Typeform / Zoho / Qualtrics)

User ka instruction: "online ache survey system samjho, admin + user panel dono, jitni
possibility hai sab." Yeh research findings hain (sources niche).

### 5.1 Question-type taxonomy (comprehensive)
- **Choice:** Single choice (radio), Multiple choice (checkbox), Dropdown (single/multi),
  Image choice (single/multi), Yes/No (dichotomous)
- **Text:** Short text, Long text, Multiple textboxes, Number, Email, Full name, Contact info
- **Scale/Rating:** Rating scale (numeric), Star rating, NPS (0–10), Slider/range,
  Likert scale, Weighted choice
- **Matrix/Grid:** Matrix single/multi choice, Matrix rating, Matrix star, Matrix dropdown,
  Matrix textbox, Matrix grid (mixed types)
- **Other:** Ranking, Date/time, File upload, Signature, Continuous sum, Click-map (image
  hotspot)
- **Layout:** Section heading / description (non-question), media-rich (question with
  image/video)

### 5.2 Data-model pattern (generic answer storage)
Core entities: **Survey → Question → QuestionOption**, aur **Response → Answer →
AnswerOption**. Question ka `Type` decide karta hai answer kaise store ho:
- Open/text → `Answer.TextValue`
- Numeric/scale → `Answer.NumberValue`
- Choice → `OptionIds` (jsonb array — single ke liye 1 element, multi ke liye N)
- Matrix/ranking → `ExtraValue` (jsonb)

Survey-level: `StartDate, EndDate, MinResponses, MaxResponses, Status (Planned/Open/Closed)`.
Question-level: `Order, IsMandatory`. Versioning + conditional logic = advanced (Phase 3+).

### 5.3 Admin features (settings)
- **Skip/branch logic** — answer ke basis pe agla question/page/end decide.
- **Randomization** — questions/options ka order shuffle (bias kam).
- **Scheduling** — start/end date, response-count limit, time-to-complete limit.
- **Required questions**, **anonymous responses**, **one-response-per-user vs recurring**,
  progress bar, thank-you/redirect, templates, question bank.

### 5.4 Sources
- [SurveyMonkey — question types](https://www.surveymonkey.com/mp/survey-question-types/)
- [Zoho Survey — question types](https://www.zoho.com/survey/question-types.html)
- [Qualtrics — skip logic](https://www.qualtrics.com/support/survey-platform/survey-module/question-options/skip-logic/)
- [Qualtrics — question randomization](https://www.qualtrics.com/support/survey-platform/survey-module/block-options/question-randomization/)
- [QuestionPro — randomization](https://www.questionpro.com/features/randomization-of-questions.html)
- [Redgate — survey database schema](https://www.red-gate.com/blog/database-design-survey-system/)
- [SurveyLab — skip logic / scheduling](https://www.surveylab.com/survey-skip-logic/)

---

## 6. PHASE 3 — Advanced targeting

- Targeting JSON → **nested AND/OR rule-groups** over dimensions: application, auth-method,
  provider, **role** (end-user RBAC), **linked-identity**, **new-users-only**,
  **has-not-responded**, geo/IP (optional). Backward-compatible parse.
- **Audience preview** — "is targeting se kitne / kaunse users match karte hain" (query over
  users + LoginHistory + SocialIdentity).

### Phase 3 task checklist
- [ ] Targeting model upgrade (rule-groups) + backward-compatible parser
- [ ] Audience preview query + UI
- [ ] Role/identity/new-user/has-not-responded conditions
- [ ] Build + tests

---

## 7. Verification (Phase 1)

1. **Build + tests** — `dotnet build Authly.slnx` & `dotnet test Authly.slnx` (existing green
   suite — koi regression nahi).
2. **Migration** — `dotnet ef migrations add AddPoliciesEngine -p src/Authly.Infrastructure
   -s src/Authly.Web`; `docker compose up --build -d`, logs me migration apply confirm.
3. **Admin flow** — TenantAdmin → Policies → create draft (HTML + PDF) → publish as
   `Mandatory`, targeting=All → version "1".
4. **Enforcement (har path)** — ek end-user se password, magic-link, passkey, phone-OTP,
   **social (Google)** se login — har baar `/account/policies` par block; accept → original
   destination resume. Reject (`Mandatory`) → blocked.
5. **OIDC** — client app se SSO login (`/connect/authorize`) → gate trigger → accept ke baad
   app ko code/token. App-targeted policy sirf us app pe trigger ho.
6. **Bypass case** — social login se enter (jab policy nahi thi) → policy publish → next login
   par zaroor poochhe.
7. **Skip mode** — `SkippableUntil` → skip → andar; logout/login → phir poochhe; skip-deadline
   ke baad → block.
8. **Versioning + re-consent** — v1 accept → republish v2 → next login dobara poochhe. Admin
   "Re-request acceptance" → bina version change ke dobara prompt.
9. **Portal** — `/portal/consents` me decisions + versions dikhein.
10. **Signup** — Register page ke terms/privacy links ab tenant ki published policies pe jaayein
    (404 gone).

---

## 8. Open items / notes

- **HTML sanitizer NuGet** add karni hogi (e.g. `HtmlSanitizer`) — admin-content render safety.
- **`UserSignIn` static** rehne dega; enforcement gate middleware me hai (refactor avoid).
  Auth-method stamp ke liye chhota addition (claim ya session field) lag sakta hai.
- Har phase ki **alag branch + PR** (user khud merge karta hai). Phase 1 = `feat/policies-engine`.
- PDF storage DB me (self-host friendly, BrandingAsset pattern) — koi external blob nahi.
