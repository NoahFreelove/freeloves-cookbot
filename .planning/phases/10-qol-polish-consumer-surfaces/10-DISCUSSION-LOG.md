# Phase 10: QOL, Polish & Consumer Surfaces - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-16
**Phase:** 10-qol-polish-consumer-surfaces
**Areas discussed:** Pantry-match algorithm, AI Chat raw-edit dialog, Profile prompt editor + warning, TopBar slot mechanism

---

## Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Pantry-match algorithm | Scoring formula weights, recency window, dietary-filter shape, result count. STATE.md flagged weights as open. | ✓ |
| AI Chat raw-edit dialog | RawRecipeEditorDialog surface, parse-success flow, textarea initial content, validation feedback. | ✓ |
| Profile prompt editor + warning | BuildSystemPrompt wiring (REQUIREMENTS claim is incorrect), variable insertion affordance, reset, injection-warning placement. | ✓ |
| TopBar slot mechanism | ICbTopBarService vs CascadingValue; adoption scope; lifecycle; responsive collapse. | ✓ |

**User's choice:** All four areas selected (multiSelect).

---

## Pantry-match algorithm

### Q1: Scoring formula recency handling

| Option | Description | Selected |
|--------|-------------|----------|
| Hard 7d cutoff | score = matched/total; recipes cooked in last 7d drop to bottom tier-2 sort key. Simple, bounded, no weight tuning. | |
| Linear decay | score = (matched/total) - 0.3 * exp(-daysSinceCooked/7). Adjustable knob, smooth behavior. STATE.md's proposed weight. | ✓ |
| Step penalty | score = (matched/total) - 0.15 if cooked in last 7d, else 0. Middle ground: bounded, fixed constant. | |

**User's choice:** Linear decay.
**Notes:** User picked the smooth-curve variant over the cliff cutoff; quality signal that the user values gradient over discrete buckets.

### Q2: Dietary filter shape

| Option | Description | Selected |
|--------|-------------|----------|
| Tag-match (positive) | Filter to recipes whose RecipeTag rows match user's DietaryPreferences. Case-insensitive per Phase 8 D-34. | |
| Ingredient-exclude (negative) | Exclude recipes containing ingredients flagged for the diet via diet→IngredientCategory map. | |
| Both, AND-combined | Apply tag-match AND ingredient-exclude. Strictest filter; minimal false positives. | ✓ |

