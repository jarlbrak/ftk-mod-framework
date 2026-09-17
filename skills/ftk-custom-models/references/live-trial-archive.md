## Repeatable live-trial record

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

For each new exact model/chassis assignment, preserve a small immutable evidence
supplement beside the authoring package. Use a fresh isolated process and the
catalog/profile hash recorded by the runner. Keep the complete pass, attack and
explicit `KillSingle` captures, their journals/results, the case result, profile
and manifest references, and every source PNG hash. A local `archive.py` may
gzip metadata losslessly, pin all source PNGs, copy a deliberately small set of
root-reviewed originals, and derive presentation videos; it must refuse to
overwrite a completed destination. Verify artifact hashes and every gzip
round-trip independently after the archive is built:

For a completed queue-route runner record, first turn the reviewed exact run
into a fresh archive plan rather than copying a historical plan:

```sh
python3 tools/ai-model-pipeline/prepare_model_visual_review.py \
  scratch/my-isolated-game/model-test-output/case-<id>/case-result.json \
  --output scratch/my-route-root-review.json
```

Inspect the pinned source images, replace every pending observation, and set a
reviewed status before using that review below. The plan generator rejects a
pending template.
When the case retains repeated accepted actions, use the unique archive action
keys in each reviewed frame row, for example `attack-attempt-1` and
`attack-attempt-2`. A shared `attack` label plus the same frame index would map
two sources to one selected filename, so the archiver rejects the collision.

```sh
python3 tools/ai-model-pipeline/make_execution_queue_archive_plan.py \
  --record scratch/my-route-run.json \
  --visual-review scratch/my-route-root-review.json \
  --plan-output art-experiments/my-model/live-validation-vN-plan.json \
  --archive-output art-experiments/my-model/live-validation-vN \
  --revision VN \
  --limit 'State the observations outside this archive.'
```

The generator checks the runner, case, catalog, profile document, source-asset,
and review-session pins before it writes a new plan. It does not select review
frames, build the archive, or make a visual conclusion.

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

The verifier rejects changed metadata, selected-frame copies that differ from
their pinned source images, unsafe game-payload mappings, and changed
presentation-video hashes or metadata. Its `PASS` is artifact integrity only;
use the validation ledger and root review for behavior and art conclusions.

After a batch of new archives, regenerate
[`MODEL-VALIDATION-ARCHIVE-INTEGRITY.md`](../../../docs/MODEL-VALIDATION-ARCHIVE-INTEGRITY.md)
with:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_archive_integrity.py \
  --overwrite --check-video-metadata
```

Keep any historical integrity gap visible until a
fresh immutable replacement exists; do not silently loosen the verifier or
relabel the model result.

After the original-model index, validation-gate ledger, topology coverage, and
package-readiness report are current, regenerate candidate coverage with that
current archive-integrity ledger:

```sh
python3 tools/ai-model-pipeline/audit_model_candidate_coverage.py \
  --output-json docs/model-candidate-validation-coverage.json \
  --output-markdown docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md \
  --overwrite --fail-on-unmapped
```

The report compares the exact indexed validation hash against the integrity
ledger. An absent, changed, or integrity-unverified artifact stays in the
follow-up queue even when its structured fields are complete.

Turn the remaining routes into concrete profile-and-renderer work items with:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_execution_queue.py \
  --output-json docs/model-validation-execution-queue.json \
  --output-markdown docs/MODEL-VALIDATION-EXECUTION-QUEUE.md \
  --overwrite
```

The queue chooses the priority archive's exact source assignment where one
exists, otherwise one remaining exact topology representative. It does not
choose among multiple matching historical profile documents, prove a deployment,
or promote static preflight into a live result. A target without a profile needs
new authoring before a trial can be staged, unless the queue records an explicit
adapter-or-retarget requirement.

For a new enemy route, use the plan-based
[`archive_model_validation_case.py`](../../../tools/ai-model-pipeline/archive_model_validation_case.py)
builder described in
[MODEL-VALIDATION-ARCHIVES.md](../../../docs/MODEL-VALIDATION-ARCHIVES.md).
It requires an exact root review and immutable source paths, verifies a declared
frame identity across every retained image, and emits the common archive shape.
Do not retrofit it onto a historical archive or use it to upgrade a review that
was never performed.

