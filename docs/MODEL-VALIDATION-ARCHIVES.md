# Model-validation evidence archives

Use this workflow after a fresh isolated model trial has completed and a root
review has named the exact capture frames it inspected. It freezes evidence for
one exact native source assignment. It does not make a source assignment,
motion capture, ordinary damage observation, or visual conclusion reusable for
a sibling renderer, another resource-prefab route, or a different asset hash.

Enemy combat trials use a plan with schema
`ftkmf.model-validation-archive-plan.v1`. Native player-preview trials use
`ftkmf.player-model-validation-archive-plan.v1`. Both plans are intentionally
explicit: they record the reviewer's source-specific identity expectations and
capture the result of a completed test. Neither launches the game or chooses
art.

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/<package>/live-validation-vN-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

The builder refuses an existing destination. If an input is invalid after its
temporary stage has begun, it leaves that stage in place for inspection instead
of replacing or deleting any evidence.

## Enemy combat plan fields

| Field | Purpose |
| --- | --- |
| `output` | New archive directory below the repository root. It must not exist. |
| `caseResult` | Fresh `exercise_case.py` result from the isolated game. |
| `visualReview` | Root-reviewed JSON with the same session, exact binding, and selected frame paths and hashes. |
| `assetFiles` | Original GLB and authored texture files whose current hashes are pinned. |
| `metadata` | Profile, manifest, stage receipt, preflight, and other source documents to preserve as gzip-lossless metadata. Set `pinAs` when a source document needs a direct pin in `validation.json`. |
| `captures` | One entry for every accepted action in the case result. `label` names its explicit evidence, `scope` states the observation boundary, and `frameIdentity` gives each renderer field that must remain unchanged in every retained frame. |
| `validation` | Reviewed source identity and conclusions that cannot be derived mechanically, including status, revision, display name, binding, limits, and any observed gameplay records. |

Use a path relative to the repository for every plan path. The builder accepts
an absolute input path only when it resolves inside the same repository. It
rejects game DLLs, Unity resource payloads, and other native game bundles.

Here is a compact enemy plan. The actual `binding`, review text, source IDs,
and capture scope must come from the fresh exact trial.

```json
{
  "schema": "ftkmf.model-validation-archive-plan.v1",
  "output": "art-experiments/example/live-validation-v3",
  "caseResult": "scratch/example-game/model-test-output/case-<id>/case-result.json",
  "visualReview": "scratch/example-root-visual-review-v3.json",
  "assetFiles": [
    "art-experiments/example/example.glb",
    "art-experiments/example/example-palette.png"
  ],
  "metadata": [
    {
      "path": "art-experiments/example/runtime-profile.json",
      "pinAs": "runtimeProfile"
    },
    {
      "path": "scratch/example-v3-stage/receipt.json",
      "pinAs": "stageReceipt"
    },
    "art-experiments/example/manifest.json"
  ],
  "captures": {
    "pass": {
      "label": "idle-and-native-attack",
      "scope": "Settled idle and exact native attack capture.",
      "frameIdentity": {
        "celRelativeRendererPath": "exactRendererPath",
        "mesh": "ftkmf_glb_example.glb",
        "boneSignature": "<observed exact signature>"
      }
    },
    "attack": {
      "label": "ordinary-attack-and-nonlethal-hit",
      "scope": "Same-target ordinary no-focus player attack and native Damaged observation.",
      "frameIdentity": {
        "celRelativeRendererPath": "exactRendererPath",
        "mesh": "ftkmf_glb_example.glb",
        "boneSignature": "<observed exact signature>"
      }
    },
    "kill-fixture": {
      "label": "explicit-fixture-death",
      "scope": "Explicit KillSingle fixture only, not ordinary lethal damage.",
      "frameIdentity": {
        "celRelativeRendererPath": "exactRendererPath",
        "mesh": "ftkmf_glb_example.glb",
        "boneSignature": "<observed exact signature>"
      }
    }
  },
  "validation": {
    "status": "reviewed_exact_source_pending_any_remaining_limits",
    "revision": "V3",
    "displayName": "Example model",
    "sourceKind": "native_enemy_row",
    "nativeChassis": "exampleEnemy",
    "rendererPaths": ["exactRendererPath"],
    "sourceRendererIds": [123456],
    "binding": {
      "rendererPath": "exactRendererPath",
      "mesh": "ftkmf_glb_example.glb",
      "boneSignature": "<observed exact signature>"
    },
    "ordinaryLethal": false,
    "limits": [
      "State the specific observations that remain outside this archive."
    ]
  }
}
```

The builder derives the session, enemy key, catalog hash, capture metadata,
source and capture image pins, selected copies, asset pins, lossless metadata
mappings, and presentation videos. It derives an ordinary hit record only from
the measured `attack` action's numeric HP outcome. For older case results whose
`hpOutcome` was overwritten after waiting for a later player turn, it prefers
the exact target HP in the action's own `before` and post-capture `after` states,
and preserves any differing later Ready-state HP as `laterReadyObservedHp`.
This prevents intervening turns or status ticks from becoming attack damage.
It derives an explicit
fixture record only from an accepted `kill-fixture` action. It copies
`finalReady` from the case result unless the plan supplies a matching subset.
It rejects a plan whose binding disagrees with its root review or whose retained
frame identity changes during a capture.

