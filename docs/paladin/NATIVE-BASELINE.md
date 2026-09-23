# Paladin paper-design native baseline

Authority: installed original FTK `Assembly-CSharp.dll`, SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
The 2026-09-22 read-only audit used fresh ILSpy declarations/methods and matching
serialized database and prefab metadata. This report contains findings, not
native source, asset bytes or a new gameplay test.

## Slots and templates

`FTK_itembase.ObjectType` defines trinket and necklace equipment categories.
`PlayerInventory` supplies one Trinket and one Neck slot. Its Belt container has
three spaces for usable belt items; it is not wearable belt armor. The paper
inventory therefore adds two accessory families, not three.

| Template candidate | Category | Modifier facts | Original-art route |
| --- | --- | --- | --- |
| `trinketDefense1` | Trinket, equip | Native row +2 Armor | Display prefab child `trinketHorn2` |
| `amuletVitality1` | Necklace, equip | Native row +3 VIT and +4 max HP | Display prefab child `amuletLocket1` |

The amulet prefab root is spelled `amluetLocket1`; its child has the correctly
spelled path above. Both routes have icons/display prefabs and no male/female
wearable prefab. The complete `CharacterOverworld.CheckUpdateAvatarAndPortrait`
implementation handles body, boots, helmet, shield and weapon visuals, with no
accessory branch. Original display art is required; new avatar bindings are not.

The [Equipment](EQUIPMENT.md) comparison table records low/high native Armor,
Resistance and Vitality accessory values. `Content.SetItemModifiers` creates a
blank custom modifier row keyed to the new item; it does not clone the
template's bonuses. Explicitly supplying the proposed modifiers therefore
excludes native amulet HP, Focus and skill bonuses.

## Public capability boundary

The current declarative modifier schema accepts Armor, Resistance, Vitality,
Speed and Reflect. The proposed accessories use its first four fields. Native
`m_ExtraFocus`, `m_ExtraHealth` and `m_ModAttackPhysical` exist, but their existence
does not expose them through marketplace JSON. Any later proposal to use those
stats needs a deliberate public API, schema, runtime/helper validation and
lifecycle coverage. None is required for the current accessory proposal.

## Current weapon inheritance

| Template | Regular action | Additional native actions |
| --- | --- | --- |
| `bluntSmithHammer` | `STR_profSmash` | `hammerSplash` |
| `bluntWarHammer` | `STR_profCrush` | `bluntShockwaveSplash`, `bluntStun` |

Both native templates have regular attacks enabled, ordinary Focus enabled and
`m_CanBreak = false`. The package overrides governing stat to Vitality and
authors damage/check counts. Native action identifier names do not override
that authored weapon stat.

`Content.AttachProficiencies` and `AddProfsToWeapon` append entries to the cloned
weapon proficiency dictionary. They do not clear native actions. Censure's
custom action is therefore additional to the rows above. Paper balance must
include those options. A future action-list redesign would be a deliberate
behavior change, not documentation cleanup.

## Class inheritance and derived stats

The native Blacksmith enables only Steadfast among its class skill flags.
Paladin explicitly disables it, leaving no inherited class skill in that set.
The native class row also has a 0.45 taunt-chance field which Paladin does not
override. That field is not a guarantee that Guard changes enemy targeting;
Guard has its own equipment-independent action and does not invoke taunt.

`CharacterStats.MaxFocus` clamps the sum of base Focus, equipment modifiers,
augmentation and difficulty bonus to 1 through 9. Apprentice contributes one
Focus and five main-stat points; Medium and High contribute neither in the
inspected difficulty rows. Thus the authored three-Focus Paladin appearing with
four maximum Focus in an Apprentice trial is expected. Combined equipment
tables deliberately exclude difficulty, and must not be presented as screenshots
of every actual class panel.

Primary stats clamp to **20 through 95**. `RawVitality` sums class, equipment,
augmentation and difficulty; `FormatSkillValue` applies poison and curse
multipliers before clamping to `GameFlow`'s serialized limits. The stat panel
uses that capped value, as does the Vitality contribution to maximum Health.
Focus and attack-specific bonuses can subsequently raise a roll chance to 100%.
There is no separate 99-point stat-panel ceiling in this route.

Mercy apparel gives 90 Vitality before difficulty. Apprentice's five points
already reach 95. More Vitality could cushion penalties applied before the cap,
but would give no healthy accuracy or derived Health benefit there. The proposed
Mercy accessories therefore spend their budget on defense and Speed instead.

Future live acceptance still needs native equip/remove stat deltas, supported
display behavior and ordinary acquisition. The audit establishes suitable
routes and constraints, not acceptance of the new accessory items.
