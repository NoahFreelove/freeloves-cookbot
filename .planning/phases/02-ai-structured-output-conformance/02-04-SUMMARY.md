---
phase: 02-ai-structured-output-conformance
plan: 04
subsystem: ai
tags: [ai, blazor, cookbook-import, upcaster, migration, markdig, polish, requirements-doc]

# Dependency graph
requires:
  - phase: 02-ai-structured-output-conformance
    plan: 01
    provides: "PromptInjectionGuard (NOT called from this plan; AI-08-AUDIT mitigation is the Markdig pipeline lockdown — separate surface from the wrap)"
  - phase: 02-ai-structured-output-conformance
    plan: 02
    provides: "StructuredResult<T> envelope shape (Ok / Value / RawResponse / Validation / SanitizedError) consumed by AiChat.razor's _lastStructuredRecipe field"
  - phase: 02-ai-structured-output-conformance
    plan: 03
    provides: "IAiRecipeGenerator.GenerateAsync (called from AiChat.GenerateRecipeAsync); AiConversation.FormatVersion column (read at conversation-load time, written at SaveConversation)"
  - phase: 01-canonical-recipe-format
    provides: "RecipeUpcasterChain (constructor-injected into CookbookTransferService); RecipeValidator (per-recipe validate); RecipeFormatParser (D-08 parser-route + MIGRATION-06 verification target); JsonRecipeSerializer.SerializeIndented (typed-doc → JSON for SaveRecipeDialog handoff)"
provides:
  - "AiChat.razor wired end-to-end to IAiRecipeGenerator — POLISH-01 (ExtractRecipeContent + HasRecipe deleted), AI-02/AI-03 wiring (Generate Recipe button → orchestrator → Save), AI-08-AUDIT (Markdig DisableHtml() pipeline), POLISH-06 (FormatVersion stamping + resume note), UI-SPEC Surfaces 1-4 (Drafting indicator, Save gate, Edit-and-save-anyway, sanitized snackbar mapper)"
  - "CookbookTransferService.Deserialize as instance method routing per-recipe through upcaster + validator — MIGRATION-04 (v1 envelopes upcast cleanly; v2 envelopes accepted; mixed cookbooks return partial-success errors; malformed/unsupported envelopes return null with descriptive error)"
  - "RecipeFormatParserVersionStampingTests — MIGRATION-06 verification (legacy YAML without `version:` is stamped to 1 BEFORE upcaster runs, so V1→V2 RenameKey reconciliation fires; v2 YAML round-trips no-op)"
  - "AI-09 formally moved to FUTURE-12 in REQUIREMENTS.md — traceability table + phase-summary count + top-of-file count adjusted; CONTEXT.md `<canonical_refs>` corrected to reference IStructuredAiService (Plan 02 layering deviation)"
affects: [02-05-verify-phase, future-phase-3-chip-editor, future-phase-4-polish-cleanup]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Markdig pipeline lockdown via static readonly field — MarkdownPipelineBuilder().DisableHtml().Build() injected as the second arg to Markdown.ToHtml; reusable for any other Razor surface that renders untrusted markdown"
    - "Static-to-instance service refactor preserving caller surface — CookbookTransferService.Deserialize becomes instance method; ImportCookbookDialog already injected the service so call-site change is mechanical"
    - "Per-recipe upcast+validate loop with index/name-prefixed error collection — partial-success envelope returned even when some recipes fail; caller decides what to import"
    - "Operate on raw JsonNode for upcasting (not the round-tripped DTO) — the v1-shaped DTO would silently drop v2 fields (id/kind/heading) on a v2 envelope; raw-node path preserves whatever shape was sent"
    - "_lastStructuredRecipe state field gating Save button visibility — `msg == lastMessage && _lastStructuredRecipe?.Ok == true` gates the Save button on the most-recent assistant turn AND a successful structured-output result; free-form turns and pre-Phase-2 turns produce null and never show the button (D-24/D-25)"
    - "AssembleMessagesForAiCall — transient resume system note inserted at request-assembly time when FormatVersion < 2; note is NOT persisted to MessagesJson (one-shot per-conversation per D-23)"
    - "MapToSanitizedSnackbarCopy — pure UX-copy mapping function from sanitized error string to one of 4 prescriptive copy templates (UI-SPEC Surface 4); raw exception messages NEVER reach Snackbar.Add"

