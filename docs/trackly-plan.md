# Trackly — Ticket Management App (powered by Authly SSO)

## Context

Trackly is a standalone, multi-tenant ticket management SaaS. It is designed so that **any existing Authly tenant can plug Trackly in immediately** — users in that tenant can Single Sign-On (SSO) into Trackly with zero migration, no new passwords, and no separate identity store.

**Authly is the authentication layer.** Trackly does NOT build its own auth system — it delegates 100% of login, user management, roles, and token issuance to Authly. Since Authly already supports multi-tenancy (Organization → Projects/Tenants), each customer who buys Trackly gets their own Authly tenant, giving full isolation.

---

## How SSO Works Between an Authly Tenant and Trackly

This is the core design principle. Here is the exact flow, step by step:

### One-time Setup (done once per customer)

```
Authly TenantAdmin Console
  └─ Applications → "New Application"
       ├─ Name:          Trackly
       ├─ Type:          SPA (public client)
       ├─ Grant types:   authorization_code, refresh_token
       ├─ Redirect URI:  https://trackly.yourdomain.com/auth/callback
       └─ Scopes:        openid profile email roles
```

This produces a `client_id` (e.g. `client_abc123`). That value + the Authly tenant URL go into Trackly's environment variables:

```env
AUTHLY_ISSUER=https://auth.yourdomain.com   # your Authly instance URL
AUTHLY_CLIENT_ID=client_abc123              # the client_id from above
```

No client secret is needed — SPA clients use PKCE instead (Authly enforces this automatically for SPA type).

---

### SSO Login Flow (every time a user signs in)

```
1. User visits Trackly → clicks "Sign In"
        │
        ▼
2. Trackly SPA generates a PKCE pair:
   code_verifier  = random 64-byte string (kept in memory)
   code_challenge = SHA-256(code_verifier), base64url-encoded
        │
        ▼
3. Browser redirects to Authly:
   GET https://auth.yourdomain.com/connect/authorize
       ?response_type=code
       &client_id=client_abc123
       &redirect_uri=https://trackly.yourdomain.com/auth/callback
       &scope=openid profile email roles
       &state=<random csrf token>
       &code_challenge=<hash>
       &code_challenge_method=S256
        │
        ▼
4. Authly serves its own hosted login page
   User enters their existing Authly credentials (email + password, passkey, social, OTP — whatever they enrolled)
   MFA challenge fires if tenant policy requires it
        │
        ▼
5. Authly authenticates the user and redirects back:
   GET https://trackly.yourdomain.com/auth/callback
       ?code=<authorization_code>
       &state=<same csrf token>
        │
        ▼
6. Trackly SPA sends code + verifier to its own backend:
   POST /api/auth/token
   { code, code_verifier, redirect_uri }
        │
        ▼
7. Trackly backend calls Authly's token endpoint:
   POST https://auth.yourdomain.com/connect/token
   grant_type=authorization_code
   &code=<code>
   &code_verifier=<verifier>
   &client_id=client_abc123
   &redirect_uri=https://trackly.yourdomain.com/auth/callback
        │
        ▼
8. Authly validates PKCE and returns tokens:
   {
     "access_token":  "<JWT signed RS256>",
     "id_token":      "<JWT>",
     "refresh_token": "<opaque rotating token>",
     "expires_in":    3600
   }
        │
        ▼
9. Trackly backend:
   - Sets refresh_token in HttpOnly cookie (secure, SameSite=Strict)
   - Returns access_token to SPA in response body
        │
        ▼
10. Trackly SPA stores access_token in Zustand memory store (never localStorage)
    Reads user profile from JWT claims — no extra API call needed:
    {
      "sub":            "user-uuid",          ← Authly user ID
      "email":          "alice@acme.com",
      "name":           "Alice Smith",
      "picture":        "https://...",
      "tenant_id":      "tenant-uuid",        ← isolates data per customer
      "roles":          ["agent"],            ← drives access control in Trackly
      "email_verified": true,
      "exp":            1234571490
    }
```

### What the User Experiences

