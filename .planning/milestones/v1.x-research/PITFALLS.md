# Pitfalls Research — v1.3 Production-Ready & Format Maturity

**Domain:** Self-hosted Blazor Server (.NET 10) cooking app — adding file upload, V3 schema bump, encrypt-at-rest, Dockerfile, smart pantry algorithm, and token-cost telemetry to an existing single-process app.
**Researched:** 2026-05-15
**Confidence:** HIGH (all pitfalls grounded in actual source code at the cited paths; no generic advice included)
**Scope:** Pitfalls specific to ADDING the five v1.3 feature buckets to THIS codebase. The prior `PITFALLS.md` (2026-04-25) covered v1.1/v1.2 format-consolidation concerns and is superseded for v1.3 planning purposes by this document.

---

## Critical Pitfalls

Mistakes that cause data loss, security incidents, or rewrites if shipped.

### Pitfall C1: `IDataProtector` key ring lost on container restart — all encrypted API keys become unreadable

**What goes wrong:**
`UserProfile.AiApiKey` will be encrypted with ASP.NET Core Data Protection (`IDataProtector`). By default, the key ring is stored in `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys` on the host. In a Docker container, that directory lives inside the writable container layer — it is destroyed on every `docker stop && docker start` (NOT just `docker rm`). After restart, the app cannot decrypt any `AiApiKey` row in the database. Every user's AI features silently break; they get `CryptographicException` caught somewhere inside `AiApiKeyResolutionService.ResolveAsync`, which returns `null`, which makes the app behave as if no key is configured — but the plaintext key is gone, not just unusable.

**Why it happens:**
Developers test encryption in the development environment where the key ring persists across restarts. Docker volume mounts are configured for `cookbot.db` and `uploads/` but not for the key-ring directory. This is the single most common ASP.NET Core + Docker production failure.

**How to avoid:**
- Mount the key ring directory to a named Docker volume: `docker-compose.yml` must have a volume entry for `/root/.aspnet/DataProtection-Keys` (or wherever `PersistKeysToFileSystem` is pointed).
- In `Program.cs`, call `builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("/data/keys"))` and set `/data/keys` as a volume mount beside `/data/cookbot.db`.
- The Dockerfile `VOLUME` instruction is NOT sufficient — it creates an anonymous volume that is also ephemeral. Explicit named volumes in `docker-compose.yml` are required.
- Add a startup diagnostic: on first launch, if `AiApiKey` rows exist but none can be decrypted, log a clear message: "Data Protection key ring not found. If you are restoring a backup, ensure the key ring volume is also restored."
- Test: `docker-compose up`, configure a key, `docker-compose stop`, `docker-compose start` (not `down`), verify key resolves correctly.

**Warning signs:**
- `AiApiKeyResolutionService.ResolveAsync` returns `null` for users who have a key set.
- AI features stop working after container restart without `docker rm`.
- `CryptographicException` appears in server output.

**Phase to address:** Phase 12 (Prod-ready — Dockerfile + docker-compose). Must be resolved before the Docker chapter ships. The key-ring volume must appear in `docker-compose.yml` on day one — retrofitting it requires users to re-enter their keys.

---

### Pitfall C2: Key sharing uses owner's `IDataProtector` scope — recipient decryption path is never exercised

**What goes wrong:**
The current key-sharing flow (`AiApiKeyResolutionService.ResolveAsync`, lines 42-66) reads `p.AiApiKey` from the *owner's* `UserProfile` row and returns it to the *recipient's* AI call. If `AiApiKey` is encrypted with a per-user purpose string (`IDataProtector.CreateProtector($"user-{ownerUserId}")`) — a natural design choice to add per-user key isolation — then the decrypt call must use the owner's purpose, not the recipient's. If the protector is scoped to the *calling user*, decryption fails silently and the recipient gets no key.

**Why it happens:**
The design of `IDataProtector.CreateProtector("purpose")` encourages per-user scoping for isolation. But in a sharing model, the resolution path reads another user's row and must decrypt it with the owner's context, which requires the resolution code to know the owner's ID at decrypt time.

**How to avoid:**
- Use a **single shared purpose string** for all API key encryption: `IDataProtector.CreateProtector("AiApiKey")`. No per-user scoping. The key ring itself is protected at the host level (the volume mount); per-row isolation is not needed.
- Alternatively, use a purpose that includes the ownerUserId and pass that explicitly into the decryption call site inside `ResolveAsync`.
- Write an integration test that: seeds two users (owner, recipient), sets owner's key via the encrypted path, creates a share, resolves key as recipient — asserts non-null result.

**Warning signs:**
- Recipient's AI calls return "No key configured" even though the owner's key is set and the share exists.
- `AiApiKeyResolutionService.ResolveAsync` returns null from the share path but not the own-key path.

**Phase to address:** Phase 11 (Encrypt-at-rest). The sharing round-trip test must be part of the phase acceptance criteria.

---

### Pitfall C3: Existing plaintext keys not migrated — mixed plaintext/ciphertext state in DB

**What goes wrong:**
If encryption is added via an EF Core `ValueConverter` on `UserProfile.AiApiKey`, the converter encrypts on write and decrypts on read. Existing rows in `cookbot.db` have plaintext keys. On first read after the migration, the converter attempts to decrypt a plaintext string — `IDataProtector.Unprotect` throws `CryptographicException` on non-ciphertext input. The unhandled exception in `AiApiKeyResolutionService.ResolveAsync` propagates as `null` (if caught) or crashes the circuit (if uncaught). Either way, every existing user's AI features break immediately on upgrade.

**Why it happens:**
EF Core `ValueConverter` applies uniformly to all rows; there is no "only on new writes" mode. The migration must explicitly re-encrypt existing plaintext rows before the converter is registered, or the converter must detect plaintext input and pass it through.

**How to avoid:**
- **Sentinel prefix pattern:** Encrypted values are stored as `enc:v1:<base64>`. The `ValueConverter` read path: if the stored string starts with `enc:v1:`, decrypt the remainder; otherwise return as-is (legacy plaintext) AND schedule a re-encryption on next save. No existing row breaks.
- Run a one-time EF migration that rewrites all non-null `AiApiKey` values through encryption. This requires the `IDataProtector` to be available inside the migration — which it is NOT by default (migrations don't have DI). Instead: use the `DatabaseSeeder.SeedAsync` startup path to re-encrypt all un-prefixed rows on first boot post-migration.
- Add a startup log: "Re-encrypted N legacy API keys at startup."
- Test: seed a row with a plaintext key, run the sentinel-aware converter, assert the key is readable AND the row is updated to the prefixed form.

**Warning signs:**
- `CryptographicException` in `AiApiKeyResolutionService` immediately after upgrade.
- All users lose AI access on first start of the new version.

**Phase to address:** Phase 11 (Encrypt-at-rest). The migration + sentinel-prefix approach must be specified in the PLAN.md before any code is written.

---

### Pitfall C4: `SecretRedactor` does not cover the new encryption error path — cleartext key leaks in exception messages

**What goes wrong:**
`SecretRedactor` (`src/CookBot.Infrastructure/AI/SecretRedactor.cs`) strips `sk-ant-*` patterns and `x-api-key` header values from error messages. The decrypt path in the new encryption layer will throw `CryptographicException` whose message may include the raw bytes it failed to decrypt — which is the *ciphertext*, not the plaintext, so `sk-ant-` won't match. However: if the resolve path falls back to reading an unencrypted key (Pitfall C3's legacy path), then crashes during an AI call, the cleartext key may appear in a `HttpRequestException` message that is NOT caught by `SecretRedactor` because it happens before the key is passed to `AnthropicAiService`. The `SecretRedactor.Redact(raw, resolvedKey)` signature requires the `resolvedKey` to be known at the catch site — if `resolvedKey` is read from `AiApiKeyResolutionService` before the error, it is available; but in a fallback/exception path it may not be.