key-files:
  created:
    - "tests/CookBot.Tests/Migration/CookbookUpcastImportTests.cs"
    - "tests/CookBot.Tests/AI/RecipeFormatParserVersionStampingTests.cs"
  modified:
    - "src/CookBot.Web/Components/Pages/AiChat.razor (392-line rewrite — 293 insertions / 99 deletions; deleted ExtractRecipeContent + HasRecipe; added Markdig pipeline lockdown, IAiRecipeGenerator wiring, FormatVersion stamping, resume-note assembly, sanitized snackbar mapper, Drafting indicator + Edit-and-save-anyway UI surfaces)"
    - "src/CookBot.Web/Services/CookbookTransferService.cs (static → instance; constructor takes RecipeUpcasterChain + RecipeValidator; per-recipe loop stamps version, upcasts, deserializes, validates; SchemaVersion 1 or 2 accepted)"
    - "src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor (TransferService.Deserialize instance call; partial-success snackbar surface)"
    - "tests/CookBot.Tests/CookBot.Tests.csproj (adds project reference to src/CookBot.Web — first test-time reference to the Web project, needed to exercise CookbookTransferService directly)"
    - ".planning/REQUIREMENTS.md (AI-09 active line removed; FUTURE-12 added with rationale; traceability row updated; phase summary adjusted; top-of-file count: 46 → 45 net)"
    - ".planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md (`<canonical_refs>` corrected — Domain/Interfaces/IAiService.cs removed from `modifies`; IStructuredAiService.cs added to `creates` with Plan 02 deviation rationale)"

key-decisions:
  - "Operate on raw JsonNode from input string (not the round-tripped DTO) for per-recipe upcast — the existing CookbookTransferRecipe DTO models the v1 wire shape (localId / isSection); using SerializeToNode(recipeDto) on a v2 envelope would silently drop v2-only fields (id / kind / heading) before the upcaster ever saw them. This is a Rule 1 fix (correctness) discovered while reading the DTO during Task 1; the plan text suggested SerializeToNode of the DTO but the raw-array path is the only correct one for v2-future input."
  - "AiChat.razor: keep BOTH Send (free-form chat via IAiService.StreamMessageAsync) AND Generate Recipe (structured-output via IAiRecipeGenerator) buttons rather than collapsing into a single intent-detection submit handler. Plan text noted this as executor's discretion (`<action> §14`); the explicit button surface matches UI-SPEC Surface 2 invariant ('Free-form chat turns never show the Save button') without heuristic intent detection. SendMessage clears _lastStructuredRecipe so a free-form turn after a recipe turn correctly hides the Save button."
  - "SaveRecipeDialog handoff via JsonRecipeSerializer.SerializeIndented(_lastStructuredRecipe.Value), not a new RecipeService.CreateFromCanonicalAsync method. Plan text mentioned that latter as preferred-if-exists; the existing path keeps blast radius minimal — the typed RecipeDocument flows into the dialog as canonical JSON, the dialog re-parses via IRecipeFormatParser (Phase 1 RecipeFormatParser handles raw-JSON input via the non-frontmatter branch), and the existing persistence pipeline runs unchanged. Phase 1 invariant (non-conforming recipes never persist) still holds because RecipeValidator runs again in the dialog's parse step."
  - "OpenDraftInEditor (D-08/D-09 fallback) uses Snackbar.Add('Copy from chat and paste into editor') rather than navigating to a new route or opening PasteRawTextDialog with prefilled text. Plan text described both routes as discretionary; the snackbar matches the existing project UX (no other Razor surface auto-navigates on parse failure) and avoids introducing a new dialog parameter contract. The `_lastStructuredRecipe.RawResponse` is already visible in the chat as the assistant's JSON code block, so the user has a workable path."
  - "@using Markdig added to the AiChat.razor top-of-file `@using` block — needed because MarkdownExtensions.DisableHtml() is an extension method in the Markdig namespace (not a method on MarkdownPipelineBuilder itself). Without the using, the static field initializer fails to compile. This was a small Rule 3 (blocking) fix during the Task 3 first build."

requirements-completed: [AI-08, MIGRATION-04, MIGRATION-06, POLISH-01, POLISH-06]
# AI-09 was in the plan's `requirements:` field but is moved to FUTURE-12 (dropped from v1 scope).
# AI-08 and POLISH-06 were already marked complete by Plan 02-03 (orchestrator + AI-08 directive,
# FormatVersion column); this plan completes the consumer-side wiring that satisfies
# AI-08-AUDIT and POLISH-06's resume-system-note + FormatVersion stamping behavior.

# Metrics
duration: 13min
completed: 2026-04-26
---

# Phase 02 Plan 04: AiChat IAiRecipeGenerator Wiring + Cookbook Upcaster Routing + AI-09 Reframing Summary

**End-to-end Phase 2 user-facing landing: `/ai` recipe generation now flows IAiRecipeGenerator → typed RecipeDocument → gated Save button; AI-08-AUDIT closes the Markdig raw-HTML exfil surface; CookbookTransferService.Deserialize routes legacy v1 envelopes through the upcaster chain; AI-09 formally reframed as FUTURE-12 with AI-08 + AI-08-AUDIT as the load-bearing trusted-LAN mitigations.**

## Performance