- They never see Trackly's own login page. They see Authly's hosted login (which can be white-labeled with the customer's branding, logo, and custom domain).
- If they are already logged into Authly (active session cookie), Authly skips the login form and immediately redirects back — **true SSO, zero clicks**.
- All MFA, passkeys, suspicious-login checks, and lockouts are handled entirely by Authly.

### Auto Login — Already Logged into Another App?

If the user is already signed into **any other SPA registered in the same Authly tenant**, they will be automatically logged into Trackly with zero interaction.

**Why this works:** When a user logs into any Authly-connected app, Authly sets an HttpOnly session cookie (`authly.user`) on the Authly domain (e.g. `auth.yourdomain.com`). That cookie persists in the browser independently of any individual app.

When Trackly redirects the user to Authly's `/connect/authorize`:

```
1. Browser hits auth.yourdomain.com/connect/authorize
        │
        ▼
2. Authly checks: does this browser have an active authly.user session cookie?
        │
   YES ─┘
        ▼
3. Authly skips the login page entirely
   Immediately redirects back to Trackly with an authorization code
        │
        ▼
4. Trackly exchanges the code → gets tokens → user is in
```

The user never sees a login form. It feels instant.

**The one condition:** Both apps must be registered as OAuth clients in the **same Authly tenant**. If App A is in Tenant A and Trackly is in Tenant B, the session cookie does not carry over — tenants are fully isolated.

**What if the Authly session has expired?**
Authly sessions have a sliding expiry (default 8 hours). If the user hasn't touched any Authly-connected app for 8 hours, the session expires and they'll see the login page once. After that single login, all apps connected to that tenant are back to auto-login for another 8 hours.

### Token Refresh (silent, keeps user logged in)

```
When access_token expires (default 1 hour):
  Trackly backend reads refresh_token from HttpOnly cookie
  → calls POST /connect/token with grant_type=refresh_token
  → Authly rotates and returns a new access_token + refresh_token
  → Trackly sets new refresh_token in cookie, returns new access_token to SPA
  → User sees nothing — session continues transparently
```

Refresh tokens are single-use and rotated by Authly on every use. If a stolen refresh token is replayed, Authly detects it (family reuse detection) and revokes the entire session family immediately.

### Logout

```
User clicks "Sign Out" in Trackly
  → Trackly clears access_token from memory + clears refresh_token cookie
  → Redirects to Authly RP-initiated logout:
    POST https://auth.yourdomain.com/connect/logout
  → Authly terminates the session
  → Authly redirects back to Trackly's post-logout URL
```

Signing out of Trackly also signs the user out of their Authly session (true SSO logout). If you want Trackly-local logout only (leave other apps logged in), skip the Authly redirect and just clear the local tokens.

---

## Tenant Isolation in Trackly

Every JWT issued by Authly includes `tenant_id`. Trackly reads this claim on every API request and scopes all database queries to that tenant:

```csharp
// Middleware extracts tenant_id from the validated JWT
var tenantId = Guid.Parse(httpContext.User.FindFirst("tenant_id")!.Value);

// Every EF Core query is filtered automatically
tickets.Where(t => t.TenantId == tenantId)
```

- Customer A's users can never see Customer B's tickets, even on a shared Trackly deployment
- No code change needed per customer — purely data-driven from the JWT claim

---

## System Overview

```
End User (browser)
    │
    ▼
React SPA                              ← customer portal + agent dashboard
    │  OAuth2 Authorization Code + PKCE
    ▼
Authly (OIDC Provider)                 ← login, MFA, sessions, RBAC + ABAC
    │  JWT access_token (RS256)
    ▼
ASP.NET Core Web API                   ← ticket business logic
    │
    ▼
PostgreSQL "trackly" database          ← separate DB on same PostgreSQL server as Authly
```

---

## Roles & Policies (Authly RBAC + ABAC, surfaced in JWT)

**RBAC — Role-Based Access Control** (who can do what by job title):

| Authly Role | Permissions in Trackly |
|-------------|------------------------|
| `trackly_customer` | Submit tickets, view own tickets, reply to agents |
| `trackly_agent`    | View all tickets, respond, change status, assign |
| `trackly_admin`    | Everything + manage categories, priorities, team settings |

These roles are created in each customer's Authly tenant, namespaced with a `trackly_` prefix (see "Role Setup When Adding Trackly to an Existing Authly Tenant" below — Authly roles are tenant-wide, not scoped per application, so plain names like `admin` would collide with other apps' roles in the same tenant). Trackly's backend strips the `trackly_` prefix before evaluating `[Authorize(Roles = "agent")]` on the API.

**ABAC — Attribute-Based Access Control** (fine-grained, context-sensitive rules):

Authly's ABAC `POST /api/v1/access/evaluate` can be called from the ticket API for rules that depend on *context*, not just role:

| Rule | Condition |
|------|-----------|
| Agent can only see tickets assigned to them | `resource.assignee_id == subject.sub` |
| Senior agents can override ticket priority | `subject.role == "agent" AND subject.seniority == "senior"` |
| Customers from specific org can view reports | `subject.org_id in ["acme", "globex"]` |

**When to use RBAC vs ABAC:**
- RBAC: main access gates (can a customer see the agent dashboard? No.)
- ABAC: nuanced rules within a role (can *this* agent reassign *this* ticket to another team?)

---

## How Customers Submit Tickets

Trackly supports **both anonymous and authenticated submission** — customers choose whichever suits them on the same page.

### Submission Page UX

When a customer lands on `/submit`, they see two clear paths side by side:

```
┌─────────────────────────────────────────────┐
│           Submit a Support Ticket           │
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │      Sign in with Authly  →           │  │
│  │  (Use your existing account)          │  │
│  └───────────────────────────────────────┘  │
│                                             │
│                 ── or ──                    │
│                                             │
│  Continue as Guest                          │
│  Name  _________________________________    │
│  Email _________________________________    │
│                                             │
│  [ Fill ticket form below once verified ]   │
└─────────────────────────────────────────────┘
```

**Path A — Sign in with Authly:**
1. Customer clicks "Sign in with Authly"
2. Redirected to Authly login (email, phone/WhatsApp OTP, social, passkey — whatever they enrolled)
3. On return, ticket form is pre-filled with their name and email/phone
4. They fill subject, description, category → submit
5. Ticket is tied to their Authly account and visible in their portal

**Path B — Continue as Guest:**
1. Customer enters name + email
2. Fills subject, description, category → clicks Submit
3. Trackly sends a 6-digit OTP to their email
4. Customer enters OTP to verify their email is real
5. Ticket is created — reference number shown on screen
6. Confirmation email sent with a magic link to track the ticket

Both paths lead to the **same ticket form fields** — the only difference is identity.

---

### Anonymous (no login required)

- Customer visits the public `/submit` page — no account needed
- Fills in: name, email, subject, description, category
- Clicks **Submit** → Trackly sends a **6-digit OTP** to their email
- Customer enters the OTP in Trackly to confirm their email is real
- Ticket is created only after OTP is verified
- Receives a **ticket reference number** (e.g. `TRK-1042`) on screen
- Receives a **confirmation email** with a magic link to track their ticket
- Clicking the magic link opens their specific ticket without any login

**Why Trackly sends the OTP (not Authly):**
Authly's magic link and OTP flows create a full Authly session with a session cookie. That cookie would allow the anonymous guest to auto-login into any other app registered in the same tenant — which is not desirable. By sending the OTP from Trackly independently, the guest's email is verified without creating any Authly session. They remain anonymous with no access to other apps.

