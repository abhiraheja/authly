/* =============================================================
   SAARVIX Admin — shared page interactions
   - wire():       per-page widgets (segmented, tabs, check-all,
                   accordions, star ratings) — re-run after each mount.
   - globalInit(): document-level behaviours wired ONCE
                   (dropdowns, dismiss, Escape, toast, overlay).
   mountShell() calls window.wireApp() after injecting content.
   ============================================================= */

function wire() {
  // Segmented controls
  document.querySelectorAll("[data-segmented]").forEach((grp) => {
    if (grp.__wired) return; grp.__wired = true;
    grp.addEventListener("click", (e) => {
      const btn = e.target.closest("button");
      if (!btn) return;
      grp.querySelectorAll("button").forEach((b) => b.classList.remove("active"));
      btn.classList.add("active");
    });
  });

  // Tabs: [data-tabs] with .tab[data-target] + panels [data-panel] in [data-tabs-root]
  document.querySelectorAll("[data-tabs]").forEach((grp) => {
    if (grp.__wired) return; grp.__wired = true;
    grp.addEventListener("click", (e) => {
      const tab = e.target.closest(".tab");
      if (!tab) return;
      grp.querySelectorAll(".tab").forEach((t) => t.classList.remove("active"));
      tab.classList.add("active");
      const root = grp.closest("[data-tabs-root]") || document;
      root.querySelectorAll("[data-panel]").forEach((p) => p.classList.add("hidden"));
      const target = root.querySelector(`[data-panel="${tab.dataset.target}"]`);
      if (target) target.classList.remove("hidden");
    });
  });

  // "Select all" checkbox in tables
  document.querySelectorAll("[data-check-all]").forEach((master) => {
    if (master.__wired) return; master.__wired = true;
    master.addEventListener("change", () => {
      const scope = master.closest("table") || document;
      scope.querySelectorAll("[data-row-check]").forEach((c) => (c.checked = master.checked));
    });
  });

  // Accordions: [data-accordion] > [data-acc-item] > [data-acc-trigger] + [data-acc-panel]
  document.querySelectorAll("[data-acc-trigger]").forEach((trig) => {
    if (trig.__wired) return; trig.__wired = true;
    trig.addEventListener("click", () => {
      const item = trig.closest("[data-acc-item]");
      const panel = item.querySelector("[data-acc-panel]");
      const chev = trig.querySelector("[data-acc-chev]");
      const open = !panel.classList.contains("hidden");
      panel.classList.toggle("hidden", open);
      if (chev) chev.classList.toggle("rotate-180", !open);
    });
  });

  // Star ratings: [data-rating] with data-value; renders/locks on click
  document.querySelectorAll("[data-rating]").forEach((host) => {
    if (host.__wired) return; host.__wired = true;
    const render = (val) => {
      host.querySelectorAll("[data-star]").forEach((s, i) => {
        s.classList.toggle("text-warning", i < val);
        s.classList.toggle("fill-current", i < val);
        s.classList.toggle("text-muted-foreground", i >= val);
      });
    };
    let value = +host.dataset.value || 0;
    host.querySelectorAll("[data-star]").forEach((s, i) => {
      s.addEventListener("mouseenter", () => render(i + 1));
      s.addEventListener("click", () => { value = i + 1; host.dataset.value = value; render(value); });
    });
    host.addEventListener("mouseleave", () => render(value));
    render(value);
  });
}

/* ---- Global toast ---- */
function toast(msg, type = "info") {
  let host = document.getElementById("toastHost");
  if (!host) {
    host = document.createElement("div");
    host.id = "toastHost";
    host.className = "fixed top-4 right-4 z-[80] flex flex-col gap-2 items-end";
    document.body.appendChild(host);
  }
  const tones = {
    success: ["check-circle-2", "text-success"],
    error:   ["alert-circle",   "text-coral"],
    danger:  ["alert-circle",   "text-coral"],
    warning: ["alert-triangle", "text-warning"],
    info:    ["info",           "text-primary"],
  };
  const [icon, color] = tones[type] || tones.info;
  const el = document.createElement("div");
  el.className = "card card-body !p-3 flex items-center gap-3 shadow-lg min-w-[260px] max-w-sm translate-x-4 opacity-0 transition-all duration-300";
  el.innerHTML = `<i data-lucide="${icon}" class="size-5 ${color} shrink-0"></i>
    <div class="text-sm flex-1">${msg}</div>
    <button class="btn btn-ghost btn-icon btn-sm shrink-0" aria-label="Dismiss"><i data-lucide="x" class="size-4"></i></button>`;
  host.appendChild(el);
  if (window.lucide) lucide.createIcons();
  requestAnimationFrame(() => el.classList.remove("translate-x-4", "opacity-0"));
  const close = () => { el.classList.add("translate-x-4", "opacity-0"); setTimeout(() => el.remove(), 300); };
  el.querySelector("button").addEventListener("click", close);
  setTimeout(close, 4000);
}

/* ---- Overlay (modal/drawer) helpers ---- */
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
function closeAllOverlays() {
  document.querySelectorAll(".overlay.open").forEach((o) => o.classList.remove("open"));
  document.querySelectorAll(".modal.open, .drawer.open").forEach((m) => m.classList.remove("open"));
}
function closeAllDropdowns(except) {
  document.querySelectorAll("[data-dropdown-menu]").forEach((m) => { if (m !== except) m.classList.add("hidden"); });
}

/* ---- Document-level behaviours, wired once ---- */
function globalInit() {
  if (window.__saarvixGlobal) return;
  window.__saarvixGlobal = true;

  document.addEventListener("click", (e) => {
    // Dropdown trigger: <button data-dropdown="menuId">  + <div id="menuId" data-dropdown-menu class="menu hidden ...">
    const trig = e.target.closest("[data-dropdown]");
    if (trig) {
      const menu = document.getElementById(trig.getAttribute("data-dropdown"));
      closeAllDropdowns(menu);
      if (menu) menu.classList.toggle("hidden");
      e.stopPropagation();
      return;
    }
    // Dismiss: <button data-dismiss> removes closest [data-alert]; or data-dismiss="#id"/selector
    const dis = e.target.closest("[data-dismiss]");
    if (dis) {
      const sel = dis.getAttribute("data-dismiss");
      const target = sel ? (dis.closest(sel) || document.querySelector(sel)) : dis.closest("[data-alert]");
      if (target) target.remove();
      e.stopPropagation();
      return;
    }
    // Click outside an open dropdown closes it
    if (!e.target.closest("[data-dropdown-menu]")) closeAllDropdowns();
  });

  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape") { closeAllOverlays(); closeAllDropdowns(); }
  });
}

// Expose globals
window.wireApp = function () { globalInit(); wire(); };
window.toast = toast;
window.openOverlay = openOverlay;
window.closeOverlay = closeOverlay;

if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", window.wireApp);
else window.wireApp();