When a bounded native attack produced more than one accepted attempt, name each
capture key `attack-attempt-1`, `attack-attempt-2`, and so on, using the exact
positive `attempt` values recorded in the case. Preserve every attempt's raw
capture. The builder derives `ordinaryHit` only from the one accepted attempt
with measured `nonlethal_hp_loss`; an earlier no-loss attempt remains a
separate observation instead of being relabeled as damage.

## Player native-preview plan

Use the player builder when the observed route is a game-owned Party Select
avatar, rather than an enemy `exercise_case.py` combat sequence:

```sh
python3 tools/ai-model-pipeline/archive_player_model_validation.py \
  art-experiments/<package>/live-validation-vN-player-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

The player plan replaces `caseResult` with an explicit `session` and a capture
array. Every capture names the raw fixed-step preview JSON, its scope, the
fields that must stay constant in every frame, and any native clips that must
be present. Its validation object must include the exact player `profile` and
an `avatarOwners.preview.observedAvatars` count. That makes a pending native
preview distinct from an observed game-owned avatar.

```json
{
  "schema": "ftkmf.player-model-validation-archive-plan.v1",
  "output": "art-experiments/example-player/live-validation-v2",
  "session": "<fresh-native-preview-session>",
  "visualReview": "scratch/example-player-root-review-v2.json",
  "assetFiles": [
    "art-experiments/example-player/body.glb",
    "art-experiments/example-player/palette.png"
  ],
  "metadata": [
    {
      "path": "art-experiments/example-player/runtime-profile.json",
      "pinAs": "runtimeProfile"
    }
  ],
  "captures": [
    {
      "label": "native-preview-idle",
      "rawCapture": "scratch/example-game/model-test-output/<capture>.json",
      "scope": "Game-owned Party Select avatar idle capture.",
      "requiredClips": ["standardIdle_handsDown"],
      "frameIdentity": {
        "ownerKind": "player-preview",
        "ownerInstanceId": 123,
        "celInstanceId": 456,
        "instanceId": 789,
        "celRelativeRendererPath": "playerBody",
        "mesh": "ftkmf_glb_body.glb",
        "boneSignature": "<observed exact signature>",
        "active": true,
        "enabled": true,
        "isVisible": true
      }
    }
  ],
  "validation": {
    "status": "reviewed_native_preview_pending_any_remaining_limits",
    "revision": "V2",
    "displayName": "Example player",
    "profile": {
      "key": "ftkmf_modeltest_player_example",
      "baseClass": "blacksmith",
      "skinset": "blacksmith_Female",
      "defaultSkinType": "Female",
      "renderers": [{"rendererPath": "playerBody"}]
    },
    "binding": {
      "rendererPath": "playerBody",
      "mesh": "ftkmf_glb_body.glb",
      "boneSignature": "<observed exact signature>"
    },
    "avatarOwners": {
      "preview": {"observedAvatars": 1}
    },
    "limits": [
      "State the observations outside this native preview archive."
    ]
  }
}
```

The player builder does not transfer preview evidence to combat, equipment
branches, overworld, death, portraits, resource teardown, another skinset, or
another class. Record those as their own fresh observations and archives.

The generic verifier checks archive artifact integrity, not the validity of the
review conclusion. After a passing integrity check, add the exact
`validation.json` path and SHA-256 to `docs/model-runtime-validation.json`,
then regenerate the original-model index, validation-gate ledger,
archive-integrity ledger, topology coverage, package readiness, and
candidate-coverage reports in their documented order. Candidate coverage must
read the current archive-integrity ledger so an old or altered immutable artifact
cannot receive canonical-route credit. Finally regenerate the validation
execution queue so its selected profile document and renderer target reflect the
new candidate report. The queue is planning data only; its static profile match
does not accept the fresh archive's behavior or visual review.

A `run_execution_queue_route.py` record can point to the exact fresh binding
stage and exercise result, but it is not an archive. Preserve its referenced
case artifacts, complete the root visual review, build a new asset-local
immutable archive, and verify that archive before adding any runtime-index or
coverage reference.

Start the review from a non-overwriting template that pins the runner-selected
source PNGs and observed renderer identity:

```sh
python3 tools/ai-model-pipeline/prepare_model_visual_review.py \
  scratch/my-isolated-game/model-test-output/case-<id>/case-result.json \
  --output scratch/my-route-root-review.json
```

Inspect every listed original image, replace the pending observations, and set
`reviewStatus` to a reviewed value. A pending template is rejected by the
archive-plan generator.

After the review, create a non-overwriting enemy archive plan from that exact
record instead of copying an older route's plan by hand:

```sh
python3 tools/ai-model-pipeline/make_execution_queue_archive_plan.py \
  --record scratch/my-route-run.json \
  --visual-review scratch/my-route-root-review.json \
  --plan-output art-experiments/my-model/live-validation-vN-plan.json \
  --archive-output art-experiments/my-model/live-validation-vN \
  --revision VN \
  --limit 'State the observations outside this archive.'
```

It checks the runner's catalog/profile/asset pins and the review session before
writing the plan. Then build and independently verify the archive with the two
commands at the top of this guide. The generator does not select frames or make
a review finding.