### Logged-in (Authly SSO)

- Customer signs in via Authly SSO
- Gets a full **customer portal** at `/portal/tickets`
- Sees all their past tickets in one place
- Replies and updates are tied to their identity
- Supports **email login, social login, passkeys, and phone/WhatsApp OTP** — whatever the user enrolled in Authly. Trackly receives the same JWT regardless of which method was used.

### Phone / WhatsApp Login Support

Authly supports phone number login via WhatsApp or SMS OTP natively. When a user authenticates this way, Authly issues the same JWT structure — the only difference is `email` may be absent if the user only registered with a phone number.

Trackly handles this gracefully:

```json
{
  "sub":            "user-uuid",
  "phone":          "+919876543210",
  "phone_verified": true,
  "email":          null,           ← absent for phone-only users
  "name":           "Ravi Kumar",
  "roles":          ["customer"]
}
```

**What Trackly does differently for phone users:**
- Uses `phone` as the display identifier where `email` would normally appear
- Sends ticket notifications via WhatsApp/SMS instead of email (if messaging is configured)
- `user_cache` stores `phone` alongside `email` — either can be null but not both

No changes are needed to the auth flow itself — Trackly receives a valid JWT from Authly regardless of whether the user signed in with email, phone, social, or passkey.

---

### Linking Anonymous Tickets to an Account

If an anonymous customer later signs in (or signs up) with the **same email address**, their anonymous tickets are automatically linked to their Authly account and appear in their portal history.

### What This Requires

| Addition | Purpose |
|----------|---------|
| Public `/submit` route | Anonymous ticket form — no auth required |
| Trackly-side OTP send + verify | Verifies guest email without creating an Authly session |
| `guest_email` column on tickets | Stores verified submitter email for anonymous tickets |
| `guest_name` column on tickets | Stores submitter name for anonymous tickets |
| Magic link in confirmation email | Lets anonymous users view/track their ticket without login |
| Email-to-account linking on login | Merges anonymous tickets into account on first SSO login |
| Phone/WhatsApp JWT support | Handle JWTs where `email` is null and `phone` is the identifier |

---

## Agent Dashboard & Ticket Assignment

There is **no separate website for agents**. Trackly is one app — the UI adapts based on the `roles` claim in the Authly JWT:
- `customer` → lands on `/portal`
- `agent` / `admin` → lands on `/dashboard`

Same URL, same deployment, no duplication.

### Ticket Assignment

New tickets are **auto-assigned via round-robin** across all active agents, with the ability to **manually reassign** at any time.

**Round-robin logic:**
```
New ticket arrives
  → Query user_cache for all agents in this tenant
  → Find the agent with the fewest open tickets (weighted round-robin)
  → Assign ticket to that agent
  → Send assignment email notification to the agent
```

**Manual override:**
- Any `agent` or `admin` can reassign a ticket to a different agent from the ticket detail page
- Reassignment triggers a new assignment email to the newly assigned agent

**What this requires:**

| Addition | Purpose |
|----------|---------|
| `AssignTicketService` | Round-robin logic — picks agent with fewest open tickets |
| Reassign endpoint `PATCH /api/tickets/{id}` | Already planned — `assignee_id` update |
| Assignment history | Log of who was assigned when (for audit trail) |

```sql
CREATE TABLE ticket_assignments (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id   UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    assigned_to UUID NOT NULL,   -- Authly sub of agent
    assigned_by UUID,            -- Authly sub of admin/agent who reassigned; null if auto
    assigned_at TIMESTAMPTZ DEFAULT now()
);
```

### Ticket Watchers

Any agent or admin can be added as a **watcher** on a ticket. Watchers are not responsible for resolving the ticket but receive email notifications on every update — new reply, status change, or reassignment.

**How it works:**
- From the ticket detail page, the assigned agent or admin can add/remove watchers via an agent selector
- Watchers see the ticket in a "Watching" filter in their dashboard
- Every notification event that fires for the assigned agent also fires for all watchers
- Watchers can remove themselves at any time

**API endpoints:**
```
GET    /api/tickets/{id}/watchers          -- list current watchers
POST   /api/tickets/{id}/watchers          -- add a watcher { agentId }
DELETE /api/tickets/{id}/watchers/{userId} -- remove a watcher
```

**Database:**
```sql
CREATE TABLE ticket_watchers (
    ticket_id  UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    agent_id   UUID NOT NULL,   -- Authly sub of watching agent
    added_by   UUID NOT NULL,   -- Authly sub of who added them
    added_at   TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (ticket_id, agent_id)
);
```

**Notification flow with watchers:**
```
Any ticket update (reply, status change, reassignment)
  → Notify assigned agent (if enabled)
  → Notify all watchers (if agent notification type is enabled)
  → Notify customer (if customer notification type is enabled)
```

---

## Email Notifications

All notifications are **on by default** but can be individually enabled or disabled by the admin per tenant in `/admin/settings/notifications`.

### Notification Types

| Trigger | Recipient | Configurable |
|---------|-----------|-------------|
| Ticket created (guest) | Customer — confirmation + magic link | Yes |
| Ticket created (logged in) | Customer — confirmation | Yes |
| Ticket assigned to agent | Agent | Yes |
| Customer replied to ticket | Assigned agent | Yes |
| Agent replied to ticket | Customer | Yes |
| Ticket status changed | Customer | Yes |
| Ticket reassigned | Newly assigned agent | Yes |

### Notification Settings (per tenant, stored in DB)

