# Thief 1.0.0 playtest package

This local development package registers the selectable Thief and all 45 designed equipment pieces: seven paired-dagger sets, seven bows, seven each of coats, hoods, boots, and charms, plus three artifact weapons. Eight proficiency variants describe Feint, Draw Out, Pierce, and Thread the Needle across the progression. Street Twins and the Street coat, hood, and boots are starting equipment. Each item has an original icon and original authored model assets. The package is not listed in the production catalog.

The [design baseline](../../../docs/thief/DESIGN.md) specifies the Speed-based two-handed paired-dagger path and unique Thief mechanics. One-handed dagger and buckler is an emergency fallback. The complete [art approval sheet](../../../art-experiments/thief/approval-board/thief-art-approval.png) covers every outfit, weapon, and distinct icon. Offline asset validators and isolated-game registration have passed. Full combat edge cases, ordinary acquisition, save/resume, co-op, and balance remain runtime gates before production release.

See [equipment progression](../../../docs/thief/EQUIPMENT.md), [combat rules](../../../docs/thief/COMBAT.md), and [verification gates](../../../docs/thief/VALIDATION.md). The legacy bundled Thief sample is independent of this package.

## Local release preparation

`listing.json` contains candidate marketplace copy. The [banner](promo/thief-playtest-banner.png) uses the tagline **Strike from the Shadows**. It is a promotional illustration, not a gameplay screenshot. See [playtest notes](../../../docs/thief/PLAYTEST-1.0.0.md).

Build with `python3 marketplace/packages/package_thief.py --game-assembly <isolated-assembly> --helper <helper> --fixture`. Only referenced runtime assets enter the deterministic archive; banner and provenance stay separate. Artifacts and managed fixture state go under ignored `scratch/`. This does not publish or change the production catalog. `build_thief.py` remains the content generator.
