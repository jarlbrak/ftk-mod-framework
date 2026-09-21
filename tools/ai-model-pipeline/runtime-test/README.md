# Isolated FTK runtime model test plugin

Separate developer tooling, not a production framework patch. It lets one running
single-player game inspect enemy rigs, replace explicitly selected meshes, and
capture motion inside the game. It does not turn a successful mesh load into a
claim of visual quality or normal-combat validation.

## Build and launch

Build against an existing disposable copy. No original Steam path is a default:

```sh
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj -c Release \
  -p:TestGameRoot=/absolute/project/scratch/game-copy
```

Managed references auto-detect the Mac bundle path
`FTK.app/Contents/Resources/Data/Managed` or `FTK_Data/Managed` under that copy.
An explicit `TestManagedDir` overrides detection. For example, Windows:

```powershell
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj -c Release `
  -p:TestGameRoot=C:/Projects/FTK/scratch/game-copy `
  -p:TestManagedDir=C:/Projects/FTK/scratch/game-copy/FTK_Data/Managed
```

Linux or a differently named executable data directory:

```sh
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj -c Release \
  -p:TestGameRoot=/home/user/FTK/scratch/game-copy \
  -p:TestManagedDir=/home/user/FTK/scratch/game-copy/FTK_Data/Managed
```

BepInEx references still come from `TestGameRoot/BepInEx/core`. These are portable
build-path options; build and live runtime testing have been performed on macOS
only. They do not establish Windows/Linux runtime compatibility.

The framework DLL is `../../../FTKModFramework/bin/Release/net35/FTKModFramework.dll`.
The helper DLL is `bin/Release/net35/FtkRuntimeModelTest.dll`; the paired content
plugin is `runtime-test-content/bin/Release/net35/FtkRuntimeModelTestContent.dll`.
Select whichever framework, helper, or content binaries changed, stop the
isolated game, and use `../deploy_isolated_test_binaries.py` for a dry review
followed by its explicit `--execute` replacement. It creates a
hash-pinned backup under that exact isolated copy and refuses a running game or
symlinked path. Do not manually replace a helper in a live process.

Launch the copy with `FTK_MODEL_TEST=1`, `FTK_MODEL_TEST_ROOT` equal to its exact
absolute directory, `FTK_AGENT_BRIDGE=1`, and a dedicated
`FTK_AGENT_BRIDGE_PORT`. Preserve any required content opt-in such as
`FTK_MIREWARDEN_BODY=1`; otherwise a default procedural body can conceal the
custom model. The directory must be directly beneath `scratch`, and may not be
a symlink. Check framework, helper, and content-plugin hashes and the `MODEL TEST ACTIVE` log.

The plugin uses `save-model-test-<16 hex digits>`, derived from SHA-256 of the exact
canonical game-copy root. Different copies receive different stable namespaces.
Early static assignments support legacy save
paths and Harmony prefixes keep `GetSavePath`/`GetSavePathSlash` in this namespace
across scene transitions. It does not modify PlayerPrefs, but the game can still
read/write shared PlayerPrefs. Use a disposable run, and never load a real save.
Runtime results report the namespace; actual save behavior needs live verification.

## Validate a separately installed content package

For package gameplay tests that must not register unrelated model fixture content,
set `FTK_MODEL_TEST_PACKAGE_ONLY=1` and supply `model-test-profiles.json` with exactly
`{"version":1,"profiles":[]}`. The content plugin records `mode: package-only`;
nonempty fixture lists are rejected in this mode. This retains the isolated save
namespace and general diagnostic operations. An existing player fixture catalog
is also rejected, so it cannot silently add unrelated classes. This mode
does not create an enemy/player
profile or satisfy any profile-specific model validation gate. Normal model tests
still require their nonempty validated catalogs.

In package-only mode, `player-preview-state` accepts an exact registered custom
`classKey` and native `skinset`, without `catalogSha256`. It observes an existing
native Party Select avatar and its renderer/resource state; it never creates a
fixture class. The result labels its selection source as the registered class
database and sets `assetManifestCompared: false`. This is runtime inspection,
not comparison against a fixture asset manifest.

## Run one pinned execution-queue route

For a direct-enemy or resource-prefab route already marked
`stage_ready_revision_available`, use `run_execution_queue_route.py` to bind one
exact queue entry to one owned isolated process. Start with its dry plan:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/game-copy \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy
```

It proves the current queue/readiness hashes, profile-document hash, isolated
catalog hash, declared model assets, and selected source assignments before it
can launch. The stage-readiness report may resolve several historical documents
through the hash-pinned profile selection ledger while keeping every alternative
visible. Pass `--profile-document` when several documents remain and no current
selection exists. Pass `--motion-renderer-path` when it has more
than one selected skinned renderer. Player skinsets and adapter-or-retarget
routes use their dedicated workflows.

For all current routes, `../plan_model_validation_campaign.py` validates these
same inputs and emits exact route commands in one read-only JSON plan. It keeps
ordinary enemy trials, once-only passive arrival, native player workflows, and
adapter implementation work separate. It never launches the commands or
combines their evidence.

Player plan commands include a new `--output` path directly under `scratch/`.
Once written, the campaign compares the complete recorded plan with the current
route plan and reports it as current or requiring inspection. It never treats a
matching plan as native preview, motion, gameplay, visual, or archive evidence.

Regenerate that campaign after a live route. If the proposed runner record now
exists, it validates the record and case identity, then reports whether the
route awaits a review template, manual review, or archive planning. A stale,
invalid, partial, or errored record requires inspection and is never overwritten
or converted into an automatic retry.

When inspection reports `enemy_trial_record_identity_mismatch`, compare the
runner plan with the current route field by field. If the profile document,
catalog, assets, topology, route kind, source assignments and motion renderer
all match and only the generated queue or stage-readiness hashes changed,
preserve the immutable runner and write a pinned reconciliation record. Review
its original frames and build a new archive without replaying the live actions.
Any changed semantic field requires a newly planned trial. The canonical
[Verdigrin V4 archive](../../../art-experiments/verdigrin-mimic/live-validation-v4/README.md)
is the reference for this snapshot-only reconciliation workflow.

Append `--run --output scratch/my-route-run.json` only after the dry plan is
current. The runner will reject a concurrent FTK session and a busy bridge port,
then launch and stop only its own isolated game process. It executes one binding
stage and one bounded exercise, preserving the stage/case result paths in its
new record. The record still requires manual image review and a separately
verified immutable archive before any coverage ledger changes.

Create the review template from the retained case result with
`../prepare_model_visual_review.py CASE_RESULT --output scratch/my-review.json`.
It pins selected original images but leaves every observation pending. Inspect
and edit that template before using it to create the archive plan.
If a bounded retry leaves more than one accepted action with the same base
name, use the unique archive keys in reviewed frame rows, such as
`attack-attempt-1` and `attack-attempt-2`. Reusing `attack` plus the same frame
index would create an ambiguous selected-image filename, and the archiver
rejects that collision before writing the destination.

After every reviewed observation is concrete, create and verify the immutable
supplement through the generic route workflow:

```sh
python3 tools/ai-model-pipeline/make_execution_queue_archive_plan.py \
  --record scratch/my-route-run.json \
  --visual-review scratch/my-route-root-review.json \
  --plan-output art-experiments/my-model/live-validation-vN-plan.json \
  --archive-output art-experiments/my-model/live-validation-vN \
  --revision VN \
  --limit 'Exact source scope and remaining limitation.'
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/my-model/live-validation-vN-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/my-model/live-validation-vN \
  --check-video-metadata
```

Repeat `--limit` for every meaningful boundary. The plan generator rejects a
pending review or drifted runner, case, profile, catalog, asset, frame, or
session pin. The archive verifier checks artifact integrity only; canonical
coverage still requires an exact runtime-index record and regenerated coverage
ledgers.

## One enemy validation sequence

After `run_case.py` stages an exact enemy in an owned isolated game, use
`exercise_case.py` to record one pass/enemy attack, an ordinary hero attack,
an explicit fixture death, and native loot progression. The default is one
ordinary attack; native rows whose first attack can be blocked or protected
may opt into a bounded sequence:

```sh
python3 tools/ai-model-pipeline/runtime-test/exercise_case.py \
  --root /absolute/project/scratch/game-copy --port 8788 \
  --enemy YOUR_REGISTERED_ENEMY_KEY --renderer-path 'EXACT_RENDERER_PATH' \
  --profile-sha256 SHA256_OF_DEPLOYED_MODEL_TEST_PROFILES_JSON \
  --attack-attempts 8 --capture-timeout 360
```

The hash covers the entire deployed catalog, not an individual combat profile.
Select the renderer path from that exact profile. To stage and exercise a new
candidate from an existing eligible native Ready slot, add `--from-ready
--level LEVEL --room ROOM` with the currently observed indices. Staged mode
accepts neither index. This does not start a campaign, restart the game, cross
stairs, replace non-enemy rooms, or continue to another candidate.

Each action capture in this exercise now requests the passive native combat
motion observer. It arms only when the exact selected enemy CEL is enabled and
settled at `Base Layer.IDLE` and the bound renderer resolves to that CEL's native
Animator. The first retained PNG must still show that exact idle state before an
action can qualify. The observer then records bounded `PlayAttackSequence` entries
and exact target `CombatTrigger` entries while the existing bridge owns the
unchanged action. The case requires an enemy-attacker event for pass, a
`Damaged` or `DamagedHeavy` victim response and matching target trigger for the
ordinary hit, and a `Death` victim response and target trigger for the
KillSingle fixture. The raw capture's `motionObservation` plus the case entry's
`motionEvidence` identify the original PNG after each event. This is native
action provenance and a review pointer, not automatic pose, art, clip-coverage
or ordinary-lethal acceptance.

Only one operator may issue helper commands or game actions during the run.
The runner claims the session/slot and stops on uncertain actions, changed
identity/pins, or an ordinary attack without observed nonlethal HP loss. With
`--attack-attempts N`, a later attack is issued only after the preceding attack
has a complete same-target `no_hp_loss_unclassified` observation; the sequence
stops on any other outcome and still requires measured nonlethal HP loss. This
bounded retry preserves an observed `Block`, `Dodge`, or absent exact victim
response as an `attackResponseObservation`, but does not infer block, dodge,
protection or immunity from it. Only the accepted positive-HP-loss attempt must
also prove the exact `Damaged`/`DamagedHeavy` target motion and trigger. Uncertain
actions are never retried. A stopped run is evidence to inspect, not permission
to rerun the command. Known
renderer destruction during death preserves the raw partial failure; it does
not become a complete capture. Loot clicks require a fresh native Collect
predicate and observed progress before another submission.

If a retained no-focus process proves repeated exact native `Block` outcomes,
one separate fresh execution-queue run may opt into the disposable hero-side
skill fixture:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/game-copy \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --attack-attempts 8 \
  --cap-equipped-attack-skill \
  --run --output scratch/my-native-cap-run.json
```

The fixture requires exactly one equipped right-hand weapon and zero spent
focus. It resolves that weapon's real `_skilltest`, raises only the matching
hero augmented stat to `GameFlow.m_MaxCharacterStat`, verifies the resulting
zero-focus value, and records the weapon, skill, native cap, exact augmentation,
and before/after values. It does not set attack results, damage, enemy stats,
focus, RNG, or animation state. The native roll is still probabilistic, and the
fixture is balance-unrepresentative even when the later action is an ordinary
zero-focus combat action. Preserve every preceding Block attempt and the full
fixture receipt with the accepted hit. Do not use this option with
`--skip-fortify` or `next-case`.

If two separately retained bounded no-focus routes still prove exact native
no-loss responses, inspect the native damage calculation and source row before
trying another route. When that evidence supports a conservative threshold, one
further fresh run may combine the skill cap with an explicit minimum native
weapon maximum damage:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/game-copy \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --attack-attempts 8 \
  --cap-equipped-attack-skill \
  --minimum-native-weapon-max-damage 30 \
  --run --output scratch/my-native-damage-run.json
```

The damage request must be 1 through 100, exceed the inspected current maximum,
and require no more than 50 additional physical damage. After entry preflight
and immediately before dungeon entry, `run_case.py` uses
`hero-damage-fixture` to inspect and pin the exact hero, stats object, equipped
native weapon and database row, character event listener, Animator, controller,
focus and before-values. It applies only the required physical augmentation
through native `AugmentCharacterOther(PhysDmg, delta)`. It does not replace the
weapon, spend focus, set RNG, choose an attack result, issue animation triggers,
or modify the enemy.

Derive the minimum from the exact decompiled enemy source row and native damage
calculation. It must exceed the effective physical armor boundary. A native
skill cap can improve the roll while repeated successful actions still resolve
to Block when damage cannot penetrate armor. Preserve the inspected source value
and any runtime-scaling caveat instead of probing higher damage values blindly.
For Jungle Snake C, source armor 32 justifies a conservative minimum of 33.

The stage result stores the exact receipt. `exercise_case.py` accepts it only
from that same session and isolated output root, then issues the normal
zero-focus native action. After death and loot handling reach native between-room
`Ready`, it makes one receipt-bound restoration attempt and verifies the exact
prior augmentation and the native maximum expected at the hero's current
legitimate level. Native combat may award XP and levels before Ready. The restore
must preserve XP and level progression, remove only the fixture augmentation,
and report the original maximum, current-level expected maximum, actual restored
maximum, and level progression used in the calculation. Object, controller, focus, weapon,
row or invariant drift rejects restoration. An error path may attempt restore
once, but never retries it; if combat remains active the helper rejects the
mutation and the runner stops without granting later action credit. Preserve
that rejected state rather than treating process disposal as restoration.
Every result under this fixture is balance-unrepresentative, remains
probabilistic, and cannot establish ordinary lethal damage. Amberwake V3 is the
canonical 10 to 30 maximum-damage, 11-damage hit and exact Ready-time restore
example. Bramblecoil V2 records source armor 32, a requested minimum of 33,
maximum damage 12→35, an ordinary native HP 86→85 hit, XP 0→110 and level 0→2,
then restoration of augmentation 23→0 and the correct level-adjusted maximum 12.
Its earlier stale-wrapper rejection remains rejected evidence even though the
helper itself restored the correct current-level value.

The 120 full-size PNGs in one fixed capture can take several minutes to encode
and visibly slow the isolated game. The default capture budget is 360 seconds;
that is observation time for the one already-issued capture, never permission
to submit a second action or capture.

After a completed no-focus bounded result, a separate fresh run may add
`--focus` to spend native maximum focus for a strongest legitimate attack. Its
raw case is explicitly marked `focusedAttack: true`; preserve the no-focus
boundary beside it, and do not relabel focused damage as ordinary no-focus
damage.

Successful completion prints a `case-result.json` path with status
`needs_visual_review`. Its journals, raw captures, PNG hashes, HP observations,
and final Ready snapshot support the recorded mechanics. `selected-frames.html`
is a small deterministic sample, initially marked unreviewed. Inspect the clip
occurrences in each capture summary and additional attack, hit, and death poses;
the default selected frames can miss those moments. Record visual findings in a
separate artifact and preserve the original raw result. An explicit `KillSingle`
death is not ordinary lethal damage, and one renderer does not validate other
parts of a multipart model.

The first live trial used the exact fairyA calibration profile in session
`5d8860c3aa834e8286ad4d97dedea7c4`, case
`9fa034b1acf246eaa5b8b8dc144aaf01`: three 120-frame captures, ordinary HP
58 to 55, one native Collect, then strict Ready at level 0 / room 3.
This establishes that sequence on that case, not universal rig or art acceptance.

## Preserve a model evidence archive

After root visual review, keep the case result and original PNGs in a separate
asset-local supplement. The repeatable pattern is visible in the
[Rimecrown V3](../../../art-experiments/rimecrown-sentinel/live-validation-v3/archive.py),
[Mirewarden V2](../../../art-experiments/mirewarden-ftk/live-validation-v2/archive.py),
the generic-plan [Mirewarden V3 exact-source archive](../../../art-experiments/mirewarden-ftk/live-validation-v3/README.md),
[Rustpetal V1](../../../art-experiments/rustpetal-snapper/live-validation-v1/archive.py) and the generic-plan [Rustpetal V2 canonical exact-source archive](../../../art-experiments/rustpetal-snapper/live-validation-v2/README.md),
[Belladusk V2](../../../art-experiments/belladusk-pitcher/live-validation-v2/archive.py)
and the corrected-timeline [Belladusk V3](../../../art-experiments/belladusk-pitcher/live-validation-v3/archive.py),
[Cinderbloom V3](../../../art-experiments/cinderbloom-plant/live-validation-v3/archive.py),
[Emberjaw V2](../../../art-experiments/emberjaw-skull/live-validation-v2/archive.py),
[Cinderwing V2](../../../art-experiments/cinderwing-bat/live-validation-v2/archive.py),
the generic-plan [Cinderwing V3 exact-source archive](../../../art-experiments/cinderwing-bat/live-validation-v3/README.md),
[Bronzewake V2](../../../art-experiments/bronzewake-champion/live-validation-v2/archive.py),
[Bronzewake V3](../../../art-experiments/bronzewake-champion/live-validation-v3/archive.py),
[Bronzewake V4](../../../art-experiments/bronzewake-champion/live-validation-v4/README.md),
[Bronzewake V5](../../../art-experiments/bronzewake-champion/live-validation-v5/README.md),
the generic-plan [Bronzewake V6 exact-boots-source archive](../../../art-experiments/bronzewake-champion/live-validation-v6/README.md),
the generic-plan [Bronzewake V7 exact-hair-source archive](../../../art-experiments/bronzewake-champion/live-validation-v7/README.md),
[Bronzehollow V2](../../../art-experiments/bronzehollow-sentinel/live-validation-v2/archive.py) and the corrected generic-plan [Bronzehollow V4 native-cap archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v4/README.md),
[Tamarind V2](../../../art-experiments/tamarind-trickster/live-validation-v2/archive.py),
[Verdigrin V2](../../../art-experiments/verdigrin-mimic/live-validation-v2/archive.py), [Verdigrin V3](../../../art-experiments/verdigrin-mimic/live-validation-v3/archive.py), and [Verdigrin V4](../../../art-experiments/verdigrin-mimic/live-validation-v4/archive.py),
plus [Sunspire Roc V2](../../../art-experiments/sunspire-roc/live-validation-v2/archive.py), [Sunspire Roc V3](../../../art-experiments/sunspire-roc/live-validation-v3/archive.py), and [Sunspire Roc V4](../../../art-experiments/sunspire-roc/live-validation-v4/archive.py),
and [Copperveil V2](../../../art-experiments/copperveil-spider/live-validation-v2/archive.py),
with [Tideglass V2](../../../art-experiments/tideglass-crab/live-validation-v2/archive.py)
and the generic-plan [Tideglass V3 exact-source archive](../../../art-experiments/tideglass-crab/live-validation-v3/README.md)
and [Mossglass V4](../../../art-experiments/mossglass-reliquary/caps-v3/live-validation-v4/archive.py),
plus the generic-plan [Mossglass V5 exact-source archive](../../../art-experiments/mossglass-reliquary/live-validation-v5/README.md),
plus [Vesper Eye V3](../../../art-experiments/vesper-eye/live-validation-v3/archive.py)
and the generic-plan [Vesper Eye V4 exact-source archive](../../../art-experiments/vesper-eye/live-validation-v4/README.md)
and [Emberglass Bee V2](../../../art-experiments/emberglass-bee/live-validation-v2/archive.py)
and [Emberglass Bee V3](../../../art-experiments/emberglass-bee/live-validation-v3/archive.py),
plus the generic-plan [Emberglass Bee V4 exact-source archive](../../../art-experiments/emberglass-bee/live-validation-v4/README.md)
plus [Resinmaw V2](../../../art-experiments/resinmaw-bogling/live-validation-v2/archive.py)
and [Duskquill V2](../../../art-experiments/duskquill-raven/live-validation-v2/archive.py)
plus the generic-plan [Duskquill V3 exact-source archive](../../../art-experiments/duskquill-raven/live-validation-v3/README.md)
and [Basilight V2](../../../art-experiments/basilight-cockatrice/live-validation-v2/archive.py)
plus the generic-plan [Basilight V3 exact-source archive](../../../art-experiments/basilight-cockatrice/live-validation-v3/README.md)
and [Lunacrest V3](../../../art-experiments/lunacrest-clam/live-validation-v3/archive.py)
and [Reefstrider V2](../../../art-experiments/reefstrider-fish/live-validation-v2/archive.py)
plus the generic-plan [Reefstrider V3 exact-source archive](../../../art-experiments/reefstrider-fish/live-validation-v3/README.md)
and [Gloamcap V2](../../../art-experiments/gloamcap-imp/live-validation-v2/archive.py)
plus the generic-plan [Gloamcap V3 exact-resource archive](../../../art-experiments/gloamcap-imp/live-validation-v3/README.md)
and [Thistlewick V2](../../../art-experiments/thistlewick-hexer/live-validation-v2/archive.py)
plus [Thistlewick V3 exact-source coverage](../../../art-experiments/thistlewick-hexer/live-validation-v3/README.md)
and [Saffronspine pufferA V2](../../../art-experiments/saffronspine-puffer/live-validation-v2/archive.py)
and [Saffronspine pufferB V2](../../../art-experiments/saffronspine-puffer-b/live-validation-v2/archive.py)
scripts. Combat scripts read a completed `case-result.json`; the passive-arrival
script reads `arrival-case-result.json` plus its immutable setup journal. Combat
archives verify three
120-frame captures and exact profile/session identity, gzip-compresses metadata
with a zero timestamp, pins every source PNG, copies only root-reviewed frames,
and derives presentation videos. It refuses to overwrite a destination that
already contains `validation.json`.

The resulting `validation.json` records binding, ordinary HP observations,
explicit-fixture scope, native Collect/Ready progression, selected-frame
observations, limits, and hashes for every archive artifact. Independently
verify each artifact hash, each gzip decompression byte-for-byte, and every PNG
pin before linking the supplement from the skeleton register and runtime index.
Keep the raw case and historical failed runs immutable; a selected-frame review
does not upgrade a fixture death into ordinary lethal acceptance or establish
full animation, culling, material, portrait, collision or resource-lifetime
coverage.

When a complete retained trial predates the current structured archive shape,
compare the selected historical and current profile objects rather than the
catalog-wide hashes alone. If the profile, authored assets, source assignment
and motion renderer are unchanged, preserve the old run, pin the reconciliation,
review the original pixels again and write a new immutable archive. Belladusk V3
also shows why selected-frame labels must come from the raw animator intervals:
its older review called two early `idle` frames attacks, while V3 selects actual
`attack1`, `hit1` and `death` frames. A changed semantic route requires a fresh
isolated trial.

For rows that bind exactly but stop before a classified native attack, use the
[unresolved enemy probe archive](../../../docs/evidence/unresolved-enemy-probes-v1/README.md)
and its offline [archive script](../../../docs/evidence/unresolved-enemy-probes-v1/archive-script.py).
It records the complete pass/attack prefix, preserves `no_hp_loss_unclassified`
without guessing its cause, and keeps any renderer-destroyed fixture prefix
separate from ordinary lethal evidence.

For a profile that naturally removes itself before hero readiness, use the
[passive native enemy-arrival protocol](ENEMY-ARRIVAL.md) and preserve the
arrival archive separately. It has one immutable arm, one native Ready click,
and a nominal 120-sample capture; it cannot claim hero damage or loot. Keep the
first spawn-settling samples and any stale-field warning in the review so a
settled body view is not confused with full combat acceptance.

The canonical [Tamarind V2 record](../../../art-experiments/tamarind-trickster/live-validation-v2/validation.json)
shows the exact `monkeyC / enMonkeyBasey / 121301` use of this route. One complete
arrival capture is structured as settled idle, native `enSuicideCurse` attack and
native self-removal evidence, with strict Ready0/2 recorded immediately before
the single arm/Ready sequence. Its exact native behavior makes a received-hit
phase and ordinary hero damage impossible. Record such exceptions only through
`ftkmf.source-specific-evidence-applicability.v1`, with the exact chassis,
workflow, an allowed gate-specific reason code and a valid evidence pointer.
The gate auditor rejects broader or unsupported exceptions, and no applicability
classification transfers to a sibling source. Hidden terminal samples still do
not prove a visible full death, corpse or post-combat loot progression.

## Commands

The plugin writes `model-test-session.json` on startup. Every command requires
that session nonce and a new alphanumeric `id`; stale requests are rejected.
Commands are consumed on the main thread from `model-test-command.json`. Results
are written to `model-test-output/<id>.json`. Submit one at a time:

```sh
python3 tools/ai-model-pipeline/runtime-test/command.py \
  --root /absolute/project/scratch/game-copy inventory
