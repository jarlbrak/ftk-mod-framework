# Paladin accessory verification

This is the offline validation record for six trinkets and six necklaces added
before Paladin 1.0.0 publication. A later final-package macOS trial registered
all 54 content entries, including the accessories. Individual accessory views,
stats, acquisition and persistence remain unverified. See the
[launch record](LAUNCH-1.0.0.md). The candidate-specific sections below retain
their original evidence scope.

## Content boundary

The production source contains 54 entries: one class, 14 weapons, 37 items and
two proficiencies. That is 51 equipment items, including the twelve accessories.
All original 42 rows are unchanged by value, and the five starting equipment
grants are unchanged. The accessory modifier tables and combined loadout totals
match [Equipment](EQUIPMENT.md).

Accessories use `trinketDefense1` and `amuletVitality1`, complete private modifier
rows and the existing display-only model API. They add no Guardian bonus,
Focus restoration, class restriction, set bonus or new runtime code. The
[Native baseline](NATIVE-BASELINE.md) establishes template and slot facts; it
does not establish the new objects' behavior in game.

## Offline checks

- Accessory definitions match exact approved names, templates, modifiers,
  prices, tiers, market/drop flags, icons and renderer paths.
- The all-gear fixture boundary tests pass: 51 equipment grants, 55 fixture rows,
  production data unchanged and the extra Hunter restricted to the fixture.
- All six matching loadout totals agree with the production modifier rows.
- The game-free PlayerMods test executable passes. Existing harness warnings
  about unassigned UI fields are separate from test failures.
- [Independent art validation](../../art-experiments/paladin-accessories/validation.json)
  passes for all twelve static GLBs: source/binary agreement, finite arrays,
  valid indices and normals, outward winding, closed positive-volume components,
  root-orientation compensation, palette UVs and transparent icon bounds.
- [Art reproducibility](../../art-experiments/paladin-accessories/reproducibility.json)
  matches all 49 GLB/source/piece/icon/palette files across two complete builds.
  The contact sheet and 64-pixel icon strip were visually inspected offline.
- Production validation passes for 54 entries / 51 equipment items. All 175
  asset files match original-source and package provenance; the existing 150
  asset records remain unchanged and the new campaign adds exactly 25 files.
- The current marketplace helper accepts the complete descriptor and archive.
  Two independent package builds produce identical archive bytes. The archive
  contains no fixture class, executable code, game data or local evidence.
- Relative documentation links and `git diff --check` pass.

## Unpublished candidate identity

Archive: `paladin-1.0.0-85aaad10576f.zip`.

SHA-256: `85aaad10576f44e86ed81f047c50170a8982aed31bb770716079a6eccd9dafec`.

Runtime inventory: **177 files**, comprising 175 original GLB/PNG assets,
`manifest.json` and `content.json`. The local draft descriptor is unpublished.
The separate all-gear fixture contains 55 rows and preserves the same 175 asset
files; it is never a release artifact. Neither candidate nor fixture was
installed or activated in a game during this work.

Reproduce the checks with:

```sh
python3 art-experiments/paladin-accessories/validate.py
python3 marketplace/packages/validate_paladin.py
python3 tools/ai-model-pipeline/paladin-gear-fixture/test_build.py
dotnet run --project FTKModFramework/Tests/PlayerMods/PlayerMods.csproj -c Release
git diff --check
```

See the [package build instructions](../../marketplace/packages/paladin/README.md#validate-a-source-change)
and [original-art workflow](../../art-experiments/paladin-accessories/README.md)
for archive and reproducibility commands. Offline checks do not prove native
registration, display fit, balance, acquisition or persistence.

## Required native checks after the pause

1. Load the final candidate and observe all 54 registrations without errors.
2. Inspect all twelve icons and original models in native inventory, item-card,
   merchant and loot views. Verify scale, orientation, framing and no fallback.
3. Equip and remove one Trinket and one Neck item on Paladin and a native class.
   Verify exact stat deltas, replacement behavior and no inherited HP/Focus/skill
   bonus. Repeat matching and mixed loadouts on both hammer routes.
4. Verify normal acquisition without a Paladin and record pool variety; fixture
   grants and offline eligibility are not ordinary drop evidence.
5. Save equipped/backpack accessories, exit and resume in a fresh process.
6. Exercise Guardian and artifact regressions with the new accessories, then
   verify install/off/on/uninstall/reinstall against the final package identity.

Balance, full-campaign coverage, online co-op and other-platform gameplay remain
separate release gates. No offline render or package validation closes them.
