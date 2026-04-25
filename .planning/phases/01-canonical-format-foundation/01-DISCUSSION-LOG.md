# Phase 1: Canonical Format Foundation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in `01-CONTEXT.md` — this log preserves the alternatives considered.

**Date:** 2026-04-25
**Phase:** 01-canonical-format-foundation
**Mode:** auto (recommended defaults selected without interactive prompts)
**Areas discussed:** Schema & Domain Modeling, Parser/Serializer/Schema Provider, Persistence & Migration, Prompt Consolidation & Anti-regression, Testing & Fixtures

---

## Schema & Domain Modeling

### Q1.1 — Where should `RecipeDocument` and its nested types live?

| Option | Description | Selected |
|---|---|---|
| New namespace `CookBot.Domain/Recipes/` (flat record hierarchy) | Pure POCO records with `[JsonPolymorphic]`. Consistent with `CookBot.Domain`'s zero-package-reference posture. | ✓ |
| New project `CookBot.Schemas` | Isolated schema definitions; explicit boundary. | |
| Inside existing `CookBot.Domain/Entities/` | Co-locate with EF entities. | |
| Inside `CookBot.Application/Recipes/` | Treat as DTOs alongside parser/serializer. | |

**Selected:** `CookBot.Domain/Recipes/` (recommended).
**Rationale:** `RecipeDocument` is a pure POCO — Domain is the right home. A separate project adds solution complexity for no isolation benefit (`SUMMARY.md §6` anti-feature). Co-locating with EF entities risks coupling the canonical record to the relational schema. Application-layer DTOs are reserved for service-orchestrated DTOs, not domain records.

### Q1.2 — How should step polymorphism be expressed?

