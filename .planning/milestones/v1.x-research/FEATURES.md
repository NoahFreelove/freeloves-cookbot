# Feature Research — v1.3 Production-Ready & Format Maturity

**Domain:** Self-hosted recipe authoring + AI-assisted cooking app (FreelovesCookBot)
**Researched:** 2026-05-15
**Milestone scope:** v1.3 — Schema v3 + Photos, Format cleanup, QOL, Small-stuff polish, Prod-ready self-hosters
**Overall confidence:** HIGH for ecosystem photo/backup patterns (verified via WebFetch against live docs); MEDIUM for pantry-match scoring (no app documents their algorithm publicly); MEDIUM for token-cost telemetry (Anthropic Admin API requirements confirmed HIGH, per-request tracking approach confirmed HIGH)

This document answers "what does good look like in 2026" for each of v1.3's five buckets. It does NOT re-research already-shipped features. Existing features are treated as constraints (e.g., `RecipeDocument` v2, `IRecipeMadeService`, `AiApiKeyResolutionService`).

---

## Bucket 1 — Schema v3 + Photos

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Single hero photo on recipe** | Every recipe app surveyed (Paprika, Mealie, Tandoor, Cooklang viewers) shows a hero image above the title or below it. Its absence is the most visible gap in the current UI (v1.2 placeholder already says "photo · 4:3 (coming soon)"). | S | Already designed into RecipeView hero slot and RecipeEditor placeholder. Wire-up only. |
| **File upload for photos** | Paprika (iOS guide, verified via WebFetch) exclusively uses file upload — camera, photo library, clipboard. "You can add multiple photos to a recipe. They will be resized to a max size of 2048×2048 pixels." Users expect to upload a file, not paste a URL. Paste-URL is the MVP workaround; file upload is what every consumer app ships. | M | Requires `<InputFile>`, size cap, content-type validation, `wwwroot/uploads/` directory, `IFormFile` pipeline. The v1.3-PHASE-CANDIDATE flipped "file upload is out" — PROJECT.md confirms file upload is now in scope. |
| **onerror fallback to placeholder** | External URLs 404, photos get deleted. Every app that renders an `<img>` from an external URL needs an `onerror` handler. This is so table-stakes it's rarely documented — it's just expected. Paprika handles it via local storage (no external URL issue); browser apps using URL references must handle it in HTML. | S | Already planned in IMG-10/11. Use `onerror="this.style.display='none'"` + sibling `<StripedPlaceholder>` or the CSS hidden-sibling pattern. |
| **`recipe.Description` field** | Schema.org Recipe has `description` as a recommended property. Mealie shows a recipe description/notes block below the title. Paprika shows a "note" textarea. The RecipeEditor already has a description input wired in markup (D-25) with no backend column — users have been typing into a field that discards on save. That is a trust-breaking bug. | S | One migration column + one upcaster step. Wires directly into the V2→V3 schema bump alongside photos. |
| **Per-step temperature surfaced in cooking mode** | Baking recipes routinely change oven temperature between steps (preheat vs. reduce vs. broil). Apps like SideChef and Kitchen Stories surface step-level oven temperature instructions prominently. Cooklang does not treat temperature as a structured field (verified via WebFetch of the spec — temperature is plain text only). Schema.org's `HowToStep` also has no structured temperature field. **CookBot would be ahead of the current recipe format ecosystem by making it a first-class structured field.** | M | New `temperature?: { value: number, unit: "F"|"C"|"gas" }` on `ContentStep`. V2→V3 upcaster sets null. Cooking mode shows it as a chip beside the timer chip. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Temperature range support** (`tempMin`/`tempMax` or a single `range: [325, 375]`) | Real recipes use ranges: "350–375°F until deeply browned." Paprika stores temperatures as plain text only. Mealie and Tandoor have no per-step temperature field at all. A structured range (`{ min: 350, max: 375, unit: "F" }`) lets cooking mode show "350–375°F" as a chip and lets the AI emit a precise semantic value. | S | Add `temperatureMin` + `temperatureMax` alongside `temperature` on `ContentStep`, OR use a single `temperature` object with optional `max` property. Either fits the V2→V3 schema bump. |
| **"Not scaled" badge on temperature chip** | Already identified in v1.1 FEATURES.md as a differentiator. Doubling servings does not change oven temperature. The UI should make this explicit ("350°F — not scaled"). No consumer app surveyed does this. | S | Trivial once the temperature field exists. Badge in cooking mode only requires a conditional CSS class. |
| **`onerror` fallback that is graceful in the editor** | In the editor only (not read-only RecipeView), when a URL fails to load, show a subtle "Photo URL appears broken — try another?" inline note rather than a silent empty space. | S | Small UX improvement over the silent onerror fallback. Only in edit context. |

### Anti-Features

