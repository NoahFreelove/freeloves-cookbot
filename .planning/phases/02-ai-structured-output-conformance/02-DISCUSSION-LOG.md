# Phase 2: AI Structured Output & Conformance - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-25
**Phase:** 02-ai-structured-output-conformance
**Areas discussed:** Streaming UX with structured output, "Edit and save anyway" UX after 2 failed retries, Cookbook-import consent banner (AI-09), XML-tag wrapping scope (AI-08)

---

## Streaming UX with Structured Output

| Option | Description | Selected |
|--------|-------------|----------|
| Compose-then-reveal | Internally accumulate streamed JSON; show "Drafting recipe…" indicator only; render recipe card after final-chunk validation. | ✓ |
| Stream a friendly progress signal | Parse partial JSON live, show high-level signals as fields appear ("Got title… 6 ingredients… 4 steps so far"). | |
| Hybrid streaming | Keep token streaming for non-recipe AiChat turns; compose-then-reveal only for recipe-emitting calls. | |
| Show raw JSON streaming | Render partial JSON as it arrives. | |

**User's choice:** Compose-then-reveal (Recommended).
**Notes:** Cleanest UX, matches what users actually want from a recipe (atomic result), avoids exposing JSON syntax mid-stream.

| Option | Description | Selected |
|--------|-------------|----------|
| Non-streaming `Task<StructuredResult<T>>` | SendStructuredAsync returns one final validated result; SSE used internally for transport. | ✓ |
| `IAsyncEnumerable<StructuredProgress<T>>` | Yield progress events (Started, PartialField, Completed, Failed). | |
| Both — streaming + non-streaming | Two overloads. | |

**User's choice:** Non-streaming `Task<StructuredResult<T>>` (Recommended).
**Notes:** Simpler contract; "composing…" indicator handles latency UX. Repair retries owned inside `IAiRecipeGenerator`.

---

## "Edit and save anyway" UX After 2 Failed Retries

| Option | Description | Selected |
|--------|-------------|----------|
| Open chip editor pre-filled best-effort | Run raw output through `IRecipeFormatParser` (parser-route + coercion); open `RecipeEditor.razor` pre-filled, validation errors inline. Save still applies standard validation. | ✓ |
| Read-only raw viewer + Try Again | Show raw response in a modal with copy/retry; no path to persist. | |
| Quarantine save on AiConversation | Persist failed output as a chat-message attachment, never as a Recipe row. | |

**User's choice:** Open chip editor pre-filled best-effort (Recommended).
**Notes:** Honors Phase 1 invariant (non-conforming recipes never persist). Reuses Phase 3's chip composer once it ships.

| Option | Description | Selected |
|--------|-------------|----------|
| Wire to existing `RecipeEditor.razor` today | Phase 3's chip composer drops in for free when ready. Zero coupling. | ✓ |
| "Pending recipe" staging table | Phase 3 picks it up later. | |
| Block this affordance until Phase 3 ships | Show degraded "copy text" viewer in interim. | |

**User's choice:** Wire to existing `RecipeEditor.razor` today (Recommended).
**Notes:** Phase 2 doesn't block on Phase 3.

---

## Cookbook-Import Consent Banner (AI-09)

User pushed back: *"Why do we need the consent banner? Realistically what bad stuff could the AI possibly do with just a text output?"*

Threat-model review during discussion:

- **Real risks (small):** token waste from injected `ignore-previous` payloads (capped by `max_tokens`); phishing-shaped output (Anthropic safety training resists); markdown image-URL exfil IF chat renders external `<img>` tags from assistant responses.
- **Theatrical risks:** "ignore previous instructions" itself (modern Claude resists; XML wrapping handles it), system-prompt disclosure (no secrets in prompt; keys are headers), cooking misinformation (very unlikely).
- **Threat actor:** Has to be someone the user already added via the `CookbookShare` table. Trusted-LAN posture means sharers are trusted by definition.

**Conclusion:** AI-08 (XML wrapping) is the load-bearing technical defense. AI-09 (the user-visible banner) was hygiene without proportional value.

| Option | Description | Selected |
|--------|-------------|----------|
| Drop AI-09; keep AI-08 (XML wrapping) | Move AI-09 to FUTURE-12. Add AI-08-AUDIT for Markdig hardening. | ✓ |
| Keep AI-09 but downgrade to one-line warning | Inline informational text, no persistence. | |
| Keep AI-09 as originally specified | New table, modal-blocks-import, plain-language explainer. | |

**User's choice:** Drop AI-09; keep AI-08 (XML wrapping).
**Notes:** AI-09 reframed as FUTURE-12 ("relevant if/when the app ever supports cookbook imports from untrusted sources outside a LAN"). Added in-phase task: audit AiChat's Markdig pipeline for `<img>`/external-link exfil surface, lock down if reachable.

The pre-decision questions on persistence/trigger/wording (where state lives, when banner fires, copy direction) are moot now and not recorded as decisions.

---

## XML-Tag Wrapping Scope (AI-08)

| Option | Description | Selected |
|--------|-------------|----------|
| Recipe-context-only | Wrap only when full RecipeDocument is injected (cooking-step assist + "ask about this recipe" surfaces). | ✓ |
| All non-active-user content | Recipes + cookbook descriptions + recipe notes + comments. | |
| Everything user-typed | Wrap active user's own input too. | |

**User's choice:** Recipe-context-only (Recommended).
**Notes:** Smallest surface; matches the threat model. AiChat freeform user typing is NOT wrapped (the user is the user, not untrusted content).

| Option | Description | Selected |
|--------|-------------|----------|
| Helper at call sites: `PromptInjectionGuard.WrapRecipe` | Pure static; called by `RecipeCookingAiContext` + `IAiRecipeGenerator`. Visible at call sites. | ✓ |
| Auto-wrap at `IAiService` boundary | Sensitive-content parameter on SendMessageAsync; centralized. | |

**User's choice:** Helper at call sites (Recommended).
**Notes:** Visibility beats centralization for security-relevant transformations.

---

## Claude's Discretion

Areas where the user deferred to the planner during plan-phase (see CONTEXT.md `Claude's Discretion` block for the full list):

- File grouping in `CookBot.Application/AI/`
- Repair-loop helper class extraction (method vs class)
- Specific log levels for AI events
- Whether `SecretRedactor` accepts the resolved key as a parameter or fetches via DI
- AiChat "Save this recipe" button styling
- Test-framework details for stream-accumulator behavior
- Markdig lockdown mechanism (after AI-08-AUDIT confirms what's actually needed)
- Whether MIGRATION-06 needs any work beyond a verification test

## Deferred Ideas

(Mirror of CONTEXT.md `<deferred>` section.)

- AI-09 → FUTURE-12 (per-sharer consent banner relevant only if app goes multi-tenant)
- Token-cost telemetry → FUTURE-02
- Tool-use fallback → FUTURE-09
- Encrypt-at-rest for AiApiKey → FUTURE-01
- AiChat.razor UX overhaul (broader than targeted Phase 2 edits)
- Prompt snapshot test → POLISH-05 (Phase 4)
- Tags relational table → POLISH-04 (Phase 4)
- Per-step temperature → FEATURE-V2 (Phase 4)
- Chip composer → Phase 3