**How to avoid:**
- Every catch site in the AI call path must call `SecretRedactor.Redact(errorMessage, resolvedKey)` where `resolvedKey` comes from the `EffectiveAiCredentials` returned by `ResolveAsync`. The existing pattern in `AnthropicAiService.SendStructuredAsync` (line 219, 263, 274) is the correct model — verify the new encrypt/decrypt path follows it.
- Add a unit test: `SecretRedactor.Redact(rawError, legacyPlaintextKey)` where `rawError` embeds the key — assert `[REDACTED]` result.

**Warning signs:**
- Exception messages visible in Blazor error UI contain `sk-ant-` substrings.
- The new key-read path adds a `catch` block that constructs an error string without calling `SecretRedactor`.

**Phase to address:** Phase 11 (Encrypt-at-rest). The `SecretRedactor` coverage check should be a mandatory step in the plan.

---

### Pitfall C5: `wwwroot/uploads/` committed to git — user photos become public

**What goes wrong:**
The repo's `.gitignore` covers `*.db` and `*.db.pre-*.bak` but NOT `wwwroot/uploads/` (confirmed by reading `.gitignore`). When a developer creates the directory locally and uploads a test photo, `git add .` or `git add src/` will include the file. If the repo is ever pushed to a public fork or a CI service, user-uploaded photos are exposed. The `*.bak` entry was added post-hoc (WARN-BAK-FILE-UNTRACKED in the v1.2 audit); the uploads directory is the same category of omission.

**How to avoid:**
- Add to `.gitignore` before any upload code ships:
  ```
  src/CookBot.Web/wwwroot/uploads/
  ```
- Also add a `.gitkeep` inside the empty directory (committed) so the directory is created on fresh clone without needing a startup mkdir.
- Add a startup check in `Program.cs`: `Directory.CreateDirectory(uploadPath)` where `uploadPath` is from configuration, to create the directory on first run without it needing to be in git.

**Warning signs:**
- `git status` shows `wwwroot/uploads/` files as untracked after a test upload.
- A `.gitignore` entry for uploads does not exist before Phase 9 is executed.

**Phase to address:** Phase 9 (Photos surface) — add the `.gitignore` entry as the FIRST task of Plan 9-1 before writing any upload code.

---

### Pitfall C6: DB backup does not include `uploads/` — restore leaves photos broken

**What goes wrong:**
`IDatabaseBackupService` copies `cookbot.db` to `cookbot.db.pre-{migrationName}.bak`. The photos stored in `wwwroot/uploads/` are referenced by `Recipe.PhotoUrl` (e.g. `/uploads/abc123.jpg`) in the database but the directory is never backed up. If a user restores from a DB backup (or a Docker volume restore covers only the `cookbot.db` volume, not the `uploads/` volume), every recipe renders with a broken image. The 404'd `src` attribute triggers the `onerror` fallback to `StripedPlaceholder` — silent breakage from the user's perspective, but their photos are permanently lost.

**How to avoid:**
- The Docker `docker-compose.yml` must define SEPARATE named volumes for `cookbot.db` and `uploads/` and document that BOTH must be backed up.
- The README backup section must explicitly say: "Backing up only the database file is insufficient if you use photo uploads. Also back up the uploads directory."
- The pre-migration backup step (`IDatabaseBackupService.BackupBeforeMigrationAsync`) should log a reminder: "Note: wwwroot/uploads/ is not included in this backup. Back it up separately."
- Consider a startup integrity check: count `Recipe.PhotoUrl` rows that start with `/uploads/` and verify the corresponding files exist; log a warning for missing files.

**Warning signs:**
- `docker-compose.yml` has a volume for the database but not for uploads.
- README backup instructions mention only `cookbot.db`.
- A restore-from-backup test shows broken images.

**Phase to address:** Phase 12 (Prod-ready — Docker + backup docs). Must be addressed alongside the Dockerfile, not after.

---

### Pitfall C7: V2→V3 upcaster bundling — partial failure of one addition breaks all three

