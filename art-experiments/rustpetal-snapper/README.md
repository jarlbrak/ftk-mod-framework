# Rustpetal Snapper - original plantD bind candidate

A broad copper seedpod mouth with two split upper leaf blades, a pale biting rim, deep-blue throat and paired olive side leaves. The flattened head and tall split foliage distinguish this candidate from Belladusk's round purple pitcher. Native behavior remains unchanged; the design adds no mechanics or copied native surface.

Exact plantD selection: renderer121537 enJungleNibbler_C, CEL138791 enPlantD,43-bone palette. Preserve native1.7 root scale through visual factor1. The30 native-positive bones are used;13 native-zero-weight bones are retained unused. In particular Leaf5 is positive here, unlike plantE. Additional crown bones and mirrored chains are not invented animation targets.

Belladusk's original surface functions are useful authoring code, but its37-bone asset is not a compatible replacement. This generator reads the separately verified D bind reference and exports all43 exact IBMs. The distinct wider head uses continuous Head/Jaw outer tissue and an intentionally open inner mouth funnel. Positive-volume checks apply only to closed pieces, not the open sleeve/interior. Native bind-bounds, source-only regeneration and direct/reopened binary checks pass for this initial candidate.

build_geometry.py creates original surfaces; verify_original_geometry.py restricts generator reads to bone names/bindposes. audit_surfaces.py checks closed-piece orientation and native bind bounds. build_blender.py creates the editable scene and independently reopens/exports before studio rendering. Run Python scripts using scratch/model-venv/bin/python and the Blender script using Blender --background --python.

The initial bind-pose candidate is preserved as authoring history. The current package and exact plantD profile are frozen and live validated. No D/G sharing claim is implied. Frozen Belladusk and Saffronspine assets are untouched.

## Native D pose fitting

The initial bind candidate is preserved in offline-history/initial-bind. No geometry revision was needed for this first360-pose study. Actual D attack3 is sampled46/48/50/53/55; hit1 is sampled27/29/30/31, with later attack1 at75. The full body sheets retain the stalk, crown and lip attachment; a separate per-frame head closeup examines recoil. Both crown blades and hood use exactly Head weights, independently checked, so different skin transforms cannot detach them. This is not an all-angle culling or intersection proof.

Pass maximum edge is Leaf5 at52, .105541 to.191122 (1.81088×); hit maximum is lower lip at74, .039859 to.085192 (2.13731×); death maximum is Leaf6 at40, .060641 to.128667 (2.12180×). Pass/hit normalize Root_M for anatomy, while death retains renderer-local travel. Death60/119 shows analytical collapse, not measured floor contact. All three reports cover120 frames and pin the exact GLB. Source diagnostic was explicit fixture death, not ordinary lethal damage. Root selected-view review is archived; final offline package is approved for future live testing, not live acceptance.

## Frozen offline package

manifest.json pins the original generator, editable scene, direct and reopened exports, scoped surface checks, all native pose studies and preserved initial state. runtime-profile.json selects plantD only, native1.7 scale via factor1, and explicit emission opt-out. The offline package does not claim material, portrait, indirect/ordinary lethal death or final resource teardown.

## Live validation history

The historical [catalog-411 V1 archive](live-validation-v1/README.md) records session `39eec55ed9ac43fc90c2fc1f1d756c7d`: the Rustpetal mesh bound to `plantD` / `enJungleNibbler_C` (renderer 121537) at visual scale 1.0 on one native enemy owner. Pass, attack and explicit `KillSingle` captures each completed 120 frames. Selected original views show the copper seedpod, split crown, mouth and leaves camera-fit; an ordinary attack changed HP 58 to 48, and one native Collect reached strict Ready at level 0, room 2. Its older shape leaves several current structured gates unrecorded, so it is retained as history rather than the canonical route representative.

The canonical [V2 exact-source archive](live-validation-v2/README.md) records fresh session `ea71b19cb70a474e867ecc09da0531bf`. One owner binds `rustpetal.glb` to exact `plantD / enJungleNibbler_C / 121537` through renderer instance `-245392`, owner `369188`, CEL `-245386`, the 43-joint signature `249fc7724b59d734e5bd0191e8c18c0276fd72f2c699e89c7b50e35e1b2c1810`, and native CEL scale 1.7. Live material readback records `matMasterJungle (Instance)` with the authored base-color texture on Standard, emission disabled, black emission color, and no emission map.

Two complete 120-frame captures preserve settled idle, native `attack3`, recovery, an ordinary zero-focus HP 58 to 50 `Damaged` result with `hit1`, and a later native `attackProf1` that visibly resolves as `WHIFF`. The explicit `KillSingle` fixture changes HP 50 to 0 for 1000 damage and records native `Death`; it is not ordinary lethal evidence. The exact source has `m_DoRagdoll=false` and zero rigidbodies, so the fall is animated rather than a ragdoll.

The death capture retains exact frames 0 through 93 of the requested 120. It shows the native `death` state, coherent fall, fallen pose, and Victory handoff before the selected renderer becomes inactive and not visible with its animator disabled, then is destroyed at the next sample. This accepted prefix does not prove the full requested duration, cleanup causality, an active corpse at frame 93, or later corpse lifetime. No loot Collect occurred; strict Ready succeeded at level 0, room 2.

V2 pins all 334 capture PNGs, 20 reviewed original frames, both authored assets, raw captures, journals, results, the root review, and three metadata-verified videos. Native effects, UI, foreground hero overlap, motion blur, and depth blur limit the stated views. Other plant sources, portraits, every animation interval, collision, culling, long-session resource lifetime, final disposal, and finished-art acceptance remain outside this exact-source result.