- **Duration:** ~13 min
- **Started:** 2026-04-26T05:48:00Z (b2fc4e2)
- **Completed:** 2026-04-26T06:00:50Z (aaca339)
- **Tasks:** 4 (3 TDD: Tasks 1, 2, 3 — Task 4 is pure-doc, no test gate)
- **Files created:** 2 (test files only)
- **Files modified:** 6

## Accomplishments

- **POLISH-01 / AI-02 / AI-03 wiring landed.** `AiChat.razor.ExtractRecipeContent` (47 lines, the three-tier extractor) and `HasRecipe` are deleted. The "Generate Recipe" button now drives `GenerateRecipeAsync` → `IAiRecipeGenerator.GenerateAsync` → typed `StructuredResult<RecipeDocument>` stored in `_lastStructuredRecipe`. The Save button gates on `msg == lastMessage && _lastStructuredRecipe?.Ok == true` (D-24 / D-25 — free-form turns and pre-Phase-2 turns never show the button).
- **AI-08-AUDIT mitigation in place.** `AssistantContentPipeline = new MarkdownPipelineBuilder().DisableHtml().Build()` is the static readonly field used by `RenderContent`. Raw HTML (`<img src='https://attacker/log'>`, `<a href='javascript:...'>`) injected by a prompt-compromised assistant cannot reach the DOM. Markdown image syntax (`![](url)`) still renders, but Markdig itself controls the `<img>` tag — the open question (out of scope per CONTEXT.md `<specifics>`) is whether to block external image hosts via a custom LinkInlineRenderer; FUTURE-12 expansion would carry that.
- **POLISH-06 stamping + resume-note logic shipped.** `SaveConversation` always sets `_currentConversation.FormatVersion = 2` before SaveChangesAsync; `AssembleMessagesForAiCall` prepends a transient user-role note ("Note: this conversation's earlier assistant outputs may reference an older recipe format. Emit any new recipes in the current structured format only.") when `_currentConversation.FormatVersion < 2`. The note is NOT persisted to MessagesJson — once SaveConversation re-stamps to 2, it disappears on subsequent calls (D-23 one-shot per conversation).
- **UI-SPEC Surfaces 1, 3, 4 wired.** Surface 1: "Drafting recipe…" MudPaper with indeterminate progress while `_isDraftingRecipe == true`. Surface 3: Edit-and-save-anyway + Try again bubble when `_lastStructuredRecipe is { Ok: false, Validation: not null }`. Surface 4: `MapToSanitizedSnackbarCopy(sanitizedError)` maps to one of 4 prescriptive copy templates ("Could not connect to the AI", "Could not reach the AI", "The AI declined this request", "Something went wrong"); raw `ex.Message` NEVER reaches `Snackbar.Add` (verified: `! grep -E 'Snackbar\.Add\(\$"AI Error: \{ex\.Message\}"'`).
- **MIGRATION-04: CookbookTransferService.Deserialize is now an instance method.** Constructor takes `RecipeUpcasterChain` + `RecipeValidator` (already in Application DI from Phase 1). Per-recipe: parses raw `JsonNode` from input, stamps `version` from envelope.SchemaVersion if absent, calls `_upcasterChain.UpcastToCurrent(node)`, deserializes to `RecipeDocument`, runs `_validator.Validate(doc)`. Per-recipe errors carry "Recipe #N (Name)" prefix and are collected into the `errors` out param; the envelope is returned even on partial-success so the dialog can show warnings. SchemaVersion ∈ {1, 2}; v3+ returns null with descriptive error.
- **Critical correctness fix during Task 1 (Rule 1).** The plan text suggested `JsonSerializer.SerializeToNode(recipeDto)` for the per-recipe upcast input, but the existing `CookbookTransferRecipe` DTO models the v1 wire shape (`localId`, `isSection`) — using it on a v2 envelope would silently drop v2-only fields (`id`, `kind`, `heading`) BEFORE the upcaster ever saw them. The fix: parse the input string to a `JsonNode root` and pull recipes from `root["recipes"] as JsonArray`, deep-cloning each one for upcast. This preserves whatever shape was sent and is the only correct path for forward-compat v2 imports.
- **MIGRATION-06 verified (no source change).** `RecipeFormatParser.cs:103-106` already implements Phase 1's H1 mitigation (stamp `version=1` if absent before upcasting). Two `RecipeFormatParserVersionStampingTests` lock the behavior: legacy YAML without `version:` parses with `PrepTimeMinutes != null` and equal to the legacy `prepTime` value (proving both stamping AND V1→V2 RenameKey fired); v2 YAML round-trips no-op.
- **AI-09 → FUTURE-12 documentation landing.** REQUIREMENTS.md AI-09 active line removed; FUTURE-12 entry added in deferred section citing AI-08 (XML-tag wrapping) + AI-08-AUDIT (Markdig DisableHtml) as the load-bearing trusted-LAN mitigations; traceability row updated to `AI-09 | dropped → FUTURE-12`; phase-2 summary updated to "9 requirements net (… ; AI-09 reframed as FUTURE-12)"; top-of-file count adjusted from 46 to 45 net. CONTEXT.md `<canonical_refs>` corrected: `Domain/Interfaces/IAiService.cs` line removed from `<modifies>` (Plan 02 deviated — Domain interface unchanged); `Application/AI/IStructuredAiService.cs` added to `<creates>` with the Plan 02 layering rationale.
- **Build clean, full suite green.** 0 warnings, 0 errors. 153/153 tests pass (146 prior + 5 cookbook-upcast + 2 parser-stamping = 153). No new NuGet packages introduced.

