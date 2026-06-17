/* =============================================================
   SAARVIX Admin — Application Shell
   Renders the sidebar + top header, mounts page content from a
   <template id="page-content">, and wires interactions.
   Usage in each page:
     <template id="page-content"> ...main content... </template>
     <script>mountShell({ active: "leads", title: "Leads" })</script>
   ============================================================= */

const NAV = [
  { group: "Overview", items: [
    { key: "dashboard", label: "Dashboard", icon: "layout-dashboard", href: "index.html" },
  ]},
  { group: "CRM", items: [
    { key: "leads",     label: "Leads",     icon: "user-plus",  href: "leads.html", badge: "128" },
    { key: "contacts",  label: "Contacts",  icon: "users",      href: "contacts.html" },
    { key: "companies", label: "Companies", icon: "building-2", href: "companies.html" },
    { key: "pipeline",  label: "Pipeline",  icon: "kanban",     href: "pipeline.html" },
  ]},
  { group: "Channels", items: [
    { key: "calling",  label: "Calling",      icon: "phone-call",     href: "calling.html" },
    { key: "whatsapp", label: "WhatsApp",     icon: "message-circle", href: "whatsapp.html", badge: "9" },
    { key: "email",    label: "Email",        icon: "mail",           href: "email.html" },
    { key: "sms",      label: "SMS",          icon: "message-square", href: "sms.html" },
    { key: "social",   label: "Social Media", icon: "share-2",        href: "social.html",
      children: [
        { key: "social",           label: "All Channels", href: "social.html" },
        { key: "social-linkedin",  label: "LinkedIn",     href: "social-linkedin.html" },
        { key: "social-instagram", label: "Instagram",    href: "social-instagram.html" },
        { key: "social-facebook",  label: "Facebook",     href: "social-facebook.html" },
        { key: "social-x",         label: "X (Twitter)",  href: "social-x.html" },
      ]},
  ]},
  { group: "Growth", items: [
    { key: "campaigns",  label: "Campaigns",  icon: "megaphone", href: "campaigns.html" },
    { key: "automation", label: "Automation", icon: "workflow",  href: "automation.html" },
    { key: "ai",         label: "AI Agents",  icon: "bot",       href: "ai.html" },
  ]},
  { group: "Productivity", items: [
    { key: "tasks",    label: "Tasks",    icon: "check-square", href: "tasks.html" },
    { key: "calendar", label: "Calendar", icon: "calendar",     href: "calendar.html" },
  ]},
  { group: "Insights", items: [
    { key: "reports", label: "Reports & Analytics", icon: "bar-chart-3", href: "reports.html" },
  ]},
  { group: "UI Kit", items: [
    { key: "components", label: "Design System", icon: "component", href: "components.html" },
    { key: "ui-elements", label: "UI Elements", icon: "box", children: [
      { key: "ui-alerts",      label: "Alerts",            href: "ui-alerts.html" },
      { key: "ui-avatar",      label: "Avatar",            href: "ui-avatar.html" },
      { key: "ui-buttons",     label: "Buttons",           href: "ui-buttons.html" },
      { key: "ui-badges",      label: "Badges",            href: "ui-badges.html" },
      { key: "ui-cards",       label: "Cards",             href: "ui-cards.html" },
      { key: "ui-carousels",   label: "Carousels",         href: "ui-carousels.html" },
      { key: "ui-checkradio",  label: "Check & Radio",     href: "ui-checkradio.html" },
      { key: "ui-dropdowns",   label: "Dropdowns",         href: "ui-dropdowns.html" },
      { key: "ui-grids",       label: "Grids",             href: "ui-grids.html" },
      { key: "ui-images",      label: "Images",            href: "ui-images.html" },
      { key: "ui-list",        label: "List",              href: "ui-list.html" },
      { key: "ui-modals",      label: "Modals",            href: "ui-modals.html" },
      { key: "ui-navs",        label: "Navs",              href: "ui-navs.html" },
      { key: "ui-navbar",      label: "Navbar",            href: "ui-navbar.html" },
      { key: "ui-offcanvas",   label: "Offcanvas",         href: "ui-offcanvas.html" },
      { key: "ui-pagination",  label: "Paginations",       href: "ui-pagination.html" },
      { key: "ui-popover",     label: "Popover & Tooltips",href: "ui-popover.html" },
      { key: "ui-progress",    label: "Progress",          href: "ui-progress.html" },
      { key: "ui-spinners",    label: "Spinners",          href: "ui-spinners.html" },
      { key: "ui-tabs",        label: "Tabs & Accordions", href: "ui-tabs.html" },
      { key: "ui-toasts",      label: "Toasts",            href: "ui-toasts.html" },
      { key: "ui-typography",  label: "Typography",        href: "ui-typography.html" },
      { key: "ui-videos",      label: "Videos",            href: "ui-videos.html" },
    ]},
    { key: "adv-ui", label: "Advanced UI", icon: "layers", children: [
      { key: "adv-animation", label: "Animation",       href: "adv-animation.html" },
      { key: "adv-clipboard", label: "Clip Board",      href: "adv-clipboard.html" },
      { key: "adv-highlight", label: "Highlight",       href: "adv-highlight.html" },
      { key: "adv-idle",      label: "Idle Timer",      href: "adv-idle.html" },
      { key: "adv-kanban",    label: "Kanban",          href: "adv-kanban.html" },
      { key: "adv-lightbox",  label: "Lightbox",        href: "adv-lightbox.html" },
      { key: "adv-nestable",  label: "Nestable List",   href: "adv-nestable.html" },
      { key: "adv-range",     label: "Range Slider",    href: "adv-range.html" },
      { key: "adv-ratings",   label: "Ratings",         href: "adv-ratings.html" },
      { key: "adv-ribbons",   label: "Ribbons",         href: "adv-ribbons.html" },
      { key: "adv-session",   label: "Session Timeout", href: "adv-session.html" },
      { key: "adv-sweet",     label: "Sweet Alerts",    href: "adv-sweet.html" },
    ]},
    { key: "kit-forms", label: "Forms", icon: "list-checks", children: [
      { key: "form-advance",    label: "Advance Elements", href: "form-advance.html" },
      { key: "form-basic",      label: "Basic Elements",   href: "form-basic.html" },
      { key: "form-editors",    label: "Editors",          href: "form-editors.html" },
      { key: "form-upload",     label: "File Upload",      href: "form-upload.html" },
      { key: "form-repeater",   label: "Repeater",         href: "form-repeater.html" },
      { key: "form-validation", label: "Validation",       href: "form-validation.html" },
      { key: "form-wizard",     label: "Wizard",           href: "form-wizard.html" },
      { key: "form-xeditable",  label: "X Editable",       href: "form-xeditable.html" },
    ]},
    { key: "kit-charts", label: "Charts", icon: "bar-chart-3", children: [
      { key: "chart-apex",    label: "Apex",    href: "chart-apex.html" },
      { key: "chart-chartjs", label: "Chartjs", href: "chart-chartjs.html" },
      { key: "chart-flot",    label: "Flot",    href: "chart-flot.html" },
      { key: "chart-morris",  label: "Morris",  href: "chart-morris.html" },
    ]},
    { key: "kit-tables", label: "Tables", icon: "table-2", children: [
      { key: "table-basic",      label: "Basic",      href: "table-basic.html" },
      { key: "table-datatables", label: "Datatables", href: "table-datatables.html" },
      { key: "table-editable",   label: "Editable",   href: "table-editable.html" },
      { key: "table-responsive", label: "Responsive", href: "table-responsive.html" },
    ]},
    { key: "kit-icons", label: "Icons", icon: "smile", children: [
      { key: "icon-dripicons", label: "Dripicons",       href: "icon-dripicons.html" },
      { key: "icon-feather",   label: "Feather",         href: "icon-feather.html" },
      { key: "icon-fa",        label: "Font awesome",    href: "icon-fa.html" },
      { key: "icon-material",  label: "Material Design", href: "icon-material.html" },
      { key: "icon-themify",   label: "Themify",         href: "icon-themify.html" },
      { key: "icon-typicons",  label: "Typicons",        href: "icon-typicons.html" },
    ]},
    { key: "kit-maps", label: "Maps", icon: "map", children: [
      { key: "map-google",  label: "Google Maps",  href: "map-google.html" },
      { key: "map-leaflet", label: "Leaflet Maps", href: "map-leaflet.html" },
      { key: "map-vector",  label: "Vector Maps",  href: "map-vector.html" },
    ]},
    { key: "kit-email", label: "Email Template", icon: "mail", children: [
      { key: "email-alert",   label: "Alert Email",        href: "email-alert.html" },
      { key: "email-action",  label: "Basic Action Email", href: "email-action.html" },
      { key: "email-billing", label: "Billing Email",      href: "email-billing.html" },
    ]},
  ]},
  { group: "Widgets", items: [
    { key: "widgets", label: "Widgets", icon: "layout-grid", href: "index.html" },
  ]},
  { group: "Pages", items: [
    { key: "page-blogs",   label: "Blogs",          icon: "newspaper",   href: "blogs.html" },
    { key: "faq",          label: "FAQs",           icon: "help-circle", href: "faq.html" },
    { key: "pricing",      label: "Pricing",        icon: "tag",         href: "pricing.html" },
    { key: "profile",      label: "Profile",        icon: "user-round",  href: "profile.html" },
    { key: "page-starter", label: "Starter Page",   icon: "file",        href: "starter.html" },
    { key: "timeline",     label: "Timeline",       icon: "history",     href: "timeline.html" },
    { key: "page-tree",    label: "Treeview",       icon: "list-tree",   href: "treeview.html" },
    { key: "login",        label: "Authentication", icon: "log-in",      href: "login.html" },
  ]},
  { group: "Administration", items: [
    { key: "integrations", label: "Integrations",    icon: "plug",     href: "integrations.html" },
    { key: "team",         label: "Team Management", icon: "shield-check", href: "team.html" },
    { key: "settings",     label: "Settings",        icon: "settings", href: "settings.html" },
  ]},
];

