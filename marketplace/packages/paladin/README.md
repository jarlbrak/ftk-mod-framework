# Paladin 1.4.0

Release for the original For The King. Requires FTK Mod Framework
1.2.1 or a compatible later 1.x version. Author: JarlBrak.

Paladin is a Vitality-based protector with 51 equipment items across six families
and three Artifacts. This version uses a two-sentence flavor description and
compact native-style ability labels. New characters start with Novice Hammer
and Novice Aegis only; armor and accessories must be acquired during play.
Abilities retain their existing ownership:

- **Class:** Guard protects another ally, and Cleansing March prevents new Poison
  and Curse while exploring. It does not remove existing conditions or prevent
  combat ailments, fire damage, or other chaos-tile losses.
- **Weapon:** Every Paladin hammer grants Censure. A successful debuff application
  randomly lowers Armor or Resistance by four for one-handed hammers or six for
  two-handed hammers. Existing native weapon actions remain.
- **Trinket:** Every equipped Paladin trinket grants Smite. Trinkets, including the Tin Oath
  Token, must be acquired and equipped during play. Smite uses the current weapon's rolls and deals magic damage
  at 0.25 times weapon damage, multiplied by six while the enemy has an active
  negative Resistance effect from Censure. Unrelated debuffs do not enable it.

Censure's two effects can coexist after successive casts. They use native timed
expiry; a follow-up turn before expiry is not guaranteed. Smite consumes no mark.
Perfect rolls are not required for damage. Ordinary Attack keeps the native
weapon-family glyph; custom action symbols are separate from equipment renders.

The upstream 1.2.0 balance is retained: base Vitality 80 and Highward, Mercy,
Censure and Verdict great-hammer damage 32, 34, 34 and 37. Equipment retains its
native acquisition declarations. Controlled test inventories do not establish
ordinary drop frequency or long-campaign balance.

The [1.4.0 revision notes](../../../docs/paladin/RELEASE-1.4.0.md) record the
native class comparison. The Steam deployment loaded framework 1.2.1 and registered all enabled
content without content errors. Visual fit and new-character behavior require
separate observation. The original 1.3.0 art provenance is retained unchanged.

## Compatibility and evidence

The listing is restricted to the observed macOS game assembly fingerprint
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
Windows, Linux, and online co-op are unverified for this overhaul. Historical 1.3.0
observations include female tier previews, corrected helmet placement, selected
combat/action UI, the previous six-piece starter kit, and equipment-dependent ability
swaps. Full skinset fit, all animation/display paths, detached break fragments,
and long-campaign balance are not comprehensively accepted by those checks.

The [manifest](manifest.json) owns runtime identity and framework minimum;
[listing metadata](listing.json) owns marketplace claims. The
[provenance record](../paladin-assets.provenance.json) pins runtime art and source
evidence. Campaign source identifiers in that record refer to separately retained
authoring files, not bundled or downloadable repository paths. The release
contains the runtime assets, not the full generation and Blender source campaign.
Native bodies, faces, hair and backpacks remain game-owned. Read the
[generated artwork notice](ASSET-LICENSE.md) separately from the code/data MIT
license. On 2026-09-25 the author confirmed an active Creator plan during model
generation; the official Rodin terms were reviewed. This account statement was
provided by the author, not independently verified with the provider.

## Validation and publication

Run `python3 marketplace/packages/validate_paladin.py` from the repository root.
It checks balance, acquisition, action ownership, asset hashes, binary structure,
and renderer routes. The [package builder](../build_paladin.py) prepares a
content-addressed archive and descriptor; neither command establishes live
acceptance or publication. A new version and reviewed artifact are required for
corrections to published bytes. Published 1.3.0 archive bytes remain unchanged.

See [Writing Content](../../../docs/WRITING-CONTENT.md),
[Custom Models](../../../docs/CUSTOM-MODELS.md), and
[Marketplace](../../../docs/MARKETPLACE.md) for authoring contracts.

The release preview is a native inventory screenshot of Censure armor with an
Oathkeeper hammer and Novice shield. See the [native release evidence](../../../docs/evidence/paladin-1.3.0/README.md) for all six male armor tiers and combat UI.
