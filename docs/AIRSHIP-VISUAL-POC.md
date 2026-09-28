# Airship visual proof of concept

Skyreach (Spike) is an experimental adventure for testing custom airship presentation
on the overworld and in native sea encounters. It is enabled only when the game
process starts with `FTK_SKY_SPIKE=1`. It is not a published adventure or a supported
mod-author API. The visual overhaul preserves the prototype's mechanics, boat
identities, prices, statistics, sailing, and character combat.

This guide describes the asset contract and verification route. It does not certify
finished artwork, successful runtime import, gameplay, or platform coverage.

## Existing behavior

The adventure is cloned from the Pirates template with the Kraken disabled. Its
water is presented as a cloud sea. The player ship keys remain `clipper`,
`stormbreaker`, `dandelion`, and `sunwing`; the enemy model keys are
`enemy_corsair`, `enemy_raider`, `enemy_ghost`, and `enemy_cult`. Existing deterministic
item and boat registration identifiers are retained. Replacing an art export does
not require a new item identity.

The default combat presentation places both crews on one host ship. A boat-camp
encounter uses the enemy ship; a sea encounter without an enemy boat uses the
party's ship. Characters retain native combat slots and actions. These visual
changes do not introduce cannons, ship-action turns, or a new damage system.
Legacy `broadside` and `legacy` placement options remain experimental compatibility
paths; the authored deck contract described below targets `arena` placement.

The authored arena also has an experimental showcase camera. Its first eligible
idle view presents the complete visible ship from a nearly broadside angle for
1.8 seconds after any native tutorial overlay closes. A one-time fit uses actual
visible mesh vertices and the full rotation envelope of authored propellers,
with separate HUD-safe vertical bands for the ship and fighters. Invalid geometry
or a changed camera lens falls back to the earlier close reveal. The opening
composition eases into the close view over 750 milliseconds. This wider reveal
passed native framing review on both ships: each shows its complete near stern
assembly above the player HUD. The far assembly remains physically occluded by
the hull. Subsequent idle views ease toward
the active crew and slot while retaining the same broadside and leaving room
for the HUD. Extreme hull tips may extend outside the frame; the camera does
not pull back to fit an empty bounding box around the entire ship. Native attack cuts, victory shots, lens
settings and zoom-out transitions retain control. After a native shot, the showcase
blends back over 750 milliseconds along an orbital path. During that bounded return
it preserves the native shot's near clipping; completed idle framing again requires
all living fighters to fit the authored depth range. Within the exact active authored
arena, native auto-look stops following a defender once that defender dies. This
holds the last native shot instead of tracking a falling ragdoll below the deck;
it does not cancel an attack, cut sequence, death animation or turn callback.
The showcase composes its final pose immediately before the combat camera renders,
after native updates and cut coroutines. Handoffs begin at the last displayed
pose, with a 750-millisecond easing curve in both directions. Changes in required
framing distance ease outward as well as inward. Native hard cuts remain cuts.
While the showcase owns the view, its far clipping distance temporarily extends
for the fitted content and eases back during the outgoing handoff. Set `FTK_SKY_SPIKE_CAMERA=0` to
use the native camera throughout. Invalid assets and non-authored arenas do not
enable this controller. The eligible cloud-water surface expands horizontally
for the wider view; its scene-instance scale and position are saved and restored
with the other scenery state. The separately lit native ocean backdrop is hidden
only in that authored arena, with its original renderer visibility saved in the
same restoration transaction. An encounter-owned blue-grey cloud material uses
a private mipmapped texture of broad procedural billows as emission, avoiding
the native sunset tint and hard received shadows on the combat floor. It leaves
overworld cloud materials and native actor lighting unchanged. For a single
separately movable horizontal water surface, an owned clone of the initialized
water mesh receives planar horizontal UVs. Its vertex positions, topology and
native deformation remain unchanged. This avoids the compressed native border
UVs that made the earlier cloud texture repeat densely at the horizon. Tiling
follows the expanded world bounds, targeting a 50-unit texture period. If native
water initialization is pending, one guarded Start postfix completes the binding;
cleanup cancels pending work. Unsupported or invalid surfaces retain their mesh
and conservative default texture scale. A native Corsair ordinary-action comparison removed the earlier dense horizon
strip. The first trial reported zero restored mesh bindings despite releasing
the private mesh. Eagerly materializing the mesh getter used by native rendering
now tracks its returned private instance and releases only the superseded private
allocation. A subsequent Stormbreaker native trial confirmed distinct assigned
and materialized instances, a stable owned binding through rendering, and one
original binding restored before the private mesh was released on Save/Exit.
Cleanup restores the original mesh binding and renderer material slots before
destroying the private mesh, material and texture. Both native
diorama reset and cleanup restore the
scene-owned floor, materials, renderer flags and effects, hide its custom combat
ships, and release only that scene's showcase camera. Save/Exit uses the native
scene-reload callback instead of encounter cleanup, so the same restoration
also runs immediately before that callback reloads the title scene. Camera readiness still requires native visual review;
frustum mathematics alone does not prove readable silhouettes or smooth handoffs.
For local diagnosis, `FTK_SKY_SPIKE_CAMERA_DIAGNOSTICS=1` logs UTC timestamps on
camera ownership changes and when dead-defender tracking is stopped. It also
writes a bounded CSV of up to 60,000 combat render samples, including native and
presented positions and rotations, handoff state, and native cut/action flags.
This opt-in trace supports motion diagnosis; it does not establish performance.

