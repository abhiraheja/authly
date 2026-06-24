# Sign-in Methods Config + Magic-Link OIDC Landing

> Is session ka kaam: (1) email delivery ka silent-fail diagnose, (2) magic-link ko OIDC
> flow ke saath kaam karwana (returnUrl thread + relying-app pe wapas land), (3) tenant-
> configurable **sign-in methods** (5 options, kam-se-kam-ek-effective guard), (4) login
> page ko **adaptive** banana taaki sirf enabled methods clean dikhein. Code DONE — 311
> tests green, build 0 CS/RZ errors. Branch pe committed nahi (user khud merge karta hai).

---

## 1. Email delivery — silent "sent" but nothing arrives

**Problem:** magic-link / koi bhi email "sent" log hota tha par inbox me aata nahi tha.

**Root cause:** saari email ek hi path se jaati hai
([MessagingService.DeliverEmailAsync](../../src/Authly.Modules/Messaging/MessagingService.cs)).
Agar tenant ka koi **active Email provider** (`GetActiveAsync(tenantId, Email)`) nahi hai,
to fallback **"log" provider** use hota hai
([LogProviders.cs](../../src/Authly.Infrastructure/Messaging/LogProviders.cs)) jo kuch
bhejta nahi par **hamesha `DeliveryResult.Ok` return karta hai** → MessageLog me "sent"
likh jaata hai. Isliye log "sent" dikhata tha par mail nahi aati thi.

**Fix:** koi code change nahi — TenantAdmin → **Messaging** me ek **active Email provider
(SMTP / Zepto)** configure karna padta hai. Tab `magic_link` samet saare email templates
(verify_email, reset_password, otp, security_alert, account_recovery, verify_new_contact,
contact_change_alert, operator_invite) real me jaate hain. (`welcome` template define hai
par kahin enqueue nahi hota.)

> Note: pipeline me **outbox/retry nahi** hai — Hangfire fire-and-forget
> ([HangfireMessageQueue](../../src/Authly.Web/Infrastructure/Messaging/HangfireMessageQueue.cs)
> → [MessageDispatchJob](../../src/Authly.Web/Infrastructure/Messaging/MessageDispatchJob.cs)).

---

## 2. Magic-link + OIDC: relying-app (e.g. localhost:4200) pe wapas land

**Problem:** SPA (localhost:4200) OIDC se login shuru karta hai; magic link click karne pe
user Authly ke `/portal/profile` (localhost:8080) pe atak jaata tha, apni app pe wapas nahi.

### 2a. returnUrl threading
Pehle magic-link flow returnUrl **carry hi nahi** karta tha. Ab poora chain threaded hai:
- [`IAuthUrlBuilder.BuildMagicLinkUrl(tenantId, rawToken, returnUrl?)`](../../src/Authly.Modules/Auth/AuthModels.cs)
  + impl [AuthUrlBuilder](../../src/Authly.Web/Infrastructure/AuthUrlBuilder.cs) — link me
  `&returnUrl=...` append.
- [`IMagicLinkService.RequestAsync(..., returnUrl?, ct)`](../../src/Authly.Modules/AdvancedAuth/IMagicLinkService.cs)
  + [MagicLinkService](../../src/Authly.Modules/AdvancedAuth/MagicLinkService.cs).
- [`AccountController.MagicLink` POST + `Magic` GET](../../src/Authly.Web/Controllers/AccountController.cs)
  — POST `SafeReturnUrl(model.ReturnUrl)` deta hai; GET re-validate karta hai.
- `SafeReturnUrl` sirf **local** URLs allow karta hai (`Url.IsLocalUrl`) — yani returnUrl =
  local `/connect/authorize?...` continuation (cross-origin redirect OpenIddict apne
  registered `redirect_uri` se karta hai). Open-redirect blocked.

