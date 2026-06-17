# Pluggable Observability — customer apna logging laaye (BYO)

> Faisla: hard-coded / env-var-only logging ke bajaye, observability ko **customer-
> configurable + opt-in** banao. Admin panel mein ek menu item ho jahan customer apna
> **Grafana Loki / Azure Application Insights / OTLP endpoint** ka config + key daale.
> **Kuch nahi daala → koi logging nahi.** Plus docker-compose mein local observability
> stack taaki test ho sake. Doc-only — abhi koi code nahi.

---

## 1. Confirmed decisions

| Decision | Value |
|---|---|
| **Scope** | **Instance-global** — poore Docker box ke liye ek config. Har log/trace/metric pe `project`/`tenant` label, taaki Grafana mein project se filter ho. |
| **Signals** | **Full OpenTelemetry** — logs + traces + metrics (OTLP). |
| **Opt-in** | Config nahi → koi exporter wire nahi hota → kuch ship nahi hota. |
| **Providers** | Grafana **Loki/Tempo/Prometheus** (via OTLP → OTel Collector), **Azure Application Insights** (connection string), generic **OTLP endpoint**. |

---

## 2. Aaj kya hai (baseline)

- **Koi structured app-logging nahi** — sirf default `ILogger` → console. Na Serilog,
  na OpenTelemetry, na App Insights.
- **Ek hi cheez exist karti hai:** `LogStreamJob` (Hangfire, har 5 min) jo **audit logs**
  ko `LOG_STREAM_ENDPOINT` / `LOG_STREAM_KEY` (env-var) pe POST karta hai.
  Files: [LogStreaming/LogStreamJob.cs](../../src/Authly.Web/Infrastructure/LogStreaming/LogStreamJob.cs), [Program.cs](../../src/Authly.Web/Program.cs).
- **docker-compose:** sirf `app` + `postgres:16` + `redis:7`. Koi Grafana/Loki nahi.

Yani aapko jo chahiye uska aadha dhaancha (config-se-chalne wala, gracefully-disabled
job) already mojood hai — usse generalize karna hai.

---

## 3. Reuse karo: existing "BYOK provider" pattern

Authly mein already ek bilkul yahi UX pattern hai — **MessagingProvider** (aur
SocialProvider, Webhooks). Naya code likhne ke bajaye **isko mirror karo**:

| Layer | Existing file | Kya seekhna |
|---|---|---|
| Entity | `MessagingProvider` (Provider, Mode, `Config` jsonb, IsActive) | jsonb config blob, secrets andar |
| Service | [MessagingService.cs](../../src/Authly.Modules/Messaging/MessagingService.cs) `BuildEmailConfig`/`ResolveEmailConfig` | secret **blank → purana rakho**, naya → `IEncryptionService.Encrypt()` |
| Encryption | [AesEncryptionService.cs](../../src/Authly.Infrastructure/Security/AesEncryptionService.cs) (`IEncryptionService`, AES-256-GCM, `ENCRYPTION_KEY`) | secrets at-rest encrypted |
| Controller | [MessagingController.cs](../../src/Authly.Web/Areas/TenantAdmin/Controllers/MessagingController.cs) | GET form / POST save / delete |
| View | [Provider.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Messaging/Provider.cshtml) | **write-only secret** UX (`leave blank to keep`) |
| Nav | [_AdminLayout.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml) "Developers" section | menu item kahan add karna |

---

## 4. Design

### 4.1 Storage — ek GLOBAL config (per-tenant NAHI)
MessagingProvider per-tenant (`TenantId` FK) hai. Observability **instance-global** hai,
isliye `TenantId` **nahi**. Do options:
- Naya singleton entity **`ObservabilityConfig`** (ek hi row), ya
- Ek `PlatformState` key (existing global key/value store).

Recommended: dedicated `ObservabilityConfig` (saaf, typed). Fields:
- `Enabled` (bool)
- `Exporter` (`otlp` | `azure_monitor`)
- `OtlpEndpoint`, `OtlpHeaders` (e.g. auth header — **encrypted**)
- `AzureConnectionString` (App Insights — **encrypted**)
- `Signals` (logs / traces / metrics — kaun-kaun on)
- `SamplingRatio` (traces ke liye)
- `UpdatedAt`

Secrets `IEncryptionService` se encrypt (wahi `ENCRYPTION_KEY`).