```

Other operations take `--payload /absolute/request.json`.

`inventory`, `capture`, and `play` accept `scope`: `enemies` (default),
`player-overworld`, `player-combat`, or `player-preview`. Player scopes enumerate
only actual current avatar references: `CharacterOverworld.m_Avatar`,
non-enemy `CharacterDummy.m_EventListener` with a live COW reference, and
`uiQuickPlayerCreate.m_Avatar`. Asset prefabs and renderers outside those CEL
subtrees are excluded. Player inventory includes inactive avatars and reports
visibility separately; the default enemy scope keeps active-hierarchy filtering.

For player capture/play, copy `ownerInstanceId`, renderer ID, `rendererPath`,
mesh name, and bone signature from the same fresh scoped inventory. Ownership
must be unambiguous across all avatar scopes and is rechecked during capture.
Snapshots report owner kind/ID, both root identities, CEL-relative paths,
controller/bones/visibility, and read-only resource lease ID/reference counts
where available. Preview CELs can be parented outside the UI owner hierarchy;
then the harness `rendererPath` is explicitly prefixed `@cel/`. Production
assignment paths always use `celRelativeRendererPath`, without that prefix.
Player scopes support only inspection/capture/play; `reload` remains enemy-only.
A preview still requires the existing single-player guard to succeed.


- `inventory`: active enemy skinned renderers, exact path relative to the
  EnemyDummy (`enemyRelativeRendererPath`, also `rendererPath` for harness requests),
  and separately the CharacterEventListener clone (`celRelativeRendererPath`),
  with both root names and instance IDs. A renderer on the CEL root is `.`;
  a null CEL-relative path means it is outside that subtree. Production renderer
  assignment APIs use **CEL-relative paths**: never blindly copy the
  EnemyDummy-relative harness path. Inventory also includes mesh name,
  bone/bind signature, transforms,
  bindposes, bounds, controller and clip metadata. Capture a fresh inventory
  after every spawn or reload. Clip names are not controller state names.
- `reload`: `{"assignments":[...]}`; each assignment needs `rendererId`,
  `rendererPath`, `expectedMesh`, `boneSignature` copied verbatim from inventory,
  plus a `model` GLB basename and optional `texture` PNG basename. Both GLB and PNG paths resolve through the framework's actual
  `CustomModelLoader.ResolveModelPath`, matching the runtime loader. Optional `material` is
  `preserve` (default) or `neutral` (emission disabled). All assignments preflight
  before any mesh is changed. Materials are cloned during preflight, null slots
  are preserved, and a commit failure restores all original mesh/bone/material
  assignments while destroying only newly prepared assets. Prior owned resources
  are retired only after all assignments succeed. Unknown/duplicate joint names fail instead of
  accepting the loader's dropped-joint fallback. Before/after evidence is saved.
  Multipart rigs require an explicit assignment for every intended renderer;
  nothing selects the first renderer automatically. A disabled renderer stays
  disabled, so remove procedural-body overrides before judging the replacement.
- `capture`: renderer identity fields above, `seconds` (default 2), `fps`
  (default 10), `maxWidth` (default 1280, range 320–3840), and `fixedStep`
  (default false). Aspect ratio is preserved without upscaling. Unity 2017
  first reads the final screen including UI, then a temporary GPU render target
  downsamples it; only the smaller image is PNG-encoded. The active render target
  is restored in a `finally` block, and temporary textures are released.
  Limits remain 10 requested seconds, 20 requested fps, 120 frames.

  With `fixedStep:false`, this observes ordinary runtime timing. Encoding may
  reduce achieved fps and game time may lag wall time; requested duration does
  **not** establish complete clip coverage. With `fixedStep:true`, integer fps
  is required and the plugin sets `Time.captureFramerate` for the capture,
  restoring its previous value in `finally`, even on failure. This is labeled
  `offline-fixed-step-gameplay`: the game advances at fixed simulation steps
  while wall time may be much longer. It still runs normal gameplay/animation
  events and is not evidence of real-time performance. `seconds * fps` determines
  the frame count (rounded up); verify actual recorded game time and normalized
  animator progress to establish coverage, including any loop or transition.

  Each frame records realtime seconds, game seconds, unscaled seconds,
  `deltaTime`, `timeScale`, `captureFramerate`, frame number, bone transforms,
  active clips with lengths, full normalized animator state progress, Animator
  enabled/speed/culling mode, CEL `m_DoRagdoll`, and up to 128 child Rigidbody
  paths/kinematic flags/positions/rotations/velocities (with total/truncation
  fields). `m_DoRagdoll` alone indicates configured ragdoll capability, not that
  physics has taken over. Native death may intentionally disable the Animator
  near the beginning of a death clip and continue via nonkinematic rigidbodies.
  Judge that transition from Animator state, physics flags and measured bone/body
  movement; a frozen normalized time does not by itself prove failed death motion.
  PNG readback/encoding happens after pose/time sampling for that frame.

  Enemy-only `motionObservation:true` is available for an ordinary `capture`,
  never `play`. It requires the selected live custom skinned renderer's exact
  enemy/CEL identity, that renderer's association with the CEL's native Animator,
  and an enabled settled `Base Layer.IDLE` state before starting. Its passive bounded observer records only matching native
  `CharacterDummy.PlayAttackSequence` and `CharacterEventListener.CombatTrigger`
  entries, with target identity, event frame and finalizer exception state. It
  records the native Animator state with each retained frame; exercise evidence
  rejects an action that precedes the retained idle frame. It does not call a
  trigger, select a combatant or send a bridge action. The
  capture result adds `motionObservation`; use `exercise_case.py` to interpret
  its pass, hit and fixture-death evidence safely.
- `play`: same fields/timing as capture (including optional `fixedStep:true`) plus exact `state` and optional `layer`
  (default 0). `Animator.HasState` must validate the state before playback.
  Native animation events remain active and may affect gameplay. This is
  explicitly labeled `native-state-playback`, not a normal combat action.
  Reset the disposable encounter afterward; restoring an animation state would
  not undo event side effects.
- `combat-trigger-capture`: enemies only, with the same exact renderer identity
  and capture timing fields as `capture`. It has no state or trigger input. The
  helper requires the sole current mapped enemy to be alive, not the current
  attacker, and settled at non-transitioning `Base Layer.IDLE`, with an enabled
  native `DeathLight` Animator trigger. It additionally requires the native
  fight-order head to be a player whose `CharacterDummy` FSM is at `Wait For
  Stance` before it issues the trigger. The target's
  `m_ActionAnimationPlayed` is recorded as prior-action telemetry rather than
  rejected: native `ActionCompleted` does not clear that historical latch. It
  invokes native
  `EnemyDummy.PlayAnim(DeathLight)` once, which reaches
  `CharacterEventListener.CombatTrigger`, then captures the resulting frames as
  `native-combat-trigger:DeathLight`. This probes the native trigger path and
  its animation events. Its result also contains a passive exact-CEL
  `motionObservation` record armed before the direct call. It does not prove ordinary lethal damage selects
  DeathLight, loot/Ready progression, or full death cleanup. Reset the isolated
  encounter afterward because the trigger changes animation state.
- `select-room`: `{"enemy":"exact-row-ID"}`. Outside combat, selects an existing
  generated room containing that enemy. Generate rooms with the existing
  bridge's `dungeon_regen` first. Selection is synthetic setup. Wait for native
  spawn/camera initialization; never force an early dungeon acknowledgment.
  This operation does not invent enemies or clear a campaign.

- `stage-enemy`: `{"enemy":"wolfA","level":0,"room":1,"regenerate":true}`.
  Requires both encounter sessions outside combat. Optional `regenerate:true`
  calls native `GenerateDungeonEncounters(dungeon, dungeonRandom)` and selects
  the replacement room in the same main-thread Update. Generation and requested
  indexes validate before the generated dictionary is assigned. Call immediately
  after `enter_dungeon` succeeds, before waiting for camera/dialog flow; this
  removes the separate `dungeon_regen`/stage timing gap. A rejected staging request
  must stop the caller: never follow it with `dungeon_encounter`.
  The operation
  validates an exact DB row and its native enemy/weapon assets, then substitutes
  a new `RoomInfo(Enemy, null, [rowID], -1)` into that existing generated room.
  This removes inherited boss dialogue/context and selects the room without
  creating fake dummies. Use normal `dungeon_encounter` if native flow has not
  already started. It is disposable test setup, not production content authoring;
  use a fresh test encounter for each chassis and never force an early ack.

- `stage-next-enemy`: `{"enemy":"wolfA","level":0,"room":2}` replaces only
  the **current** generated room during native Ready preparation. Both supplied
  indices must match the actual current indices; no regeneration or index changes
  occur. The strict gate requires ES not in combat, MC encounter type exactly
  `Ready` (not `Break`), both vote types `Ready`, enabled native vote FSM,
  an existing empty fight order, an alive single-player party, and an active
  native Ready button. Replacement uses a fresh context-free Enemy room with
  validated native assets. Neither encounter session nor its FSM/ack is changed.
- `ready`: `{}` clicks the actual active Ready `VoteButton.OnLeftClick` path
  under that same strict predicate. With multiple visible hero Ready buttons,
  supply `{"heroInstanceId":123}` explicitly. It does not send raw FSM events,
  force acknowledgments, call Encounter directly, or change room indices.
  Native `MiniHexDungeon.NextEncounter` reads the staged slot after the vote.
  Between cases: finish combat/loot normally, inspect current indices, optionally
  `fortify-party` with `allowReady:true`, stage the next enemy, then click Ready.
  Stop on a rejected operation; do not fall through to `dungeon_encounter`.

- `fortify-party`: `{"targetMaxHp":999}` (default 999; range 1–999).
  Requires both encounter sessions outside combat and every hero alive. Optional
  `"allowReady":true` also accepts the strict native Ready preparation predicate
  below (ES is not in combat); the result records which gate was used. Uses
  native `AugmentCharacterOther(MaxHP, positiveDelta)` to reach the requested
  health ceiling where native modifiers permit, then
  `SetSpecificHealth(actualMax, false)` to heal. Existing higher maximum health
  is never reduced. Before/after HP, maximum HP, augmented HP and hero IDs are
  recorded; repeat requests do not stack bonuses once the target is reached.
  This is a boosted-HP disposable fixture, not representative game balance.
  Capture results include the fixture target. Damage, attacks and enemy behavior
  remain native. Defeated runs are rejected; this is not resurrection. Optional
  `"capEquippedAttackSkill":true` also requires one equipped right-hand weapon
  and zero spent focus, then raises only that weapon's native attack skill to
  the current native stat cap. The result records exact weapon and skill values.
  It remains probabilistic, modifies no enemy or attack result, and is intended
  only for a fresh balance-unrepresentative validation fixture after retained
  repeated native Block evidence.
- `hero-damage-fixture`: explicit `inspect`, `apply`, and receipt-only `restore`
  transaction for one exact disposable hero. All actions require single player
  and an outside-combat or strict Ready preparation boundary. `inspect` takes
  `{"action":"inspect","heroInstanceId":ID}` and returns the exact native
  weapon, source row, physical augmentation, maximum damage, focus and object
  identities. `apply` repeats the hero ID plus the inspected weapon item ID,
  augmentation and maximum damage as expected values, and supplies
  `minimumNativeWeaponMaxDamage`. It refuses an active receipt, a target outside
  1 through 100, a non-increase, a delta above 50, or authority drift. `restore`
  accepts only the exact hero ID and opaque active receipt. It restores through
  the inverse native physical augmentation and verifies the complete pinned
  authority and exact baseline. If the inverse native mutation throws or leaves
  an unexpected value, restoration uncertainty remains latched and later reads
  cannot clear it. Use the execution-queue runner instead of issuing this
  transaction manually during normal validation.
- `quiet-tutorials`: `{}` caches the current tutorial manager's nonserialized
  prompt/show flags, sets both false, and closes an existing ordinary tutorial
  through `CloseCurrentTutorial`. It never calls `OnDisbleTutorial` (which writes
  shared PlayerPrefs), never manually changes time scale, and does not close an
  existing EndGame tutorial. Before/after flags and time scale are recorded.
  `{"release":true}` restores cached flags if that manager still exists;
  return-to-title and plugin disposal also restore them. There is no global
  `ShowTutorial` patch: forced tutorials can still appear. An EndGame caller
  without an explicit force flag could also be suppressed, so **release quiet
  tutorials before testing campaign completion**; caller force behavior has not
  been verified. Always check sampled
  game time and normalized animation progress; wall time alone cannot establish
  motion while any unrelated pause is active.
- `return-to-title`: `{}` calls the native `GameLogic.RestartGameRT()` once per
  current GameLogic instance and reports **requested**, not completed. It uses
  the realtime fade/reset/disconnect path and does not call `SaveAndQuit`.
  Native cleanup may commit pending lore through the isolated save getter.
  Returning to a title-like state is not a validated fresh-process reset.
  A measured second `start_run` in the same process failed native map generation
  before custom-class application. Start a new owned game process for `new-run`;
  use strict native Ready `next-case` transitions to reuse a healthy process.

- `native-create-character-preflight`: `{}` is a synchronous, read-only
  diagnostic for the native Create Game route. It never invokes a callback,
  creates UI, constructs an avatar, changes a class, or marks the helper busy.
  It reports the present/instance/activity state of the menu, game config, game
  logic, current screen, and Create Game button; the resume, online,
  single-player, definition, `m_GameStarted`, and preview-list values; and an
  `eligible` result calculated from the exact gate used by
  `native-create-character-screen`. An ineligible response is still a successful
  diagnostic (`ok: true`); use its `checks` object to establish the visible
  native state before issuing the action. It is deliberately dispatched before
  the normal single-player command guard so unavailable game logic can be
  reported instead of rejected without context.

- `native-create-character-input-state` also reports the current native action names, positive key slots and modifier flags without remapping; this binding snapshot remains available outside Party Select.
- `native-create-character-input-state`: `{}` is a synchronous, read-only
  Party Select focus diagnostic. On the actual native character-create screen,
  it records FTKInput's current focus and selected selectable, the character
  root's enabled/focus/selected state, and each live `uiQuickPlayerCreate`'s
  owner, class, label, turn, mode, claim, interactable, focus, selected, and
  selectable-name state. It never calls `SetFocus`, selection/class methods, a
  button callback, or any UI construction method. A title/loading/hidden
  character-create state returns `ok: true` with
  `unavailable_native_party_select`; issue it after the native create-screen
  action and before using the visible keyboard controls to make the chosen
  player/class route repeatable.

- `native-create-character-screen`: `{}` is a bounded route to an actual native
  character-create screen. It accepts no payload fields and only runs while the
  current screen is the active offline Create Game `StartGameFE.GameConfig`, its
  visible Create Game button is enabled, no preview UI already exists, and the
  exact singleton/menu/config identities are pinned. It invokes only that
  button's public native callback, `StartGameFE.GameConfig.OnStartGame()`, then
  observes the game's own offline-room, map, and character-create continuation
  for at most 3,600 frames. Success requires the native character-create root,
  a map-ready signal, and a live reciprocal `uiQuickPlayerCreate`/avatar pair
  parented to its native pedestal. It does not call `ShowCreateCharacter`,
  create UI, construct an avatar, or select a class. The result records the
  initial native candidates, which may reflect the game's stored/default class
  choice. Use the normal visible menu controls to choose a different class, then
  use `player-preview-state` for the strict profile/art observation; reaching
  this screen alone does not validate custom model acceptance.

- `lease-test`: `{}` creates only throwaway GameObjects with disabled skinned
  renderers, a tiny original triangle mesh and a fresh material. Six measured
  cases cover active clones, never-activated inactive clones, and never-active
  source/clone pairs, in both destruction orders. Assertions check serialized
  renderer-reference remapping, exact ownership, shared resource/lease identity,
  reference counts (including repeated idempotent retains), survivor resources,
  and final lease removal/Unity-null assets. The test yields through real frames
  for the production plugin's `Update` pruning; it never invokes prune itself.
  Temporary objects/resources are cleaned in `finally`, including cancellation.
  Results are explicitly **synthetic ownership lifecycle evidence**, not proof
  of native avatar cloning or player model integration. The Core version must
  contain the reviewed `EnemyMeshResources` lease implementation.

No arbitrary C#, eval, general reflection invocation, save deletion, or network
listener is exposed. The ordinary FTK bridge remains responsible for legitimate
encounter/game actions. Use separate captured evidence for actual combat attacks,
hits, deaths, and deliberate animation playback.

## Current validation boundary

The isolated runtime `lease-test` passed all 106 measured assertions across six
cases with Core hash prefix `df0cb4` and helper hash prefix `134f3e`. Evidence is
local ignored `scratch/lease-test-result.json`, command ID
`f457e34566234e67a3e6b23c773e3099`. Pruning occurred through the actual production
`Plugin.Update`, with no manual prune invocation. This verifies the synthetic
ownership lifecycle only: native player avatar creation/cloning/teardown and
visual integration still require their own live checks.

Enemy mesh capture and fixture workflows have been exercised in the isolated
macOS game. Scoped player inventory and native gameplay captures have also been
exercised for the Blacksmith Female calibration avatar in overworld/combat scopes,
including one native armor59 unequip/re-equip fixture that retained all three
custom bindings through both avatar rebuilds. This does not establish arbitrary
clip playback, preview, player death, native owner cleanup, art acceptance, or
coverage of other player profiles. See the scoped evidence in
[the runtime validation index](../../../docs/model-runtime-validation.json);
do not infer native lifecycle coverage from the ownership micro-test.

## Run one prepared case with a journal

`run_case.py` implements the measured setup against an already running game you
own. It never launches, deploys, restarts, fights, captures, or clears a game.
The enemy must be an exact key in the isolated copy's `model-test-profiles.json`
and successful `model-test-registration.json`. Supply the bridge port belonging
to that same copy; the bridge currently has no root/session identity endpoint.
Helper file operations additionally use the current session nonce and unique
operation IDs. Do not run this concurrently with another controller.

Registration freshness is checked conservatively: the report's timezone-aware
`updatedUtc` must be at or after the current helper session file's creation-write
mtime and no later than the present clock; the profile input cannot have been
modified after that report. An unverifiable order is refused, without a grace
window. This is local timestamp evidence, not a cryptographic process identity.
If `--class` is supplied, it must match an optional player profile and a current
successful player registration from the same content run. The selected class's
resolved integer ID must match the actual party both after startup and before
dungeon entry. Unknown `modalOpen` values stop the runner without dismissing.

If a native story message appears after staging, the encounter wait now returns
`pending_story_message` immediately, even when combat has not been created.
That result has no renderer matches and establishes no binding or readiness;
the runner does not inspect inventory, dismiss the message, or retry staging.
The [Reefstrider setup archive](../../../art-experiments/reefstrider-fish/setup-failure-v1/README.md)
preserves the observed failure and the exact bridge state used by the regression
test. Detecting this boundary does not fix the underlying newly triggered quest
sequence. Resolve that sequence outside the dungeon before another fresh run.

From a freshly launched owned game process, before its first run:

The current `new-run` and interrupted-start continuation require a helper with
`entry-preparation-state`, `entry-position` and `entry-discover`. Older helper
`0c65f127` does not implement this protocol. Reviewed helper `2c42e55d` completed
two fresh startup trials; verify its full receipt and deployment hashes.
Both story chains finished before positioning/discovery in both trials, so this
does not establish a repair for the earlier late-story failure. The
[second trial archive](../../../docs/evidence/entry-preparation-native-v1/second-trial/validation.json)
preserves that chronology and leaves the first validation unchanged.
Do not mix the new Python runner with the older helper. The runner positions once,
services exact native story pages while one discovery check is pending, and
requires its callback, the registered dungeon quest/destination and fresh
quiet readiness before entry. `next-case` retains its existing native Ready
path. [Entry orchestration](entry_setup.py) never retries an uncertain mutation.

```sh
python3 tools/ai-model-pipeline/runtime-test/run_case.py new-run \
  --root /absolute/project/scratch/game-copy --port 8766 \
  --enemy ftkmf_modeltest_ashfang
