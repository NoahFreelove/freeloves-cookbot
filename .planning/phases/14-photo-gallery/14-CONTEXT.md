# Phase 14: Photo Gallery - Context

**Gathered:** 2026-06-07
**Status:** Ready for planning
**Mode:** `--auto` (gray areas auto-resolved with the recommended, lowest-risk option that honors the locked v1.4 invariants; **review the ⚠ FLAGGED decision D-14-01 before planning** — it interprets the "photos never in canonical" invariant against the already-shipped `Recipe.PhotoUrl` field).

<domain>
## Phase Boundary

Grow recipe photos beyond the single v1.3 hero into a **curated multi-photo gallery**, backed by a new relational `RecipePhoto` entity, with an **AI search-term helper** that introduces zero hallucination or copyright risk. Concretely (GALLERY-01..04, locked WHAT):

1. **`RecipePhoto` entity** (GALLERY-01) — ordered, optional caption, exactly one primary; an EF migration **backfills the existing `Recipe.PhotoUrl`** into a primary `RecipePhoto` row with no data loss.
2. **Authoring** (GALLERY-02) — upload multiple photos *sequentially*, reorder, set captions, choose the primary/hero, from `RecipeEditor` — reusing the v1.3 **12 MB / magic-byte / scheme-allowlist** safeguards.
3. **Display + cleanup** (GALLERY-03) — `RecipeView` shows the gallery (primary as hero); deleting a photo *or* the recipe removes the backing file from `wwwroot/uploads/` — **no orphaned files** in the Docker volume.
4. **AI helper** (GALLERY-04) — gated by `AiFeaturesEnabled` **and** `UserProfile.AiEnabled`; describes the dish and suggests **text-only** photo search terms for free-licensed sites. The AI **never emits or auto-embeds an image URL**; the user's pasted URL is **HEAD-validated before persist**; a copyright disclaimer is visible on every photo input surface.

**Out of scope** (carried from REQUIREMENTS.md "Future"/"Out of Scope"): AI image *generation*; AI-emitted/auto-embedded URLs; Unsplash/Pexels *API* integration (external key dependency — conflicts with self-host posture, the helper only links out); per-step photo linking (Paprika-style); a vision/image-input path on `AnthropicAiService`. This phase is **additive** — no breaking changes to the v4 round-trip or the trusted-LAN posture.

</domain>

<decisions>
## Implementation Decisions

