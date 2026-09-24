# Paladin 1.0.1

Paladin is the first published marketplace mod and the reference package for
authoring through FTK Mod Framework 1.0.1. It adds a Vitality-based protector
class, Guard, 51 equipment items, original 3D models and icons. Six equipment
families cover early gear through horizontal endgame choices; three Artifacts
add legendary hammers and a shield. The package author is JarlBrak.

Install the published package from the in-game Mods catalog. Its
[manifest](manifest.json) owns the mod identity, author, version and short
description; [listing metadata](listing.json) supplies marketplace copy and
requirements. The builder combines those files with measured archive facts to
produce the [catalog descriptor](../../catalog.json). The published archive
contains content and original art, without game assemblies or executable code.

The catalog permits installation on macOS, Windows and Linux. The listing owns
this platform allowlist; the builder's `--platform` selects only the local fixture
platform. Installation permission does not establish gameplay validation. The
framework version and game assembly fingerprint checks still apply, so an
unlisted game build remains blocked. Framework release manifests already include
helpers for all three desktop operating systems.

This source shows mod authors how to declare a class, abilities, equipment,
custom models and icons. See [Writing Content](../../../docs/WRITING-CONTENT.md),
[Custom Models](../../../docs/CUSTOM-MODELS.md), and
[Marketplace](../../../docs/MARKETPLACE.md) for the supported contracts. The
[design contract](../../../docs/paladin/DESIGN.md) and
[validation matrix](../../../docs/paladin/VALIDATION.md) retain deeper design
and test evidence.

The original GLB and PNG assets have an
[external provenance receipt](../paladin-assets.provenance.json). Native FTK
character bodies, faces, hair and backpacks remain game-owned; custom gear is
worn over them. The class uses the original game's appearance choices and
normal unlock checks.

The 1.0.0 macOS game trial verified package registration, native new-game and
fresh-process resume, Guard's direct-hit reduction, and controlled loot
collection and cross-class equip. It did not establish natural drop frequency,
every accessory view, full campaign balance, Windows/Linux gameplay or online
co-op. See the [release notes](../../../docs/releases/v1.0.1.md) and
[accessory validation record](../../../docs/paladin/ACCESSORY-VALIDATION.md).

## Validate a source change

Run `python3 marketplace/packages/validate_paladin.py` from the repository
root for structural checks. The [package builder](../build_paladin.py) creates a
content-addressed archive and descriptor; its local validation does not
publish them or prove in-game behavior. Corrections to the published 1.0.1
archive require a new mod version and a new reviewed artifact.
