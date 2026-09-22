# Paladin hot activation prototype

Historical prototype evidence follows. See [the current title-screen activation contract](HOT-RELOAD.md) for production hardening and current support boundaries.

Status: restricted same-process Paladin activation passed a final 100-cycle trial and fresh enabled/disabled adventures. The isolated test process is stopped. No changes have been merged back. Responsiveness, generation retention, remembered-class UX and native title-navigation containment remain release gates. See [the live evidence record](HOT-RELOAD-LIVE-EVIDENCE.md).

## Scope

The `codex/paladin-hot-reload` worktree starts at the active Paladin branch's commit and includes a copied snapshot of its uncommitted work. The original checkout remains separate. The baseline file hashes and copies are retained locally so the experimental delta can be separated from the preexisting changes before any eventual merge. No changes have been merged back. The original checkout changed concurrently during this work; those changes were not overwritten. Reconcile the recorded experimental delta with the latest active work before any eventual merge.

This is an opt-in experiment requiring `FTK_HOT_RELOAD=1` plus the protected, package-only runtime-test environment. Normal launches do not offer hot activation. Only the audited game assembly fingerprint, empty selections, and the current Paladin content shape/templates are supported. Arbitrary dependencies, DLLs, other plugins, demo content, injected diagnostics, new content kinds, and different proficiency templates are rejected.

The intended result is same-process install/enable/update/disable/remove at the initial title screen, followed by a new local adventure. Character/adventure setup seals the process permanently. Existing-save and online entry are blocked in the experiment. Returning to title does not reopen activation.

## Implemented surfaces

- [Coordinator](../FTKModFramework/Core/HotReload/HotReloadCoordinator.cs): locks entry, validates the prepared generation with the helper, captures old state, restores the native baseline, loads a strict candidate, checks resources and caches, commits the durable selection, and retires old objects. Failures restore the old state or fault closed when restoration/durable state cannot be proven.
- [Definition snapshot](../FTKModFramework/Core/HotReload/DefinitionState.cs): five affected DB arrays/indexes, ID maps, retained-row ledger, allocator, localization and mod metadata. Canonical rebuild drops historical ID reservations, including collision-probe history.
- [Native caches](../FTKModFramework/Core/HotReload/HotReloadNativeCaches.cs): four item caches plus initialized flag, and proficiency instances retaining row references. Keeps exact old maps and instances for rollback; unchanged vanilla proficiency instances are shared.
- [Resources](../FTKModFramework/Core/HotReload/PaladinResourceState.cs): Guardian state, model/apparel maps, asset locations, sprites/textures, behavior hosts and cloned weapon prefabs. Deferred Unity destruction is an observable fence, not assumed complete after Destroy.
- [Asset preflight](../FTKModFramework/Core/HotReload/PaladinAssetPreflight.cs): strict GLB data/rig checks, native held/display/apparel targets, and model texture decoding. Incomplete prefab rig evidence fails closed. The final enabled-adventure trial observed novice equipment on the native combat avatar; exhaustive gear and avatar coverage remains open.
- [PNG admission](../FTKModFramework/Core/PngStructure.cs): bounded container structure, chunk CRCs and ordering, image-data presence and terminal IEND before Unity allocation; decoded dimensions must match the header. This rejects the IHDR-only icon Unity initially accepted. It does not independently validate compressed pixels.
- [Boundary](../FTKModFramework/Core/HotReload/HotReloadBoundary.cs): monotonic session latch, scene/native/network checks, entry guards including Steam invitations, protected save-path verification, and supported-plugin admission.
- [Marketplace helper](../launcher/helper/marketplace.go): `hot-validate` writes recovery intent without changing current; `hot-commit` revalidates bytes and compare-and-swaps current/pending. After an interrupted attempt, normal startup quarantines that uncommitted target instead of silently activating it. Current is the durable decision if an acknowledgement is lost.
- Mods menu: experimental Apply prepared mods now action in the pending-changes view. UI selection, history and plans are invalidated around publication. Existing prepare/install/update/remove flows remain the source of candidate generations.
- [Runtime probe](../tools/ai-model-pipeline/runtime-test/HotReloadProbe.cs): protected main-thread status, apply and failure injection through the existing command-file harness. It does not launch a game.

## Deliberate restrictions and unresolved live gates