function icon(name, cls = "size-[18px]") {
  return `<i data-lucide="${name}" class="${cls}" stroke-width="1.75"></i>`;
}

function navMarkup(active) {
  return NAV.map((g) => {
    const items = g.items.map((it) => {
      const isActive = it.key === active || (it.children || []).some((c) => c.key === active);
      const base = "group flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition-colors";
      const state = isActive
        ? "bg-primary/10 text-primary font-medium"
        : "text-sidebar-foreground/85 hover:bg-sidebar-accent hover:text-foreground";
      const badge = it.badge
        ? `<span class="ml-auto badge ${isActive ? "badge-primary" : "badge-neutral"} sb-label">${it.badge}</span>` : "";
      if (it.children) {
        const open = isActive ? "" : "hidden";
        const kids = it.children.map((c) => `
          <a href="${c.href}" class="block rounded-lg py-1.5 pl-11 pr-3 text-sm transition-colors sb-label
             ${c.key === active ? "text-primary font-medium" : "text-sidebar-foreground/70 hover:text-foreground"}">${c.label}</a>`).join("");
        return `
          <div data-collapsible>
            <button class="${base} ${state} w-full" data-toggle-sub>
              ${icon(it.icon)}<span class="truncate sb-label">${it.label}</span>
              ${icon("chevron-right", "size-4 ml-auto transition-transform sb-label " + (isActive ? "rotate-90" : ""))}
            </button>
            <div class="mt-0.5 space-y-0.5 ${open}" data-sub>${kids}</div>
          </div>`;
      }
      return `<a href="${it.href}" data-tip="${it.label}" class="${base} ${state}">
                ${icon(it.icon)}<span class="truncate sb-label">${it.label}</span>${badge}</a>`;
    }).join("");
    return `
      <div class="px-3">
        <div class="px-3 pt-5 pb-1.5 text-[11px] font-semibold uppercase tracking-wider text-muted-foreground sb-label">${g.group}</div>
        <div class="space-y-0.5">${items}</div>
      </div>`;
  }).join("");
}