```sql
CREATE TABLE notification_settings (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                   UUID NOT NULL UNIQUE,
    notify_customer_on_create   BOOLEAN DEFAULT true,
    notify_customer_on_reply    BOOLEAN DEFAULT true,
    notify_customer_on_status   BOOLEAN DEFAULT true,
    notify_agent_on_assign      BOOLEAN DEFAULT true,
    notify_agent_on_reply       BOOLEAN DEFAULT true,
    notify_agent_on_reassign    BOOLEAN DEFAULT true,
    updated_at                  TIMESTAMPTZ DEFAULT now()
);
```

---

## Email Interaction Mode

Admins configure how deep email interaction goes in `/admin/settings/email`. Three modes are available:

| Mode | What it means |
|------|--------------|
| **Notifications only** | Emails are sent but replies go nowhere. Agent and customer must log into Trackly to reply. No inbound email setup needed. |
| **One-way (customer replies via email)** | Customer can reply to notification emails and the reply appears in the ticket. Agent still logs into Trackly to respond. |
| **Full two-way threading** | Both agent and customer can reply directly via email. All replies are captured in the ticket thread. Requires inbound email provider setup. |

```sql
ALTER TABLE email_configs ADD COLUMN email_mode TEXT NOT NULL DEFAULT 'notifications_only';
-- values: 'notifications_only', 'one_way', 'two_way'
```

Inbound email provider fields (`inbound_provider`, `inbound_reply_domain`, `inbound_webhook_secret`) are only required when mode is `one_way` or `two_way`. The admin panel shows/hides those fields based on the selected mode.

---

## Full Two-Way Email Threading

Both agents and customers can reply directly via email — replies are captured and appear in the ticket thread in Trackly. Neither party needs to log in just to reply.

### How It Works

**Outbound — every notification email Trackly sends includes:**
```
Reply-To:   reply+<ticket-uuid>@tickets.yourdomain.com
Message-ID: <ticket-uuid>.<comment-uuid>@trackly
```

**Inbound — when agent or customer hits Reply in their email client:**
```
1. Reply goes to reply+<ticket-uuid>@tickets.yourdomain.com
        │
        ▼
2. Catch-all mailbox receives it
   Inbound email provider parses it and POSTs to Trackly webhook:
   POST /api/email/inbound
        │
        ▼
3. Trackly extracts ticket UUID from the reply+ address
   Strips quoted original text (keeps only the new reply)
   Identifies the author by From: email → matches user_cache or guest_email
        │
        ▼
4. New comment created on the ticket
        │
        ▼
5. Other party notified (email + in-app)
```

**Example inbound address:**
```
reply+a3f7c291-4b2e-4c1d-9f3a-123456789abc@tickets.yourdomain.com
              ↑ ticket UUID embedded — tells Trackly exactly which ticket
```

### Inbound Email Provider

Trackly needs a provider that supports **inbound email parsing** and delivers it to a webhook. Admin configures this in `/admin/settings/email`:

| Provider | Notes |
|----------|-------|
| Mailgun | Inbound routes → webhook POST |
| SendGrid Inbound Parse | Webhook POST with parsed fields |
| Postmark | Clean inbound webhook API |
| AWS SES + S3 | Fully self-hostable option |

### Security — Inbound Webhook Verification

To prevent spoofed emails from injecting fake comments into tickets:
- Inbound webhook endpoint verifies the provider's HMAC signature on every request
- `From:` email must match a known user (`user_cache`) or a known guest (`guest_email` on the ticket)
- Unknown senders receive an auto-reply explaining they are not authorized for this ticket

### What This Requires

| Addition | Purpose |
|----------|---------|
| `POST /api/email/inbound` | Webhook endpoint for inbound email provider |
| `InboundEmailService` | Parse provider payload, extract ticket ID, strip quoted text, create comment |
| `reply+<uuid>@` catch-all mailbox | DNS MX record pointing to inbound provider |
| HMAC signature verification | Prevent spoofed inbound emails |
| Quoted-text stripper | Remove `> On 12 Jun, Alice wrote:` from replies |

```sql
-- Track which comments originated from email vs in-app
ALTER TABLE comments ADD COLUMN source TEXT DEFAULT 'web'; -- 'web' or 'email'
ALTER TABLE comments ADD COLUMN email_message_id TEXT;     -- for threading headers
```

---

## Email Configuration

Email sending is **configurable per tenant**. Admin chooses one of two options in `/admin/settings/email`:

### Option A — Reuse Authly's SMTP Config
If Authly is already configured with an SMTP provider (MSG91, Zepto, SendGrid, etc.), Trackly can call Authly's internal messaging infrastructure directly — no separate config needed. This is the default zero-setup path.

### Option B — Tenant-provided SMTP
Admin enters their own SMTP credentials in Trackly's admin panel. Trackly uses these for all outbound emails for that tenant, completely independent of Authly.

```sql
CREATE TABLE email_configs (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    UUID NOT NULL UNIQUE,
    use_authly   BOOLEAN DEFAULT true,   -- true = delegate to Authly, false = own SMTP
    smtp_host              TEXT,
    smtp_port              INT,
    smtp_user              TEXT,
    smtp_pass              TEXT,         -- encrypted at rest (AES-256-GCM)
    from_name              TEXT,         -- e.g. "Acme Support"
    from_email             TEXT,         -- e.g. "support@acme.com"
    inbound_provider       TEXT,         -- mailgun, sendgrid, postmark, ses
    inbound_reply_domain   TEXT,         -- e.g. "tickets.acme.com" for reply+ addresses
    inbound_webhook_secret TEXT,         -- HMAC secret for verifying inbound provider — encrypted
    updated_at             TIMESTAMPTZ DEFAULT now()
);
```

**Security:** `smtp_pass` is encrypted at rest using AES-256-GCM (same approach as Authly).