### 2b. New-tab problem → app-origin pe land
Email link aksar **naye tab** me khulta hai, jiske paas SPA ka PKCE `code_verifier`/`state`
(per-tab `sessionStorage`) nahi hota — to OIDC continuation wahin complete nahi hoti
(`?iss=...` pe atakti thi). **Fix:** `Magic` GET sign-in ke baad user ko seedha **relying
app ke origin** (4200) pe bhej deta hai. SPA load hote hi (token na milne pe) khud
`/connect/authorize` chalata hai — IdP cookie set hai → silent, seamless login usi tab me.

App-origin securely derive hota hai
([`AccountController.ResolveAppLandingAsync`](../../src/Authly.Web/Controllers/AccountController.cs)):
returnUrl ke andar ke `client_id` + `redirect_uri` ko us client ke **registered
RedirectUris ke against exact-match validate** karke, uska origin (`scheme://host:port`)
liya jaata hai. Match na ho to fallback (no open-redirect). `IApplicationRepository`
inject kiya.

### 2c. Original-tab auto-complete (fallback)
Agar app-origin resolve na ho to "check your email" tab khud flow complete kar leta hai:
- [`GET /account/auth-status`](../../src/Authly.Web/Controllers/AccountController.cs) — sirf
  caller ke apne end-user cookie ka `{authenticated}` JSON (anonymous-safe).
- [MagicLinkSent.cshtml](../../src/Authly.Web/Views/Account/MagicLinkSent.cshtml) — har
  2.5s poll; auth hote hi returnUrl pe navigate (us tab me PKCE maujood). Plus
  `localStorage` heartbeat se [MagicComplete.cshtml](../../src/Authly.Web/Views/Account/MagicComplete.cshtml)
  (naya throwaway-tab page) decide karta hai: live waiting-tab hai to "return to your tab",
  warna khud continue.

**Requirement:** SPA ko root pe load hone par unauthenticated state me login initiate karna
chahiye (standard route-guard behaviour). TenantAdmin → Applications me SPA ka
`http://localhost:4200/auth/callback` registered redirect URI hona zaroori hai.
**Cross-device** (laptop start, phone pe link) PKCE ki inherent limitation se cover nahi.

---

## 3. Configurable Sign-in Methods

**Faisla:** tenant admin 5 sign-in methods independently on/off kare; method tabhi kaam kare
jab enabled ho; **kam-se-kam ek EFFECTIVE method** hamesha on rahe (lockout se bachne ke
liye). Yeh **sign-in only** hai — existing self-service *sign-up* toggles alag axis hain.

| Method | Setting (`TenantSecuritySettings`) | Prerequisite (effective) |
|---|---|---|
| Email & password | `AllowPasswordLogin` | — |
| Email sign-in link | `AllowMagicLinkLogin` | — |
| Passkey | `AllowPasskeyLogin` | — |
| Social | `AllowSocialLogin` | ≥1 active social provider |
| Phone (WhatsApp) | `AllowPhoneLogin` (existing) | WhatsApp + OTP template linked |