function shellMarkup(active, title) {
  return `
  <div class="flex min-h-screen">
    <!-- ===== Sidebar ===== -->
    <aside id="sidebar" class="fixed inset-y-0 left-0 z-50 flex w-64 -translate-x-full flex-col border-r border-sidebar-border bg-sidebar transition-all duration-200 md:translate-x-0">
      <div class="flex h-16 items-center gap-2.5 border-b border-sidebar-border px-5">
        <div class="grid size-9 place-items-center rounded-xl bg-gradient-to-br from-primary to-teal text-white font-bold shrink-0">S</div>
        <div class="sb-label leading-tight">
          <div class="font-display font-bold tracking-tight">SAARVIX</div>
          <div class="text-[11px] text-muted-foreground -mt-0.5">Lead Platform</div>
        </div>
        <button id="collapseBtn" class="btn btn-ghost btn-icon btn-sm ml-auto hidden md:inline-flex sb-label" title="Collapse">
          ${icon("panel-left-close", "size-4")}
        </button>
      </div>
      <nav class="flex-1 overflow-y-auto scroll-thin pb-6">${navMarkup(active)}</nav>
      <div class="relative border-t border-sidebar-border p-3">
        <button class="w-full flex items-center gap-3 rounded-lg p-2 hover:bg-sidebar-accent transition-colors text-left" data-dropdown="profileMenu">
          <div class="avatar size-9 bg-gradient-to-br from-primary to-teal text-xs">RV</div>
          <div class="min-w-0 sb-label">
            <div class="truncate text-sm font-semibold">Rahej Verma</div>
            <div class="truncate text-xs text-muted-foreground">vivek@saarvix.in</div>
          </div>
          ${icon("chevron-down", "size-4 ml-auto text-muted-foreground sb-label")}
        </button>
        <div id="profileMenu" data-dropdown-menu class="menu hidden absolute bottom-full left-3 right-3 mb-2 z-50">
          <a href="profile.html" class="menu-item">${icon("user-round", "size-4")}Profile</a>
          <a href="settings.html" class="menu-item">${icon("settings", "size-4")}Settings</a>
          <button class="menu-item" id="themeMenuBtn">${icon("moon", "size-4")}Toggle theme</button>
          <div class="menu-sep"></div>
          <a href="login.html" class="menu-item text-coral">${icon("log-out", "size-4")}Sign out</a>
        </div>
      </div>
    </aside>

    <!-- backdrop for mobile -->
    <div id="sbBackdrop" class="fixed inset-0 z-40 bg-ink/40 backdrop-blur-sm md:hidden hidden"></div>

    <!-- ===== Main column ===== -->
    <div id="mainCol" class="flex min-w-0 flex-1 flex-col md:pl-64 transition-all duration-200">
      <!-- Top header -->
      <header class="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-border glass px-4 md:px-6">
        <button id="menuBtn" class="btn btn-ghost btn-icon md:hidden">${icon("menu", "size-5")}</button>
        <div class="flex items-center gap-2 min-w-0">
          <h1 class="font-display text-lg font-semibold tracking-tight truncate">${title}</h1>
        </div>

        <!-- Global search -->
        <div class="relative ml-auto hidden lg:block w-72">
          ${icon("search", "size-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground")}
          <input class="input input-sm pl-9 pr-12" placeholder="Search anything…">
          <span class="kbd absolute right-2 top-1/2 -translate-y-1/2">⌘K</span>
        </div>

        <div class="flex items-center gap-1.5 ml-auto lg:ml-0">
          <!-- Workspace switcher -->
          <div class="relative hidden xl:block">
            <button class="btn btn-outline btn-sm" data-dropdown="wsMenu">
              ${icon("layers", "size-4 text-primary")} Acme Inc ${icon("chevron-down", "size-3.5 text-muted-foreground")}
            </button>
            <div id="wsMenu" data-dropdown-menu class="menu hidden absolute right-0 mt-2 w-56 z-50">
              <div class="px-2 py-1 text-[11px] uppercase tracking-wider text-muted-foreground">Workspaces</div>
              <button class="menu-item"><span class="avatar size-6 bg-primary text-[10px]">A</span>Acme Inc ${icon("check", "size-4 ml-auto text-primary")}</button>
              <button class="menu-item"><span class="avatar size-6 bg-teal text-[10px]">G</span>Globex</button>
              <button class="menu-item"><span class="avatar size-6 bg-coral text-[10px]">S</span>Soylent</button>
              <div class="menu-sep"></div>
              <button class="menu-item">${icon("plus", "size-4")}Create workspace</button>
            </div>
          </div>
          <!-- Quick add -->
          <div class="relative">
            <button class="btn btn-outline btn-icon" title="Quick add" data-dropdown="addMenu">${icon("plus", "size-5 text-primary")}</button>
            <div id="addMenu" data-dropdown-menu class="menu hidden absolute right-0 mt-2 w-52 z-50">
              <a href="leads.html" class="menu-item">${icon("user-plus", "size-4 text-primary")}New Lead</a>
              <a href="contacts.html" class="menu-item">${icon("users", "size-4 text-teal")}New Contact</a>
              <a href="tasks.html" class="menu-item">${icon("check-square", "size-4 text-warning")}New Task</a>
              <a href="campaigns.html" class="menu-item">${icon("megaphone", "size-4 text-coral")}New Campaign</a>
            </div>
          </div>
          <!-- Notifications -->
          <div class="relative">
            <button class="btn btn-ghost btn-icon relative" title="Notifications" data-dropdown="notifMenu">
              ${icon("bell", "size-5")}
              <span class="absolute right-1.5 top-1.5 size-2 rounded-full bg-coral ring-2 ring-background"></span>
            </button>
            <div id="notifMenu" data-dropdown-menu class="menu hidden absolute right-0 mt-2 w-80 !p-0 z-50">
              <div class="flex items-center justify-between px-3 py-2.5 border-b border-border"><span class="font-semibold text-sm">Notifications</span><span class="badge badge-primary">3 new</span></div>
              <div class="max-h-80 overflow-y-auto scroll-thin divide-rows">
                <a href="#" class="flex gap-3 p-3 hover:bg-accent"><span class="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">${icon("user-plus", "size-4")}</span><span class="text-sm"><span class="font-medium">New lead</span> assigned to you — James Carter<span class="block text-xs text-muted-foreground mt-0.5">2m ago</span></span></a>
                <a href="#" class="flex gap-3 p-3 hover:bg-accent"><span class="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg bg-success/15 text-success">${icon("phone-call", "size-4")}</span><span class="text-sm">Call completed with <span class="font-medium">Sofia Almeida</span><span class="block text-xs text-muted-foreground mt-0.5">18m ago</span></span></a>
                <a href="#" class="flex gap-3 p-3 hover:bg-accent"><span class="mt-0.5 grid size-8 shrink-0 place-items-center rounded-lg bg-coral/15 text-coral">${icon("alert-triangle", "size-4")}</span><span class="text-sm">Campaign <span class="font-medium">Q2 Outreach</span> budget at 90%<span class="block text-xs text-muted-foreground mt-0.5">1h ago</span></span></a>
              </div>
              <a href="#" class="block text-center text-sm text-primary py-2.5 border-t border-border hover:bg-accent">View all notifications</a>
            </div>
          </div>
          <!-- Theme switch -->
          <button id="themeBtn" class="btn btn-ghost btn-icon" title="Toggle theme">${icon("moon", "size-5 dark:hidden")}${icon("sun", "size-5 hidden dark:block")}</button>
          <!-- AI assistant -->
          <button class="btn btn-primary btn-sm" onclick="toast('SAVI assistant is connecting…', 'info')">${icon("sparkles", "size-4")}<span class="hidden sm:inline">AI Assistant</span></button>
        </div>
      </header>

      <main class="flex-1 p-4 md:p-6" id="pageMount"></main>
    </div>
  </div>`;
}