| Anti-Feature | Why Avoid | Alternative |
|--------------|-----------|-------------|
| **Multiple photos / gallery** | Gallery UI requires carousel, lightbox, ordering controls, thumbnail strip. Mealie has multi-photo but it required significant UI work. The v1.3 goal is a single hero. Ship the slot, not the gallery. | Single hero now; gallery in v1.4+ if requested. |
| **Thumbnail generation / resizing server-side** | Requires ImageSharp or SkiaSharp, adds a GPL-incompatible or LGPL dependency, needs background processing. Paprika resizes to 2048×2048 but they control the binary. Browser CSS `object-fit: cover` with a fixed aspect-ratio container handles display-side cropping adequately for a trusted-LAN app. | CSS `aspect-ratio: 4/3; object-fit: cover` on the `<img>`. |
| **CDN / image proxy integration** | Public CDN is cloud, not trusted-LAN. Privacy-wise, proxying breaks the `referrerpolicy="no-referrer"` benefit (the host's IP becomes the referrer, not the user's). | Local file upload (`wwwroot/uploads/`) is the privacy-preserving path; paste-URL is a convenience. |
| **Cookbook cover photo as separate field** | Adds another schema field, another migration, another upload surface. The Cookbook collage thumbnail already samples from recipe photos (IMG-13 stretch). That's sufficient visual identity. | Use recipe photo sampling for the collage. Dedicated cookbook cover is v1.4+. |
| **EXIF/metadata strip from uploaded photos** | The app never reads the file bytes for anything other than serving them. EXIF doesn't execute; it doesn't embed credentials. Stripping it adds a Sharp/SkiaSharp dependency for zero user-visible benefit on a trusted LAN. | Document in the privacy section of the README: "Upload photos from the app; strip EXIF in your camera app if privacy matters." |
| **Reverse-image-search / "find a photo for this recipe" AI feature** | AI call per recipe, potentially large token spend, complex UI. Separate AI-feature phase. | Defer to v1.4+. |

---

## Bucket 2 — Format Cleanup

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **`LegacyRecipeProjector` deletion** (FUTURE-V1.1-03) | It is a documented deletion-target. Keeping dead code in the repo means future contributors (or the AI) might revive it. Once V2→V3 upcaster exists, the projector's only remaining consumer is the upcaster itself — and even that should be ripped out and replaced with a proper V1→V2 migration step. | S | Confirm zero callers via `grep` before deletion. |
| **`TagsJson` → relational `RecipeTag`** (FUTURE-V1.1-02) | `TagsJson` is a JSON column on `Recipe` — the same denormalized-storage anti-pattern that `CanonicalDocumentJson` replaced for the recipe body. It means tags can't be queried, filtered, or indexed without JSON functions. Moving to a relational `RecipeTag` join table is standard EF Core practice and unlocks filtering. | M | EF migration: add `RecipeTags` table, backfill from `TagsJson`, drop `TagsJson` column. Domain: `RecipeTag` entity. Application: update CRUD. Upcaster: tags now come from the join table, not the JSON column. |
| **Prompt-snapshot regression test** (FUTURE-V1.1-04) | The system prompt string is the most fragile artifact in the codebase — it lives in `PromptBuilderService` and changes in format or wording can silently break AI conformance. A snapshot test (serializes the output of `PromptBuilderService.BuildSystemPrompt(testProfile, testPantry)` and diffs against a checked-in `.txt` file) catches accidental regressions. | S | xUnit + `Verify` NuGet or hand-rolled snapshot comparison. |
| **README "Recipe Format" section** (FUTURE-V1.1-05) | A self-hostable app needs a human-readable description of its data format for contributors, third-party tool authors, and users who want to understand their `.cookbook.json` files. Currently there is nothing. | S | Markdown doc, not code. Covers the YAML wire format, the V3 schema fields, and the `.cookbook.json` envelope. |

### Anti-Features

| Anti-Feature | Why Avoid | Alternative |
|--------------|-----------|-------------|
| **Keeping `TagsJson` as a convenience column alongside `RecipeTag`** | Dual-write bugs. Consistency requires every tag mutation to hit both columns. | Full migration: backfill → switch reads → drop column. One-way. |
| **Generating README from code annotations** (XML docs / Swagger) | There is no Web API; there is nothing to generate from. Recipe format docs are human-authored prose, not API documentation. | Hand-write the README section. Keep it close to the `PromptBuilderService.DefaultTemplate` string so changes are visible. |

---