### 4.2 Wiring — OpenTelemetry in Program.cs
- `AddOpenTelemetry()` ke saath `.WithLogging()` / `.WithTracing()` / `.WithMetrics()`.
- Exporter: **OTLP** (Loki/Tempo/Prometheus ke liye, OTel Collector ke through) aur
  **Azure Monitor** exporter (App Insights ke liye).
- **Startup pe** stored `ObservabilityConfig` padho → uske hisaab se exporter register
  karo. **Config absent / disabled → koi exporter register mat karo** (= no logging,
  opt-in). 
- **Honest limitation:** OTel pipeline **startup pe** banta hai. Admin panel se config
  change → **restart pe apply** hoga. Yeh doc mein/UI mein clearly likhna ("changes take
  effect after restart").
- Env fallback: `OTEL_EXPORTER_OTLP_ENDPOINT` (docker-compose default = bundled collector).

### 4.3 Project/tenant tagging
Ek OTel **enrichment processor** (log + trace) jo har record pe `tenant.id` / `project.id`
(aur jahan mile `account.id`) stamp kare — `ITenantContext` se. Isse single destination
pe sab kuch jaata hai par Grafana mein **project se filter** ho jaata hai (yahi
"instance-global + project label" decision ka point hai).

### 4.4 Audit-stream ko isme merge karo
Existing `LogStreamJob` ka target env-var se nahi, **`ObservabilityConfig` se** aaye.
Job rahe; bas destination config-driven ho jaaye. Isse "audit streaming" bhi same
admin-configured observability ka hissa ban jaata hai.

### 4.5 Admin surface
- [_AdminLayout.cshtml](../../src/Authly.Web/Areas/TenantAdmin/Views/Shared/_AdminLayout.cshtml)
  ke "Developers" section mein naya **"Observability"** menu item.
- Naya `ObservabilityController` — **project-scoped NAHI** (global config pe kaam karta
  hai). Self-host mein account owner == operator, toh koi bhi owner isse set kar sakta hai.
- Form: enable toggle, exporter dropdown, endpoint/connection-string (secret = write-only),
  signal checkboxes, sampling. Save pe audit log + secret encrypt.
- "Test connection" button (optional, MessagingProvider ke test jaisa).

---

## 5. docker-compose — local observability stack (test ke liye)

Aap local se test kar sako, iske liye ek optional stack add karo. App OTLP pe export
karega → **OTel Collector** receive karega → fan-out:

```
Authly app ──OTLP──▶ OTel Collector ─┬─▶ Loki        (logs)
                                     ├─▶ Tempo       (traces)
                                     └─▶ Prometheus  (metrics)
                                              │
                                          Grafana (dashboards, pre-provisioned
                                                   datasources: Loki+Tempo+Prometheus)
```

- Naye services: `otel-collector`, `loki`, `tempo`, `prometheus`, `grafana`.
- Ek **alag overlay file** `docker-compose.observability.yml` (taaki optional rahe —
  `docker compose -f docker-compose.yml -f docker-compose.observability.yml up`).
- App ko `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317` de do.
- Grafana ko pre-provisioned datasources + ek basic dashboard ke saath ship karo.

**Local test flow:**
1. `docker compose -f docker-compose.yml -f docker-compose.observability.yml up`
2. Admin panel → Observability → OTLP endpoint daal ke enable (ya env se collector default).
3. App pe thoda activity (login/signup).
4. Grafana (`http://localhost:3000`) → Explore → Loki/Tempo/Prometheus → logs/traces/metrics
   dikhein, `project.id` label se filter.

---

## 6. Implementation plan pe asar
Plan mein **Phase 6 — Pluggable observability** add hota hai (Account model + SuperAdmin
removal ke baad). Steps: `ObservabilityConfig` entity + admin surface (BYOK pattern mirror)
→ OTel wiring in Program.cs (opt-in) → tenant/project enrichment processor → `LogStreamJob`
target config-driven → docker-compose observability overlay.

## 7. TL;DR
- **BYO observability**: customer apna Loki/App-Insights/OTLP config admin panel se de.
- **Instance-global**, har log project-label ke saath. **Full OTel** (logs+traces+metrics).
- **Opt-in**: config nahi → kuch log nahi. Secrets encrypted (existing `IEncryptionService`).
- **Reuse** MessagingProvider BYOK pattern; **generalize** existing `LogStreamJob`.
- **docker-compose** mein Grafana+Loki+Tempo+Prometheus+OTel-Collector (optional overlay)
  local testing ke liye.
- **Caveat**: config changes restart pe apply (OTel pipeline startup pe banta hai).
```
