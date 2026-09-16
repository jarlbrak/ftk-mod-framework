# Bronzehollow Sentinel

Original articulated dark stone and bronze guardian body for exact deathknightA, renderer121217 (`deathKnight`),36 ordered bones, native blunt controller5983. Broad separated bronze ribs, bracers and greaves surround faceted stone cores. Small jade sternum seal adds a readable accent. All geometry and palette are original parametric surfaces; the generator accesses native bone names and inverse binds only.

This is a body replacement. The native rigid horned helmet, shield and weapon remain native and are not included in the original assets or studio renders. The compact head core is deliberately hidden beneath the helmet; this is not a custom head or custom portrait silhouette. Reviewed V4 combat frames show the custom body moving coherently beneath that equipment, while detailed clearance across every clip remains untested.

Primary candidate is `bronzehollow.glb` with `bronzehollow_basecolor.png`. `bronzehollow.blend` is the editable armature scene. It is reopened and exported through the repository FTK Blender exporter to `bronzehollow-reopened.glb`; the separate roundtrip audit compares the exact candidate and reopened export. The studio scene is presentation only and contains no native geometry.

The trial uses plural renderer assignment on `deathKnight`, factor1 preserving native CEL scale1.2, with `disableNativeEmission:true` on the body material. Native equipment emission remains unchanged. Fresh V4 inventory reports the authored body texture on `matDeathKnight (Instance)` using Standard with emission disabled, black emission color and no emission map. No AI, controller or weapon behavior changes are part of the model profile.

[runtime-profile.json](runtime-profile.json) records that exact current
`deathknightA` route and passes static direct-route preflight. It is a repeatable
staging input only; selecting it does not make a historical archive apply to a
later catalog revision.

## Reproduce offline

Run from the repository root with the exact local121217 reference extracted under ignored `scratch/skeleton-audit/121217`:

```
scratch/model-venv/bin/python art-experiments/bronzehollow-sentinel/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender -b --python art-experiments/bronzehollow-sentinel/build_blender.py
scratch/model-venv/bin/python art-experiments/bronzehollow-sentinel/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/bronzehollow-sentinel/audit_surfaces.py
scratch/model-venv/bin/python art-experiments/bronzehollow-sentinel/finish_offline_audits.py
```

The final command verifies the reopened source equivalence and recorded exact bone order. To regenerate each pose sheet use `audit_native_poses.py --capture PATH --label pass|blocked|death --steps` followed by six valid frame indices recorded in that sheet's JSON; `--keep-root-motion` produces the distinct death-travel sheet. Captures remain ignored local evidence, not authoring assets. Rebuild in a copy to preserve a frozen candidate.

## Evidence boundaries

The three native diagnostic captures contain120 pass,120 blocked-attack and120 explicit-death frames. All360 exact bone sequences are verified, and original surfaces are evaluated with their captured matrices. Death switches to native physics with Animator disabled from frame45. The blocked attack did not damage the enemy; no ordinary-hit or ordinary-lethal acceptance follows. Root-normalized pose sheets inspect articulation; the separate death-travel sheet retains renderer-local motion. Raster sheets are two-sided and do not establish culling, floor collision, physics settling, or live material/portrait acceptance.

Closed-piece signed volume is positive independently of normal agreement. All36 binding joints are preserved; the two scapula joints have no directly weighted vertices, but affect descendants. Each rigid piece follows its intended native bone. Joint gaps are deliberate. Bind surface slightly exceeds native fingertips/toes; native animation bounds are unchanged and live culling remains pending.

Root approved selected studio and native pose views for a pinned trial only. The [fresh catalog-411 live archive](live-validation-v1/README.md) now records the exact body binding, four complete 120-frame recordings, two blocked ordinary paid-focus attempts, explicit `KillSingle` death and two native Collects reaching strict Ready at level 0, room 2. Both ordinary attacks remained blocked with HP 58 unchanged, so ordinary damaging/lethal acceptance is still open. Accessory clearance, collision/sleeping, full animation coverage, material cause of the darker live tint and final resource lifetime remain separate checks. No sibling Deathknight variant acceptance is claimed.

The [fresh V2 live archive](live-validation-v2/README.md) repeats the exact `deathKnight` bind in a new catalog-411 process with complete pass, ordinary attack and explicit `KillSingle` captures. The ordinary attack again resolves as native `BLOCKED` with HP 58 unchanged; one guarded native Collect reaches strict Ready at level 0, room 2, and one guarded Ready vote advances to the normal Jelly Cube plus cultist-probe room. The body material reports the authored basecolor with emission disabled and no emission map. This confirms the limitation is a repeatable native combat boundary rather than a missing renderer bind; ordinary damaging/lethal acceptance remains open.

The corrected [V4 canonical live archive](live-validation-v4/README.md) preserves one fresh current-catalog process with ten complete 120-frame captures. All 1,200 frames retain the exact `deathKnight` renderer, custom mesh, bone signature, owner and CEL identity while active, enabled and visible. The first seven ordinary zero-focus attacks return native `Block`, damage 0 and HP 45 to 45. Attempt 8 returns native `Damaged`, damage 1 and HP 45 to 44, with a complete hit recoil. The disposable party fixture raises only the equipped `bluntSmithHammer` Toughness skill from 0.81 to FTK's own 0.95 cap, spends no focus and leaves enemy combat behavior unchanged; this makes the recorded balance unrepresentative. The separate explicit `KillSingle` fixture takes HP 44 to 0, triggers `Death`, changes eleven retained bodies to dynamic and produces a reviewed coherent prone ragdoll. Two guarded native Collects reach strict Ready at level 0, room 2.

V4 reviews nineteen exact original PNGs and clearly separates the authored dark stone and bronze body from the retained native helmet, shield and mace. V3 freezes the same successful process but its review text mistakenly credited some native equipment as authored geometry, so V3 is historical and noncanonical. The explicit death is not ordinary lethal evidence. Other attacks, detailed equipment clearance, portrait behavior, collision, full culling, physics sleeping, long-session resource lifetime, final disposal, sibling Death Knight sources and finished-art approval remain open.