## Bucket 3 — QOL

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Smart pantry-match algorithm replacing the deterministic stub** (FUTURE-13) | The Home "Tonight from your pantry" section is the highest-visibility surface in the app. The v1.2 stub is explicitly a placeholder — users have already noticed it doesn't use real pantry data. The research baseline (Cooklang's greedy coverage algorithm, SuperCook's ingredient-unlock approach) establishes that **ingredient-coverage percentage is the standard scoring axis**: "what fraction of this recipe's ingredients do I have?" SuperCook shows partial matches; Mealie's pantry filtering does the same via a basic ingredient intersection check (no documented algorithm beyond that). A score of `pantryMatches / totalIngredients` with a minimum threshold (≥50% coverage) is the industry baseline. | M | Depends on `PantryItem` (existing) and `Recipe.Ingredients` (existing). Does NOT require `IRecipeMadeService`. `IRecipeMadeService` enables the debounce enhancement (see differentiators). |
| **AiChat "Edit anyway" path hardened** (FUTURE-15, WARN-AICHAT-RAW-EDIT-EDGE) | The v1.2 audit flagged this as a known fragile edge: when AI emits a recipe that fails validation and the user clicks "Edit anyway," `RawResponse` goes through `IRecipeFormatParser.TryParse` — if that also fails, the flow silently toasts "Could not parse the draft" with no navigation. Users lose AI-generated content. A modal that shows the raw text and lets them edit it before submitting to the parser is the standard recovery UX. This is table-stakes for an AI-first app. | M | Requires a new `RawEditDialog.razor` or an existing dialog with a `<CbTextarea>` bound to the raw response. On submit, re-attempt parse. If parse succeeds, proceed to SaveRecipeDialog. If parse fails again, show inline error on the textarea. |
| **Accent variant picker** (FUTURE-14 — terracotta/sage) | The v1.2 design tokens already wire the CSS accent variables (DS-02). Users expect a way to change the accent color. The pattern is well-established: Catppuccin's accent picker (mauve, red, peach, green, teal, blue, lavender) and Material You's dynamic color system both use a row of color swatches with instant in-page preview. The minimal version is 2–3 named accents (terracotta/orange, sage/green, slate/neutral) as CSS class toggles on `<html>`, stored in `localStorage` alongside `cookbot_dark_mode`. | S | DS-02 token structure already exists. No migration needed. localStorage only. |
| **Profile-side AI prompt editor** (DEFERRED-PROF-AIPROMPT) | `UserProfile.AiSystemPromptTemplate` is already loaded by `BuildSystemPrompt` but has no editing UI. Cursor and Cline expose system-prompt customization as a plain textarea with variable documentation. The pattern: show the current template with `{{variable}}` tokens highlighted, provide a "reset to default" button, and show a live preview of the expanded prompt. For CookBot, the variables are `{{experience_level}}`, `{{unit_system}}`, `{{equipment}}`, `{{dietary_preferences}}`, `{{pantry}}`, `{{recipe_format}}` — these already exist in `PromptBuilderService.DefaultTemplate`. | M | Expands the existing Profile page. Requires a `<CbTextarea>` bound to `UserProfile.AiSystemPromptTemplate`, a variable reference panel, and a save path through `UserService`. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Recently-cooked debounce in pantry-match** | Recipes made in the last 7 days are deprioritized in the "Tonight from your pantry" list, even if pantry coverage is high. Prevents serving the same recipe every night just because the fridge happens to have eggs. Depends on `IRecipeMadeService.GetLastCookAsync` (already exists from v1.2 slice 09). No app surveyed documents this behavior, but it's the obvious next step after coverage-based scoring. | S | Add `recentlyMadeBonus` weight: sort by `(coverageScore * 1.0) - (recentlyMadePenalty * 0.3)`. `recentlyMadePenalty = 1.0` if cooked in last 7 days, `0.5` if 8–14 days, `0` otherwise. |
| **Dietary-filtered pantry-match** | The user's `UserProfile.DietaryPreferences` (already a multi-select chip row from v1.2) can pre-filter recipes before scoring. A vegetarian user should not see pork chop suggestions regardless of pantry coverage. SuperCook does this implicitly via recipe tags. Mealie has dietary filtering on the recipe list but not on the "what can I cook" suggestion. | S | Adds a pre-filter step before coverage scoring. Depends on `RecipeTag` (which the format-cleanup bucket creates). |
| **Prompt injection warning on AI prompt editor** | When a user saves a custom prompt template, show a low-key info notice: "Custom prompts apply to your AI sessions only. Avoid including personal information or API keys in the template." Cursor and Cline show no such warning — they trust developer users. CookBot has non-technical users sharing a LAN. | S | Static info callout below the textarea. No active scanning. Reference `PromptInjectionGuard` behavior in the tooltip text. |
| **Variable token highlight in prompt editor** | In the prompt template textarea, `{{variable}}` tokens appear in the accent color (or with a distinct background) so users can see at a glance what will be expanded. Requires either a contenteditable div with highlight overlay or a side-by-side "preview" panel. | M | Side-by-side live preview is simpler to implement safely (no raw innerHTML injection from user input). The preview panel calls `PromptBuilderService.ResolveTemplate` with a mock profile. |

### Anti-Features