---

## Broadcast Announcements — Outage Emails

Admins can send a mass email to **all customers who have an Authly login** in the tenant. Designed for planned and unplanned outage communications.

### Announcement Types

| Type | Use case |
|------|---------|
| `planned_outage` | Scheduled maintenance window communicated in advance |
| `unplanned_outage` | Unexpected downtime or degraded service |
| `resolved` | Follow-up to confirm the issue is fixed |
| `general` | Any other broadcast (release notes, policy changes, etc.) |

### How It Works

```
Admin creates announcement in /admin/announcements
  → Sets type, subject, body, scheduled time (or send now)
        │
        ▼
Trackly fetches all users from user_cache
  WHERE tenant_id = current tenant
  AND 'customer' = ANY(roles)
  AND email IS NOT NULL          ← Authly-login users only, skips phone-only users
        │
        ▼
Background job fans out emails in batches (to avoid SMTP rate limits)
        │
        ▼
Delivery status tracked per recipient (sent, failed, bounced)
        │
        ▼
Admin sees delivery report in /admin/announcements/{id}
```

### Link to a Problem (optional)

An announcement can optionally be linked to a Problem so agents have full context:

```
Problem: "Payment gateway down"
  ├── Linked tickets (incidents)
  └── Linked announcement → "We are aware of payment issues..." email sent to all customers
```

### Scheduling

- **Send now** — background job fires immediately
- **Scheduled** — admin picks a future date/time, job is queued and fires at that time
- **Cancel** — scheduled announcements can be cancelled before they fire

### Behaviour Details

- Only customers with an **Authly login** receive the broadcast — anonymous/guest users who only submitted a ticket via the public form are excluded (Trackly does not have a verified opt-in relationship with them)
- Phone-only Authly users (no email) are skipped — broadcast is email only
- Admin can preview the email before sending
- Each announcement is logged with: subject, body, type, sender, sent_at, recipient count, success count, failure count

### API Endpoints

```
GET    /api/announcements              -- list all announcements (admin)
POST   /api/announcements              -- create and send/schedule
GET    /api/announcements/{id}         -- detail + delivery report
DELETE /api/announcements/{id}         -- cancel a scheduled announcement (not yet sent)
```

### Database

```sql
CREATE TABLE announcements (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        UUID NOT NULL,
    type             TEXT NOT NULL,   -- planned_outage, unplanned_outage, resolved, general
    subject          TEXT NOT NULL,
    body             TEXT NOT NULL,
    problem_id       UUID REFERENCES problems(id) ON DELETE SET NULL,
    created_by       UUID NOT NULL,   -- Authly sub of admin
    scheduled_at     TIMESTAMPTZ,     -- null = send immediately
    sent_at          TIMESTAMPTZ,     -- null until dispatched
    recipient_count  INT DEFAULT 0,
    success_count    INT DEFAULT 0,
    failure_count    INT DEFAULT 0,
    created_at       TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE announcement_deliveries (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    announcement_id UUID NOT NULL REFERENCES announcements(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL,    -- Authly sub
    email           TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',  -- pending, sent, failed, bounced
    sent_at         TIMESTAMPTZ,
    error           TEXT              -- error message if failed
);
```

---

## Problems — Grouping Related Tickets

When multiple customers report the same underlying issue, agents can group those tickets under a **Problem**. The Problem tracks the root cause while the individual tickets (called **incidents**) track each customer's experience.

### Concept

```
Problem: "Payment gateway down"          ← root cause, worked on by agents
  ├── Ticket #1042 — Alice: "Can't pay"  ← incident
  ├── Ticket #1043 — Bob: "Checkout failing" ← incident
  └── Ticket #1044 — Carol: "Payment error"  ← incident
```

### Behaviour

- Agents create a Problem from the agent dashboard (`/dashboard/problems`)
- Any ticket can be linked to a Problem — one ticket can only belong to one Problem at a time
- The Problem has its own title, description, status, and assigned agent (separate from individual tickets)
- When a Problem is marked **resolved**, agents can optionally bulk-resolve all linked tickets in one action
- Customers are not aware of the Problem grouping — they only see their own ticket and its status
- Agents and admins can see the Problem panel on each linked ticket showing: Problem title, status, and how many other tickets are linked

### Problem Statuses

| Status | Meaning |
|--------|---------|
| `investigating` | Root cause being identified |
| `identified` | Root cause known, fix in progress |
| `monitoring` | Fix applied, watching for recurrence |
| `resolved` | Problem fully resolved |

### API Endpoints

```
GET    /api/problems                        -- list all problems (agent/admin)
POST   /api/problems                        -- create a problem
GET    /api/problems/{id}                   -- get problem detail + linked tickets
PATCH  /api/problems/{id}                   -- update title, status, assignee
DELETE /api/problems/{id}                   -- delete problem (unlinks tickets, does not delete them)
POST   /api/problems/{id}/tickets           -- link a ticket { ticketId }
DELETE /api/problems/{id}/tickets/{ticketId}-- unlink a ticket
POST   /api/problems/{id}/resolve-all       -- resolve problem + bulk-resolve all linked tickets
```

### Database

```sql
CREATE TABLE problems (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL,
    title       TEXT NOT NULL,
    description TEXT,
    status      TEXT NOT NULL DEFAULT 'investigating', -- investigating, identified, monitoring, resolved
    assignee_id UUID,              -- Authly sub of agent handling the problem
    created_by  UUID NOT NULL,     -- Authly sub
    created_at  TIMESTAMPTZ DEFAULT now(),
    updated_at  TIMESTAMPTZ DEFAULT now(),
    resolved_at TIMESTAMPTZ
);

-- Link tickets to a problem (one ticket → one problem max)
ALTER TABLE tickets ADD COLUMN problem_id UUID REFERENCES problems(id) ON DELETE SET NULL;
```

