# Paladin 1.4.0

Requires FTK Mod Framework 1.2.1 or a compatible later 1.x release.

- The class description is two sentences of flavor, without ability rules.
- Special Abilities shows `Skill: Guard` and `Passive Skill: Cleansing March`.
- New Paladins start with **Novice Hammer and Novice Aegis**. Plate, boots,
  helmet and Tin Oath Token must be acquired during play.
- Censure remains on the starting hammer. Smite becomes available when a
  Paladin trinket is acquired and equipped.

Base stats, starting gold, combat mechanics, equipment properties, acquisition
settings, content IDs and artwork are unchanged. Existing characters keep their
equipment; this update does not implement an inventory migration.

The installed-data comparison covered all thirteen released native classes.
None starts with a full armor loadout. Nine start with a weapon and one extra
item, including Blacksmith's hammer and shield. See the
[revision notes](../paladin/RELEASE-1.4.0.md) for the comparison and verification.

## Evidence and limitations

Source and archive validation, class UI tests, Guardian combat regression tests
and the framework Release build pass. The Steam test deployment loaded
framework 1.2.1 and registered all 112 enabled content entries with zero content
errors or warnings. Its data-content determinism check passed. This is loading
evidence, not comprehensive visual, save/resume or campaign-balance acceptance.

The package remains available for Windows, macOS and Linux on its listed game
fingerprint. Gameplay evidence is macOS-only; Windows/Linux gameplay and online
co-op remain unverified. Full avatar/animation coverage and ordinary acquisition
frequency remain incomplete. Artwork is unchanged from 1.3.0 and retains the
[original provenance](../../marketplace/packages/paladin-assets.provenance.json)
and [separate asset terms](../../marketplace/packages/paladin/ASSET-LICENSE.md).
The existing equipment preview depicts acquired Censure gear, not the starting kit.