| Anti-Feature | Why Avoid | Alternative |
|--------------|-----------|-------------|
| **Expiration-weighted pantry-match scoring** | Tracking expiry dates requires adding `ExpiresOn` to `PantryItem`, a new UI for entry (per-item date picker), and ongoing maintenance burden. No app other than dedicated pantry apps (KitchenPal, NoWaste.ai) makes expiry a core feature — and those apps have barcode scanners to auto-fill it. Manual expiry entry on a trusted-LAN cooking app is unlikely to see consistent use. | Use coverage + recency debounce. Mention expiry as a future enhancement if user-requested. |
| **"What can I cook" AI call per pantry refresh** | Sending pantry contents to the AI to generate recipe suggestions every time the home page loads is expensive (token cost on every page load) and slow (LLM latency on navigation). SuperCook is deterministic; Mealie is deterministic. | Deterministic scoring: coverage + recency + dietary filter. Fast, free, explainable. |
| **`{{cookbook_recipes:ID}}` expansion in the user-editable template** | The cookbook-recipe token is expanded by the Razor page, not `PromptBuilderService.ResolveTemplate`. Exposing it in the editable template creates an expectation that users can type it anywhere — but the expansion only works in `AiChat.razor`. | Document it as a "chat-only" token in the variable reference panel, not in the profile editor. |
| **Prompt-injection scanning of the user's custom template** | False positives on legitimate recipes that happen to mention "ignore previous instructions." Trust-LAN users are known. | `PromptInjectionGuard` wraps external content (`<recipe>` tags), not the user's own system prompt. Static info callout is sufficient. |
| **OS-following / system-default accent color** | Material You dynamic color extraction from the wallpaper is a native Android feature. CSS `color-scheme` and `prefers-color-scheme` apply to light/dark, not accent. There is no browser API to extract the user's system accent color in a cross-platform way. | Two or three hardcoded named accents plus the existing dark-mode toggle cover the personalization use case. |

---

## Bucket 4 — Small-Stuff Polish

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Cookbook reparenting on edit** (D-26) | The cookbook switcher is visible on the RecipeEditor right rail and does nothing on edit. A visual affordance that cannot be activated is confusing. | S | `RecipeService.UpdateAsync` needs a `cookbookId` parameter and an ownership check: the new cookbook must belong to the same user. |
| **Pantry per-row quick-add to grocery** (D-37) | The cart icon exists and is disabled. The grocery-add flow (GR-01..04) is already implemented. A disabled button implies an incomplete feature, which erodes trust. | S | Wire the row-level cart icon to `GroceryListService.AddItemAsync`. |
| **Moon glyph for dark-mode toggle** (D-15) | The 36-icon set has a sun but no moon. The tooltip covers the directional cue but accessibility users (and keyboard nav users) see "toggle" with no visual indicator of current state. Adding a moon icon or toggling the sun icon fill vs. outline is the industry standard. | S | Add a moon SVG to the icon set OR use filled/outline variants of the existing sun icon. |
| **TopBar RightSlot passthrough** (D-16) | RV-05 actions render inline above the hero because MainLayout cannot pass a RightSlot through to individual pages. This works but it's not the designed behavior. | S | Pass a `RenderFragment` from each page through a `CascadingValue` or a layout parameter. |
| **Home active-timer live JS tick** | The timer band reads from `localStorage` snapshot on render; remaining seconds do not update without a page reload. Any other app showing a countdown timer would tick in real time. This was explicitly called out in the v1.2 audit as a punch-list item. | S | `setInterval` in JS that updates the DOM span containing remaining seconds. Wire back via DotNet object reference or just update the DOM directly (simpler, no StateHasChanged overhead for a tick counter). |

### Anti-Features

| Anti-Feature | Why Avoid | Alternative |
|--------------|-----------|-------------|
| **Real-time multi-user timer sync** | SignalR groups for shared cooking sessions would require a session model, user discovery, and conflict resolution. Trust-LAN with 2–4 users doesn't need synchronization — they're in the same kitchen. | Per-device `localStorage` timer state is sufficient. |

---

## Bucket 5 — Prod-Ready for Self-Hosters

### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Dockerfile + docker-compose with persistent volumes** | Mealie, Tandoor, and every serious self-hosted app ships a `docker-compose.yml` with named volumes for data. The 2026 "first 5 minutes" standard for a self-hosted app is: `git clone`, `cp .env.example .env`, `docker compose up -d`, open `http://localhost:PORT`. Without Docker, potential self-hosters must install .NET 10 locally — a significant barrier. The `wwwroot/uploads/` directory (new in v1.3 for photos) and `cookbot.db` both need persistent volume mounts. | M | Dockerfile: `FROM mcr.microsoft.com/dotnet/aspnet:10.0`, multi-stage build with `dotnet/sdk:10.0`. `docker-compose.yml`: two named volumes (`cookbot_db`, `cookbot_uploads`). App reads `COOKBOT_DB_PATH` env var to override `Data Source=cookbot.db`. |
| **Default admin user creation on first run** | Mealie creates a default admin user (`changeme@example.com` / `MyPassword`) on first boot and prompts the user to change it immediately. The `DatabaseSeeder.SeedAsync` already creates a "Home Chef" admin user — this just needs to be documented, and the first-run flow should show a profile-completion prompt if the default password is still set. | S | Document the default credentials. Add a one-time "complete your profile" banner on Home if user is the seeded default. |
| **No-AI-key first-run flow** | Mealie requires a $5 OpenAI deposit for AI features but launches fine without it. CookBot already has `CookBotSettings.AiFeaturesEnabled` and `UserProfile.AiEnabled`. The gap is documentation: new self-hosters need a clear README section that says "AI features are off by default — add your Anthropic key in Profile to enable them." The app itself already handles the gate gracefully. | S | README section. Optionally add a one-time toast on first login: "AI features are disabled. Add your API key in Profile to enable them." |
| **Encrypt-at-rest for `UserProfile.AiApiKey`** (FUTURE-01) | `AiApiKey` is stored as plaintext in SQLite today. For a self-hosted app where the database file lives on disk (and may be backed up to a NAS, cloud storage, or git repo by the user), plaintext API keys in the DB are a genuine risk. The ASP.NET Core Data Protection API with an EF Core value converter (`IProtector.Protect` / `Unprotect`) is the standard approach for field-level encryption in .NET apps (confirmed via Microsoft Learn docs). The encryption key is derived from the machine's Data Protection key ring, not stored in the DB. | M | EF Core value converter on `UserProfile.AiApiKey`. Existing key-sharing logic (`AiApiKeyResolutionService`) reads the decrypted value server-side — no changes to callers. The key is never returned to the client. Migration: existing plaintext values must be re-encrypted on first startup (one-time migration step in `DatabaseSeeder`). |
| **README install/config/backup/upgrade sections** | Every self-hosted app (Mealie, Tandoor, n8n, Gitea) has an installation guide in the README or docs site. The minimum sections: Prerequisites, Docker quick-start, Environment variables reference, First-run setup, Backup strategy, Upgrading (pull → compose up). Without this, the app cannot be picked up by a new self-hoster. | S | Markdown authoring. Backup section should reference `IDatabaseBackupService` behavior (auto-backup on migration) and recommend mounting `cookbot_db` to a host path that is included in the user's backup strategy. |
| **Per-key-owner token-cost telemetry** (FUTURE-02) | When one user shares their API key with others, the key owner incurs costs on behalf of recipients. Currently there is no visibility into this. The Anthropic Usage Admin API (`/v1/organizations/usage_report/messages`) provides org-level breakdowns by API key ID — but it requires an **Admin API key** (`sk-ant-admin...`), which differs from the standard user API key. **This is a significant constraint**: individual Anthropic accounts without an organization cannot use the Admin API (confirmed via official Anthropic docs). The practical alternative for a single-user or small-group self-hosted app is **application-level telemetry**: log `usage.input_tokens` + `usage.output_tokens` from each Anthropic API response (these are in the response body — confirmed via Anthropic API docs) to a `TokenUsageLog` table keyed by `UserId` and `EffectiveKeyOwnerId`. | M | New `TokenUsageLog` entity: `Id`, `UserId`, `KeyOwnerId`, `ModelId`, `InputTokens`, `OutputTokens`, `CreatedAt`. `AnthropicAiService` already returns the full response — add `usage.input_tokens` + `usage.output_tokens` extraction. A "My API Usage" section on the Profile page shows a per-user rolling 30-day total and a cost estimate (tokens × published pricing). Cost estimate is approximate; link to Anthropic's pricing page. |
| **Backup strategy documented and accessible** | Mealie ships a UI backup button at `/admin/backups` that produces a `.zip` of the DB + media files (verified via WebFetch). Tandoor relies on Docker volume mounts and documents the volume path. For CookBot v1.3, the minimum is: document that `cookbot_db` volume = the backup target, and that `cookbot_uploads` volume = photo backup target. Optionally expose a "Download backup" button that streams a `.zip` of `cookbot.db` + `wwwroot/uploads/` to the browser. | S–M | UI backup button: `S` if it just downloads the SQLite file (already possible via `IDatabaseBackupService`). `M` if it zips DB + uploads folder. At minimum, the README must document the volume paths. |

### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Sample-data toggle in docker-compose** | An `COOKBOT_SEED_SAMPLE_DATA=true` environment variable that pre-populates the DB with 5–10 example recipes, covering a range of cuisines and techniques. Helps new self-hosters evaluate the app without creating content from scratch. Mealie has no sample-data toggle; Tandoor has import-demo-data in admin. | S | Extend `DatabaseSeeder.SeedAsync` to check the env var and call a `SampleDataSeeder` that inserts example `RecipeDocument` v3 records. Idempotent (check for existing sample recipes before inserting). |
| **"Encrypt-at-rest" status badge on Profile** | After encrypting `AiApiKey`, surface a subtle "API key stored encrypted" badge on the API Key card in Profile. Trust signals matter to privacy-conscious self-hosters even when they cannot verify the implementation. Similar to how password managers show "secured with AES-256." | S | One-line UI addition to the existing API key card. Conditional on `COOKBOT_ENCRYPTION_ENABLED=true` env var (allows opt-out for debugging). |
| **Token-cost display on shared-key recipient cards** | In the `SharedKeysDialog.razor`, show the key owner their rolling 30-day token cost broken down by recipient. "Noah: 42,000 tokens (~$0.06)". This gives the key owner visibility into how their key is being used without revoking access. | S | Reads from `TokenUsageLog`. Aggregate by `KeyOwnerId` + `UserId`. Cost estimate: `(inputTokens / 1_000_000 * inputPrice) + (outputTokens / 1_000_000 * outputPrice)` using the model's published per-million-token rate. |

### Anti-Features

| Anti-Feature | Why Avoid | Alternative |
|--------------|-----------|-------------|
| **Anthropic Admin API usage endpoint for telemetry** | Requires an `sk-ant-admin...` key, which only exists for organizations. A personal Anthropic account (the expected user profile) cannot provision one. Even if the user has an org, the Admin API requires storing a second credential type with elevated privileges. | Application-level `TokenUsageLog` from response body `usage` fields — no Admin API needed, more granular (per-conversation), available to all users. |
| **SQLCipher full-database encryption** | Replaces the standard SQLite driver with a third-party encrypted variant (`Zetetic.EntityFrameworkCore.SqlCipher`). Requires a database password at startup (env var or secure store), breaks the existing EF Core migration pattern, and means any DB inspection tool (DB Browser for SQLite) stops working without the password. Disproportionate complexity for a trusted-LAN app. | EF Core value converter on the single sensitive column (`AiApiKey`). Other data (recipes, pantry) is not sensitive in a trusted-LAN context. |
| **CI/CD pipelines** | Not requested, not in PROJECT.md scope. Adds GitHub Actions secrets, Docker Hub publishing, semver tag workflows. The author explicitly noted "CI/CD: no `.github/` workflows today; out of scope." | `./run.sh` for dev, `docker compose up` for prod. Document upgrade as `docker compose pull && docker compose up -d`. |
| **Public-facing rate limiting / WAF** | Trust-LAN posture means no public internet exposure. Hardening for public access (fail2ban, nginx rate limits, CSP headers) is out of scope and would give users a false sense of internet-exposure safety. | README: "CookBot is designed for trusted LAN use. Do not expose it to the public internet without additional hardening." |
| **User-facing "your key is encrypted with AES-256-CBC"** | Exposing implementation details of the encryption scheme gives attackers information without giving users meaningful assurance. The implementation might change. | "API key stored encrypted" badge without algorithm details. Link to the README if technically curious users want to audit the code. |
| **Online account creation flow** | Trust-LAN posture. Already in PROJECT.md Out of Scope. First-run creates a local admin user; additional users are added by the admin from within the app. | Document this in README "First-run setup" section. |
| **Email-based password reset** | No SMTP, no Identity middleware. Trust-LAN users know each other. | Admin user can reset another user's password from the admin UI. |
| **Cloud sync / remote backup** | Out of scope. Would require cloud credentials, a sync protocol, conflict resolution. | Volume mount to a host path that the OS backup tool (Time Machine, rsync, rclone) covers. |

---

## Feature Dependencies

```
[Schema v3 + Photos]
    ├── V2→V3 upcaster (carries PhotoUrl + Description + TemperatureField)
    │       └── All three schema additions ship in ONE upcaster step
    ├── File upload pipeline (wwwroot/uploads/) → needed for prod-ready Docker volumes
    │       └── Docker volumes (cookbot_uploads) ─enhances─> file upload
    └── Recipe.Description → closes D-25 (RecipeEditor description wired but not persisted)

[Format cleanup — TagsJson → RecipeTag]
    └── enables Dietary-filtered pantry-match (Bucket 3 QOL differentiator)

[Smart pantry-match] (FUTURE-13)
    ├── requires PantryItem (existing)
    ├── requires Recipe.Ingredients (existing via CanonicalDocumentJson)
    ├── enhanced by IRecipeMadeService (existing, v1.2 slice 09) ─→ recency debounce
    └── enhanced by RecipeTag (format cleanup bucket) ─→ dietary pre-filter

[Encrypt-at-rest AiApiKey] (FUTURE-01)
    └── does NOT break AiApiKeyResolutionService — decryption is in the EF value converter,
        transparent to callers. Existing share table logic unchanged.

[Token-cost telemetry] (FUTURE-02)
    └── requires AnthropicAiService response parsing (existing — usage fields already in response)
    └── enables token-cost display in SharedKeysDialog (differentiator)

[Docker + volumes]
    └── requires file upload pipeline (photos need a persistent mount)
    └── requires COOKBOT_DB_PATH env var override in appsettings / Program.cs

[Profile AI prompt editor]
    └── requires UserProfile.AiSystemPromptTemplate (existing column)
    └── requires PromptBuilderService.ResolveTemplate (existing method)
    └── enhanced by variable-token highlight (differentiator)
```