For a game-owned player preview, use
[`archive_player_model_validation.py`](../../../tools/ai-model-pipeline/archive_player_model_validation.py)
with the player plan in the same guide. It pins the native preview session,
requires a concrete observed-avatar count, and checks the selected renderer and
native idle clips across every retained frame. It does not transfer a preview
observation to combat, apparel branches, teardown, portraits, or another
skinset.

The root review names exact frame paths and hashes and records what is visible
under native effects and hero occlusion. It must separate ordinary damage from
the explicit kill fixture, native Collect/Ready progression from art acceptance,
and selected-frame observations from claims about every animation interval,
culling, collision/sleeping, material properties, portraits, or final resource
disposal. Update the skeleton register and runtime evidence index with a new
supplemental entry while leaving historical failed or superseded trials intact.
Pin each supplement's `path` and `sha256` directly in that index entry; the
reconciliation and package-readiness audits inventory nested pinned references
without promoting them to a new acceptance result.
For a constructed fixture that intentionally has no runtime source assignment,
pin it in its own named runtime-index collection; the original-model audit will
report it separately rather than treating it as an unresolved enemy archive.
Immediately run [the original-model index audit](../../../tools/ai-model-pipeline/audit_original_model_index.py)
with `--summary --fail-on-novel --fail-on-unresolved`. A novel exact native
assignment must be added to the runtime index before treating the archive as
registered, and a runtime-style archive must declare its exact native identity.
An unindexed historical supplement for an already indexed exact assignment
remains a separate record, not a reason to rewrite it. This reconciliation
checks registration and native identity only; it never converts evidence
presence into an art or gameplay PASS.

Regenerate the [validation evidence ledger](../../../docs/MODEL-VALIDATION-GATES.md)
at the same time:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_gates.py \
  --output-json docs/model-validation-gates.json \
  --output-markdown docs/MODEL-VALIDATION-GATES.md \
  --overwrite --fail-on-unresolved --fail-on-integrity