## Task Commits

Each task followed the planned discipline (TDD where applicable; pure-doc for Task 4):

1. **Task 1 RED: CookbookUpcastImportTests** — `b2fc4e2` (test) — 5 new xUnit Facts cover v1 envelope upcast, v2 already-canonical, mixed-cookbook partial-success, malformed JSON, unsupported schema version. Adds `tests/CookBot.Tests → src/CookBot.Web` project reference (first test-time reference to Web). Compiles into expected RED state: CS1739 (no upcasterChain ctor param) + 5 × CS0176 (Deserialize is still static).
2. **Task 1 GREEN: Deserialize instance refactor** — `0fabdb3` (feat) — Static→instance; `RecipeUpcasterChain` + `RecipeValidator` constructor params; per-recipe loop on raw `JsonNode` from input (correctness fix vs. plan-text DTO path); SchemaVersion ∈ {1, 2}; per-recipe errors with index/name prefix; ImportCookbookDialog switches to instance call + partial-success snackbar. All 5 new tests pass; full suite 151/151.
3. **Task 2: RecipeFormatParserVersionStampingTests (verification only)** — `e9fb0ef` (test) — 2 new xUnit Facts verify legacy YAML stamps to v=1 before the upcaster, and v2 YAML round-trips no-op. No source change required — Phase 1 D-10 / H1 mitigation already in place at `RecipeFormatParser.cs:103-106`. Suite 153/153.
4. **Task 3 GREEN: AiChat.razor major rewrite** — `23a8515` (feat) — 392-line diff (293 insertions / 99 deletions). Deletes ExtractRecipeContent + HasRecipe (POLISH-01); adds AssistantContentPipeline = MarkdownPipelineBuilder().DisableHtml().Build() (AI-08-AUDIT); adds @using Markdig + @inject CookBot.Application.AI.IAiRecipeGenerator AiRecipeGenerator; adds GenerateRecipeAsync orchestrator-driver + GenerateRecipeFromInput button handler; adds MapToSanitizedSnackbarCopy + SaveRecipeFromMessageAsync + OpenDraftInEditor + RetryRecipeGeneration + AssembleMessagesForAiCall; SaveConversation stamps FormatVersion = 2 (D-22); UI-SPEC Surfaces 1, 3 wired in markup. Build clean; 153/153.
5. **Task 4: AI-09 → FUTURE-12 + CONTEXT.md correction (docs only)** — `aaca339` (docs) — REQUIREMENTS.md three coordinated edits (active-list removal + deferred entry + traceability row + phase summary count + top-of-file count); CONTEXT.md `<canonical_refs>` correction (remove stale Domain/Interfaces/IAiService.cs line, add IStructuredAiService.cs creates entry). Pure documentation; no code changes; build clean; 153/153.

**Plan metadata commit:** added by `/gsd-execute-phase` after this SUMMARY (includes STATE.md, ROADMAP.md, REQUIREMENTS.md updates).

## Files Created/Modified

