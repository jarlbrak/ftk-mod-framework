# Paladin 1.3.0

For the original For The King. Requires FTK Mod Framework
**1.2.0** or a compatible later 1.x release. This is a minor Paladin content
release; package identity remains `ftkmf.paladin` / `com.ftkmf.paladin`.

## Class, weapon and trinket abilities

- **Paladin class:** Guard protects another ally and retains the Guardian support
  rules. Cleansing March prevents new Poison and Curse while exploring, including
  hazardous tiles. It does not cleanse existing conditions, block combat ailments,
  or prevent fire damage and other chaos-tile losses.
- **All 14 Paladin hammers:** Censure deals 0.75 times weapon damage. Each
  successful debuff application selects Armor or Resistance reduction with equal
  probability: four for one-handed hammers or six for two-handed hammers. Native
  weapon actions remain available, including on the two legendary hammers.
- **All six Paladin trinkets:** Smite is available while equipped. Paladin starts
  with Tin Oath Token equipped. Smite deals magical damage at 0.25 times current
  weapon damage and uses that weapon's rolls. Against an enemy with an active
  negative Censure Resistance effect, its multiplier rises sixfold to 1.5 times
  weapon damage before native mitigation and other modifiers.

Smite is equipment-granted, not a class action. Other resistance reductions and
Censure's Armor outcome do not independently enable its bonus. Successive
Censure casts can leave both defense effects active. Native expiry remains;
a follow-up turn before expiry is not guaranteed. Smite consumes no mark, and
perfect rolls are not required for damage.

## Art and retained balance

The overhaul supplies original equipment meshes and textures across 51 items,
including six progression families and three Artifacts. Guard, Censure and Smite
have original action symbols. Ordinary Attack inherits the native shared
weapon-family glyph rather than displaying an equipment studio render.
Oathkeeper and Censure helmet placement was corrected after native review.

The 1.2.0 balance is retained: Vitality 80 and Highward, Mercy, Censure and Verdict
great-hammer base damage of 32, 34, 34 and 37. Existing acquisition declarations
and legendary Guardian bonuses remain. Controlled test inventories do not prove
ordinary acquisition rates or long-campaign balance.

## Compatibility and evidence limits

The listing is restricted to macOS and game assembly SHA-256:

```text
94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8
```

Observed checks include female tier previews, all six male armor tiers in the
native inventory, selected combat and action UI,
Censure outcomes and Smite's conditional damage, normal starting equipment, and
action ownership changing with equipped weapon/trinket combinations. These do
not establish universal visual or lifecycle acceptance. Full animation and
skinset coverage, complete item-display and detached-fragment behavior,
long-campaign balance, online co-op, Windows, Linux, and other game fingerprints
remain unverified or incompletely covered. Fixture-assisted combat captures are
behavior evidence, not normal-play balance evidence.

The [native release evidence](../evidence/paladin-1.3.0/README.md) includes
screenshots from the final framework build. The marketplace preview is a native
inventory screenshot of Censure armor with an Oathkeeper hammer and Novice shield.

The [source validator](../../marketplace/packages/validate_paladin.py) checks
57 unique entries, ownership contracts, retained balance and acquisition,
exact assets and renderer routes. Archive validation, public download hashes,
and installation lifecycle checks remain separate from those source checks.
The [Paladin release](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/paladin-v1.3.0)
is public. Archive and preview downloads were independently hash-verified before
catalog inclusion. Public game installation is a separate follow-up check.

## Artwork provenance and rights

Models and atlases were generated with Hyper3D Rodin from original concepts,
then fitted and exported with Blender. Action artwork is original. Native
bodies, faces, hair and backpacks remain game-owned and are not redistributed as
new original assets. Runtime asset hashes are pinned in the
[provenance record](../../marketplace/packages/paladin-assets.provenance.json).
The full concept, raw generation and editable source campaign is retained
separately; ledger source paths are not promises that those files are bundled.

On 2026-09-25 the author confirmed that the Creator plan was active when these
models were generated. This is author confirmation, not provider-verified
account evidence. Official [Hyper3D terms](https://hyper3d.ai/legal/terms),
including Rodin section 5(b) and applicable restrictions, were reviewed. Code
and declarative data retain MIT licensing; generated artwork has the separate
[asset notice](../../marketplace/packages/paladin/ASSET-LICENSE.md), which must
accompany the release and relevant redistribution. This is not a blanket MIT
relicensing of generated assets or third-party rights.
