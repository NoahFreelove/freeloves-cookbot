// src/CookBot.Web/wwwroot/js/cooking-session-state.js
//
// Browser-side persistence for in-progress cooking sessions and the active
// long-running timer (Plan 07-09). Two keys, single-session scope each:
//
//   cookbot_in_progress_recipe = { recipeId, currentStepIndex, scaledServings, startedAtIso }
//   cookbot_active_timer       = { recipeId, stepLabel, durationSeconds, startedAtIso }
//
// CookingMode owns the write path; Home reads both on first render.
// Failures (e.g. storage quota, private mode) fail-soft — Blazor never throws.

window.CookbotSession = {
    IN_PROGRESS_KEY: 'cookbot_in_progress_recipe',
    ACTIVE_TIMER_KEY: 'cookbot_active_timer',

    saveInProgress(recipeId, currentStepIndex, scaledServings) {
        try {
            const existing = this.readInProgress();
            const startedAtIso = (existing && existing.recipeId === recipeId && existing.startedAtIso)
                ? existing.startedAtIso
                : new Date().toISOString();
            localStorage.setItem(this.IN_PROGRESS_KEY, JSON.stringify({
                recipeId,
                currentStepIndex,
                scaledServings,
                startedAtIso,
            }));
            return startedAtIso;
        } catch (e) {
            return null;
        }
    },

    readInProgress() {
        try {
            const raw = localStorage.getItem(this.IN_PROGRESS_KEY);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            if (typeof parsed !== 'object' || parsed === null) return null;
            return parsed;
        } catch (e) {
            return null;
        }
    },

    /**
     * Returns the in-progress entry only if its recipeId matches the supplied id;
     * otherwise clears the entry (so a stale session can't hijack a different recipe)
     * and returns null. CookingMode calls this on load with the current page's recipeId.
     */
    readInProgressForRecipe(recipeId) {
        const entry = this.readInProgress();
        if (!entry) return null;
        if (entry.recipeId !== recipeId) {
            this.clearInProgress();
            return null;
        }
        return entry;
    },

    clearInProgress() {
        try { localStorage.removeItem(this.IN_PROGRESS_KEY); } catch (e) { /* ignore */ }
    },

    saveActiveTimer(recipeId, stepLabel, durationSeconds) {
        try {
            localStorage.setItem(this.ACTIVE_TIMER_KEY, JSON.stringify({
                recipeId,
                stepLabel: stepLabel || '',
                durationSeconds: durationSeconds | 0,
                startedAtIso: new Date().toISOString(),
            }));
        } catch (e) { /* ignore */ }
    },

    readActiveTimer() {
        try {
            const raw = localStorage.getItem(this.ACTIVE_TIMER_KEY);
            if (!raw) return null;
            const parsed = JSON.parse(raw);
            if (typeof parsed !== 'object' || parsed === null) return null;

            // Compute remaining seconds for the caller.
            const startedAtMs = Date.parse(parsed.startedAtIso);
            if (!Number.isFinite(startedAtMs)) return null;
            const elapsed = Math.floor((Date.now() - startedAtMs) / 1000);
            const remaining = Math.max(0, (parsed.durationSeconds | 0) - elapsed);
            if (remaining <= 0) {
                // Expired — clear and report none. Home re-renders without the band.
                this.clearActiveTimer();
                return null;
            }
            return {
                recipeId: parsed.recipeId,
                stepLabel: parsed.stepLabel || '',
                durationSeconds: parsed.durationSeconds | 0,
                startedAtIso: parsed.startedAtIso,
                remainingSeconds: remaining,
            };
        } catch (e) {
            return null;
        }
    },

    clearActiveTimer() {
        try { localStorage.removeItem(this.ACTIVE_TIMER_KEY); } catch (e) { /* ignore */ }
    },

    /**
     * Computes a human-friendly "started 12m ago"-style string from an ISO
     * timestamp. Used by Home to label the resume cards. Pure JS so the home
     * page can update the label without a server round-trip.
     */
    formatStartedAgo(startedAtIso) {
        const startedAtMs = Date.parse(startedAtIso);
        if (!Number.isFinite(startedAtMs)) return '';
        const deltaMs = Math.max(0, Date.now() - startedAtMs);
        const m = Math.floor(deltaMs / 60000);
        if (m < 1) return 'just now';
        if (m < 60) return m + 'm ago';
        const h = Math.floor(m / 60);
        if (h < 24) return h + 'h ago';
        const d = Math.floor(h / 24);
        return d + 'd ago';
    },
};