See [adventure authoring](ADVENTURES.md) for the public adventure API. The
`SkySpike*` classes are internal prototype implementation, not a new public surface.

## Runtime files and authored measurements

Place runtime assets beside the deployed framework under:

```text
FTKModFramework_content/skyspike/airships/
  stormbreaker.glb
  stormbreaker.ship.json
  stormbreaker_arena.glb
  stormbreaker_arena.ship.json
  airships.json
```

The directory is relative to the folder containing `FTKModFramework.dll`.
`airships.json` contains optional presentation tuning and enemy-faction mapping.
Each `.ship.json` is the authored contract for the GLB with the same filename stem.
The example below illustrates the schema, not measurements suitable for any
particular ship:

```json
{
  "schemaVersion": 1,
  "longAxis": "z",
  "hull": { "minX": -3, "maxX": 3, "minZ": -8, "maxZ": 8 },
  "deck": { "y": 1.5, "minX": -2.5, "maxX": 2.5, "minZ": -5, "maxZ": 5 },
  "combatModel": "stormbreaker_arena",
  "upperworks": ["lift_port", "lift_starboard"],
  "rotors": [
    {
      "node": "rotor_port",
      "pivot": [-2, 0.5, -6],
      "axis": [0, 0, 1],
      "degreesPerSecond": 240
    }
  ]
}
```

Measurements use the loader's Unity coordinate space after glTF node transforms
and the X reflection have been applied: Y is up; `longAxis` is `x` or `z`. Export
the sidecar from the final fitted geometry, not from concept art or an earlier
source model. The hull rectangle describes the hull footprint without broad lift
assemblies. The deck rectangle lies inside the hull and describes a continuous,
level, unobstructed fighting surface at `deck.y`.

Optional `rotors` identify separately exported propeller nodes. Each entry names
an unambiguous source node and supplies its measured axle pivot and nonzero axis
in the same final runtime coordinates as the hull and deck. Signed speed is in
degrees per second, bounded to 720 in either direction. Nodes must be unique and
separate from removable upperworks; pivots must remain within the loaded model
bounds. Omit the array for a static asset.

The loader groups every primitive and split chunk of a named rotor under an owned
transform carrier. Pivot and child offsets preserve its authored rest geometry;
only that carrier rotates. Engine housings must remain in static nodes. Rotation
uses game delta time and affects no gameplay or shared state. Disabling the owned
ship resets its carriers to the authored rest pose for subsequent placement.
This does not add motion to old models whose propellers are still baked into
their static hull meshes. New rotor exports require full-turn clearance checks
and native overworld and combat review before art acceptance.

The current stern-pusher candidate replaces the first pair's original engine and
blade assemblies with braced longitudinal nacelles. Both shafts point aft in
model coordinates, and the port/starboard blades have mirrored pitch geometry
with opposite signed rotation. Checks use the final post-reflection coordinates;
mirroring the animation speed alone is insufficient. Whole-ship review also
checks source fragments in lift nodes and repairs where old engines were removed.
The [rotor authoring record](../art-experiments/airship-overhaul/ROTOR-AUTHORING.md)
separates discarded layouts, reproducible exports, sampled clearance, geometric
projection and native evidence. These checks support a readable propulsion
layout, not aerodynamic performance or flight simulation. Native trials of both ships observed visible propeller rotation, single-player
pause/resume and valid combat placement. Overworld loading passed for both
identities; simultaneous unobscured motion of both map propellers remains
unverified. The closer idle view can crop the near assembly behind the HUD.