# Optional explicit class for a new run:
#   --class ftkmf_modeltest_player_blacksmith_female
# Optional two-enemy party-loss setup for a terminal observer campaign:
#   --companion-enemy ogreA --skip-fortify
```

This requires `phase:menu`, starts HollowMire with one hero, waits for a real
nonempty living party, quiets tutorials, fortifies to 999 maximum HP, and
dismisses introductory messages until modal state stays clear for two seconds.
It calls `enter_dungeon` followed immediately by `stage-enemy` with generation,
level 0 and room 1, with no intervening state read or dialog wait. No
`dungeon_encounter` call is made. It waits for exactly the requested enemy set
and native `heroTurnReady` before inspecting the original mesh assignments.
Without `--companion-enemy`, that remains one exact enemy. A new-run-only
`--companion-enemy` stages one additional exact native row for a measured
multi-enemy fixture. `--skip-fortify` leaves the party's ordinary starting
health intact. These options exist for the Kraken party-loss campaign, where
the head's own four proficiencies are harmless and cannot defeat a solo hero.

`phase:menu` only means `!inSession` in the state reader; it does not prove that
the actual MainScreen FSM is ready or that a prior game was cleanly reset. The
native startup driver has stronger internal gates, but its asynchronous failure
is not returned by the initial successful `start_run` response. The runner
therefore requires an operator-confirmed fresh process and refuses any prior
`new-run` journal with the same helper session nonce. An exclusive
`new-run-session-<nonce>.json` marker also prevents a second attempt after a
timeout/rejection, including concurrent runners. Do not delete that marker to
bypass the guard. Manually started runs outside the runner cannot be inferred
from its journal, so the fresh-process prerequisite still applies.

Startup polling is bounded and inspects newly appended BepInEx log text for
`start_run failed` or `_createRealmCasterTable` failures. Detection stops with
an ordered log excerpt; an ordinary state timeout also stops without retry.
The measured failed case followed `return-to-title` and a second run in the
same process, failing native `GameDefinition._createRealmCasterTable` before
the requested custom class was applied. Initial process startup and strict
same-process Ready transitions are the observed successful paths; title reset
is not evidence that another new run will work.

At an existing native Ready preparation slot, supply its current indices:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_case.py next-case \
  --root /absolute/project/scratch/game-copy --port 8766 \
  --enemy ftkmf_modeltest_probe_craba --level 0 --room 2
```

The indices must equal the current dungeon state. `stage-next-enemy` performs
the authoritative native Ready preconditions, followed by `fortify-party` with
`allowReady:true`, then `ready`. Rejection stops the sequence; it never attempts
to force a vote, rebuild an encounter, or automatically recover a defeated run.

Each operation has one attempt. A timeout is uncertain execution and stops the
runner without retry. The sole tolerated rejection is the exact
`dismiss_message` response `no message open` during bounded intro transitions.
Defaults are 40 seconds per operation and 120 seconds per state-wait phase;
override using `--operation-timeout` and `--wait-timeout`.

The journal is written in order under
`<root>/model-test-output/case-<UUID>/journal.jsonl`, with request/response IDs,
states, profile/asset hashes, session identity, timestamps, and fixture settings.
Helper result files retain their unique IDs alongside the case directory.
Successful output is `binding_metadata_observed` only: inventory must contain
the expected GLB mesh names and exact CEL-relative paths under one native enemy
owner, with active enabled renderers. This does not verify full asset bytes in
GPU memory, texture appearance, private materials, animation quality, gameplay,
or artistic acceptance. Continue those checks separately and preserve the journal.

The CLI `next-case` sequence was also exercised live for multipart `plantA`
at native Ready room 0/3, following the bat encounter. It reached the intended
single enemy and `heroTurnReady`, with both body and leaves GLBs at their exact
CEL paths. The ordered evidence is
`scratch/mirewarden-game/model-test-output/case-ebd1347719be49aca145a2c861d545ef/journal.jsonl`.
This confirms the measured Ready transition and binding metadata for that run;
the original thin calibration geometry still required better combat visibility.

A later weighted-joint `snowmanA` diagnostic iteration in session
`8fda43f4ef534b258d4e0a0bdd8bfd7f` retained all five expected bindings through
`case-8d6d3ec68c224896b1000689b251e2c4`. Native Pass, an ordinary hit (23 to 13 HP),
and an explicit `KillSingle` death fixture (13 to 0 HP) each produced 120 frames
at requested 12 fps, zero paused frames, and about 10.908 measured game seconds.
The captures sampled `snowman_attack1`, `snowman_damage`/`snowman_attack2`, and
`snowman_deathDirect`; two guarded loot collections then reached strict Ready
at level 0/room 3. Pass/hit telemetry targets middleBody and death telemetry
Scarf; these are not complete motion samples of all five renderers.

Visual acceptance remains pending. Weighted probes removed the duplicated whole
rigs seen in the earlier all-joint fixture, but reviewed death frames 55 and 119
still show a bright green elongated connector and separated red markers. This
is improved diagnostic evidence, not finished art or evidence of a loader
failure. Preserve both iterations and their distinct asset hashes. Capture IDs,
reviewed frame hashes, action results, and readiness evidence are recorded in
[the runtime validation index](../../../docs/model-runtime-validation.json).

The following `beholderA` diagnostic (`case-e56fc126e85841b1aa040a38b021b168`,
same session) bound both `EyeBody` and `EyeBody/EyeEye` to their expected probes.
Native Pass, ordinary Attack (135 to 122 HP), and explicit `KillSingle` (122 to
0 HP) each recorded 120 unpaused frames and about 10.908 game seconds; one guarded
loot collection reached strict Ready at level 0/room 4. Pass/death telemetry
sampled the body, while hit telemetry sampled the eye. Early attack, damage,
and death animation portions were clipped, so observed clip names do not prove
full-cycle coverage. Reviewed markers were nearly subpixel and attack effects
occluded them. Visual acceptance remains pending until marker sizing and a new
review resolve those limits; see the same validation index for immutable hashes.

A subsequent Snowman iteration used five weighted probes at radius scale 6 with
`connections=none` in session `432473c3a0f24b169472a2272e01e70c`
(`case-6169026607ae4aac8b616c7eb9c529ed`). Reviewed frames show the green connector
absent and separate point markers following the separating parts. Native Pass,
ordinary Attack (23 to15 HP), and explicit `KillSingle` (15 to0 HP) each recorded
120 unpaused frames over about 10.9083 game seconds. Two guarded Collect actions
then reached strict Ready at level0/room2. Source analysis traced the previous
long edge to a generated fully weighted connector exaggerating native part
separation. The validation index preserves both earlier iterations, the new
asset hashes, and reviewed frames. This resolves the observed connector artifact
for these diagnostic markers; finished art, all-animation coverage, and culling
validation remain pending.

Offline orchestration checks, with no game connection:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s tools/ai-model-pipeline/runtime-test -p test_run_case.py -v
python3 tools/ai-model-pipeline/runtime-test/run_case.py --help
```

## Let a low-health enemy attack before the hero kills it

Use the read-only helper operation `{"op":"fixture-state"}` to inspect the
current strict Ready and Loot predicates, their rejection reasons, dungeon
indices, native vote FSMs, active Collect/Ready buttons and hero ownership.
It also reports the isolated root/session/save identity and available real
player avatar scopes. A snapshot is evidence of that instant only; every
mutating operation checks its guards again on the game thread.

After native victory, `{"op":"collect-loot"}` clicks exactly one current
native Collect button. Both encounter vote types must be Loot, the native
loot FSM must be enabled, every party member must be alive, and the button
must be active, enabled, interactable, unfocused, and owned by the selected
hero inside an active Loot container. If several buttons qualify, supply
`heroInstanceId` from `fixture-state`. The Ready operation now checks the
same native button interactability and hero ownership.

Observe `fixture-state` again after each click. A successful response means
one native vote was submitted; it does not mean all loot was collected or
the next room is ready. Stop on rejection or uncertain timeout. These
operations never send raw FSM events, acknowledgments, or room advances.
The new readiness/collection operations have build validation only until
their results are recorded from an isolated live session.

For manual motion evidence, the caller can yield a hero turn through the bridge:

```json
{"action":"end_turn","args":{}}
```

Before sending it, verify an active combat with the exact intended enemy, a
living party, and `combat.heroTurnReady:true` from a fresh state. The caller must
provide these guards: the current endpoint's combat branch checks combat and
the stance-button object, then calls native `DoSkipCombatTurn` (Pass); it does
not enforce the full hero-turn readiness gate itself. Send once, stop on any
rejection or uncertain timeout, and observe the ensuing native enemy turn.
Do not send this action outside verified combat, where it has different behavior.

This allows a low-HP enemy to take an ordinary attack before a lethal hero hit.
Record before/after state and native attack frames, then review the frames and
timing. An accepted Pass or completed enemy turn alone is not a visual motion
pass; attack appearance remains pending until viewed. `run_case.py` deliberately
does not issue combat actions, including this one.

## Native owned-equipment avatar rebuild checks

`equipment-inventory` is read-only: it reports each real party hero's native
slot item enum integers/counts, current overworld and dummy CEL identities,
and measurable resource leases. Default clothing appearance does not prove
an owned Body item exists. If no Body armor is equipped and no equippable
armor is owned in Backpack, stop; this harness does not grant items. Obtain
an item through ordinary loot/shop gameplay, or prepare an explicit custom-class
starting-armor fixture before a fresh process/run.

The [test-content plugin](../runtime-test-content/README.md) supports optional
`startingArmor` without runtime item grants. For the verified common native
`armorCloth1` fixture:

```sh
python3 tools/ai-model-pipeline/runtime-test-content/prepare_player_profiles.py \
  --classification scratch/rig-candidate-classification.json \
  --probes scratch/rig-probes/probes.json --probe-root scratch/rig-probes \
  --output-dir scratch/player-armor-profile-batch \
  --base-class blacksmith --default-skin-type Female --skinset blacksmith_Female \
  --starting-armor armorCloth1
