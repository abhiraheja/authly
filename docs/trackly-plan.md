# Trackly — Ticket Management App

## Context

Trackly is a standalone, multi-tenant ticket management SaaS that can be sold to **any organisation** regardless of their existing identity infrastructure. Authly is supported as one of many identity providers — not a hard dependency.

This design mirrors how products like Claude for Teams, Notion, and GitHub handle enterprise SSO: each workspace configures the identity provider they already use (Okta, Google Workspace, Microsoft Entra ID, Authly, or plain email+password), and Trackly works with all of them identically.

**Trackly owns its own identity layer.** Users, roles, and sessions are all managed in Trackly's own database. External IdPs are used only for authentication — they never dictate what a user can do inside Trackly.

---

## System Overview

```
End User (browser)
    │
    ▼
React SPA                              ← customer portal + agent dashboard
    │  OIDC (Authorization Code + PKCE) or SAML
    ▼
Configured IdP for this workspace      ← Authly / Okta / Entra ID / Google / Custom / Email+Password
    │  user identity (sub, email, name, groups)
    ▼
Trackly ASP.NET Core Web API           ← JIT provisions user, issues Trackly session
    │  Trackly session cookie (HttpOnly)
    ▼
PostgreSQL "trackly" database          ← workspaces, users, roles, tickets, etc.
```

---

## Authentication Architecture

### Workspaces (Trackly's own multi-tenancy)

Trackly has its own `workspaces` table — this replaces any dependency on an external IdP's tenant concept. Each customer gets one workspace. All data (tickets, users, roles, settings) is scoped to a `workspace_id` in Trackly's own DB.

### Supported Identity Providers

