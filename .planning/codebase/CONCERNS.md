# Codebase Concerns

**Analysis Date:** 2026-04-25

This audit focuses on the milestone goals: making recipe mode intuitive (no special syntax knowledge), standardizing the file format, and getting AI chat to respond in & use the format. Concerns are ordered roughly by relevance to that milestone.

---

## File Format Inconsistencies (HIGHEST PRIORITY)

The app currently has **three competing serialization shapes** that all describe the same recipe concept. They have drifted apart and the AI is told about only some of them. Standardizing this is a prerequisite for the milestone.

### Concern 1: Three parallel recipe formats

**Format A — YAML frontmatter ("CookBot YAML")** — `src/CookBot.Application/Services/RecipeFormatParser.cs:1-221`
- Uses YAML between `---` fences with camelCase keys (`prepTime`, `cookTime`).
- Step ingredient refs use markdown link syntax `[name](#id)`.
- Steps may have a `timers:` array OR an inline section via the `section:` key (a step is *either* `text` or `section` — these are exclusive but sit in the same `steps` list).
- This is what `IRecipeFormatParser.Parse` / `Serialize` produce and what is sent to the AI in `PromptBuilderService.ResolveRecipeFormat()` (`src/CookBot.Application/Services/PromptBuilderService.cs:168-201`).

### Concern 2: JSON transfer format diverges from YAML format

**Format B — JSON cookbook export** — `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:1-52`, `src/CookBot.Web/Services/CookbookTransferService.cs:1-221`
- Uses camelCase JSON via `JsonNamingPolicy.CamelCase` (so `prepTimeMinutes`, not `prepTime`).
- Different field names from Format A: `prepTimeMinutes` / `cookTimeMinutes` here vs `prepTime` / `cookTime` in YAML; `localId` here vs `id` there.
- `IsSection` is a boolean on every step instead of a mutually-exclusive `section` key.
- `SchemaVersion = 1` is declared (`CookbookTransferDocument.SchemaVersion`) but the YAML format has no version field at all — there is no way to evolve either format safely.
- Round-tripping a recipe through export → import → AI prompt forces three different shapes for the same data.

### Concern 3: Database storage uses yet another representation

**Format C — owned-entity JSON column** — `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs:15-19`
- `RecipeStep` and nested `StepTimer` are stored as a JSON column via `OwnsMany(...).ToJson()`.
- `RecipeStep.IngredientRefs` is `List<int>` (`src/CookBot.Domain/Entities/RecipeStep.cs:9`) and is detected/written by `RecipeService` (`src/CookBot.Application/Services/RecipeService.cs:69`) but is **not** present in either Format A (YAML) or Format B (JSON export). It is recomputed on save from text.
- `Recipe.TagsJson` is a `string` column holding serialized JSON (`src/CookBot.Domain/Entities/Recipe.cs:11`); every read site (e.g. `RecipeView.razor:185`, `RecipeCookingAiContext.cs:18`, `CookbookTransferService.cs:62`) calls `JsonSerializer.Deserialize<List<string>>` independently with try/catch wrappers.
- Domain entity has no `Tags` property at all; this is a subtle sign of an unfinished schema.

### Concern 4: Two YAML key names for the same fields

`src/CookBot.Application/Services/RecipeFormatParser.cs:188-220` uses `prepTime` / `cookTime` in YAML, but `src/CookBot.Application/DTOs/CookbookTransferDtos.cs:23-24` uses `PrepTimeMinutes` / `CookTimeMinutes` in JSON. The AI is taught only `prepTime` (`PromptBuilderService.cs:170-201`). When the user exports a cookbook to JSON and pastes it back into the AI, the AI cannot read it.

**Fix approach:** Define a single canonical schema (likely the YAML form, since it is what users paste and what the AI emits). Generate the JSON export from it. Add a `version` field. Make `IngredientRefs` a derived, non-stored projection or persist it to the canonical format.

---

## Recipe Mode UX — Special Syntax Hard to Use

User flagged this directly. The "recipe format" requires the user (or the AI) to know multiple non-obvious markdown/YAML conventions.

### Concern 5: Ingredient references require knowing markdown link syntax

**Files:** `src/CookBot.Application/Services/IngredientRefDetectionService.cs:1-35`, `src/CookBot.Application/Services/RecipeStepTextFormatter.cs:1-65`, `src/CookBot.Web/Components/Pages/RecipeEditor.razor:144-167`