`combatModel` is an optional local model key without an extension or path. It selects
a matched combat export with its own valid sidecar. Omit it to use the same model.
Both exports should derive from the same authored craft and retain recognizable
hull, material, and silhouette features.

`upperworks` optionally lists exact, unique mesh-node names. Each entry identifies
a complete assembly that the authored combat cutaway omits; primitive suffixes
produced by the importer are matched automatically. Do not include the fighting
deck or hull. Avoid duplicate node names. The overworld keeps the complete model.
This path removes whole assemblies instead of splitting arbitrary triangles into
transparent fragments.

Authored arena placement uses uniform scale and checks that native combat slots fit
the declared deck. Runtime bounds checks establish that measurements lie inside
the asset envelope. They cannot establish that the declared rectangle actually has
planks beneath it or that props leave attack lanes clear. Those remain export
validation and visual-review responsibilities.

A missing sidecar preserves the legacy heuristic path. An invalid authored sidecar
is a failed authored asset, not permission to guess its measurements. Treat fallback
presentation as a failed art integration gate. The loader notices GLB and sidecar
timestamp changes on subsequent model requests; already instantiated ships are not
a reliable hot-reload preview. Replacing model files repeatedly within one process
can retain prior model and split allocations in the prototype caches. This is an
authoring limitation, not a supported live-reload workflow. Use a fresh isolated
session for each asset revision and final evidence.

## Export and preview

The spike uses its own rigid `SkySpikeGlb` importer. Its contract differs from the
public [custom-model routes](CUSTOM-MODELS.md). Use self-contained glTF 2.0 binary
files with embedded PNG/JPEG textures, triangle primitives, and baked static
geometry. Required compression extensions are unsupported. Skins and morph targets
do not provide runtime animation on this route.

The importer reads base color and limited emissive data. It does not reproduce
normal maps, packed metallic/roughness textures, translucent glTF materials, or
double-sided material settings. Artwork must remain readable through supported
albedo and geometry. A full-PBR Blender image is not an exact preview of the native
material result.

The [art campaign source](../art-experiments/airship-overhaul/README.md) records art
direction and source inspection. To inspect a generated source using Blender:

```sh
blender --background --python art-experiments/airship-overhaul/inspect_source.py -- \
  scratch/airship-source.glb scratch/airship-inspection --renders
```

Run from the repository root, substituting the actual source and output filenames.
Retain original source hashes, generation inputs, editable geometry, export commands,
runtime GLB and sidecar hashes, and the camera/material setup used for previews.
Render the final exported geometry at overworld and combat viewing sizes. Label
concepts, source inspection, runtime-export renders, and native captures separately.
Bulk source archives and diagnostic media belong outside ordinary Git history.

The independent [airship geometry auditor](../tools/ai-model-pipeline/validate_airship_asset.py)
reads the runtime GLB and sidecar, applies node transforms and runtime handedness,
and samples the declared deck after omitting named upperworks:

```sh
python3 tools/ai-model-pipeline/validate_airship_asset.py \
  art-experiments/airship-overhaul/runtime/stormbreaker.glb \
  --height-tolerance 0.025 --max-triangles 60000 --output scratch/airship-audit.json
```

The tolerance is in exported asset units; record its relationship to deck width and
the eventual world scale. Without an override, tolerance is 2% of declared deck beam
and sample spacing is at most beam/40. The report distinguishes upward floor contact
from topmost obstructions and reports actual coverage, height variation, and problem
sample coordinates. A finite grid can miss gaps or props between samples.

The triangle threshold is a configurable authoring target, not a game-engine limit.
The spike loader caps a model at 250,000 source vertices and splits large primitives
into parts of at most 65,000 vertices. Neither limit establishes acceptable runtime
performance. Overworld and combat exports can have different detail budgets; select
them from their viewing needs and measure native performance before claiming a
supported budget.

A metadata-only inspection of the installed game's `resources.assets` provides
context for individual native boat meshes:

| Mesh names | Triangle range | Median |
| --- | ---: | ---: |
| `boatA_overworld` through `boatE_overworld` | 3,402 to 4,723 | 3,822 |
| `boatA` through `boatE` | 5,733 to 8,399 | 6,525 |

These counts sum triangle submesh index counts for each named mesh. The inspection
read names, index counts, and topology metadata; it did not decode native surface
geometry. They are not complete ship assemblies, scene budgets, renderer-instance
counts, or performance measurements. The inspected resource file's SHA-256 was
`e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117`.

