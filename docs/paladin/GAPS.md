# Paladin completion and gap plan

Status: accessory source implementation, 2026-09-22. The source candidate now
contains 54 entries, including twelve new accessories and original display
art. The previously installed 42-entry candidate is unchanged. Work is paused
before deployment or any in-game test, as requested. See
[Accessory validation](ACCESSORY-VALIDATION.md) for offline evidence.

## Coverage ledger

| Area | Current state | Planned closure |
| --- | --- | --- |
| Class identity and rules | Implemented Guardian kit; existing bounded evidence | Keep [Design](DESIGN.md) and [Combat](COMBAT.md) consistent with supported behavior |
| Ordinary weapons, shields and apparel | 36 authored items across six families | Preserve IDs, original art and existing progression; verify complete action/stat loadouts |
| Artifacts | Three authored items with partial live effect/appearance evidence | Complete the named [legendary gates](LEGENDARY-VALIDATION.md) |
| Trinket | Six rows and original assets authored; offline checked | Native acceptance of the keepsakes in [Equipment](EQUIPMENT.md) |
| Necklace | Six rows and original assets authored; offline checked | Native acceptance of the necklaces in [Equipment](EQUIPMENT.md) |
| Belt | Native consumable storage, not missing wearable gear | No new belt equipment or consumable system |
| Accessory power budget | Authored values match complete loadout totals; native cap audited | Playtest native alternatives, mixed families and pool pressure |
| Accessory art | Twelve original icons and display objects made with Astra High | Verify exact native card framing, facing and render replacement |
| Sanctum | Native Grand Sanctum of Life exists; separate custom scope unresolved | Preserve the separate decision and live gate in [Launch 1.0.0](LAUNCH-1.0.0.md) |
| Distribution | Existing candidate installed locally; production catalog unpublished | Final package/lifecycle checks and the existing release process |

The complete authored inventory is 51 equipment items: 48 ordinary pieces and
three artifacts. One class and two Censure proficiency rows bring it to 54
content entries. Those are authored counts, not native registration results. The previous
live-tested candidate contained 42 entries.

## 1. Accessory design: authored

Use one item per family per real accessory slot. Novice remains simple;
Oathkeeper and Highward improve ordinary equipment; Mercy, Censure and Verdict
offer distinct late-game choices. The initial numbers are conservative and use
existing stat capabilities. No accessory receives `guardianBonuses`, a new
action, a set bonus, an immunity, Focus regeneration or a rescue charge.

Check matching and mixed totals against native accessories at the same item
tier. Include Apprentice's stat bonus and the native stat cap. Keep the five
starting equipment grants unchanged; new accessories enter ordinary acquisition.
If a theme bonus is already capped, redistribute its budget before implementing
more unused stat points. A necklace should be a worthwhile alternative to a
native necklace, without needing to outclass every native find.

## 2. Accessory rows: implemented offline

- Confirm the final slot/type and template from [Native baseline](NATIVE-BASELINE.md).
- Create six stable `paladin_trinket_*` and six `paladin_necklace_*` entries.
- Supply complete private modifiers, original names/icons/display maps and
  explicit item-level, rarity, stock, market, drop, Lore and DLC fields.
- Keep all-class equipment and class-independent loot eligibility. Preserve
  native rarity normalization; do not manufacture copies or alter enemy drops.
- Update validators and the dedicated inventory fixture to 51 equipment and
  54 production rows only after all rows exist. The fixture's extra class must
  remain excluded from the production package.

No public schema extension is currently needed. If a later design introduces
maximum Focus or another unsupported stat, revise the plan and validate that
capability before writing unsupported JSON fields.

## 3. Original accessory art: implemented offline

Follow [Art direction](ART-DIRECTION.md). The Astra High specialist owns creative
modeling and revisions. Keep sources, palette inputs, piece maps, reproducible
exports and provenance with the campaign. Inspect original icons and exported
objects before integration, then verify native display bindings. An accessory
does not need a new character attachment when the game does not render one.

Package completeness requires every referenced file and no copied native
geometry, vanilla fallback portrayed as final art, or undeclared extra renderer.
Do not use an image-generation concept as evidence of a working 3D object.

## 4. Verify the full loadout: paused before game testing

| Gate | Required observations |
| --- | --- |
| Rows and modifiers | Exactly 12 new IDs; correct categories; only listed stats; no template HP/Focus/passive leakage |
| Equipping | One Trinket and one Neck item; replacement/removal restores correct stats; both hammer routes and mixed outfits work |
| Class restrictions | Native classes can use ordinary accessory stats; equipping one never grants Guard or legendary effects |
| Art | Original icons and complete native shop/item/loot displays; no attempted body replacement |
| Acquisition | Ordinary eligible shops/loot with and without a Paladin, no class-dependent stock, no excessive pool displacement |
| Persistence | Backpack and equipped accessories survive save/exit/fresh-process resume; appropriate missing-mod handling |
| Combat regression | Guard, Focus healing, rescue and all three artifacts retain their existing rules with either or both accessories |
| Balance | Low/mid/high tiers, native alternatives, matching/mixed branches, physical/magic foes and difficulty stat caps |
| Delivery | Updated archive validated; install/off/on/uninstall/reinstall/next-launch metadata tested with exact final hashes |

Start with data/model checks and focused stat tests. Continue through native
single-player checks, then record multiplayer and other-platform outcomes
separately. A shop listing or fixture grant alone does not prove ordinary drops.

## 5. Update the release candidate without overstating completion

When the rows and art exist, rebuild the unpublished 1.0.0 candidate with its new
content-addressed archive name. Record the new identity; earlier receipts still
belong to their original bytes. If publication occurs first, follow the
repository versioning policy rather than replacing already published files.

Update package documentation and marketplace copy to mention accessories only
after implementation. Keep the release, sanctum, acquisition, visual, save and
co-op gates explicit. The source implementation closes the missing-data and original-art production
gaps. Offline checks do not close native registration, visual fit, gameplay,
acquisition, persistence or release gates.