The prototype does not write PlayerPrefs to repair class selections. It rejects activation if a remembered class would change meaning, including an out-of-range remembered class becoming another definition. This avoids treating a copied executable as an isolated preferences profile. The live copy uses verified disposable preferences and persistent profile handling; the runtime-test save namespace alone does not isolate every native preference write. See the live evidence record for the native path findings and launch correction.

The actual native save path must match the runtime-test namespace derived from the exact disposable root. No real saves may be loaded. New test adventures may create saves only inside that protected namespace. Save compatibility stamps, existing-save migration and multiplayer fingerprint negotiation are not implemented in this proof, so those routes remain outside acceptance.

The asset preflight reads native prefab bindings without instantiating an avatar. It follows native Stitcher flattening and name-maps the garment palette against playable avatar prefabs. Missing or ambiguous evidence fails closed. Live Paladin preflight passes. Native combat appearance supports the five-piece novice equipment trial; the blank world-avatar studio capture is not visual evidence. Exhaustive equipment, avatar and ability coverage remains a separate gate.

Game-free tests cover the constituent state, resource and helper protocols. Live transaction, cycle and fresh-adventure evidence is recorded separately; do not infer rendering or gameplay correctness from a build or menu success message. Broader native reward/consumer census and post-adventure teardown remain outside this slice.

## Game-free evidence

Checks executed in the experimental worktree:

- Framework Release build against the installed assembly, read-only references.
- Runtime-test plugin Release build against the existing isolated copy, using an explicit managed-directory override for its renamed app. This builds a local output only; it does not deploy or launch.
- HotReloadDefinitions: 100 baseline/candidate/rollback histories, exact array/index/map reference restoration, real forced FNV collision history, replaced-component rejection, failed proficiency initialization cleanup, cache commit/retirement and vanilla-instance preservation.
- HotReloadResources: 320 ledger checks including 100 ownership cycles and deferred-destruction fences, using Unity doubles.
- PlayerMods: current Paladin strict JSON shape, unknown-capability rejection and existing marketplace/discovery/UI protocol checks.
- [PNG structure tests](../FTKModFramework/Tests/PngStructure/Program.cs): malformed containers, CRC/order failures and bounded dimensions, including the IHDR-only regression.
- PackageModels: 23 checks; GuardianCombat: 145 checks; ItemApparel: 38 checks.
- Production GLB loader/preflight: 36 checks plus 78 static and 18 skinned Paladin assets. Skin checks use self-consistency data, not native game bones. Renderer transaction suite: 188 assertions with Unity stand-ins.
- Helper Go tests, including hot-commit intent requirements, changed pending revision, changed generation bytes, interrupted activation quarantine and committed recovery.
- `git diff --check` and Python command-harness syntax check.

The 100-cycle results above are **game-free tests**, not 100 live enable/disable cycles. The final offline pass completed successfully. Initial test invocation errors were corrected by supplying required fixture paths and the renamed isolated app managed-directory override. The framework build reports seven existing unused-field warnings; the runtime-test plugin build reports none.

The GLB verification can be reproduced without launching the game:

```bash
python3 tools/ai-model-pipeline/multi-primitive-tests/make_fixtures.py /tmp/ftk-hot-glb-preflight-fixtures
dotnet run --project tools/ai-model-pipeline/multi-primitive-tests -c Release -- /tmp/ftk-hot-glb-preflight-fixtures marketplace/packages/paladin/assets
```

The original 100-cycle build and intermediate PNG-admission build are separate evidence sets. That intermediate build completed 10 further full cycles and repeated transaction/failure checks; these are not a 110-cycle single-build leak trial.

Deployed framework identities:

| Artifact | SHA-256 |
| --- | --- |
| Original 100-cycle framework DLL | `6db417cdbf12c3ea4c2af0aa833b998a699d27cc38e01f004b937d672525f236` |
| Original cycle runtime-test DLL | `e6a4e8b076b539f1eb83803d5f06341371c0edd0e40ad0ce945b0ff74e870c5e` |

The intermediate PNG-admission framework SHA-256 is
`606a0edfc8ed9a1a7dc48c957f5c9a93369fe33e7356d3dce68f8b21fff1c920`.

The final baseline-index correction uses framework SHA-256
`92f3f93604976105018a106480c4168013d09cd19b9667233c1e6e6ebe5207e0`.
The initial baseline had captured three null native indexes; disabling restored them
and broke the next native New Game configuration screen. Baseline capture now calls
native CheckAndMakeIndex first, and candidate validation proves complete exact
vanilla/custom row lookups before cache rebuilding and commit. The regression suite
starts with null indexes and rejects missing, stale or wrong-row indexes. A full 100-cycle live repeat with those new assertions passed, followed by the
transaction suite and a fresh disabled adventure. Earlier cycle counts alone did not
prove vanilla lookup completeness.

