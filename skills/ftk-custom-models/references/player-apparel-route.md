## Player apparel lifecycle workflow

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

Treat each player class and skinset as an assembled outfit, not a single body
mesh. Inspect a real native avatar first and separate paths into two groups:
required body and hair renderers that must always be present, and conditional
apparel paths that appear only for a particular native equipment branch. Every
conditional assignment needs the exact expected native `sharedMesh.name`, as
well as its own original GLB, palette, bind matrices, and positive weights for
its complete palette. Do not substitute a similarly named renderer or infer a
layout from another class, gender, armor, or weapon.

Use a player profile with the required `renderers` array and optional `apparel`
array. Stage it through the same pinned transaction with `--catalog-kind
player`; the catalog, all body and apparel assets, and the player registration
report must stay in one receipt. If the profile declares `startingArmor`, launch
a fresh isolated process and fresh run after deployment. Native starting-item
processing owns the item transfer, so never grant the item through the test
bridge or assume an existing run acquired a revised profile.

The minimum live sequence for one player outfit is:

1. Inspect `equipment-inventory` and record the exact real hero instance ID,
   native item enum, and Body and Backpack counts. Do not continue when no
   native armor ownership exists.
2. Reach strict native Ready outside combat. Arm `lease-watch` on the real
   overworld avatar before each native transfer. The watcher is read-only and
   pins the old owned assets for later disposal observation.
3. Submit exactly one guarded `unequip-body` with the newly observed counts.
   Reinspect both overworld and combat renderer inventories. The expected
   default apparel branch must be custom, the boots must remain custom, and
   missing alternatives must be explicitly absent rather than silently mapped.
4. Read `lease-watch-state`, then arm a watch for the new avatar lease. Submit
   the inverse guarded `equip-body` only with fresh counts. Reinspect both
   scopes again and verify the equipped apparel branch and boots.
5. Read every watch until its old lease is absent and all pinned Mesh, Material,
   and Texture objects are Unity-null, or preserve a bounded
   `not-observed-disposed` result. Clear only diagnostic watch references after
   recording the result. Never invoke cleanup manually.
6. Separately inspect an actual native character-creation preview, overworld
   avatar, and combat dummy. To reach the preview without constructing a test
   avatar, first navigate normally to the visible offline Create Game screen,
   then run `command.py --root /absolute/scratch/game-copy
   native-create-character-preflight`. Require its read-only `eligible:true`
   result before issuing `native-create-character-screen`. The action accepts
   no payload and only invokes the exact enabled native Create Game callback;
   it waits for the game's own room, map, character-create root, and reciprocal
   preview-owner relationship. It does not select a class.

   After the native screen appears, call
   `native-create-character-input-state` before using a keyboard control and
   again after every navigation step that matters. It records the real FTK
   focus owner and selected native control without changing either. Navigation
   state varies with the previously focused control, so never assume a fixed
   number of arrow presses. Require the report to name the intended owner and
   intended owner before activating a class control. `toggleClass` is the
   central class button and is not a directional arrow. Use the guarded
   `native-party-class inspect` operation to resolve exactly one visible
   `classArrowNext` and one visible `classArrowPrevious` owned by that player;
   zero or multiple distinct arrows fail closed. Submit the returned token and
   direction for one native `OnClassClick` or `OnClassClickLeft` callback, then
   inspect again before another step. Tokens pin the owner, class identity,
   control identity, callback, visibility, and direction. Horizontal keyboard
   navigation can cross player cards and must never be treated as class cycling. It
   must never call private `SetClass`, change readiness, or construct an avatar.
   If the resulting native default is not the target profile, choose it through
   these native controls, then call `player-preview-state` with the exact profile
   key, skinset, catalog hash, and current owner ID. Record at least a guarded
   native pass, ordinary attack, and incoming damage capture on the selected
   custom renderer. Review frames for fit, branch selection, accessories,
   clipping, culling, and material response. Combat motion does not substitute
   for preview or equipment evidence.

The Blacksmith Female fixture uses body and hair paths plus two armor branches
and boots. [Hearthveil Blacksmith](../../../art-experiments/hearthveil-blacksmith/README.md)
is the reusable original-art example. Its [V3 canonical route](../../../art-experiments/hearthveil-blacksmith/live-validation-v3-canonical/README.md)
combines the historical V1 native attack, hit, equipment rebuild, lease disposal
and Ready evidence with current same-session Party Select body and six-bone hair
idle captures and explicit `death_overworld` motion fixtures. Verify it with
`verify_hearthveil_canonical_archive.py`. The death captures are native-state
playback on a real preview owner; they do not prove ordinary player death,
corpse lifetime, or cleanup. Use this archive as a sequence and verifier
template, not proof for another
skinset, apparel branch, or player layout. Repeat the full discovery,
transaction, native-equipment, preview, motion, and lifetime sequence for every
new layout. A successful previous skinset never establishes the renderer paths,
native mesh identities, equipment behavior, or cleanup of another one.

[Wildbloom Herbalist](../../../art-experiments/wildbloom-herbalist/README.md)
provides the separate `herbalist_Female` 24/6/7-bone authoring package, including
the player-only seven-bone `hairBottom` layout. Its
[V2 canonical route](../../../art-experiments/wildbloom-herbalist/live-validation-v2-canonical/README.md)
uses the exact directional native Party Class controls and preserves all three
meshes through Party Select, overworld, ordinary combat, native progression,
and one combat-avatar rebuild. It records ordinary enemy HP `72 to 62`, hero HP
`999 to 962`, strict Ready, and level 1 progression. Its death captures are
native-state playback fixtures, and its kill fixture is used only to reach
progression. No custom apparel is declared, so native clothing remains
game-owned. Run `verify_wildbloom_canonical_archive.py` before using it as a
sequence template. Do not reuse Hearthveil's six-bone hair or transfer this
evidence to FishPerson, another skinset, final owner teardown, or final art.

[Tideglass Fishsmith](../../../art-experiments/tideglass-fishsmith/README.md)
provides the distinct `blacksmith_Fish` route. Its V1 native-preview archive
shows the exact casing `playerFIsh`: the body and seven-bone `hairBottom` are
visible in the default Fish preview, while `hairTop` is bound but inactive.
Record that state rather than claiming every configured renderer is visible.
When a real preview exposes an art problem, first freeze its source assets and
raw evidence in a non-overwriteable archive, then stage a hash-pinned asset
revision with `--replace-existing-assets` and restart the isolated game before
a fresh preview. A revised studio render never upgrades the earlier live result.