function mountShell({ active, title }) {
  const tpl = document.getElementById("page-content");
  document.body.insertAdjacentHTML("afterbegin", shellMarkup(active, title));
  if (tpl) document.getElementById("pageMount").appendChild(tpl.content.cloneNode(true));

  // Icons
  if (window.lucide) lucide.createIcons();
  // Wire shared page interactions (segmented/tabs/checkboxes) on injected content
  if (window.wireApp) window.wireApp();

  // Theme
  const applyTheme = (t) => {
    document.documentElement.classList.toggle("dark", t === "dark");
    try { localStorage.setItem("saarvix-theme", t); } catch (e) {}
  };
  const toggleTheme = () => applyTheme(document.documentElement.classList.contains("dark") ? "light" : "dark");
  document.getElementById("themeBtn").addEventListener("click", toggleTheme);
  const themeMenuBtn = document.getElementById("themeMenuBtn");
  if (themeMenuBtn) themeMenuBtn.addEventListener("click", toggleTheme);

  // Mobile sidebar
  const sb = document.getElementById("sidebar");
  const bd = document.getElementById("sbBackdrop");
  const openMobile = () => { sb.classList.remove("-translate-x-full"); bd.classList.remove("hidden"); };
  const closeMobile = () => { sb.classList.add("-translate-x-full"); bd.classList.add("hidden"); };
  document.getElementById("menuBtn").addEventListener("click", openMobile);
  bd.addEventListener("click", closeMobile);

  // Collapse (desktop)
  document.getElementById("collapseBtn").addEventListener("click", () => {
    const collapsed = sb.classList.toggle("md:w-[72px]");
    sb.classList.toggle("w-64", !collapsed);
    document.getElementById("mainCol").classList.toggle("md:pl-[72px]", collapsed);
    document.getElementById("mainCol").classList.toggle("md:pl-64", !collapsed);
    document.querySelectorAll(".sb-label").forEach((el) => el.classList.toggle("md:hidden", collapsed));
  });

  // Sub-menu toggles
  document.querySelectorAll("[data-toggle-sub]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const sub = btn.parentElement.querySelector("[data-sub]");
      const chev = btn.querySelector('[data-lucide="chevron-right"]');
      sub.classList.toggle("hidden");
      if (chev) chev.classList.toggle("rotate-90");
    });
  });
}

/* ---- Generic drawer/modal helpers usable from any page ---- */
function openOverlay(id) {
  const el = document.getElementById(id);
  const ov = document.getElementById(id + "-overlay");
  if (ov) ov.classList.add("open");
  if (el) el.classList.add("open");
}
function closeOverlay(id) {
  const el = document.getElementById(id);
  const ov = document.getElementById(id + "-overlay");
  if (ov) ov.classList.remove("open");
  if (el) el.classList.remove("open");
}

window.mountShell = mountShell;
window.openOverlay = openOverlay;
window.closeOverlay = closeOverlay;