A 60,000-triangle combat export is a proposed target to test empirically, not a
ceiling or a guarantee of acceptable performance. Assess the complete rendered
scene, materials, textures, and concurrent ships during native profiling. An
armor-oriented 15,000-triangle authoring budget does not govern this spike's rigid
ship reader. Its structural loader limits remain distinct from both targets.

The current two-ship art revision derives from separate 60,000-triangle originals.
Its exact exported counts are recorded with hashes in the
[art manifest](../art-experiments/airship-overhaul/manifest.json):

| Ship | Overworld triangles | Combat triangles | Combat exported vertices |
| --- | ---: | ---: | ---: |
| Stormbreaker | 36,040 | 46,181 | 77,564 |
| Corsair | 36,050 | 48,128 | 88,259 |

The combat variants physically omit lift assemblies and use low side rails. The
map variants retain the complete airship silhouette. Both retain the original source
albedo on the hull and deck, with local geometry repairs and authored floor
measurements. Propulsion supports now sample dark hull wood; housings, collars
and blades use an additional embedded 1,254 by 1,254 painted metal atlas. The
material pass preserves every triangle position, normal and winding, original
hull UVs, original texture bytes and all sidecar bytes. UV seams add 672 exported
vertices per model without adding triangles. Other ship
keys retain their earlier prototype artwork. The
[tuning example](../art-experiments/airship-overhaul/airships.example.json) supplies
the presentation settings; broad enemy-faction remapping belongs only to the
isolated test fixture.

The combat exports batch static geometry by material while retaining two independent
rotor nodes. The loader produces seven runtime mesh parts for each combat export and six
for each map export, including vertex splitting. The additional material adds
one part to each map export and Corsair combat; Stormbreaker combat remains at
seven because the revised batches no longer need the same vertex split. The [stern authoring record](../art-experiments/airship-overhaul/STERN-PROPULSION.md)
pins the exact retained source attributes, deliberate removal of former engines,
local hull repairs, new propulsion geometry and reproducible export recipe.
Its reports distinguish sampled clearance and projection from aerodynamic or
runtime performance. Prior batching and performance evidence applies to the
specific older hashes recorded in the ledger. The
[material authoring record](../art-experiments/airship-overhaul/STERN-MATERIALS.md)
records the atlas provenance, rejected texture studies and exact preservation
checks. Texture readability and renderer counts do not establish the new
revision's performance.

## Verification and evidence boundaries

Follow [model authoring](MODEL-AUTHORING.md),
[model validation gates](MODEL-VALIDATION-GATES.md), and the
[in-game smoke workflow](../.agents/skills/ingame-smoke/SKILL.md). Exact game types
and patch assumptions must be checked against the installed managed assembly using
the [decompile workflow](../.agents/skills/decompile-lookup/SKILL.md). Do not commit
that assembly or decompiled source. Historical prototype observations are context;
they do not validate a new export or changed renderer.

Keep these gates separate:

1. Build and focused game-free checks for sidecar validation, placement, and fallback.
2. Final export inspection, topology/material inventory, and sidecar measurements.
3. Visual review of complete overworld craft and matched combat deck.
4. Native loading and placement in an isolated game copy, including malformed or
   missing authored assets falling back without an empty combat scene.
5. Boarding and ambush encounters, camera views during attacks, deck contact, and
   clear character silhouettes without lift assemblies hiding actions.
6. Save/reload and repeated encounters, then a non-spike adventure to check visual
   restoration. Record resource-lifetime observations separately.
7. Explicit platform and co-op checks before making corresponding support claims.

