# All raw rig profiles

The CEL catalog is a character-ancestor selection, not the complete set of
skinned renderers. On the inspected FTK installation, `inventory_skeletons.py`
found 750 raw `SkinnedMeshRenderer` objects and 384 distinct valid bind-pose plus
topology fingerprints. The CEL selection contains 467 renderers and 230 profiles.
The other 283 renderers consist of 253 renderers on 154 additional profiles,
26 renderers sharing an existing CEL fingerprint, and four invalid palettes.
Two CEL renderers also lack meshes. All six remain in the report; they are not
silently counted as successful profiles.

These are observed installation counts, not universal version-independent
constants. Fingerprint equivalence does not mean identical meshes, controllers,
owners, or live compatibility. The denominator remains 384 valid raw profiles
plus six explicit renderer failures, even when a workflow selects only 230 CEL
profiles or only 154 additional profiles.

## Reproduce locally

Use the Python environment and dependencies described in [README.md](README.md).
Set `FTK_DATA` to your own installed game's `Data` directory and `PYTHON` to the
Python executable with those dependencies. `ilspycmd` must be available, or pass
its path with `--ilspy`. The managed assembly, resources file, and external
serialized asset files must come from the same installation. UnityPy resolves
external references through each owning asset file's pointer table.

Run from the repository root. Keep all generated extraction, game-table metadata,
and audit files in the gitignored `scratch/` directory:

```sh
"$PYTHON" tools/ai-model-pipeline/inventory_skeletons.py \
  --assets "$FTK_DATA/resources.assets" \
  --output scratch/skeleton-inventory-reproducible.json
"$PYTHON" tools/ai-model-pipeline/map_enemy_rigs.py \
  --resources "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --output scratch/enemy-rig-mapping-reproducible.json
"$PYTHON" tools/ai-model-pipeline/classify_rig_candidates.py \
  --resources "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --enemy-mapping scratch/enemy-rig-mapping-reproducible.json \
  --output scratch/rig-candidate-classification.json
```

Then reconcile all
raw renderers, including apparel that is stitched into player avatars at runtime:

```sh
"$PYTHON" tools/ai-model-pipeline/classify_raw_rig_profiles.py \
  --assets "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --classification scratch/rig-candidate-classification.json \
  --output scratch/raw-rig-ownership.json

"$PYTHON" tools/ai-model-pipeline/audit_raw_profiles.py \
  --assets "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --output scratch/all-raw-rig-audit \
  --extra-only --selection-only

"$PYTHON" tools/ai-model-pipeline/audit_raw_profiles.py \
  --assets "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --output scratch/all-raw-rig-audit \
  --extra-only
```

Omit `--extra-only` to audit all 384 valid raw profiles. `--selection-only` checks
the source hash and prints selected representative IDs and excluded renderer
metadata without extracting geometry. Full audits write one native extraction
and roundtrip GLB per profile, `audit.json`, and `raw-profile-selection.json`.
Any failed roundtrip makes the command exit nonzero. Audit output must be
inside a gitignored directory. Do not commit native geometry or game DLLs.

Optionally pass `--audit scratch/all-raw-rig-audit/audit.json` to the classifier.
It records the report's digest and summary after checking its source hash. This
is evidence for the reported selection only; the tool does not upgrade ownership
or animation claims on the strength of an offline roundtrip.

## Ownership evidence and remaining uncertainty

The classifier resolves actual `FTK_skinset` avatar, armor, helmet, backpack, and
boot pointers, plus native `FTK_items` wearable pointers and object types. It
records ancestor component identities and exact database row/field references.
Names are retained for navigation, never used to establish ownership. Native
item enum/header counts, unambiguous row recognition, skinset row offsets/header
counts, matching CEL fingerprint sets, and resource hashes are checked; malformed
or mismatched input fails instead of silently dropping rows. Database and managed
assembly hashes are recorded for provenance. The binary field-prefix recognizer
is specific to the inspected FTK schema; another build requires source verification.

The inspected additional 154 profiles divide into 134 with apparel evidence,
10 with noncharacter component evidence, and 10 with no positive ownership
proof. At renderer level, 203 of the 253 have apparel evidence, 25 have boat,
airship, or dungeon-encounter component evidence, and 25 remain unknown. A
component reference alone does not establish a live spawn path or public API
support. Unknown objects are not declared unused or portraits based on names.

The six excluded renderer IDs are 121018 and 121680 (missing mesh pointers), and
121042, 121196, 121232, 121267 (19 palette bones versus 20 bind poses). They need
separate source repair or an explicit unsupported-geometry decision before they
can enter a valid-profile audit. Their absence from the valid fingerprint set
must never be presented as completed coverage.

All 154 additional profile representatives passed the recorded native offline
roundtrip audit. That verifies extraction/export contracts only. Original probes,
live deformation, equipment rebuilds, renderer lifetimes, and visual acceptance
are separate evidence. Default apparel and alternate equipped apparel require
conditional assembly-aware assignments; a body-only player profile does not
replace stock clothing merely because its body rig was audited.

The subsequent additional-profile calibration exercise also recorded 154/154
original probe validations and 154/154 Blender save/reopen/export roundtrips,
using weighted probes, no connections, and radius scale 6. Those are separate
from the native roundtrip: none of these 154 additional profiles had been
injected live at that checkpoint. The local Blender report is
`scratch/additional-blender-bridge-v1/bridge-audit.json`, SHA-256
`7f32c2482cd6f7a6fcc5119f8f4066c457a64ade6e136c05e40e90a0b1cbc360`.
The native additional-profile audit SHA-256 is
`fb9ebd7d874f9b3f82ef15a026a1e4f3dd4053f3def53330c81267092c6d1457`.
A later live acceptance report must add its own profile identities and evidence;
it cannot inherit live status from either offline report.
