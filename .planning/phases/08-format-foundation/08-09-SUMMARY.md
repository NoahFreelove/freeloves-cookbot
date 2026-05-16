---
phase: 08-format-foundation
plan: "09"
subsystem: tests
tags: [dotnet, csharp, xunit, verify, snapshot, prompt-safety, clean]

# Dependency graph
requires:
  - phase: 08-format-foundation/08-06
    provides: "expected-system-prompt.txt updated for v3 schema example — byte-matched into .verified.txt"

provides:
  - "Verify.Xunit 31.12.5 in CookBot.Tests (D-35)"
  - "ModuleInitializer routing all snapshots to tests/CookBot.Tests/Snapshots/"
  - "PromptSnapshotTests: Verify-based snapshot test replacing hand-rolled fixture equality"
  - "PromptSnapshotTests.BuildSystemPrompt.verified.txt: approved initial snapshot"
  - "BuildSystemPrompt_WithAliasInTemplate_DiffsAreVisible: self-check alias-injection Fact"

affects:
  - "Future intentional prompt changes require promoting .received.txt → .verified.txt for PR visibility"

# Tech tracking
tech-stack:
  added:
    - "Verify.Xunit 31.12.5 (xunit snapshot framework; requires xunit 2.9.3 for extensibility.core compatibility)"
    - "xunit bumped 2.9.2 → 2.9.3 (patch bump; required by Verify.Xunit 31.12.5 transitive dependency)"
  patterns:
    - "ModuleInitializer + Verifier.DerivePathInfo for centralized snapshot directory routing"
    - "Verifier.Verify(string) as snapshot assertion — test fails on first run, approved by promoting .received.txt → .verified.txt"
    - "In Verify v31+, [UseVerify] assembly attribute is injected automatically by the MSBuild WriteVerifyXunitAttributes target; no class-level decoration needed"

key-files:
  created:
    - tests/CookBot.Tests/ModuleInitializer.cs
    - tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt
  modified:
    - tests/CookBot.Tests/CookBot.Tests.csproj
    - tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs
  deleted:
    - tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt

key-decisions:
  - "xunit bumped 2.9.2 → 2.9.3 to resolve extensibility.core version conflict with Verify.Xunit 31.12.5 (Rule 1 auto-fix; patch bump is backward-compatible)"
  - "[UseVerify] class attribute removed from PromptSnapshotTests — Verify v31+ injects it at assembly level via MSBuild target (Rule 1 auto-fix; PATTERNS.md was written against older API)"
  - ".verified.txt content has UTF-8 BOM (Verify-written) vs. no-BOM legacy fixture; .verified.txt is authoritative — content is identical"

requirements-completed:
  - CLEAN-03

# Metrics
duration: 12min
completed: "2026-05-16"
---

# Phase 8 Plan 09: Verify.Xunit Snapshot Test Migration Summary

**Replaced Phase 1's hand-rolled PromptSnapshotTests fixture-equality with Verify.Xunit 31.12.5; ModuleInitializer routes snapshots to centralized Snapshots/ directory; initial .verified.txt committed; legacy fixture deleted**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-16T03:35:00Z
- **Completed:** 2026-05-16T03:47:00Z
- **Tasks:** 3
- **Files created:** 3 (ModuleInitializer.cs, verified.txt, csproj updated)
- **Files modified:** 2 (CookBot.Tests.csproj, PromptSnapshotTests.cs)
- **Files deleted:** 1 (expected-system-prompt.txt)

## Accomplishments

- Added `Verify.Xunit 31.12.5` to `CookBot.Tests.csproj`; bumped `xunit` from `2.9.2` to `2.9.3` to resolve `extensibility.core` version conflict
- Added `None Update` glob for `Snapshots\**\*.verified.txt` so committed snapshots ship with test binaries
- Created `tests/CookBot.Tests/ModuleInitializer.cs`: standard `[ModuleInitializer]` with `Verifier.DerivePathInfo` routing all snapshots to `{projectDirectory}/Snapshots/`
- Rewrote `PromptSnapshotTests.cs`: single `BuildSystemPrompt` Fact returning `Verifier.Verify(actual)` + self-check `BuildSystemPrompt_WithAliasInTemplate_DiffsAreVisible` Fact (Assert.Contains for alias token in rendered output)
- Generated initial `PromptSnapshotTests.BuildSystemPrompt.received.txt` via test run, promoted to `.verified.txt`, committed
- Deleted legacy `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt`
- All 5 snapshot + denylist tests pass; full suite 246/246 (excluding RequiresApiKey gated tests)

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Verify.Xunit 31.12.5 + Snapshots glob** — `973898b` (chore)
2. **Task 2: Create ModuleInitializer for Verify path config** — `f834b56` (feat)
3. **Task 3: Replace PromptSnapshotTests + commit verified snapshot + delete legacy fixture** — `7f0ed14` (feat)

