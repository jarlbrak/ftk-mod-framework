# Launch acquisition and sanctum observation

`launch-acquisition-state` reads an existing isolated single-player world. It
accepts no payload fields. The helper's exact scratch-root and current-session
guards apply, and the operation checks the root for links and requires existing
party, world and sanctum-manager objects.

```sh
python3 tools/ai-model-pipeline/runtime-test/command.py \
  --root "$PWD/scratch/paladin-game" launch-acquisition-state
```

The report contains:

- Native party class IDs, stable database keys and display-name fields.
- All `paladin_` item and weapon rows, their actual enum IDs, row types, categories,
  slots, weapon attributes, rarity, inclusive item-level bands, drop and market
  flags, DLC and collection-lore keys. The count is observed, not assumed to be 36.
- Reference membership in the existing `GameCache.Cache.Items._itemsByCategory`
  cache. This does not initialize or rebuild the cache.
- The native `Sanctum08` row's ignore and spawn values; `Sanctum08` and `Sanctum08E`
  modifier values for Vitality, extra health, health regeneration and maximum health.
- Existing world sanctum POIs, including their IDs, grand/claimed/broken state and
  map indices; the pending overworld `_sanctumsToGenerate` list; and the sanctum
  manager's `m_DungeonSanctumGrandLookUp` source pool and
  `m_AvailableDungeonSanctums` remaining draw list.

Unavailable containers are distinguished from empty ones. An empty dungeon draw
list can precede its first native draw or follow consumption; the source dictionary
is reported separately. An empty pending overworld list does not mean no sanctums
were generated: existing POIs are reported separately. Pool order is preserved
where order belongs to native state. Source dictionary keys are sorted in a local
copy for readable output. Bounds limit database rows, category size, party size
and sanctum pools without modifying those containers.

This operation never draws randomness, generates or refreshes stock, grants an
item, changes stats or devotion, instantiates an object, or forces a drop. It gives
data without an acceptance verdict. Category membership proves presence in that
cache only. It does not prove a native random drop, purchase, collection, sanctum
devotion, or measured loot frequency. Use `town-stock-state` and
`equipment-inventory` alongside actual native gameplay to observe acquisition.

Build without deploying to a running game:

```sh
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/paladin-game" \
  -p:TestManagedDir="$PWD/scratch/paladin-game/PaladinTest.app/Contents/Resources/Data/Managed" \
  -o "$PWD/scratch/paladin-launch-observer-build"
python3 tools/ai-model-pipeline/runtime-test/test_launch_acquisition_state_readonly.py
```

The build and boundary tests are offline evidence. A fresh live observation
requires explicit deployment of this helper into the isolated game copy.
