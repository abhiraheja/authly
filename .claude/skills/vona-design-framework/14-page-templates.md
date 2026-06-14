# 14 — Page Templates

Ready-made full-page layouts that ship with Vona. Reuse their structure rather than rebuilding from scratch.

## Authentication pages (no app shell)
Auth pages **do not** use the sidebar/topbar wrapper — they're a centered card / split layout on a plain body. Pages: `auth-sign-in`, `auth-sign-up`, `auth-reset-pass`, `auth-new-pass`, `auth-two-factor`, `auth-lock-screen`.

Common structure:
```html
<div class="d-flex align-items-center justify-content-center min-vh-100">
  <div class="card" style="max-width:420px;width:100%">
    <div class="card-body p-4">
      <a class="logo">…</a>
      <h4 class="…">Sign In</h4>
      <form><!-- form-floating / form-control + validation (file 07) --></form>
      <!-- social buttons, links to other auth pages -->
    </div>
  </div>
</div>
```
- **Two-factor:** segmented OTP inputs (one box per digit) + verify button.
- **Lock screen:** avatar + password only.
- **Reset / new password:** email field / new+confirm password with validation.
- Build these as standalone layouts (separate from the main shell layout in file 02).

## Utility / content pages (inside app shell)
These use the normal shell + page-title row:
| Page | What it is | Key building blocks |
|---|---|---|
| `pages-pricing` | pricing tier cards | `row` of `.card`, featured card highlighted, list of features, CTA button |
| `pages-invoice` | printable invoice | header (logo + addresses), line-item `table`, totals, print button (`window.print()`) |
| `pages-timeline` | activity timeline | vertical timeline list (theme `timeline` classes — confirm) |
| `pages-terms-conditions` | long-form legal text | typography utilities, table of contents |
| `pages-empty` | blank starter | shell + empty `.container-fluid` to build on |
| `404` | error page | centered illustration + message + "back home" button |

Start any new page from `pages-empty` (shell + page-title) and drop in cards.

## Prebuilt apps
Richer, interactive layouts:
| App | Page | Layout notes |
|---|---|---|
| **AI Chat ("Ton AI")** | `ton-ai` | chat shell: conversation list / sidebar + message thread + composer input; message bubbles, streaming-style UI |
| **Calendar** | `calendar` | full-calendar style month/week grid + event create modal (uses a calendar JS lib — confirm, likely FullCalendar) + draggable events |
| **Directory** | `directory` | filterable grid/list of people/cards with search + filters |

These combine many components from earlier files (cards, modals, forms, list groups, avatars, dropdowns). When recreating one, identify the JS lib it relies on (calendar) and load it per-page.

## Authoring rules
- Auth pages = standalone centered layout; everything else = the shared shell (file 02) + page-title (`page-title-head`).
- Reuse the pricing/invoice/timeline structures verbatim from your licensed copy; this file maps what each contains so you know which components to assemble.
- For the apps, treat them as compositions — pull each piece from files 03–10 and add the app-specific JS lib.

— end of component library. Back to [SKILL.md](SKILL.md).