| Option | Description | Selected |
|---|---|---|
| `abstract record StepNode` + `ContentStep` + `SectionStep` with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` | Closed discriminated union. Schema rejects mixed shapes. | ✓ |
| `record Step(string? Text, string? Section, ...)` with mutual-exclusivity validation | Single record, validator enforces exclusivity. | |
| `record Step(StepKind Kind, string Text, ...)` with enum + nullable fields | Flat record with explicit enum discriminator. | |
| Keep `IsSection: bool` (status quo) | No structural change. | |

**Selected:** Discriminated union with `kind` discriminator (recommended; closes Pitfall C3).
**Rationale:** Discriminated unions make "section steps cannot have timers" structurally impossible, where the boolean-flag approach lets the bug exist and relies on the validator to catch it. JSON Schema `oneOf` constraints against the discriminator give Anthropic Structured Outputs strict enforcement at the token level too.

### Q1.3 — Where does the `version` field live in the JSON?

| Option | Description | Selected |
|---|---|---|
| Top-level root field, integer | `{ "version": 2, "name": "...", ... }`. Stable at top so a v1 install can read enough to route to upcaster. | ✓ |
| Nested under `meta: { version: 2 }` | Groups versioning with other metadata. | |
| JSON Schema `$schema` URI | Schema-level rather than data-level. | |
| `_v` short field name | Saves bytes. | |

**Selected:** Top-level integer (recommended).
**Rationale:** Anthropic Structured Outputs schemas need a stable top-level discriminator; a v1 install reading a v3 file can detect the version cheaply without parsing the rest. Nesting buys nothing here. `$schema` URIs are heavy and require URL hosting. Short field names are premature optimization.

### Q1.4 — Forward-compat handling for unknown fields?

| Option | Description | Selected |
|---|---|---|
| `[JsonExtensionData] Dictionary<string, JsonElement> Extras` on root + step + ingredient records | STJ round-trips automatically. | ✓ |
| Reject unknown fields hard (strict mode only) | Simpler; matches Anthropic strict schema. | |
| Log + drop unknown fields | Permissive, but loses data on round-trip. | |

**Selected:** `[JsonExtensionData]` on root + step + ingredient (recommended; closes Pitfall H4).
**Rationale:** Lets a v1 install be a transit hub for a v2 cookbook (forward-compat). Hard rejection breaks shared cookbooks across mixed-version installs. Drop-on-read is a silent data-loss bug.

---

## Parser, Serializer, Schema Provider

### Q2.1 — How is the JSON Schema generated?

| Option | Description | Selected |
|---|---|---|
| `System.Text.Json.Schema.JsonSchemaExporter` (BCL on .NET 10) + post-process to set `additionalProperties: false` | Zero new packages. STJ-native. Caches the result. | ✓ |
| `NJsonSchema` | Mature, multi-target. Newtonsoft-rooted (would pull a parallel JSON model). | |
| `JsonSchema.Net` for both generation and validation | Single package handles both. | |
| Hand-write the schema as a JSON file | Most explicit. Drifts from the C# record. | |

**Selected:** `JsonSchemaExporter` for generation; `JsonSchema.Net` 9.2.x for validation only (recommended; matches `STACK.md §2`).
**Rationale:** Generating the schema from the record is the only way to keep the format spec, the AI prompt, and the validator from drifting. Newtonsoft-rooted libraries violate the codebase's STJ-only posture. Hand-writing the schema reintroduces the duplicated-truth problem this milestone is removing.

### Q2.2 — Validator output shape?

| Option | Description | Selected |
|---|---|---|
| `ValidationResult(IReadOnlyList<ValidationError> Errors, IReadOnlyList<ValidationWarning> Warnings)` | Errors fatal; warnings (coercion) non-fatal. Two-tier policy. | ✓ |
| `bool TryValidate(out IReadOnlyList<ValidationError> errors)` | Single severity. | |
| Throw `ValidationException` | Exceptions as control flow. | |
| Return `Result<RecipeDocument, ValidationError[]>` (Either-style) | Functional pattern. Unfamiliar to existing codebase. | |

**Selected:** `ValidationResult` with errors + warnings (recommended; matches `SUMMARY.md` Q4 two-tier policy).
**Rationale:** Coercion (e.g. `"30"` → `30`, single-string tag → array) is a useful permissiveness for AI output and paste-in but should be visible (warning). Hard semantic violations need to be errors. Throwing violates FORMAT-07 ("validator never throws"). Result-style is unfamiliar to the existing C# style.

### Q2.3 — Upcaster registration mechanism?

| Option | Description | Selected |
|---|---|---|
| DI-registered ordered list of `IRecipeUpcaster` impls; `RecipeUpcasterChain` reads + sorts by `FromVersion` | Standard .NET DI pattern. No reflection. | ✓ |
| Static dictionary keyed by `(from, to)` | Simpler but less testable. | |
| Reflection-based discovery via `AppDomain.GetAssemblies` | Magical. Hard to test. | |
| Dedicated migration framework (Liquibase-style) | Overkill for ≤2 transitions. | |

**Selected:** DI-registered + ordered list (recommended; matches `SUMMARY.md §2` "ordered C# functions are simpler than any library").
**Rationale:** Aligns with the existing DI conventions in `AddApplication()`. Easy to mock for testing each upcaster in isolation. Static dictionaries hide registration; reflection-based discovery is debug-hostile; a migration framework is over-engineered for this scale.

### Q2.4 — YAML's role going forward?

| Option | Description | Selected |
|---|---|---|
| YAML demoted to paste-in input only; YamlDotNet as YAML→JsonNode adapter | JSON canonical. YAML still parseable for back-compat. | ✓ |
| Keep YAML as canonical; teach Anthropic to emit YAML | Anthropic Structured Outputs is JSON-only. | |
| Drop YAML support entirely | Breaks back-compat for old AI conversations + manual pastes. | |

**Selected:** YAML demoted to paste-in adapter (recommended; matches `SUMMARY.md §1`).
**Rationale:** Anthropic Structured Outputs is JSON-only; canonical must be JSON. Dropping YAML breaks every existing AI conversation's saved messages and every manual paste-in. Adapter pattern keeps the back-compat surface tiny.

---

## Persistence & Migration

### Q3.1 — How is `Recipe.CanonicalDocumentJson` mapped in EF?

| Option | Description | Selected |
|---|---|---|
| Plain `string?` column (TEXT) — we own the serialization | Simpler. The column holds projected JSON, not a relational projection. | ✓ |
| `OwnsOne(e => e.Document, b => b.ToJson())` | EF handles serialization. | |
| Separate table `RecipeCanonicalDocuments` with FK | Normalized but unnecessary. | |

**Selected:** Plain string column (recommended).
**Rationale:** `OwnsOne` is for relational projections; the canonical document is a snapshot, not a query target. Separate table introduces a join for every load.

### Q3.2 — Pre-migration backup mechanism shape?

| Option | Description | Selected |
|---|---|---|
| `IDatabaseBackupService.BackupBeforeMigrationAsync(string migrationName, CancellationToken ct)`; `File.Copy` to `{name}.pre-{migration}.bak`; last-3 retention by mtime | Simple. Configurable retention via settings. | ✓ |
| SQLite `.backup` command via `Microsoft.Data.Sqlite` | Native SQLite backup API. More correct under load (but the seeder runs single-threaded at startup). | |
| External `tar` / shell script | Out of process. Fragile. | |
| No backup, rely on user backups | Risk of unrecoverable user data loss (Pitfall C4). | |

**Selected:** File copy with retention (recommended; closes Pitfall C4).
**Rationale:** SQLite file copy is safe at startup before `MigrateAsync()` runs (single-process). Shell scripts are anti-Windows. No backup is unacceptable for users with no other backups (the project's stated trusted-LAN posture means users are often non-technical home installers).

### Q3.3 — Backfill strategy after migration applies?

| Option | Description | Selected |
|---|---|---|
| Idempotent `WHERE CanonicalDocumentJson IS NULL` loop in `DatabaseSeeder.SeedAsync` after `MigrateAsync()`; batches of 50 | Re-runnable. Bounded memory. | ✓ |
| One-shot SQL UPDATE in the migration's `Up()` method | Couples migration with serialization logic. Hard to test. | |
| Lazy backfill on first read of each recipe | Distributes load but leaves DB in mixed state. | |
| Manual backfill script the user runs | Friction; users won't do it. | |

**Selected:** Idempotent loop in `DatabaseSeeder` (recommended).
**Rationale:** Keeps EF migrations pure schema (Up/Down). The seeder already runs at startup and already handles idempotent operations (admin user, default cookbook, ingredient seed). Re-running on a partially migrated DB is safe. Lazy backfill creates a confusing "some recipes have it, some don't" state.

### Q3.4 — Should `Recipe.IngredientRefs` column be dropped this phase?

| Option | Description | Selected |
|---|---|---|
| Stop *writing* this phase, drop *column* in Phase 4 alongside `LegacyRecipeProjector` | Safe rollback during the milestone. | ✓ |
| Drop the column in Phase 1's migration | One-shot cleanup. | |
| Keep writing it for back-compat indefinitely | Deferred decision; technical debt grows. | |

**Selected:** Stop writing this phase, drop in Phase 4 (recommended).
**Rationale:** During the v1.1 milestone, a rollback to v1.0 needs the column to exist (the v1.0 code reads from it). After v1.1 ships and stabilizes, the field is genuinely unused and Phase 4's cleanup pass drops it.

### Q3.5 — `CookbookTransferDocument.SchemaVersion` bump timing?

| Option | Description | Selected |
|---|---|---|
| Bump to 2 in Phase 1 (envelope marker) | Lets us identify v2-aware exports immediately. Deserializer routing in Phase 2. | ✓ |
| Bump to 2 in Phase 2 when deserializer hot path changes | Tightly couples version bump with code change. | |
| Don't bump until a future milestone | Loses the version-axis signal. | |

**Selected:** Bump in Phase 1 (recommended).
**Rationale:** The two-axis versioning (envelope + per-recipe) needs the envelope-version signal even if the hot path doesn't yet use it, so Phase 2 can route on it confidently. Cost: a one-line constant change.

---

## Prompt Consolidation & Anti-regression

### Q4.1 — How is the duplicated format spec eliminated?

| Option | Description | Selected |
|---|---|---|
| Extract `IRecipeSchemaDocumentationProvider` singleton; both `ResolveRecipeFormat` and `BuildCopyablePrompt` call its `GetFormatPrompt()` | Single source of truth. Generated from the canonical record. | ✓ |
| Move both literal strings to embedded resources | Still duplicated; just relocated. | |
| Inline-comment the two strings as "must stay in sync" | Documentation only; will drift. | |

**Selected:** Extract `IRecipeSchemaDocumentationProvider` (recommended; closes CONCERNS §13).
**Rationale:** The strings drift because they're owned by two methods; centralizing ownership in a single service is the only structural fix. Generating prose from the record is what closes the loop with `RecipeJsonSchemaProvider`.

### Q4.2 — How does the opt-out clause stay removed?

| Option | Description | Selected |
|---|---|---|
| Snapshot test on assembled system prompt + lint denylist (xUnit test grepping `PromptBuilderService.cs` for forbidden words) | Catches both regression mechanisms (clause re-added in code; clause re-added in template). | ✓ |
| Code review only | Manual; will fail under PR pressure. | |
| Runtime check: log a warning if the clause appears in the prompt | After-the-fact. | |
| External linter (e.g. roslynator custom rule) | New tooling overhead. | |

**Selected:** Snapshot test + lint denylist (recommended).
**Rationale:** Two-layer defense — the snapshot catches semantic changes ("new prose appeared in the prompt") while the denylist catches lexical regressions ("someone re-added the word `fallback`"). Both run on every CI build with no new tooling.

### Q4.3 — Snapshot framework choice?

| Option | Description | Selected |
|---|---|---|
| Hand-rolled (fixture file + xUnit string equality) | Zero new dependencies. Visible diffs in PRs. | ✓ |
| `Verify` (Microsoft) | Mature snapshot framework. Diff tooling. | |
| `ApprovalTests` | Older; similar surface to Verify. | |

**Selected:** Hand-rolled (recommended for this milestone).
**Rationale:** The prompt is small and stable. The diff tooling is overhead for a single fixture. If the maintenance burden becomes painful, switching to `Verify` is a one-package addition with no other code impact.

---

## Testing & Fixtures

### Q5.1 — Round-trip fixture organization?

| Option | Description | Selected |
|---|---|---|
| `tests/CookBot.Tests/Fixtures/Recipes/` with subdirs per source format (`v1-yaml/`, `v1-json-export/`, `v1-db-projections/`, `v2-canonical/`); xUnit `[Theory]` + `[MemberData]` | Filesystem-driven; new fixtures need no code change. | ✓ |
| Embedded resources in the test assembly | Faster load; less convenient to inspect/edit. | |
| Inline string constants in the test class | Hardest to maintain. | |

**Selected:** Filesystem fixtures with `[Theory]` + `[MemberData]` (recommended; matches existing test style).
**Rationale:** Adding a new fixture is just dropping a file. The diffs are visible in source control. Embedded resources are appropriate for assets distributed with the assembly; tests are not.

### Q5.2 — Round-trip property assertion?

| Option | Description | Selected |
|---|---|---|
| `Parse(Serialize(Upcast(input))) == canonical` with deep-equality, plus non-zero assertions on `prepTimeMinutes`/`cookTimeMinutes` for fixtures with time values | Catches Pitfall C2 (silent zeroing) directly. | ✓ |
| Just deep-equality | Misses Pitfall C2 if upcaster zeroes values into the canonical (which then matches itself). | |
| Reference-output snapshot comparison | Tests serialization stability, not round-trip correctness. | |

**Selected:** Deep-equality + non-zero on time fields (recommended).
**Rationale:** The non-zero assertion is the specific defense against the field-rename ambiguity Pitfall C2 documents. Pure round-trip can pass on degenerate canonical → canonical with zeros. The fixture set must include time-bearing recipes for this assertion to fire.

### Q5.3 — Smoke test for backfill?

| Option | Description | Selected |
|---|---|---|
| In-memory SQLite + `RecipeService` seeds 3 representative recipes; `LegacyRecipeProjector` runs against rows; assert round-trip with no value drift | Tests the projector contract in isolation. | ✓ |
| Integration test that runs the actual EF migration | Slower, brittle, redundant with `DatabaseSeeder` patterns. | |
| Manual smoke check against a copy of `cookbot.db` | Not automatable. | |

**Selected:** In-memory + projector contract (recommended).
**Rationale:** EF migration application is covered by the existing seeder smoke tests; this one focuses on the new projector logic. In-memory SQLite is fast and deterministic.

---

## Claude's Discretion

The following decisions were not gray areas the user needed to weigh in on; the planner can resolve them during planning:

- File-name granularity within `CookBot.Domain/Recipes/` (one file per record vs. grouped by concept).
- Specific xUnit `[Fact]` vs `[Theory]` choices for non-fixture tests.
- Whether `RecipeUpcasterChain` validates registration completeness at startup.
- Whether `JsonRecipeSerializer` exposes both compact and indented APIs.
- Log levels for backup/migration events.
- Whether to add a thin temporary `Tags` helper in this phase or wait for Phase 4's relational migration.

## Deferred Ideas

- `Recipe.TagsJson` → relational `RecipeTag` table → Phase 4 (POLISH-04).
- Drop `Recipe.IngredientRefs` column → Phase 4.
- Delete `AiChat.ExtractRecipeContent` ladder → Phase 2 (POLISH-01).
- Route `CookbookTransferService.Deserialize` through upcaster chain → Phase 2 (MIGRATION-04).
- Route legacy YAML pastes through upcaster chain in `IRecipeFormatParser` → Phase 2 (MIGRATION-06).
- `RedactSecrets` chokepoint → Phase 2 (AI-07).
- `<recipe>` XML wrapping for prompt-injection defense → Phase 2 (AI-08).
- Cookbook-import consent banner → Phase 2 (AI-09).
- Encrypt-at-rest for `UserProfile.AiApiKey` → FUTURE-01.
- MudBlazor 9.x upgrade → FUTURE-10.
- Cooklang as one-way export target → FUTURE-11.

---

*Auto-mode discussion — recommended defaults selected from `.planning/research/SUMMARY.md` and codebase scout. User can review `01-CONTEXT.md` and edit any decision before planning starts.*
