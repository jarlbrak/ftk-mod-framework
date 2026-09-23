# Paladin implementation

Tracking epic: [#138](https://github.com/jarlbrak/ftk-mod-framework/issues/138).
The [design contract](DESIGN.md) records the agreed player behavior.

## Accessory completion plan

The [equipment inventory](EQUIPMENT.md) records 51 authored items, including
six new trinkets plus six new necklaces. [Gap plan](GAPS.md) orders data, original art,
native verification and final package work. The new source candidate contains 54 entries;
[accessory validation](ACCESSORY-VALIDATION.md) records offline checks and the
explicit pause before live testing. [Native baseline](NATIVE-BASELINE.md)
verifies the two equipment slots and existing modifier/display capabilities.

## Delivery boundary

One data-driven marketplace package owns the Paladin class and its equipment.
The framework supplies reusable guard mechanics and declarative model support.
Installing this package must not require a separate behavior DLL, manually copying
models into the framework, or enabling bundled sample content.

The development marketplace now validates bounded GLB assets and typed content
capabilities while retaining executable-code rejection and matching helper/runtime
validation. These capabilities ship in the planned framework 1.0.0. Models resolve within the owning package, with stable
package-specific identities; references must never escape that root.

## Ordered implementation slices

1. Pure combat rules: designation, active protection, expiry, incapacitation,
   non-stacking reduction, deterministic rescue consumption and focused healing.
2. Installed-assembly investigation and native action integration: equipment-free
   friendly action, authoritative damage outcomes, successful attack completion,
   turn boundaries, combat teardown, and feedback.
3. Typed public authoring and data declarations for the mechanics, then matching
   marketplace validation. No generic script execution or callback language.
4. Package-relative model ownership plus item-bound weapon, shield and apparel
   appearance. Unrestricted gear must render on other classes as well as Paladin.
5. Original geometry, textures and icons; both player appearances; complete
   progression; three contrasting endgame specialties for each equipment path.
6. Immutable package construction, local installation, gameplay and visual proof.
   Record failures and missing gates without claiming a finished beta.

## Initial arithmetic and lifecycle decisions

These are implementation defaults, subject to balance evidence:

- Apply one 50% reduction to direct damage remaining after existing native
  defenses. Round retained damage upward, so an odd hit does not gain extra
  mitigation from truncation. Do not turn a positive one-point hit into zero.
- A lethal rescue is considered after reduction. It leaves one HP and consumes
  one available charge, selected by ordinal stable guardian identity.
- Base focused healing is floor(maximum recipient HP * 8 / 100), capped at the
  recipient's missing HP. It never revives, damages, or exceeds maximum HP. Successful
  slot results with native Block/MagicBlock qualify even when armor or resistance
  absorbs all damage. Zero-slot misses, dodges, harmless actions and forced misses
  do not. The matching native hit must occur before healing; AoE still heals once.
- A positive equipment Guard-heal perk restores at least one HP when injured,
  otherwise flooring its percentage and capping at missing HP. This makes the
  Novice Aegis useful below 50 maximum HP; base focused healing is unchanged.
- Expiring protection does not erase designation. Incapacitation cancels the
  active application permanently; recovery alone does not reapply it.
- Transient designations, active guards, spent rescue charges and attack receipts
  reset at encounter boundaries. They must not leak into a later fight.
- Native resurrection also calls `ResetForCombat`, so that hook only expires
  active protection and preserves spent rescue. `InitDummyForCombat` resets the
  actor for a genuinely new fight, including the next dungeon battle. See
  [the native lifecycle evidence](guardian-revival-native.json).
- Healing receipts identify a committed attack, not an animation event or one
  individual target in an area attack.

Exact attack success semantics, AoE ordering, stable runtime identity and skipped
turn hooks require installed-assembly evidence before native integration. Host
migration and online parity remain explicit unverified beta limitations until
supported by actual tests, not inferred from master-only code.

## Equipment and original art inventory

The proposed minimum progression uses four acquisition bands: starter, early,
midgame and endgame. Exact native market level ranges and combat numbers require
inspection and balance comparison. Earlier bands offer one coherent item per
path; the final band offers three alternatives rather than a strict upgrade chain.

| Family | Starter | Early | Midgame | Endgame |
| --- | --- | --- | --- | --- |
| One-handed Vitality hammers | 1 | 1 | 1 | 3 alternatives |
| Two-handed Vitality hammers | 1 | 1 | 1 | 3 alternatives |
| Dedicated defensive shields | 1 | 1 | 1 | 3 alternatives |
| Armor appearance families | Original initiate | Original gilded knight | Original transitional crusader | Original hooded adjudicator, branch colors/details |

The implemented inventory includes these 18 weapon/shield items plus six body
armor, six boot and six helmet items, for 36 pieces of equipment.
Each receives original geometry and an icon. Reusing an original modular design
within a set is allowed; a vanilla model or palette-only vanilla reskin is not an
original asset. Male and female body/outfit variants require separate binding and
visual acceptance, even when they share motifs or textures.

Candidate shield specialties are debuff protection, recovery on Guard, and
retaliation. Candidate two-handed specialties are focused healing, armor-breaking,
and direct damage. These effects are implemented prototypes, pending live behavior and balance
validation. Shields retain 50% Guard reduction in every tier.

Art direction uses broad classic fantasy cues: early silver/gold plate and blue
cloth; later an original hooded, dark-red/gold judicial order silhouette. Create
new proportions, symbols, ornament and surface treatment. Do not trace, extract,
or redistribute source-game or reference-game art. Keep a provenance manifest
covering every shipped model, texture and icon plus reproducible authoring inputs.

## Acceptance and release

Game-free tests establish rules, schema, containment, registration and package
integrity. Live isolated tests establish native action consumption, feedback,
normal shop/loot acquisition, save round-trips and model appearance/motion.
Neither result substitutes for the other.

For each added appearance and item, retain evidence of preview where applicable,
overworld and combat, relevant attacks, incoming damage, equipment rebuilds and
owned-resource cleanup. Exercise unrestricted equipment on another class. Do not
count native fallback as success. Label any synthetic fixture separately from
ordinary gameplay and preserve package/binary hashes.

Beta requires the applicable local gates to pass and clear disclosure of untested
platforms and online co-op. Production additionally requires community evidence
for host/client agreement, multiple Paladins, switching, non-stacking, one rescue
consumed per lethal hit, and combat/save transitions.

## Native source findings

Installed class tables give Blacksmith 80 Vitality, 76 Strength and 56 Speed;
Monk 62 Vitality, 74 Strength and 60 Speed, both with three Focus. Initial Paladin
stats are proposed at 84 Vitality, 70 Strength, 40 Intelligence, 60 Awareness,
50 Talent, 60 Speed and three Focus. This trades away Blacksmith's Talent and
Strength and Monk's Intelligence in return for the protector kit. These are
prototype values, not playtested balance.

The native Smith Hammer is one-handed; War Hammer, Great Hammer and Royal Hammer
are two-handed, verified through serialized object-slot metadata. Native hammers
use Strength. New Vitality hammers therefore need their own numbers rather than
inheriting a supposed existing Vitality hammer progression.

Cloned equipment does not automatically inherit defense bonuses: native equip
logic looks up a separate character-modifier row by item identity. New shields
and armor require an explicit matching modifier registration. A copied visual
prefab alone does not establish item stats.

## Current integration evidence

The complete package has registered all 39 entries in isolated startup. Native
SaveExit/Resume retained the observed Paladin class identity and five custom gear
identities, without establishing coverage of every item or appearance.

The six helmets use the single-material `helmetHeavy1` binding. A live trial
applied the original helmet but exposed a Head-versus-Hair mount offset. Original
export geometry now accounts for the native Hair attachment; offline fit passes,
and the corrected female novice helmet fits in live preview. Other appearances,
progression sets and animation remain unverified.

The [combat-entry investigation](combat-entry-investigation.json) preserves the
failed trials and identifies the corrected framework. Replacing clone-retention
finalization with retention immediately after native clone assignment passed the
first untraced Paladin entry at the previously failing encounter, with diagnostic
trace and skip options disabled. Placement used the engage fixture; battle
interaction used native UI.

Native Guard selected the Scholar, advanced the action without consuming Focus,
and became inactive at the next Paladin turn while retaining designation. A
secondary Bleed tick bypassed active protection. The compact Guard panel fit.
No guarded direct hit landed in that ordinary trial. Subsequent
[controlled native-pipeline fixtures](guardian-native-fixtures.json) supplied 9
direct damage and observed 5 applied damage (36 to 31 HP), then supplied a lethal
62 damage at 31 HP and observed 1 HP, alive, with rescue spent. Real UI Guard
casts preceded both fixtures; production transformation and native hit processing
ran, while enemy targeting and the incoming damage outcome were fixture-controlled.
Splash/group-debuff attempts safely aborted before replacement. These are not
ordinary RNG or co-op passes. [Later multi-Guardian fixtures](multi-guardian-native-fixtures.json) verify lethal death after charge consumption. [The revival regression](guardian-revival-live.json) verifies spent charges survive native revival and a subsequent Guard cast. The [acceptance record](VALIDATION.md) scopes these observations
and retains full combat, visual, acquisition, save, lifecycle and online gates.

### Later live validation findings

The native class skin enum is Female=0, Male=1. The reversed package skinset
array was corrected. Earlier female-body preview attribution must not be treated
as female apparel fit coverage: those observations cross-paired body and armor.
All 36 items loaded on the all-gear fixture, with twelve set/loadout snapshots,
and both two-handed Paladins could cast Guard. The current package preserves native bodies and uses revision-3 equipment; [bounded current fit evidence](armor-revision3-review.json) is distinct from these historical body-swap observations.

A native market Buy & Equip transaction passed for Novice Great Hammer on a
Hunter: 15 gold paid, stock decremented, and the purchased item equipped. See
[native purchase evidence](native-shop-purchase.json). The test also revealed
that large item cards render a separate 3D loot clone. Original display assets are now packaged, with [bounded card observations](item-display-native.json). The native backpack is retained under the revised equipment-only scope. Unobserved card and motion variants remain explicit in the acceptance matrix.