**User's choice:** Both, AND-combined.
**Notes:** Strictest variant; recipes need proper tagging AND clean ingredient categorization to survive. Planner must commit to a curated diet→category map (Claude's discretion D-47).

### Q3: Result count and minimum-coverage floor

| Option | Description | Selected |
|--------|-------------|----------|
| Keep 3+0.6, no knob | Top 3 cards, hide < 60% coverage. Matches current Home.razor.cs behavior. | |
| Top 5, no floor | Show 5 results, no coverage floor. More options for sparse pantries. | |
| Configurable in appsettings | Both count and floor in CookBot:PantryMatch with defaults 3/0.6. Self-hosters can tune. | ✓ |

**User's choice:** Configurable in appsettings.
**Notes:** Explicit, deliberate departure from v1.3's "bounded no-knob" pattern.

### Q4: Configurability of scoring weights

| Option | Description | Selected |
|--------|-------------|----------|
| Hardcoded constants | Pantry-match formula hardcoded in IPantryMatchService. Cleanest code, no IOptions pattern. | |
| appsettings.json knobs | CookBot:PantryMatch section binds to PantryMatchOptions. STATE.md's literal recommendation. | ✓ |
| Per-user override later | Hardcoded for v1.3; flag for v1.4+ if user requests adjustability via Profile. | |

**User's choice:** appsettings.json knobs.
**Notes:** Pantry-match is the ONE tuning surface the user wants exposed in v1.3.

**Continue?** Next area.

---

## AI Chat raw-edit dialog

### Q1: Dialog surface

| Option | Description | Selected |
|--------|-------------|----------|
| CbDialog modal | Same CbDialog pattern as SaveRecipeDialog. Consistent UX surface. | ✓ |
| Side panel in canvas | Open in-place in AI Chat right-canvas. Keeps chat visible alongside. | |
| Inline expansion | Expand the failed-validation chat bubble into an inline textarea. No modal context switch. | |

**User's choice:** CbDialog modal.

### Q2: 'Parse and save' success flow

| Option | Description | Selected |
|--------|-------------|----------|
| Open SaveRecipeDialog | Hand parsed text to SaveRecipeDialog (cookbook picker, persist). Two-dialog hop. Consistent with success path. | ✓ |
| Save inline | Raw-edit dialog has embedded cookbook picker + Save button. One dialog. Duplicates SaveRecipeDialog logic. | |
| Save + open editor | Persist immediately to a "Drafts" cookbook, then navigate to RecipeEditor. | |

**User's choice:** Open SaveRecipeDialog.
**Notes:** Preserves Phase 1 invariant "never persist non-conforming recipes" structurally — RawRecipeEditorDialog cannot bypass SaveRecipeDialog's cookbook picker.

### Q3: Initial textarea content

| Option | Description | Selected |
|--------|-------------|----------|
| Raw JSON (pretty-printed) | Indented _lastStructuredRecipe.RawResponse via JsonSerializer with WriteIndented=true. Most editable form. | ✓ |
| Raw JSON (one-liner) | JsonNode.ToJsonString() as-is, no pretty-print. Smaller; harder to edit. | |
| YAML translation | Serialize raw JSON to YAML via parser path before showing. Translation may itself fail. | |

**User's choice:** Raw JSON (pretty-printed).

### Q4: Validation feedback

| Option | Description | Selected |
|--------|-------------|----------|
| On-action only | Parse runs when user clicks Parse-and-save — inline error toast on failure. Lowest CPU. | |
| Debounced live | After 500ms idle, attempt parse and show green check / red X next to action button. More responsive. | ✓ |
| Schema highlight only | JSON syntax errors highlighted via textarea attribute. No semantic feedback. | |

**User's choice:** Debounced live.

**Continue?** Next area.

---

## Profile prompt editor + warning

### Q1: BuildSystemPrompt wiring (corrects REQUIREMENTS claim)

| Option | Description | Selected |
|--------|-------------|----------|
| Null-fallback override | If profile.AiSystemPromptTemplate is non-null/non-whitespace, use as template; else DefaultTemplate. Smallest change. | ✓ |
| Always custom + merge | Always run user template through a merge layer that fills missing required tokens. Safer; more code surface. | |
| Compose-only | Custom template appended to DefaultTemplate, not replacing it. Safest, least flexible. | |

**User's choice:** Null-fallback override.
**Notes:** Important — custom templates can omit `{{recipe_format}}` or other required tokens; the injection warning copy must explicitly call this out. Phase 8 Verify snapshot test needs a third re-verify (Phase 8 initial + Phase 9 D-42 prose + Phase 10 wiring change).

### Q2: Variable insertion affordance

| Option | Description | Selected |
|--------|-------------|----------|
| Clickable CbChip row | Row of clickable CbChips above textarea — click inserts at cursor via JS interop. Discoverable. | ✓ |
| Read-only labels | Plain text block listing available tokens with descriptions. User copy-pastes manually. | |
| Auto-complete in textarea | Type `{{` to surface a dropdown of available tokens. Most editor-like; highest cost. | |

**User's choice:** Clickable CbChip row.
**Notes:** Mirrors the existing recipe-chip-composer.js pattern.

### Q3: Reset affordance

| Option | Description | Selected |
|--------|-------------|----------|
| Reset button + confirm | CbButton "Reset to default" + CbDialog confirm. Reversible by not-clicking-save. | ✓ |
| Reset button, no confirm | Same button, immediate effect on textarea. Less friction. | |
| No reset button | User must manually delete contents and Save to clear. Simplest; lowest discoverability. | |

**User's choice:** Reset button + confirm.

### Q4: Injection warning placement

| Option | Description | Selected |
|--------|-------------|----------|
| Inline note below textarea | Small CbCard with subtle warning styling immediately below textarea. Always visible. | ✓ |
| Click-to-expand callout | Collapsed "About custom prompts →" link above textarea; clicks reveal. Quieter UI. | |
| Dialog on first edit | Modal warning shown first time per user. Hard to ignore but only fires once. | |

**User's choice:** Inline note below textarea.
**Notes:** Read-once-and-internalize; matches Phase 9 D-42 prose-nudge precedent.

**Continue?** Next area.

---

## TopBar slot mechanism

### Q1: Plumbing approach

| Option | Description | Selected |
|--------|-------------|----------|
| ICbTopBarService scoped | New scoped service with event-based RightSlot updates. ROADMAP literal compliance. Future-proof. | ✓ |
| CascadingValue RenderFragment | MainLayout exposes a [CascadingParameter] RenderFragment. Simpler; less DI ceremony. Diverges from ROADMAP literal. | |
| Layout-level CascadingValue | Hybrid: scoped state object as CascadingValue from MainLayout. Middle ground. | |

**User's choice:** ICbTopBarService scoped.
**Notes:** Honors ROADMAP success criteria 4's literal text.

### Q2: Adoption scope

| Option | Description | Selected |
|--------|-------------|----------|
| RecipeView only | Phase 10 ships slot mechanism + migrates RV-05 actions only. Smallest blast radius. | |
| RecipeView + RecipeEditor | Migrate both pages. Two real cases (one read surface, one write surface). | ✓ |
| All Phase 10 surfaces | Audit every Phase 10 page and route through RightSlot. Biggest blast radius. | |

**User's choice:** RecipeView + RecipeEditor.

### Q3: Lifecycle on navigation

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-clear on nav | Service auto-clears on NavigationManager.LocationChanged. Predictable; no boilerplate. | ✓ |
| Page must clear | Pages set on init AND clear on Dispose. Misbehaving pages leak stale buttons. | |
| Sticky until replaced | Slot retains content until next page explicitly sets. Surprising. | |

**User's choice:** Auto-clear on nav.

### Q4: Responsive collapse

| Option | Description | Selected |
|--------|-------------|----------|
| Hide on narrow | CSS media-query hides RightSlot below 720px; each page handles its own narrow layout. | ✓ |
| Always visible, scrolls | RightSlot stays visible at all widths; overflow scrolls horizontally. | |
| Collapse to overflow menu | Below 720px, RightSlot contents move into a kebab (⋮) menu. Most polished; needs overflow component. | |

**User's choice:** Hide on narrow.

**Continue?** Wrap up.

---

## Final check

| Option | Description | Selected |
|--------|-------------|----------|
| Ready for context | Decisions tight; write CONTEXT.md. Remaining POLISH items can be Claude's discretion. | ✓ |
| Explore more gray areas | Pantry quick-add target list, AI usage widget shape, accent picker order, etc. | |

**User's choice:** Ready for context.

---

## Claude's Discretion

The user did not weigh in on these; the planner can make the calls at plan time. Captured in CONTEXT.md `<decisions> ### Claude's Discretion` section:

- `IPantryMatchService` location & lifetime (Application layer, Scoped)
- Per-cookbook reparenting UI shape on RecipeEditor (POLISH-01)
- Pantry quick-add target list resolution (POLISH-02 design-gap — GroceryListService has no AddItemAsync; no "primary list" concept exists)
- Moon glyph weight & path (POLISH-03)
- Live timer tick implementation (`startTickLoop` in cooking-session-state.js, pagehide teardown) (POLISH-05)
- Accent picker UI shape on EditProfile (QOL-05)
- AI usage widget shape on EditProfile (single rolling-30d card; PROD-17 read surface)
- `PantryMatchOptions` DTO location
- Diet → excluded-IngredientCategory map (hardcoded static)
- Stable sort key construction (tertiary by Name asc)
- Phase 8 composite index verification
- Plan / wave structure

---

## Deferred Ideas

Captured in CONTEXT.md `<deferred>` section. Highlights:

- AI usage widget chart / per-model breakdown → v1.4+
- TopBar.LeftSlot symmetry → v1.4+
- TopBar slot adoption in pages beyond RecipeView + RecipeEditor → opportunistic
- Per-user spending caps / billing quotas → v1.4+
- Cross-user admin telemetry view → v1.4+
- `CookBotSettings.TelemetryEnabled` killswitch → v1.4+
- Case-insensitive tag dedup → v1.4+
- Auto-complete in prompt-template textarea → v1.4+
- Pantry-match expiration-aware scoring → v1.4+ (anti-feature)
- Pantry-match scoring weights as per-user override → v1.4+
- CookingMode parallel live-tick (not needed; cooking-timers.js already runs interval)
- "Drafts" cookbook for failed raw-edit auto-save → not a concept in v1.3
- Pantry quick-add to shared pantry's owner's grocery list → v1.4+
- Reverse-cookbook reparenting from CookbookDetail / RecipeView → v1.4+
- Moon glyph filled variant → v1.4+