```

Choose a new output directory if that evidence batch already exists. The plugin
validates the native armor and appends one item to a fresh custom-class
`m_StartItems` array; native initialization decides its initial inventory slot.
After deploying the reviewed fixture through the separate setup workflow, start
a fresh owned process/run with its exact custom class key. Existing runs do not
receive newly configured starting items. Inspect `equipment-inventory` before
choosing an equip or unequip operation; do not assume the item begins in Backpack.

In session `8fda43f4ef534b258d4e0a0bdd8bfd7f`, the read-only
`scratch/armor-initial-inventory.json` observed one `armorCloth1` (native ID 59)
already in Body slot 3 after ordinary startup. Native starting-item processing
had auto-equipped it. That initial inventory establishes ownership only; the
subsequent measured native rebuild checks are described below.

`unequip-body` and `equip-body` require strict native Ready preparation and
all living single-player heroes. Supply `heroInstanceId`, `item` (the enum
integer), `expectedBodyCount`, and `expectedBackpackCount`; the latter two
are exact counts of that item from a fresh inventory snapshot. Only native
armor rows marked equippable qualify. Unequip requires exactly one Body
item and Backpack.CanAdd; equip requires empty Body, a positive owned
Backpack count, and Body.CanAdd. These guards protect native methods whose
internal Remove result is unchecked.

Each command calls exactly one native `UnequipItem(item,true)` or
`EquipItem(item,false)` and records immediate before/after inventory and
avatar identities. It does not grant items, directly rebuild avatars, retry,
or automatically restore equipment. Wait for the expected transfer and
avatar identity change, inspect model/lease evidence, then explicitly issue
the inverse with newly observed counts. A submitted native method is not
itself an avatar integration pass.

One fixture was subsequently measured in that same session: native
`UnequipItem(59,true)` moved the item from Body to Backpack, then native
`EquipItem(59,false)` restored it to Body. Fresh inventories confirmed counts
Body/Backpack `1/0 -> 0/1 -> 1/0`. Both overworld and combat CEL identities
changed after each operation. Scoped inventories retained all three custom
body/hair mesh assignments, and the native `armorGambesonF` renderer returned
after re-equip. Shared leases progressed `3 -> 5 -> 6`, each new pair settling
at two references. Old lease disposal was not directly measured; these numbers
are not a no-leak or final-owner cleanup result. Native visibility flags also
left overworld renderers disabled in combat and `hairTop` inactive.

The hashed evidence is recorded under `player_equipment_rebuilds` in
[the runtime validation index](../../../docs/model-runtime-validation.json),
including before/after inventories, both submitted native operations, and
overworld/combat renderer snapshots after each. This validates one item's native
inventory transfer and avatar rebinding fixture, not all equipment, visual
quality, animation correctness, or resource cleanup. To reproduce it, seed the
explicit starting-armor profile in a fresh process/run, reach strict native
Ready, inspect exact item counts, issue one native unequip, wait and inspect both
avatars, then issue the inverse using newly observed counts and inspect again.

## Enemy catalog runtime decode preflight

`catalog-preflight` reads the fixed isolated `model-test-profiles.json`; it
must exactly match the content plugin's loaded profiles. Run only in strict
native Ready preparation, with no concurrent bridge/UI changes. The coroutine
checks Ready and the same dungeon/indices before every profile, processes one
profile per frame, and blocks other helper commands while running. Allow a
longer command timeout for the complete catalog (for example 600 seconds);
never retry an uncertain result.

For every registered enemy it resolves the original native CEL prefab and
exact unique CEL-relative renderer paths, then calls the framework's strict
four-argument GLB decoder with those prefab bones and bindposes. It records
native rig signatures, material-slot availability, asset hashes/counts and
per-renderer decoder results. Unknown fields, stale profile inputs, ambiguous
paths, scene objects and assets outside the isolated content directory fail.
An optional resourcePrefab selects the exact native Resources GameObject
instead of the base enemy CEL. Its basename allows only letters, digits,
underscore and hyphen, with length1..120. It must resolve to an unparented
prefab asset with one root CEL and a root Animator, and the registered enemy must reference that same CEL. Loading the
asset is read-only; it does not spawn a character or run a test encounter.
Absent resourcePrefab preserves the base-enemy CEL identity check. Results
record resolved prefab identity separately from the native base enemy;
controller compatibility remains pending native combat validation.
Only temporary decoded meshes are destroyed; native prefab objects are never
instantiated, enabled, edited or destroyed. No registration is performed.

Results are **runtime_decode_preflight**, not live renderer binding, texture
application, animation, gameplay or artistic acceptance. PNG files are checked
for owned path/existence/size only. Player profiles are excluded.

The isolated session `8fda43f4ef534b258d4e0a0bdd8bfd7f` completed all 366 requested
profiles with 366 decoder passes. Its immutable result is
`scratch/mirewarden-game/model-test-output/0e3b033c47bd42c4be5681543fd07ae5.json`;
the runtime validation index records its hash and catalog hash. These are
runtime decode-preflight results against prefab bones, not 366 spawned,
animated, textured, or visually accepted models.

## Observe native equipment resource disposal

Before a native equipment transfer, issue `lease-watch` with the exact
`heroInstanceId` from fresh equipment inventory. This requires strict Ready
and an owned real overworld avatar with an acquired model lease. It copies
only managed references to that lease's existing Mesh/Material/Texture assets,
plus scalar names/types/instance IDs. It does not retain the avatar, instantiate
anything, increment the production lease, or change cleanup behavior. At most
8 distinct leases and 256 resource references can be tracked; duplicate arms
are rejected. Root, single-player and command-session guards remain mandatory.

Then issue one separately guarded `equip-body` or `unequip-body`. Wait for
native delayed destruction and call read-only `lease-watch-state`. Each watch
reports old lease presence/refcount and the Unity-null status of every pinned
resource. `observed-disposed` requires BOTH old lease absence AND all pinned
assets being Unity-null. The command's outer `ok:true` only means observation
succeeded; it does not turn `not-observed-disposed` into a lifecycle pass.
Current-avatar references settling at two alone are not retirement evidence.

Repeat observations as needed; never invoke cleanup manually. Save the armed,
transfer and observation results together. `lease-watch-clear` explicitly
releases diagnostic managed references only; plugin disposal does the same.
These measurements apply only to the watched old lease assets, not overall
process leak freedom. Already-retired assets cannot be reconstructed from old
mesh names: arm first, then perform a fresh native transfer. Live validation
is required; compilation does not establish Unity destruction timing.

## Record one already-staged enemy action

`record_case.py` replaces ad hoc capture scripts for an enemy that is already
in native combat. Explicitly select the isolated root, its bridge port, exact
registered enemy, CEL-relative renderer assignment, expected SHA256 of the
profile JSON, and one action:

```sh
python3 tools/ai-model-pipeline/runtime-test/record_case.py \
  --root /absolute/project/scratch/game-copy --port 8788 \
  --enemy ftkmf_modeltest_ashfang --renderer-path wolf01 \
  --profile-sha256 EXPECTED_SHA256_OF_MODEL_TEST_PROFILES_JSON --action pass \
  --motion-evidence