### Notifications When Problem Resolves

When an agent resolves a Problem and bulk-resolves linked tickets:
- Each linked ticket is marked `resolved`
- Each customer whose ticket was linked receives their normal "ticket resolved" email notification
- Watchers on each linked ticket are notified per the existing watcher notification rules

---

## Embeddable Widget & Integration Options

Trackly provides multiple ways for existing applications to embed the ticket submission form — admins configure it once and copy a generated snippet.

### Widget Builder (Admin Panel)

Located at `/admin/widget` in the Trackly admin panel. The admin:

1. **Chooses an embed type:**

| Type | How it works |
|------|-------------|
| Floating button | A fixed button (bottom-right corner) on any page. Opens ticket form in a pop-up overlay. Drop in one `<script>` tag. |
| Inline iframe | An `<iframe>` snippet pasted into any page. Renders the form inline within the existing page layout. |
| Direct link | A standalone URL (e.g. `https://trackly.yourdomain.com/submit?tenant=acme`) to link to from any app. No code needed. |

2. **Configures which fields to show:**

| Field | Options |
|-------|---------|
| Name | Show / Hide / Pre-fill from parent app |
| Email | Show / Hide / Pre-fill from parent app |
| Subject | Show / Hide / Required |
| Description | Show / Hide / Required |
| Category | Show / Hide / Limit to specific categories |
| Priority | Show / Hide / Lock to a default value |

3. **Copies the generated embed snippet** — Trackly builds it automatically based on the config:

```html
<!-- Floating widget -->
<script
  src="https://trackly.yourdomain.com/widget.js"
  data-client="acme-tenant"
  data-fields="name,email,subject,description"
  data-theme="light"
></script>
```

### Pre-filling from the Parent App

If the parent app already knows who the user is, it can pass their details into the widget as data attributes. Trackly pre-fills those fields and optionally hides them so the user only sees what they actually need to fill in:

```html
<script
  src="https://trackly.yourdomain.com/widget.js"
  data-client="acme-tenant"
  data-user-name="Alice Smith"
  data-user-email="alice@acme.com"
></script>
```

When `name` and `email` are pre-filled, those fields are hidden in the form — the user only sees subject, description, and category. The OTP step is still triggered to verify the pre-filled email unless the parent app passes a verified Authly JWT alongside.

### Authly SSO in the Widget

The widget also offers the "Sign in with Authly" option inside the pop-up/iframe. If the user is already logged into Authly (session cookie on the Authly domain), clicking it auto-logs them in and pre-fills all their details instantly — no OTP needed.

### What This Requires

| Addition | Purpose |
|----------|---------|
| `/admin/widget` page | Visual widget builder UI for admins |
| `widget_configs` DB table | Stores field visibility, embed type, theme per tenant |
| `widget.js` script | Embeddable JS that renders the floating button + iframe |
| `/embed/submit` route | Iframe-safe version of the submit form (no nav bar, minimal chrome) |
| `data-*` attribute parsing | Widget reads pre-fill values from the script tag |

### Database — Widget Config Table

```sql
CREATE TABLE widget_configs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id   UUID NOT NULL UNIQUE,
    embed_type  TEXT NOT NULL DEFAULT 'floating', -- floating, iframe, link
    fields      JSONB NOT NULL,                   -- field visibility + required config
    theme       TEXT NOT NULL DEFAULT 'light',    -- light, dark, auto
    created_at  TIMESTAMPTZ DEFAULT now(),
    updated_at  TIMESTAMPTZ DEFAULT now()
);

-- Example fields JSONB:
-- {
--   "name":        { "show": true,  "required": true,  "prefillKey": "data-user-name" },
--   "email":       { "show": true,  "required": true,  "prefillKey": "data-user-email" },
--   "subject":     { "show": true,  "required": true  },
--   "description": { "show": true,  "required": true  },
--   "category":    { "show": true,  "required": false, "limitTo": ["billing", "tech"] },
--   "priority":    { "show": false, "lockedValue": "medium" }
-- }
```

---

## Components to Build

### 1. React Frontend (Vite + TypeScript)

**Stack:** React 18, TypeScript, Vite, Material UI, TanStack Query, React Router v6, React Hook Form + Zod, Zustand

**Pages / Areas:**

| Area | Routes | Auth required | Who sees it |
|------|--------|--------------|-------------|
| Public ticket form | `/submit` | No | Anyone |
| Anonymous ticket view | `/tickets/:id?token=<magic>` | No (magic link token) | Anonymous submitter |
| Auth | `/login` → redirect to Authly | No | All |
| Auth callback | `/auth/callback` | No | All |
| Customer portal | `/portal/tickets`, `/portal/tickets/new`, `/portal/tickets/:id` | Yes | `customer` |
| Agent dashboard | `/dashboard/tickets`, `/dashboard/tickets/:id` | Yes | `agent`, `admin` |
| Admin settings | `/admin/categories`, `/admin/team`, `/admin/settings` | Yes | `admin` |

**Key shared components:**
- `<TicketCard>`, `<StatusBadge>`, `<PriorityChip>`, `<CommentThread>`
- `<TicketFilters>` (status, priority, category, assignee, date range)
- `<AgentSelector>` dropdown
- `<PublicTicketForm>` (name, email, subject, description, category — no auth)

---

### 2. ASP.NET Core Web API

**Solution structure:**

```
src/
  Trackly.Core/           # Entities, interfaces, enums
  Trackly.Modules/        # Business logic (Tickets, Comments, Categories, Notifications)
  Trackly.Infrastructure/ # EF Core, repositories, email adapter
  Trackly.Api/            # Controllers, middleware, auth config
```

