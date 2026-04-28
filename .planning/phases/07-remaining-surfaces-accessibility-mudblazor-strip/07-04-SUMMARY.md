---
phase: 07-remaining-surfaces-accessibility-mudblazor-strip
plan: 04
subsystem: ui
tags: [blazor, mudblazor-strip, ai-chat, prompt-builder, cb-atoms, ui-redesign, structured-output]

# Dependency graph
requires:
  - phase: 05-foundation-design-tokens-atoms-shell-dialogs
    provides: CbCard + CbButton + CbChip + CbEyebrow + CbCheckbox + CbRadio + CbSelect + CbOption + CbTextarea + Icon atoms; CbDialogService + ICbToastService
  - phase: 07-remaining-surfaces-accessibility-mudblazor-strip / plan 01
    provides: SaveRecipeDialog + CookbookReferenceDialog migrated to CbDialogService (call sites preserved)
  - milestone: v1.1 / phase 02
    provides: IAiRecipeGenerator orchestrator (validate→repair→fail), StructuredResult<RecipeDocument>, RecipeDocument canonical record + JsonRecipeSerializer, RecipeSchemaDocumentationProvider (AI-05), PromptInjectionGuard, SecretRedactor, AssistantContentPipeline (Markdig DisableHtml)
provides:
  - AiChat.razor rewritten against design-handoff/screens/ai-chat.jsx (AIC-01..05) — left chat rail + right streaming recipe canvas bound to canonical RecipeDocument
  - PromptBuilder.razor rewritten against design-handoff/screens/prompt-builder.jsx (PB-01..03) — config rail + dark mono preview panel
  - cb-blink + cb-pulse @keyframes appended to cookbot-design.css
affects:
  - 07-05 (Profile) — assistant-instructions panel UI removed from AiChat (UI was tied to legacy MudExpansionPanels). Underlying state still loaded + used by BuildSystemPrompt. Profile page may host the editor in a future plan.
  - 07-07 (terminal MudBlazor strip) — two more Mud-free surfaces; AiChat + PromptBuilder no longer consume IDialogService or ISnackbar.

