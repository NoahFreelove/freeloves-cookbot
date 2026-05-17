// Phase 10 / Plan 10-07 / QOL-06 / D-53 — variable-chip insertion at the textarea caret.
// Module shape mirrors recipe-chip-composer.js (window.<Name> = { ... }; no ES modules).
window.CookbotPromptEditor = {
    insertAtCursor(textareaId, token) {
        const el = document.getElementById(textareaId);
        if (!el || typeof el.selectionStart !== 'number') return false;
        const start = el.selectionStart;
        const end = el.selectionEnd;
        el.setRangeText(token, start, end, 'end');
        el.dispatchEvent(new Event('input', { bubbles: true }));
        el.focus();
        return true;
    }
};