- The "official" way to link a step to an ingredient is `[ingredient name](#id)`. There is no UI in `RecipeEditor.razor` that inserts this for you — the textarea is plain text (`MudTextField ... Lines="3"` at line 146-148).
- A fallback in `IngredientRefDetectionService.DetectRefs` matches plain text names (case-insensitive substring match, length ≥ 3, line 29). This is fragile: an ingredient called "salt" will match the word "salt" anywhere in a step (including "asalted" partially due to `Contains`).
- Inside `IngredientRefDetectionService` the substring match is `textLower.Contains(nameLower)` with no word-boundary check — false positives on substrings.
- The `id` in `[name](#id)` is the **per-recipe local id**, not a global ingredient id. Users must hand-track these ids while writing steps. If they reorder ingredients, the ids do not change but the visual numbers shift.
- `RecipeEditor.razor:69` shows the local id as a tiny caption, but the editor has no "click an ingredient to insert it into this step" affordance.

**Fix approach:** A token / chip-based step composer that lets users select ingredients from a dropdown which inserts the `[name](#id)` automatically. Show ingredient chips inline with hover/replace UI so users never see the raw markdown unless they want to.

### Concern 6: Section vs. step is confusing in YAML and not clear in editor

**Files:** `src/CookBot.Application/Services/RecipeFormatParser.cs:208-213`, `src/CookBot.Web/Components/Pages/RecipeEditor.razor:117-120`, `src/CookBot.Application/Services/RecipeService.cs:65-70`

- In YAML: a step is either `{ text: "..." }` or `{ section: "..." }`. These keys are mutually exclusive but the parser silently takes whichever is non-empty (line 52-69 of `RecipeFormatParser.cs`). If both are set, the `Section` wins and `Text` is dropped without an error.
- In the DB: same shape is encoded as `IsSection: bool` + a single `Text` field (`RecipeStep.cs:6-7`). Two different mental models for the same thing.
- AI prompt says "Steps may have ... `section: \"Section header\"`" but does not explain that `section` excludes `text`. AI sometimes emits both.

### Concern 7: Timer detection runs in two incompatible places

**Files:** `src/CookBot.Application/Services/TimerDetectionService.cs:1-29`, `src/CookBot.Application/Services/RecipeService.cs:65-70`

- `RecipeService.CreateAsync` and `UpdateAsync` (lines 65 and 125) auto-detect timers from step text **only when** `ps.Timers` is null/empty. So a recipe with explicit `timers:` from YAML wins, but a free-typed step has timers detected by regex.
- The regex (`TimerDetectionService.cs:8-10`) only matches `\d+ (minutes|mins|hours|hrs|seconds|secs)`. It misses fractional times ("1 1/2 hours"), ranges ("20-25 minutes"), word-form numbers ("ten minutes"), and multi-segment timers ("1 hour 30 minutes" parses as two separate timers).
- Result: cooking mode sometimes shows timer chips for steps the user did not annotate, and other times misses obvious ones.

### Concern 8: Recipe scaling does not scale times or temperatures

`src/CookBot.Application/Services/RecipeScalingService.cs:1-17` only scales ingredient amounts. The step text — which contains the actual cooking instructions like "bake 25 min at 350°F" and the only place timers live — is rendered verbatim regardless of `_targetServings`. `CookingMode.razor:147` scales ingredients but `CurrentStep.Text` (line 58) is the raw text. The timer chips in cooking mode (line 64-79) also use the original `timer.Duration` without scaling.

**Fix approach:** Decide explicitly — either document "timers and temps are not scaled" prominently, or scale them with care (most cooking timing does NOT scale linearly with serving count, so the right call may simply be "display original timing with a note").

---

## AI Chat Output Not Validated Against the Format

User wants AI chat to respond in and use the file format. Today the validation is permissive and the parsing path is heuristic.

### Concern 9: Three different recipe extractors with weakening fallbacks

**File:** `src/CookBot.Web/Components/Pages/AiChat.razor:493-540`