## Observed fresh-adventure boundary

After hot activation of the updated package, native Lost Civilization setup displayed
Paladin Reload Fixture. The created Paladin had all five custom novice equipment
pieces. All 36 equipment rows were exact members of their current native category
cache lists, and native town-stock generation included Paladin equipment. This proves
current-row pool participation and observed stock, not acquisition of every tier.

In a native Timberwolf combat reached through an isolated encounter fixture, Guard
selected an ally through native targeting and resolved with protection active, rescue
available and no Focus spent. A focused attack killed the wolf. Native combat appearance
showed the novice custom equipment. This is bounded gameplay evidence, not exhaustive
ability or equipment verification.

Native return to title retained PID 78047 and the permanent activation seal. Guardian
transient state remained nonempty. This directly supports keeping post-adventure
activation unsupported: returning to title did not clear the gameplay state. A separate final corrected-build trial started a vanilla adventure after 100 hot
toggle cycles, with no Paladin rows or town stock and no native lookup failures.

## Decisions before implementation can advance

The final corrected build also booted disabled, hot-activated the update fixture,
started a fresh Paladin adventure and resolved native Guard against a Beastman Warrior.
Return to title remained sealed. Both final adventure selections therefore have their
own live evidence; exhaustive equipment/ability coverage remains outside this proof.

The prototype remains unmerged. The two-second activation target failed: the original
100-cycle trial measured enable median 3.71 seconds, p95 10.84 seconds and maximum
32.72 seconds, excluding generation preparation. The final build measured enable
median 3.82 seconds, p95 4.13 seconds and maximum 4.55 seconds, still above target.
Agree on a responsiveness budget and
profile transaction phases before selecting optimizations. Approximately 7.0 GiB of
immutable generations accumulated across all preserved trials; define retention that protects current, pending,
rollback, in-flight and any future save-pinned generations before release.

The native Lore Store retains display objects and purchase callbacks outside this
transaction. Current Paladin has no lore entries, so a stale Paladin reference was
not demonstrated there. However, Back-to-title and other non-adventure title panels
remain reachable during commit. Lock those transitions and explicitly admit current
screens before shipping or expanding package support. See the live report for evidence.

Choose a policy for remembered class choices when disabling a class; the prototype
rejects semantic changes. Choose whether the next scope remains initial-title Paladin
only, define the required
final-build regression and equipment/ability coverage, and reconcile the experimental
delta with concurrent original-checkout work before merging. Existing-save compatibility,
multiplayer, arbitrary DLL/plugin unloading and post-adventure reset remain unsupported.
Automatic restart remains a separate fallback, not the hot activation result.

## Isolated verification procedure

1. Prepare a new task-owned disposable copy under this worktree's scratch directory. Do not repurpose another session's game copy, profiles, ports, logs or saves. Verify process, save and preferences isolation before launch.
2. Build and deploy pinned framework/runtime-test binaries using the isolated deployment script. Build the matching helper and update its verification record in that disposable copy only. Preserve artifact hashes and deployment receipts.
3. Use the protected runtime-test launch route with exact root, empty package-only profile, unique free bridge port, and experimental flag. Verify the protected save path and current session before issuing commands. Use only an explicitly authorized live-test window.
4. Start with an empty managed selection. Prepare Paladin through the marketplace, apply, and capture status, IDs, row/cache/resource counts and PID. Repeat through disabled/enabled selections, an update carrying both changed and unchanged assets, removal, and reinstall.
5. Inject `after-reset`, `after-load`, `after-cache`, and `before-commit` failures. Verify previous rows, assets, caches and durable selection remain intact. Exercise helper rejection, lost acknowledgement and crash recovery with isolated fixtures.
6. Run 100 live toggle cycles with frame fences, object identities, resource counts and native/managed memory samples. Compare final IDs with a clean boot of the same package bytes. Check invalid assets, stale UI callbacks, invitations/queued joins and denied boundary transitions.
7. Start a fresh adventure using the final generation. Observe abilities, starting gear, acquisition, all equipment/model categories, and avatar lifecycles. Separate final enabled/disabled runs use separate processes because session setup seals activation.
8. Stop only the owned test process and preserve evidence. Merge consideration requires successful live results and review of the experimental delta against the copied baseline. A build or menu success message is insufficient.

