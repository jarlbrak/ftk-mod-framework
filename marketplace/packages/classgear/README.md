# Blacksmith Forge Gear source package

**Unpublished 0.2.0 development prototype.** This package contains 32 equipment
items across Coalmark, Bellowsworn, Rivetwatch and Kilnward, with both weapon
paths available in every tier. It depends on unreleased framework capabilities.
The manifest's `frameworkVersion: 1.0.2` is a local development baseline, not a
verified public compatibility floor. Publishing requires a compatible public
framework and launcher release as well as the remaining acceptance gates.

The native Blacksmith, starting loadout and Steady remain intact. Every class
can equip the items and receive their ordinary stats. Native Blacksmiths gain
piece-specific passives: Vitality on weapons, helmets and necklaces; Armor on
body armor and shields; Speed on boots; Resistance on trinkets.

Blacksmith equipment supplies three action choices:

- **Set Hammer:** a shield-required strike at 75% damage, granting temporary
  Armor on a positive HP hit until the next scheduled own turn.
- **Overhand:** a maul strike at 115% damage, with an Armor penalty through the
  next scheduled own turn even on a miss.
- **Temper:** Bellowsworn and later trinkets grant a full-action, once-per-combat
  reinforcement for a living ally or self through its next two completed turns,
  without a roll or Focus cost.

Set Hammer and Temper use the strongest positive Armor increase. Overhand's
penalty remains additive. The themed weapons have a native basic attack plus
the eligible Blacksmith action; inherited Splash, Shockwave and Stun are removed.
Shields retain native Taunt. See the [combat contract](../../../docs/blacksmith/COMBAT.md)
for exact tier values, cancellation, duration, stacking and equipment rules.

Items are configured for shared level-appropriate shop and drop pools, without
a Blacksmith-in-party, lore-unlock or DLC requirement. Configuration is not a
claim of observed natural purchase/drop rates or a completed balance pass.

## Source inventory

The current [provenance](assets.provenance.json) lists 162 referenced runtime
assets: 106 GLBs and 56 PNG textures/icons. It covers 24 rigid identities and
eight apparel identities. Both early mauls and all four equipped helmet routes
are included. Body armor declares female/male Blacksmith routes, plus Cat/Demon
variants for Bellowsworn, Rivetwatch and Kilnward. Variant declarations and source
validation do not establish native visual acceptance.

The [approved art campaign](../../../art-experiments/blacksmith-forge-rodin/approved-redesign/README.md)
records original Rodin lineage, reused sources and Blender fitting. The original
Kilnward [banner](promo/blacksmith-banner.png) is unchanged promotional
illustration. It is neither a screenshot nor acceptance of all four tiers.

- [Current progression and price ledger](../../../docs/blacksmith/GEAR.md)
- [Marketplace copy and banner provenance](MARKETING.md)
- [Listing metadata draft](listing.json)
- [Content declarations](content.json)
- [Package identity and local baseline](manifest.json)
- [Exact art delivery ledger](../../../art-experiments/blacksmith-forge-rodin/approved-redesign/delivery/manifest.json)
- [Redesigned package builder](../build_classgear_blacksmith_redesign.py)
- [Redesigned package validator](../validate_classgear_blacksmith_redesign.py)

Regenerate the prototype from its complete delivery ledger:

```sh
python3 marketplace/packages/build_classgear_blacksmith_redesign.py \
  --redesign-manifest art-experiments/blacksmith-forge-rodin/approved-redesign/delivery/manifest.json
python3 marketplace/packages/validate_classgear_blacksmith_redesign.py \
  --redesign-manifest art-experiments/blacksmith-forge-rodin/approved-redesign/delivery/manifest.json
```

## Evidence and release gates

This source-package description makes no new live acceptance claim. Native
fit, card framing, actions, cancellation, Focus/full-action costs, appearance
variants, ordinary equipment changes, break behavior, acquisition, balance,
lifecycle and co-op must be tied to the exact current candidate. Windows/Linux
coverage and applicable model export/distribution entitlement also remain
release gates. A local build or helper validator pass does not prove public
compatibility or native gameplay correctness.

The earlier [banner correction](../../../docs/blacksmith/BANNER-MODELS.md) and
[full release pass](../../../docs/blacksmith/FULL-RELEASE-PASS.md) preserve
superseded 30-item evidence, including sampled native fitting, combat and
manual lifecycle checks. Those checks do not accept the 0.2.0 redesign.
The package is not in the production catalog and has no published archive or
download URL.