```

`pass` uses native combat end-turn. By default, `attack` explicitly uses
`cheat:None` and `focus:false`; `--focus` makes a separately labelled native
max-focus attack. `kill-fixture` explicitly uses `KillSingle`; its death is
fixture cheat evidence, not normal damage. The recorder never stages a room,
starts a run, collects loot, restarts, deploys, or retries an action.

`--motion-evidence` adds no game action. It requests the helper's bounded
passive target-CEL observer and leaves interpretation to
`motion_evidence.py` or `exercise_case.py`. A missing required native event is
an evidence failure, not permission to repeat the action.

After a successfully finalized native `Death` trigger, a renderer can lose its
unique controller association during native teardown. The observer still checks
the exact renderer, owner, CEL, native Animator and FID first. Only the specific
absent/ambiguous controller resolution then ends the capture as a terminal
prefix, before sampling that unresolved frame. The raw result remains `ok:false`
and retains an explicit `motionObservation.termination` boundary. Pre-death
ambiguity, replacement controllers and identity changes remain failures.
`allowDeathControllerTeardownPrefix:true` is a separate, default-off verifier
option; `exercise_case.py` enables it only for `kill-fixture`, validates the
successful exact-target Death and boundary provenance, and labels the outcome
`expected_death_capture_boundary`. `record_case.py` preserves the partial raw
failure and records its validated boundary in the summary. Neither path claims
a complete requested capture or full death-animation coverage. The existing
renderer-destroyed prefix rule remains separate and unchanged.

[Rustpetal V2](../../../art-experiments/rustpetal-snapper/live-validation-v2/README.md)
records the renderer-destroyed variant on exact
`plantD / enJungleNibbler_C / 121537`. Its pass and ordinary-hit captures are
complete before the explicit fixture death. The death prefix retains 94 of 120
frames with stable mesh, renderer path, bone signature, owner and CEL identity;
the last retained sample marks the selected renderer inactive and not visible,
then the next sample reports renderer destruction. Preserve the raw `ok:false`
result and `termination:"renderer_destroyed"`. This supports the reviewed
animated fall and sampled Victory handoff, while full duration, cleanup
causality, active-corpse lifetime and later disposal remain unproved.

After a successful `KillSingle`, FTK can remain at `combat.active:true` with
`heroTurnReady:false` while it presents the native Loot vote. That is the
expected victory handoff when the target is dead, `liveEnemies:0`,
`winningPlayerFid` is present, and `stuck:false`. `record_case.py` records it
as `killFixtureHandoff.status:"victory_pending_native_loot"` and stops; it
does not click Collect. Use the separately guarded native collection path in
`exercise_case.py` or an archived continuation to advance. The actual combat
failure signature remains `stuck:true`, meaning a zero-HP enemy still reports
`alive:true`.

The runner checks successful current registration, fixed profile/asset hashes,
helper nonce, native living single-player hero-turn readiness and exact enemy.
A fresh enemy inventory must match every profile mesh assignment. It starts
120 fixed-step frames at 12 FPS (10 requested game seconds, max width1280),
waits for the first PNG, rechecks inputs and fresh combat identity/readiness,
then sends the action once. Attack and kill-fixture send that fresh enemy FID
as explicit targetFid and require the returned target to match. Timeouts must
be finite positive numbers. Do not operate the UI or another bridge client
concurrently. Bridge HTTP has no root/session identity endpoint: the explicit
port is operator-owned, not cryptographically tied to the helper root. HTTP
observation/action is not atomic; per-frame helper owner checks and final
identity validation detect capture changes but cannot undo a submitted action.

A UUID case directory preserves ordered journal requests/results, before and
after state, action uncertainty, capture ID/path, immutable final result and
`summarize_capture.py` output. Capture files remain in their original helper
output directory. Even on an action timeout it waits for the existing capture
without retrying the action. Missing/paused/changed/partial captures fail the
run and retain available evidence. Success requires 120 present PNGs, stable
renderer/CEL/mesh/bone identities, fixed-step12 and at least 9 sampled game
seconds. It means action/frame recording completed, never visual acceptance.
A late capture after timeout remains inspectable at its journaled result path.

Keep `exercise_case.py` as a one-shot setup and diagnostic probe by default.
Its optional `--attack-attempts N` mode is the bounded exception for native
rows that produce a complete same-target no-loss observation such as a block or
protection turn. Every attempt is retained in the case result and archive, the
sequence stops on a different native outcome, and acceptance still requires
measured nonlethal HP loss. For uncertain actions or a stopped run, start a
fresh process and use bounded `record_case.py` captures for `pass`, `attack`,
and `kill-fixture`, then compose the reviewed frames and native Collect/Ready
continuation into an asset-local archive. This separation keeps stochastic
native combat outcomes from being mistaken for a broken model or a completed
live validation.

The [Thistlewick V2 archive](../../../art-experiments/thistlewick-hexer/live-validation-v2/README.md)
demonstrates the same boundary when a native scourge removes itself on `pass`:
three fresh processes preserve pass/flee removal, an ordinary `58→0` attack
without a cheat, and a separate `deathHeavy_imp` `KillSingle` capture. Only the
third process performs the two guarded native Collect clicks and the final
Ready vote. The pass endpoint is not relabeled as death, and the ordinary HP
result is not used as a death-animation claim.

The canonical [Thistlewick V3 archive](../../../art-experiments/thistlewick-hexer/live-validation-v3/README.md)
adds the current recorder's causal motion record and the retained ordinary
nonlethal hit without replaying the terminal action. The fresh complete pass
contains `cidle_impUnarmed`, `attackProf_leprechaun`, recovery, and full-health
robbery/flee removal at HP 58 after 20 hero damage and 11 stolen gold. The
retained ordinary no-focus process records HP 58 to 48 and
`damageHeavy_imp`. Its archive schema keeps these mixed-motion captures under
their real action and terminal outcome while exposing explicit clip ranges and
causal categories to the gate ledger. For this route, capture an ordinary hit
before a pass that can remove the enemy, then use a separate fresh process for
the explicit death fixture and native loot/Ready progression. Never retry the
same terminal pass automatically when the wrapper later reports that the
target is gone.

The [Bronzewake V3 archive](../../../art-experiments/bronzewake-champion/live-validation-v3/README.md)
shows the multipart version of the same repeatable boundary: one fresh
`bossGladiator` process binds body, hair, armor and boots to one owner, records
complete pass/ordinary attack captures and ordinary HP `58→48`, then commits a
`KillSingle` fixture from `48→0`. Native cleanup destroys the renderer during
frame `91/120`, so the death prefix remains explicitly partial. The post-death
surface is strict Ready at level 0 / room 2 with no Collect vote; one guarded
native Ready click advances the next room, where the normal Jelly Cube and the
registered cultist probe are observed. This is progression and binding evidence,
not full ragdoll, culling, resource-lifetime or finished-art acceptance.

The [Bronzewake V4 archive](../../../art-experiments/bronzewake-champion/live-validation-v4/README.md)
is the canonical exact-source example for a controller-unresolved death prefix.
It credits only `bossGladiator / armorBossGladiator / 121522`, even though the
same native owner also binds the package's body, hair, and boots. Pass and
ordinary hit captures complete 120 frames; the `KillSingle` capture retains 90
frames, a finalized exact-target native `Death`, and `2HandWield_Death` before
the typed controller-resolution boundary. The raw capture stays `ok:false`, the
case result is `expected_death_capture_boundary`, and the archive states that
full-duration death and later controller state remain unproven. Use this pattern
only when the verifier accepts the exact default-off teardown-prefix contract;
never transfer its evidence to a sibling multipart renderer.

The [Bronzewake V5 archive](../../../art-experiments/bronzewake-champion/live-validation-v5/README.md)
applies that bounded pattern to the exact `bossGladiator / enBossGladiator /
121272` body source. Pass and ordinary hit captures complete 120 frames, with
ordinary HP `11→6`. The separate `KillSingle` capture retains 90 frames through
native death and an actual ragdoll prefix because this controller reports
`m_DoRagdoll=true`: 11 active surviving rigid bodies are nonkinematic from frame
28. The controller then becomes unresolved, so later corpse lifetime and
cleanup causality remain open. This archive credits only the body topology;
verified hair, armor and boots companion bindings remain sibling evidence.

The [Bronzewake V6 archive](../../../art-experiments/bronzewake-champion/live-validation-v6/README.md)
applies the same bounded pattern to the exact `bossGladiator /
bootsBossGladiator / 121661` source. Pass and ordinary attack captures each
complete 120 frames and preserve the exact boots through idle, critical attack,
heavy hit, recovery, and a native basic attack. Ordinary no-focus damage is
`11→1` with `DamagedHeavy`. The separate `KillSingle` fixture retains 90 frames
through native death and an actual ragdoll prefix: `m_DoRagdoll=true`, the body
set changes from 15 to 11 at frame 26, and all 11 survivors are nonkinematic
from frame 28. The exact renderer remains active, enabled and visible in all 330
retained frames, and 19 exact originals were reviewed. The controller then
becomes unresolved, so full-duration death, later corpse lifetime and cleanup
causality remain open. This archive credits only the boots topology; verified
body, hair and armor companion bindings remain sibling evidence. Strict Ready
is observed at level 0 room 2 without Collect.

The [Bronzewake V7 archive](../../../art-experiments/bronzewake-champion/live-validation-v7/README.md)
repeats the bounded pattern for the exact `bossGladiator /
hairBottomBossGladiator / 121500` source. Pass and ordinary attack captures
each complete 120 frames and preserve the exact hair through idle, basic
attack, heavy hit, recovery, and critical attack. Ordinary no-focus damage is
`11→1` with `DamagedHeavy`. The separate `KillSingle` fixture retains 90 frames
through native death and an actual ragdoll prefix: `m_DoRagdoll=true`, the body
set changes from 15 to 11 at frame 26, and all 11 survivors are nonkinematic
from frame 28. The exact hair renderer remains active, enabled and visible in
all 330 retained frames, and 20 exact originals were reviewed. The controller
then becomes unresolved, so full-duration death, later corpse lifetime and
cleanup causality remain open. This archive credits only the hair topology;
verified body, armor and boots bindings remain sibling evidence. Strict Ready
is observed at level 0 room 2 without Collect. Together, V4 through V7 provide
separate canonical archives for all four Bronzewake renderer topologies.

The [Tideglass V3 archive](../../../art-experiments/tideglass-crab/live-validation-v3/README.md)
applies the complete-capture pattern to the exact `crabB / enCrabWizard /
121411` source. Three 120-frame captures preserve the authored mesh through
`Crab_Idle`, native `Crab_Attack1`, ordinary `Crab_Hit` and recovery, native
`Crab_Attack2`, and explicit-fixture `Crab_DeathDirect`; the exact renderer is
active, enabled and visible in all 360 retained frames. Ordinary HP is `58→56`.
The separate `KillSingle` fixture reports `m_DoRagdoll=false`, so it establishes
animated death rather than ragdoll or ordinary lethal damage. One guarded
Collect reaches strict Ready at level 0 / room 2, and the archive pins 21 exact
originals plus the retained native purple hat. Keep indirect death, later corpse
lifetime, portrait, culling, resource lifetime and final art approval open.

The [Bronzehollow V2 archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v2/README.md)
records the same sequence for the exact `deathKnight` body: one fresh process
binds the authored body, completes pass/ordinary attack/`KillSingle` captures,
and repeats the native `BLOCKED` attack boundary with HP `58→58`. One guarded
native Collect reaches strict Ready at level 0 / room 2, and one guarded Ready
vote advances the normal Jelly Cube plus registered cultist-probe room. The
explicit kill is a fixture and the live darker palette remains unresolved, so
this is repeatable binding/progression evidence rather than ordinary combat or
finished-art acceptance.

The corrected [Bronzehollow V4 archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v4/README.md)
is the reference for the optional native-cap method. One current-catalog process
retains seven exact zero-damage `Block` attempts before attempt 8 records native
`Damaged`, damage 1 and HP `45→44`. The fixture receipt pins the equipped
`bluntSmithHammer`, Toughness `0.81→0.95`, zero spent focus, and FTK's own 0.95
stat cap. Ten complete captures also preserve native attack, defense, hit,
explicit-fixture death and the Death Knight ragdoll in all 1,200 frames. V4
separates the original body from the retained native helmet, shield and mace;
V3 is historical because its review text attributed some native equipment to
the authored mesh.

The [Cinderwing V2 archive](../../../art-experiments/cinderwing-bat/live-validation-v2/README.md)
packages the completed native-scale batA regression: one process keeps the
`enBat01` renderer at native root scale `0.78`, records complete pass, ordinary
HP `58→50` hit and `KillSingle` HP `50→0` captures, and accepts one guarded
Collect into strict Ready at level 0 / room 2. The previous old-framework
92-frame renderer-destroyed death remains historical evidence; the fixture
capture does not become ordinary lethal or finished-art acceptance.

The [Cinderwing V3 archive](../../../art-experiments/cinderwing-bat/live-validation-v3/README.md)
applies the complete-capture pattern to the exact `batA / enBat01 / 121104`
source. Three 120-frame captures preserve `batFlySlow`, native
`batAttackProf`, recovery, `attackCrit_bat`, ordinary `batTakeHit`, a second
recovery and retaliation, and explicit-fixture `batDeath`. The exact renderer
remains active, enabled and visible in all 360 frames. Ordinary HP is `58→48`;
the separate `KillSingle` death has `m_DoRagdoll=false` and zero rigid bodies,
so it is animated death rather than ragdoll or ordinary lethal evidence. One
guarded Collect reaches strict Ready at level 0 / room 2. The archive pins 22
reviewed originals and keeps sibling bat sources, other clips, later corpse
lifetime, portraits, culling, resource lifetime and final art approval open.

The [Duskquill V3 archive](../../../art-experiments/duskquill-raven/live-validation-v3/README.md)
applies the same complete-capture sequence to exact `crowC / enCrow / 120964`.
Three 120-frame captures keep the renderer active, enabled, and visible through
`BirdFlySlow`, native `birdAttack2`, ordinary HP `58→50` plus `BirdDamage`,
recovery, a later native attack, and explicit-fixture `BirdDeath`. The fixture
has `m_DoRagdoll=false` and zero rigid bodies, so it proves an animated death
within the sampled window. Two guarded Collect actions reach strict Ready at
level 0 / room 2. The archive also pins a first fresh run in which native crow
self-removal stopped the pass after 107 frames. That run is rejected boundary
evidence rather than a binding failure or a partial success; only the second
fresh process supplies canonical coverage.

The [Mirewarden V2 archive](../../../art-experiments/mirewarden-ftk/live-validation-v2/README.md)
packages the native-scale trollCaveA regression at root scale `0.95`: one
process records complete pass, ordinary HP `50→42` hit and `KillSingle`
HP `42→0` captures, preserves 14-body ragdoll telemetry, and accepts two
guarded Collects into strict Ready at level 0 / room 2. Rock-segment gaps are
retained as an art limitation; the fixture is not ordinary lethal acceptance.

The canonical [Mirewarden V3 archive](../../../art-experiments/mirewarden-ftk/live-validation-v3/README.md)
applies the generic exact-source pattern to `trollCaveA / enTroll01 / 121153`.
Three complete 120-frame captures keep the exact renderer active, enabled,
visible, and identity-stable through `cidle_troll`, `attackProf_troll`, ordinary
HP `58→48` with `damage_troll` and recovery, `attack_troll`, and the separate
explicit-fixture `deathHeavy_troll`. The fixture reports `m_DoRagdoll=true`,
the native body set reaches 11 survivors at frame 26, and all 11 are
nonkinematic from frame 28 as the reviewed model falls to the floor. One
guarded Collect reaches strict Ready at level 0 / room 2. Twenty-eight exact
originals were reviewed. The fixture does not establish ordinary lethal
damage or corpse lifetime after frame 119; the sibling troll sources and the
separate Gloamcap resource-prefab route receive no credit.

The canonical [Gloamcap V3 archive](../../../art-experiments/gloamcap-imp/live-validation-v3/README.md)
applies the generic exact-source pattern to the separate
`impA / enbaseyimp / enBaseyImp / 121117` resource-prefab route. Five complete
120-frame captures preserve the exact renderer through `cidle_impUnarmed`,
repeated `attack_impUnarmed`, two bounded zero-loss `dodge_imp` trials, ordinary
HP `58→48` with `damageHeavy_imp` and recovery, and the separate explicit-fixture
`deathHeavy_imp`. All 11 recorded rigidbodies become nonkinematic at frame 26
with `m_DoRagdoll=true`, and the reviewed body reaches the floor by frame 45.
One guarded Collect reaches strict Ready at level 0 / room 2. Twenty-nine exact
originals were reviewed. This is not ordinary lethal evidence, no corpse lifetime
after frame 119 is claimed, and direct enemy rows, sibling Imp sources and the
Mirewarden troll route receive no credit.

The [Emberglass Bee V3 archive](../../../art-experiments/emberglass-bee/live-validation-v3/README.md)
applies the same sequence to the small single-renderer `beeA` model: one fresh
process records complete pass/ordinary attack captures and ordinary HP `58→53`,
then commits `KillSingle` from `53→0`. Native cleanup destroys the renderer at
frame `94/120`, so the prefix remains explicitly partial. The post-death surface
is strict Ready at level 0 / room 2 with no Collect vote; one guarded Ready click
advances the next room, where Jelly Cube and the registered cultist probe are
observed. This is exact binding and progression evidence, not full ragdoll,
fine-scale insect detail, culling, resource-lifetime or finished-art acceptance.

The [Emberglass Bee V4 archive](../../../art-experiments/emberglass-bee/live-validation-v4/README.md)
shows the current generic-plan form for a bounded ordinary retry. Its four
archive action keys keep the complete pass, first zero-loss dodge attempt,
second HP 58 to 50 hit, and explicit fixture death distinct. The native Bee
switches 16 bodies dynamic with `m_DoRagdoll=true`, reaches a visible grounded
corpse, and retains it through frame 102 before the renderer becomes inactive
at frame 103. This supports only that sampled physics and corpse interval. It
does not make the fixture ordinary lethal evidence, prove cleanup causality, or
extend corpse lifetime beyond the recorded boundary. The 24 root-reviewed
frames use `attack-attempt-1` and `attack-attempt-2` so selected-frame names stay
unique and traceable to the corresponding raw capture.

The [Verdigrin V3 archive](../../../art-experiments/verdigrin-mimic/live-validation-v3/README.md)
closes the mimicA continuation that V2 left open: one fresh process binds the
exact `mimic01` mesh, records ordinary HP 58→52 and a complete 120-frame
`KillSingle` fixture, then accepts two guarded Collect responses and observes
strict Ready at level 0 / room 2 before one guarded Ready vote advances the next
Enemy encounter. The first Collect leaves the same native button with no reward
delta; the second is still a current owned native Collect and advances the loot
surface. This is progression evidence, not ordinary lethal-damage or finished-art
acceptance.

The canonical [Verdigrin V4 archive](../../../art-experiments/verdigrin-mimic/live-validation-v4/README.md)
shows how to preserve a complete immutable runner after current campaign
bookkeeping changes. Its exact profile, catalog, assets, `mimicA / mimic01 /
121192` source assignment and motion renderer still match; only the queue and
stage-readiness hashes differ. The pinned reconciliation allows the retained
three complete captures to supply `cidle_mimic`, native `attack_mimic`,
ordinary HP `58→52` with `damageSmall_mimic`, later `chompAOE_mimic`, and the
separate explicit-fixture `deathHeavy_mimic`. V3 remains the named source for
two guarded Collects, strict Ready and next-room progression. This does not
turn fixture death into ordinary lethal evidence or expand the reviewed corpse
lifetime.

The canonical [Sunspire Roc V4 archive](../../../art-experiments/sunspire-roc/live-validation-v4/README.md)
shows the related repair for complete sessions that predate the current archive
schema. It compares the exact selected profile object from the historical and
current catalogs, verifies unchanged assets and route identity, preserves the
V2 combat and V3 portrait archives, and conducts a new root review of original
pixels. The new archive pins all 360 combat images plus the portrait and records
source-specific binding, appearance, idle, attack, hit, death, fixture action,
ordinary damage, and Ready fields. Catalog-wide hash drift caused by unrelated
profile additions does not require replay. A changed selected profile, asset,
source renderer, or motion renderer does.

The [Saffronspine pufferA V2 archive](../../../art-experiments/saffronspine-puffer/live-validation-v2/README.md)
shows the same repeatable boundary on a puffer chassis: one fresh process
preserves pass/attack behavior, ordinary HP 58→48 without a cheat, and a
complete `BlowFish_DeathDirect` `KillSingle` capture. Two guarded native
Collect clicks and strict Ready at level 0 / room 2 are pinned separately;
pufferA must be validated as its own bind/controller claim.

The [Saffronspine pufferA V3 archive](../../../art-experiments/saffronspine-puffer/live-validation-v3/README.md)
is the canonical schema example for the exact `pufferA / enBlowFishA / 121509`
route. One fresh process completes pass, ordinary attack, and `KillSingle`
captures at 120 frames. Semantic evidence records settled idle, native
`Attack`, ordinary HP `58→48` with native `Damaged`, and native `Death`; one
guarded Collect reaches strict Ready at level 0 / room 2. The manual review
pins 18 exact originals, including the largest sampled inflation and the native
direct-death transition that hides the renderer between reviewed frames 23 and
27. Treat that transition as a hide-and-effect handoff, not a visible corpse,
ordinary lethal, indirect-death, sibling-puffer, or final-art conclusion.

The [Saffronspine pufferB V2 archive](../../../art-experiments/saffronspine-puffer-b/live-validation-v2/README.md)
repeats that boundary against the independent pufferB bind matrices: one fresh
process preserves pass/attack behavior, ordinary HP 58→50 without a cheat, and
a complete `BlowFish_DeathDirect` `KillSingle` capture. Two guarded native
Collect votes and strict Ready are recorded separately; pufferA evidence is not
transferred to pufferB.

The [Copperveil V3 archive](../../../art-experiments/copperveil-spider/live-validation-v3/README.md)
is the canonical many-limbed example for `spiderB / enSpiderB / 121386`. One
fresh process completes three 120-frame captures with settled idle, native
`AttackProf`, ordinary HP `63→59` plus `Damaged`, explicit-fixture `Death`, one
guarded Collect, and strict Ready. Its 18 reviewed originals inspect the body
and each visible leg chain through attack, recovery, airborne death, collapse,
and the terminal loot overlay. The custom corpse remains sampled behind that
overlay, which supports the recorded handoff without establishing ordinary
lethal damage or later corpse lifetime. Keep Spider A routes independent.

For an ordinary lethal-damage trial, use separate `--action attack` recordings
against the same current enemy until native combat resolves. Inspect each
terminal result and its before/after HP, then require a fresh living target and
hero-ready state before submitting the next action. Preserve dodges and other
zero-damage outcomes; they are not missing trials to overwrite. On uncertainty,
poll the original capture/result identity and stop dependent actions. Never use
`kill-fixture` or a health setter to finish this sequence. Record the profile's
base-health fixture and party fortification separately from ordinary attacks;
this remains an isolated test, not unmodified campaign balance evidence.

An HP-zero endpoint alone does not establish lethal damage: native flee can
produce it too. Join the submitted attack, damage/death poses or native ragdoll
transition, and final combat state before naming the outcome. Preserve native
renderer destruction as a partial recording if it interrupts capture. Collect
loot only after capture is terminal, using the exact current native Collect
predicate and observed reward/button progress between submissions. The
[Reefstrider ordinary-lethal follow-up](../../../art-experiments/reefstrider-fish/live-validation-ordinary-lethal-v1.json)
preserves eight attacks including a dodge, the final ordinary 7→0 HP outcome,
native ragdoll transition and two Collect submissions reaching Ready0/2. Its
earlier progress records and explicit-kill validation remain unchanged.
An enemy that naturally removes itself before hero readiness needs arrival
observation instead of this hero-action procedure.

Offline mock-transport checks (no game connection):

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover \
  -s tools/ai-model-pipeline/runtime-test -p test_record_case.py -v
```

A live watched armor59 fixture in session `432473c3a0f24b169472a2272e01e70c`
now verifies the narrow disposal condition above. Native unequip/re-equip moved
Body1 to Backpack1 to Body1; old leases3 and5 each became absent from the
production dictionary and all nine watched resources on each became Unity-null.
New lease6 settled at two references, with three custom GLBs and native armor
restored on both avatars. The watch performed no Destroy, lease retention, or
manual prune. Evidence and hashes are indexed under `player_equipment_rebuilds`
in [the runtime validation index](../../../docs/model-runtime-validation.json),
with sources `scratch/watch-armor-*.json`. The earlier unwatched session remains
historical evidence with disposal unmeasured. This is not a global no-leak,
all-player, final-owner teardown, preview, death, or art acceptance result.

`record_case.py` has now completed live Pass, ordinary Attack (135 to127 HP), and
explicit kill-fixture (127 to0 HP) captures for the larger Beholder diagnostic
markers in session `432473c3a0f24b169472a2272e01e70c`. Each journal returned
`recorded_action_and_frames`, with120 unpaused frames and about 10.9083 measured
game seconds. The weighted markers used explicit radius0.2; body and eye bound
at their expected CEL paths. Reviewed idle/hit views show the two larger markers,
but attack glow and death effects occlude them. One guarded Collect then reached
strict Ready at level0/room3. This validates these CLI action/capture runs, not
finished art or complete animation visibility. The runtime index preserves the
thin-probe history, new asset hashes, all three case journals, seven reviewed
frame hashes, and current CLI source hashes (not execution-time attestations).

A subsequent two-renderer `skullA` diagnostic in the same session used stage case
`3d509ec8d42344d18ad8f452bbb66b9a`. Pass sampled `ChaosSkullBottom`; ordinary Attack
(69 to62 HP) and explicit KillSingle (62 to0 HP) sampled `ChaosSkullTop`.
All three captures recorded 120 unpaused frames and about 10.908 game seconds.
Reviewed regions showed visible purple calibration geometry with native effects,
without obvious catastrophic deformation; death faded inside purple effects.
Damage/death starts were clipped (first sampled normalized0.4442/0.1375), so this
is not full-cycle or finished-art acceptance. Two guarded Collect actions reached
strict Ready at level0/room4. The runtime index records all case/capture/frame
hashes and those visibility limits.

The separate nine-resource candidate batch at `scratch/resource-profile-visible-v1`
uses6x probe radii and retains the earlier1x batch. Its preparation and asset hashes
are recorded separately from live evidence. Deployment, decoding and production
spawn checks were pending when that preparation record was written.

`plantC` was also recorded as a second native plant variant in stage case
`0c008ef0af42429e94a9bac2af3e069a`. It shares PlantA's recorded rig/controller
fingerprint and body/leaves rig IDs, so it adds a testcase rather than a unique
combat-profile group. Pass, ordinary Attack (25 to14 HP), and KillSingle fixture
(14 to0 HP) each recorded 120 unpaused frames over about 10.9083 game seconds.
Reviewed calibration bends/collapses showed no obvious explosive deformation in
visible areas, with small features, effects and hero occlusion limiting review.
Hit/death beginnings were clipped (normalized0.4125/0.125). One guarded Collect
reached strict Ready at level0/room5. This remains diagnostic evidence, not
finished art or complete animation coverage; hashes are in the runtime index.
The separate375-entry combined catalog at `scratch/runtime-profile-375-v1` was
staged but not deployed when recorded, and does not establish375 runtime passes.

The first resource-prefab fixture, `enbaseysnake` on the `snakeJungleA` base,
has now run live in session `6b86eff0890f479f90b9134814cc0ae8`, stage case
`5b6e1a005bd442c7b66fa96ca686d8e5`. Exact path `enSnake_Basey` used probe121488.
Pass, ordinary Attack (27 to19 HP), and explicit KillSingle (19 to0 HP) each
recorded 120 unpaused frames over about 10.908 seconds. Reviewed views show the
probe upright/coiled, biting and collapsing into a coil, with some head clipping
and label/hero occlusion. Animator disabled at frame28 of death and13 native
rigidbodies moved and settled. Frozen `Snake_DeathBig` normalized1.65 after that
handoff is not a failed death; see per-body physics measurements in the index.
Two guarded Collect actions reached strict Ready at level0/room2. This is one
diagnostic resource case, not finished art or all nine candidates accepted.
The375-entry catalog is deployed, but its full decode run was still pending at
this update; no375-pass claim follows from the single live case.

The expanded decoder subsequently completed 375/375 entries with zero errors in
command `2773ae950a954b48996582a9ecdf2221`, session
`6b86eff0890f479f90b9134814cc0ae8`, against catalog hash `729365f5...`.
It decoded390 renderer assignments spanning171 native bone signatures, including
all nine resource-prefab cases against their resolved native CELs. This is
`runtime_decode_preflight` only: temporary meshes were destroyed and native
prefabs were not instantiated or edited. It does not validate texture decoding,
production binding, animation or art. Only the resource snake has separate live
evidence at this update. The index preserves the earlier366-entry preflight and
its original catalog hash alongside this expanded result.

Optional enemy `minimumBaseHealth` is an integer1..1000 fixture applied by the
separate content plugin to custom clones before spawning. Catalog preflight
validates the field and reports its requested floor, native template base HP,
registered base HP and whether the clone matches the requested maximum.
This health report is separate from decode success and controller compatibility;
native scaling can make spawned HP differ. No runtime health setter is used.
The action recorder preserves the full profile and explicitly journals the
requested minimum alongside actual enemy HP before capture/action; its result
also includes minimumBaseHealth (null when absent). Preserve this journal with
capture files so boosted enemies cannot be mistaken for vanilla balance tests.

