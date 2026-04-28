// recipe-chip-composer.js
// Phase 3 (D-A1, D-D3, D-D4): caret-anchored MudAutocomplete + cooking-mode scroll-highlight + fail-soft probe.
// Module shape mirrors cooking-timers.js (window.<Name> = { ... }; no ES modules).
window.RecipeChipComposer = {
    // D-D4: probe called by C# on first render; if this throws or returns non-"ok",
    // the composer falls back to MudTextField Lines=3 (raw [name](#id) text editing).
    ping() {
        return "ok";
    },

    // D-A1: returns the caret pixel position relative to the parent element rect.
    // Used by C# to absolutely-position the MudAutocomplete<Ingredient> popover at the @-trigger location.
    // For contenteditable spans, uses Selection/Range API (no mirror-div needed).
    getCaretCoords(elementId) {
        const el = document.getElementById(elementId);
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        const sel = window.getSelection();
        if (sel && sel.rangeCount > 0) {
            const r = sel.getRangeAt(0).cloneRange();
            r.collapse(true);
            const cr = r.getClientRects()[0];
            if (cr) {
                return { x: cr.left - rect.left, y: cr.bottom - rect.top };
            }
        }
        // Fallback: bottom-left of the element.
        return { x: 0, y: rect.height };
    },

    // D-D3: cooking-mode ingredient-chip click scrolls the sidebar item into view and pulses a highlight class.
    // Idempotent across rapid clicks via per-element timer cancellation.
    scrollIntoViewWithHighlight(elementId, highlightClass, durationMs) {
        highlightClass = highlightClass || "chip-highlight-pulse";
        durationMs = durationMs || 1500;
        const el = document.getElementById(elementId);
        if (!el) return false;
        el.scrollIntoView({ behavior: "smooth", block: "center" });
        // Cancel any prior timer to avoid flicker on rapid re-clicks.
        if (el._highlightTimer) {
            clearTimeout(el._highlightTimer);
            el.classList.remove(highlightClass);
        }
        el.classList.add(highlightClass);
        el._highlightTimer = setTimeout(() => {
            el.classList.remove(highlightClass);
            el._highlightTimer = null;
        }, durationMs);
        return true;
    },

    // WR-01 / IN-03 (EDITOR-07): Attach input + keydown listeners to a contenteditable segment span.
    // Calls back into .NET via dotNetRef.invokeMethodAsync("OnSegmentInputFromJs", segmentIndex, textContent)
    // and dotNetRef.invokeMethodAsync("OnSegmentKeyDownFromJs", segmentIndex, key, caretOffset).
    // The keydown callback returns true if .NET handled the event (preventDefault should be called).
    // D-D4 contract: any rejection from invokeMethodAsync is swallowed — never throw out of a JS event handler.
    bindSegmentEvents(elementId, dotNetRef, segmentIndex) {
        const el = document.getElementById(elementId);
        if (!el) return false;

        // Idempotent rebind: detach prior listeners if already bound.
        if (el._chipComposerBound === true) {
            this.unbindSegmentEvents(elementId);
        }

        // Input listener: reports el.textContent (not ChangeEventArgs.Value, which is null on contenteditable).
        const inputListener = () => {
            dotNetRef.invokeMethodAsync("OnSegmentInputFromJs", segmentIndex, el.textContent).catch(() => { /* D-D4: circuit may have disposed */ });
        };

        // Keydown listener: reports key name + caret offset; honors boolean return to call preventDefault.
        const keydownListener = (event) => {
            let caretOffset = 0;
            try {
                const sel = window.getSelection();
                if (sel && sel.rangeCount > 0) {
                    const range = sel.getRangeAt(0);
                    if (range.collapsed && (sel.anchorNode === el.firstChild || sel.anchorNode === el)) {
                        caretOffset = range.startOffset;
                    } else {
                        caretOffset = range.startOffset;
                    }
                }
            } catch (_) { /* ignore selection errors */ }

            dotNetRef.invokeMethodAsync("OnSegmentKeyDownFromJs", segmentIndex, event.key, caretOffset)
                .then(handled => {
                    if (handled === true) {
                        event.preventDefault();
                    }
                })
                .catch(() => { /* D-D4: circuit may have disposed */ });
        };

        el.addEventListener("input", inputListener);
        el.addEventListener("keydown", keydownListener);

        // Store listener references for unbindSegmentEvents.
        el._chipComposerInputListener = inputListener;
        el._chipComposerKeydownListener = keydownListener;
        el._chipComposerBound = true;

        return true;
    },

    // WR-01 / IN-03: Detach the input + keydown listeners previously attached by bindSegmentEvents.
    // Called from C# DisposeAsync and before rebinding on re-render.
    unbindSegmentEvents(elementId) {
        const el = document.getElementById(elementId);
        if (!el || el._chipComposerBound !== true) return false;

        el.removeEventListener("input", el._chipComposerInputListener);
        el.removeEventListener("keydown", el._chipComposerKeydownListener);

        delete el._chipComposerInputListener;
        delete el._chipComposerKeydownListener;
        delete el._chipComposerBound;

        return true;
    }
};