- **NEW** `tests/CookBot.Tests/Migration/CookbookUpcastImportTests.cs` — 5 xUnit Facts covering v1 envelope, v2 already-canonical, mixed-cookbook partial-success, malformed JSON, unsupported schema version. Uses `MakeService()` helper that constructs CookbookTransferService with `db: null!, cookbookService: null!, recipeService: null!` because Deserialize doesn't touch those members.
- **NEW** `tests/CookBot.Tests/AI/RecipeFormatParserVersionStampingTests.cs` — 2 xUnit Facts: legacy YAML (no `version:` key) → `PrepTimeMinutes != null` and equal to legacy `prepTime` value (proves stamping + V1→V2 RenameKey fired); v2 YAML round-trips no-op.
- **MOD** `src/CookBot.Web/Components/Pages/AiChat.razor` — 392-line rewrite. See Task 3 commit for the full delta. Highlights: `@inject CookBot.Application.AI.IAiRecipeGenerator AiRecipeGenerator`; `private static readonly Markdig.MarkdownPipeline AssistantContentPipeline = new Markdig.MarkdownPipelineBuilder().DisableHtml().Build()`; `RenderContent` uses 2-arg `Markdown.ToHtml(content, AssistantContentPipeline)`; `_isDraftingRecipe` + `_lastStructuredRecipe` + `_generationCts` state fields; `GenerateRecipeAsync(string)` orchestrator-driver; `MapToSanitizedSnackbarCopy(string?)` mapper; `SaveRecipeFromMessageAsync` reads `_lastStructuredRecipe.Value` directly; `AssembleMessagesForAiCall` prepends resume note when FormatVersion < 2; `SaveConversation` stamps FormatVersion = 2.
- **MOD** `src/CookBot.Web/Services/CookbookTransferService.cs` — static→instance Deserialize. Constructor adds `RecipeUpcasterChain upcasterChain, RecipeValidator validator` params. Per-recipe upcast loop operates on raw `JsonNode` from input (not DTO round-trip).
- **MOD** `src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor` — `TransferService.Deserialize(json, out var errors)` instance call; partial-success snackbar surface (`Severity.Warning` when `errors.Count > 0` and envelope is non-null).
- **MOD** `tests/CookBot.Tests/CookBot.Tests.csproj` — adds `<ProjectReference Include="..\..\src\CookBot.Web\CookBot.Web.csproj" />` (first test-time reference to Web project).
- **MOD** `.planning/REQUIREMENTS.md` — AI-09 active line removed; HTML comment placeholder + FUTURE-12 deferred entry added; traceability row updated; phase summary count adjusted (10 → 9 net); top-of-file coverage adjusted (46 → 45 net).
- **MOD** `.planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md` — `<canonical_refs>` "Source files this phase modifies" stripped of `Domain/Interfaces/IAiService.cs`; `Application/AI/IStructuredAiService.cs` added to "Source files this phase creates" with Plan 02 deviation rationale and a forward reference to 02-02-SUMMARY.md.

## Build & Test Output

```
$ dotnet build FreelovesCookBot.sln -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test FreelovesCookBot.sln --no-build -c Debug
Passed!  - Failed:     0, Passed:   153, Skipped:     0, Total:   153, Duration: 1 s

$ dotnet test --filter "FullyQualifiedName~CookbookUpcastImportTests"
Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 63 ms

$ dotnet test --filter "FullyQualifiedName~RecipeFormatParserVersionStampingTests"
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 90 ms
```

## Threat Model Mitigations Honored

