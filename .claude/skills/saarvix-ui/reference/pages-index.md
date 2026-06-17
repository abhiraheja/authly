# Example pages (learn by mirroring)

The CaludeUI design project contains ~100 built pages on this system. When unsure how to build
something, open the closest example and mirror its structure. Key references:

## App screens
- `index.html` — Dashboard (KPIs, Chart.js, funnel, activity timeline).
- `leads.html` — List with Table/Kanban/List views, bulk actions, detail drawer.
- `calling.html` — Softphone (active call, dialer, transcript, queue, agents).
- `whatsapp.html` — 3-pane inbox. `email.html` — Gmail-style mail. `sms.html` — SMS threads.
- `pipeline.html` — Kanban deal board. `reports.html` — analytics dashboard.
- `team.html` — members/roles/teams + invite modal. `settings.html` — sub-nav settings.
- `social-linkedin|instagram|facebook|x.html` — per-channel social.

## Design system / UI kit
- `components.html` — the full design-system showcase (foundations + every component, all interactive).
- `ui-*.html` — one page per element (buttons, cards, modals, tabs, toasts, carousels, …).
- `adv-*.html` — advanced (kanban drag, lightbox, ratings, range, ribbons, idle/session timers, sweet alerts).
- `form-*.html` — forms (basic, advance, validation, wizard, file upload, repeater, x-editable, editors).
- `table-*.html` — basic / datatable (sort+search) / editable / responsive.
- `chart-*.html` — apex/chartjs/flot/morris (all Chart.js).
- `icon-*.html` — searchable Lucide galleries. `map-*.html` — styled map UIs.
- `email-*.html` — responsive email templates.

## Pages
- `pricing.html`, `profile.html`, `faq.html`, `timeline.html`, `blogs.html`, `starter.html`, `treeview.html`.
- `login.html`, `register.html`, `forgot-password.html`, `404.html` — full-screen, no shell.

Note: these examples live in the project root, not in this skill. The skill ships the reusable
engine (`assets/`) + `templates/` so you can scaffold the same way in any project.