Each workspace configures exactly one SSO connection (like Claude's model — one active provider at a time, switchable). Additionally, email+password is always available as a fallback unless the admin disables it (stored as `password_login_enabled` on the workspace — see schema).

| Provider | Protocol | Notes |
|----------|---------|-------|
| **Authly** | Custom OIDC | Your own product — first-class support |
| **Google Workspace** | SAML or OIDC | Most common for SMBs |
| **Microsoft Entra ID** | SAML | Enterprise standard |
| **Okta** | SAML | Enterprise standard |
| **Auth0** | SAML or OIDC | Common in SaaS companies |
| **Custom SAML** | SAML | Any SAML 2.0 compliant IdP |
| **Custom OIDC** | OIDC | Any OIDC compliant IdP |
| **Email + Password** | Native | Trackly manages credentials itself |

---

### SSO Configuration Wizard (per workspace)

Admin sets up SSO at `/admin/settings/sso`. The wizard follows the same pattern as Claude's SSO setup (as seen in the reference screenshots):

```
Step 1 — Select your identity provider
         (list of pre-built providers + Custom SAML + Custom OIDC)
         ↓
Step 2 — Create an application in your IdP
         Trackly shows: redirect/callback URI to register in the IdP
         ↓
Step 3 — Add required claims
         IdP must send: sub (required), email (required),
                        given_name (required), family_name (required),
                        groups (optional — used for auto role mapping)
         ↓
Step 4 — Provide your OIDC / SAML configuration
         OIDC: Discovery endpoint URL, Client ID, Client Secret
               (Client Secret is optional if the IdP supports PKCE for
                public clients — e.g. Authly SPA clients)
         SAML: IdP metadata URL (or paste XML), SP Entity ID
         ↓
Step 5 — Configure group → role mapping (optional)
         e.g. Okta group "support-agents"  → agent
              Okta group "support-admins"  → admin
              (everyone else)              → customer
         ↓
Step 6 — Test Single Sign-On
         Trackly initiates a test auth flow and confirms claims are received
```

**Switching providers:** Admin can switch to a different provider at any time. Existing user records and tickets are preserved — only the SSO connection changes. User identity records (`user_identities`) for the old provider are kept but marked inactive (`is_active = false`); users are re-matched by email and get a new identity record on first login via the new provider.

---

### Domain Verification

Admin verifies their organisation's email domain(s) at `/admin/settings/domains`:

- Add a domain (e.g. `acme.com`)
- Verify ownership via DNS TXT record
- Toggle **Discoverable** — if on, users entering an `@acme.com` email on the Trackly login page are automatically routed to this workspace's SSO provider

```sql
CREATE TABLE workspace_domains (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    domain       TEXT NOT NULL UNIQUE,  -- globally unique: only one workspace may claim a domain
    verified     BOOLEAN DEFAULT false,
    discoverable BOOLEAN DEFAULT true,
    dns_txt_token TEXT NOT NULL,         -- token to place in DNS for verification
    verified_at  TIMESTAMPTZ,
    created_at   TIMESTAMPTZ DEFAULT now()
);
```

---

### Login Flow

```
User visits trackly.yourdomain.com/login
    │
    ▼
User enters email address
    │
    ▼
Trackly checks: is this email domain linked to a workspace with active SSO?
    │
   YES ──────────────────────────────────────────────────────────────────────┐
    │                                                                        ▼
    │                                                    SSO flow (OIDC or SAML)
    │                                                    Redirect to IdP → user authenticates
    │                                                    IdP redirects back with code/assertion
    │                                                    Trackly validates, extracts claims
    │                                                    JIT provision or update user record
    │                                                    Apply group→role mapping (if configured)
    │                                                    Issue Trackly session → redirect to app
    │
   NO
    ▼
Show email + password form (native Trackly credentials)
    │
    ▼
Trackly validates password → issues session → redirect to app
```

---

### Just-in-Time (JIT) User Provisioning

When a user authenticates via SSO for the first time, Trackly automatically creates their account:

```
IdP returns: { sub: "okta|uid123", email: "alice@acme.com",
               given_name: "Alice", family_name: "Smith",
               groups: ["support-agents"] }
    │
    ▼
Trackly looks up user_identities WHERE connection_id=X AND provider_sub="okta|uid123"
    │
Not found                              Found
    ▼                                    ▼
Create users record                   Load existing user
{ workspace_id, email, name,          Update name/email if changed
  role from group mapping }
Create user_identities record
    │
    ▼ (both paths merge here)
Apply group → role mapping (if configured, always re-evaluates on login)
Issue Trackly session cookie (HttpOnly, SameSite=Strict)
Redirect to portal (customer) or dashboard (agent/admin)
```

**No admin action required for new SSO users** — they are provisioned automatically with the role their IdP group maps to, or `customer` by default if no mapping matches.

---

### Trackly Session (after SSO)

After SSO completes, Trackly issues its **own** session — completely independent of the IdP:

```
Trackly backend:
  → Creates a session record (sessionId, userId, workspaceId, expiresAt)
  → Sets HttpOnly cookie: trackly.session=<sessionId> (SameSite=Strict, Secure)
  → SPA reads user profile from GET /api/users/me (returns Trackly user record)
```

The SPA never holds an IdP token. All API requests are authenticated via the Trackly session cookie. This means:
- Token format doesn't matter per-provider — Trackly normalises everything into its own session
- Revoking a user in Trackly immediately blocks them regardless of their IdP session state
- No JWKS or issuer config needed on the API — only Trackly's own session store is consulted

---

## User Management (Trackly-Owned)

Trackly owns its own user table. This is the **primary source of truth** for user identity — not a cache of an external system.

| Field | Source |
|-------|-------|
| `id` | Trackly-generated UUID |
| `email` | From IdP JWT/SAML assertion (or entered at signup for email+password) |
| `name` | From IdP JWT/SAML assertion |
| `role` | Set in Trackly's DB (via group mapping or manual assignment by admin) |
| `password_hash` | Only set for email+password users; null for SSO-only users |
| `workspace_id` | Determined at login by domain lookup or workspace slug |

**Users panel in Trackly admin** (`/admin/users`):
- View all workspace members
- Change role (customer / agent / admin)
- Deactivate / reactivate
- See last login, linked SSO identity
- **No bulk role assignment gap** — roles are in Trackly's own DB, admin can update them freely without touching the IdP

---

## Roles & Policies

Roles are managed entirely within Trackly — no dependency on any IdP.

**RBAC — Role-Based Access Control:**

| Trackly Role | What they can do |
|-------------|-----------------|
| `customer` | Submit tickets, view own tickets, reply to agents |
| `agent` | View all tickets, respond, change status, assign, add watchers |
| `admin` | Everything + manage categories, team, SSO config, email settings, widget |

Roles are stored on the `users` table (`role` column) — not as JWT claims from an external system.

**How roles are assigned:**
1. **Auto via group mapping** (recommended for SSO workspaces): Admin configures `IdP group → Trackly role` mapping in the SSO wizard. On every login, Trackly re-evaluates the user's groups and updates their role if the mapping changed.
2. **Manual assignment**: Admin goes to `/admin/users` → selects user → changes role. Works for all auth methods including email+password.

**ABAC — Attribute-Based Access Control** (fine-grained, context-sensitive):

These rules are evaluated within Trackly's own API:

| Rule | Condition |
|------|-----------|
| Agent can only see tickets assigned to them | `ticket.assignee_id == currentUser.id` |
| Senior agents can override ticket priority | `currentUser.metadata.seniority == "senior"` |
| Customers from specific org can view reports | `currentUser.metadata.org_id in ["acme"]` |

---

## How Customers Submit Tickets

### Submission Page UX

When a customer lands on `/submit`, they see two clear paths:

```
┌─────────────────────────────────────────────┐
│           Submit a Support Ticket           │
│                                             │
│  ┌───────────────────────────────────────┐  │
│  │   Sign in with [Workspace SSO]  →     │  │
│  │   (Use your existing account)         │  │
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

The SSO button label reflects the workspace's configured provider (e.g. "Sign in with Google", "Sign in with Okta", "Sign in with Authly").

**Path A — Sign in via SSO:**
1. Customer clicks SSO button → workspace's configured IdP login
2. On return, ticket form is pre-filled with their name and email
3. They fill subject, description, category → submit
4. Ticket is tied to their Trackly user record and visible in their portal

**Path B — Continue as Guest:**
1. Customer enters name + email, fills ticket form → clicks Submit
2. Trackly sends a **6-digit OTP** to their email to verify it's real
3. Customer enters OTP → ticket created
4. Reference number shown on screen + confirmation email with magic link to track ticket

### Linking Anonymous Tickets to an Account

If a guest later signs in (SSO or email+password) with the **same email**, their anonymous tickets are automatically linked to their Trackly user record and appear in the portal.

### What This Requires

| Addition | Purpose |
|----------|---------|
| Public `/submit` route | Anonymous ticket form — no auth required |
| Trackly-side OTP | Verifies guest email independently |
| `guest_email`, `guest_name` columns on tickets | Stores anonymous submitter |
| Magic link | Lets anonymous users view/track ticket without login |
| Email-to-account linking on login | Merges anonymous tickets on first login |

---

## Agent Dashboard & Ticket Assignment

**One app, role-based UI.** The UI adapts based on the Trackly session role:
- `customer` → lands on `/portal`
- `agent` / `admin` → lands on `/dashboard`

### Ticket Assignment

New tickets are **auto-assigned via round-robin** across all active agents, with **manual reassign** available at any time.

```
New ticket arrives
  → Query users WHERE workspace_id=X AND role='agent' AND is_active=true
  → Pick agent with fewest open tickets
  → Assign → send assignment email
```

Manual reassign: any `agent` or `admin` can change `assignee_id` from the ticket detail page.

```sql
CREATE TABLE ticket_assignments (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id   UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    assigned_to UUID NOT NULL REFERENCES users(id),
    assigned_by UUID REFERENCES users(id),  -- null if auto-assigned
    assigned_at TIMESTAMPTZ DEFAULT now()
);
```

### Ticket Watchers

Any agent or admin can be added as a **watcher** — receives all notifications for that ticket without being responsible for resolving it.

```sql
CREATE TABLE ticket_watchers (
    ticket_id  UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    agent_id   UUID NOT NULL REFERENCES users(id),
    added_by   UUID NOT NULL REFERENCES users(id),
    added_at   TIMESTAMPTZ DEFAULT now(),
    PRIMARY KEY (ticket_id, agent_id)
);
```

**Notification flow:**
```
Any ticket update → notify assigned agent + all watchers + customer (each configurable on/off)
```

---

## Private Notes (Internal Comments)

Agents and admins can add **private notes** — visible only to agents, admins, and watchers. Customers never see them.

- Toggle in the reply box: **Reply** (public) vs **Private Note** (internal)
- Visually distinct: amber background + lock icon
- Visibility is locked on creation — cannot be changed after posting
- Private notes do **not** notify the customer but **do** notify assigned agent and watchers

**API enforcement:**
```
GET /api/tickets/{id}/comments
  → customer role or guest magic link: WHERE is_internal = false
  → agent or admin: all comments

POST /api/tickets/{id}/comments { body, is_internal }
  → customer or guest: is_internal forced to false
  → agent or admin: can set is_internal = true
```

---

## Problems — Grouping Related Tickets

When multiple customers report the same underlying issue, agents group tickets under a **Problem**.

```
Problem: "Payment gateway down"          ← root cause
  ├── Ticket #1042 — Alice: "Can't pay"
  ├── Ticket #1043 — Bob: "Checkout failing"
  └── Ticket #1044 — Carol: "Payment error"
```

- Problems have their own status: `investigating → identified → monitoring → resolved`
- Resolving a Problem can bulk-resolve all linked tickets in one action
- Customers are never shown the Problem grouping — they only see their own ticket

```sql
CREATE TABLE problems (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    title       TEXT NOT NULL,
    description TEXT,
    status      TEXT NOT NULL DEFAULT 'investigating',
    assignee_id UUID REFERENCES users(id),
    created_by  UUID NOT NULL REFERENCES users(id),
    created_at  TIMESTAMPTZ DEFAULT now(),
    updated_at  TIMESTAMPTZ DEFAULT now(),
    resolved_at TIMESTAMPTZ
);

ALTER TABLE tickets ADD COLUMN problem_id UUID REFERENCES problems(id) ON DELETE SET NULL;
```

---

## Broadcast Announcements — Outage Emails

Admins can send a mass email to **all customers with a Trackly account** in the workspace.

| Type | Use case |
|------|---------|
| `planned_outage` | Scheduled maintenance communicated in advance |
| `unplanned_outage` | Unexpected downtime |
| `resolved` | Follow-up confirming issue is fixed |
| `general` | Release notes, policy changes, etc. |

- Announcement can be linked to a Problem
- Send now or schedule for a future date/time
- Delivery tracked per recipient (sent / failed / bounced)
- Anonymous/guest users excluded — Trackly has no verified opt-in for them

```sql
CREATE TABLE announcements (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id     UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    type             TEXT NOT NULL,
    subject          TEXT NOT NULL,
    body             TEXT NOT NULL,
    problem_id       UUID REFERENCES problems(id) ON DELETE SET NULL,
    created_by       UUID NOT NULL REFERENCES users(id),
    scheduled_at     TIMESTAMPTZ,
    sent_at          TIMESTAMPTZ,
    recipient_count  INT DEFAULT 0,
    success_count    INT DEFAULT 0,
    failure_count    INT DEFAULT 0,
    created_at       TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE announcement_deliveries (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    announcement_id UUID NOT NULL REFERENCES announcements(id) ON DELETE CASCADE,
    user_id         UUID NOT NULL REFERENCES users(id),
    email           TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',
    sent_at         TIMESTAMPTZ,
    error           TEXT
);
```

---

## Email Interaction Mode

Configurable per workspace in `/admin/settings/email`:

| Mode | What it means |
|------|--------------|
| **Notifications only** | Emails sent, replies go nowhere. Login required to reply. |
| **One-way** | Customer can reply via email; appears in ticket. Agent replies in Trackly. |
| **Full two-way** | Both sides reply via email. Requires inbound email provider setup. |

### Full Two-Way Threading

Every outbound email includes:
```
Reply-To:   reply+<ticket-uuid>@tickets.yourdomain.com
Message-ID: <ticket-uuid>.<comment-uuid>@trackly
```

Inbound emails are parsed by the provider (Mailgun / SendGrid / Postmark / AWS SES), POSTed to `POST /api/email/inbound`, matched to the ticket by the UUID in the `reply+` address, and added as a comment.

Security: HMAC signature verification on inbound webhook + `From:` email must match a known user or guest.

```sql
ALTER TABLE comments ADD COLUMN source TEXT DEFAULT 'web';      -- 'web' or 'email'
ALTER TABLE comments ADD COLUMN email_message_id TEXT;
ALTER TABLE email_configs ADD COLUMN email_mode TEXT NOT NULL DEFAULT 'notifications_only';
```

---

## Email Configuration

Per workspace in `/admin/settings/email`. Admin provides their own SMTP credentials (Gmail, SendGrid, Mailgun, etc.) or uses a shared deployment-level SMTP configured at install time.

```sql
CREATE TABLE email_configs (
    id                     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id           UUID NOT NULL UNIQUE REFERENCES workspaces(id),
    use_shared_smtp        BOOLEAN DEFAULT true,
    smtp_host              TEXT,
    smtp_port              INT,
    smtp_user              TEXT,
    smtp_pass              TEXT,                 -- AES-256-GCM encrypted
    from_name              TEXT,
    from_email             TEXT,
    email_mode             TEXT NOT NULL DEFAULT 'notifications_only',
    inbound_provider       TEXT,
    inbound_reply_domain   TEXT,
    inbound_webhook_secret TEXT,                 -- AES-256-GCM encrypted
    updated_at             TIMESTAMPTZ DEFAULT now()
);
```

### Notification Settings (per workspace)

```sql
CREATE TABLE notification_settings (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id                UUID NOT NULL UNIQUE REFERENCES workspaces(id),
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

## Embeddable Widget & Integration Options

Admin configures at `/admin/widget`. Three embed types:

| Type | How it works |
|------|-------------|
| Floating button | `<script>` tag — renders button + overlay on any page |
| Inline iframe | `<iframe>` snippet — renders form inline |
| Direct link | Standalone URL — no code needed |

Admin configures which fields to show/hide/require/pre-fill. Trackly generates the embed snippet automatically:

```html
<script
  src="https://trackly.yourdomain.com/widget.js"
  data-workspace="acme"
  data-fields="name,email,subject,description"
  data-theme="light"
  data-user-name="Alice Smith"
  data-user-email="alice@acme.com"
></script>
```

Pre-filled fields can be hidden. The SSO button inside the widget initiates the workspace's configured provider. OTP is still triggered for pre-filled email unless the parent app passes a verified Trackly session token.

```sql
CREATE TABLE widget_configs (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL UNIQUE REFERENCES workspaces(id),
    embed_type   TEXT NOT NULL DEFAULT 'floating',
    fields       JSONB NOT NULL,
    theme        TEXT NOT NULL DEFAULT 'light',
    created_at   TIMESTAMPTZ DEFAULT now(),
    updated_at   TIMESTAMPTZ DEFAULT now()
);
```

---

## Website Structure & Wireframes

Trackly has **three surfaces**, each with its own audience:

| Surface | URL | Audience | Branding |
|---------|-----|----------|----------|
| Marketing site | `trackly.com` | Enterprises evaluating Trackly | Trackly's own brand |
| Internal portal | `app.trackly.com` (or `acme.trackly.com`) | Workspace admins + agents | Trackly brand + workspace name |
| Customer-facing support | `acme.trackly.com/support` (+ widget) | The enterprise's end customers | **The enterprise's brand** (logo, colors) |

Layout inspiration: three-pane agent workspace (ticket list left, conversation centre, details right) — styled with Material UI and Trackly's own branding, not a pixel copy of any reference design.

---

### 1. Enterprise Journey — Discover → Sign Up → Live

```
Marketing site                    Onboarding wizard                      Live
──────────────                    ─────────────────                      ────
Landing page                      Step 1  Create admin account
  → Features                      Step 2  Create workspace
  → Pricing                       Step 3  Add your branding
  → [Start free trial] ────────►  Step 4  Invite agents        ────►  /dashboard
                                  Step 5  Set up SSO (optional,       (setup checklist
                                          skippable — do later)        card shown)
```

The signup itself always uses **email+password or Google sign-in** — SSO can't be used yet because the workspace doesn't exist. The first user becomes the workspace `admin`.

---

### 2. Marketing Site — Landing Page

```
┌──────────────────────────────────────────────────────────────────────┐
│ ◆ Trackly      Features   Pricing   Docs          [Sign in] [Start free →] │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│        Customer support that works with YOUR identity                │
│   Ticketing, email threading, and a branded customer portal.         │
│   Bring your own SSO — Okta, Google, Entra ID, Authly — or none.     │
│                                                                      │
│              [ Start free trial ]   [ Book a demo ]                  │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│          ┌──────────────────────────────────────────────┐            │
│          │  (screenshot: agent three-pane workspace)    │            │
│          └──────────────────────────────────────────────┘            │
├────────────────────┬────────────────────┬────────────────────────────┤
│ 🎫 Smart Ticketing │ 🔐 Bring your own  │ ✉️ Two-way email           │
│ Round-robin        │    SSO             │    threading               │
│ assignment,        │ Works with the IdP │ Customers reply from        │
│ watchers, private  │ you already have   │ their inbox — it lands      │
│ notes, problems    │                    │ in the ticket               │
├────────────────────┴────────────────────┴────────────────────────────┤
│  Pricing:   Free (3 agents)  ·  Team  ·  Enterprise (SSO, SLA)       │
├──────────────────────────────────────────────────────────────────────┤
│  Footer: docs · security · status · contact                          │
└──────────────────────────────────────────────────────────────────────┘
```

---

### 3. Onboarding Wizard (after "Start free trial")

```
Step 1 — Create your account            Step 2 — Create your workspace
┌────────────────────────────┐          ┌────────────────────────────┐
│  Create your account       │          │  Name your workspace       │
│                            │          │                            │
│  Work email  ____________  │          │  Company name  __________  │
│  Password    ____________  │          │  Subdomain     [acme   ]   │
│                            │          │                .trackly.com│
│  ───────── or ─────────    │          │                            │
│  [ Continue with Google ]  │          │        [ Continue → ]      │
└────────────────────────────┘          └────────────────────────────┘

Step 3 — Add your branding              Step 4 — Invite your team
┌────────────────────────────┐          ┌────────────────────────────┐
│  Brand your support portal │          │  Invite agents             │
│                            │          │                            │
│  Logo         [ Upload ]   │          │  email@…  [agent ▾]  [+]   │
│  Brand color  [■ #2563EB]  │          │  email@…  [admin ▾]  [+]   │
│  Portal title __________   │          │                            │
│                            │          │  Invitees get an email     │
│  ┌ Live preview ────────┐  │          │  with a join link          │
│  │ [logo] Acme Support  │  │          │                            │
│  │  Submit a ticket …   │  │          │  [ Skip ]  [ Send & → ]    │
│  └──────────────────────┘  │          └────────────────────────────┘
│  [ Skip ]  [ Continue → ]  │
└────────────────────────────┘

Step 5 — Single Sign-On (optional)
┌───────────────────────────────────────┐
│  Connect your identity provider       │
│                                       │
│  ○ Okta   ○ Google   ○ Entra ID       │
│  ○ Authly ○ Custom SAML ○ Custom OIDC │
│                                       │
│  You can set this up any time in      │
│  Settings → SSO.                      │
│                                       │
│  [ Skip for now ]  [ Configure → ]    │
└───────────────────────────────────────┘
        │
        ▼
Lands on /dashboard with a "Getting started" checklist card:
  ☐ Verify your domain   ☐ Configure SSO   ☐ Embed the widget
  ☑ Invite agents        ☑ Add branding
```

---

### 4. Internal Portal — Agent Workspace (three-pane)

The layout the design review converged on — open tickets on the left, conversation in the middle, details on the right:

```
┌────┬───────────────────┬─────────────────────────────────┬──────────────────┐
│ ◆  │ Open Tickets   ⚙  │ #1126 · Cannot verify my code   │ Ticket details ✕ │
│    │ [search…    ] [▾] │           status: [ Open ▾ ]    │                  │
│ 🏠 │───────────────────│─────────────────────────────────│ Assignee         │
│ 🎫 │ ▸ Javier O.   45s │ ┌─────────────────────────────┐ │  Viola D         │
│ 👥 │   Verifying code… │ │ Javier: Email came through  │ │ Watchers         │
│ 📊 │───────────────────│ │ but there is no code in it. │ │  Taylor B        │
│ ⚙  │   S. Walker    2m │ └─────────────────────────────┘ │  Gavin B   [+Add]│
│    │   Where is my…    │ ┌─────────────────────────────┐ │──────────────────│
│    │───────────────────│ │ Viola (agent): Thanks — our │ │ ID       #1126   │
│    │   Carmen S.    5m │ │ team is looking into it.    │ │ Priority High    │
│    │   Overseas ship…  │ └─────────────────────────────┘ │ Category Technical│
│    │───────────────────│ ┌─ 🔒 internal ──────────────┐ │ Problem  PG down │
│    │   Brian H.    11m │ │ Viola: @Gavin can you check │ │──────────────────│
│    │   Wholesale ord…  │ │ the OTP service logs?       │ │ Requester        │
│    │                   │ └─────────────────────────────┘ │  Javier Ortiz    │
│    │                   │─────────────────────────────────│  javier@ortiz.com│
│    │                   │ [ Public reply | Private note ] │                  │
│    │                   │ ┌─────────────────────────────┐ │                  │
│    │                   │ │ Type your reply…            │ │                  │
│    │                   │ └────────────────── 📎  [Send]│ │                  │
└────┴───────────────────┴─────────────────────────────────┴──────────────────┘
```

Key behaviours:
- Left list: searchable, filterable (status/priority/assignee), unread indicators
- Centre: public replies and 🔒 private notes visually distinct; status dropdown at top
- Right panel: assignee, watchers, priority, category, linked problem, requester info

**Admin view** = same shell + extra nav items (Users, Settings, Announcements, Widget).

---

### 5. Customer-Facing Support Form (workspace-branded)

Rendered entirely with the **enterprise's branding** — logo, brand colour, portal title. Trackly's brand appears only as a small "Powered by Trackly" footer (removable on paid tiers).

```
┌──────────────────────────────────────────────┐
│  [ACME LOGO]   Acme Support        ← brand   │  ← header uses workspace
│  ────────────────────────────────   colour   │    logo + primary_color
│                                              │
│        How can we help you?                  │  ← welcome_text
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │  Sign in with Okta →                   │  │  ← label reflects the
│  └────────────────────────────────────────┘  │    workspace's SSO provider
│                 ── or ──                     │
│  Continue as guest                           │
│  Name    ______________________________     │
│  Email   ______________________________     │
│  Subject ______________________________     │
│  Category [ Billing ▾ ]                      │
│  Message ______________________________     │
│          ______________________________     │
│  📎 Attach files                             │
│                                              │
│              [  Submit ticket  ]             │  ← button in brand colour
│                                              │
│  ──────────────────────────────────────────  │
│           Powered by Trackly                 │
└──────────────────────────────────────────────┘
```

The same branding is applied to: the customer portal (`/portal`), the embeddable widget, outbound notification emails (logo in header), and the guest magic-link ticket view.

---

### 6. Workspace Branding

Configured at `/admin/settings/branding` (and during onboarding Step 3):

```sql
CREATE TABLE workspace_branding (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL UNIQUE REFERENCES workspaces(id) ON DELETE CASCADE,
    logo_url      TEXT,                     -- stored via IFileStorage, same as attachments
    primary_color TEXT DEFAULT '#2563EB',   -- hex; drives header, buttons, links
    page_title    TEXT,                     -- e.g. "Acme Support"
    welcome_text  TEXT,                     -- shown on the submit form
    footer_text   TEXT,                     -- optional custom footer line
    hide_powered_by BOOLEAN DEFAULT false,  -- paid-tier flag
    updated_at    TIMESTAMPTZ DEFAULT now()
);
```

Served to the public form/widget via an unauthenticated, cacheable endpoint:
`GET /api/public/workspaces/{slug}/branding` → `{ logoUrl, primaryColor, pageTitle, welcomeText, ssoProviderName }`

---

## Components to Build

### 1. React Frontend (Vite + TypeScript)

**Stack:** React 18, TypeScript, Vite, Material UI, TanStack Query, React Router v6, React Hook Form + Zod, Zustand

| Area | Routes | Auth required | Who sees it |
|------|--------|--------------|-------------|
| Marketing site | `/`, `/features`, `/pricing` | No | Prospective enterprises |
| Signup + onboarding | `/signup`, `/onboarding/*` (5-step wizard) | Signup only | New workspace admins |
| Accept invite | `/invite/:token` | No | Invited agents/admins |
| Public ticket form | `/submit` (workspace-branded) | No | Anyone |
| Anonymous ticket view | `/tickets/:id?token=` | No | Guest (magic link) |
| Login | `/login` | No | All |
| SSO callback | `/auth/callback` | No | All |
| Customer portal | `/portal/tickets`, `/portal/tickets/new`, `/portal/tickets/:id` | Yes | `customer` |
| Agent dashboard | `/dashboard/tickets`, `/dashboard/tickets/:id`, `/dashboard/problems` | Yes | `agent`, `admin` |
| Admin settings | `/admin/users`, `/admin/settings/sso`, `/admin/settings/email`, `/admin/settings/domains`, `/admin/settings/branding`, `/admin/widget`, `/admin/announcements` | Yes | `admin` |

---

### 2. ASP.NET Core Web API

**Solution structure:**
```
src/
  Trackly.Core/           # Entities, interfaces, enums
  Trackly.Modules/        # Tickets, Comments, Auth, Users, Notifications, Announcements
  Trackly.Infrastructure/ # EF Core, OIDC/SAML handlers, email adapter, session store
  Trackly.Api/            # Controllers, middleware, session auth
```

**Authentication middleware:**
```csharp
// Session-based — no external JWKS needed
// Trackly issues its own session after SSO completes
builder.Services.AddAuthentication("TracklySession")
    .AddScheme<TracklySessionOptions, TracklySessionHandler>("TracklySession", _ => { });
```

**OIDC handling (generic, per-workspace config):**

> **Implementation caveat:** ASP.NET Core registers authentication schemes at
> startup — you cannot call `AddOpenIdConnect` per workspace at runtime.
> Instead, register **one** generic OIDC scheme and resolve the workspace's
> `sso_connections` record inside the handler events (or via a custom
> `IOptionsMonitor<OpenIdConnectOptions>` keyed by workspace). The workspace
> is carried through the flow in the OIDC `state` parameter.

```csharp
// ONE generic scheme; per-workspace config resolved at request time
services.AddOpenIdConnect("WorkspaceOidc", options => {
    options.CallbackPath = "/auth/callback";
    options.Events.OnRedirectToIdentityProvider = ctx => {
        var conn = ctx.HttpContext.ResolveSsoConnection(); // by workspace slug
        ctx.ProtocolMessage.IssuerAddress = conn.AuthorizeEndpoint;
        ctx.ProtocolMessage.ClientId      = conn.ClientId;
        // secret (if any) decrypted at token exchange, same pattern
        return Task.CompletedTask;
    };
});
```

**SAML handling:** `ITfoxtec.Identity.Saml2` or `Sustainsys.Saml2` NuGet package.

**Key API endpoints:**

| Method | Path | Auth | Role |
|--------|------|------|------|
| POST   | `/api/signup` | None | Create admin account + workspace (onboarding steps 1–2) |
| POST   | `/api/invitations` | Session | admin — invite agents by email |
| POST   | `/api/invitations/accept` | None | Accept invite via token, create account |
| GET    | `/api/public/workspaces/{slug}/branding` | None | Public, cacheable — branding for form/widget |
| PUT    | `/api/admin/branding` | Session | admin — update logo, colour, portal title |
| GET    | `/api/auth/sso?workspace=` | None | Initiate SSO for workspace |
| GET    | `/auth/callback` | None | OIDC/SAML callback |
| POST   | `/api/auth/login` | None | Email+password login |
| POST   | `/api/auth/logout` | Session | Clear session |
| GET    | `/api/users/me` | Session | Get current user profile |
| GET    | `/api/tickets` | Session | agent/admin: all; customer: own |
| POST   | `/api/tickets` | Session | customer, agent, admin |
| GET    | `/api/tickets/{id}` | Session | owner or agent/admin |
| PATCH  | `/api/tickets/{id}` | Session | agent/admin |
| POST   | `/api/tickets/{id}/comments` | Session | owner or agent/admin |
| POST   | `/api/guest/otp/send` | None | Public — send 6-digit OTP to guest email (rate-limited) |
| POST   | `/api/guest/otp/verify` | None | Public — verify OTP, returns short-lived submission token |
| POST   | `/api/tickets/guest` | None | Public — anonymous submission (requires verified submission token) |
| GET    | `/api/tickets/guest/{id}?token=` | None | Guest magic link |
| POST   | `/api/tickets/{id}/attachments` | Session or guest token | Upload attachment |
| GET    | `/api/attachments/{id}` | Session or guest token | Download via signed URL (visibility-checked) |
| POST   | `/api/admin/sso` | Session | admin — save SSO connection |
| POST   | `/api/admin/sso/test` | Session | admin — test SSO connection |
| GET    | `/api/problems` | Session | agent, admin |
| POST   | `/api/announcements` | Session | admin |

---

### 3. Database Schema (PostgreSQL — `trackly` database)

```sql
-- Workspaces (Trackly's own multi-tenancy — no dependency on external IdP)
CREATE TABLE workspaces (
    id                     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                   TEXT NOT NULL,
    slug                   TEXT NOT NULL UNIQUE,   -- e.g. "acme" → acme.trackly.com
    password_login_enabled BOOLEAN DEFAULT true,   -- admin can force SSO-only login
    created_at             TIMESTAMPTZ DEFAULT now(),
    updated_at             TIMESTAMPTZ DEFAULT now()
);

-- Verified email domains
CREATE TABLE workspace_domains (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    domain        TEXT NOT NULL UNIQUE,  -- globally unique: only one workspace may claim a domain
    verified      BOOLEAN DEFAULT false,
    discoverable  BOOLEAN DEFAULT true,
    dns_txt_token TEXT NOT NULL,
    verified_at   TIMESTAMPTZ,
    created_at    TIMESTAMPTZ DEFAULT now()
);

-- SSO connections (one active per workspace)
CREATE TABLE sso_connections (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id       UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    provider_name      TEXT NOT NULL,       -- "Authly", "Okta", "Google", "Entra ID", "Custom OIDC", etc.
    protocol           TEXT NOT NULL,       -- 'oidc' or 'saml'
    -- OIDC fields
    discovery_endpoint TEXT,
    client_id          TEXT,
    client_secret      TEXT,               -- AES-256-GCM encrypted
    -- SAML fields
    idp_metadata_url   TEXT,
    idp_metadata_xml   TEXT,
    sp_entity_id       TEXT,
    -- Status
    status             TEXT DEFAULT 'pending',  -- pending, active, error
    tested_at          TIMESTAMPTZ,
    created_at         TIMESTAMPTZ DEFAULT now(),
    updated_at         TIMESTAMPTZ DEFAULT now()
);

-- IdP group → Trackly role mappings
CREATE TABLE sso_group_role_mappings (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id UUID NOT NULL REFERENCES sso_connections(id) ON DELETE CASCADE,
    group_name    TEXT NOT NULL,    -- IdP group name e.g. "support-agents"
    trackly_role  TEXT NOT NULL     -- 'customer', 'agent', 'admin'
);

-- Trackly users (primary source of truth — not a cache)
CREATE TABLE users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    email         TEXT,
    phone         TEXT,
    name          TEXT,
    avatar_url    TEXT,
    role          TEXT NOT NULL DEFAULT 'customer',  -- customer, agent, admin
    password_hash TEXT,            -- Argon2id; null for SSO-only users
    is_active     BOOLEAN DEFAULT true,
    created_at    TIMESTAMPTZ DEFAULT now(),
    updated_at    TIMESTAMPTZ DEFAULT now(),
    last_login_at TIMESTAMPTZ,
    CONSTRAINT email_or_phone CHECK (email IS NOT NULL OR phone IS NOT NULL),
    UNIQUE (workspace_id, email)
);

-- Agent/admin invitations (onboarding Step 4 and /admin/users)
CREATE TABLE workspace_invitations (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    email        TEXT NOT NULL,
    role         TEXT NOT NULL DEFAULT 'agent',   -- agent or admin
    token_hash   TEXT NOT NULL UNIQUE,            -- SHA-256 of the invite link token
    invited_by   UUID NOT NULL REFERENCES users(id),
    expires_at   TIMESTAMPTZ NOT NULL,            -- 7 days
    accepted_at  TIMESTAMPTZ,
    created_at   TIMESTAMPTZ DEFAULT now()
);

-- Links Trackly users to external IdP identities (for JIT provisioning)
CREATE TABLE user_identities (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id       UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    connection_id UUID NOT NULL REFERENCES sso_connections(id) ON DELETE CASCADE,
    provider_sub  TEXT NOT NULL,   -- 'sub' claim from IdP
    is_active     BOOLEAN DEFAULT true,  -- false when the workspace switches providers
    created_at    TIMESTAMPTZ DEFAULT now(),
    UNIQUE (connection_id, provider_sub)
);

-- Trackly sessions (issued after SSO or password login)
-- The cookie holds a random 256-bit token; only its SHA-256 hash is stored,
-- so a DB leak does not yield usable sessions.
CREATE TABLE sessions (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token_hash   TEXT NOT NULL UNIQUE,   -- SHA-256 of the session token in the cookie
    user_id      UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    ip_address   TEXT,
    user_agent   TEXT,
    expires_at   TIMESTAMPTZ NOT NULL,
    created_at   TIMESTAMPTZ DEFAULT now()
);

-- Guest email verification OTPs (for anonymous ticket submission)
CREATE TABLE otp_codes (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id  UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    email         TEXT NOT NULL,
    code_hash     TEXT NOT NULL,          -- SHA-256 of the 6-digit code
    attempts      INT DEFAULT 0,          -- verification locked after 5 failed attempts
    expires_at    TIMESTAMPTZ NOT NULL,   -- 10 minutes
    consumed_at   TIMESTAMPTZ,
    created_at    TIMESTAMPTZ DEFAULT now()
);
-- Rate limiting: max 3 OTP sends per email per 15 minutes,
-- plus per-IP limits on the public endpoints (email-spam protection).

-- Tickets
CREATE TABLE tickets (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id     UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    subject          TEXT NOT NULL,
    description      TEXT NOT NULL,
    status           TEXT NOT NULL DEFAULT 'open',    -- open, pending, resolved, closed
    priority         TEXT NOT NULL DEFAULT 'medium',  -- low, medium, high, urgent
    category_id      UUID REFERENCES categories(id),
    requester_id     UUID REFERENCES users(id),       -- null if anonymous
    guest_email      TEXT,
    guest_name       TEXT,
    guest_token_hash TEXT,                            -- SHA-256 of magic link token
    assignee_id      UUID REFERENCES users(id),
    problem_id       UUID REFERENCES problems(id) ON DELETE SET NULL,
    created_at       TIMESTAMPTZ DEFAULT now(),
    updated_at       TIMESTAMPTZ DEFAULT now(),
    CONSTRAINT requester_or_guest CHECK (requester_id IS NOT NULL OR guest_email IS NOT NULL)
);

-- Comments / replies
CREATE TABLE comments (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id        UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    author_id        UUID REFERENCES users(id),   -- null for guest comments
    guest_email      TEXT,                        -- set for guest replies
    body             TEXT NOT NULL,
    is_internal      BOOLEAN DEFAULT false,
    source           TEXT DEFAULT 'web',          -- 'web' or 'email'
    email_message_id TEXT,
    created_at       TIMESTAMPTZ DEFAULT now()
);

-- Attachments (on tickets and comments)
CREATE TABLE attachments (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    ticket_id    UUID NOT NULL REFERENCES tickets(id) ON DELETE CASCADE,
    comment_id   UUID REFERENCES comments(id) ON DELETE CASCADE,  -- null if attached to the ticket itself
    uploaded_by  UUID REFERENCES users(id),   -- null for guest uploads
    file_name    TEXT NOT NULL,
    content_type TEXT NOT NULL,
    size_bytes   BIGINT NOT NULL,             -- enforce max size (e.g. 10 MB) at API level
    storage_key  TEXT NOT NULL,               -- path/key in blob storage
    created_at   TIMESTAMPTZ DEFAULT now()
);
-- Storage: local disk volume for self-hosted deployments, S3-compatible
-- object storage (S3 / MinIO / Azure Blob) for cloud — behind an
-- IFileStorage abstraction. Downloads served via short-lived signed URLs;
-- access checked against ticket visibility rules first.

-- Categories
CREATE TABLE categories (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
    name         TEXT NOT NULL,
    color        TEXT
);
```

---

### 4. SSO Setup — Authly as a Provider (Optional)

If a customer uses Authly, they configure it as a **Custom OIDC** connection in Trackly:

```
Step 1 — Select provider: "Authly" (or "Custom OIDC")
Step 2 — In Authly TenantAdmin → Applications → New SPA client
          Redirect URI: https://trackly.yourdomain.com/auth/callback
          Copy client_id
Step 3 — Claims: sub, email, given_name, family_name already in Authly JWT.
          For group mapping: configure a custom claim "groups" in Authly
          using the ClaimConfig feature (webhook-sourced or metadata-mapped)
Step 4 — Trackly: Discovery endpoint = https://auth.yourdomain.com
                   Client ID = client_abc123
                   Client Secret = optional. Since Trackly's backend performs
                   the code exchange server-side, registering a confidential
                   Web client with a secret is the standard setup; an Authly
                   SPA client with PKCE (no secret) also works.
Step 5 — Group → role mapping in Trackly:
          Authly role "support_agent" → trackly role "agent"
Step 6 — Test
```

Authly is treated exactly the same as any other OIDC provider. No special code path for Authly in Trackly.

---

### 5. User Sync

**JIT provisioning on login** (primary): user record created/updated automatically from IdP claims on every SSO login. No separate sync job needed.

**Workspace webhook** (optional): If a customer uses Authly, they can configure Authly webhooks (`user.suspended`, `user.deleted`) to call a Trackly endpoint that deactivates the matching user by email in Trackly's DB.

---

## Tech Stack Summary

| Layer | Choice | Reason |
|-------|--------|--------|
| Frontend | React 18 + Vite + MUI + TanStack Query + Zustand + RHF + Zod | Type-safe, fast SPA |
| State | Zustand | Auth state is minimal |
| Backend | ASP.NET Core Web API (.NET 9+) | Strong auth middleware ecosystem |
| OIDC | Built-in `Microsoft.AspNetCore.Authentication.OpenIdConnect` | Generic OIDC support |
| SAML | `ITfoxtec.Identity.Saml2` NuGet | SAML 2.0 for enterprise providers |
| ORM | Entity Framework Core | Consistent, well-supported |
| Database | PostgreSQL (`trackly` DB) | No external infra dependency |
| Session | HttpOnly cookie → Trackly `sessions` table | Provider-agnostic, fully controlled |
| Secrets encryption | AES-256-GCM | For client secrets and SMTP passwords at rest |

---

## Verification Checklist

- [ ] Configure Authly as Custom OIDC in a local Trackly workspace — confirm SSO login works
- [ ] Configure Google as OIDC in a second workspace — confirm SSO login works
- [ ] JIT provisioning: new SSO user auto-created in Trackly `users` table on first login
- [ ] Group → role mapping: agent group maps to `agent` role, customer group maps to `customer`
- [ ] Manual role change in `/admin/users` takes effect on next request without re-login
- [ ] `customer` cannot access `/dashboard`; `agent` can
- [ ] Email+password login works independently of any SSO connection
- [ ] Anonymous guest submits ticket via OTP → magic link tracks ticket without login
- [ ] OTP rate limiting: 4th send within 15 minutes for the same email is rejected
- [ ] Attachment upload on ticket + comment; customer cannot download attachments on private notes
- [ ] Disable password login for a workspace → email+password form rejected, SSO still works
- [ ] Guest ticket linked to user on first SSO login with matching email
- [ ] Agent responds → customer notified; customer replies via email → appears in thread
- [ ] Suspend user in Trackly → session invalidated, access denied immediately
- [ ] Workspace B cannot see Workspace A's tickets (workspace isolation)
