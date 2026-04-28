// CookBot dialog focus trap + body scroll lock + ESC handler + outside-click helper.
// Phase 5 / Plan 05-04 / DIALOG-01..04. Exposed under window.cookbotDialog.

window.cookbotDialog = window.cookbotDialog || (function () {
    var traps = {};   // elementId -> { handleKey, previousFocus }
    var scrollLockCount = 0;

    function focusableSelector() {
        return 'a[href], area[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    }

    function trapFocus(elementId, dotnetRef) {
        var el = document.getElementById(elementId);
        if (!el) return;
        var prev = document.activeElement;
        var firstFocusable = el.querySelector(focusableSelector());
        if (firstFocusable) {
            try { firstFocusable.focus(); } catch (e) { /* ignore */ }
        } else {
            // No focusable child — focus the dialog itself so ESC keydown still fires.
            try { el.focus(); } catch (e) { /* ignore */ }
        }
        function handleKey(e) {
            if (e.key === 'Escape') {
                if (dotnetRef && dotnetRef.invokeMethodAsync) {
                    dotnetRef.invokeMethodAsync('OnEscape');
                }
                return;
            }
            if (e.key !== 'Tab') return;
            var nodes = el.querySelectorAll(focusableSelector());
            if (nodes.length === 0) { e.preventDefault(); return; }
            var first = nodes[0];
            var last = nodes[nodes.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                last.focus(); e.preventDefault();
            } else if (!e.shiftKey && document.activeElement === last) {
                first.focus(); e.preventDefault();
            }
        }
        el.addEventListener('keydown', handleKey);
        traps[elementId] = { handleKey: handleKey, previousFocus: prev };

        // Body scroll lock — refcounted so multiple stacked dialogs unlock cleanly.
        if (scrollLockCount === 0) {
            document.body.style.overflow = 'hidden';
        }
        scrollLockCount++;
    }

    function releaseFocus(elementId) {
        var el = document.getElementById(elementId);
        var trap = traps[elementId];
        if (el && trap) { el.removeEventListener('keydown', trap.handleKey); }
        if (trap && trap.previousFocus && trap.previousFocus.focus) {
            try { trap.previousFocus.focus(); } catch (e) { /* ignore */ }
        }
        if (trap) { delete traps[elementId]; }

        scrollLockCount = Math.max(0, scrollLockCount - 1);
        if (scrollLockCount === 0) { document.body.style.overflow = ''; }
    }

    function bindOutsideClick(elementId, dotnetRef) {
        function handler(e) {
            var el = document.getElementById(elementId);
            if (!el) return;
            if (!el.contains(e.target)) {
                if (dotnetRef && dotnetRef.invokeMethodAsync) {
                    dotnetRef.invokeMethodAsync('OnOutsideClick');
                }
            }
        }
        // Defer attaching by one tick to avoid catching the open-click event itself.
        setTimeout(function () { document.addEventListener('mousedown', handler); }, 0);
        traps['outside_' + elementId] = { handleKey: handler };
    }

    function unbindOutsideClick(elementId) {
        var key = 'outside_' + elementId;
        var trap = traps[key];
        if (trap) {
            document.removeEventListener('mousedown', trap.handleKey);
            delete traps[key];
        }
    }

    return {
        trapFocus: trapFocus,
        releaseFocus: releaseFocus,
        bindOutsideClick: bindOutsideClick,
        unbindOutsideClick: unbindOutsideClick
    };
})();
