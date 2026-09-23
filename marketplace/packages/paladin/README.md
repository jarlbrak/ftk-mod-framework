# Paladin

Work in progress for [epic #138](https://github.com/jarlbrak/ftk-mod-framework/issues/138).
This source directory is not a published or accepted marketplace package.

One mod enables the immediately available Paladin, full Vitality hammer/shield
progression, native character appearances and original horizontal endgame equipment. The complete
[design contract](../../../docs/paladin/DESIGN.md) and
[implementation plan](../../../docs/paladin/IMPLEMENTATION.md) govern delivery.
The [acceptance matrix](../../../docs/paladin/VALIDATION.md) separates offline,
native gameplay, visual, save, package and online gates.

The launch target is Paladin 1.0.0 on framework 1.0.0. Neither is published yet.
The [launch checklist](../../../docs/paladin/LAUNCH-1.0.0.md) tracks the remaining
checks separately from historical beta evidence.

`manifest.json` owns identity, author, version and the short description. `listing.json`
owns marketplace feature copy and requirements. The builder combines these with
measured archive facts into the shared marketplace descriptor. Framework versions,
platforms and artifact hashes are not repeated in prose.

## Current content state

The source package declares one class, 51 equipment items and two Censure
proficiencies. All equipment has new models and icons. The twelve newest items
are six trinkets and six necklaces using native accessory slots; their stats and
source art have offline checks only. [Accessory validation](../../../docs/paladin/ACCESSORY-VALIDATION.md)
records the explicit pause before in-game testing. Character bodies, faces,
hair and backpacks remain native FTK assets. The cloned Blacksmith appearance
list is inherited unchanged: Female, Male, Undead, Cat, Demon, Fish and Goblin,
with normal native unlock checks. Nonhuman races are individual appearance
entries, not independent male/female combinations.

Six armor sets provide body, foot and head items, with native female/male garment
bindings. The class starts with a novice hammer, shield and full novice armor
set. Native fresh-game initialization adds and equips eligible starting items;
fresh Party Select uses an empty preview inventory and therefore shows native
class clothing until the game starts. The [historical production fresh-game
trial](../../../docs/paladin/production-new-game-observation.json) for archive
`92dd4d19d8ef` confirms all five Novice pieces equipped and persisted across
save/resume and restart. The current art archive is separately covered by the
[current world-start receipt](../../../docs/paladin/current-package-world-start-2026-09-21.json):
it equips the same five slots after native startup, with the user separately confirming the current candidate
through native Save and Exit followed by a fresh resume with its starter equipment
retained. This is user-operated validation without an automation receipt. All-race gear fit is a live acceptance gate.

The original GLB/PNG assets are traced by
[the external provenance receipt](../paladin-assets.provenance.json).
No custom body, hair, portrait or backpack replacements are shipped. Hammers
include original break fragments for every declared native break renderer.
The class/action icon supplies the original Guard button. Native HUD portraits
render the game-owned character with its equipped custom gear.

Four acquisition bands use native item tiers: Novice 0, Oathkeeper 1-2,
Highward 3, and Mercy/Censure/Verdict 4-6. These are not character levels.
All three endgame sets are eligible at the ordinary final campaign tier, 4.
Native rarity weights normalize drops, and fixed shop stock counts remain intact.
Neither path requires a Paladin in the party. Final combat balance still requires
play evidence.

Three Artifact items extend the late-game selection: The Last Vigil, Kingsfall
and The Last Bastion. They use item tiers 4-6 and can enter shared drops, night
markets and dungeon merchants without requiring a Paladin. They do not enter
ordinary town stock or starting inventories. Their Guardian perks preserve
Guard's 50% reduction. See the [legendary equipment design](../../../docs/paladin/ARTIFACTS.md)
for exact triggers and the remaining implementation and validation status.

| Equipment | Declared prototype behavior |
| --- | --- |
| Novice/Oathkeeper/Highward shields | No personal armor or resistance, minus two Speed points; Guard heals its ally for 2/3/4% maximum HP |
| Mercy shield | Same personal tradeoff; Guard heals its ally for 8% maximum HP |
| Censure shield | Same tradeoff; selected guarded-ally debuff ward |
| Verdict shield | Same tradeoff; four retaliation damage |
| Mercy great hammer | Adds four percentage points to focused-hit healing |
| Censure great hammer | Censure action: 75% damage, four slots, native timed armor reduction of six |
| Verdict great hammer | Higher direct damage, 39 base |
| Mercy one-handed hammer | Adds two percentage points to focused-hit healing |
| Censure one-handed hammer | Censure action: 75% damage, three slots, native timed armor reduction of four |
| Verdict one-handed hammer | Higher direct damage, 31 base |
| The Last Vigil | 30 damage, three Vitality checks; first mitigated Guard hit restores one Focus to the protected ally |
| Kingsfall | 42 damage, five Vitality checks; Guard mitigation charges the next single-target hammer attack for 50% more damage |
| The Last Bastion | No personal armor or resistance, minus four Speed; Guard cleanses one existing eligible condition |
| Mercy armor pieces | Equal base defenses within each slot plus two Vitality points |
| Censure armor pieces | Equal base defenses within each slot plus one Speed point |
| Verdict armor pieces | Equal base armor within each slot plus two resistance |

Both Censure armor deltas and timed expiry have [native observations](../../../docs/paladin/censure-live.json). Armor budgets and endgame alternatives remain beta balance values, not claims of playtested equivalence.

The class uses 84 Vitality, 70 Strength, 40 Intelligence, 60 Awareness, 50 Talent,
60 Speed and three Focus. Its initial three gold and modest offensive stats
reflect the power of guaranteed protection and rescue. Vanilla Steadfast is
explicitly removed from its privately copied skills.

One-handed hammers clone the verified one-handed Smith Hammer mechanical
chassis; two-handed hammers clone the verified two-handed War Hammer. Original
models replace the visuals. All gear remains usable by other classes.

Body armor clones `armorHeavy1`; boots clone `bootsHeavy3`; helmets clone
`helmetHeavy1`. This helmet template has one native rigid renderer and one
material slot, matching the static replacement contract. The prior
`helmetHeavy2` trial rejected its multiple material slots and retained native
fallback; that trial does not establish original helmet rendering. The cloned armor/boot rows use Blacksmith female/male wearable
bindings, while exact original renderer assignments remain scoped to the custom
items. The helmet root replacement uses the native post-detach renderer path `.`.
Asset transforms and helmet placement remain subject to native validation.
The verified native mount rotation is Euler `(-90, 90, 0)`; selecting the new
template does not itself prove that the original helmet fits this mount.
Original helmet exports now include the Head-to-Hair mount-space correction
documented in the [character source package](../../../art-experiments/paladin-characters/README.md#helmet-mount-correction).
Captured transform arithmetic passes; a fresh native appearance check is pending.

Offline structural checks establish unique identities, local asset references,
original source hashes, complete progression and declared renderer coverage.
Reproduce them with `python3 marketplace/packages/validate_paladin.py`.
They do not establish registration, visual fit, normal shop acquisition, save
behavior, native action semantics or co-op. The current 1.0.0 candidate remains
unpublished until its applicable launch gates pass.

Online co-op remains unverified. Host/client behavior, multiple guardians and
state transitions need community confirmation before claiming co-op support.

## Build an unpublished local candidate

The builder archives only `manifest.json`, `content.json`, and PNG/GLB assets.
It sorts entries, fixes ZIP timestamps and permissions, and names artifacts by
their content hash. README and external provenance files are not runtime files.
It emits a matching descriptor marked LOCAL DRAFT with a deliberately unpublished
`LOCAL-DRAFT-NOT-PUBLISHED` release URL. It does not create a release, upload an
archive, or modify the production catalog.

Use `--release` to prepare the same archive with versioned `paladin-v1.0.0`
release URLs and its final display name. This flag does not publish anything.
The banner must be uploaded alongside the immutable archive before catalog inclusion.

```sh
(cd launcher/helper && go build -o ../../scratch/paladin-package-helper .)
python3 marketplace/packages/build_paladin.py \
  --helper scratch/paladin-package-helper --fixture \
  --game-assembly scratch/paladin-game/PaladinTest.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll
python3 marketplace/packages/build_paladin.py \
  --output scratch/paladin-package-rebuild --helper scratch/paladin-package-helper \
  --game-assembly scratch/paladin-game/PaladinTest.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll
```

The commands fingerprint the configured isolated macOS `PaladinTest.app` copy at
`scratch/paladin-game`; another explicitly configured copy can be supplied with
`--game-assembly` and `--platform`. Platform metadata permits local fixture
validation and does not establish platform gameplay support.

The helper runs its actual `marketplace-validate` command against the archive.
`--fixture` additionally prepares a new private managed state under the build
directory and records its request/result. It does not activate that state or
modify the live isolated game's managed selection. Build receipts pin every
runtime file; validation receipts pin the helper binary. Game review remains
required even when both commands pass.

Armor and boots also declare original rigid shop/inventory-card meshes, separate
from their worn skinned apparel. Large cards use native 3D loot clones, not icon
sprites. `bootsHeavy3` supplies a non-null single-material display prefab; explicit
original wearable bindings and modifiers are preserved. Separate `displayModels` renderer paths
are `armorSplintVestDisplay` and `bootsIronGreavesDisplay`. Original sources and
offline checks are in the [loot-display campaign](../../../art-experiments/paladin-characters/loot-display/README.md).
Native card framing and refresh remain live gates.

Two-handed hammer cards use separate fitted display GLBs. Their transformed
bounds fit within the original novice one-handed hammer card envelope, with a
five-percent margin. Equipped hammer models remain unchanged. The earlier
two-handed card crop remains a failed framing trial; new card fit needs live
confirmation. See the [original hammer display sources](../../../art-experiments/paladin-equipment/loot-display/README.md).