The next resource case `enbaseywolf` used native `wolfA` health 8, catalog
`729365f5...`, stage `970eb94463794925a09f7d3ef7953794`, in the same session.
`Wolfie` bound probe120975. Pass sampled `attackProf_wolf`; ordinary Attack dealt
10 damage and killed it (8 to0 HP), sampling `deathHeavy_wolf`. Both captures
recorded 120 unpaused frames over about 10.908 seconds. Animator stayed enabled
throughout death, unlike the separately observed Ashfang ragdoll. Reviewed
calibration was visible and folded in death, but corpse markers appeared above
the floor, so ground contact/artwork fit is not accepted. Nonlethal hit evidence
remains pending. Two guarded Collect actions reached strict Ready at level0/room3.
The separately prepared health 64 catalog was not deployed for this baseline and
does not retroactively change its health or evidence.

The health 64 catalog (`903d46b8...`) subsequently ran in session
`033a44209e08452aba8858dcd3eda29c`. All375 registered custom enemy base values
matched `max(native,64)`;150 native values above64 remained unchanged. Startup
also registered96 player fixtures, which is registration evidence only. For
resource `enbaseywolf`, native base9 became custom base64 and native combat HP 58.
The recorded critical ordinary hit dealt13 (58 to45), sampling `damaged_wolf`
and `attack_wolf`; explicit KillSingle then reduced45 to0. Both captures had120
unpaused frames over about 10.9083 seconds. Two guarded Collect actions reached
strict Ready at level0/room2. Heavy hero occlusion and apparent corpse ground
fit still limit visual acceptance. Source reconstruction of the prior baseline
rules out missing probe foot extent, but terrain height was not recorded, so
ground contact remains unaccepted. Both health iterations and that analysis are
hashed separately in the index.

The health catalog decoder `3b11077c433a4b38892abfb7ccac0b71` completed 375/375
with zero errors. This confirms decode/base-fixture checks for the new catalog,
not additional live rig or art coverage. The earlier `729365f5...` decode result
remains preserved with its original input hash.

Between-floor safety: `stage-next-enemy` now accepts only an existing queued
Enemy inside the definition's room bounds. It rejects active Stair/ExitRoom/
Cleared and every queued non-enemy slot. Native generation appends Stair beyond
`GetRoomCount(level)`, so a valid generated list index is not enough to permit
replacement. Preserve that slot and follow native Ready/descent; never edit
indices, acknowledge forcibly, or substitute a staircase. Once native descent
resets the active type and reaches the next floor's genuine Enemy preparation,
normal strict Ready staging can resume.

`fixture-state.dungeon` distinguishes activeDungeonEncounterType from
queuedRoomType, definitionRoomCount from generatedRoomCount, and reports queued
objects/context, definitionLevelCount, nextLevelGenerated, isAtLastLevel,
isDungeonCleared and slotAllowsEnemySubstitution. Slot eligibility alone is not
Ready authorization: the staging operation checks both again before mutation.

## Native stair traversal and next-floor staging

A native floor transition was measured after the snow goblin encounter in session
`033a44209e08452aba8858dcd3eda29c`. Starting at strict Ready level0/room5, one
guarded `ready` click succeeded. A fresh fixture still showed strict Ready0/5,
while bridge state identified the room as `Stair`. Only after that fresh check,
a second guarded `ready` succeeded and traversed naturally to level1/room0.
Actual combat was already active on arrival, with a Bone Archer and generated
cubeA probe. No room indices were written and no Stair was replaced.

Do not batch those clicks or treat the first unchanged indices as a reason to
retry blindly: inspect the new fixture and room type between actions. The first
encounter on the next floor starts automatically; do not assume an opportunity
to stage level1/room0 before combat. Use the native traversal and wait for an
eligible later native Ready room before another staged case. This is a finite
two-level FloodedCrypt definition, not unlimited floor generation.

The new `StageNextEnemy` definition-boundary guard and fixture metadata were
source-built and architect-approved but not yet deployed when this traversal was
recorded. Their live validation is separate. Six action/state evidence hashes
are preserved under `native_floor_transitions` in the
[runtime index](../../../docs/model-runtime-validation.json).

The deployed queued-slot guard now has both a positive enemy staging result
(Emberjaw case `7992f3d02fd849e7b17441040bec9111`) and a negative Trap1 result in
session `6dd2bce53bfa4af2907d96d4ef59ba1a`. Attempt
`580ca9c88d1743dfa24f949549d2fa79` requested the fat resource case at strict
Ready0/3. It was rejected before substitution because the queued room was
`Trap1`, with objects `[trap]` and `slotAllowsEnemySubstitution:false`.
Immutable before/after snapshots show identical dungeon slot metadata. The
caller stopped after that one rejection; no retry or forced replacement.
Strict Ready is therefore necessary but insufficient: the queued slot must also
be an eligible native Enemy within definition bounds. Stair-specific rejection
still lacks a direct live test. See `native_queued_slot_guard_checks` in the
runtime index; do not reinterpret earlier source-only guard notes as current
lack of deployment or infer this test covers every non-enemy room type.

## Single-clip Kraken sampling fixture (no adapter application)

`kraken-sample-fixture` takes one explicit clip (`krakenAttack`, `krakenIdle`,
`krakenDamage`, `krakenDisappear`, or `kraken_appear`) and `times`, an array of
1..32 finite seconds within that native clip's length (maximum 60 seconds).
For example, start with `{"clip":"krakenAttack","times":[0]}` to record
identity/length before selecting additional times. Save that JSON to an ignored
scratch file and invoke against the already-running owned copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/command.py \
  --root /absolute/project/scratch/game-copy kraken-sample-fixture \
  --payload /absolute/project/scratch/kraken-sample.json
```

Then use the returned helper result JSON as the numerical `--samples` input;
keep its cleanup/error and native source identity metadata. Strict Ready, isolated-root
and single-player command/session guards apply; do not change the game phase
concurrently.

The helper resolves modern native `krakenHead`'s prefab and weapon controller,
and old Resources `enkrakenhead`. It creates two **transform-only** temporary
hierarchies using new inactive GameObjects (maximum 256 transforms each). It
copies exact local TRS, never instantiates native objects or copies CEL, Animator,
renderer, collider, FX or gameplay components. After construction and component
checks, the two transform-only roots are activated for sampling. Before EVERY sample it restores
all copied local positions, rotations and scales, then invokes the verified
Unity2017 `AnimationClip.SampleAnimation(GameObject,float)` on the temporary
roots only. Output includes independent modernRootAnimated/driverLocals for the
numerical audit and separate oldRootAnimated/oldTargetLocals for appearance.

Both temporary roots are destroyed in finally; completion waits two frames and
reports their Unity-null status. Partial frames and errors remain in the result.
The busy guard remains held through deferred cleanup and result publication.
The native SampleAnimation implementation is opaque in managed decompilation:
this fixture does not assert that events never dispatch. Its targets contain
only Transforms and therefore no gameplay event handlers; component counts are
checked before and after samples. No gameplay state setters are called.

Feed a four-main-clip result directly to `audit_kraken_adapter.py --samples`;
`kraken_appear` is separate baseline data and intentionally rejected by the
main-state adapter audit. Native Animator reference comparison, unkeyed-channel
semantics, controller crossfades, appearance blending, visual acceptance and
live adapter integration remain pending. Build/source checks do not establish
actual SampleAnimation behavior; isolated runtime measurement is required.

First isolated sampling trial (`scratch/kraken-native-samples-v1/motion-audit.json`)
returned success and verified temporary-root cleanup for all five clips. However,
nine timestamps per clip produced identical matrices, including appearance.
This is **failed motion validation**, despite successful calls and numerical
audits on those constant inputs. Inactive-tree/API behavior needs investigation;
no native motion, controller equivalence or live old-Kraken support is credited.

The revised Kraken sampler activates only its audited transform-only copies and
records root activity on every sampled pose. This is an unvalidated response to
the first inactive-copy trial returning constant matrices, not a proven fix.
Nonlegacy muscle-clip evaluation requirements remain a possible cause if active
sampling is still constant. Native clips/controller/prefabs remain untouched.
`poseVariation` reports maximum matrix-element differences from the first sample
for both roots and each modern/old local bone path, with threshold1e-5. It labels
insufficient, constant or varying samples and separately identifies local
articulation. Outer ok still describes API execution and cleanup only. The
numerical audit independently reports constant supplied inputs as
`supplied_constant_samples_no_motion_evidence`; algebra passing on those inputs
cannot be presented as animation evidence. Original inactive-trial files remain
preserved for comparison.

## Record a real custom player's combat avatar

`record_player_case.py` reuses the enemy recorder's transport, immutable case
journal, first-PNG gate, one-action rule, fixed-step120-frame capture, partial
result preservation and numerical summary. It supports only native `pass` and
ordinary `attack`; it never creates a run, stages a room, collects loot, grants
items or deploys. A pass allows an enemy turn but does not guarantee the hero
is hit. Review recorded HP/action/frames before claiming any actual damage or
animation acceptance.

```sh
python3 tools/ai-model-pipeline/runtime-test/record_player_case.py \
  --root /absolute/project/scratch/game-copy --port 8788 \
  --enemy ftkmf_modeltest_ashfang --profile-sha256 EXPECTED_ENEMY_PROFILE_SHA256 \
  --class-key ftkmf_modeltest_player_blacksmith_female --skinset blacksmith_Female \
  --player-profile-sha256 EXPECTED_PLAYER_PROFILE_SHA256 \
  --hero-instance-id ACTUAL_EQUIPMENT_INVENTORY_HERO_ID \
  --renderer-path playerBlacksmith --action attack
