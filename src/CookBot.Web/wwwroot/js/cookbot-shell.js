// CookBot shell helpers — accent + density attribute application on <html>.
// Phase 5 / D-05: defaults are accent=orange, density=comfy.
// User-facing accent picker is FUTURE-14; density toggle lands on Profile in Phase 7.
// NOTE: this module deliberately does NOT touch dark-mode state — the existing
//       cookbot_dark_mode toggle in MainLayout.razor (body.dark-mode class) is the
//       single source of truth for that and is preserved verbatim.

window.cookbot = window.cookbot || {};

window.cookbot.setAccent = function (name) {
  // Allowed: "orange" | "terracotta" | "sage". Unknown values default to orange.
  var allowed = ["orange", "terracotta", "sage"];
  var v = allowed.indexOf(name) >= 0 ? name : "orange";
  document.documentElement.setAttribute("data-accent", v);
};

window.cookbot.setDensity = function (mode) {
  // Allowed: "comfy" | "compact". Unknown values default to comfy.
  // Persists in localStorage so subsequent visits / page reloads honor the choice.
  var allowed = ["comfy", "compact"];
  var v = allowed.indexOf(mode) >= 0 ? mode : "comfy";
  document.documentElement.setAttribute("data-density", v);
  try { localStorage.setItem("cookbot_density", v); } catch (e) { /* ignore quota / privacy mode */ }
};

window.cookbot.getDensity = function () {
  // Returns the persisted density preference, or "comfy" if unset.
  try {
    var stored = localStorage.getItem("cookbot_density");
    if (stored === "comfy" || stored === "compact") return stored;
  } catch (e) { /* ignore */ }
  return "comfy";
};

window.cookbot.applyDefaults = function () {
  // Idempotent — safe to call on every render. Phase 7 Plan 07-05: density toggle
  // ships on Profile, persisting via cookbot.setDensity() → localStorage. Restore
  // here so the preference survives reload / new sessions.
  if (!document.documentElement.hasAttribute("data-accent")) {
    document.documentElement.setAttribute("data-accent", "orange");
  }
  if (!document.documentElement.hasAttribute("data-density")) {
    var density = "comfy";
    try {
      var stored = localStorage.getItem("cookbot_density");
      if (stored === "comfy" || stored === "compact") density = stored;
    } catch (e) { /* ignore */ }
    document.documentElement.setAttribute("data-density", density);
  }
};

window.cookbot.hardReloadTo = function (href) {
  // Force a full document reload to a target URL. Used by the user-switcher so
  // the SignalR circuit is torn down and rebuilt with the new user's scope.
  // location.assign is more reliable than location.reload(): it works even when
  // the URL is the current page, and it bypasses Blazor's enhanced-nav optimizer.
  var target = (typeof href === "string" && href.length > 0) ? href : "/";
  try {
    window.location.assign(target);
  } catch (e) {
    // Last-resort fallback for sandboxed iframes / locked-down environments.
    window.location.href = target;
  }
};