See [the feasibility design](HOT-RELOAD-FEASIBILITY.md) for the broader architecture, transaction requirements and acceptance matrix.

### Remembered class preferences

Activation captures the three native `PlayerNclass` preferences by semantic class ID.
Surviving classes are remapped to their candidate array positions. Removed or invalid
selections use the first released, DLC-free native class which the game's local
`IsUnlock` check accepts. Missing preferences and the native `-1` unset sentinel remain
unchanged. This affects remembered new-party choices, not save contents.

Before committing the generation, the framework writes `class-preferences.intent` in
the marketplace state directory. Preferences change only after the helper's durable
current generation matches the candidate. A failed activation preserves the original
preferences. Startup resolves an outstanding journal before marketplace bootstrap,
even when hot reload is disabled: old current restores old preferences; candidate
current finishes the remap. An unrelated generation, malformed journal, or concurrent
preference edit fails closed and requires repair before entering gameplay. All slots
are checked before any write. Recovery accepts either recorded side to finish a
partially saved preference update without overwriting unrelated edits.

The net35 journal uses write-through file writes and an atomic rename. Game-free tests
cover process interruption recovery; power-loss durability and platform-specific
PlayerPrefs persistence are not established by those tests. Run the focused suite with
`dotnet run --project FTKModFramework/Tests/ClassPreferences/ClassPreferences.csproj -c Release`.

### Exact-set adventure save namespace (pending live validation)

The save namespace adapter identifies a library by the audited native assembly SHA-256,
framework version, sorted enabled package GUID/artifact SHA-256 pairs, and registration
settings. Generation IDs are excluded: preparing identical content again or restarting
returns to the same `persistentDataPath/ftkmf-saves/<fingerprint>` directory. Disabled
packages do not affect the identity. A durable `save-pins/<fingerprint>.json` records an
immutable generation for garbage-collection retention at adventure admission, save
creation, or resume. Empty sets pin their canonical empty generation so that the
same library can be selected again. Preparation creates no
pin and does not publish a new active directory. Publication occurs only after the durable generation commits.

Native save getters also serve lore, tutorial progress, custom controls, and other
profile data. The adapter therefore preserves those getters and `SAVE_PATH` fields.
Audited call-site patches route adventure filename generation, resume-browser scanning,
and main-menu scanning to the exact-set directory. The three adventure methods which
read or write `LastSave` use a fingerprint-specific preference key. Legacy old-save
filename conversion also stays inside the new directory. The native main-menu selection
and resume-browser rows are invalidated when a committed namespace changes.

Existing protected runtime tests retain their original isolated path. Namespace testing
requires the additional `FTK_HOT_RELOAD_SAVE_TEST=1` flag. Game-free identity/pin checks:
`dotnet run --project FTKModFramework/Tests/SaveNamespace/SaveNamespace.csproj -c Release`.
Required live gate: create a new save, quit, reopen the exact package set, confirm only
its saves appear, and resume it; change or remove Paladin and confirm the original save
is hidden; restore identical artifact bytes in a different generation and confirm the
original save reappears. Legacy saves and shared profile files must remain unchanged.

### Candidate asset preflight scheduling

The main thread resolves immutable model paths and copies native bone names and bind
matrices before scheduling GLB validation. At most four workers validate file bytes
with the same strict decoder used by synchronous preflight. Workers never access
Unity objects or package-path registries. The iterator waits for every worker before
reporting the first failure in job order; rollback must not interrupt that wait.
Texture decoding and destruction remain on the main thread, one texture per iterator
step. No validation cache crosses an activation transaction. Game-free worker tests:
`dotnet run --project FTKModFramework/Tests/PreflightWorkers/PreflightWorkers.csproj -c Release`.
Responsiveness and timing improvements require fresh live measurements.

### Selecting a saved mod set

Settings & Help now includes Saved mod sets. The list reads bounded generation/pin
metadata and counts adventure filenames without deserializing saves. Incompatible,
missing, corrupt, or linked records are unavailable. Reviewing a library captures the
current and pending generations; confirming explicitly replaces pending community
changes and opens the prepared-change view. Applying and resuming remain separate user
actions. Listing libraries does not seal the session or create namespace directories.
Reader fixtures run with
`dotnet run --project FTKModFramework/Tests/SavedSets/SavedSets.csproj -c Release`.
The native layout, controller navigation, pending-conflict response, and restoration
followed by resume still require live verification.