| Threat ID | Category | Status | Verification |
|-----------|----------|--------|--------------|
| T-02P04-01 | Information Disclosure (raw `<img>`/`<a>` exfil via assistant content) | mitigated | `grep -q "DisableHtml" src/CookBot.Web/Components/Pages/AiChat.razor` passes (3 hits — field initializer + 2 doc-comment refs); `grep -q "ToHtml(content, AssistantContentPipeline)" src/CookBot.Web/Components/Pages/AiChat.razor` passes; raw HTML is stripped at the Markdig pipeline level before reaching `MarkupString`. Replaces the dropped AI-09 banner as the realistic exfil mitigation on the trusted-LAN posture. |
| T-02P04-02 | Tampering (v3+ schemaVersion bypassing v2 validation) | mitigated | `Deserialize_UnsupportedSchemaVersion_ReturnsNullWithError` passes; envelope.SchemaVersion ∈ {1, 2} gate at line 165 of CookbookTransferService.cs returns null with "Unsupported schema version" error. The upcaster chain itself throws on `version > Current = 2` (Phase 1 D-09); per-recipe try/catch catches it as a sanitized per-recipe error. |
| T-02P04-03 | Tampering (mixed cookbook with one bad recipe poisoning the import) | mitigated | `Deserialize_MixedCookbook_PartialSuccess` passes; per-recipe error collection with `Recipe #N (Name)` prefix; envelope returned with all original recipes (caller decides what to import). ImportCookbookDialog shows `Severity.Warning` snackbar for partial-success. |
| T-02P04-04 | Information Disclosure (raw ex.Message bypassing SecretRedactor) | mitigated | `grep -q "MapToSanitizedSnackbarCopy" src/CookBot.Web/Components/Pages/AiChat.razor` passes (4 hits — definition + 3 call sites); `! grep -E 'Snackbar\.Add\(\$"AI Error: \{ex\.Message\}"'` passes (the old raw-binding pattern is gone). All 3 Snackbar.Add error paths route through MapToSanitizedSnackbarCopy. |
| T-02P04-05 | Spoofing (pre-Phase-2 turn pretending to be a structured recipe) | mitigated | `grep -q "_lastStructuredRecipe?.Ok == true" src/CookBot.Web/Components/Pages/AiChat.razor` passes; Save button gate is `msg == lastMessage && _lastStructuredRecipe?.Ok == true` — pre-Phase-2 turns never set _lastStructuredRecipe (it's cleared on conversation load and on free-form chat turn), so the button stays hidden. |
| T-02P04-06 | Tampering (YAML paste-in lacking version field treated as v2) | mitigated | `RecipeFormatParserVersionStampingTests.TryParse_YamlWithoutVersion_*` passes; legacy YAML's `prepTime` correctly maps to canonical `PrepTimeMinutes`, which can only happen if version-stamping + V1→V2 upcasting both fired (Phase 1 H1 mitigation at RecipeFormatParser.cs:103-106). |
| T-02P04-07 | Repudiation (AI-09 silent removal without traceability) | mitigated | `grep -q "FUTURE-12" .planning/REQUIREMENTS.md` passes (6 hits — deferred entry + traceability + phase summary + top-of-file count + 2 commit/SUMMARY refs); `! grep -E '^- \[ \] \*\*AI-09\*\*' .planning/REQUIREMENTS.md` passes (active line removed); traceability table reflects `AI-09 | dropped → FUTURE-12`. |

## Layering Verification

- `! grep -q "ExtractRecipeContent" src/CookBot.Web/Components/Pages/AiChat.razor` passes (deletion)
- `! grep -q "private bool HasRecipe" src/CookBot.Web/Components/Pages/AiChat.razor` passes (deletion)
- `! grep -E "public static .* Deserialize" src/CookBot.Web/Services/CookbookTransferService.cs` passes (instance method)
- `! grep -q "CookbookTransferService\.Deserialize" src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor` passes (instance call)
- `git diff` of `*.csproj` shows only one PackageReference change: the test csproj added a project reference (not a NuGet package); `git diff -- '*.csproj' | grep -c '<PackageReference '` returns 0
- `grep -q "DisableHtml" src/CookBot.Web/Components/Pages/AiChat.razor` passes
- `grep -q "AiRecipeGenerator.GenerateAsync" src/CookBot.Web/Components/Pages/AiChat.razor` passes
- `grep -q "FormatVersion = 2" src/CookBot.Web/Components/Pages/AiChat.razor` passes (2 hits — actual stamp + comment)
- `grep -q "FormatVersion < 2" src/CookBot.Web/Components/Pages/AiChat.razor` passes (2 hits — code + comment)
- `grep -q "IStructuredAiService" .planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md` passes
- `! grep -E "Domain/Interfaces/IAiService\.cs.*adds.*SendStructuredAsync" .planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md` passes

## Decisions Made

- **Raw JsonNode array path for per-recipe upcast** — the plan-text path (SerializeToNode of the v1-shaped DTO) would silently drop v2-only fields (id / kind / heading) on a v2 envelope. The implementation pulls recipes from `root["recipes"] as JsonArray` and deep-clones each one. This is the only correct path for forward-compat v2 imports.
- **Two explicit submit buttons (Send + Generate Recipe) instead of intent detection** — the plan noted this as executor's discretion. The explicit-button surface is preferred because it matches UI-SPEC Surface 2's invariant (free-form chat turns never show the Save button) without heuristic intent detection. SendMessage clears _lastStructuredRecipe so a free-form turn after a recipe turn correctly hides the Save button.
- **SaveRecipeDialog handoff via JsonRecipeSerializer.SerializeIndented + existing dialog** — instead of a new `RecipeService.CreateFromCanonicalAsync` method. Smallest blast radius: typed RecipeDocument → canonical JSON → existing dialog → existing parser → existing persistence. Phase 1 invariant (non-conforming recipes never persist) holds because RecipeValidator runs again in the dialog's parse step.
- **OpenDraftInEditor uses Snackbar fallback (D-09)** — when `Parser.TryParse` succeeds for the raw response, it opens SaveRecipeDialog; when parsing fails, it shows a `Severity.Info` snackbar suggesting copy/paste from chat. The plan-text alternative (PasteRawTextDialog with prefilled text) would have introduced a new dialog parameter contract; the snackbar matches existing project UX.
- **`@using Markdig` added to top of AiChat.razor** — needed because `MarkdownExtensions.DisableHtml()` is an extension method in the Markdig namespace, not on the type. Without the using directive, the static field initializer fails to compile.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] Plan-text DTO-round-trip path would silently drop v2 fields on per-recipe upcast**
- **Found during:** Task 1 (read-first of CookbookTransferRecipe DTO and Plan 02-04 §interfaces)
- **Issue:** The plan's Task 1 §action body suggested `var node = JsonSerializer.SerializeToNode(recipeDto, JsonOptions);` for the per-recipe upcast input. But `CookbookTransferRecipe` (and its nested `Ingredient` / `Step` types) models the v1 wire shape — `localId`, `isSection: bool`, no `id` / no `kind` / no `heading`. Deserializing a v2 envelope into the DTO would silently drop those v2 fields BEFORE the upcaster (which is a no-op on v2 anyway) ever saw them. v2-shape inputs would round-trip with data loss.
- **Fix:** Parse the input string twice — once as `CookbookTransferDocument envelope` (for envelope metadata + the existing DTO-driven `ImportAsNewCookbookAsync` output), once as `JsonNode root`. Pull per-recipe nodes from `root["recipes"] as JsonArray`, deep-clone each one, stamp version, run upcaster, deserialize the upcasted node to `RecipeDocument`. Operates on the original shape regardless of v1/v2.
- **Files modified:** src/CookBot.Web/Services/CookbookTransferService.cs
- **Verification:** `Deserialize_V2Envelope_AlreadyCanonical_ImportsCleanly` and `Deserialize_MixedCookbook_PartialSuccess` (both v2 fixtures) pass — proving v2 fields survive.
- **Committed in:** 0fabdb3 (Task 1 GREEN commit)