- Naye bools default **`true`** → existing tenants (JSON me keys nahi) ke sab methods on
  rahenge. **Koi migration nahi** (settings JSON node, `System.Text.Json` missing keys ko
  C# default pe chhodta hai). [SecurityModels.cs](../../src/Authly.Modules/Security/SecurityModels.cs).
- **Single source of truth:** [`IAuthMethodPolicy`](../../src/Authly.Modules/Security/AuthMethodPolicy.cs)
  → `EnabledSignInMethods(Password, MagicLink, Passkey, Social, Phone)` with `.Any` /
  `.EffectiveCount`. **Effective** = toggle on AND prerequisite met. Readiness sources reuse:
  `IMessagingService.IsWhatsAppOtpReadyAsync`, `ISocialLoginService.ListActiveOptionsAsync`.
  DI: [DependencyInjection.cs](../../src/Authly.Modules/DependencyInjection.cs).
- **Admin UI:** [Security/Index.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Security/Index.cshtml)
  "Sign-in methods" card (5 switches; social/phone disabled+hint jab tak ready na ho; JS
  aakhri effective switch off nahi hone deta). **Phone sign-in toggle yahin move hua**
  (Phone card me sirf sign-up bacha) — double-binding se bachne ko.
  [SecurityController](../../src/Authly.Web/Areas/TenantAdmin/Controllers/SecurityController.cs)
  POST candidate settings banata hai, social/phone ko not-ready hone par force-off karta
  hai, phir `!effective.Any` hone par ModelState error de kar **save reject** karta hai.
- **Server-side guards (UI hide ke alawa):** disabled method ke endpoint seedha refuse karte
  hain — `Login` POST (`AllowPasswordLogin`), `MagicLink` GET+POST, `PasskeyController.Options`,
  `SocialController.Start`. (Phone pehle se `PhoneAuthEnabledAsync` se gated.)

---

## 4. Adaptive login page

**Faisla:** button-spam ki jagah login page ek **primary inline method** chune, baaki
**secondary buttons**. [Login.cshtml](../../src/Authly.Web/Views/Account/Login.cshtml):

```
primary = password ? "password" : passkey ? "passkey" : phone ? "phone" : "none"
phoneButton   = phone   && primary != "phone"     // phone button sirf jab email-method primary ho
passkeyButton = passkey && primary != "passkey"
```

- **Primary area:** password form / (email + passkey button) / phone form / kuch nahi.
- **Phone** jab koi email-method (password/passkey) nahi hai → **inline phone input**
  (button nahi); email + phone dono ho → phone **secondary button**.
- **Dividers** sirf tab jab dono taraf content ho (only-social pe trailing "or" nahi).
- Phone form ek shared partial me nikla: [_PhoneLoginForm.cshtml](../../src/Authly.Web/Views/Account/_PhoneLoginForm.cshtml)
  — dedicated [PhoneLogin.cshtml](../../src/Authly.Web/Views/Account/PhoneLogin.cshtml) aur
  inline dono use karte hain. Dono pe ab **branding heading** (Welcome back) aata hai.

**Behaviour:** only-phone → branded inline phone form · only-social → buttons, no divider ·
social+phone → social + inline phone · email+phone → password form + "Sign in with phone"
button · only-passkey → email + spaced passkey button · only-magic → magic button.

Login GET ([AccountController.Login](../../src/Authly.Web/Controllers/AccountController.cs))
flags `IAuthMethodPolicy` se set karta hai: `ShowPasswordLogin`/`ShowMagicLink`/`ShowPasskey`/
`AllowPhoneLogin` + gated `SocialOptions`.

---

## 5. Files + verification

**Modules:** SecurityModels.cs, AuthMethodPolicy.cs (naya) + DI, IMagicLinkService/MagicLinkService, AuthModels.cs.
**Web:** AccountController.cs (magic returnUrl + app-landing + auth-status + method guards), SecurityController.cs, PasskeyController.cs, SocialController.cs, AuthUrlBuilder.cs, SecuritySettingsViewModel.cs.
**Views:** Login.cshtml, _PhoneLoginForm.cshtml (naya), PhoneLogin.cshtml, MagicLinkSent.cshtml, MagicComplete.cshtml (naya), Security/Index.cshtml.
**Tests:** tests/Authly.Tests/Security/AuthMethodPolicyTests.cs (naya). **311 green.**

**Verify:**
1. `dotnet build Authly.slnx` (filter `error CS`/`error RZ`) + `dotnet test` → all green.
2. TenantAdmin → Security → Sign-in methods: har toggle; social/phone tab tak disabled jab
   tak configured na ho; aakhri effective method off karne par save reject.
3. `/account/login`: disabled method gayab; har combination clean (only-phone, only-social,
   social+phone, only-passkey, email+phone).
4. OIDC: `/connect/authorize` se login → magic link → naye tab me kholo → seedha
   `localhost:4200` pe logged-in (Authly portal nahi). App ka redirect URI registered ho.