```

It records only explicit structured evidence in each indexed archive. A
`recorded` cell is neither a gate PASS nor an art verdict; a blank cell means the
current adapter did not find the named record. Preserve explicit capture labels
or state clips for idle, attack, hit and death, numeric ordinary no-focus HP
transitions, and a structured `finalReady`/`ready` result so a future author can
audit the same package without interpreting prose.

Use each archive's `unrecordedSourceSpecificCoreEvidence` list to plan a
source-specific follow-up without treating it as a failure verdict or applying
it to a sibling source route.

For new archives, use a direct top-level `binding`, `visualReview`, and
`captures` array. Label captures exactly `idle`, `attack`, `nonlethal-hit`, and
`kill-fixture`/`death` when those actions are actually captured. Store ordinary
no-focus damage as numeric `beforeHp`/`afterHp` under `ordinaryAttack` or
`ordinaryHit`, keep `explicitKillFixture` separate, and store native
progression as `finalReady`/`ready` with `ok:true`. Player records also need
`avatarOwners.preview.observedAvatars`; zero records a pending native preview,
not a pass. Leave an unobserved field absent rather than deriving it from
prose, a generic pass capture, a focused trial, or a sibling source.

Measure an ordinary enemy hit inside the action's own `before` and post-capture
`after` states. Waiting for the next native player turn can advance more enemy
actions or status ticks. Preserve that later net result as `readyHpOutcome` or
`laterReadyObservedHp`, but never attribute the difference to the submitted
attack without its own causal native damage record. The generic archive builder
applies this action-window rule to older retained cases as well.

When one complete capture contains several causal motions, keep its real action
and terminal outcome, store the summarized native clip ranges under `states`,
and include `causalMotion` from the recorder when available. A mixed capture
such as idle, native attack, recovery, and full-health flee must not be reduced
to a generic `pass` label or split into invented actions. The gate adapter can
read explicit `cidle`, `attack`, `damage`, and `death` clip stems while the
archive keeps the actual chronology and limits.

If a completed runner becomes an `enemy_trial_record_identity_mismatch` after
the campaign is regenerated, inspect every plan field before deciding to run
again. A changed profile document, catalog, asset, topology, route kind, source
assignment, or motion renderer requires a new trial. If those semantic fields
all match and only the generated queue or stage-readiness hashes changed,
preserve the immutable runner, pin a reconciliation record that lists both old
and current snapshot hashes, conduct the root image review, and build a new
archive from the retained captures. Do not replay complete live actions solely
because unrelated route completions changed campaign bookkeeping. Verdigrin V4,
Sunspire Roc V4 and Belladusk V3 are exact-source examples for this workflow.
Belladusk also demonstrates why the new root review must derive each selected
frame's label from the raw animator timeline: two older selected frames described
as attacks were actually inside `idle` and receive no attack credit in V3.

Current examples are the [Rimecrown V3 historical whole-owner archive](../../../art-experiments/rimecrown-sentinel/live-validation-v3/README.md), [Rimecrown V4 canonical head archive](../../../art-experiments/rimecrown-sentinel/live-validation-v4-head/README.md), [Rimecrown V5 canonical scarf archive](../../../art-experiments/rimecrown-sentinel/live-validation-v5-scarf/README.md), [Rimecrown V6 canonical Root_M-weighted base archive](../../../art-experiments/rimecrown-sentinel/live-validation-v6-base/README.md), [Rimecrown V7 canonical hat archive](../../../art-experiments/rimecrown-sentinel/live-validation-v7-hat/README.md), and [Rimecrown V8 canonical middle-body archive](../../../art-experiments/rimecrown-sentinel/live-validation-v8-middle-body/README.md),
[Honeyback V3 canonical bearB archive](../../../art-experiments/honeyback-portrait-v2/live-validation-v3-canonical/README.md),
[Duneshade V2 canonical snakeDesertA archive](../../../art-experiments/duneshade-desert-asp/live-validation-v2-canonical/README.md),
[Ashfang V2 canonical wolfA archive](../../../art-experiments/ashfang-wolf/live-validation-v2-canonical/README.md),
[Moonreed V2 canonical fairyA archive](../../../art-experiments/moonreed-sylph/live-validation-v2-canonical/README.md),
[Sargassum primary V2 canonical krakenTentacle archive](../../../art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/README.md),
[Abyssal Crown V5 canonical krakenHead archive](../../../art-experiments/abyssal-kraken/live-validation-head-v5-canonical/README.md),
[Mirewarden V2 archive](../../../art-experiments/mirewarden-ftk/live-validation-v2/README.md) and [Mirewarden V3 exact-source archive](../../../art-experiments/mirewarden-ftk/live-validation-v3/README.md),
[Mireglass Croaker V1 archive](../../../art-experiments/mireglass-croaker/live-validation-v1/README.md),
[Sablevine Serpent V2 archive](../../../art-experiments/sablevine-basey-snake/live-validation-v2-scale055/README.md),
[Rustpetal V1 historical archive](../../../art-experiments/rustpetal-snapper/live-validation-v1/README.md) and [Rustpetal V2 canonical exact-source archive](../../../art-experiments/rustpetal-snapper/live-validation-v2/README.md),
[Belladusk V2 archive](../../../art-experiments/belladusk-pitcher/live-validation-v2/README.md) and [Belladusk V3 canonical exact-source archive](../../../art-experiments/belladusk-pitcher/live-validation-v3/README.md),
[Cinderbloom V3 archive](../../../art-experiments/cinderbloom-plant/live-validation-v3/README.md),
[Emberjaw V2 archive](../../../art-experiments/emberjaw-skull/live-validation-v2/README.md),
[Cinderwing V2 archive](../../../art-experiments/cinderwing-bat/live-validation-v2/README.md),
[Cinderwing V3 exact-source archive](../../../art-experiments/cinderwing-bat/live-validation-v3/README.md),
[Bronzewake V2 archive](../../../art-experiments/bronzewake-champion/live-validation-v2/README.md),
[Bronzewake V3 archive](../../../art-experiments/bronzewake-champion/live-validation-v3/README.md),
[Bronzewake V4 exact-source archive](../../../art-experiments/bronzewake-champion/live-validation-v4/README.md),
[Bronzewake V5 exact-body-source archive](../../../art-experiments/bronzewake-champion/live-validation-v5/README.md),
[Bronzewake V6 exact-boots-source archive](../../../art-experiments/bronzewake-champion/live-validation-v6/README.md),
[Bronzewake V7 exact-hair-source archive](../../../art-experiments/bronzewake-champion/live-validation-v7/README.md),
[Bronzehollow V1 archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v1/README.md),
[Bronzehollow V2 archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v2/README.md) and [Bronzehollow V4 canonical native-cap archive](../../../art-experiments/bronzehollow-sentinel/live-validation-v4/README.md),
the [Tamarind V2 passive-arrival archive](../../../art-experiments/tamarind-trickster/live-validation-v2/README.md),
the [Verdigrin V2 archive](../../../art-experiments/verdigrin-mimic/live-validation-v2/README.md), [Verdigrin V3 archive](../../../art-experiments/verdigrin-mimic/live-validation-v3/README.md), and [Verdigrin V4 exact-source archive](../../../art-experiments/verdigrin-mimic/live-validation-v4/README.md),
the [Sunspire Roc V2 combat archive](../../../art-experiments/sunspire-roc/live-validation-v2/README.md), [Sunspire Roc V3 portrait archive](../../../art-experiments/sunspire-roc/live-validation-v3/README.md), and [Sunspire Roc V4 canonical exact-source archive](../../../art-experiments/sunspire-roc/live-validation-v4/README.md),
the [Copperveil V2 archive](../../../art-experiments/copperveil-spider/live-validation-v2/README.md) and [Copperveil V3 exact-source archive](../../../art-experiments/copperveil-spider/live-validation-v3/README.md),
the [Tideglass V2 archive](../../../art-experiments/tideglass-crab/live-validation-v2/README.md) and [Tideglass V3 exact-source archive](../../../art-experiments/tideglass-crab/live-validation-v3/README.md),
the [Mossglass V4 archive](../../../art-experiments/mossglass-reliquary/caps-v3/live-validation-v4/README.md) and [Mossglass V5 exact-source archive](../../../art-experiments/mossglass-reliquary/live-validation-v5/README.md),
the [Vesper Eye V3 archive](../../../art-experiments/vesper-eye/live-validation-v3/README.md) and [Vesper Eye V4 exact-source archive](../../../art-experiments/vesper-eye/live-validation-v4/README.md),
the [Amberwake Dragon V2 archive](../../../art-experiments/amberwake-dragon/live-validation-v2/README.md) and [Amberwake Dragon V3 canonical fixture-assisted archive](../../../art-experiments/amberwake-dragon/live-validation-v3/README.md),
the [Emberglass Bee V2 archive](../../../art-experiments/emberglass-bee/live-validation-v2/README.md)
and [Emberglass Bee V3 archive](../../../art-experiments/emberglass-bee/live-validation-v3/README.md),
plus the [Emberglass Bee V4 exact-source archive](../../../art-experiments/emberglass-bee/live-validation-v4/README.md),
the [Resinmaw V2 archive](../../../art-experiments/resinmaw-bogling/live-validation-v2/README.md),
the [Duskquill V2 archive](../../../art-experiments/duskquill-raven/live-validation-v2/README.md) and [Duskquill V3 exact-source archive](../../../art-experiments/duskquill-raven/live-validation-v3/README.md),
the [Basilight V2 archive](../../../art-experiments/basilight-cockatrice/live-validation-v2/README.md) and [Basilight V3 exact-source archive](../../../art-experiments/basilight-cockatrice/live-validation-v3/README.md),
the [Lunacrest V3 archive](../../../art-experiments/lunacrest-clam/live-validation-v3/README.md),
the [Reefstrider V2 archive](../../../art-experiments/reefstrider-fish/live-validation-v2/README.md) and [Reefstrider V3 exact-source archive](../../../art-experiments/reefstrider-fish/live-validation-v3/README.md),
the [Gloamcap V2 archive](../../../art-experiments/gloamcap-imp/live-validation-v2/README.md) and [Gloamcap V3 exact-resource archive](../../../art-experiments/gloamcap-imp/live-validation-v3/README.md),
the [Thistlewick V2 archive](../../../art-experiments/thistlewick-hexer/live-validation-v2/README.md) and [Thistlewick V3 exact-source archive](../../../art-experiments/thistlewick-hexer/live-validation-v3/README.md),
the [Saffronspine pufferA V2 archive](../../../art-experiments/saffronspine-puffer/live-validation-v2/README.md),
the [Saffronspine pufferA V3 exact-source archive](../../../art-experiments/saffronspine-puffer/live-validation-v3/README.md),
and the [Saffronspine pufferB V2 archive](../../../art-experiments/saffronspine-puffer-b/live-validation-v2/README.md).
For a many-limbed controller that leaves a visible corpse, use Copperveil V3 as
the `spiderB / enSpiderB / renderer 121386` example. Review at least one settled
frame, the causal native attack frame, post-attack recovery, the causal
`Damaged` frame, post-hit recovery, the causal `Death` frame, a readable
airborne or transitional death pose, a settled corpse, and the terminal loot
frame. A corpse visible behind a native loot overlay supports the sampled
handoff only. Keep ordinary lethal damage and later corpse lifetime false or
out of scope unless separate evidence directly establishes them. Inspect each
leg chain for detached segments and stretching rather than treating an intact
central body as sufficient.

For a long-limbed biped that enters native physics death, use Reefstrider V3 as
the `fishA01 / enFishA / renderer 121695` example. Review the causal native
`Death` frame even when impact effects obscure the torso, then add readable
launch, descending, settled, and terminal-loot frames. Distinguish native
impact particles from authored loose geometry by checking the frozen piece
manifest and earlier frames. A connected corpse behind the loot panel supports
only the sampled handoff; retain ordinary-lethal and later-lifetime limits
unless separate evidence proves them.

For an articulated creature whose native death remains animator-driven, use
Basilight V3 as the `cockatriceC / enChicken / renderer 121484` example. Record
both the death clip and per-frame Animator and `m_DoRagdoll` state. An enabled
Animator with `m_DoRagdoll=false` supports a native animated fall and prone pose;
do not call it a physics ragdoll. Review the recoil, fall, settled animation
pose, and terminal loot handoff separately, and retain ordinary-lethal and later
corpse-lifetime limits unless direct evidence establishes them.

For a small flying creature whose native death uses physics, use Emberglass V4
as the `beeA / Monster Bee / renderer 121062` example. Preserve every accepted
ordinary attempt when the bounded retry policy first observes no HP loss. Keep
the zero-loss attempt separate from the later successful nonlethal hit, and use
the native trigger, clip readback, and visible game label only for what each
source directly records. Review wing, feeler, limb, abdomen, and tail continuity
through idle, attack, dodge, hit recoil, recovery, and the physics transition.
When `m_DoRagdoll=true`, verify the rigid bodies actually change from kinematic
to dynamic before calling the death a physics ragdoll. Record a readable falling
pose, first grounded pose, last visible active-renderer pose, and teardown
boundary. A grounded body through one sampled interval does not establish later
corpse lifetime or native cleanup causality. The shared 47-bone topology still
requires exact beeB and dragonflyB representatives; do not transfer beeA credit
to those sources.

For an articulated bird, [Sunspire Roc V4](../../../art-experiments/sunspire-roc/live-validation-v4/README.md)
combines a retained ordinary no-focus combat session with a separate constructed
native row-portrait session. It preserves `cidle_roc`, `attackProf_roc`, ordinary
HP 81 to 71 with `damageLight_roc`, later `attackCrit_roc`, animated
`deathHeavy_roc`, two native Collects, strict Ready, and the 328 by 280 portrait.
Its binding-only source generator and closed wing panels are a reusable `rocA`
example, not evidence for a different bird rig. V4 is also the reference for
repairing an older archive layout without replay: prove that the historical and
current selected profile objects, assets, source renderer, and motion renderer
are identical; preserve the old archives; review original pixels again; and
write a new immutable archive with every structured gate and integrity pin.
Catalog-wide hash drift alone is insufficient reason to replay a complete
trial. Any selected-profile or semantic route change requires a fresh run.

For an animator-driven plant, use
[Belladusk V3](../../../art-experiments/belladusk-pitcher/live-validation-v3/README.md)
as the exact `plantE / enJungleNibbler_A / renderer 121530` example. Preserve
the raw interval boundaries for `idle`, `attack1`, `hit1` and `death`, and select
review frames from inside those intervals. Record the ordinary HP transition
separately from the explicit `KillSingle` fixture. When every retained frame
reports `m_DoRagdoll=false` and zero rigidbodies, call the result animated death.
Do not infer collision, physics sleeping or corpse lifetime from a low death
pose. The route credits only plantE even when another plant looks similar.

For an animator-driven plant whose source renderer is removed during native
cleanup, use [Rustpetal V2](../../../art-experiments/rustpetal-snapper/live-validation-v2/README.md)
as the exact `plantD / enJungleNibbler_C / renderer 121537` example. Require
complete idle/native-attack and ordinary-hit captures first. After an exact
native `Death` trigger, retain a renderer-destroyed death prefix only when every
sample before the boundary keeps the selected mesh, renderer path, bone
signature, owner and CEL identity. Review the fall, a readable fallen pose, the
Victory handoff, and the final telemetry sample separately. If the last sample
marks the renderer inactive or not visible, a blurred body shape in that PNG
does not establish an active corpse. Record `m_DoRagdoll=false` and zero
rigidbodies as animated death, preserve the raw `ok:false` result, and leave the
full requested duration, cleanup causality and later corpse lifetime unproved.

For a small bird whose native behavior can remove it during a pass capture, use
[Duskquill V3](../../../art-experiments/duskquill-raven/live-validation-v3/README.md)
as the exact `crowC / enCrow / renderer 120964` example. Preserve the stopped
run, its partial raw capture, and the last active frame as rejected boundary
evidence. Do not accept a renderer-destroyed pass prefix as idle or attack
coverage and do not call native self-removal a binding failure without evidence
that the binding changed first. Start one fresh isolated process for the retry
and require complete captures there. In the successful process, review the full
wing span during flight and attack, the compact recovery pose, the damage
response, and the animated floor pose. `m_DoRagdoll=false` with zero rigid
bodies establishes an animator-driven `BirdDeath`, not a ragdoll. Keep sibling
bird rows, ordinary lethal damage, and lifetime after the sampled loot window
outside the exact-source result.

For an enemy whose native proficiency attacks, robs, and then removes the
full-health enemy after the hero passes, use [Thistlewick V3](../../../art-experiments/thistlewick-hexer/live-validation-v3/README.md)
as the exact `scourgeG / enScourgeLeprechaun / renderer 121222` example. The
complete terminal capture records `cidle_impUnarmed`,
`attackProf_leprechaun`, upright recovery, uniform translucency, and renderer
disable while HP remains 58. Preserve that sequence as native robbery/flee,
not death, and do not automatically replay it. Capture an ordinary nonlethal
hit before any pass that can remove the enemy, or use a separately retained
process with exact identity and hashes. Keep the ordinary HP transition,
explicit `KillSingle` death capture, and native loot/Ready progression in
separate fresh-process claims. `m_DoRagdoll=false` makes Thistlewick's sampled
death animated rather than a body ragdoll.

For a tall articulated humanoid with game-owned attachments, use
[Tidecrown Sovereign V2](../../../art-experiments/tidecrown-sea-king/live-validation-v2-canonical/README.md)
as the `seaKing/enSeaKing` profile. Its 60-bone generator uses only bone names
and inverse bind matrices, retains the whole palette, and builds the robe,
mantle, arms, fingers, armor, and crown along actual chains. `Knee_L` and
`Knee_R` remain palette entries but have no native surface weight, so it does
not fabricate a below-floor leg shell merely to occupy them. Keep native trident,
shield, breakable children, tentacles, ragdoll bodies, controller, and portrait
cache outside the GLB unless the selected replacement renderer owns them. The
V2 record pins the exact 1.0-scale binding and disabled material emission. It
preserves two Blocks followed by a fixture-assisted ordinary zero-focus
720-to-719 `Damaged` response, a 91-frame fixture-ragdoll prefix and strict
Ready. The fixture changes only the equipped native weapon skill augmentation
to FTK's native cap and leaves focus, RNG, weapon, damage, enemy stats, response
and animation authority native. Keep its balance limitation, giant-camera crop
and fixture-only lethal result explicit. Reuse the authoring and archive shape
for this exact renderer, then repeat all live checks for another humanoid,
accessory layout, or controller.
