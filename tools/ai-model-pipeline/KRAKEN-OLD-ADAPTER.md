# Explicit old Kraken adapter mechanics fixture

`kraken-adapter-fixture` is a helper-only diagnostic. It does not register an
adapter, add a component to a live/native avatar, or enable old Kraken support.
The operation accepts exactly `id`, `session`, `op`, `method`, `clip`, `times`.
Use `method: "clip-playable"`, one of `krakenIdle`, `krakenAttack`, `krakenDamage`,
`krakenDisappear`, and 1..32 explicit seconds within the native clip length.
Appearance and additional blend/state arguments are rejected before allocation.

The owned modern and old surfaces contain copied transforms and one controller-free
Animator each. Their immutable rest snapshots precede graph initialization. The
existing native Avatar manual playable samples both surfaces after a complete
rest reset. The adapter snapshots the independent modern models and actual old
shared Root_M before any writes. Desired old model matrices are
`modernAnimated * inverse(modernRest) * oldRest`. The joint1 local uses the inverse
actual old Root_M; descendants use their desired parents. Only the five exact old
target locals are written. The shared Root_M stays read-only. Old jaw is rest-local
for these four main clips only.

Every matrix must be finite, nonsingular, positive determinant and shear-free
within 1e-5, and must reconstruct from Unity TRS within 1e-5. All outputs pass
preflight before committing. Commit/readback failure restores all five previous
locals. Readback composes actual Unity local TRS along the checked ancestry to the owned
model top, excluding that top's arbitrary placement. The original top placement
is retained and checked. World-inverse times world matrices remain a separate
diagnostic: the old native prefab's Z132.820007 causes float cancellation at a
1.525879e-5 world ULP. The first live fixture failed a 1e-5 target readback gate
with world-product arithmetic after a neutral error of 7.808208e-6; it produced
zero accepted frames and cleaned up successfully. Its exact failing pose was not
recorded. The corrected path retains the 1e-5 gate and reports numeric failing
matrices. The subsequent v2 live run passed the bounded mechanics checks below.

Unity local-chain readback, repeated inputs, root/target identities, neutral recovery,
injected commit rollback and invalid TRS rejection are recorded. Ready dungeon,
level, room, both encounter objects and session are pinned throughout. Graphs
are disposed before their owned roots and deferred destruction is observed.

## Acceptance gates

1. Build the helper against the isolated game's Managed assemblies, zero errors.
   Pin the actual deployment receipt, loaded helper/framework identities and
   session before the parent runs the operation. No build alone is runtime proof.
2. Run all four main clips at the exact seconds selected by independently validated
   native modern-reference samples. Reusing those timestamps does not by itself
   establish a reference chain across a new process. Require complete successful fixture output, unchanged
   Ready pin, neutral/negative checks, every sampled adapter check and cleanup.
3. Independently compare actual Unity target locals and models against source-asset
   algebra, and the fixture source samples against same-process original native
   playable samples with matching clip/prefab/controller IDs and exact times:

   ```sh
   scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_adapter_fixture.py \
     --assets scratch/GAME/FTK.app/Contents/Resources/Data/resources.assets \
     --sample scratch/ADAPTER_RESULT.json --request scratch/ADAPTER_REQUEST.json \
     --reference scratch/ORIGINAL_NATIVE_SAMPLE.json --output scratch/ADAPTER_CHECK.json
   ```

   Require `owned_adapter_mechanics_match` and maximum error <=1e-5. The referenced
   original sample must also have its separate successful native-controller
   comparison manifest, or the explicit cross-process bridge below. The adapter
   verifier still requires its reference sample to be from the adapter's own
   process. Neither a same-process sample nor historical timestamps alone are
   native-controller proof.
4. Before any skinned-avatar integration, separately test old appearance and its
   native jaw animation and an actual mixer graph transition. Serialized native
   appearance entry offset is about .276341587; appearance-to-idle uses exit time
   about .9000006 and duration .1, with WriteDefaults enabled. Exact source assets
   and controller audit remain authoritative. A LateUpdate state-name toggle or
   interpolation of independently completed retarget poses does not establish
   native crossfade behavior. Capture real transition influences and clock behavior.
5. Only then consider an explicitly opted-in owned old skinned-avatar fixture.
   Preserve original palette ordering, IBMs, event routing on the native CEL,
   bounds, root motion and cleanup. Verify appearance, idle, attack, damage,
   disappear and transitions numerically and visually before a public API design.