```

This first implementation requires exactly one living party hero and one
native equipment owner. The bridge party must have the exact registered custom
class ID and be the acting hero. A fresh equipment-inventory must match the
explicit heroInstanceId; its dummyCelInstanceId joins to fresh player-combat
inventory. Owner and renderer IDs are read from that native join, never guessed
from names. Wrong scopes, mismatched/ambiguous CELs and missing profile meshes
reject. All player assignments must match expected GLB identities; the selected
renderer must be active/enabled, while other correctly bound apparel pieces
may be hidden. Main capture result and every frame must retain player-combat
scope and the exact owner/CEL/renderer/mesh/bone identity.

Both current enemy and player profile JSON hashes are explicit and checked;
all their asset hashes are pinned. Player registration metadata must match
class/skinset/default skin type/starting armor, and its run identity must match
the current enemy registration. The journal preserves the full player profile.
This verifies skinset registration and mesh assignment metadata, not a separate
query of the native skinset field or artistic appearance. Multiple-party-hero
mapping is deliberately rejected until helper inventory supplies a direct
hero-instance-ID-to-FID mapping. Single-hero use is generic across supported
player profiles; no Blacksmith IDs are hard-coded.

Run both recorder suites offline with `-p 'test_record*case.py'`; wrong class,
owner, scope, mesh and ambiguous ownership cases are covered alongside one-call
native action uncertainty. No live player recording is implied by these tests.

Catalog preflight accepts optional `visualScale` using the exact pure validator
source shared with the test-content plugin: finite JSON number0.1..4, rejecting
null, strings, booleans and nonfinite values. The helper compiles that source
into its own assembly and does not load or invoke the content plugin to validate
it. Omission preserves existing behavior. Results report the requested scale,
resolved native prefab root local scale, and explicit pending native-spawn scale
measurement. The helper never sets scale; native prefab metadata is not a claim
about final spawned size or an animation/decode acceptance criterion. Recorder
journals retain the complete profile, including this fixture value.

Focused validation reuses `scratch/visual-scale-validation/ScaleChecks.csproj`
when available locally; its18 boundary/type/nonfinite cases execute the same
linked source used by both plugins. This test project is ignored local tooling.

Kraken sampling v2 repeated all five native clips on active transform-only
hierarchies in session `4fac21c62c7c4624a648ec782d88c34d`, helper `a0d46...`.
Each clip used five distinct times spanning its range, but every measured matrix
delta remained zero, including `kraken_appear`. Both fixture roots were active;
all per-call temporary cleanup reported Unity-null and the final fixture remained
Ready0/2. Active hierarchy therefore did not resolve the sampling problem, whose
cause remains unknown. Successful calls and cleanup are not motion validation.
V1 evidence remains preserved; v2 has no numerical adapter audit or visual pass.
Exact clip IDs, times, deltas and hashes are in `kraken_sampling_trials` in the
runtime index.

### Controller-free manual clip playable comparison

The same guarded `kraken-sample-fixture` operation now accepts
`{"method":"clip-playable","clip":"krakenAttack","times":[0]}`.
Omitting method preserves the SampleAnimation diagnostic; results label the two
methods separately. Use the existing payload-file command recipe. This remains
an isolated single-clip comparison, not an adapter or controller test.

Clip-playable mode adds exactly one owned Animator to each copied hierarchy,
assigns the corresponding native source Avatar and leaves runtime controller
null. It disables animation events, root motion and foot IK, sets culling to
AlwaysAnimate, and drives one AnimationClipPlayable through one output in a
manually updated graph. Each sample restores every copied local TRS, sets the
explicit playable time and evaluates with zero delta. Component and graph audits
reject extra components, an assigned controller or changed evaluation settings.
No native AnimatorController or StateMachineBehaviours are instantiated; this
avoids native CombatAction/AttackStart callbacks that require a real CEL.

Cleanup destroys each graph before its target root, including partial setup
failures, then observes root destruction. Results preserve native Avatar and
clip identities, Animator flags, graph counts/update mode, per-frame activity,
variation metrics and cleanup failures. The native Avatar contains old Kraken
paths and may not bind modern main-state paths; no generic-avatar fallback is
performed. Varied poses must be measured, and reference Animator evaluation,
appearance transitions and gameplay remain separate unresolved acceptance gates.
The exact installed Unity2017 API was checked and the helper built successfully;
actual playable behavior is pending isolated runtime validation.

### Actual Unity enemy scale baseline test

At a native Ready screen, send the `scale-baseline-test` operation with the usual session/id envelope and no additional arguments. It pins the same Ready dungeon, level, room and encounter sessions across the test. The helper resolves the actual loaded framework's `EnemyVisualScale` component and reports its module ID and assembly-file hash.

Six cases use owned detached roots with only Transform and the production scale component: native-style baselines 0.35, 0.9 and nonuniform (0.35, 0.9, 1.2), each active and never activated. They test neutral and explicit scale/width factors, repeat application, real Unity cloning of already scaled objects, serialized baseline fields, independent source/clone changes, factor 0.55 and the internal legacy absolute mode. All owned roots must compare Unity-null after deferred destruction. No DB row, prefab, CEL or live avatar is changed. This validates component arithmetic and Unity lifecycle behavior; it does not itself prove the combat spawn hook or visual acceptance.

Playable initialization now occurs after the copied roots are active: Rebind,
graph.Play, SetTime(0), then zero-delta Evaluate. The installed Unity2017
Animator.isInitialized property is recorded before and after initialization,
alongside native Avatar validity; sampling rejects an uninitialized Animator.
Every requested sample still performs its own full rest reset afterward.
Initialized does not mean correctly bound: native-Avatar path compatibility and
nonzero pose variation remain required measurements, not inferred acceptance.

### Measured scale lifecycle and Kraken sampling follow-up

`scratch/scale-baseline-live-v1.json` records six actual Unity synthetic scale
cases against framework9e533f89, with all assertions passing and twelve owned
roots observed Unity-null after cleanup. Neutral scale, multiplicative axes,
repeat application and cloned baseline behavior passed; Ready remained0/2.
These are lifecycle tests, not visual acceptance for every native prefab.

Kraken v3 (`scratch/kraken-native-samples-v3-playable`) uses initialized native
Avatars with controller-free manual ClipPlayable evaluation. Five samples per
clip now show motion: the four main clips vary modern local articulation while
old locals remain constant; appearance varies old neck/head/jaw. Root motion
varies on both copies and all temporary copies clean up. Preserve the earlier
inactive and active zero-delta trials. This does not yet validate a hierarchy
adapter, native controller reference, crossfade or production retargeting.

### Conditional player apparel validation

Player profiles can include `apparel` (0–16 entries) alongside the nonempty required
`renderers` (1–16). Each conditional entry has `rendererPath`,
`expectedNativeMeshName`, `glbFile`, and optional `textureFile`. Paths must be unique
across both arrays. Native mesh names are exact nonblank strings, at most 160
characters, with no control characters. Required and conditional assets use the
same owned model directory and basename checks.

`command.py --root /absolute/path/to/scratch/game-copy playercatalog-preflight`
checks the loaded player catalog against its immutable content-plugin snapshot and
reads each registered class's exact native skinset avatar prefab. It shares the
strict single-player Ready/session/root/dungeon-position guards with enemy catalog
preflight. Present conditional renderers must match their exact native mesh name
before strict GLB decode. Absent conditional paths still have asset paths, sizes,
and hashes checked, but are reported `conditional_absent_on_base_prefab_not_decoded`;
`allConfiguredAssignmentsDecoded` is false. Native Stitcher adds apparel later, so
this result cannot establish live outfit coverage. No prefab instantiation, native
mesh mutation, gameplay action, or texture application occurs.

`record_player_case.py` accepts an explicitly selected required or present
conditional renderer. It checks all present conditional GLB identities, hashes
all conditional assets (even absent ones), and requires exact required/apparel
arrays in the content registration report for profiles specifying apparel.
A wrong mesh at a present conditional path fails before the one native action.
The `player-outfit-coverage` journal entry separates `absentApparelPaths` from
`activeUnmappedRenderers` (actual current path, mesh, and instance ID). Absence of
an alternative garment alone does not mean the current outfit is unstyled.
Conversely, all configured assignments being present does not prove that other
active native garments are styled. An unmapped renderer cannot be selected for a
custom-player capture. Native pre-replacement mesh names are no longer available
in the swapped live inventory; the recorder proves current custom GLB identities,
not independent recovery of those original names or an artistic verdict.

Offline validation covers optional absence, present wrong/duplicate custom mesh,
selection boundaries, actual unmapped apparel reporting, schema bounds and
registration snapshot mismatch. Live validation of these additions remains pending.

### On-disk deployed binary pins

New `run_case.py`, `record_case.py`, and `record_player_case.py` journals begin with
`kind: deployed-binaries`, before profile preflight, helper calls, or bridge actions.
The entry records the helper session, isolated root, and exact framework/helper/content
DLL paths, lengths, and SHA256 values under `data.binaries.framework` and
`data.binaries.helper`. For journals parsed as JSON arrays, the comparator pointers
are `/0/data/binaries/framework/sha256` and `/0/data/binaries/helper/sha256`.
The corresponding deployment receipt must independently pin those same hashes.
These are measured **on-disk DLL identities**, not loaded-memory attestation.

Both DLLs must exist as regular nonsymlink files in the exact isolated
`BepInEx/plugins` directory. Shared input guards remeasure them before every helper
request and HTTP request; recorder pre-capture/action guards also pin them. A changed
or missing binary stops further dependent operations. An already submitted uncertain
action is never retried; pending capture collection still preserves its evidence.
Older journals without these measurements remain unpinned and must not be backfilled.

Player inventory coverage is renderer-type specific: the Blacksmith backpack,
helmet, shield and hammer remain native MeshRenderers outside the five custom
SMRs present in the apparel fixture. Backpack occlusion can hide a correct
custom body. Original apparel marker counts192/504/408 (boots/Gambeson/default
armor) are generated calibration geometry, not native exports. See the hashed
accessory and excluded-tentacle metadata in docs/model-runtime-validation.json.
The four invalid diorama tentacles still require an explicit adapter; their
used twentieth bind matrix cannot be discarded to force validation.

### Completed conditional apparel and native reference checks

The97-profile Blacksmith apparel fixture's native item59 cycle is now measured:
auto-equipped Gambeson, owned native unequip to default armor, then native
re-equip to Gambeson. Both avatars retain five custom SMRs; old leases4/6 each
release15 observed Unity-null resources, and new lease8 settles at refs2. This
is one outfit/equipment cycle, not all-apparel or global cleanup acceptance.
The97-profile base-skinset decoder passes but reports all three conditional
paths absent and not decoded. The separate378-enemy preflight passes in its
recorded earlier b3478c0c session; do not relabel it as the current575479 session.

Kraken modern native-reference comparisons now match selected stable poses for
Pass and attempted damage, plus the retained death prefix. The damage action
was blocked, leaving324HP unchanged. Death captured only91 of120 requested
frames before RendererDestroyed, so its match is explicitly a prefix result.
The comparisons use verified single-layer, weight-one interior clip frames;
transitions/crossfade are excluded. Large orange native effects obstruct visual
acceptance. Neither selected-pose agreement nor temporary sampler cleanup
validates the old-hierarchy adapter, whole-body fit or complete death motion.
See hashed reports in docs/model-runtime-validation.json and local
scratch/kraken-native-reference-v1 for manifests, selections and samples.


### Explicit native emission opt-out

Enemy `renderers` entries accept optional `disableNativeEmission` as a strict
JSON boolean. Missing or false preserves the native material's emission; true
uses the public four-argument `EnemyRendererMesh` option on the private replacement
material. Strings, numbers and explicit null reject the profile. This field is
not accepted on the separate player descriptor schema.

Full inventory snapshots now include read-only `materials` metadata from
`sharedMaterials`: material/shader identity, `emissionKeyword`, `emissionColor`,
and `_MainTex`/`_EmissionMap` texture identities (plus property support flags).
The read does not instantiate materials. For opt-in live acceptance require the
expected custom `_MainTex`, keyword false, emission RGB black and emission map
null on every selected part; compare the native source baseline separately.
The route stage validator enforces those configured texture and emission values
for every single-material skinned or rigid assignment before any gameplay
action, and records the private material instance IDs in its binding result.
The generic archive-plan builder also pins the stage journal when present so
the full per-renderer material snapshots remain losslessly auditable.
These checks establish material state at the snapshot, not every subsequent
native hit effect or visual quality.

The first actual Unity Kraken adapter trial fails target-model readback before
recording any frames (`scratch/kraken-adapter-live-v1`). Cleanup succeeds and
six invalid requests are refused; Ready remains0/2. Four independent modern
reference bridges from session575479 to245a805 match exactly (maximumerror0).
Their success does not turn the failed adapter into a pass. Source precision
analysis records old prefab rootZ132.8200073 with float32 spacing1.5258789e-5;
neutral world-roundtrip/readback error is about 7.4e-6/7.9e-6. A local-chain
readback correction at unchanged1e-5 tolerance awaits reviewed fresh live
validation. No adapter, appearance or crossfade acceptance is implied.

Kraken adapter v2 now passes four independently checked owned-hierarchy
mechanics trials after composing actual local TRS ancestry at unchanged1e-5
tolerance. Attack/Idle/Damage/Disappear use32/32/6/27 exact sample times;
maximum errors are2.398e-6/1.614e-6/1.395e-6/2.623e-6. Four fresh-reference
bridges match with maximumerror0. Actual Transform writes, root preservation,
repeat/neutral checks, internal invalid-input checks, restoration and temporary
cleanup are measured. The earlier v1 readback failure remains historical.
This is not skinned-mesh, native-event, blending, appearance or production
old-rig support. Disappear still derives from the91/120-frame native death
prefix. Hashed raw requests/results and independent reports are indexed in
docs/model-runtime-validation.json; aggregate Ready/pin receipt is separate.

### Native Kraken controller observation

`kraken-controller-fixture` uses the actual native controller in manual graphs
on two owned Transform/Animator surfaces. Fixed scenarios and source/callback
preconditions are documented in [the controller fixture guide](../KRAKEN-CONTROLLER-FIXTURE.md).
It performs no adapter writes and does not attach gameplay components. Run only
after reviewed deployment in the exact Ready slot. Graph completion is separate
from observing the intended crossfades and repeated-input agreement.

### Enemy portrait marker profile option

Enemy profiles accept optional `portraitMarkerPath` with one exact proper-child
path string. Runtime content registers it through `Content.SetEnemyPortraitMarker`
after the custom enemy's resource prefab is selected. Catalog preflight records
the source marker identity and unique leaf name, with live framing explicitly
pending. Missing means unchanged native framing; null and invalid paths reject.
For the old-Yeti/Briarback head marker use
`Root_M/BackA_M/BackB_M/Chest_M/Neck_M/Head_M/EncounterCam`.

After reviewed deployment, inspect new turn-order and enemy-panel snapshots and
`[enemy-portrait] selected` logs. Also verify a default/non-opt-in portrait, then
exercise missing/ambiguous-marker fallback only in owned test objects. This option
does not refresh cached UI textures or change body/combat-camera transforms.

The382-enemy catalog passes strict runtime decode in session16074358 with
catalog75671881; decode remains distinct from live fit or controller acceptance.
Five owned Kraken controller scenarios (appear, damaged, damaged-heavy, death,
death-light) and their241-frame repeats independently match. These native graph
observations do not validate an old-rig blending adapter. In fact, the proposed
Dj * inverse(DsRoot) * OjNative policy fails the required pure-main endpoint
by up to6.817214574, despite matching pure appearance within 2.98023e-7. The
modern and old immutable root frames differ; changing tolerance cannot repair
that algebra. The proposal was only audited offline, never assigned to Unity.
Hashed numerical findings are retained in the runtime evidence index; a new
explicit blend policy and verification remain necessary.

Kraken endpoint-policy v1 demonstrates why helper success and independent
agreement remain separate: all five241-frame scenarios and repeats report raw
success, but appearance fails independent comparison at1.06378e-4 against
1e-5 tolerance. The four other scenarios pass while appearance contribution
is zero, so they do not validate that interpolation. Unity's close-angle
Quaternion.Slerp approximation diverges from the explicit policy formula.
The reviewed exact-SLERP helper correction is source/build-only at this record
and requires a fresh live run. No failed appearance trial is promoted to PASS;
mesh deformation, native events and production old-rig acceptance remain outside
this owned-fixture numerical exercise. Hashes are in the runtime evidence index.

Passive native portrait tracing is available through explicit `portrait-watch`; see [PORTRAIT-TRACE.md](PORTRAIT-TRACE.md) for bounds, readout and limitations.

The exact-SLERP appearance correction now passes its actual owned-fixture rerun
and repeat:241 frames each, maximum independent error2.36390344e-6 across
447703 numeric checks at unchanged1e-5 tolerance (session1ed421a5, helper3ec43281).
This replaces Unity's close-angle approximation with the explicit declared
formula; it does not relax tolerance. The V1 appearance failure and earlier
four nonappearance passes remain distinct historical results. This is authored
endpoint mechanics only: no mesh deformation, visual continuity, jaw model-space
equivalence, production live adapter or full-controller acceptance. Exact
requests, manifests, cleanup and Ready evidence are hashed in the runtime index.

### Existing native portrait texture readback

`portrait-texture-capture` exports the **current contents** of the existing native
`uiActiveTime.m_PortaitTextures[FID]` Texture2D. First arm `portrait-watch`, allow the
native encounter to produce its portrait, and read the passive trace. Then submit
an explicit payload using that same session/arm and the current inventory enemy:

```json
{
  "enemy": "exact_registered_enemy_key",
  "enemyDummyInstanceId": 123,
  "photonId": -1,
  "turnIndex": 0,
  "expectedTextureId": -456,
  "traceFrame": 789,
  "traceArmCommandId": "exact_prior_watch_command_id"
}
```

IDs above are placeholders; never reuse IDs from a prior process or encounter.
The helper requires active single-player combat, the exact live enemy dummy/FID,
registered row, traced source CEL and texture, and a later frame than the passive
pre-render observation. The current texture must be the traced Texture2D at its
original 204×172 resolution. There is one CPU `EncodeToPNG` call, with no render,
resize, GPU readback fallback, pose/material/texture change or retained Unity refs.
Unreadable textures fail. Owner/FID/texture/combat identities are rechecked after
encoding, then bounded PNG bytes are written exclusively to the owned output
`<command-id>.portrait.png` via a temporary file and atomic rename.

The JSON includes PNG SHA256, export frame/time, original trace and arm evidence,
texture filtering/format, and active loaded-scene RawImages using the exact same
texture. RawImage metadata includes hierarchy and portrait ancestor, UV rectangle,
color, canvas alpha/culling, rect/world corners/lossy scale, parent Mask/RectMask2D
metadata, serialized assigned material and existing CanvasRenderer materials.
It never invokes `materialForRendering` or stencil-material modifiers. Inventory
and ancestry limits fail explicitly; successful reports have `uiMetadataTruncated:
false`. No matching RawImage is an observed empty list, not proof the texture is
unused. UI metadata is not baked into the exported PNG, and world/rect dimensions
are not claimed to be final screen pixel dimensions.

The same texture instance ID does **not** establish unchanged pixels since the
trace, cache reuse, correct UV sampling, or visual quality. Static tests enforce
absence of render/mutation APIs and post-encode identity guards; the combined
helper build checks shipped Unity/game signatures. Actual CPU export and HUD
appearance remain live validation, performed only by the runtime owner.

The optional one-shot original Kraken five-bone marker experiment is documented in
[KRAKEN-SKIN-PROBE.md](KRAKEN-SKIN-PROBE.md). It preserves the existing endpoint
request and verifier; skin/image evidence is a separate acceptance gate.

Portrait export additionally requires `nativeSnapshotFinalized: true`, written by
the native Snapshot finalizer, `telemetryComplete: true`, and an explicit JSON-null
`nativeSnapshotException`. Absent/legacy finalization records remain rejected.
The shipped Newtonsoft.Json 4.5 library turns an implicit C# string-null into an
in-memory `JTokenType.String` whose serialization looks like JSON null; the helper
constructs `new JValue((object)null)` explicitly. A shared pure contract is linked
into the finalizer, export guard and regression executable, so tests exercise the
actual in-memory token representation rather than only serialized JSON.

Reproduce the historical rejection and test the fix with the shipped library:

```sh
dotnet run --project tools/ai-model-pipeline/portrait-finalization-tests/PortraitFinalizationTests.csproj \
  -c Release -p:TestGameRoot=/absolute/path/to/scratch/game-copy -- \
  scratch/honeyback-native-pixels-trace.json scratch/honeyback-native-pixels-request.json
```

These 24 assertions cover the observed trace/request join, concealed string-null,
explicit successful finalization, deep clone/serialization, native exception, and
missing/malformed fields. They run on the host .NET runtime with the game's actual
Newtonsoft DLL; native Mono/Harmony execution still requires the next isolated
live test. Existing saved evidence is not rewritten or retrospectively stamped.

### Constructed native row-preview UI fixture

`native-row-portrait-fixture` is an explicitly constructed native UI call, **not**
an opened encounter menu. At strict native Ready, first arm `portrait-watch` for
one exact registered custom row. It must remain empty and error-free. Submit:

```json
{"enemy":"exact_registered_row","traceArmCommandId":"exact_watch_command_id"}
```

The dispatcher supplies the normal id/session/op fields. The fixture creates its
own detached RectTransform/UI/target/level text, then invokes the actual
`uiEnemyEncounterPortrait.Initialize(string)` exactly once. Native code instantiates
the real active-time portrait prefab, creates the portrait texture, calls the row
Snapshot path and forwards the resulting texture to its HUD-style RawImage.
No EnemyDummy is synthesized and no menu or combat action is opened.

Preconditions include: exact custom enum→row identity; current game definition;
no enemy-scale DB entry for the custom key (excluding the native lazy scale cache);
Deimos inactive; bounded audited UI component types; and native dimensions derived
from a fixed-anchor child RawImage rectangle using the native cast-before-AA formula.
Each dimension is bounded to 1..1024 pixels and AA to 1..8. The shipped row prefab is
82×70 at AA4 (328×280); the combat HUD is a different 51×43 prefab (204×172).
An existing matching camera must be idle with no target or current texture. When
absent, only the native Initialize call may create its normal cache entry. The
fixture requires exactly that one new entry, unchanged existing entry references,
exact RenderTexture dimensions, and clear target/texture afterward. This native
camera and RenderTexture persist; they are excluded from preview disposal claims.
Unsupported dependencies reject before the native call.

The report includes loaded assembly/file identity, original watch evidence, the
single finalized row-forwarded passive trace, current owned PNG/hash, both native
RawImage texture IDs/UV/color and level text, and source prefab TRS/mesh/material-ref
snapshots. Successful execution requires exact source and Ready invariance. The
newly created native portrait lease is read immediately after native Snapshot
returns, before deferred destruction, without retaining it. After four Unity frames,
its absence and each pinned resource's Unity-null state are measured. Only those
preview assets are covered by the disposal claim.

Owned UI and fresh texture are destroyed in finally; `RenderTexture.active` is
restored. Native normal success performs its own target Stop/destruction. If a
native exception leaves an unproven partial camera target, the fixture does not
call broad shared-camera Stop or delete unknown objects: the result fails with
cleanup-unproven metadata. There is no retry or alternate render. PNGs captured
before a later failed cleanup remain partial evidence, not successful validation.
Source/type checks and the shipped-Newtonsoft finalizer regression run offline;
actual native rendering/UI/lease behavior requires an isolated live run.

The row fixture snapshots pre-call Texture2D instance IDs (bounded at65,536).
Only a new, nonzero texture identity with the exact derived row dimensions may enter its cleanup-owned slot;
rejected shared/unknown textures are never destroyed. Recovery uses the same gate,
and independent cleanup attempts ensure a reflection failure cannot skip known
owned UI destruction. `row-portrait-ownership-tests` links the exact pure gate and
checks26 positive/negative ownership and native dimension cases on the host runtime. Native lifetime
and rendering remain live tests. Row trace acceptance also requires the actual
`uiEnemyEncounterPortrait.Initialize` caller, requested native pose, and each
configured custom GLB path/name exactly once in the fully recorded target clone.

### Pinned Gloamfin organic skin fixture

The existing `kraken-skin-probe-arm` request without `variant` retains the exact
120-vertex five-octahedra manifest, protocol, and geometric verification. Optional
`"variant":"gloamfin-v1"` selects only the reviewed original organic blockout:

```json
{"op":"kraken-skin-probe-arm","variant":"gloamfin-v1","scenario":"appear","manifestSha256":"8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e"}
```

Use the existing command transport to add the unique command ID and current
session nonce; then submit the unchanged endpoint-policy appearance controller
fixture once. Prepare the three pinned files under the isolated models directory:
`gloamfin-kraken-blockout-v1.manifest.json`, `gloamfin.glb`, and
`gloamfin_basecolor.png`. The authoring artifact directory is
`art-experiments/gloamfin-kraken`. Arming creates no render assets; consumption
rechecks the same variant and all file hashes before the owned fixture begins.
The arm result and skin identity carry the variant and selected manifest hash.
No runtime player/enemy model is replaced by this operation.

This asset has 5,640 vertices/indices, 1,880 triangles, and 1,128 vertices with two
positive weights. The new branch caps counts at 8,192 vertices and 49,152 indices,
requires the original five joint names/order/inverse bind matrices, normalized
one-or-two-influence weights, all five bones represented, and the exact pinned
weight histogram. Unity gets one owned Unlit/Texture material with the separate
pinned PNG; the GLB has no embedded material. The fixed 512×512 camera uses the
manifest's full-trajectory framing (orthographic size7, far plane60). All 12 PNG
and four BakeMesh steps remain unchanged. The independent verifier checks every
weighted vertex at the unchanged 1e-5 tolerance and runs the same 241-frame plus
repeat endpoint verification. Positive-weight motion cohorts overlap; their
displacement does not establish independent motion of each bone.

`verify_kraken_skin_probe.py` uses the same hash-pinned evidence manifest structure
for either variant and fails unknown/mismatched variants. The nine Gloamfin tests
include actual asset and original-bind identity, independent two-endpoint soft
weight arithmetic, malformed weights/counts/indices, variant mismatches, and a
last-vertex corruption to check full-array coverage. The 20 original probe tests
remain unchanged in behavior. Offline success does not establish Unity rendering,
continuous anatomy, final art quality, native mixed-pose equivalence, gameplay,
or a production Kraken adapter. Those remain separate evidence gates.

## Production Gloamfin Kraken observer campaign

The `kraken-production-adapter-arm`, `kraken-production-adapter-state`, and
`kraken-production-adapter-clear` operations passively observe the exact
`enkrakenhead` resource route after Gloamfin binds to the real `krakenHead`
enemy. This route must not have a generic `runtime-profile.json`. Arm with the
live `EnemyDummy`, CEL, and replacement renderer instance IDs, the pinned
Gloamfin manifest hash, and one campaign role:

```json
{
  "enemyDummyInstanceId": 123,
  "celInstanceId": 456,
  "rendererInstanceId": 789,
  "manifestSha256": "8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e",
  "campaignRun": "native-combat-death"
}
```

The two required roles are `native-combat-death` and
`enemy-victory-terminal`. Use a fresh real owner for each. The first combines
the four source-backed Kraken proficiency attacks, ordinary hit responses,
DEFEND and DAMAGED, an ordinary non-cheat lethal hit, native DEATH, and natural
teardown. The second reaches native VICTORY through an ordinary party loss and
then tears down. After launching the isolated game and obtaining its helper
port, stage and run the death role:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_case.py new-run \
  --root /absolute/project/scratch/game-copy \
  --port PORT \
  --enemy ftkmf_modeltest_gloamfin_kraken_legacy

python3 tools/ai-model-pipeline/runtime-test/run_kraken_production_campaign.py run \
  --root /absolute/project/scratch/game-copy \
  --port PORT \
  --role native-combat-death \
  --report /absolute/project/scratch/kraken-campaign/native-combat-death-SESSION.json
```

