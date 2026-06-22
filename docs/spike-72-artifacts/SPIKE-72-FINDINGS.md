# Spike #72 findings: runtime-glTF boss body (approach B)

Spec: #72 (epic #65). Branch: `campaign-d1-hollow-mire`. Acceptance instrument: the
spec #66 visual gate (`tools/ai-model-pipeline/run_visual_gate.py`), consumed unchanged
(NFR-4 honored: no gate file or threshold was modified).

## Verdict (FR-6): runtime-glTF (B) is ROBUST for reconstruction

The runtime skinned-mesh reconstruction was fixed. The Mudwretch Foreman now renders
in-game as a single COHERENT hulking golem, not an exploded triangle cloud. B can build
a coherent custom boss body editor-free. No pivot to A (AssetBundle) or C (procedural) is
warranted for the reconstruction problem this spike targeted.

A full four-criterion in-game gate PASS was NOT reached. The residual failure (`scaled`,
and an intermittent `connected`/`upright`) is NOT a reconstruction defect; it is asset
proportion + scene-framing + capture-pose, deferred to epic #65's "Placement, scale, and
animation correctness" child (see "Deferred" below). Decision recorded with the controlling
gate output; the user accepted closing on the reconstruction win and deferring the rest.

## What was actually wrong (evidence-based, not the spec's initial hypothesis)

The spec hypothesized a runtime SMR rebind bug. The investigation (FR-1 skip-skin
discriminator + a runtime renderer dump + a built-boneweight dump + direct glb geometry
analysis) found TWO distinct causes, only one of which was the rebind:

1. **Rebind (real, fixed by FR-2).** The glb path did only `smr.sharedMesh = gmesh;` on the
   live renderer and never reassigned `smr.bones`, so Unity left the skin bound to the
   previous mesh's bindpose set. FR-2 (reassign `smr.bones` with a fresh array of the same
   live bones after the swap) forces the rebind. This alone moved in-game `connected` from
   5 components / fill 0.43 to 2 components / fill 0.84. FR-3 (fresh Stitcher-exact SMR) was
   NOT needed and would not have helped (the dump proved a single coherent renderer).

2. **Asset stray triangles (the real residual "cloud").** The mesh is 100% RIGID (all 17007
   verts weighted to bone slot 0 = Root_M; verified in-game: `RIGID_OK=True`), so it cannot
   scatter under skinning. The "cloud" was ~145 DEGENERATE SLIVER triangles (1.1%) baked by
   the offline rig: near-zero-area needles connecting body-center verts to arm-tip verts,
   rendering as radiating shards. Removing them (`tools/ai-model-pipeline/06_clean_slivers.py`,
   maxEdge > 10% of bbox diagonal) eliminated the cloud (`fracTrisOver10pctDiag` 0.011 -> 0.0)
   while preserving the rig byte-for-byte. After cleanup the in-game body is a coherent golem.

The decode path was never at fault (verified byte-identical by `diag_decode_replica.py` and
by the consistently-PASSing offline preview).

## Why the full 4/4 was not reached (deferred to epic #65, sanctioned by FR-4)

- **`scaled` is structurally unreachable for this creature.** The band (`height_ratio`
  [1.20,1.65], `area_ratio` [1.45,2.60]) is calibrated for the BULKY stock troll. The slim AI
  golem tops out around `area_ratio` ~1.0 even fully detected at proper height. It is a leaner
  silhouette than the troll the band encodes. NFR-4 forbids relaxing the band.
- **The scale lever moves the boss off-frame.** The combat body is a fresh scale-1.0 clone
  (`EnemyDummy` never scales it). Scaling the clone root (`enTrollCave(Clone)`) multiplies the
  skeleton's bone offsets, shifting the boss partly out of the clean capture framing and
  fragmenting the silhouette. Scaling in place (Root_M, feet-anchored) is the proper lever and
  is left to the placement child.
- **In-game silhouette metrics are scene/pose-variable.** Two runs of the SAME final config
  gave `connected` count=1 (PASS) and count=3 (FAIL): the difference is the doorway VEGETATION
  behind the boss being intermittently detected, plus the combat animation pose at capture.
  The boss is coherent in every capture; the mechanical `connected`/`upright` criteria are
  confounded by the busy crypt-doorway scene and capture timing. Stable capture (fixed pose /
  scene masking) is a gate/harness hardening item.

## Final shipped state

- `Core/EnemyVisualPatch.cs`: FR-2 `smr.bones` rebind (permanent) + baked `BossBodyScale = 2.0f`
  (the custom golem is ~0.7x troll height; 2.0x is a boss-appropriate size). Original emission.
- `Core/RuntimeGltfMeshLoader.cs`: unchanged decoder; ALL spike diagnostics removed
  (`FTK_DIAG_SKIP_SKIN`, the WDIAG built-weight dump) per FR-5/NFR-5. No `Environment.*` reads.
- `ai-model-gen/mudwretch_rigged.glb`: sliver-cleaned (13068 tris, was 13213; rig byte-preserved).
- Build green; `SELF-TEST PASS` (x24) with no diagnostic flags; offline preview 4/4 consistently.

## Artifacts in this folder

- `BEFORE_fr2_stray_triangle_cloud.png` -- FR-2 rebind done, asset strays still present (the cloud).
- `AFTER_cleaned_coherent_boss.png` -- cleaned mesh + scale: coherent golem, in-game 3/4 (best run).
- `AFTER_coherent_boss_with_doorway_vegetation.png` -- same config, a run where doorway vegetation
  inflated `connected` to count=3 (the boss itself is still coherent): the scene/pose variance.
- `phase0_*` -- the FR-1 skip-skin discriminator crops.
- `TROUBLESHOOT_boss_visual.md` -- the original problem brief that opened the spike.

## Follow-ups for epic #65 (placement/animation child + gate hardening)

1. Scale the boss in place (Root_M, feet-anchored) so it can reach `scaled` without off-frame drift.
2. Re-calibrate or shape-normalize the `scaled` band for non-troll silhouettes (or accept a
   per-chassis baseline) -- the current band is stock-troll-specific.
3. Stabilize the in-game capture against doorway vegetation + combat-pose variance (fixed-pose
   capture or boss-region scene masking) so `connected`/`upright` are not scene-confounded.
4. (FR-7, #76, optional) Re-enable real multi-bone weights and re-confirm coherence.
