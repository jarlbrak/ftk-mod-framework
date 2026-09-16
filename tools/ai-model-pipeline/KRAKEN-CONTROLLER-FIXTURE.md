# Proposed native Kraken controller graph observation fixture

The helper prototype is implemented and builds against the shipped Unity API.
The source review and first appearance/repeat observations are complete. Other
scenarios and adapter integration remain separate gates; this is not public rig support.
The old five-bone adapter has passed the four isolated main-clip mechanics gates.
Those results do not establish mixed appearance/main bindings or WriteDefaults.

## Owned surfaces and controls

Use a new explicit helper operation, `kraken-controller-fixture`, with only a
fixed scenario name. The supported scenarios are `appear`, `damaged`,
`damaged-heavy`, `death`, and `death-light`. Each maps to exactly one known native
trigger after an initial stable IDLE interval. Reject arbitrary state, trigger,
clip, seek, clock, speed and transition arguments. Attack states are excluded.

Create independent modern and old copies using the audited transform-only copy
routine, preserving each native prefab's local rest values and top placement.
Add exactly one Animator with its native Avatar to each owned root. Attach the
actual native `krakenHeadController` to an AnimatorControllerPlayable in a manual
PlayableGraph with one AnimationPlayableOutput. Keep the Animator's own controller
null, fireEvents false, applyRootMotion false and culling AlwaysAnimate. The graph
owns evaluation; no CEL, gameplay component, native controller clone/edit, or
adapter writes are involved. Do not reset bones between consecutive graph steps:
that would erase the WriteDefaults/transition behavior being measured.

