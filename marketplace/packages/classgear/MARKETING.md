# Blacksmith promotional artwork and draft listing

The unchanged [marketplace banner](promo/blacksmith-banner.png) is a 1920 x 1080
PNG, 1,733,267 bytes, with SHA-256
`bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b`.
It is original AI-generated promotional illustration for Kilnward, not a game
screenshot or evidence that all four tiers have passed visual review.

The composition follows the Paladin Censure banner's scenic arrangement: a
full-body hero on the right, open title space on the left, ivory serif lettering
and a fine gold divider. It reads "BLACKSMITH" and "TEMPER YOUR LEGEND".
The illustration and its provenance below are unchanged by the 0.2.0 redesign.

## Draft card tagline

32 forge-crafted items. Four complete tiers. Brace with a shield, commit to a
heavy swing, or spend a turn to Temper an ally's armor.

## Draft listing description

Build a forge-smith's arsenal from Coalmark through Bellowsworn, Rivetwatch and
Kilnward. Blacksmith Forge Gear adds 32 equipment items, with a one-handed
hammer, two-handed maul, shield, body armor, helmet, boots, necklace and trinket
in every tier. Both weapon paths begin at the earliest item band.

Every class can wear the gear. The native Blacksmith receives piece-specific
passives and three equipment-provided choices: Set Hammer trades damage for
personal Armor while carrying a shield; Overhand trades Armor for a stronger
maul strike; Temper spends a full action to reinforce a living ally or self,
once per combat, from Bellowsworn onward. Temper costs no roll or Focus and
lasts through the target's next two completed turns. Positive protection uses
the strongest applicable increase; Overhand's penalty still applies.

Ordinary weapon attacks remain available, and shields retain native Taunt.
Set Hammer and Overhand use normal weapon checks and Focus. The themed weapon
menus remove inherited Splash, Shockwave and Stun. The package preserves the
native Blacksmith's starting loadout and Steady. It adds no class duplicate,
set-count requirement or resource meter.

Items are configured for shared level-appropriate shop and drop pools without
a Blacksmith-in-party requirement. Their natural appearance rates and overall
balance require current campaign evidence.

**Unpublished 0.2.0 prototype.** This draft depends on unreleased framework
capabilities. The manifest's `frameworkVersion: 1.0.2` denotes a local
development baseline and does not promise public release compatibility.
Nothing has been uploaded to or published in the production catalog.

The current source inventory contains 162 referenced runtime assets: 106 GLBs
and 56 PNG textures/icons, covering all 32 item identities. The
[provenance ledger](assets.provenance.json) records Rodin lineage and selected
Blender conversions. Equipped helmet routes cover all four tiers; body armor
includes declared Cat/Demon variants for the upper three tiers. These are asset
and routing facts, not blanket fit or motion acceptance.

Helmet fit was corrected after user review identified floating headgear missed
by the original review. The updated scoped review uses a vanilla comparison;
small Demon ear intersections comparable to the native Kettle Helm remain. See the
[correction record](../../../docs/blacksmith/HELM-FIT-CORRECTION.md).

Current native evidence includes both weapon paths at all four tiers, native
action costs and Armor behavior, cancellation, 35 static presentations across
seven appearances, and all 32 item cards. Sampled female human maul motion
passed at every tier, plus Kilnward hammer-and-shield motion. Mixed native gear
and Hunter wearability passed without granting Blacksmith-only bonuses or
actions. Stats and prices were retained after native comparisons. Broader
weapon/skin motion, campaign acquisition frequencies, statistical balance and
co-op remain unclaimed. See the
[current validation ledger](../../../docs/blacksmith/REDESIGN-VALIDATION.md) for
the exact coverage and limitations. Windows/Linux coverage,
public framework/launcher compatibility and applicable model export/distribution
entitlement also require release evidence. The earlier
[release pass](../../../docs/blacksmith/FULL-RELEASE-PASS.md) records historical
30-item tests and failures; it does not validate this revised package.

See the [gear ledger](../../../docs/blacksmith/GEAR.md) for exact tier values and
prices, [combat contract](../../../docs/blacksmith/COMBAT.md) for action rules,
and [structured listing](listing.json) for the matching draft scope.

## Provenance

An Astra High agent directed the built-in `image_gen` tool on 2026-09-25 using
the [initial text prompt](promo/PROMPT.txt) followed by a
[style revision prompt](promo/EDIT-PROMPT.txt). Those original outputs remain
archived. On 2026-09-26, a new aligned banner was generated with the built-in
`image_gen` tool using the [alignment prompt](promo/ALIGNED-PROMPT.txt). The
Paladin Censure banner
(`marketplace/packages/paladin/promo/paladin-censure-banner.png`) informed the
composition and art direction; the earlier Blacksmith banner was used only as a
theme and equipment reference. The new source is saved at
[`blacksmith-banner-aligned-source.png`](promo/blacksmith-banner-aligned-source.png).
Its lettering was generated within the illustration; no font file or separately
licensed art was incorporated. The image service's exact underlying model
identifier was not exposed by the tool.

The [first generated source](promo/blacksmith-banner-source.png) and the
[earlier revised source](promo/blacksmith-banner-stylized-source.png) remain
archived as historical outputs. The earlier source and new aligned source are
1672 x 941 pixels. `blacksmith-banner.png` is the final preview upload. The
[export script](promo/export-banner.sh) normalizes the aligned source to 1920 x
1080 without color quantization. Export was performed with ImageMagick
7.1.2-31 Q16-HDRI. The final PNG omits metadata; the source is retained for
provenance. Regeneration through the image service is nondeterministic; export
is repeatable from the saved aligned source with the stated tool version.

| Artifact | SHA-256 |
|---|---|
| `promo/blacksmith-banner-source.png` | `c81f4fd739355539e700e1e253fa27a0d039bc99f14c2c5623df67f433f05ff0` |
| `promo/blacksmith-banner-stylized-source.png` | `6da6b15983da1153f438406f494c235ed4eb77123c82edfdd9796642c606d068` |
| `promo/blacksmith-banner-aligned-source.png` | `431f4f161d3905a7ad17e5fd46f7cbe50afc37d8e9b98da17b2e5cafac48e920` |
| `promo/blacksmith-banner.png` | `bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b` |
| `promo/PROMPT.txt` | `27df71c9b31d1f3df2a31391091e128f452a3d1f1a6273f242f380074915258d` |
| `promo/EDIT-PROMPT.txt` | `089f2841a60db4007eac730371547b09fabb3fcea2276a840f1a5d6f8697ef40` |
| `promo/ALIGNED-PROMPT.txt` | `63b74c2535b6ebb19bbbb84dea4c6b77a6ab0f5ef834adf7e7db98a617af2b21` |
| `promo/export-banner.sh` | `6a24065a1c27c1e70fbca5e9d97bf2e9003d7f741db840bc86f9e25314bdf248` |

## Validation

- Decoded the final PNG with ImageMagick and checked dimensions and byte count.
- Checked exact 16:9 ratio and all three marketplace image limits.
- Visually inspected the aligned source and final export for readable title,
  tagline, separated gear silhouettes, and intact framing against the Paladin
  banner's composition.
- Re-ran the export script and confirmed the same final SHA-256.
- Resolved every relative documentation link to an existing local file.
- Ran `git diff --check` and checked the new text files for trailing whitespace.

No live-game or gameplay validation is claimed by this artwork. The banner has
not been published or attached to a production catalog entry.