The existing pure modern references pass for attack/idle/damage. The disappear
reference is an explicitly reported failed-capture prefix terminated when its
renderer was destroyed. Its retained stable frames matched; this does not claim
complete death coverage. This diagnostic contains no renderer, so original skin
deformation, visual quality and native event semantics remain untested.

## Explicit cross-process native pose bridge

Use `bridge_kraken_native_samples.py` only when a fresh original single-clip
sampler result is being compared with a historical sampler result that already
passed the native-controller comparator. The adapter verifier's same-process
rule is unchanged. The chain is historical native capture -> historical original
sample -> fresh original sample -> fresh adapter result.

Supply an explicit manifest with this shape; every path is an existing exact
file and every SHA256 is calculated from that file's bytes:

```json
{
  "schema": "kraken-native-cross-process-bridge-v1",
  "evidence": {
    "assets": {"path": "/absolute/fresh-game/resources.assets", "sha256": "..."},
    "historicalManifest": {"path": "/absolute/historical-manifest.json", "sha256": "..."},
    "historicalComparison": {"path": "/absolute/historical-comparison.json", "sha256": "..."},
    "historicalSample": {"path": "/absolute/historical-original-sample.json", "sha256": "..."},
    "freshSample": {"path": "/absolute/fresh-original-sample.json", "sha256": "..."},
    "freshRequest": {"path": "/absolute/fresh-original-request.json", "sha256": "..."}
  }
}
```

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/bridge_kraken_native_samples.py \
  --manifest scratch/BRIDGE_MANIFEST.json --output scratch/BRIDGE_COMPARISON.json
```

Require `cross_process_pose_bridge_match`. The bridge verifies hashes, replays
the historical native comparator's capture/profile/controller/binary checks,
requires unchanged source assets and exact clip names/durations/ordered times,
and compares every modern and old local matrix and composed prefab-root matrix
within 1e-5. Native object instance IDs differ between processes and are not
claimed identical. Both sample surfaces must retain the explicit manual graph
and disabled gameplay/event settings. The fresh process deployment receipt and
actual loaded identities remain separately pinned by the runtime operator.

The historical disappear result retains `prefix_reference_match` and its exact
failed-capture boundary in the bridge report. A matching bridge neither expands
that interval nor proves appearance, transitions or native event behavior.

## Verified owned mechanics, v2

Session `a1eba409646e41f2b0784d751865866e`, helper SHA256
`434b762fc970612c3be9252c7525c9957d4a852c87fbc4f326001d50cf89c5f6`.
Evidence lives in ignored `scratch/kraken-adapter-live-v2/`. All four reports
have status `owned_adapter_mechanics_match`; every corresponding cross-process
bridge has status `cross_process_pose_bridge_match` with maximum error exactly
0.0. Historical native references were revalidated by the bridge.

| Clip | Frames | Maximum independent matrix error | Verification report SHA256 |
|---|---:|---:|---|
| krakenAttack | 32 | 2.397957628e-06 | `d00a2b04c91d8e6c911dcf17514b168b0f53c54b7007df0cf7fde0b43e8b53d6` |
| krakenIdle | 32 | 1.614042272e-06 | `3ab370cfb1c3bf529ad2fbeb889958c453d30f52453ab4448094258065375bc7` |
| krakenDamage | 6 | 1.394595145e-06 | `4c28436e41aa6696ac4a4b0a12fd5fca519d4461c99f0d6efee57e01c045df08` |
| krakenDisappear | 27 | 2.622526809e-06 | `617a6155fa5ef4e7e9c9809167c8e5fb51315477c442b78cd642a4866cb9d0a0` |

Each fixture retained the same Ready context, preserved the shared root and
target identities, passed repeat/neutral/negative checks, and disposed both
graphs before destroying both owned roots. Neutral local-chain readback error
was `9.5367431640625e-7`, compared with the failed v1 world-product neutral error
of `7.808208465576172e-6`. The disappear bridge retains the historical partial
renderer-destroyed capture boundary. These are actual owned Unity transform
mechanics with independently checked numerical outputs. They do not establish
a skinned old-avatar integration, appearance/WriteDefaults crossfades, native
events, or original model visual quality.

The next gate is an isolated native-controller graph observation fixture,
described in `KRAKEN-CONTROLLER-FIXTURE.md`. It will not apply the adapter or
invent a blend policy.