### Dependency Notes

- **Schema v3 bundle**: PhotoUrl + Description + TemperatureField ship in a single V2→V3 upcaster step. Doing them separately would require two upcaster steps and two AI-prompt regression passes. Bundle them.
- **TagsJson → RecipeTag precedes dietary pantry-match**: The pantry-match algorithm filters by `UserProfile.DietaryPreferences` against recipe tags. If tags are still in `TagsJson`, the filter requires JSON extraction in SQL — possible but fragile. Relational tags make it a clean JOIN.
- **Encrypt-at-rest does not break key sharing**: The value converter decrypts on read. `AiApiKeyResolutionService.ResolveAsync` reads `UserProfile.AiApiKey` via EF — the decryption is transparent.
- **File upload depends on Docker volumes**: If photos are stored in `wwwroot/uploads/`, that directory must be a persistent Docker volume. The upload feature and the Docker deployment are co-dependent for production use.

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Recipe hero photo (file upload) | HIGH | MEDIUM | P1 |
| Recipe.Description field | HIGH | LOW | P1 |
| V2→V3 upcaster (bundle) | HIGH | MEDIUM | P1 |
| LegacyRecipeProjector deletion | MEDIUM | LOW | P1 |
| TagsJson → RecipeTag | MEDIUM | MEDIUM | P1 |
| Dockerfile + docker-compose | HIGH | MEDIUM | P1 |
| README install guide | HIGH | LOW | P1 |
| Smart pantry-match (coverage score) | HIGH | MEDIUM | P1 |
| AiChat "Edit anyway" hardening | HIGH | MEDIUM | P1 |
| Encrypt-at-rest AiApiKey | MEDIUM | MEDIUM | P1 |
| Per-step temperature field | MEDIUM | MEDIUM | P1 |
| Cookbook reparenting (D-26) | MEDIUM | LOW | P1 |
| Pantry per-row grocery quick-add (D-37) | MEDIUM | LOW | P1 |
| Moon glyph dark-mode toggle (D-15) | LOW | LOW | P1 |
| Home active-timer live JS tick | MEDIUM | LOW | P1 |
| TopBar RightSlot passthrough (D-16) | LOW | LOW | P1 |
| Accent variant picker | MEDIUM | LOW | P2 |
| Profile AI prompt editor | MEDIUM | MEDIUM | P2 |
| Token-cost telemetry | MEDIUM | MEDIUM | P2 |
| Prompt-snapshot regression test | MEDIUM | LOW | P2 |
| README recipe format section | MEDIUM | LOW | P2 |
| Recency debounce in pantry-match | MEDIUM | LOW | P2 |
| Dietary-filtered pantry-match | MEDIUM | LOW | P2 |
| Temperature range (min/max) | LOW | LOW | P2 |
| Sample-data seed toggle | LOW | LOW | P3 |
| UI backup download button | LOW | MEDIUM | P3 |
| Variable-token highlight in prompt editor | LOW | MEDIUM | P3 |
| Token-cost display in SharedKeysDialog | LOW | LOW | P3 |

**Priority key:**
- P1: Must have for v1.3 — either closes a named carry-forward item or is core to the milestone goal
- P2: Should have — adds meaningful value within the milestone scope
- P3: Nice to have — defer if time constrained; carry to v1.4 if not shipped

---

## Competitor Feature Analysis

| Feature | Paprika | Mealie | Tandoor | CookBot v1.3 Approach |
|---------|---------|--------|---------|----------------------|
| Recipe hero photo | File upload + cloud sync, resize to 2048×2048 | File upload, stored in `/app/data/` volume | File upload, stored in `/opt/recipes/mediafiles` | File upload → `wwwroot/uploads/` volume + paste-URL coexists |
| Photo galleries | Multi-photo with gallery tap | Single hero (multi optional) | Single hero | Single hero only (v1.3) |
| onerror fallback | N/A (local) | Shown in `<img>` tags | Shows placeholder | `onerror` → `<StripedPlaceholder>` |
| Per-step temperature | Plain text detection only | None | None | Structured `{ value, unit }` field on `ContentStep` (first-class) |
| Recipe description | "Note" free-text field | Description/notes block | Notes block | `Recipe.Description` column + `RecipeDocument.Description` field |
| Pantry-to-recipe matching | None | Basic ingredient intersection | None (meal planner only) | Coverage score + recency debounce + dietary filter |
| AI system prompt editor | None | None | None | Profile page `<CbTextarea>` + variable reference |
| Backup UI | iCloud/Dropbox sync | Admin UI button → `.zip` download | Volume mount only | README documents volume; optional UI backup button (P3) |
| Token cost display | N/A (no LLM) | No (OpenAI key, no tracking) | No | `TokenUsageLog` table + Profile 30-day rolling total |
| Field-level encryption | iCloud Keychain (OS) | None | None | EF Core value converter on `AiApiKey` |
| Docker deployment | Not self-hosted | Official `docker-compose.yml` | Official `docker-compose.yml` | New `Dockerfile` + `docker-compose.yml` with named volumes |
| First-run admin setup | App Store install | Default credentials + UI redirect | Django createsuperuser | Seeded "Home Chef" user + "complete profile" banner |