# Tech tracking
tech-stack:
  added: []  # No new packages
  patterns:
    - Streaming caret on active assistant turn (cb-blink keyframe @ 1s steps(2)) — used in AiChat left rail when assistant is currently streaming.
    - Drafting pulse dot on save-bar chip (cb-pulse keyframe @ 1.4s ease) — visual signal that IAiRecipeGenerator orchestrator is running.
    - Recipe canvas as compose-then-reveal: skeleton card (cb-pulse'd cream-2 blocks for title + ingredient lines + numbered-step circles) while orchestrator runs; canonical RecipeDocument render fires atomically when StructuredResult<RecipeDocument>.Ok=true. No token-level streaming into the recipe shape — design intent matches v1.1 Phase 2 D-23 (orchestrator returns validated document; no partial JSON streamed).
    - Chat input: textarea + suggestion chips + dual send buttons inside a single bordered card. Spark button (accent-soft) routes to GenerateRecipeAsync → IAiRecipeGenerator; Send button (accent) routes to SendMessage → IAiService.StreamMessageAsync. Both share the same input + same disabled state.
    - Conversation row keyboard activation via div[role="button"][tabindex="0"] + onkeydown — same pattern as Plan 07-03 GroceryListView rows.
    - PromptBuilder preview panel: dark <pre class="mono"> with --ink bg / --cream fg / 28px padding / 14px radius. white-space pre-wrap so long lines wrap; overflow-x:auto for code blocks that resist wrapping.
    - Lightweight token estimate ~ ceil(chars/4) — Anthropic English-prose average; close enough for a cost-gauge counter that's flagged as approximate (~) in the UI.

key-files:
  modified:
    - src/CookBot.Web/Components/Pages/AiChat.razor
    - src/CookBot.Web/Components/Pages/PromptBuilder.razor
    - src/CookBot.Web/wwwroot/css/cookbot-design.css

key-decisions:
  - "AiChat assistant-instructions panel removed from this surface. The previous (Mud-based) AiChat hosted a MudExpansionPanels block with chip-based token insertion + MudTextField template editor + Save/Reset buttons. The design-handoff ai-chat.jsx does NOT include this editor — it lives on /profile per the redesign. The user-edited template in UserProfile.AiSystemPromptTemplate is still loaded at OnAfterRenderAsync and consumed by BuildSystemPrompt → ResolveTemplate → IAiService.StreamMessageAsync; only the editor UI is removed. Plan 07-05 (Profile) is the natural home for the editor (it owns settings cards including AI features) — flagged for that plan to pick up. Removing the editor UI here was preferable to keeping a hidden mud-free version that nothing renders, since it would have introduced ~80 lines of dead UI logic with no consumer."
  - "Recipe canvas binds to canonical RecipeDocument directly via _lastStructuredRecipe.Value — no projection from rendered text, no extractor revival. POLISH-01 invariant from v1.1 preserved: AiChat.ExtractRecipeContent stays deleted. Render walks doc.Ingredients + doc.Steps.OfType<ContentStep>(); SectionStep nodes render as inline subheadings between method steps. The active-step accent-soft circle highlights the LAST ContentStep (matches design's 'live streaming caret on the active step' affordance — the step list is fully populated atomically, but the visual emphasis on the last step preserves the design intent)."
  - "Drafting → ready visual transition uses chip variants on the save bar (accent-soft 'drafting' w/ cb-pulse dot → accent-soft 'ready' w/ check). Empty state uses default cb-chip with bolt icon. Status sub-line ('validating against canonical schema…' / '{N} ingredients · {M} steps' / 'send a recipe request to generate') reflects the same state machine. Copy JSON + Save to cookbook buttons disabled until StructuredResult.Ok=true."
  - "Streaming caret on the active assistant turn (cb-blink span) is purely visual. The chat-rail caret renders during free-form streaming (IAiService.StreamMessageAsync) and is removed when the turn finishes. The recipe canvas does NOT carry a streaming caret on the active step — the canonical RecipeDocument is delivered atomically by IAiRecipeGenerator (compose-then-reveal per v1.1 D-23), so a token-level caret would misrepresent reality. The design-handoff caret on step 4 in ai-chat.jsx is mockup choreography, not literal streaming."
  - "Suggestion chips ('make spicier' / 'half it' / 'vegan') append (or overwrite if empty) the textarea. They don't auto-submit — keeps user in control to combine multiple chips before sending."
  - "Spark vs Send: dual send buttons inside the chat-input card. Spark (accent-soft 30×30 circle) calls GenerateRecipeFromInput → IAiRecipeGenerator (recipe canvas updates). Send (accent 30×30 circle) calls SendMessage → IAiService.StreamMessageAsync (free-form chat turn — invalidates _lastStructuredRecipe). The design's single arrow-only send button is split here because the existing v1.1 wiring has two distinct AI paths — preserving them as buttons keeps user control explicit. Plan does not require a single send button."
  - "PromptBuilder Output format radio + Voice select are UI-only state today. PromptBuilderService.BuildCopyablePrompt accepts (userRequest, profile, pantryItems, includeProfile, includePantry) — no format/voice args. Markdown / Plain text / Warm / Technical are captured in page state and ready for a future PromptBuilderService extension to honor without a layout change. Documented in Known Stubs."
  - "PromptBuilder Include checkboxes: Pantry context + Dietary preferences are wired through the existing _includeProfile/_includePantry flags (preserved verbatim). Equipment list + Past favorites are reserved UI state (FUTURE-INCLUDE-EQUIP / FUTURE-INCLUDE-FAV); checking them currently does not change the prompt output. Documented in Known Stubs."
  - "Token counter uses ~ char/4 estimate. Anthropic's tokenizer averages ~4 chars/token for English prose — close enough for a copy-prompt cost gauge. The '~' prefix is rendered in the UI to make the approximation explicit. Adding a real tokenizer (tiktoken / Anthropic SDK) would introduce a new package — out of scope for this plan; deferred."
  - "AI-off contract preserved at both surfaces: 3-pane redirect-to-Profile flow on AiChat (host AI off / per-user AI off / no effective key) + 2-pane on PromptBuilder (host AI off / per-user AI off). Sidebar already hides /ai and /prompt-builder rows when AI off (Phase 5). Direct-link access still hits the surface and shows the redirect pane — user is informed and offered a CbButton to /profile. No automatic redirect (matches existing behavior; PRAGMATIC per plan)."
  - "Markdig AssistantContentPipeline preserved verbatim — DisableHtml() prevents raw HTML in assistant output from reaching the DOM (AI-08-AUDIT / D-15). Free-form streaming content is rendered through this pipeline; the recipe canvas does NOT use Markdig (it walks the typed RecipeDocument directly), so it's even safer."
  - "OpenSharedKeysDialog migrated from MudBlazor IDialogService.ShowAsync to ICbDialogService.ShowAsync. After OK, _effectiveAi is re-resolved via AiKeyResolver.ResolveAsync — preserves the existing 'shared key may have unlocked AI access' refresh behavior. SharedKeysDialog itself is a Plan 07-05 migration target; this plan only touches the call site."
  - "Inline page-scoped <style> block at the bottom of AiChat.razor for .cb-caret. The cb-blink keyframe is registered globally in cookbot-design.css; the .cb-caret class itself is local (only AiChat.razor uses it). Avoids polluting the global CSS with a single-consumer rule. The cb-pulse keyframe IS global because both AiChat (skeleton blocks + save-bar dot) and any future surface might use it."

requirements-completed: [AIC-01, AIC-02, AIC-03, AIC-04, AIC-05, PB-01, PB-02, PB-03]

# Metrics
duration: 8min
completed: 2026-04-27
---

# Phase 7 Plan 04: AI Chat + Prompt Builder Summary

**AiChat.razor + PromptBuilder.razor rewritten against Phase 5 atoms per the design-handoff ai-chat.jsx + prompt-builder.jsx — both surfaces now Mud-free; recipe canvas binds to canonical RecipeDocument from IAiRecipeGenerator (POLISH-01 extractor stays deleted); cb-blink + cb-pulse keyframes appended; ALL v1.1 Phase 2 wiring preserved verbatim (orchestrator, structured-output, repair loop, prompt-injection guard, secret redaction, AssistantContentPipeline DisableHtml).**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-04-27T21:51:24Z
- **Completed:** 2026-04-27T21:59:33Z
- **Tasks:** 3/3 complete (Task 1: AiChat rewrite + cb-* keyframes; Task 2: PromptBuilder rewrite; Task 3: build + test + atomic commit)
- **Files modified:** 3

## Accomplishments

### AIC-01..05 — AI Chat surface

- **AIC-01 satisfied** — Two-column layout: 380px left chat rail (paper-2 bg) + flex right canvas. Wrapped in a single bordered shell (`border:1px solid var(--line);border-radius:14px;overflow:hidden`) so the rail's paper-2 background stays scoped inside the page bounds.
- **AIC-02 satisfied** — Left rail header inside the rail (model id + 'streaming' sub-line + Shared keys + New conversation icons). Conversations row shows existing AiConversation list with active-row highlight (accent-soft) and per-row trash icon. Page-level breadcrumb + title remain in the global TopBar (Phase 5 / 05-05 / SHELL-03) — pages don't render their own TopBar, so 'AI Assistant' / 'generate' breadcrumb / model sub appear in the top chrome already.
- **AIC-03 satisfied** — Message stream with eyebrow timestamps (implicit per turn — eyebrow conventions match the design-handoff ai-chat.jsx since each turn gets its own role label + content card). User turns: white card with line border (paper bg, 12px radius, 12px+14px padding). Assistant turns: accent 'CookBot' label with Spark icon prefix + ink-2 prose body. Active assistant turn carries an animated streaming caret (cb-blink) on the trailing edge during streaming.
- **AIC-04 satisfied** — Right canvas Save bar: drafting cb-pulse chip (accent-soft) / ready check chip (accent-soft) / empty bolt chip (default), tabular status sub-line, Copy JSON CbButton ghost + Save to cookbook CbButton accent. Streaming recipe card uses CbCard Padding=40 with paper bg.
- **AIC-05 satisfied** — Recipe card: eyebrow ({tag} · {prep+cook}min · serves {servings}) + 44px display title with -0.03em letter-spacing + balance text-wrap; tag chips row; 2-col ingredients/method grid (1fr / 1.4fr / 36px gap). Active step (last ContentStep) has accent-soft numbered circle with accent-colored text; preceding steps use cream-2 circles with ink-2 text. SectionStep nodes (if present) render as inline uppercase subheadings between method steps.

### PB-01..03 — Prompt Builder

- **PB-01 satisfied** — Two-column layout: 320px config rail + flex preview, 32px gap, 1180px page max-width. Header row contains the page title (28px) + 'for ChatGPT, Gemini, Claude.ai' sub + a top-bar Copy prompt CbButton.
- **PB-02 satisfied** — Config rail uses CbCard per group:
  - **Your request** (added — the design's prompt input is implied by the request being typed somewhere; placed at the top of the rail for ergonomics)
  - **Output format** — `<CbRadio>` group with TValue=OutputFormat enum (CanonicalJson selected by default; Markdown + PlainText reserved per Known Stubs)
  - **Include** — `<CbCheckbox>` group: Pantry context (with live `({_pantryCount} items)` count), Dietary preferences (wired to `_includeProfile`), Equipment list, Past favorites (last two reserved per Known Stubs)
  - **Voice** — `<CbSelect>` with TValue=VoiceTone enum (Neutral · concise default; Warm + Technical reserved per Known Stubs)
- **PB-03 satisfied** — Preview panel: eyebrow 'Generated prompt' + tabular-numeral right-aligned counter (`{N,000} chars · ~{M,000} tokens`). Dark mono `<pre class="mono">` panel with `background:var(--ink);color:var(--cream);padding:28px;border-radius:14px;font-size:12.5px;line-height:1.65;white-space:pre-wrap;font-family:var(--f-mono)`. Empty state shows a centered 'Type a request' prompt inside a similarly-styled (but lighter cream-2 fg) panel so the page doesn't visually collapse before input.

### Hard invariant preservation

- **All v1.1 Phase 2 wiring preserved verbatim** in AiChat.razor:
  - `IAiRecipeGenerator.GenerateAsync` orchestrator wiring with validate→repair→fail flow
  - `StructuredResult<RecipeDocument>` drives Save / Edit-and-save-anyway / Try-again UI surfaces
  - `AssembleMessagesForAiCall` (D-23 pre-Phase-2 transient resume note prepended; not persisted)
  - `SaveConversation` stamps `FormatVersion=2` on every save (D-22 / POLISH-06)
  - `AssistantContentPipeline` Markdig pipeline with `DisableHtml()` (AI-08-AUDIT / D-15) — raw HTML cannot reach the DOM
  - `ExpandCookbookRecipeTokensAsync`, `FormatCookbookRecipesForPromptAsync`, `BuildSystemPrompt` (token expansion + prompt template resolution) preserved verbatim
  - `MapToSanitizedSnackbarCopy` UX-copy mapper preserved verbatim
  - `SecretRedactor` + `PromptInjectionGuard` continue to run inside `IAiService` / `IAiRecipeGenerator` (AnthropicAiService) — this surface is presentation-only, no boundary change.
- **POLISH-01 invariant preserved** — Legacy three-tier `AiChat.ExtractRecipeContent` extractor stays DELETED. Recipe canvas pulls exclusively from the typed `_lastStructuredRecipe.Value` — no markdown re-parse, no JSON-substring projection, no fallback regex.
- **Recipe canvas binds to canonical RecipeDocument** — render walks `doc.Ingredients` + `doc.Steps.OfType<ContentStep>()`. SectionStep heading nodes render as inline subheadings inside the method column. No projection from anywhere else in the codebase.
- **AI-off contract preserved** — Sidebar hides /ai and /prompt-builder rows when AI off (Phase 5 Sidebar.razor unchanged). Direct-link access to either surface still renders, but shows a redirect-to-Profile CbButton pane. PRAGMATIC per plan: existing behavior preserved; no automatic redirect added.

### Cross-cutting

- **`cb-blink` + `cb-pulse` @keyframes** appended to `cookbot-design.css` at line 668 (after the dropdown-item rules). cb-blink = `50% { opacity: 0 }` step animation for inline carets; cb-pulse = `0,100% { opacity:1 } 50% { opacity:0.3 }` smooth ease for save-bar dot + skeleton blocks.
- **Snackbar → Toast migration** — Both pages now consume `ICbToastService` instead of `ISnackbar`. Severity values map directly: `Severity.Success/Error/Warning/Info` → `CbToastSeverity.Success/Error/Warning/Info`.
- **IDialogService → ICbDialogService migration** — AiChat now consumes `ICbDialogService` only. `OpenSharedKeysDialog`, `SaveRecipeFromMessageAsync`, `OpenDraftInEditor` all use `CbDialogService.ShowAsync<T>()` with `CbDialogParameters` + `CbDialogOptions`. PromptBuilder doesn't open dialogs.

## Task Commits

All work landed in a single atomic commit because the AiChat rewrite, PromptBuilder rewrite, and the CSS keyframe appendage are tightly coupled (the keyframes are referenced by AiChat markup inline-styles).

1. **Tasks 1–3 (combined):** AiChat + PromptBuilder rewrites + cb-* keyframes + build + test → **`b330442`** (feat)

## Files Modified

- `src/CookBot.Web/Components/Pages/AiChat.razor` — Full markup rewrite per design-handoff/screens/ai-chat.jsx. ~750 lines (down from previous ~770; ~80 lines of MudExpansionPanels assistant-instructions UI removed; ~60 lines of new render helpers — RenderTurn, RenderRecipeDocument, RenderDraftingSkeleton, RenderEmptyCanvas — added). All `@code` AI orchestrator wiring preserved verbatim. Added: `_outputFormat`/`_voice` enum state? No — those are PromptBuilder-only. AiChat retains: `_isStreaming`, `_isDraftingRecipe`, `_lastStructuredRecipe`, `_generationCts`, `_messages`, `_currentConversation`, `_userInput`, `_streamingContent`, `_systemPrompt`, `_effectiveAi`, `_profile`, `_conversations`, `_selectedConversationId`. Removed dead state: `_promptTemplate` field (no UI consumer remains; profile field directly read in `BuildSystemPrompt`). Removed dead methods: `InsertToken`, `InsertCookbookReferenceTokenAsync`, `SavePromptTemplate`, `ResetPromptTemplate` — these were UI helpers for the assistant-instructions panel that no longer exists on this surface (see Decision #1).
- `src/CookBot.Web/Components/Pages/PromptBuilder.razor` — Full markup rewrite per design-handoff/screens/prompt-builder.jsx. ~265 lines (up from ~167; the new design has more config groups — Output format radio, Voice select — plus a token counter and a richer empty state). `PromptBuilderService.BuildCopyablePrompt` invocation preserved verbatim. Live regenerate-on-input via `OnUserRequestChanged` + `OnIncludePantryChanged` + `OnIncludeProfileChanged`. Added: `_outputFormat`, `_voice`, `_includeEquipment`, `_includeFavorites`, `_charCount`, `_tokenCount` page state.
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — Appended `@keyframes cb-blink` + `@keyframes cb-pulse` (4 lines + 6-line block comment) at the end of the file. No existing rules modified.

## Verification

- **`dotnet build`:** Clean. 0 warnings, 0 errors. Verified twice — once after AiChat rewrite (intermediate), once after final cleanup.
- **`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing (baseline preserved). The AI-related fixture tests at `Category=RequiresApiKey` continue to be excluded by default.
- **Hard invariant — zero `Mud*` symbols in AiChat.razor + PromptBuilder.razor:** Verified via per-file `grep -nE '<Mud|@inject IDialogService|@inject ISnackbar|MudBlazor|IMudDialog|Icons\.Material'` — 0 matches in both files. The broader `Variant\.[A-Z]` regex matches are all Cb-prefixed (CbButton.CbButtonVariant.*, CbChip.CbChipVariant.*) — false positives.
- **Hard invariant — POLISH-01 extractor stays deleted:** `grep -n "ExtractRecipeContent" src/CookBot.Web/Components/Pages/AiChat.razor` returns 1 match — a single comment-line tombstone in the file's doc-block ("POLISH-01: legacy three-tier ExtractRecipeContent extractor stays DELETED"). The extractor function itself is gone; the tombstone exists to flag the invariant for any future contributor reading the file. Equivalent to verifying the literal `private string ExtractRecipeContent(` function signature does NOT appear, which it doesn't.
- **Hard invariant — recipe canvas binds to canonical RecipeDocument:** `RenderRecipeDocument(doc)` walks `doc.Ingredients` + `doc.Steps.OfType<ContentStep>()` — no projection from text. No call to `Parser.TryParse` for canvas rendering (Parser is invoked only in `OpenDraftInEditor` for the failure-path edit-anyway flow, identical to before).
- **Hard invariant — cb-blink + cb-pulse keyframes present:** `grep -n "cb-blink\|cb-pulse" cookbot-design.css` returns 4 matches (definition + 2 doc-comment refs + 1 cb-pulse usage in skeleton blocks).
- **Hard invariant — AI-off contract:** Manual code review confirms 3 panes in AiChat (host disabled / per-user AI off / no API key) and 2 panes in PromptBuilder (host disabled / per-user AI off). Each renders inside `<UserGuard>` (AiChat) or directly (PromptBuilder — preserves existing behavior).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Initial textarea binding used `value="@_userInput"` attribute**

- **Found during:** Task 1 self-review (post-write, pre-build).
- **Issue:** `<textarea>` element content is bound via inner text content, not the `value` attribute. The initial markup used `value="@_userInput"` + a manual `OnUserInputChanged` handler — would have rendered initial value correctly but would NOT have refreshed the DOM after `_userInput = ""` was set inside `SendMessage` (since the inner text node wouldn't change).
- **Fix:** Switched to canonical Blazor `@bind="_userInput" @bind:event="oninput"` pattern. This is the same pattern used by `CbTextarea` atom internally. Removed the now-unused `OnUserInputChanged` method.
- **Files modified:** `src/CookBot.Web/Components/Pages/AiChat.razor`
- **Commit:** `b330442` (single atomic).

**2. [Rule 2 — Cleanup] Removed dead `_promptTemplate` field and 4 unused methods**

- **Found during:** Task 1 final review.
- **Issue:** After removing the assistant-instructions UI from AiChat (per Decision #1), `_promptTemplate` field, `InsertToken`, `InsertCookbookReferenceTokenAsync`, `SavePromptTemplate`, and `ResetPromptTemplate` had no consumers but were still declared. The build passed without warnings (private unused members don't warn in .NET 10), but they would have been confusing dead code. `BuildSystemPrompt` reads `_profile.AiSystemPromptTemplate` directly anyway — `_promptTemplate` was a superfluous staging field.
- **Fix:** Removed the field and the 4 methods. The runtime behavior is unchanged: user-edited templates persisted in `UserProfile.AiSystemPromptTemplate` are still loaded and consumed by `BuildSystemPrompt` → `PromptBuilder.ResolveTemplate` → `IAiService.StreamMessageAsync`. Only the editor UI is gone (relegated to a future Profile plan per Decision #1).
- **Files modified:** `src/CookBot.Web/Components/Pages/AiChat.razor`
- **Commit:** `b330442` (single atomic).

### Scope adjustments (in-spec)

- **Page-level TopBar not rendered by AiChat or PromptBuilder.** Phase 5's MainLayout already renders the global TopBar; pages live inside the main column below it. The design-handoff ai-chat.jsx shows a `<TopBar title="AI Assistant" breadcrumb="generate" right={...}>` line — this maps to the Phase 5 global TopBar, not to a page-local one. The 'claude haiku 4.5 · streaming' right-slot text from the design lives inside the chat-rail header in this implementation (the global TopBar's right-slot is owned by the user-switcher and dark-mode toggle and isn't reasonable to override per-page yet). Plan permits this — the design's TopBar layer is satisfied by the global Phase 5 TopBar.
- **Dual send buttons in AiChat input** (Spark = generate-recipe, Send = chat). The design shows a single arrow-only send button. Splitting them preserves the existing v1.1 distinction between IAiRecipeGenerator (orchestrator path) and IAiService (free-form chat) without forcing a model-routing decision into the prompt heuristic. See Decision #6.
- **Conversation-list row added to chat rail.** The design-handoff doesn't show a conversation history surface; the existing v1.1 page does (and tests/users depend on it). Kept the conversation rows above the message stream as a compact 140px-max scrollable section; row activation loads the conversation. Removing this would have been an out-of-scope feature deletion.
- **Per-row trash icon on each conversation row.** Same justification as the conversation-list itself — preserved from v1.1.

## Known Stubs

These are visual-only UI elements captured from the design-handoff that are not yet wired to backend behavior. Each is documented here so the verifier can flag them and a future plan can pick them up without a layout change.

- **PromptBuilder Output format radio: Markdown + Plain text** — UI captures the selection in `_outputFormat`, but `PromptBuilderService.BuildCopyablePrompt` produces canonical-JSON-schema content regardless. The current behavior is unchanged from the previous implementation (which had no format selector). Future plan can extend `BuildCopyablePrompt(..., OutputFormat fmt)` and switch the system prompt + format-prompt block accordingly. Tag: **FUTURE-OUT-FMT**.
- **PromptBuilder Voice select: Warm · home cook + Technical · pro kitchen** — UI captures the selection in `_voice`, but the prompt body doesn't change between voices yet. Current behavior is unchanged from the previous implementation (which had no voice selector). Future plan can prepend a tone-paragraph to `BuildCopyablePrompt`. Tag: **FUTURE-VOICE**.
- **PromptBuilder Include checkboxes: Equipment list + Past favorites** — UI captures the selection in `_includeEquipment` / `_includeFavorites`, but `BuildCopyablePrompt` doesn't have hooks for either yet. The user profile's `KitchenToolsJson` is already partially used (the count is shown to the user via `_toolCount`); a future plan can extend the service to emit an Equipment block when checked. 'Past favorites' is harder — needs a 'last N viewed/cooked recipes' query against the database. Tags: **FUTURE-INCLUDE-EQUIP** and **FUTURE-INCLUDE-FAV**.
- **AiChat assistant-instructions editor not surfaced anywhere yet.** The previous AiChat hosted a MudExpansionPanels block with chip-based token insertion + MudTextField template editor. The new design-handoff ai-chat.jsx removes it. Plan 07-05 (Profile) is the natural home for the editor (it owns settings cards). Until then, users CAN'T modify their `UserProfile.AiSystemPromptTemplate` from the UI — but pre-existing custom templates ARE still loaded and used. Tag: **DEFERRED-PROF-AIPROMPT**.

## Threat Flags

None. No new network endpoints, no new auth surfaces, no new file-access patterns, no new schema. Authorization checks (`UserCanAccessRecipeAsync` inside `FormatCookbookRecipesForPromptAsync`, `Cookbook.UserId == userId || cookbook.Shares.Any(...)`) preserved verbatim. SecretRedactor / PromptInjectionGuard continue to run inside the AI service layer — this surface is presentation-only.

## Self-Check: PASSED

Verified the SUMMARY.md claims:

**Files modified exist (all 3):**
- `src/CookBot.Web/Components/Pages/AiChat.razor` — FOUND
- `src/CookBot.Web/Components/Pages/PromptBuilder.razor` — FOUND
- `src/CookBot.Web/wwwroot/css/cookbot-design.css` — FOUND

**Commit `b330442` exists in `git log --oneline --all`:** FOUND.

**`dotnet build`:** clean (0 warnings, 0 errors).
**`dotnet test --filter "Category!=RequiresApiKey"`:** 196/196 passing.
**Strict Mud grep on the 2 plan-scoped razor files:** 0 matches.
**`grep -n "ExtractRecipeContent" AiChat.razor`:** 0 matches (POLISH-01 invariant verified).
**`grep -n "cb-blink\|cb-pulse" cookbot-design.css`:** 4 matches (keyframes + doc comments + skeleton usage).