Stop that process, launch a fresh process, obtain its new port, and stage the
victory role with one native companion enemy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_case.py new-run \
  --root /absolute/project/scratch/game-copy \
  --port PORT \
  --enemy ftkmf_modeltest_gloamfin_kraken_legacy \
  --companion-enemy ogreA \
  --skip-fortify

python3 tools/ai-model-pipeline/runtime-test/run_kraken_production_campaign.py run \
  --root /absolute/project/scratch/game-copy \
  --port PORT \
  --role enemy-victory-terminal \
  --report /absolute/project/scratch/kraken-campaign/enemy-victory-terminal-SESSION.json
```

The runner requires the current content-registration handshake and a same-session
`new-run` marker. It creates an exclusive role claim and will not retry an
uncertain action. With two enemies, only the selected Gloamfin owner may bind the
custom renderer; the native companion remains outside the adapter. A late Visit
story can be serviced once but is never replayed. Every report independently checks the framework, helper,
content plugin, game assembly, source/evidence files, controller extraction,
manifest, GLB, PNG, live controller, two manual samplers, full mesh resource
lease, and natural teardown. Full file hashes run only at arm and explicit
state checkpoints. Per-frame observation reads native runtime state; it does
not hash `resources.assets`, drive the controller, invoke combat, or destroy
game objects.

Keep attack evidence native. A credited Kraken window starts with
`attackAnim=AttackProf` and `override=Attack`, enters ATTACK, and contains the
native callback sequence, primary dodge and hit responses, expected post-hit
health, native exit transition, `ActionCompleted`, and a later idle frame. The
campaign needs at least one successful window for `enKrakenResistUp`,
`enKrakenInterrupt`, `enKrakenConfuse`, and `enKrakenArmorUp`. Nonattack
triggers, Foley, hit/dodge responses, and completions use a separate
state-callback ledger and cannot earn attack credit.

A failed native proficiency roll is valid evidence for that attempted window,
but it does not satisfy the required successful-proficiency set. The native
`RespondToDodge` callback belongs to the damage phase even when the resulting
response is Damaged, Block, or Death; only an actual Dodge result earns a DODGE
transition. Preserve unused `StartEngageAttack` calculations in the report.
Every applied hit must still match one exact earlier calculation in order.

Save each final report only after natural owner teardown. Assemble and verify
the two distinct helper sessions offline:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_kraken_production_campaign.py assemble \
  --death-report scratch/kraken-campaign/native-combat-death-SESSION.json \
  --victory-report scratch/kraken-campaign/enemy-victory-terminal-SESSION.json \
  --deployment-receipt scratch/game-copy/deployment-backups/DEPLOYMENT/deployment.json \
  --campaign scratch/kraken-campaign/campaign.json \
  --verification scratch/kraken-campaign/verification.json
```

The assembler writes schema `ftkmf.kraken-production-adapter-campaign.v1`,
pins exact binaries and the deployment receipt, and runs the aggregate verifier.
After preparing a structured supplemental visual observation and its image,
create a new immutable archive and verify every contained hash:

```sh
python3 tools/ai-model-pipeline/archive_kraken_production_campaign.py \
  --campaign scratch/kraken-campaign/campaign.json \
  --verification scratch/kraken-campaign/verification.json \
  --visual-observation scratch/kraken-campaign/visual-observation.json \
  --visual-image scratch/kraken-campaign/gloamfin-live.png \
  --output docs/evidence/kraken-production-adapter-vNEXT

python3 tools/ai-model-pipeline/verify_kraken_production_archive.py \
  docs/evidence/kraken-production-adapter-vNEXT
```

The verifier keeps all 15 states as a pinned static controller inventory, then
checks only source-backed exact-route reachability. It requires the natural
IDLE, ATTACK, DEFEND, DAMAGED, DEATH, and VICTORY subset, approved serialized
edges, native callbacks, and finalized adapter, sampler, and full resource-lease
cleanup. `PASSIVE VICTORY`, `DEATHLIGHT`, `ATTACKCRIT`, and `ATTACKPROF` receive
no live credit without future source and exact-route evidence. Passing this
observer archive alone still
leaves endpoint composition, root visual acceptance, camera and culling,
portrait, gameplay progression, and immutable archive review as separate gates.
The current verified observer archive is
`docs/evidence/kraken-production-adapter-v1`; its `validation.json` SHA256 is
`6154ad173ef779a04c443ad0016093f28eb222d79614ea682efbe409ae4bcdd9`.

### Canonical visual and portrait follow-up

The canonical Gloamfin route combines the observer archive with reviewed
appearance, motion and gameplay captures plus one fresh native portrait. Its
immutable archive is
`art-experiments/gloamfin-kraken/live-validation-v2-canonical`; verify it with:

```sh
python3 tools/ai-model-pipeline/verify_kraken_canonical_archive.py
```

For a new legacy Kraken asset, launch one isolated game process with the exact
reviewed framework and helper deployment. The follow-up runner arms
`portrait-watch` before encounter creation, waits for the first finalized
exact-owner snapshot, exports that snapshot's native portrait texture, records
active initiative UI users, and stops the watcher and its owned process:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_kraken_portrait_followup.py \
  --root /absolute/project/scratch/game-copy \
  --framework-sha256 EXACT_REVIEWED_FRAMEWORK_SHA256 \
  --report /absolute/project/scratch/kraken-portrait-followup.json
```

The trace must identify `verified_live_enemy_dummy`, its exact EnemyDummy ID
and FID, the selected CEL and renderer, leased custom mesh, render target, and
every active UI image using the texture. The fallback is valid only for exact
`enkrakenhead`, a synthetic custom row without an explicit portrait marker, an
actually owned explicit custom mesh, and unit renderer world scale. It may
uniformly reduce only the disposable portrait clone within 10%-100%. It must
leave the live source CEL, bones, camera, clip planes, shared prefab and combat
model unchanged.

Review the PNG at original resolution, preserve framework/helper hashes and
deployment receipts, create an immutable follow-up record, then run:

```sh
python3 tools/ai-model-pipeline/verify_kraken_portrait_followup.py
```

The portrait follow-up does not replay combat. It proves exact native portrait
framing and UI use only. Keep the production observer and reviewed combat
captures as the authority for behavior, ordinary lethal, victory and teardown.

### Native Party Select Start

`native-party-class` performs one native class-arrow step. Send `action: "inspect"`,
the exact `ownerInstanceId`, registered `classKey`, `targetClassId` (Wildbloom: 114),
and `direction: "left"` or `"right"`. Submit the same fields with the returned
`inspectionToken` and `action: "submit"`. The helper pins the entire bounded
class visibility route and actual row objects, current preview, selected adventure
and native arrow callback. It calls exactly one public `OnClassClick` or
`OnClassClickLeft`; native code performs invisible-row skipping, skin/helmet
selection, readiness updates and avatar replacement. It never calls private
`SetClass` or writes class/readiness/avatar fields.

The pinned `arrowCandidates` reports every native array entry, callback identity,
active/interactable/owner-child state, target graphic visibility and exclusion
reasons. Inactive alternative arrows are excluded before ambiguity is checked.
Direction additionally requires the exact ordinal native control name
`classArrowNext` for right or `classArrowPrevious` for left. The legitimate
`toggleClass` label invokes the same right callback but is reported as ineligible
with `not_explicit_directional_control`. Names do not bypass any callback,
ownership or visibility checks; the selected name and instance identity are pinned.
Repeated references to the same button count once; two distinct eligible buttons
remain a failure. Visibility requires an enabled graphic and canvas, separately
finite-positive graphic/renderer/applicable group alpha factors and an unculled
renderer. Graphic and canvas identities are pinned. Raw alpha factors are reported;
no approximate effective-alpha product or arbitrary opacity threshold is used.
These are scoped UI eligibility checks, not proof that final pixels are unoccluded.
Missing or ambiguous eligible arrows return diagnostics without a token or click.
Changing candidate eligibility between inspection and submission invalidates the
snapshot.

Read `reachedTarget` and `observedPreview`. A distant target needs fresh
inspect/submit pairs on later frames; there is no automatic cycling. Visible
locked intermediate classes remain navigable with native readiness false, while
the requested target must be usable, revealed and unlocked. An already selected
target consumes the token without a callback. Callback exceptions or an unexpected
class, default skin or nonreciprocal/reused avatar latch uncertainty and block
further helper clicks for that process. Successful submission is selection
evidence, not a claim that the player model or subsequent game run is validated.

`native-party-start` with `action: "inspect"` reads the actual offline,
single-player Party Select gate and returns an `inspectionToken` plus pinned
adventure, menu, Start button and reciprocal preview identities/selections.
Submit that token with `action: "submit"` and `inspectionToken`. Both requests
use the usual exact session and request ID. Missing/ambiguous state, changed
selections, locked or unready previews, another screen's focus, system dialogs,
or an inactive/noninteractable button fail closed. The button's sole enabled
persistent callback must resolve to the current menu's `EnterFahrul` method.

Submission invokes `uiStartGame.EnterFahrul()` once, preserving its readiness,
RPC, story and player-creation continuation. It does not set readiness, choose
classes, send arbitrary callbacks, or bypass native creation. The process-wide
claim is consumed before invocation, including if the callback throws or its
RPC is deferred. A new command ID cannot retry it. `ok` means the callback
returned, not that character creation or the run completed. Inspect the reported
immediate fields and subsequent native story/game state separately. No game is
launched by this operation; it is available only inside the opt-in test helper.

Offline checks:

```sh
dotnet run --project tools/ai-model-pipeline/party-start-tests -p:TestGameRoot="$PWD/scratch/mirewarden-game"
python3 -m unittest discover -s tools/ai-model-pipeline/runtime-test -p 'test_native*boundary.py'
```

For one guarded native combat Focus input without attacking, see
[NATIVE-COMBAT-FOCUS.md](NATIVE-COMBAT-FOCUS.md). This isolated helper fixture
observes the native animation debit and never retries uncertain completion.

### Read-only world input gates

`world-input-state` accepts only the normal `id`, `session`, and `op` envelope.
It snapshots the current hero and party ownership, turn, HP, action points,
respawn and hex state; movement FSM and tracked hero; native input focus and
Quick Use focus; current controller identity; end-turn availability, modal,
chat and console gates; and encounter state and retained diorama.

It reads the native `m_HexLand` property, including its last-hex fallback.
Missing objects are null; a section that throws reports `available: false` and
its error instead of inventing a gate value. The operation does not poll input
buttons, change focus, send events, advance turns, or write game/save state.
Compare snapshots before combat and after native death/revive and return to
the world. These observations identify gate differences; they do not establish
a cause or repair an inert UI. Live verification requires the updated helper
in an explicitly configured isolated game copy.

The same snapshot includes `titleScreens`: actual scene `MainScreen` instances
(including inactive ones), their instance IDs, current-focus identity, selectable
parent, and recursively located `ModsButton` cells. Cells report activation,
local position, RectTransform bounds, descendant text and button availability.
Resource prefab assets are excluded using scene validity. This distinguishes a
button injected into another menu instance from a hidden or misplaced live cell.

`creationScreens` reports scene character-creation instances, numeric class/turn
IDs, saved instantiation-array length and element type names (at most 64), and
only saved numeric turn/class values. It also reports native class-table, UI
creation-target, camera player-target, and color-palette lengths plus palette
indices. It never emits serialized character names or complete saved values.
This helps locate an initialization index mismatch without retrying `Awake` or
changing the failed screen.

Encounter diagnostics include separate client/master encounter types, master
started/combat flags and encounter index, combatant/client identities, and the
native acknowledgement waiter ID, continuation method name, delay and remaining
client IDs. Identity and waiter lists are capped at 64 entries. Current-world POI
and master POI identity/type are reported separately. Reading these values does
not call the waiter's continuation or acknowledge any client.

### Native fight entry trace

`native-fight-trace` accepts `action: arm`, `inspect`, or `disarm`. Arm installs
five read-only callback entry prefixes for `uiEnemyPoiMenu.UseFightButton`,
`uiLocationMenuEntry.OnClick`,
`MiniHexInfo.OnFight`, `GameFlow.LocalInitCombatSession`, and
`EncounterSessionMC.InitiateEncounterSessionRPC`. They expire after 60 real-time
seconds and are removed on expiry, disarm, or helper destruction. No finalizers
are installed. A sixth prefix observes only `ContinueFSM.Continue` calls whose
argument is `menuFight`. It records the continuation's ID, caller, wait state,
locality, target FSM name/state, stored event and delegate method identities; it
never calls a delegate or continuation. Changed continuation state is sampled
on later helper frames. Intermediate transitions within one frame may be missed.
A seventh prefix observes `Fsm.Event(string)` only for `menuFight` on the exact
continuation FSM reference. Its snapshot reports owner enablement, active object
path, native FSM active/started/finished flags, event routing target, active-state
transitions and global transitions (at most 64 each). This proves entry into that
FSM's event method, not successful event processing or a completed transition.
An active trace cannot be rearmed without disarming it first.

Inspect returns up to 64 entry records, a dropped count, and up to 32 currently
visible location-menu entries with resolved handler names and persistent button
callbacks (up to 16 per button). Runtime-only UnityEvent listeners are not exposed
by that native public API. Session payloads and character names are not captured.
No callback is invoked, acknowledgement sent, or focus/game state changed. Use
native UI to click Fight after arming. Entry records show methods reached, not
successful completion; instrumentation can change runtime timing. Compare with
`world-input-state` master/client diagnostics and an uninstrumented run.

### Actual native player studio image

`player-studio` renders an existing active avatar synchronously, without creating
or equipping a character. Supply `source: preview` or `world`, exact
`ownerInstanceId` and `celInstanceId` from the observer, and `view: front`,
`three-quarter`, or `back`. World capture requires an owned noncombat hero.
The isolated-root and single-player helper guards remain mandatory.

Output is `model-test-output/<command-id>.png` at 768 by 1024 pixels, with a
neutral opaque background. Framing uses live bone transforms, never native
mesh vertices or bounds. The camera preserves the current native pose. Studio
lights supplement existing ambient lighting. Renderer GameObject layers change
only for the synchronous render and are restored in `finally`; temporary cameras,
lights, textures and render targets are released there on success or failure.
No native camera, material, mesh, equipment, animation, or gameplay state is edited.

The receipt pins owner/avatar, equipment, session and binary identities and image
hash. Review framing visually before assembling a lineup. This is a presentation
capture of actual native character geometry, not a native geometry export or a
new gameplay/animation acceptance result. Equipment staging remains a separate
native action; this operation never grants items or changes outfits.

### Native preview race fit fixture

`preview-race` accepts `action: inspect`, `apply`, or `restore`, with exact active
`ownerInstanceId` and numeric `classId`. Apply also requires numeric `skinType`
(0 Female, 1 Male, 2 Undead, 3 Cat, 4 Demon, 5 Fish, 6 Goblin). Inspect reports
native skinset names, support and unlock availability. Apply accepts only a real
supported skinset but does not require its lore unlock in this isolated fixture.
It never changes unlocks or preferences.

Only one preview fixture can be active. The helper retains the original race and
inventory reference, assigns the preview skin type, and invokes only native
`SetClass(currentClass)` to rebuild the avatar. Class, inventory contents/reference,
outfit and colors must remain unchanged. Failure attempts restoration through the
same native rebuild; it does not repair inventory or retry a game action.

After apply or restore, inspect on a later frame. `studioReady` requires the old
avatar to have been destroyed and its replacement to be active and reciprocal.
`player-studio` enforces this gate. Restore before leaving the preview; once settled,
inspect clears the fixture. Helper destruction attempts restoration if the same
preview/class still exists. Never start or save a run with a staged fixture.
Race-fit captures are visual evidence only, not lore-unlock or gameplay acceptance.

Post-loot diagnostics include master loot-collection/vote and client reward/teardown
FSM activation, active/global transitions, and existing integer/boolean variables
(up to 64 each). The snapshot also reads the master completion continuation and
`IsInvoking` for `ShowNextXPGold` and `ReturnToOverworld`; it never invokes them.
A retained acknowledgement with an empty wait list can be historical after its
completion, so it is not evidence that the corresponding reward phase is blocked.
Use the live loot FSM state to locate the current phase.

### Controlled native custom loot collection

`custom-loot-fixture` accepts `action: arm`, `inspect`, or `disarm`. Arm once per
helper process during active native enemy combat before the first attack; the claim remains spent after abort
or disarm. It expires after 600 real-time seconds. The fixture pins the existing
master/client encounter objects, exact native `lootDropItems` ArrayList and current
enemy-combat diorama. Only a successful native `FillLootDropList` call
for that exact enemy-victory context can consume it.

A single postfix appends the registered `paladin_helmet_novice` string once after
native loot generation. The item must still be the same registered row, with an
empty lore-unlock string. Existing loot and gold/XP are unchanged. The hook is
removed before append and on expiry/disarm/helper destruction. No database row,
inventory or save is edited, and no vote/Collect callback is invoked.

Inspect reports the resolved item ID, original loot list/count, appended token,
resulting count, and native gold/XP. `appended` is fixture setup success only.
Record inventory before and after clicking the ordinary native Collect button,
confirm exactly one additional item and no native exception, then finish the
encounter naturally. This proves controlled custom-item collection, not natural
random drop frequency. Never automatically retry an uncertain fixture result.

### Read-only encounter location inventory

`world-input-state.encounterPois` reports existing native Enemy and Dungeon POI
categories, with each object's instance/type, native ID, tile parent/index,
active/deactivated/locked/hidden flags. Each category is capped at 1024 entries
and reports its native count and truncation flag. Missing world data returns null.
The observer checks the native category dictionary before `GetPOIList`, because
that native method would otherwise create an empty list for a missing category.
It does not generate encounters, reveal tiles, unlock POIs, or move the party.
Enemy combat level is not inferred from dungeon progress or display names.
These are test-only location observations, not evidence of encounter difficulty
or normal discovery and travel.