Live testing uses `FTK_AGENT_BRIDGE=1` and `FTK_AGENT_BACKGROUND=1`; follow the
[background-mode guide](../harness/README.md#background-mode) so tests do not take
desktop focus. The spike's optional fixture flags can create nearby boats or camps;
record their use. Fixture-assisted setup is not evidence of ordinary acquisition.
`FTK_SKY_SPIKE_ARENA_HOST=player` or `enemy` is a visual host override, not evidence
that the ordinary encounter selected that host.

New generated assets, completed runtime integration, live-game validation, and art
acceptance remain separate pending gates until receipts for the exact exported
hashes are recorded. No build or studio render closes a gameplay gate.

## Recorded local observations

The current material revision retains framework `8598be7b` and the preceding
stern geometry. Fresh Corsair and Stormbreaker native reveal and settled-idle
comparisons show dark hull-matched supports, textured iron housings and muted
warm-metal collars and blades. The Corsair test used the ordinary enemy host;
Stormbreaker used the explicit player-host visual override. The first Corsair
candidate was rejected because an inactive emission setting hid the new atlas.
The corrected export selects its textured hull material by binding, and a
regression check rejects that earlier failure. Studio previews now honor the
same texture activation gate. The original hull textures and sidecars remain
unchanged. All 39 Corsair and 46 Stormbreaker short idle samples were reviewed with no
visible texture flashes in the visible regions. Native Save/Exit restored the
original cloud mesh binding and released the private mesh, material, texture and
camera on both hosts. Both sessions stopped and all original test saves and
settings were restored. These checks are recorded in the material iteration
ledger. These observations do not renew
full-battle or performance evidence for the new material bytes. The near assembly
is visible during the reveal; the hull still occludes the far assembly, and the
closer idle view can crop the near assembly behind the HUD.

The pre-material stern revision used framework `8598be7b`. Both ships passed native
opening-framing review and Save/Exit mesh restoration with unchanged owned
bindings. The opening shows the near engine, supports and rotating blades above
the player HUD; the hull hides the far assembly. Close idle views prioritize
fighters and can hide the near assembly behind the bottom panels. Stormbreaker
pause/resume passed on this exact framework. Corsair pause/resume and one ordinary
shot, incoming action and return passed on predecessor `9c933f2d`, with identical
stern models and rotor code. A subsequent ordinary Corsair-host battle on the
final `8598be7b` build completed bow, melee, area spells, incoming actions,
victory, shared loot and return to the overworld. One hero fell; the two survivors
returned with 27 and 15 HP. The native revive chooser appeared but no revival was
performed. Combat cleanup restored the original mesh binding once and released
owned resources. An ordinary water move and return changed Stormbreaker heading
and provided additional map rotor motion evidence; full unobscured coverage of
both rotors across map angles remains partial. Original test saves and settings
were restored after the owned process stopped. Passive captures retain their
actual timestamps and sampling gaps; they do not establish fine-grained frame
pacing. Earlier full-battle observations below remain tied to their older hashes.

The [native evidence ledger](../art-experiments/airship-overhaul/native-validation.json)
pins each model, framework revision, capture and result. The initial close-camera trial
accepted both ships' close reveals and idle views, and completed a full player-host
battle through victory, loot and return to the map. The player host was explicitly
selected by the visual fixture; this does not verify natural ambush acquisition.
Rejected camera trials
remain recorded alongside accepted views so an older screenshot cannot be mistaken
for final-revision evidence.

Frame-by-frame review during the next polish pass found two camera defects in that
earlier battle: a sharp return from a native close-up and a death-shot interval
with the deck and fighters outside the useful view. Those observations limit the
earlier acceptance to its reviewed reveal and settled views. Motion acceptance
therefore required a new native capture with camera-ownership timestamps; the
earlier completed battle did not by itself establish a polished camera sequence.

Subsequent hands-on testing reported jerky combat transitions despite those sampled
checks. Source review found that the showcase restored its hidden native pose in
each camera Update prefix and stopped applying the showcase as actions began.
This could expose the hidden pose immediately; native camera coroutines could also
write after the showcase Update postfix. Earlier sampled return captures therefore
do not establish continuous motion across combat handoffs.

The first render-boundary candidate preserved exact ownership-boundary poses,
but an uninterrupted area-attack trace exposed large jumps inside its outgoing
blend. The next native position delta matched 90 percent of the prior presented
offset, showing that presentation state had fed back into native tracking.
The angular ownership comparison could reject an unchanged floating-point
quaternion. A component-based comparison, including equivalent quaternion signs,
now protects restoration; a quaternion from the rejected trace is a regression
test fixture. Render diagnostics also count successful restorations and ownership
mismatches to make a recurrence observable.

The polish trial reproduced that drift while native auto-look followed a dead
defender. Framework `8aea3351` stops that tracking within the authored arena and
adds the 450-millisecond return blend. A later native capture shows an intermediate
pullback between the close shot and settled view. A separate final-kill capture
keeps all three heroes and the deck visible through death and victory, with final
party health 28, 20 and 21. These are sampled visual checks, not full-frame-rate
recordings. Earlier captures timed out and remain labeled incomplete in the ledger;
their missing intervals are not treated as visual evidence.

Stormbreaker `18a337b4` also passed native review of its revised bow-ramp texture.
Its ordered triangle positions and normals are unchanged from `841b419a`; the
polish corrects grain scale and tone without changing the fighting surface.
Corsair `a4b42203` passed native opening and settled-view review of its restored stern
surfaces, with all six fighters visible. That view also exposed the separately
rendered native ocean backdrop above the cloud plane. Framework `b9b64348` hides
that scene renderer while the authored arena is active. Its native Corsair reveal
and settled view no longer show the maroon backdrop, and the low native camera
shows sky above the cloud horizon. Model acceptance, camera motion on `8aea3351`,
and the final backdrop/restoration trial retain separate evidence identities.
Native Save/Exit on `b9b64348` restored four materials, four renderer visibility
flags including the backdrop, one position, one scale and 45 effects, then released
the camera and hid the custom ship before returning to the title scene.

Framework `f4e1aaba` improves the imported ships' texture sampling with trilinear
filtering, anisotropy 8 and a restrained -0.25 mip bias. Original texture bytes and
mipmaps are retained; global graphics settings are unchanged. Native Corsair
comparison at 1440 x 900 shows modestly clearer oblique deck seams, wood grain and
hatch detail. The cabin and engine retain some softness in their source painting.
The opt-in lens diagnostic confirmed that depth of field was disabled in this
combat view and the texture resolution limit was zero. No postprocessing change
was needed. The 90-frame opening and idle sequence showed no conspicuous static
aliasing or halos in reviewed views, but its roughly two-frame-per-second sampling
does not establish full-rate shimmer behavior or performance. All eight canonical
model and sidecar files remain unchanged from the preceding polish pass.

A subsequent lifecycle pass found that native encounter cleanup and Save/Exit
use different exit paths. Framework `34ed38e3` adds scene-owned restoration to
both. On that build, exiting an active Corsair battle restored four materials,
three renderer flags, one position, one scale and 45 effects, hid the owned ship,
and released the camera before the title scene reloaded. A new ordinary For The
King adventure then displayed its normal forest terrain and blue water in the
same process. Ordinary combat after that switch and completed-battle cleanup on
that lifecycle build were not rerun. The later `8aea3351` polish trial completed
native victory, shared loot and return to the overworld, with the same four
materials, three renderer flags, position, scale and 45 effects restored and the
owned ship hidden and camera released. Ordinary combat after adventure switching
still remains untested. The earlier complete
battle and performance observations retain their original framework hashes;
they are not measurements of the cleanup build.

Three uninterrupted 30-second idle captures were recorded in the isolated macOS
fixture at 1440 x 900 with background input. These are separate observations, not
a controlled performance comparison: enemy variants changed, and the final trial
uses a different host ship and camera.

| Observation | Mean frame time | 95th percentile | Frames |
| --- | ---: | ---: | ---: |
| Earlier 15k-source combat export, native camera | 16.42 ms | 17.41 ms | 1,828 |
| Unbatched Corsair, native camera | 26.81 ms | 34.41 ms | 1,121 |
| Repaired, batched Stormbreaker, final turn camera | 9.66 ms | 16.94 ms | 3,107 |

The last sample used framework `095a8583`, Stormbreaker `841b419a`, five runtime
mesh parts and one observed generation-zero collection. Its maximum frame time
was 83.88 ms. The ledger contains complete hashes and CSV receipts. These samples
do not establish GPU cost, overworld throughput, a universal mesh budget, or a
causal speedup from batching. Controlled profiling and other-platform coverage
remain release work.

## Website impact

No published website edit is needed for this unreleased, environment-gated experiment.
The current production library must not imply that Skyreach is available to install.
Before an authorized public release, complete this handoff:

- Add the released package to the production catalog and regenerate
  `website/src/data/catalog.json` through `website/scripts/sync-published.mjs` using
  immutable, hash-verified public assets. Review `website/src/data/catalog.ts` and
  `website/src/data/library-art.json` for its library wording and artwork.
- Add the adventure guide at `website/src/content/docs/mods/skyreach.mdx`, using the
  final published package identity. Explain acquisition, preserved mechanics,
  supported framework version, and verified limitations.
- Update `website/src/content/docs/releases.mdx` and
  `website/src/content/docs/compatibility.mdx`. Change installation or troubleshooting
  pages only where the released setup requires different player steps.
- Prepare actual release media and record provenance in
  `website/src/data/media-provenance.json` or `film-provenance.json`. Identify studio
  views explicitly; never present generated concepts as gameplay captures.
- Follow [website maintenance](../website/README.md) for regeneration, build,
  browser, accessibility, media, and deployed-link checks. Published availability
  does not establish co-op or gameplay verification.