**JWT Validation (from Authly):**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = "https://your-authly-instance.com";
        options.Audience  = "trackly";
        options.TokenValidationParameters = new() {
            ValidateIssuer   = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            // JWKS fetched automatically from /.well-known/jwks.json
        };
    });
```

**API endpoints:**

| Method | Path | Auth | Role |
|--------|------|------|------|
| POST   | `/api/auth/token` | None | Public — PKCE code exchange |
| POST   | `/api/tickets/guest` | None | Public — anonymous ticket submission |
| GET    | `/api/tickets/guest/{id}?token=` | None | Public — anonymous magic link view |
| POST   | `/api/tickets/guest/{id}/comments?token=` | None | Public — anonymous reply |
| GET    | `/api/tickets` | JWT | agent/admin (all); customer (own only) |
| POST   | `/api/tickets` | JWT | customer, agent, admin |
| GET    | `/api/tickets/{id}` | JWT | owner or agent/admin |
| PATCH  | `/api/tickets/{id}` | JWT | agent (status, assignee); customer (reply only) |
| POST   | `/api/tickets/{id}/comments` | JWT | owner or agent/admin |
| GET    | `/api/tickets/{id}/comments` | JWT | owner or agent/admin |
| GET    | `/api/categories` | JWT | all authenticated |
| POST   | `/api/categories` | JWT | admin |
| GET    | `/api/users/me` | JWT | all (profile from JWT claims) |

**User profile:** Derived entirely from Authly JWT claims — no separate users table. A lightweight `user_cache` table (userId, email, name, lastSeen) is maintained on-login for agent assignment dropdowns.

---

### 3. Database Schema (PostgreSQL — `trackly` database)

```sql
CREATE TABLE tickets (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        UUID NOT NULL,
    subject          TEXT NOT NULL,
    description      TEXT NOT NULL,
    status           TEXT NOT NULL DEFAULT 'open',    -- open, pending, resolved, closed
    priority         TEXT NOT NULL DEFAULT 'medium',  -- low, medium, high, urgent
    category_id      UUID REFERENCES categories(id),
    requester_id     UUID,         -- Authly sub (null if anonymous)
    guest_email      TEXT,         -- set for anonymous submissions
    guest_name       TEXT,         -- set for anonymous submissions
    guest_token_hash TEXT,         -- SHA-256 of magic link token for anonymous access
    assignee_id      UUID,         -- Authly sub of agent
    created_at       TIMESTAMPTZ DEFAULT now(),
    updated_at       TIMESTAMPTZ DEFAULT now(),
    -- either requester_id (logged-in) or guest_email (anonymous) must be set
    CONSTRAINT requester_or_guest CHECK (requester_id IS NOT NULL OR guest_email IS NOT NULL)
);

CREATE TABLE comments (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id   UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    author_id   UUID NOT NULL,    -- Authly sub
    body        TEXT NOT NULL,
    is_internal BOOLEAN DEFAULT false,  -- true = private note, false = visible to customer
    created_at  TIMESTAMPTZ DEFAULT now()
);

### Private Notes (Internal Comments)

Agents and admins can add **private notes** on any ticket — visible only to other agents, admins, and watchers. The customer never sees them.

**Behaviour:**
- In the ticket thread, agents see a toggle: **Reply** (public) vs **Private Note** (internal)
- Private notes are visually distinct in the UI — amber/yellow background with a lock icon so agents always know what's internal
- Visibility is **locked on creation** — a public reply cannot be made private after posting and vice versa
- Private notes do **not** trigger customer email notifications
- Private notes **do** notify the assigned agent and all watchers (so agents can collaborate internally)

**API enforcement:**
```
GET /api/tickets/{id}/comments
  → customer role or guest magic link token: WHERE is_internal = false only
  → agent or admin role: all comments returned

POST /api/tickets/{id}/comments  { body, is_internal }
  → customer or guest: is_internal is ignored, always set to false
  → agent or admin: can set is_internal = true
```

---

CREATE TABLE categories (
    id        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name      TEXT NOT NULL,
    color     TEXT
);