`ExtractRecipeContent` tries:
1. ` ```recipe ` fenced block (line 496-505) — this is what the system prompt asks for (`PromptBuilderService.cs:172`).
2. `---\nname:` raw frontmatter anywhere in the message (line 509-515).
3. **Loose match**: any message containing `name:` AND `ingredients:` substrings, then take everything from the first line starting with `name:` to end-of-message (line 518-537). This will eat trailing prose, code blocks, the model's commentary, etc.

Only after extraction does `Parser.TryParse` run. If it fails for any of the three, the next is tried, but the third arm is so loose it can swallow surrounding prose into the YAML body and produce parsable but **wrong** recipes (e.g., the AI's closing remarks get folded into a step).

### Concern 10: AI prompt has an opt-out clause that disables format compliance

`src/CookBot.Application/Services/PromptBuilderService.cs:201` ends the recipe-format instruction with:

> "If you can't follow this exact format, plain numbered steps are fine — the app will parse them."

This is repeated almost verbatim in the copyable prompt (`PromptBuilderService.cs:295`). It tells the model the format is optional. Combined with concern 9, the AI happily produces non-compliant output and the app accepts it. This is the single biggest reason AI-generated recipes do not round-trip cleanly.

### Concern 11: No retry / repair pass when AI output is unparsable

`AiChat.razor:540` returns null when no recipe is detected, which only suppresses the "Save Recipe to Cookbook" button. There is no follow-up prompt to the model ("your last response was not in the recipe format, please re-emit it") and no in-app fix-it pass. The user has to ask again manually.

### Concern 12: System prompt "recipe_format" token is optional

`AiChat.razor:121-126` shows a warning when `{{recipe_format}}` is missing from the user's saved system prompt template — but the warning is a `Severity.Warning` `MudAlert` only. The user is allowed to save and use a template that has no recipe-format instructions at all. New conversations then never tell the AI about the format.

### Concern 13: `BuildCopyablePrompt` and `ResolveRecipeFormat` duplicate the format spec

`src/CookBot.Application/Services/PromptBuilderService.cs:168-201` (the in-app system prompt) and `:262-296` (the copyable external prompt) each contain a hand-written copy of the exact same format example. They will drift. There is no single source of truth for "this is what a CookBot recipe looks like."

**Fix approach:** Put the canonical format spec + example in one constant (or read from a versioned schema), share between in-app system prompt, copyable prompt, parser-error help text, and developer docs.

---

## API Key / Secret Handling

Recent commit: "Api key sharing, anthropic model updates" introduced the share feature.

### Concern 14: Anthropic API key stored in plaintext in SQLite

**Files:** `src/CookBot.Domain/Entities/UserProfile.cs:17`, `src/CookBot.Web/Services/AiApiKeyResolutionService.cs:33-40`, `src/CookBot.Web/Components/Pages/EditProfile.razor:135-138`

- `UserProfile.AiApiKey` is a plain `string?` column (`UserProfile.cs:17`). No encryption, no DPAPI, no host secret used.
- The profile page is honest about it (`EditProfile.razor:136`: "Your API key is stored in the local database only (not encrypted unless you encrypt the database file at the host level)") but anyone with read access to `cookbot.db` (the SQLite file) can retrieve the key.
- `appsettings.json:16` allows a server-wide `AnthropicApiKey` fallback (`AnthropicAiService.cs:38`); committed appsettings have it empty, but anyone editing it commits the key.

**Mitigation in place:** The recipient never sees the owner's key in the UI — `AiApiKeyResolutionService.ResolveAsync` runs server-side and returns an `EffectiveAiCredentials` record that the Razor pages pass directly into `AiService.SendMessageAsync` (`CookingMode.razor:362`). The key value itself is not bound to any UI input on the recipient's side. Good.

**Recommendations:**
- Encrypt `AiApiKey` at rest using `IDataProtector` (`Microsoft.AspNetCore.DataProtection`) keyed off a host-provided key persistence path.
- Add a clear warning in the Profile UI: "Anyone with file-system access to the host can read this key."
- Consider deriving an env-var-only mode: load `ANTHROPIC_API_KEY` from environment and disable per-user keys when set.

### Concern 15: API key sometimes leaks into HTTP error messages

**File:** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs:69-70` and `:88-89`

```csharp
throw new HttpRequestException($"Anthropic API error: {body}");
```