**2. [Rule 3 — Blocking] @using Markdig needed for DisableHtml extension method**
- **Found during:** Task 3 (first build after AssistantContentPipeline field added)
- **Issue:** `DisableHtml()` is defined on `MarkdownPipelineBuilder` as an extension method in the `Markdig` namespace (specifically, `MarkdownExtensions.DisableHtml(this MarkdownPipelineBuilder)`). The original `@using Markdig.Renderers` line wasn't enough; without `@using Markdig`, the static field initializer fails to resolve `DisableHtml` as a method.
- **Fix:** Added `@using Markdig` to the top-of-file `@using` block.
- **Files modified:** src/CookBot.Web/Components/Pages/AiChat.razor
- **Verification:** Build clean (0 errors); the AssistantContentPipeline field initializer compiles.
- **Committed in:** 23a8515 (Task 3 GREEN commit; folded into the major rewrite)

**3. [Rule 3 — Blocking] tests project needed reference to Web project**
- **Found during:** Task 1 RED (writing CookbookUpcastImportTests)
- **Issue:** `CookbookTransferService` lives in `src/CookBot.Web/Services/`. The `tests/CookBot.Tests` project did not previously reference `src/CookBot.Web` (only Application + Infrastructure + Domain). The test file's `using CookBot.Web.Services;` failed at compile time.
- **Fix:** Added `<ProjectReference Include="..\..\src\CookBot.Web\CookBot.Web.csproj" />` to `tests/CookBot.Tests/CookBot.Tests.csproj`. This is the first test-time reference to the Web project. No new NuGet package; pure project-reference change.
- **Files modified:** tests/CookBot.Tests/CookBot.Tests.csproj
- **Verification:** Test build compiles; CookbookUpcastImportTests run successfully.
- **Committed in:** b2fc4e2 (Task 1 RED commit)

---

**Total deviations:** 3 (1 × Rule 1 — Bug; 2 × Rule 3 — Blocking dependency). All anticipated by the plan or required for correctness; no architectural changes (Rule 4) needed.

**Impact on plan:** None functional — all task acceptance criteria met. The Rule 1 fix (raw JsonNode path) is the most consequential; the plan-text DTO path would have silently broken v2 imports.

## Issues Encountered

- **No transient build/test failures.** No CLR errors. No flaky tests. The TDD RED → GREEN gates fired cleanly: Task 1 RED commit shows expected CS1739/CS0176 errors; Task 1 GREEN commit shows them resolved with new tests passing.
- **Manual UI verification path** — per the `<ui_note>` in the execute-plan prompt, the AiChat.razor changes are not unit-tested (Razor pages have no test harness in this codebase). Verifications done in this plan are **literal grep audits + dotnet build + dotnet test of the supporting non-UI pieces**. A browser-based smoke pass is recommended next:
  1. `./run.sh`; navigate to `/ai`.
  2. Enter "vegan chocolate cookies, 12 servings"; click **Generate Recipe**. Confirm: "Drafting recipe…" bubble appears; Save Recipe button shows on success; clicking Save opens the cookbook picker dialog and persists.
  3. Enter "what's a good substitute for buttermilk?" and click **Send**. Confirm: free-form streaming response appears; Save button does NOT show.
  4. Force a validation failure (e.g., model returns `name=""` in repair attempt) and confirm the Edit-and-save-anyway bubble surfaces.
  5. Force an auth error (clear API key in profile) and confirm the snackbar shows "Could not connect to the AI — check your API key in Profile settings." (sanitized copy, not raw 401 body).
  6. Open browser devtools; confirm any `<img src='exfil'>` injected into the chat does NOT render an actual `<img>` tag (DisableHtml lockdown).
  - **Cannot exercise the UI in this execution context** (no browser available to the agent). The literal-grep audit + build-clean + test-green gates verified all behavior that can be statically verified.

## User Setup Required

**None for this plan** — pure code change. No new NuGet packages, no DB migration, no environment variables, no external service config. The Phase 03 migration (AiConversation.FormatVersion column) auto-applies on next app startup; existing rows back-fill to 1 and the next save re-stamps to 2 (D-22).

## Next Plan Readiness