All seven gray areas were auto-resolved in `--auto` mode. The recommended option (grounded in the locked v1.4 hard invariants, the milestone's "additive / minimal blast radius" stance, and the v1.3 photo precedents) was selected for each. **These are the lockable HOW decisions; the WHAT is fixed by GALLERY-01..04.**

### ⚠ Storage model + reconciliation with the legacy `Recipe.PhotoUrl` (highest-stakes — confirm before planning)
- **D-14-01 (FLAGGED for review):** `Recipe.PhotoUrl` **stays as a canonical field** and becomes a **denormalized mirror of the primary `RecipePhoto.Url`**, re-synced by `RecipeService` on every save and on every gallery mutation (set-primary, delete-primary, reorder). The **gallery rows** (the ordered set, captions, sort order, primary flag) live **only** in the `RecipePhoto` table and **never** enter `CanonicalDocumentJson`.
  - **Why this interpretation:** STATE.md's invariant *"Photo paths never stored in CanonicalDocumentJson — RecipePhoto entity table owns file paths"* is satisfied **in spirit** by interpretation (A) below — the new multi-photo gallery data never touches canonical, never travels in `.cookbook.json` (already stripped — see D-14-07), and is never fed to/emitted by the AI (D-14-04). The single hero `PhotoUrl` was **already** a v3/v4 canonical field in shipped code, and **four readers depend on it**: `RecipeView` hero (`RecipeView.razor:133-150`), the Phase 13 `JsonLdRecipeProjector` `image` field, Home thumbnails, and cookbook collage thumbnails.
  - **Interpretation (A) — CHOSEN (low risk, additive):** keep `PhotoUrl` as the synced primary mirror → **zero downstream rewiring**; the Phase 13 JSON-LD `image` and the Phase 15 "gallery hero feeds image" requirement work *automatically* with no projector change.
  - **Interpretation (B) — REJECTED (high risk):** rip `PhotoUrl` out of canonical, make every reader query `RecipePhoto`. More literally "pure" but a large blast radius (JSON-LD projector, RecipeView, Home, collage, AiChat canvas) on a shipped, working v3 design — contradicts the milestone's no-breaking-changes stance.
  - **If the user prefers strict (B), say so before planning** — it materially changes the plan's task count and touches Phase 13's projector.
- **D-14-02:** `RecipePhoto` is a **relational FK child entity** of `Recipe` (NOT owned-JSON), mirroring the `RecipeIngredient` pattern (`RecipeConfiguration.cs:29`): `HasMany(r => r.Photos).WithOne(p => p.Recipe).HasForeignKey(p => p.RecipeId).OnDelete(DeleteBehavior.Cascade)`. Owned-JSON is rejected because the gallery needs stable per-row identity for reorder, individual delete (+ file cleanup), and a primary flag.
- **D-14-03 (shape):** `RecipePhoto { int Id; int RecipeId; string Url (max 2048, matches PhotoUrl); string? Caption (max ~512); int SortOrder; bool IsPrimary; }`. Invariant: **exactly one `IsPrimary` per recipe**, enforced in the service layer (setting a new primary clears the prior; deleting the primary promotes the lowest `SortOrder`; if none flagged, lowest `SortOrder` is treated as primary defensively). `Url` holds either a local `/uploads/{guid}.ext` path (uploaded) or an external `http(s)://` URL (pasted) — same dual-source model as today's `PhotoUrl`.

### Photo count cap (resolves STATE open question #1)
- **D-14-04-cap:** A configurable **`CookBotSettings.MaxPhotosPerRecipe`**, default **10**, clamped `[1, 20]` — following the existing `DatabaseBackupRetention` clamped-int precedent (`CookBotSettings.cs:25-26`). **Enforced server-side** in the photo service (not just the UI); the editor shows a clear "max N photos" message and disables the add affordance at the cap. (Research suggested ≤5 or ≤10; 10 gives a home cook room for process shots + final dish without unbounded Docker-volume growth.)

### Multi-upload, reorder, circuit safety (GALLERY-02; pitfall P14)
- **D-14-05 (upload):** A single `<InputFile multiple accept="image/jpeg,image/png,image/gif,image/webp">` whose handler **persists each file strictly sequentially** — `await PhotoStorage.SaveAsync(file)` per file in a loop, with per-file magic-byte + 10 MB validation (reuse `LocalRecipePhotoStorage` / `ImageMagicBytes` **verbatim**), progressive per-file UI feedback, and a per-file try/catch so one bad file doesn't abort the batch. **Never buffer all files at once** — each file stays under the 12 MB SignalR `MaximumReceiveMessageSize` (`Program.cs:34`), keeping the circuit connected (P14).
- **D-14-06 (reorder):** Reordering and "set as primary" use **explicit move-up / move-down + "Set as hero" buttons**, not HTML5 drag-and-drop. Rationale: matches the v1.2 editor's keyboard-navigable immutable-id reorder convention and the A11Y carry-forward (keyboard-only nav, 2px focus rings); avoids fragile Blazor-Server drag-drop JS interop over SignalR. Drag-and-drop is a **deferred** polish item.

### AI photo helper contract (GALLERY-04; pitfall P12)
- **D-14-07-ai:** The helper is a **button** ("Suggest photo search terms"), gated by `AiFeaturesEnabled && UserProfile.AiEnabled` (the exact combined gate already in `RecipeEditor.razor:519-521`). It sends the recipe **text** (name/description/key ingredients) and returns **plain text only**: a one-line dish description + 3–5 suggested search phrases + a short list of free-licensed sites to link out to (Unsplash / Pexels / Wikimedia Commons — *links only, no API calls, no keys*). The AI **MUST NOT** emit, embed, or auto-fill any image URL (P12). The user finds a photo, pastes its URL into the paste-URL field (D-14-08 validates it), or uploads a file.
- **D-14-08-transport:** Use the **existing text path on `IAiService`** (the same client `AiChat` uses) — **NOT** `IAiRecipeGenerator`/structured output (the result is free text, not a `RecipeDocument`) and **NOT** a vision/image-input path (none exists on `AnthropicAiService`; **do not add one** — out of scope, consistent with "no `Microsoft.Extensions.AI` / no second provider"). The exact method/signature is the researcher/planner's call.
- **D-14-09-disclaimer:** A **copyright disclaimer is visible on every photo input surface** (upload, paste-URL, and the AI helper output) — e.g. "Only add photos you have the right to use. AI suggestions are search terms only — verify the license at the source." Non-negotiable per GALLERY-04 and pitfall P12.

### Paste-URL HEAD-validation (GALLERY-04 — new capability this phase adds)
- **D-14-10:** GALLERY-04 requires the pasted URL be **HEAD-validated before persist**, and **no HEAD path exists today** (`RecipePhotoUrlValidator` only checks scheme/format). Phase 14 **adds** a validation step: (1) reuse `RecipePhotoUrlValidator` for the `http`/`https` scheme-allowlist first (defangs `javascript:`/`data:`/`file:`), then (2) issue an HTTP `HEAD` (reuse the existing `HttpClient` registration) with a **short timeout**, and accept only on **2xx + `Content-Type: image/*`**. Failure **blocks persist** with a clear error (GALLERY-04 makes HEAD a gate, not a warning).
  - **Implementation note for planner:** some CDNs reject `HEAD` with `405` — fall back to a tiny ranged `GET` (first bytes) on `405` before failing. Keep redirect-following conservative (trusted-LAN posture; the user initiates, but don't blindly chase redirects to internal hosts). Apply this same validation to the **existing single paste-URL** field too, for consistency.

### Delete / orphaned-file cleanup (GALLERY-03; pitfall P13)
- **D-14-11:** EF cascade delete removes `RecipePhoto` **rows** but never **files**. So file deletion is an **explicit service-layer step**: on single-photo delete and on **recipe delete**, enumerate the recipe's `RecipePhoto` rows, and for each whose `Url` is a **local `/uploads/` path**, delete the backing file — guarded by the existing **`AssertPathInsideUploadsDirectory`** check (`LocalRecipePhotoStorage.cs:118-134`). Rows whose `Url` is an external `http(s)://` reference have **no local file** → skip (GALLERY-03: "external paste-URL photos leave no local file to clean"). Recipe-delete must run this **before/within** the recipe-delete transaction so cascade doesn't orphan files. Missing-file deletes are non-fatal (log-and-continue).

### `.cookbook.json` export behavior (resolves STATE open question #2)
- **D-14-12:** **Omit photos from `.cookbook.json` entirely — no schema change.** Photos are already excluded today (`CookbookTransferRecipe` carries no `PhotoUrl`; `ToParsedRecipe` never populates it — confirmed in `CookbookTransferService.cs`). The new `RecipePhoto` rows are likewise **never added** to the transfer DTO. This honors the invariant ("Photos stripped from `.cookbook.json` exports — host-specific operational state") with zero transfer-DTO churn. A one-line "photos are not included in cookbook export/import" note in the export/import UI is **Claude's discretion** (nice-to-have, not required).

### Claude's Discretion
- Exact `RecipePhoto` column max-lengths (follow `PhotoUrl=2048` / `Description=4096` precedents — `Caption` ~512 proposed).
- Whether the backfill (GALLERY-01) runs as raw SQL in the EF migration `Up()` (`INSERT INTO RecipePhotos SELECT … FROM Recipes WHERE PhotoUrl IS NOT NULL`) **or** in `DatabaseSeeder` after `MigrateAsync` — recommend **migration-SQL** for atomicity with the schema change and forward-only consistency (the v1.1 `CanonicalDocumentJson` backfill precedent). Either way: one primary row per recipe, `SortOrder=0`, `IsPrimary=true`.
- Gallery UI layout in `RecipeView` (hero + thumbnail strip vs. grid) and `RecipeEditor` (the multi-photo management block placement) — refine at plan / `ui-phase` time. SC fixes *that* the gallery, reorder, captions, and primary selection appear, not the pixel layout.
- The exact AI-helper prompt wording and `IAiService` method (text-in/text-out contract is locked by D-14-07/08; wording is research/plan territory).
- Internal service shape — a dedicated `RecipePhotoService` vs. extending `RecipeService` (recommend a focused `RecipePhotoService` for upload/reorder/set-primary/delete + file cleanup, with `RecipeService` owning the `PhotoUrl` re-sync per D-14-01).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents (researcher, planner) MUST read these before planning or implementing.**

### Requirements & milestone decisions (authoritative)
- `.planning/REQUIREMENTS.md` §"Photo Gallery & AI Helper (GALLERY)" — GALLERY-01..04, the locked WHAT; plus the §"Out of Scope" / §"Future Requirements" photo bullets.
- `.planning/ROADMAP.md` §"Phase 14: Photo Gallery" — goal + 4 success criteria (SC1–SC4).
- `.planning/STATE.md` §"Accumulated Context" — **Hard Invariants** ("Photo paths never stored in CanonicalDocumentJson"; "Display-only layers never mutate canonical"), **Key v1.4 Decisions** (`RecipePhoto` entity table not canonical-doc array; AI photo helper = search-term suggestion only), **Pitfall Guards** P12 (AI hallucination), P13 (orphaned files), P14 (SignalR multi-upload), P15 (canonical mutation), and the Build-Order chain (Phase 14 depends on Phase 12; feeds Phase 15).
- `.planning/research/SUMMARY.md` — v1.4 research synthesis (photo-count cap guidance, zero-new-NuGet consensus).

### Codebase precedents to copy / reuse (the v1.3 photo pipeline is the template)
- `src/CookBot.Web/Services/LocalRecipePhotoStorage.cs` — **reuse verbatim** for per-file persist; `wwwroot/uploads/{guid}{ext}` scheme; `MaxUploadBytes = 10 MB`; magic-byte sniff; **`AssertPathInsideUploadsDirectory` (lines 118-134)** for safe file deletion (D-14-11).
- `src/CookBot.Web/Services/ImageMagicBytes.cs` — JPEG/PNG/GIF/WebP content sniffing (reuse per-file).
- `src/CookBot.Application/Services/RecipePhotoUrlValidator.cs` — `http`/`https` scheme allow-list; **reuse as step 1** of the new HEAD-validation (D-14-10) and on the AI-output scrub path.
- `src/CookBot.Web/Components/Pages/RecipeEditorParts/RecipePhotoComposite.razor` — the existing single-photo upload + paste-URL composite (`InputFile OnChange`, pre-stream size check line ~181, paste validation line ~153); evolve/clone into the multi-photo manager.
- `src/CookBot.Web/Components/Pages/RecipeEditor.razor` — photo block at top (lines 76-87); the combined AI gate `_aiOn = hostOn && userOn` (lines 519-521); save via `RecipeService.Create/UpdateAsync`.
- `src/CookBot.Web/Components/Pages/RecipeView.razor` — hero photo render (lines 133-150: `referrerpolicy="no-referrer"`, `loading="lazy"`, one-shot `@onerror` → `_heroPhotoFailed`); extend into the gallery (hero = primary `RecipePhoto`).
- `src/CookBot.Infrastructure/Data/Configurations/RecipeConfiguration.cs` (line 29, the `HasMany(RecipeIngredients).WithOne().HasForeignKey().OnDelete(Cascade)` relational pattern) + `RecipeIngredientConfiguration.cs` (FK + index conventions) — the template for `RecipePhotoConfiguration`.
- `src/CookBot.Infrastructure/Migrations/20260516032653_AddRecipePhotoUrlAndDescription.cs` — the most recent column-add migration; the `RecipePhoto` table-add + backfill migration follows this shape. `CookBotDbContext.cs` uses `ApplyConfigurationsFromAssembly`.
- `src/CookBot.Domain/Entities/Recipe.cs` (line 19, `PhotoUrl`) — add `ICollection<RecipePhoto> Photos`; `Recipe.cs` is also where the new entity's navigation lives.
- `src/CookBot.Application/Services/RecipeService.cs` (`CreateAsync` ~48-56, `UpdateAsync` ~172-183) — where `PhotoUrl` is written to entity + canonical JSON; the **single owner of canonical writes** (D-14-01 re-sync goes here; P15 — projectors/gallery service never set `CanonicalDocumentJson`).
- `src/CookBot.Application/DTOs/CookBotSettings.cs` (line 25-26, `DatabaseBackupRetention` clamped-int) — home + precedent for `MaxPhotosPerRecipe` (D-14-04-cap).
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` + `src/CookBot.Application/AI/` (`IAiService`) — the text AI path for the helper (D-14-08); **no vision path exists and none is to be added**.
- `src/CookBot.Web/Services/CookbookTransferService.cs` (`BuildExportAsync`, `ToParsedRecipe`) — confirms photos are already excluded; **leave the transfer DTO unchanged** (D-14-12).

### Phase 13 integration (must not regress)
- `src/CookBot.Application/.../JsonLdRecipeProjector.cs` (Phase 13) — reads `PhotoUrl` for the JSON-LD `image` (absolute-HTTPS-only). D-14-01 keeps `PhotoUrl` synced to the primary photo so this **needs no change** and the Phase 15 "gallery hero feeds image" requirement is satisfied automatically. Re-confirm by reading `13-01-SUMMARY.md`.

### Tests / harness to extend
- `tests/uat-harness/` — the Playwright harness; Phase 16 (UATAUTO-02) will add gallery checks, but add component/service tests this phase for: backfill (no data loss), one-primary invariant, sequential upload, file-cleanup-on-delete, HEAD-validation accept/reject, AI helper never returns a URL.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`LocalRecipePhotoStorage` + `ImageMagicBytes`**: the entire safe-persist pipeline (GUID filename, 10 MB cap, magic-byte sniff, path-traversal guard) is reused per-file for multi-upload — no new upload primitives needed.
- **`RecipePhotoUrlValidator`**: scheme allow-list shipped in v1.3; reused as step 1 of the new HEAD-validation and on the AI-output scrub path.
- **`AssertPathInsideUploadsDirectory`** (`LocalRecipePhotoStorage.cs:118-134`): the guard that makes file *deletion* safe (D-14-11).
- **Combined AI gate** `hostOn && userOn` (`RecipeEditor.razor:519-521`): reused verbatim to gate the AI helper button.
- **`DatabaseBackupRetention` clamped-int setting**: the precedent for `MaxPhotosPerRecipe`.
- **`RecipeIngredient` relational FK + cascade**: the precedent for the `RecipePhoto` table config.

### Established Patterns
- **`RecipeService` is the single owner of `CanonicalDocumentJson` writes** — the `PhotoUrl` re-sync (D-14-01) lives there; the gallery/photo service and any projector never touch canonical (P15).
- **12 MB ceiling at all three Blazor boundaries** (Kestrel / Forms / SignalR, `Program.cs:25,26,34`) — already in place; sequential per-file upload (D-14-05) keeps each frame under it (P14).
- **Hero `<img>` hardening** (`referrerpolicy="no-referrer"`, `loading="lazy"`, one-shot `@onerror`) — apply to every gallery image.
- **`.cookbook.json` already strips photos** — additive gallery rows continue that; no transfer-DTO change (D-14-12).
- **Dual-source URL** (local `/uploads` path *or* external `http(s)`) — `RecipePhoto.Url` follows the same model as today's `PhotoUrl`; file-cleanup only touches local paths.

### Integration Points
- New `RecipePhoto` table (FK → `Recipe`, cascade) + `Recipe.Photos` navigation; EF migration adds the table **and** backfills the existing `PhotoUrl` (GALLERY-01).
- `RecipeService` re-syncs `Recipe.PhotoUrl` ← primary `RecipePhoto.Url` (→ canonical JSON) on every gallery mutation; this is what keeps the Phase 13 JSON-LD `image`, RecipeView hero, Home thumbnails, and cookbook collage all correct with no rewiring (D-14-01).
- `RecipeEditor` gains the multi-photo manager (upload/reorder/caption/primary + AI helper button); `RecipeView` gains the gallery (primary as hero + thumbnail strip).
- AI helper → existing `IAiService` text path (no vision, no structured output); output is plain text only.
- Recipe-delete path must run photo-file cleanup before cascade (D-14-11).

</code_context>

<specifics>
## Specific Ideas

- Gallery in `RecipeView`: **primary as the existing 420px hero**, the rest as a thumbnail strip beneath; clicking a thumbnail swaps the hero view (ephemeral, client-side). Reuse the striped placeholder for the empty state.
- Editor management block: a row/grid of photo cards, each with caption input, move-up/down, "Set as hero" (radio-like, exactly one), and delete; "Add photos" `<InputFile multiple>` + paste-URL input + the AI "Suggest search terms" button, all under one copyright disclaimer line.
- AI helper output reads like guidance, not a link dump: *"Looks like a rustic sourdough loaf — try searching: 'sourdough boule crumb', 'artisan bread dark crust'. Free-licensed sources: Unsplash, Pexels, Wikimedia Commons."*

</specifics>

<deferred>
## Deferred Ideas

- **HTML5 drag-and-drop reorder** — replaced by move-up/down buttons for v1.4 (accessibility + SignalR-interop simplicity); revisit as polish.
- **Per-step photo linking** (Paprika-style `[photo: name]`) — explicitly out of scope per REQUIREMENTS.md "Future"; high complexity.
- **Unsplash/Pexels API integration** for in-app search/backfill — adds an external API-key dependency that conflicts with the self-host posture; the helper only suggests terms + links out.
- **AI reverse-image / vision "find a photo for this dish"** — no vision path on `AnthropicAiService` and none to be added; the text search-term helper is the v1.4 shape.
- **Including photos in `.cookbook.json`** (with re-download on import) — deliberately not done (host-specific state, copyright re-download risk); revisit only if cross-host photo portability becomes a requirement.
- **Strict interpretation (B)** of the canonical invariant (remove `PhotoUrl` from canonical, rewire all readers to `RecipePhoto`) — noted under D-14-01; only pursue if the user explicitly prefers it over the low-risk mirror approach.

### Reviewed Todos (not folded)
None — no pending todos matched Phase 14 scope (`todo.match-phase 14` → 0 matches).

</deferred>

---

*Phase: 14-photo-gallery*
*Context gathered: 2026-06-07*