---

## Sources

### Recipe photo UX — HIGH confidence (live docs verified via WebFetch)

- [Paprika User Guide for iOS](https://www.paprikaapp.com/help/ios/) — file upload from camera/library/clipboard, resize to 2048×2048, multi-photo supported; fetched 2026-05-15
- [Mealie Features documentation](https://docs.mealie.io/documentation/getting-started/features/) — AI image import (OCR), backup as zip with all assets; fetched 2026-05-15
- [Mealie Backup and Restore docs](https://docs.mealie.io/documentation/getting-started/usage/backups-and-restoring/) — UI backup at `/admin/backups`, manual trigger, `.zip` format; fetched 2026-05-15
- [Mealie OpenAI integration docs](https://docs.mealie.io/documentation/getting-started/installation/open-ai/) — no per-user cost display, cost tracking via OpenAI platform directly; fetched 2026-05-15
- [Tandoor Docker setup](https://docs.tandoor.dev/install/docker/) — `mediafiles` volume at `/opt/recipes/mediafiles`, nginx serves media in Tandoor 2+; search results 2026-05-15

### Per-step temperature and Cooklang — HIGH confidence (spec verified via WebFetch)

- [Cooklang Specification](https://cooklang.org/docs/spec/) — no structured temperature field; temperature is plain text only; verified via WebFetch 2026-05-15
- Schema.org `HowToStep` — no `temperature` property (reviewed via prior research); temperature is embedded in instruction text per convention

### Pantry-match algorithm — MEDIUM confidence (algorithm logic inferred; no app publishes their scoring)

- [Cooklang Greedy Coverage Blog](https://cooklang.org/blog/14-greedy-coverage-blog/) — greedy set-cover approach, ingredient coverage as primary score; fetched 2026-05-15
- [SuperCook](https://www.supercook.com/) — ingredient-unlock scoring, shows partial matches; product page only (algorithm not documented)
- Mealie pantry discussion threads (GitHub) — basic ingredient intersection; no documented weighting

### Token-cost telemetry — HIGH confidence (official Anthropic docs via WebFetch)

- [Anthropic Usage and Cost API docs](https://platform.claude.com/docs/en/manage-claude/usage-cost-api) — Admin API key required, unavailable to individual accounts; `usage.input_tokens` + `usage.output_tokens` in every response body; fetched 2026-05-15

### Encrypt-at-rest — HIGH confidence (Microsoft Learn docs)

- [ASP.NET Core Data Protection — Configure](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0) — EF Core value converter pattern, `IDataProtector.Protect/Unprotect`, AES-256-CBC default
- [Encrypt Your Database Columns with EF Core](https://medium.com/emrekizildas/encrypt-your-database-columns-with-entityframework-1f129b19bdf8) — practical value converter implementation (MEDIUM confidence — community source, aligns with official pattern)

### Prod-ready / Docker first-run — MEDIUM confidence (Mealie/Tandoor docs + general Docker patterns)

- [Mealie Installation Checklist](https://docs.mealie.io/documentation/getting-started/installation/installation-checklist/) — default credentials, env vars, first-run flow
- [Mealie Backend Configuration](https://docs.mealie.io/documentation/getting-started/installation/backend-config/) — `ALLOW_SIGNUP`, `DEFAULT_EMAIL`, Docker env var patterns

### AI prompt customization UX — MEDIUM confidence (Cursor/Cline community sources)

- [Cursor Custom System Prompt discussion](https://forum.cursor.com/t/how-to-modify-the-default-system-instructions-for-ai-in-cursor/27783) — plain textarea with rules files, no variable substitution UI
- [Cline Custom Instructions](https://github.com/instructa/ai-prompts) — plain textarea in extension settings; no variable highlight

---

*Feature research for: FreelovesCookBot v1.3 Production-Ready & Format Maturity*
*Researched: 2026-05-15*
*Confidence: HIGH for photo patterns, backup UX, Cooklang spec, token-cost API constraints; MEDIUM for pantry-match scoring (no app documents algorithm); MEDIUM for prompt editor UX patterns (Cursor/Cline don't publish UX specs)*