Anthropic occasionally echoes request headers or partial payload in error bodies on 4xx responses. The exception message is then shown to the user via `Snackbar.Add(ex.Message, Severity.Error)` (`AiChat.razor:352`, `CookingMode.razor:367`). If the body ever includes the `x-api-key` header (it shouldn't from Anthropic, but from a misconfigured proxy it could), the key surfaces in the snackbar.

**Fix approach:** Sanitize the error body before raising / displaying. At minimum strip strings that match the configured api key.

### Concern 16: Per-request `HttpClient` instances

**File:** `src/CookBot.Infrastructure/AI/AnthropicAiService.cs:36-46, 50, 64, 78`

`CreateHttpClient` and `ListModelsAsync` both `new HttpClient()` per call. This is a known .NET socket-exhaustion footgun — DNS is also pinned per `HttpClient` lifetime. Should use `IHttpClientFactory` registered in `DependencyInjection.cs`.

### Concern 17: Share resolution silently picks the first owner

**File:** `src/CookBot.Web/Services/AiApiKeyResolutionService.cs:50-60`

When a recipient has multiple incoming shares, no preferred owner saved, and >1 are usable, `chosen` stays null and resolution returns null. The UI then forces the user into "Choose account..." in `SharedKeysDialog.razor:99-111`. So far so good. **But** when there are multiple shares and the recipient saves their *own* key after the fact, `ClearStaleSharedKeyPreferenceAsync` (`AiApiKeyResolutionService.cs:72-82`) wipes the preference but does not log it; if the user later removes their own key, they silently fall back to "no chosen owner" and see the choice prompt again. Not a bug, but unobvious.

### Concern 18: No rate limiting / abuse protection on shared keys

When user A shares their key with user B, user B can send unlimited requests through user A's account. There is no per-recipient cap, no daily quota, no "approval per request." The owner has no in-app indicator of how their key is being used. For a self-hosted "trusted network" app this is by design, but it should be documented in the share UI.

---

## Validation, Error Handling, Robustness

### Concern 19: Recipe save dialog crashes on invalid AI output

`src/CookBot.Web/Components/Pages/SaveRecipeDialog.razor:49-53` — `Parser.TryParse(RecipeContent, out var parsed, out var errors)` reports a snackbar on failure. But `RecipeContent` is whatever `AiChat.ExtractRecipeContent` returned, which (per concern 9) may be a permissive grab. There is no way for the user to edit/fix the captured text before saving — they have to ask the AI again.

### Concern 20: PantryAiPopulationService has a 500-line JSON repair pipeline

**File:** `src/CookBot.Application/Services/PantryAiPopulationService.cs:181-475`

The service has deeply nested heuristics (`ExtractJsonArray`, `TryExtractPantryJsonArray`, `TryExtractArrayFromJsonObjects`, `TryParseJsonRootForPantryArray`, `TryGetPantryArrayFromElement`, `LooksLikePantryImportArray`, `FindMatchingJsonBraceClose`, `EnumerateMarkdownCodeBlocks`, `TryExtractBalancedJsonArray`, `FindMatchingJsonArrayClose`, `NormalizePantryImportPropertyNames`, `StripAngleBracketSections`, `NormalizeJsonishText`) — about 290 lines of recovery code for AI output that should have been a clean JSON array. This is technical debt that grew because the prompt is permissive and the model lies; the recipe pipeline will need similar logic if format compliance is not tightened.

### Concern 21: Cookbook share security audit gap

**File:** `src/CookBot.Web/Services/CookbookTransferService.cs:213-220`

`CanAccessAsync` checks `cookbook.UserId == userId || CookbookShares.Any(s.SharedWithUserId == userId)`. The latter does not check whether the share is currently active or revoked — `CookbookShares` is the only source of truth and there is no `IsRevoked` column, so revoking via `_db.CookbookShares.Remove(...)` is the only way. If a future migration adds soft-delete, this check breaks silently.

### Concern 22: `ExperienceLevel` enum order assumed, never asserted

`src/CookBot.Domain/Enums/ExperienceLevel.cs` defines `Beginner/Intermediate/Advanced/Professional`. `PromptBuilderService.cs:91-105` switches on exact values. If a new level is inserted (e.g. "Home Cook" between Beginner and Intermediate) and a migration assigns it the next ordinal, every saved profile shifts. Use named values explicitly: `public enum ExperienceLevel { Beginner = 1, ... }`.

### Concern 23: `MeasurementUnit` enum exists but is barely used

**Files:** `src/CookBot.Domain/Enums/MeasurementUnit.cs:1-39`, `src/CookBot.Application/Services/UnitParser.cs:1-147`

`MeasurementUnit` is a closed enum, but `RecipeIngredient.Unit` and `PantryItem.Unit` are stored as free-text `string`. `UnitParser.TryParse` maps strings → enum but the enum is only consumed in tests / unit conversion math. The README says "Flexible units — any unit string is accepted" — that's by design. But it means the enum is dead weight: it's incomplete (e.g. no "splash", no "handful") and the tests around it don't reflect what's actually persisted. Worth either deleting or formalizing as a "canonical unit" used for conversion only.

### Concern 24: Database is auto-seeded with no idempotency guard documented

`src/CookBot.Web/Program.cs:39-43` runs `DatabaseSeeder.SeedAsync` on every startup. If seeding logic ever has a bug it can corrupt user data. The seeder file (`src/CookBot.Infrastructure/Data/DatabaseSeeder.cs`, not deeply read here) is a critical-path component that should have a "I am safe to run multiple times" assertion.

---

## UX / Lifecycle Issues

### Concern 25: Default user "Home Chef" is auto-created and auto-selected

**File:** `src/CookBot.Web/Components/Layout/MainLayout.razor:108-122, 207-211`

If the database is empty, the layout creates a default `"Home Chef"` admin and silently selects them. There is no setup wizard. A new self-hosted instance has effectively no authentication — anyone reaching the URL is auto-logged-in as the admin.

The `CookBotSettings.AuthMode` field exists (`src/CookBot.Application/DTOs/CookBotSettings.cs:6-10`) but is annotated `"Reserved for future use; not enforced by the app yet. Do not rely on this for security."` This is only acceptable for "self-hosting on a trusted network" (per README) — but the app is ready to be reverse-proxied to the public internet by an unsuspecting user.

### Concern 26: User switching forces a full-page reload

**File:** `src/CookBot.Web/Components/Layout/MainLayout.razor:191`, `EditProfile.razor:451`, `MainLayout.razor:219`

`Navigation.NavigateTo(Navigation.Uri, forceLoad: true)` is used in three places to "refresh state after a profile/identity change." This loses any in-progress dialog state, scroll position, draft recipe text, etc. It exists because Blazor Server's circuit-bound state is hard to invalidate cleanly. Worth a focused refactor toward DI-scoped state with explicit invalidation events.

### Concern 27: `OnAfterRenderAsync(firstRender)` used for one-shot data loads

Multiple pages (`CookingMode.razor:257-310`, `RecipeView.razor:162-192`, `EditProfile.razor:277-303`, `RecipeEditor.razor:203-239`) load data in `OnAfterRenderAsync` guarded by ad-hoc `_initialized`/`_loadedUserId` flags. The Blazor-idiomatic place is `OnInitializedAsync`/`OnParametersSetAsync`. The current pattern means:
- Brief flashes of "Recipe not found" or empty state during prerender (`RecipeView.razor:131-134`).
- A subtle race where `UserService.CurrentUserId` may not be set at first prerender so the page silently no-ops (`if (!UserService.CurrentUserId.HasValue) return;`).

### Concern 28: Markdown rendering trusts AI output (XSS-adjacent)

**Files:** `src/CookBot.Web/Components/Pages/AiChat.razor:484`, `CookingMode.razor:377`

Both call `Markdig.Markdown.ToHtml(content)` and inject as `MarkupString`. Markdig escapes HTML by default but also passes raw HTML through if `UseAdvancedExtensions` is enabled. The instances here use the default pipeline (no `UseAdvancedExtensions`), so this is fine *today*, but a future maintainer adding `UseAdvancedExtensions` would silently introduce a stored-XSS vector via AI output (which the user might paste from anywhere). Consider `Markdig.UsePipeTables().DisableHtml()` explicitly.

### Concern 29: `RecipeStepTextFormatter.ToHtml` is the right pattern but inconsistent

**File:** `src/CookBot.Web/Components/Pages/AiChat.razor:484` vs `RecipeView.razor:109,116`

The recipe view uses the safe `RecipeStepTextFormatter.ToHtml` (which `WebUtility.HtmlEncode`s everything except detected ingredient links). The AI chat uses raw `Markdig.Markdown.ToHtml`. So a recipe rendered in chat handles its `[name](#id)` differently from the same recipe rendered in the recipe view (the chat treats it as a markdown link to `#1`, the view treats it as an ingredient ref span). Round-trip is inconsistent visually.

---

## Performance Bottlenecks

### Concern 30: No pagination anywhere

**Files:** `CookbookList.razor`, `RecipeFormatParser.cs` callers, `PantryView.razor`, `GroceryListView.razor`

All list pages load every row owned by the user with no limit/offset. For a single-user self-hosted app this is fine; if the dataset grows past a few hundred recipes the UI becomes slow.

### Concern 31: `Repository<T>.FindAsync` materializes via `.ToList()` likely

`src/CookBot.Infrastructure/Data/Repositories/Repository.cs` (not read here, but invoked from `CookbookService.GetUserCookbooksAsync` line 16, `RecipeService.ResolveIngredientAsync` line 156) — every call to `FindAsync` returns `IReadOnlyList<T>`, suggesting full materialization. `RecipeService.ResolveIngredientAsync` is called in a foreach loop (line 44-55) for every ingredient on every save → O(N) DB round trips per recipe. For a 15-ingredient recipe, that is 15 sequential queries.

**Fix approach:** Batch-resolve ingredients with a single `Where(i => normalizedNames.Contains(i.NormalizedName))` query before the loop.

### Concern 32: AI conversation message log grows unbounded

**File:** `src/CookBot.Web/Components/Pages/AiChat.razor:343-348, 380`

Each new turn appends to `_messages` and re-serializes the full list to `MessagesJson`. The full list is also re-sent to Anthropic on every turn. There is no pruning, summarization, or token-count guard. Long conversations will hit `max_tokens` of the model and silently fail.

### Concern 33: System prompt rebuilt on every send, includes full pantry

`AiChat.razor:341-345` calls `AiService.StreamMessageAsync(_systemPrompt, ...)` where `_systemPrompt` was built once via `BuildSystemPrompt()` (line 261-276). But every cookbook recipe token is expanded inline (`ExpandCookbookRecipeTokensAsync`, line 410-426). For a user with many recipes this can balloon the system prompt to thousands of tokens that ride on every message in the conversation.

---

## Testing Gaps

### Concern 34: No tests for `AiChat.ExtractRecipeContent`

The most heuristic, format-fragile method in the codebase (`AiChat.razor:493-540`) has zero coverage. Given concerns 9-13, this is the highest-risk untested code in the milestone scope.

**Files:** `tests/CookBot.Tests/...` — see existing list (`PantryAiPopulationServiceTests.cs`, `RecipeFormatParserTests.cs`, etc.).

### Concern 35: No tests for AI pantry standardization round-trip

`PantryAiPopulationService.StandardizePantryAsync` (line 607-679) **clears the entire pantry** before re-adding parsed rows (`ClearPantryAsync(pantryId)` at line 649). If the AI returns a partial list (or the model truncates due to `max_tokens: 8192`), the pantry is destroyed and replaced with a subset. There is one test file `PantryAiPopulationServiceTests.cs` but no clear failure-mode test for partial AI output.

### Concern 36: No integration test for recipe save → DB → cooking-mode round-trip

The full path "AI emits YAML → SaveRecipeDialog → RecipeService.CreateAsync → DB → CookingMode" is not exercised end-to-end. Each piece has unit tests but the join is where most bugs live (e.g. the `IngredientRefs` recomputation in `RecipeService.cs:69` vs the parser-emitted refs is not asserted to agree).

---

## Comments / TODOs / HACKs Search

`grep -rn "TODO|FIXME|HACK|XXX" src/` returns **zero matches**. Either the codebase is exceptionally clean (unlikely for vibecoded code), or contributors have been removing the markers without addressing the underlying concerns. The concerns above were found by reading the code.

A few "soft TODOs" exist as `<summary>` comments hinting at unfinished work:
- `src/CookBot.Domain/Entities/UserProfile.cs:13`: "Optional free-text unit rules for AI features (exceptions to the preset unit system)." — implies the preset is incomplete.
- `src/CookBot.Application/DTOs/CookBotSettings.cs:8-9`: "Reserved for future use; not enforced by the app yet. Do not rely on this for security." — `AuthMode` is a placeholder.
- `src/CookBot.Web/Services/AiApiKeyResolutionService.cs:21-23`: "Recipients never receive the key in the UI; this runs only on the server." — defensive comment that should become a contract test.

---

## Quick-Win Priorities (Milestone-Aligned)

For the goals "intuitive recipe mode + standardized format + AI uses format":

1. **Concerns 1–4, 10, 13** — Single canonical recipe schema with a version field, used everywhere, exposed to the AI in one spot.
2. **Concerns 9, 11, 12, 19** — Strict AI output validation: require fenced ```recipe block, retry on failure, surface parse errors with edit option.
3. **Concerns 5, 6** — RecipeEditor: ingredient-chip insertion, clearer step vs section split.
4. **Concern 7** — Either remove the regex timer auto-detection or normalize it to a single source of truth.
5. **Concerns 14, 15** — Encrypt API keys at rest and sanitize error bodies (security follow-up to the recent share feature).
6. **Concern 34** — Add tests for `ExtractRecipeContent` before refactoring it.

---

*Concerns audit: 2026-04-25*
