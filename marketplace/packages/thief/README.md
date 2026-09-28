# Thief 1.1.0 playtest candidate

This candidate registers the selectable Thief and 45 equipment pieces: seven paired-dagger sets, seven compact flintlock pistols, seven coats, seven hoods, seven pairs of boots, seven charms, and three artifact weapons. The Street dagger pair, coat, hood, and boots are starting equipment. The coats, boots, ordinary daggers, and pistols use new detailed generated meshes fitted to the native game skeleton and item frames. The hoods, charms, and dagger artifacts retain their earlier original authored art.

Every custom pistol occupies both equipment hands and tests Talent with four checks. Instant Reload restores one round at the start of each Thief combat turn while one is equipped. Other classes can equip the pistol but use the native reload cycle. The pistol damage curve assumes one Thief shot per turn. Fire, Bait Shot, and Deadeye consume ammunition. These are design and offline implementation claims; actual combat timing, display fit, and balance require an isolated live-game trial.

The candidate requires framework 1.2.2 within 1.x. The 1.0.0 bow item IDs remain stable internally; saved custom bows resolve to the corresponding pistol when this candidate loads. Save/resume behavior still needs a live compatibility check. This candidate has not been published or added to the production catalog.

See the [design](../../../docs/thief/DESIGN.md), [equipment progression](../../../docs/thief/EQUIPMENT.md), [combat rules](../../../docs/thief/COMBAT.md), and [validation record](../../../docs/thief/VALIDATION.md). The legacy bundled Thief sample is independent of this package.

## Local release preparation

`listing.json` contains candidate marketplace copy. The [banner](promo/thief-playtest-banner.png) is a promotional illustration. Build with `python3 marketplace/packages/package_thief.py --game-assembly <isolated-assembly> --helper <helper> --fixture`. Only referenced runtime assets enter the deterministic archive; editable Blender sources and provenance stay separate. Artifacts and managed fixture state belong under ignored `scratch/`.