Before graph creation, the fixture pins resources.assets SHA256
`e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117`
and loaded game assembly file SHA256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`,
records the loaded module identity, and requires exact native controller sharing
on the two source prefabs. Runtime overrides are rejected. The independent
`kraken-graph-whitelist-proof.json` confirms default IDLE and all 21 trigger
parameters default false, with each permitted single trigger reaching only its
requested state and IDLE (or terminal death). The three attack states
have CombatAction/AttackStart StateMachineBehaviours whose null CEL checks still
dereference CEL, so excluding their reachability is a precondition. Checking a
state only after evaluation would not prevent those callbacks. `kraken-behaviour-lifecycle-audit.json` resolves all six references to
CombatAction/AttackStart directly deriving from StateMachineBehaviour. Their
constructors only call the base constructor; neither type has static constructors,
fields, creation/enable/disable/destroy callbacks, or machine enter/exit callbacks.
All six references belong to the three attack-state ranges, with no controller or
machine attachment. The native ScriptableObject creation boundary stays opaque.
A temporary error/exception/assert log observer covers creation through graph
disposal and both deferred target-destruction frames, and is removed on cancellation.
Unexpected state or engine error evidence fails the run; it is not an accepted diagnostic side effect.

Initialize only after activation, using the existing verified Rebind/Play/Evaluate
sequence. Require initialized Animators and default IDLE. Use a fixed
1/60-second manual step and four-second horizon per independently created
pair. Trigger once after 15 steps of IDLE. Preserve exact step count and graph
elapsed time, recording actual Unity frame/time separately. The horizon and
sampling rate must be justified against the observed transitions; a completed
coroutine alone is not coverage.

## Record actual graph output

For each surface and step, record controller playable current and next state
hashes, normalized clocks, clip names/IDs/weights, IsInTransition, transition hash,
normalized time and duration fields supported by the installed Unity API. Label
current and next clip weights exactly as returned by the API; they are not
assumed to be combined output weights or inferred from transition time.

Record all modern driver and old target local position/quaternion/scale values,
Root_M local values, and composed prefab-root matrices using the verified local
chain routine. Retain directly animated old jaw measurements. Record rest values,
source/deployment identities, allowed scenario, trigger step, graph configuration,
initialization, same Ready dungeon/level/room/session and cleanup evidence.
Graph disposal precedes target destruction. No helper may attach a controller to
a live native CEL or mutate shared source transforms or materials.

## Acceptance gates

1. Review source reachability and installed AnimatorControllerPlayable APIs before
   implementation; build against the isolated Unity2017 assemblies.
2. Prove default IDLE followed by actual requested-state entry. Appearance must
   expose the native destination offset, about .276341587, then the actual
   appearance-to-IDLE transition (exit about .900000632, normalized duration
   about .099999391). Record pretransition, mixed and completed destination poses.
3. Measure old jaw motion in appearance and its WriteDefaults behavior as IDLE
   becomes active, alongside modern driver motion. Preserve the data even if the
   two native hierarchies respond differently. No adapter interpretation is added.
4. Measure damage entry/return and death entry with the actual native graph.
   Require expected states and actual transitional samples; no attack/event claims.
5. Repeat a scenario on new owned surfaces and compare equal-step poses/clocks
   and transitions within explicit tolerances before using the result to design
   an adapter blend policy. Then review how independent modern retarget output
   and native old appearance can coexist under actual graph influences.

The native graph source has WriteDefaults enabled in all states. Transition
lengths are normalized source-state durations, not seconds. Serialized graph
metadata is in ignored `scratch/kraken-controller-transitions.json`; event and
state-machine analysis is in `scratch/kraken-transition-gate-notes.md`. Exact
source metadata takes precedence over rounded values in this proposal.


## Command and run sequence

```json
{"id":"UNIQUE_ID","session":"CURRENT_SESSION","op":"kraken-controller-fixture","scenario":"appear"}
```

Those four fields are required for graph observation. The optional fifth field
`"endpointPolicy":"main-appearance-local-v1"` enables the isolated
[endpoint policy diagnostic](runtime-test/KRAKEN-CONTROLLER-FIXTURE.md#optional-endpoint-policy-diagnostic-not-yet-live-verified),
which uses distinct result provenance and additional owned sampling/output surfaces.
Unknown fields or endpoint-policy values are rejected. A complete result contains 241 frames,
including step zero, with the one trigger sent before evaluating step 16.
Each scenario creates fresh independent surfaces. Run `appear` first, then a
second fresh `appear` to measure repeatability. Inspect real entry, mixed, and
IDLE-return frames before running `damaged`, `damaged-heavy`, `death`, and
`death-light`. Unknown scenarios and extra trigger/state/clock fields must reject
without allocation. The operation requires the exact native Ready slot before,
during and after evaluation. No code in this fixture advances the dungeon.


## Independent repeat verifier and first appearance observation

`verify_kraken_controller_repeat.py` requires an explicit manifest with schema
`kraken-controller-repeat-v1` and exactly six named evidence entries: `first`,
`firstRequest`, `repeat`, `repeatRequest`, `assets`, `gameAssembly`. Each entry is
`{"path":"/absolute/file","sha256":"actual-file-hash"}`. It hashes every file,
checks requests/session/source identity/Ready/cleanup, then compares equal graph
steps, state and transition clocks, raw clip arrays and weights, every local
position/quaternion/scale/matrix, and composed root-space matrices within 1e-5.
It also independently reconstructs each run's TRS and hierarchy matrices.
Owned instance IDs must remain stable within each run but are excluded from pose
comparison. Absolute Unity frames and wall-clock times differ between runs.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_controller_repeat.py \
  --manifest scratch/kraken-controller-live-v1/appear-repeat-manifest.json \
  --output scratch/kraken-controller-live-v1/appear-repeat-verification.json
```

The first two appearance runs in session `16074358d6ce4f469d99e13bb3b58211`
completed 241 frames each with no engine errors, all cleanup checks and the same
Ready slot. Helper SHA256 was
`7c277b7d1596f2b7c6eff9c2f91d2878bba11041699c8c6a6abe99c293b05ec3`.
The independent report returns `controller_repeat_match`: repeat-to-repeat
maximum error exactly 0.0; independent TRS/model reconstruction maximum error
`1.73639996958741e-6`.

Both owned surfaces observed appearance entry transition steps16..40, first
next-state phase `.27634158730506897`, then settled appearance at41. The return
transition occupied105..119, beginning at appearance phase `.9009037613868713`,
with IDLE settled at120. `nextClips` remained nonempty at41 and120 although
`inTransition` was false, then cleared on the following steps. Base `layerWeight`
was0.0 while active clip weights could be1.0. These raw API observations are
preserved; the verifier does not reinterpret them as different graph states or
require the base-layer weight to be1.

Old jaw local-matrix variation from the first frame reached `.7205031961202621`,
with maximum adjacent-step difference `.03685528039932251`; its final local matrix
matched the original rest matrix exactly. Modern neck local variation was only
`1.4901161193847656e-8`, while modern head/topHead/Root_M did vary. These are
observations of this native graph scenario, not an anatomical acceptance rule,
a derived blending algorithm, or proof that arbitrary transitions are correct.
The report's coverage section remains descriptive even if a future repeated run
fails to exercise its intended transition.