CREATE TABLE user_cache (
    id         UUID PRIMARY KEY,  -- Authly sub
    tenant_id  UUID NOT NULL,
    email      TEXT,              -- null for phone-only users
    phone      TEXT,              -- null for email-only users; at least one must be set
    name       TEXT,
    avatar_url TEXT,
    roles      TEXT[],
    last_seen  TIMESTAMPTZ,
    CONSTRAINT email_or_phone CHECK (email IS NOT NULL OR phone IS NOT NULL)
);
```

---

### 4. Authly Setup (per customer)

1. TenantAdmin → Applications → New SPA client → copy `client_id`
2. Roles → create `trackly_customer`, `trackly_agent`, `trackly_admin` (see naming rationale below — plain names like `admin` risk colliding with other apps in the same tenant)
3. Users → assign roles
4. Set `AUTHLY_ISSUER` + `AUTHLY_CLIENT_ID` in Trackly env — done

Existing users in that tenant SSO into Trackly immediately with no migration.

---

### Role Setup When Adding Trackly to an Existing Authly Tenant

Verified directly against the Authly codebase (`AuthorizationController.cs:107-110`, `Role.cs`, `UserRoleRepository.cs`). Two facts drive this design:

**1. Authly's Organization/Operator roles are unrelated to Trackly.**
Authly has a separate role system for people who manage the Authly console itself (`Account` entity, `OrganizationMembership`, roles like `org_owner`, `org_admin`, `project_admin`, `viewer`). These operators never receive OAuth tokens and have nothing to do with Trackly's customers or agents. Ignore this system entirely when setting up Trackly — it is a different system from the `User`/`Role` RBAC described in this plan.

**2. Roles for actual end-users (the `User` entity) are tenant-wide, not scoped per application.**
There is no concept of "this role only applies to App A" in Authly's data model — a `Role` belongs to a tenant, full stop (no `ApplicationId` on `Role`). When Authly issues a JWT for **any** OAuth client in that tenant, the `roles` claim contains **every role the user holds across the entire tenant** — not just roles relevant to the app requesting the token. This was confirmed in `AuthorizationController.cs:107-110`, where role lookup is filtered only by `TenantId` and `UserId`, never by `ApplicationId`.

**What this means in practice:**

If a company already has "App A" running on Authly with roles like `admin`, `editor`, `viewer`, and now adds Trackly:

- You **cannot** reuse the name `admin` for Trackly — it would be the literal same tenant-wide role, so assigning it to a Trackly agent would also grant App A's `admin` permissions, and vice versa
- Trackly must use **distinctly namespaced role names**: `trackly_customer`, `trackly_agent`, `trackly_admin`
- A user's JWT issued for Trackly will still contain App A's roles too, e.g. `roles: ["admin", "trackly_agent"]` — Trackly's backend must filter for `trackly_*` prefixed roles only and ignore anything else present in the claim

**Onboarding existing users — explicit opt-in required:**

Existing users in the tenant can SSO into Trackly with zero password/account setup (same Authly login). But having an Authly account in the tenant does **not** automatically grant Trackly access — an admin must explicitly assign one of the `trackly_*` roles to each user who should use Trackly:

```
TenantAdmin → Users → select existing user → Roles tab → assign trackly_customer / trackly_agent / trackly_admin
```

A user with no `trackly_*` role can still authenticate via SSO (Authly lets them log in fine), but Trackly's API should reject them with "no Trackly role assigned" rather than silently granting default access.

**⚠️ Known gap: Authly does not support bulk role assignment today.**

Verified directly against the codebase:
- API: `POST /api/v1/users/{userId}/roles` ([UsersController.cs:103-110](../src/Authly.Web/Controllers/Api/UsersController.cs)) accepts exactly one `userId` (route) and one `roleId` (body) per call — no array/list support
- `AssignRoleDto` ([ApiDtos.cs:77-80](../src/Authly.Web/Controllers/Api/ApiDtos.cs)) has only a single `RoleId` property
- `IRbacService.AssignRoleAsync(tenantId, userId, roleId, ...)` ([IRbacService.cs:31-32](../src/Authly.Core/Interfaces/IRbacService.cs)) — singular signature, no bulk variant
- TenantAdmin UI ([Areas/TenantAdmin/Controllers/UsersController.cs:207-222](../src/Authly.Web/Areas/TenantAdmin/Controllers/UsersController.cs)) — one dropdown, one user, one role per request; the Users list page has no multi-select checkboxes
- The existing bulk **user import** feature ([UserImportService.cs:114-151](../src/Authly.Modules/Users/UserImportService.cs)) creates users in bulk but does **not** assign roles as part of that import — roles still have to be set per-user afterward

**Practical impact:** A company onboarding, say, 50 existing users to Trackly would need to open each user individually in TenantAdmin and assign a `trackly_*` role one at a time. This does not block Trackly's launch, but it is a real friction point worth reviewing before go-live.

**Needs review before Trackly setup at any customer with a non-trivial existing user base:**
1. Decide whether to extend Authly itself with a bulk-assign feature (benefits every app on Authly, not just Trackly) — likely the right long-term fix since this is an Authly platform gap, not a Trackly-specific one
2. In the meantime, a one-off script against the existing single-user API can loop through a CSV/list of user IDs and assign roles — viable for initial setup at a new customer, not a permanent solution
3. Revisit this decision once we see how many users a typical customer onboards at once — small tenants may never feel this gap

**Recommended role-claim handling in Trackly's backend:**

```csharp
// JWT roles claim may contain roles belonging to other apps in the same tenant — filter explicitly
var racklyRoles = jwtRoles
    .Where(r => r.StartsWith("trackly_"))
    .Select(r => r.Replace("trackly_", ""));
// e.g. "trackly_agent" -> "agent" for use in [Authorize(Roles = "agent")]
```

---

### 5. User Sync (Authly → Trackly)

**On-login** (primary): `/api/auth/token` upserts `user_cache` row from JWT claims on every login.

**Authly Webhooks** (optional): Subscribe to `user.suspended` / `user.deleted` → unassign tickets or flag requester as inactive.

---

## Tech Stack Summary

| Layer | Choice | Reason |
|-------|--------|--------|
| Frontend | React 18 + Vite + MUI + TanStack Query + Zustand + RHF + Zod | Type-safe, fast SPA |
| State | Zustand | Auth state is minimal — Redux Toolkit is overkill |
| Backend | ASP.NET Core Web API (.NET 9+) | Same ecosystem as Authly |
| ORM | Entity Framework Core | Consistent with Authly |
| Database | PostgreSQL (`trackly` DB, same server as Authly) | No extra infra; RLS available |
| Token storage | Access token in memory; refresh token in HttpOnly cookie | Secure against XSS |

---

## Verification Checklist

- [ ] Register Trackly as SPA OAuth client in a local Authly instance
- [ ] Run React dev server — confirm redirect to Authly login, callback exchanges code, JWT decoded
- [ ] Verify `roles` claim drives access: customer cannot reach `/dashboard`, agent can
- [ ] Create ticket as customer, respond as agent, confirm comment thread works
- [ ] Suspend a user in Authly → confirm ticket access is denied on next request
- [ ] Test token refresh — access token expires, silent refresh via cookie succeeds
- [ ] Test SSO: log into another Authly-connected app first, then open Trackly — should skip login