**What goes wrong:**
The V3 schema bump bundles `PhotoUrl` + `Description` + per-step temperature into a single upcaster step (`V2Upcaster.Upcast(node)` → V3). If the temperature upcaster logic has a bug (e.g. it reads a field that doesn't exist in some recipes, throws a `NullReferenceException`), the upcaster chain throws for ALL v2 recipes — including ones with no temperature data. Every recipe in the app becomes unloadable. The `RecipeUpcasterChain.UpcastToCurrent` method (`src/CookBot.Application/Recipes/RecipeUpcasterChain.cs`) throws synchronously; there is no fallback.

**Why it happens:**
Bundling changes amortizes AI-prompt regression work but creates an all-or-nothing failure mode. The upcaster is in the hot read path for every recipe load.

**How to avoid:**
- Each of the three additions (PhotoUrl, Description, per-step temperature) is a **separate null-fill operation** inside the V2→V3 upcaster. Each uses `??` / null-coalescing — never throws for missing fields.
- Specifically: if a v2 recipe has no `steps[N].temperature` field, the upcaster sets it to `null` (NOT `{ value: 0, unit: "F" }`). Zero is a valid temperature; null is the absence of temperature data.
- Add a test: upcast a v2 recipe with NO temperature data — assert all three new fields are present (PhotoUrl: null, Description: null, steps[N].temperature: null) and no exception is thrown.
- Add a test: upcast a recipe with only ONE of the three new fields present — the other two are still null-filled correctly.

**Warning signs:**
- The upcaster for per-step temperature accesses `steps[N].temperature` without a null guard.
- `RecipeUpcasterChain.CurrentVersion` is bumped to 3 but the test suite doesn't exercise a v2 recipe that has no temperature.

**Phase to address:** Phase 8 (Schema V3 + Format cleanup). The upcaster tests must be written before the upcaster is merged.

---

### Pitfall C8: AI emits alternate photo field names — lint denylist not updated for V3 fields

**What goes wrong:**
The existing lint denylist (enforced in tests as of v1.1 Phase 1 Plan 01-04) prevents the AI opt-out clause from re-entering the system prompt. A parallel concern applies to field names: the AI has been trained on thousands of recipe schemas and knows that photos are often called `image`, `imageUrl`, `picture`, `thumbnail`, `coverPhoto`. When `photoUrl` appears in the JSON schema, the AI's structured-output constrained decoding forces it to use `photoUrl` — but if structured output ever degrades (model refusal, retry path falls back to free-form), the AI emits `imageUrl` instead. The `RecipeValidator` doesn't check field names; `JsonExtensionData.Extras` silently absorbs `imageUrl`, so the photo is lost with no error.

**How to avoid:**
- Extend the lint denylist test (already covering prompt opt-out phrases) to also assert that the AI prompt schema documentation uses ONLY `photoUrl` and does NOT use `image`, `imageUrl`, `picture`, or `thumbnail` in any example or comment.
- In the `RecipeValidator`, add a warning (not error) when `Extras` contains keys that are likely-alternate field names: `image`, `imageUrl`, `picture`, `thumbnail`. Surface as `ValidationWarning` with code `AlternateName`.
- Add a prompt regression test: feed the schema to the AI with a recipe prompt; assert the response's PhotoUrl field name is exactly `photoUrl`.

**Warning signs:**
- `RecipeDocument.Extras` dict on AI-generated recipes contains `imageUrl` or `image` keys.
- The repair loop does not trigger (because `Extras` round-trips silently).

**Phase to address:** Phase 8 (Schema V3). Update the lint denylist and add the `Extras`-check warning as part of the schema PR.

---

## High Pitfalls

Mistakes that cause user-visible breakage requiring a hotfix or significant rework.

### Pitfall H1: Kestrel / SignalR / FormOptions size limits block file uploads with misleading errors

**What goes wrong:**
Blazor Server `<InputFile>` uploads flow through THREE independent size limits that must all be raised:
1. `KestrelServerOptions.Limits.MaxRequestBodySize` — defaults to 30 MB. Raises a 413.
2. `FormOptions.MultipartBodyLengthLimit` — defaults to 128 MB (higher than Kestrel's limit, so not the binding constraint by default, but still must be configured explicitly when Kestrel is raised).
3. Blazor Server's SignalR `MaximumReceiveMessageSize` — defaults to 32 KB. File content passes through the SignalR circuit. A 2 MB image will exceed this and disconnect the circuit with no meaningful error to the user (the snackbar never fires — the circuit is just gone).

The symptom for limit #3 is especially misleading: the `<InputFile>` `OnChange` handler fires (the file was selected), but the circuit silently drops and reconnects. The user sees the "Disconnected" overlay, reconnects, and finds their edit unsaved. There is no 413 response — the connection just closes.

**How to avoid:**
- In `Program.cs`, configure all three explicitly for the expected max upload size (e.g. 5 MB for photos):
  ```csharp
  builder.WebHost.ConfigureKestrel(opts =>
      opts.Limits.MaxRequestBodySize = 5 * 1024 * 1024);
  builder.Services.Configure<FormOptions>(opts =>
      opts.MultipartBodyLengthLimit = 5 * 1024 * 1024);
  builder.Services.AddServerSideBlazor(opts =>
      opts.MaximumReceiveMessageSize = 5 * 1024 * 1024);
  ```
- Also enforce the cap client-side in the `<InputFile>` handler: check `file.Size` before reading; surface a snackbar error immediately if over the limit.
- Add a smoke test: attempt to upload a 6 MB file; verify the user sees a "File too large" toast, NOT a circuit disconnect.

**Warning signs:**
- The circuit drops when selecting a large image — no error toast, just "Reconnecting...".
- `MaximumReceiveMessageSize` is not set in `Program.cs`.

**Phase to address:** Phase 9 (Photos surface — file upload). Must be part of Plan 9-1 before any upload UI ships.

---

### Pitfall H2: Path-traversal in user-supplied filenames

**What goes wrong:**
When saving an uploaded file to `wwwroot/uploads/`, if the filename is derived from the user-supplied `IBrowserFile.Name`, a malicious filename like `../../appsettings.json` or `../../../etc/passwd` writes outside the intended directory. On Linux (the Docker container target), file permissions may block the worst outcomes, but on a misconfigured host the app could overwrite its own config.

**How to avoid:**
- **Never use the browser-supplied filename.** Generate a server-side filename:
  ```csharp
  var safeFileName = $"{Guid.NewGuid():N}{GetSafeExtension(file.ContentType)}";
  var savePath = Path.Combine(_uploadDir, safeFileName);
  ```
- `GetSafeExtension` maps content-type to a known-safe extension (`.jpg`, `.png`, `.webp`, `.gif`) — it does NOT use the browser filename's extension.
- Assert that the resolved `savePath` starts with the configured upload directory path (`savePath.StartsWith(_uploadDir)`) even after `Path.GetFullPath` normalization — defense in depth.

**Warning signs:**
- Upload code uses `file.Name` or any derivative of it to form the save path.
- No path-prefix assertion exists before writing.

**Phase to address:** Phase 9 (Photos surface). Part of the security checklist for Plan 9-1.

---

### Pitfall H3: Content-type sniffing failure — served uploaded file triggers XSS

**What goes wrong:**
A user uploads a file with a `.jpg` extension that is actually HTML (or SVG with embedded `<script>`). Kestrel serves files from `wwwroot/` with content-type based on extension. The browser receives the file with `Content-Type: image/jpeg` but the actual content is HTML — older browsers (and some SVG renderers) sniff the content and execute the script. If the `<img src="/uploads/...">` is rendered in another user's recipe view, this is a stored XSS.

**How to avoid:**
- Read the first 512 bytes of the uploaded file and verify the file magic bytes match the declared content-type before saving. For images: JPEG starts with `FF D8 FF`, PNG with `89 50 4E 47`, WebP with `52 49 46 46`. Reject anything that fails the magic-byte check.
- Serve the `uploads/` directory with `X-Content-Type-Options: nosniff` and `Content-Security-Policy: default-src 'none'` response headers to suppress browser sniffing. In ASP.NET Core, use `StaticFileOptions.OnPrepareResponse` to add these headers specifically for the uploads directory.
- Explicitly reject SVG uploads. SVG is XML and can contain `<script>` tags. The allowed types should be JPEG, PNG, WebP, GIF only.
- Test: upload an HTML file renamed `.jpg`; assert the save is rejected or the served file cannot execute scripts.

**Warning signs:**
- Upload validation only checks `IBrowserFile.ContentType` (user-controlled) without verifying magic bytes.
- No `X-Content-Type-Options: nosniff` on served upload responses.
- SVG is listed as an accepted content type.

**Phase to address:** Phase 9 (Photos surface). Security validation must precede serving any uploaded content.

---

### Pitfall H4: `onerror` fallback infinite loop — broken image triggers fallback that is also broken

**What goes wrong:**
IMG-10/11 (from `v1.3-PHASE-CANDIDATE-recipe-photos.md`) uses `onerror` to fall back to the `StripedPlaceholder`. The canonical implementation pattern is:
```html
<img src="@photoUrl" onerror="this.src='/img/placeholder.svg'" />
```
If `/img/placeholder.svg` itself is a broken URL (file missing, wrong path), the `onerror` fires again, which tries to load `/img/placeholder.svg` again, creating an infinite request loop that pegs the browser's network queue. On slow connections this causes a visible flicker storm.

**How to avoid:**
- The `onerror` handler must set `this.onerror = null` before changing `src` to prevent re-firing:
  ```html
  onerror="this.onerror=null; this.style.display='none'; this.parentElement.classList.add('photo-missing');"
  ```
- Better: in Blazor, use a backing field `_photoLoadFailed = false` and a Blazor event handler instead of inline JS; on error, set the flag and re-render to show `<StripedPlaceholder>` instead of the `<img>`. This is safe from the loop because Blazor replaces the DOM element entirely.
- Add a smoke test: render a recipe card with a 404 `PhotoUrl`; assert no network loop (inspect DevTools Network tab).

**Warning signs:**
- `onerror` handler sets `this.src` without first setting `this.onerror = null`.
- The fallback image path is hard-coded rather than verified to exist.

**Phase to address:** Phase 9 (Photos surface — consuming surfaces).

---

### Pitfall H5: Paste-URL accepts `javascript:`, `data:`, and `file://` URIs — XSS and local-file disclosure

**What goes wrong:**
`IMG-06` in the phase candidate already calls for a scheme allowlist. This pitfall documents what breaks if it is implemented incorrectly or incompletely:
- `javascript:alert(1)` in `<img src>` is a known XSS vector in older browsers. Modern browsers block it for `<img>`, but it should still be blocked at parse time.
- `data:text/html;base64,...` can be a bandwidth bomb (multi-megabyte inline data URI) and a persistent payload stored in the database.
- `file:///etc/passwd` causes the Blazor Server process to attempt to read local files (if it processes the URL at all), or leaks the path in error messages.
- An SVG served by a third-party URL (`https://evil.com/xss.svg`) can contain `<script>` tags that execute in the browser when loaded as an `<img>`. The `referrerpolicy="no-referrer"` attribute does not prevent this.

**How to avoid:**
- `RecipePhotoUrlValidator` (IMG-06) must use `Uri.TryCreate` + check `uri.Scheme` is `http` or `https`. Do NOT use string prefix matching (`url.StartsWith("http")` would pass `https://` but also technically pass `http://evil`).
- Validate on both user paste (editor) AND on AI-emitted `PhotoUrl` in the repair-loop validation step.
- For SVG from external URLs: the `<img>` tag in modern browsers already sandboxes SVG (scripts don't execute). Do NOT use `<object>` or `<embed>` for display; `<img>` only.
- Add unit tests to `RecipePhotoUrlValidator`:
  - `javascript:alert(1)` → rejected
  - `data:image/jpeg;base64,...` → rejected
  - `file:///etc/passwd` → rejected
  - `ftp://host/image.jpg` → rejected
  - `https://example.com/photo.jpg` → accepted
  - `http://example.com/photo.jpg` → accepted
  - `//example.com/photo.jpg` (protocol-relative) → rejected (no scheme)

**Warning signs:**
- `RecipePhotoUrlValidator` uses `StartsWith("http")` instead of `Uri.TryCreate` + scheme check.
- `data:` URIs are accepted.
- No unit tests cover the rejected cases.

**Phase to address:** Phase 8 (Schema V3 — URL safety is part of the schema work, not the UI surface).

---

### Pitfall H6: QuestPDF fetches `PhotoUrl` at PDF render time — blocking HTTP call in the render path

**What goes wrong:**
`CookbookPdfService.GeneratePdf` currently renders text-only recipes. When photo support is added, the temptation is to pass `PhotoUrl` to QuestPDF's `.Image(url)` or similar fluent API. QuestPDF does NOT fetch URLs — it requires raw bytes. If the implementation downloads the image bytes inside the `GeneratePdf` method (called synchronously on the Blazor circuit), this is a blocking `HttpClient.GetAsync(...).Result` call inside the synchronous QuestPDF document builder. This blocks the circuit thread and can deadlock the Blazor Server synchronization context.

**How to avoid:**
- Photo embedding in PDF export is optional for v1.3. The simplest correct approach: if a recipe has a `PhotoUrl` that is an external URL, the PDF omits the photo (or shows a placeholder note). The PDF export is a document format, not a browser.
- If photos are desired in PDF: download all image bytes BEFORE entering `GeneratePdf` using an `async` method; pass the byte arrays into the synchronous renderer.
- Add a XML doc comment on `CookbookPdfService.GeneratePdf` explicitly noting: "Never call HttpClient here — this method is synchronous. Pre-fetch external resources in the caller."
- Test: generate a PDF for a cookbook with a recipe that has a `PhotoUrl`; verify no blocking HTTP call occurs (use a `StubHttpClient` in tests).

**Warning signs:**
- `CookbookPdfService` is given `HttpClient` as a constructor dependency.
- `QuestPDF` `.Image(url)` — QuestPDF does not support URLs natively; any URL-to-image requires pre-fetching.

**Phase to address:** Phase 9 (Photos surface — consuming surfaces, PDF integration).

---

### Pitfall H7: Smart pantry-match algorithm is O(recipes × pantry × ingredients) — slow Home load

**What goes wrong:**
The current `BuildPantryMatchesAsync` (lines 286-321 of `Home.razor.cs`) loads ALL recipes with their ingredients via `Include(r => r.RecipeIngredients).ThenInclude(ri => ri.Ingredient)` and computes match ratios in-memory. This is already an O(recipes × ingredients) query. The smart algorithm (FUTURE-13) adds expiration weighting, dietary filtering, and percentage-of-pantry-used scoring — all of which require additional in-memory passes. For a user with 300 recipes × 15 ingredients average × a 50-item pantry, this becomes 22,500 row comparisons per Home page load, synchronously, on the circuit thread.

**How to avoid:**
- Push as much filtering as possible to EF Core / SQLite before the in-memory computation. At minimum: use a `JOIN` to filter recipes where `ANY RecipeIngredient.IngredientId IN (pantryIngredientIds)` before loading full ingredient data.
- Cap the candidate set: load only recipes where the join ratio is plausible (e.g. pre-filter to recipes where at least 50% of ingredients match) using a subquery or a CTE in raw SQL.
- Add a composite index on `RecipeIngredient(IngredientId, RecipeId)` if it does not exist — this makes the join fast.
- Add a `[Benchmark]` test (or at least a timing assertion) for `BuildPantryMatchesAsync` with 300 seed recipes and 50 pantry items; assert < 200ms.

**Warning signs:**
- Home page load time increases linearly with recipe count after v1.3.
- `BuildPantryMatchesAsync` materializes ALL recipes before filtering.
- No database index on `RecipeIngredient.IngredientId`.

**Phase to address:** Phase 10 (QOL — smart pantry-match). Must be addressed in the algorithm design, not as a follow-up.

---

### Pitfall H8: Score volatility — pantry-match recipes shuffle position on every Home reload

**What goes wrong:**
If the smart pantry-match score is computed with any floating-point arithmetic that is not deterministic across calls (e.g. depending on row retrieval order from SQLite, or on `DateTime.UtcNow` for expiration weighting), recipes will change their ranked position on every page load. The user navigates Home, sees "Pasta" as the top suggestion, navigates away, returns, and it's now second. Over repeated loads the list appears random. This is particularly jarring for the expiration-weighting logic: if an item expires "in 3 days" the score changes every second.

**How to avoid:**
- Expiration weighting must be quantized to day-granularity (`(expiry - DateTime.UtcNow.Date).TotalDays`, truncated to int). A score that changes only daily (not hourly) is stable across a user session.
- The final sort must have a deterministic tie-breaker: `OrderByDescending(score).ThenBy(recipeId)` (or `ThenBy(recipeName, OrdinalIgnoreCase)`).
- Test: call `BuildPantryMatchesAsync` twice with the same data; assert the result list is identical.

**Warning signs:**
- The score formula uses `DateTime.UtcNow` without date truncation.
- The final sort has no tie-breaker.

**Phase to address:** Phase 10 (QOL — smart pantry-match). Determinism check is part of the acceptance test.

---

### Pitfall H9: Token-cost telemetry double-counts tokens on the 2-retry repair loop

**What goes wrong:**
The `AiRecipeGenerator` retry loop sends the same conversation to Anthropic a second time on validation failure. If the telemetry log records tokens from the Anthropic `usage` object in each `SendStructuredAsync` call, a recipe that required one repair attempt is logged as costing 2× the first-call tokens. For a user who generates many complex recipes, their displayed cost is inflated by up to 100% depending on repair-loop hit rate.

**How to avoid:**
- Log each attempt's tokens separately, tagged with `attempt: 1`, `attempt: 2`. Report the SUM per recipe-generation event, not per API call.
- Include a `attempts` field in the telemetry record so cost-per-attempt can be analyzed separately from cost-per-recipe.
- Test: mock `IAiService` to return a validation failure on attempt 1 and success on attempt 2; assert the telemetry record for that recipe-generation event shows `totalInputTokens = attempt1.input + attempt2.input` and `attempts = 2`.

**Warning signs:**
- Telemetry records one row per `SendStructuredAsync` call rather than one row per `GenerateAsync` call.
- `AiRecipeGenerator.GenerateAsync` logs telemetry inside the retry loop rather than at the end.

**Phase to address:** Phase 12 (Prod-ready — token-cost telemetry).

---

### Pitfall H10: Token pricing table goes stale — Anthropic changes prices, v1.3 still computes 2026 rates

**What goes wrong:**
Token-cost telemetry (FUTURE-02) must translate `input_tokens` and `output_tokens` into a dollar estimate. If the pricing table is hardcoded in a C# constant or a static dictionary, it will be wrong when Anthropic reprices (which has happened multiple times — Sonnet pricing changed between 3.5 and 4.x). Users will see inaccurate cost estimates that are off by 40-80% after a repricing event.

**How to avoid:**
- The pricing table must be in `appsettings.json` under `CookBot.AiPricing`, not in code. The default values ship with the v1.3 release; users can update them in their local config without updating the app.
- Alternatively, store the pricing in a database table (`AiModelPricing`) that the admin can update via a simple UI or seeder.
- Display cost estimates with a clear disclaimer: "Estimates based on pricing as of [configuredDate]. Check Anthropic's pricing page for current rates."
- The cost display should never show a precise dollar figure — use "~$0.003" rather than "$0.00312" to signal it is an estimate.

**Warning signs:**
- Pricing constants appear in a `.cs` file rather than configuration.
- No `configuredDate` field indicates when the rates were last verified.

**Phase to address:** Phase 12 (Prod-ready — token-cost telemetry). The pricing-in-config design decision must be made in the PLAN.md.

---

### Pitfall H11: `RecipeFormatParser` round-trip fixtures break when V3 fields are added

**What goes wrong:**
`tests/CookBot.Tests/Services/RecipeFormatParserTests.cs` contains round-trip fixtures that assert `Parse(Serialize(doc)) == doc`. When `RecipeDocument` gains `PhotoUrl`, `Description`, and per-step `Temperature`, the serializer emits these new fields in YAML. If the test fixtures use string comparison (`Assert.Equal(expected, actual)`) rather than structural comparison, EVERY existing fixture fails because the output now includes `photoUrl: null` and `description: null` lines. This is the most likely "breaks first" location when V3 lands.

**How to avoid:**
- Before adding V3 fields, audit `RecipeFormatParserTests.cs` for string-comparison assertions. Convert them to structural assertions (`Assert.Equal(doc.Name, parsed.Name)`, etc.) or use null-omission in the YAML serializer so `null` fields are not emitted.
- Add new fixtures SPECIFICALLY for V3 fields rather than modifying existing ones.
- Run `dotnet test --filter RecipeFormatParserTests` as the first step of the V3 schema PR and verify green before any code changes.

**Warning signs:**
- `RecipeFormatParserTests.cs` uses `Assert.Equal(yamlString, serialized)` for multi-line YAML comparison.
- Existing tests do not have a "null fields are not emitted" assertion.

**Phase to address:** Phase 8 (Schema V3). The PLAN.md for the schema phase must include "audit and update `RecipeFormatParserTests` before merging schema changes."

---

## Moderate Pitfalls

Mistakes that cause friction, UX confusion, or require a non-trivial fix.

### Pitfall M1: Per-step temperature unit mismatch — user types 350°F, stored as 350°C

**What goes wrong:**
When the editor captures per-step temperature, the user types "350" and the UI likely has a unit dropdown. If the dropdown defaults to Celsius but the user is American and thinks Fahrenheit, or if the AI emits `{ value: 350, unit: "F" }` but the display renders it as degrees C, the recipe is silently wrong. The validator cannot catch this because 350 is a valid integer for either unit; the error is semantic, not structural.

**How to avoid:**
- The unit dropdown must default to the user's `UserProfile.UnitSystem` preference (already stored). Imperial → Fahrenheit default; Metric → Celsius default.
- Display the unit inline with the value: "350 °F" — not just "350" with a separate dropdown.
- In the AI prompt, instruct the model to use the user's unit system for temperature (already done for ingredient amounts; extend to steps).
- Add a test: generate a recipe with the AI for a user with `UnitSystem.Metric`; assert the emitted temperature unit is `"C"`.

**Warning signs:**
- Temperature unit dropdown defaults to Celsius regardless of user preference.
- AI-emitted temperatures are not validated against the user's unit system preference.

**Phase to address:** Phase 8 (Schema V3 — per-step temperature).

---

### Pitfall M2: V2→V3 upcaster null-fills temperature with `{ value: 0, unit: "F" }` instead of `null`

**What goes wrong:**
A developer writing the V2→V3 upcaster sets the step temperature field to a default object rather than `null` because the C# model has `Temperature Temperature { get; init; } = new Temperature(0, TemperatureUnit.F)` with a default constructor. Every existing recipe gets `temperature: { value: 0, unit: "F" }` after upcast. The UI renders "0°F" on every step of every legacy recipe. The cooking mode shows "Bake at 0°F." This is not caught by the `RecipeValidator` because 0 is a valid integer.

**How to avoid:**
- The `Temperature` property on `ContentStep` must be `Temperature? Temperature` — nullable. The default is `null`, not a zero-valued struct.
- The V2→V3 upcaster explicitly sets `temperature: null` for all steps that had no temperature in v2.
- The UI renders temperature only when the value is non-null AND `value > 0` (or some reasonable sentinel check).
- Add a test: upcast a v2 recipe; assert `doc.Steps.OfType<ContentStep>().All(s => s.Temperature == null)`.

**Warning signs:**
- `ContentStep.Temperature` is a non-nullable value type.
- The upcaster uses `new Temperature()` to initialize missing temperature fields.

**Phase to address:** Phase 8 (Schema V3). The nullable annotation must be in the initial domain model PR.

---

### Pitfall M3: `RecipeJsonSchemaProvider` not updated for V3 — AI structured-output ignores new fields

**What goes wrong:**
`RecipeJsonSchemaProvider.BuildSchema()` derives the JSON schema from `RecipeDocument` via `JsonSchemaExporter`. If `RecipeDocument` gains `PhotoUrl`, `Description`, and a `Temperature` nested type, the schema is updated automatically ONLY if the new properties have `[JsonPropertyName]` attributes. If `Temperature` is a nested record added to `ContentStep` without the attribute, or if `ContentStep` uses `[JsonPolymorphic]` and the new property is on the base class, the schema exporter may omit or mis-describe the field. The AI is then constrained-decoded against the old schema and cannot emit the new fields.

**How to avoid:**
- After adding V3 fields to the domain model, call `RecipeJsonSchemaProvider.GetSchema()` in a test and assert the returned JSON schema contains `photoUrl`, `description`, and `temperature` properties.
- Also assert that `temperature` has the correct nullable shape: `{ "type": ["object", "null"] }`.
- The existing `SetAdditionalPropertiesFalse` walker in `RecipeJsonSchemaProvider` must also visit the new `Temperature` nested object and add `"additionalProperties": false` — verify this in the test.

**Warning signs:**
- Schema output does not contain `photoUrl` after `RecipeDocument` is updated.
- `temperature` appears as a non-nullable object in the schema (causes the AI to always emit a temperature even for steps with no heat).

**Phase to address:** Phase 8 (Schema V3). Add a `RecipeJsonSchemaProvider` output assertion test as the very first test of the V3 phase.

---

### Pitfall M4: Docker container listens on `localhost` only — unreachable from host network

**What goes wrong:**
The app's `run.sh` binds Kestrel to `http://localhost:7000`. In a Docker container, `localhost` refers to the container's loopback interface — the host machine cannot reach it even with port mapping (`-p 7000:7000`). The port mapping maps host port 7000 to container port 7000 on `0.0.0.0` (the container's external interface), but the app is listening only on `127.0.0.1`. The result: the app starts, port is mapped, but all requests get `connection refused`.

**How to avoid:**
- In the Dockerfile's `ENTRYPOINT` or via an environment variable: set `ASPNETCORE_URLS=http://+:7000` (or `http://0.0.0.0:7000`). The `+` is ASP.NET Core's wildcard that binds to all interfaces.
- Alternatively, set in `appsettings.json`: `"Urls": "http://+:7000"` — but environment variable override is cleaner for Docker.
- Verify by running `docker-compose up` and `curl http://localhost:7000/` from the HOST machine (not inside the container).

**Warning signs:**
- `appsettings.json` or `launchSettings.json` hardcodes `localhost` as the bind address.
- `ASPNETCORE_URLS` is not set in the Dockerfile or `docker-compose.yml`.

**Phase to address:** Phase 12 (Prod-ready — Dockerfile). First thing to verify in the Docker smoke test.

---

### Pitfall M5: SQLite WAL mode + Docker volume + high write frequency = lock contention

**What goes wrong:**
SQLite's WAL (Write-Ahead Log) mode creates additional files alongside `cookbot.db`: `cookbot.db-shm` and `cookbot.db-wal`. If these files are on a Docker volume that has slow fsync (e.g. NFS-backed or a Docker Desktop virtualized volume on macOS/Windows), concurrent writes from the Blazor Server's scoped `CookBotDbContext` instances can experience lock contention. The symptom is intermittent `SQLITE_BUSY` errors during recipe saves. The `.gitignore` already covers `*.db-wal` and `*.db-shm`, so they won't be accidentally committed, but they must be on the SAME volume as `cookbot.db`.

**How to avoid:**
- The Docker volume mount must include the entire directory containing `cookbot.db`, not just the file. Use `./data:/app/data` where `data/` holds `cookbot.db`, not a bind mount of the file itself.
- In `Program.cs`, set the EF Core connection string with `Journal Mode=WAL;` explicitly and `Cache=Shared` for the pool. The default is WAL-off; adding WAL improves concurrent read performance but requires the shm/wal files to be co-located.
- Test: two simultaneous recipe saves from two browser sessions; assert no `SQLITE_BUSY` exception.

**Warning signs:**
- The `docker-compose.yml` mounts the specific file (`./cookbot.db:/app/cookbot.db`) rather than the directory.
- `*.db-shm` and `*.db-wal` files appear on the HOST in a different location than `cookbot.db`.

**Phase to address:** Phase 12 (Prod-ready — Dockerfile + docker-compose).

---

### Pitfall M6: `docker-compose restart: unless-stopped` masks startup failures

**What goes wrong:**
`restart: unless-stopped` causes Docker Compose to restart the container if it exits. If the app fails at startup (migration failure, missing volume, wrong connection string), it exits immediately, Docker restarts it, it fails again, restart again — a rapid restart loop that can exhaust disk space from log files and mask the root cause. The user sees a container that is "running" but immediately restarts, and `docker logs cookbot` shows the same error on loop.

**How to avoid:**
- Use `restart: on-failure` with `max_retries: 3` rather than `unless-stopped`. After 3 failures, Docker stops retrying and the container stays in the `exited` state — making it obvious something is wrong.
- Alternatively, use `unless-stopped` but implement a startup health check (`healthcheck:` in `docker-compose.yml`) that the app only passes once it has successfully migrated and is accepting connections.
- Document in the README: "If the container enters a restart loop, check `docker logs cookbot` for startup errors."

**Warning signs:**
- `docker-compose.yml` has `restart: unless-stopped` with no `healthcheck`.
- No startup validation that exits with a non-zero code on unrecoverable errors (e.g. missing key ring volume).

**Phase to address:** Phase 12 (Prod-ready — Dockerfile + docker-compose).

---

### Pitfall M7: Timezone / locale defaults break `FractionFormatter` or cooking timers in container

**What goes wrong:**
The .NET runtime inside a Linux Docker container defaults to UTC and the invariant culture. The `FractionFormatter` is culture-independent (it formats fractions as strings, not numbers), so it is safe. However, `DescribeRelative` in `Home.razor.cs` (line 330-337) calls `utc.ToLocalTime()` and formats with `"MMM d"` — inside the container, `ToLocalTime()` returns UTC (no TZ set), and `"MMM d"` produces English month names regardless of the user's locale. For a French self-hoster, "Mai 15" would be expected but "May 15" appears. This is a cosmetic issue, not a crash.

**How to avoid:**
- Set `TZ=UTC` explicitly in the Dockerfile `ENV` section to avoid surprises when the host has a different TZ.
- In `DescribeRelative`, use `DateTime.UtcNow` throughout and format with a culture-invariant format that doesn't rely on month-name localization (`"yyyy-MM-dd"` or `"MMM d"` with `CultureInfo.InvariantCulture`).
- Document in README: "The app uses UTC for all date display. Locale-specific date formatting is not yet supported."

**Warning signs:**
- `TZ` is not set in the Dockerfile.
- `ToLocalTime()` is used in server-side formatting (not just in JS client-side display).

**Phase to address:** Phase 12 (Prod-ready — Dockerfile). Minor but worth fixing at Dockerfile authoring time.

---

### Pitfall M8: Token-cost telemetry scan at Profile render time — missing composite index

**What goes wrong:**
Token-cost telemetry (FUTURE-02) requires a per-user aggregate query: "sum all input and output tokens for this user across all AI calls." If the telemetry table has no composite index on `(UserId, CreatedAt)`, this query scans the entire table on every Profile page load. For a self-hoster who has used the app for a year with daily AI recipe generation (say, 365 × 10 calls = 3,650 rows), the scan is trivial. But for the admin page showing cross-user telemetry, the scan covers all users' rows.

**How to avoid:**
- Add a composite index `IX_AiUsageLogs_UserId_CreatedAt` in the EF migration that creates the telemetry table.
- Keep the Profile query to a date-bounded aggregation (current month, last 30 days) rather than all-time, to bound the scan even without the index.
- Test: seed 10,000 telemetry rows across 10 users; assert Profile render completes in < 100ms.

**Warning signs:**
- The telemetry table has no index on `UserId`.
- The Profile page query has no date filter (`WHERE CreatedAt > @start`).

**Phase to address:** Phase 12 (Prod-ready — token-cost telemetry). The EF migration that creates the telemetry table must include the index.

---

### Pitfall M9: Cross-user token telemetry visible to admin — privacy decision not documented

**What goes wrong:**
If the self-hosting admin has access to a "total cost by user" view (useful for managing a shared key), this reveals per-user AI usage patterns to the admin. For a family-use deployment this is expected. For a shared-office deployment, users may not expect the admin to see when they used AI and how intensively. This is not a bug but a product decision that needs explicit documentation.

**How to avoid:**
- The admin telemetry view should be gated behind `User.IsCookBotAdmin` (already the admin flag). Add a visible note in the UI: "Usage telemetry is visible to CookBot administrators."
- Document in the README: "The admin can view aggregate token usage per user. If this is undesirable for your deployment, disable token-cost telemetry in appsettings."
- Add a `CookBotSettings.TelemetryEnabled` flag (similar to `AiFeaturesEnabled`) so the admin can opt out of telemetry entirely.

**Warning signs:**
- No UI disclosure that the admin can see individual user token usage.
- No `TelemetryEnabled` killswitch in `CookBotSettings`.

**Phase to address:** Phase 12 (Prod-ready — token-cost telemetry). The privacy disclosure must be in the initial telemetry plan.

---

### Pitfall M10: `Recipe.Description` collides with AI treating step 1 as the description

**What goes wrong:**
When the AI generates a recipe and `Description` is a new top-level field in V3, the model may include an introductory paragraph as step 1 of the steps array ("This classic Italian dish dates back to...") AND also populate `description` with a similar sentence. Alternatively, if the system prompt doesn't explicitly distinguish the two, the model puts the intro paragraph in step 1 instead of `description`, leaving `description` empty. Neither outcome is wrong per the validator, but both are undesirable UX.

**How to avoid:**
- In the system prompt's description of the V3 schema, explicitly define `description` as "1-2 sentence recipe summary, no historical context, no cooking advice — just what the dish is." AND specify "Do not put introductory prose in step 1. Steps begin with the first cooking action."
- Add a `RecipeValidator` warning (not error) when `steps[0]` is a `ContentStep` whose text is > 100 characters and does not start with a cooking verb — a heuristic signal that the intro paragraph ended up in step 1.
- Test: generate a recipe; assert `Description` is non-empty when the system prompt instructs the model to populate it.

**Warning signs:**
- AI-generated recipes have a non-cooking introductory sentence as `steps[0]`.
- `Description` is consistently empty on AI-generated V3 recipes.

**Phase to address:** Phase 8 (Schema V3 — AI prompt update). Add the validator warning alongside the schema.

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems in THIS codebase.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| `<img onerror="this.src='/fallback.png'">` without `this.onerror=null` | Simple one-liner | Infinite loop on double-broken URLs, pegs browser network queue | Never |
| Using `IBrowserFile.Name` for save path | Preserves user's filename | Path-traversal attack vector | Never |
| Mounting `cookbot.db` directly (not its directory) in Docker | Simpler bind mount | WAL files (`-shm`, `-wal`) land on host outside the volume, breaking WAL mode | Never for SQLite with WAL enabled |
| Pricing constants in C# source | Compile-time safety | Stale rates after any Anthropic repricing; requires app rebuild to update | Acceptable if clearly commented with verification date; better in config |
| `restart: unless-stopped` without `healthcheck` | Container auto-restarts on crash | Masks startup failures; creates rapid restart loops | Acceptable in development; unacceptable in production docs |
| Per-user `IDataProtector` scope for API keys | Better isolation | Breaks the sharing flow where recipient reads owner's row | Never for a shared-key model |
| Recording telemetry inside the retry loop | Simpler code | Double-counts token cost on repair attempts | Never; record at the `GenerateAsync` level |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| ASP.NET Core Data Protection + Docker | Key ring in ephemeral container layer | Explicit named volume for key ring directory, separate from DB volume |
| Data Protection + API key sharing | Per-user protector scope | Single shared purpose string (`"AiApiKey"`) across all rows |
| `<InputFile>` + Blazor Server SignalR | Only raising Kestrel limit | Must raise ALL THREE: Kestrel `MaxRequestBodySize`, `FormOptions.MultipartBodyLengthLimit`, `AddServerSideBlazor MaximumReceiveMessageSize` |
| QuestPDF + PhotoUrl | Passing URL string to QuestPDF | Pre-fetch bytes in async caller; pass byte array to synchronous renderer |
| SQLite WAL + Docker volume | Bind-mounting the file (`./cookbot.db:/app/cookbot.db`) | Bind-mount the directory (`./data:/app/data`) so WAL sidecar files co-locate |
| EF Value Converter + existing plaintext data | Converter decrypts all rows including legacy plaintext | Sentinel-prefix pattern (`enc:v1:`) + startup re-encryption pass |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| `BuildPantryMatchesAsync` loads all recipes before filtering | Home page slow to load | Composite DB index + pre-filter in EF query before `ToListAsync` | ~100+ recipes |
| Token telemetry all-time aggregate query | Profile page slow | Composite index `(UserId, CreatedAt)` + date-bound query (last 30 days) | ~1,000+ telemetry rows |
| Pantry-match score uses `DateTime.UtcNow` without truncation | Recipes re-sort on every reload, flickering UX | Truncate to day granularity in score formula | Immediately on first use |
| `RecipeFormatParser` round-trip fixture string comparison | Every fixture fails when new nullable fields are added | Structural assertions, not string comparison | First V3 schema merge |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Using `IBrowserFile.Name` in save path | Path-traversal attack, overwrite config files | Server-generated GUID filename + content-type-derived extension |
| Checking only `IBrowserFile.ContentType` for upload validation | Stored XSS via HTML/SVG disguised as JPEG | Magic-byte validation of first 512 bytes |
| Serving uploads without `X-Content-Type-Options: nosniff` | Browser sniffs uploaded HTML as executable | `StaticFileOptions.OnPrepareResponse` adds `nosniff` header for `/uploads/` |
| Accepting `data:` or `javascript:` in paste-URL | Bandwidth bomb, XSS, persistent payload in DB | `Uri.TryCreate` + scheme allowlist (`http`, `https` only) in `RecipePhotoUrlValidator` |
| `wwwroot/uploads/` not in `.gitignore` | User photos accidentally committed to repo | Add `.gitignore` entry before writing any upload code |
| Data Protection key ring not on a named volume | Container restart destroys all API keys | Explicit named volume in `docker-compose.yml` for key ring directory |
| Cleartext key in `CryptographicException` error path | Key leaks if sentinel-prefix check fails during decrypt | All new decrypt catch sites must call `SecretRedactor.Redact` |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Double-broken `onerror` fallback loop | Flickering image + pegged network | `this.onerror=null` before setting `this.src`; or Blazor state-flag approach |
| Upload silently drops circuit (SignalR limit) | User loses edit, no error message | Client-side size check BEFORE reading file; explicit "file too large" toast |
| Recipes re-sort on every Home reload (score instability) | Jarring UX, "random" suggestions | Day-granular expiration weighting, deterministic tie-breaker sort |
| Per-step temperature shows "0°F" on all legacy recipes | Every recipe appears to have wrong temperature | `Temperature?` nullable; render only when non-null |
| AI `Description` + introductory step 1 duplication | Redundant content, confusing recipe structure | Explicit system prompt instruction distinguishing the two fields |
| Token cost shows precise dollars (false precision) | User over-trusts the estimate | Display as `~$X.XX` with disclosure note about estimate basis |

---

## "Looks Done But Isn't" Checklist

These are the v1.3 features most likely to appear complete during development but be missing a critical piece.

- [ ] **File uploads:** Missing the SignalR `MaximumReceiveMessageSize` raise — uploads work in dev (localhost, fast) but drop circuits on LAN use. Verify: `Program.cs` has all THREE size limits configured.
- [ ] **File uploads:** Missing `wwwroot/uploads/` in `.gitignore` — will silently be committed on first `git add`. Verify: `.gitignore` entry added before Phase 9 code.
- [ ] **Photo onerror fallback:** Missing `this.onerror=null` in the handler — loop not visible until a broken image is actually encountered. Verify: test with a deliberate 404 photo URL.
- [ ] **Docker deploy:** Key ring not on a named volume — works until the first `docker stop && start`. Verify: `docker-compose stop && docker-compose start`, confirm AI still works without re-entering keys.
- [ ] **Docker deploy:** App bound to `localhost` inside container — works in `docker exec` but not from host. Verify: `curl http://localhost:7000/` from HOST machine after `docker-compose up`.
- [ ] **Encrypt-at-rest:** Existing plaintext keys not migrated — breaks ALL users on first upgrade. Verify: seed a plaintext key, run the new version, confirm key is still accessible.
- [ ] **Encrypt-at-rest:** Key sharing path not tested — recipient cannot use owner's key after encryption. Verify: owner sets key, creates share, recipient successfully uses it.
- [ ] **V3 schema:** `RecipeJsonSchemaProvider` not updated — AI cannot emit new fields. Verify: assert schema JSON contains `photoUrl`, `description`, `temperature`.
- [ ] **V3 upcaster:** Zero-fill instead of null-fill for per-step temperature — shows "0°F" on every legacy step. Verify: upcast a v2 recipe; assert all `temperature` fields are null.
- [ ] **Token telemetry:** Double-counting on repair loop — cost appears inflated. Verify: force a repair-loop hit; assert telemetry shows one record with `attempts=2`, not two records.
- [ ] **Pantry match:** Missing composite DB index — Home page becomes slow at scale. Verify: seed 200 recipes; measure `BuildPantryMatchesAsync` elapsed time.

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| C1: Data Protection key ring lost on container restart | Phase 12 (Dockerfile) | `docker-compose stop && start` smoke test with AI call |
| C2: Key-sharing decryption scope breakage | Phase 11 (Encrypt-at-rest) | Owner-sets-key + recipient-uses-key integration test |
| C3: Existing plaintext keys not migrated | Phase 11 (Encrypt-at-rest) | Sentinel-prefix unit test + upgrade smoke test from v1.2 DB |
| C4: `SecretRedactor` coverage gap in decrypt path | Phase 11 (Encrypt-at-rest) | `SecretRedactor.Redact` unit test with legacy plaintext key |
| C5: `wwwroot/uploads/` committed to git | Phase 9 (Photos surface — first task) | `git status` after test upload shows no tracked files |
| C6: DB backup excludes `uploads/` directory | Phase 12 (Dockerfile + backup docs) | Backup-restore round-trip with photos; verify image URLs still resolve |
| C7: V3 upcaster bundling causes all-or-nothing failure | Phase 8 (Schema V3) | Upcast v2 recipe with no temperature; assert no throw, all new fields null |
| C8: Lint denylist not updated for V3 alternate field names | Phase 8 (Schema V3) | `Extras` dict check warning test; denylist test covers `imageUrl` |
| H1: Three size limits block file uploads | Phase 9 (Photos surface — Plan 9-1) | Upload 6 MB test file; verify "File too large" toast, no circuit drop |
| H2: Path-traversal in uploaded filenames | Phase 9 (Photos surface — Plan 9-1) | Upload with filename `../../appsettings.json`; assert rejection |
| H3: Content-type sniffing XSS | Phase 9 (Photos surface — Plan 9-1) | Upload HTML as `.jpg`; assert rejection or `nosniff` header |
| H4: `onerror` fallback infinite loop | Phase 9 (Photos surface — consuming surfaces) | Render recipe with 404 PhotoUrl; assert no network loop |
| H5: Paste-URL accepts dangerous schemes | Phase 8 (Schema V3 — URL safety) | `RecipePhotoUrlValidator` unit tests for all rejected cases |
| H6: QuestPDF blocks on HTTP fetch | Phase 9 (Photos surface — PDF) | Generate PDF for recipe with PhotoUrl; assert no blocking call |
| H7: Smart pantry-match O(n²) performance | Phase 10 (QOL — pantry match) | Benchmark with 300 recipes; assert < 200ms |
| H8: Pantry match score volatility | Phase 10 (QOL — pantry match) | Call twice with same data; assert identical result list |
| H9: Token telemetry double-counts on repair | Phase 12 (Telemetry) | Force repair-loop; assert single telemetry record with `attempts=2` |
| H10: Pricing table goes stale | Phase 12 (Telemetry) | Pricing in `appsettings.json`; assert no pricing constants in `.cs` files |
| H11: `RecipeFormatParserTests` breaks on V3 | Phase 8 (Schema V3 — first step) | Run `RecipeFormatParserTests` before any schema changes; audit string assertions |
| M1: Per-step temperature unit mismatch | Phase 8 (Schema V3 — per-step temperature) | AI generates recipe for Metric user; assert temperature unit is `"C"` |
| M2: V3 upcaster null-fills temp with `{ value: 0 }` | Phase 8 (Schema V3 — upcaster) | Upcast v2 recipe; assert `ContentStep.Temperature == null` |
| M3: `RecipeJsonSchemaProvider` missing V3 fields | Phase 8 (Schema V3) | Schema assertion test: `photoUrl`, `description`, `temperature` present |
| M4: Docker container binds `localhost` only | Phase 12 (Dockerfile) | `curl http://localhost:7000/` from host machine after `docker-compose up` |
| M5: SQLite WAL + Docker file bind mount | Phase 12 (Dockerfile) | Bind-mount directory, not file; verify WAL files co-locate |
| M6: `restart: unless-stopped` masks startup failures | Phase 12 (Dockerfile) | Induce startup failure; assert container stops after 3 retries |
| M7: Timezone breaks date display in container | Phase 12 (Dockerfile) | Verify `TZ=UTC` in Dockerfile ENV |
| M8: Telemetry scan missing composite index | Phase 12 (Telemetry) | Migration includes `IX_AiUsageLogs_UserId_CreatedAt` |
| M9: Cross-user telemetry not disclosed | Phase 12 (Telemetry) | UI shows "visible to admins" note; `TelemetryEnabled` flag exists |
| M10: AI puts intro prose in step 1 instead of Description | Phase 8 (Schema V3 — AI prompt) | Generate recipe; assert Description non-empty; step[0] starts with verb |

---

## Sources

All findings are grounded in the actual codebase at the paths cited. External sources used to validate specific claims:

- `src/CookBot.Web/Services/AiApiKeyResolutionService.cs` — key sharing flow (C2)
- `src/CookBot.Infrastructure/AI/SecretRedactor.cs` — redaction coverage gap (C4)
- `src/CookBot.Application/Recipes/RecipeUpcasterChain.cs` — upcaster throw behavior (C7)
- `src/CookBot.Application/Recipes/RecipeJsonSchemaProvider.cs` — schema derivation from type (M3)
- `src/CookBot.Web/Components/Pages/Home.razor.cs:280-321` — current pantry-match performance (H7)
- `src/CookBot.Infrastructure/AI/AnthropicAiService.cs` — `new HttpClient()` per call, no pooling (existing concern; not new in v1.3 but relevant to telemetry)
- `.gitignore` (repo root) — absence of `uploads/` entry (C5)
- `src/CookBot.Web/Services/CookbookPdfService.cs` — synchronous PDF render, no HTTP in builder (H6)
- `.planning/v1.3-PHASE-CANDIDATE-recipe-photos.md` — IMG-01..13 requirements, threat model
- [Microsoft Learn — ASP.NET Core Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview) — HIGH confidence; key ring persistence location and Docker volume requirement
- [Microsoft Learn — Blazor file uploads](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads) — HIGH confidence; `MaximumReceiveMessageSize` requirement documented
- [Microsoft Learn — Static file middleware headers](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files) — HIGH confidence; `OnPrepareResponse` for custom headers on upload directory
- [OWASP — File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) — HIGH confidence; magic-byte validation, path-traversal prevention

---

*Pitfalls research for: FreelovesCookBot v1.3 Production-Ready & Format Maturity*
*Researched: 2026-05-15*