## Files Created/Modified

- `tests/CookBot.Tests/CookBot.Tests.csproj` — added `Verify.Xunit 31.12.5` PackageReference; bumped `xunit 2.9.3`; added `Snapshots\**\*.verified.txt` None Update glob
- `tests/CookBot.Tests/ModuleInitializer.cs` — new file; file-scoped namespace; `[ModuleInitializer] Init()` calls `Verifier.DerivePathInfo` to route snapshots to `{projectDirectory}/Snapshots/`
- `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs` — replaced: Verify-based `BuildSystemPrompt` Fact + alias-injection self-check Fact; no class attribute (Verify v31+ injects at assembly level)
- `tests/CookBot.Tests/Snapshots/PromptSnapshotTests.BuildSystemPrompt.verified.txt` — new file; approved initial snapshot; content matches legacy fixture (UTF-8 BOM added by Verify)
- `tests/CookBot.Tests/Fixtures/Prompts/expected-system-prompt.txt` — DELETED (D-35)

## Decisions Made

- **xunit 2.9.2 → 2.9.3:** Verify.Xunit 31.12.5 requires `xunit.extensibility.core 2.9.3` transitively; `xunit 2.9.2` only provides `2.9.2`, causing NU1107 conflict. Bumped `xunit` to `2.9.3` (patch version; fully backward-compatible)
- **No class-level `[UsesVerify]` decorator:** In Verify v31+, `UseVerifyAttribute` is injected at assembly level by the `WriteVerifyXunitAttributes` MSBuild target defined in `build/Verify.Xunit.props`. The class-level attribute from older Verify versions no longer exists
- **`[UseVerify]` is assembly-only:** Attempting to use `[UseVerify]` on a class produces CS0592 ("Attribute is not valid on this declaration type. It is only valid on 'assembly' declarations"). The MSBuild target handles this automatically

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] xunit version conflict (NU1107) with Verify.Xunit 31.12.5**
- **Found during:** Task 1 — `dotnet restore` failed
- **Issue:** `Verify.Xunit 31.12.5` requires `xunit.extensibility.execution 2.9.3` → `xunit.extensibility.core 2.9.3`, but `xunit 2.9.2` provides only `xunit.extensibility.core 2.9.2`. NuGet emits NU1107 (hard error, not warning)
- **Fix:** Bumped `xunit` from `2.9.2` to `2.9.3` in `CookBot.Tests.csproj`. STACK.md research correctly identified the extensibility constraint; the version bump was implied
- **Files modified:** `tests/CookBot.Tests/CookBot.Tests.csproj`
- **Committed in:** `973898b` (Task 1 commit)

**2. [Rule 1 - Bug] `[UsesVerify]` class attribute does not exist in Verify v31.12.5**
- **Found during:** Task 3 — build failed with CS0246 (type not found) then CS0592 (wrong target)
- **Issue:** PATTERNS.md (and the plan's acceptance criterion `grep -c 'UsesVerify'`) references the older Verify API. In v31+, `UseVerifyAttribute` is an assembly-level attribute auto-injected by the MSBuild `WriteVerifyXunitAttributes` target in `build/Verify.Xunit.props`. There is no class-level `[UsesVerify]` attribute
- **Fix:** Removed class-level attribute entirely from `PromptSnapshotTests`. The MSBuild target handles the assembly-level opt-in. Test runs correctly; `Verifier.Verify()` works as expected
- **Files modified:** `tests/CookBot.Tests/Prompts/PromptSnapshotTests.cs`
- **Committed in:** `7f0ed14` (Task 3 commit)

**Note on acceptance criterion:** The plan criterion `grep -c 'UsesVerify' ... returns 1` cannot be met with the modern API (no such class attribute). The functional equivalent — `[UseVerify]` at assembly level via MSBuild — is in place. All tests pass.

## Known Stubs

None — the snapshot is wired to the actual `PromptBuilderService.ResolveTemplate` output via `TestHost.GetPromptBuilderService()` and `TestHost.MakeProfile()`. No mocks or placeholder data.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema trust boundary changes introduced.

---
*Phase: 08-format-foundation*
*Completed: 2026-05-16*