- **Wave 5 (Plan 02-05 — verify-phase / phase-2 audit)** can now exercise the full Phase 2 surface end-to-end:
  - `/ai` recipe generation → IAiRecipeGenerator → typed RecipeDocument → gated Save flow
  - Cookbook import → CookbookTransferService.Deserialize (instance) → upcaster → RecipeValidator → partial-success surface
  - YAML paste-in → RecipeFormatParser → version-stamping (Phase 1 H1) → upcaster → canonical RecipeDocument
  - AI-08 + AI-08-AUDIT defense-in-depth: prompt-side `<recipe>` wrap (Plan 03) + render-side DisableHtml lockdown (this plan)
- **POLISH-06 fully landed** — FormatVersion stamping + resume-system-note logic both wired; Phase 03's prereq column is now exercised end-to-end.
- **POLISH-01 fully landed** — the three-tier ExtractRecipeContent ladder (47 lines) and HasRecipe (4 lines) are gone; the typed RecipeDocument flow is the only path.
- **AI-08 fully landed** — Plan 03 added the system-prompt directive and the cooking-context wrap; Plan 04 closes the AI-08-AUDIT loop on the realistic exfil surface (Markdig DisableHtml).
- **AI-09 formally dropped** — REQUIREMENTS.md and CONTEXT.md both reflect the FUTURE-12 reframing. The trusted-LAN threat model rationale is documented in both files; reintroduce only if the app supports cookbook imports from untrusted sources outside a LAN.

## Threat Flags

None — no new security-relevant surface beyond the threat-model rows already covered by the plan's `<threat_model>` block. The static→instance refactor of CookbookTransferService.Deserialize is a layering/quality change at an existing trust boundary (untrusted cookbook JSON → application); the Markdig pipeline lockdown narrows an existing trust boundary (assistant content → DOM). No new boundaries crossed.

## Self-Check: PASSED

Verified after writing SUMMARY:
- FOUND: tests/CookBot.Tests/Migration/CookbookUpcastImportTests.cs
- FOUND: tests/CookBot.Tests/AI/RecipeFormatParserVersionStampingTests.cs
- FOUND modified: src/CookBot.Web/Components/Pages/AiChat.razor (293 insertions / 99 deletions; ExtractRecipeContent + HasRecipe deleted; AssistantContentPipeline + IAiRecipeGenerator + FormatVersion + resume-note + sanitized-snackbar mapper added)
- FOUND modified: src/CookBot.Web/Services/CookbookTransferService.cs (static→instance Deserialize; raw JsonNode upcast loop)
- FOUND modified: src/CookBot.Web/Components/Pages/ImportCookbookDialog.razor (instance call + partial-success snackbar)
- FOUND modified: tests/CookBot.Tests/CookBot.Tests.csproj (project ref to Web)
- FOUND modified: .planning/REQUIREMENTS.md (AI-09 removed; FUTURE-12 added; traceability + phase summary + top-of-file count adjusted)
- FOUND modified: .planning/phases/02-ai-structured-output-conformance/02-CONTEXT.md (canonical_refs corrected — IStructuredAiService line added to creates list)
- FOUND commit: b2fc4e2 (test: CookbookUpcastImportTests RED)
- FOUND commit: 0fabdb3 (feat: Deserialize instance refactor + upcaster routing GREEN)
- FOUND commit: e9fb0ef (test: RecipeFormatParserVersionStampingTests verification)
- FOUND commit: 23a8515 (feat: AiChat.razor major rewrite GREEN)
- FOUND commit: aaca339 (docs: AI-09 → FUTURE-12 + CONTEXT.md correction)
- VERIFIED: 153/153 tests pass
- VERIFIED: 0 warnings, 0 errors
- VERIFIED: ExtractRecipeContent + HasRecipe deleted (grep)
- VERIFIED: DisableHtml + ToHtml(content, AssistantContentPipeline) wired (grep)
- VERIFIED: IAiRecipeGenerator @inject + AiRecipeGenerator.GenerateAsync wired (grep)
- VERIFIED: FormatVersion = 2 stamping + FormatVersion < 2 resume note (grep)
- VERIFIED: _lastStructuredRecipe?.Ok == true gate on Save button (grep)
- VERIFIED: MapToSanitizedSnackbarCopy mapper (grep) + no raw ex.Message Snackbar.Add (grep)
- VERIFIED: CookbookTransferService is instance method (grep — no `public static .* Deserialize`)
- VERIFIED: ImportCookbookDialog uses instance call (grep — no `CookbookTransferService.Deserialize`)
- VERIFIED: AI-09 active line removed (grep) + FUTURE-12 entry present (grep)
- VERIFIED: CONTEXT.md canonical_refs corrected (grep — IStructuredAiService present, stale Domain/IAiService line absent)
- VERIFIED: 0 new NuGet packages (git diff -- '*.csproj' has no PackageReference adds)

---
*Phase: 02-ai-structured-output-conformance*
*Plan: 04*
*Completed: 2026-04-26*
