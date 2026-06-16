# Roadmap

Five content goals → classes, items, combat actions, enemies, adventures, plus FTK2-inspired ports.
Strategy: a generic DB-injection core (done in v0.1) with a typed helper per content kind on top.

| Phase | Goal | Key work | Done-when |
|---|---|---|---|
| **0. Recon** ✅ | Ground every unknown | Decompile `Assembly-CSharp`; map all `FTK_*DB` tables incl. enemies/adventures | `docs/PHASE0-TYPE-INVENTORY.md` exists |
| **1. Core** ✅ | Generic injection engine | `ContentRegistry`, `IdAllocator`, `DbLookupPatcher`, `TableManager.Initialize` hook; builds | sample item compiles & registers |
| **2. Items + actions** | Goals 2 & 3 | `CustomItem` (writes `FTK_items` + `FTK_weaponStats2`), `CustomProficiency`, hit-effect mapping, AssetBundle icons, localization | a custom weapon with a brand-new ability in shops/loot |
| **3. Enemies** | Goal 4 | `CustomEnemy` over `FTK_enemyCombatDB`; proficiencies + AI tendency; encounter spawn injection; co-op spawn sync | a new enemy spawns, fights, drops custom loot, stays synced |
| **4. Classes** | Goal 1 | `CustomClass` over `FTK_playerGameStartDB` + `CustomSkinset` (model/portrait) + character skills; deterministic class id | a new class selectable at character-select with model + skills |
| **5. Adventures** | Goal 5 (hardest) | `CustomRealm`/encounter injection via `FTK_realmDB` + encounter draw tables; map/seed + save compatibility | a selectable adventure variant, solo + co-op, no desync/corruption |
| **6. FTK2 ports** | Inspiration | Implement FTK2 passives/status-effects/summons as data-driven traits (Groups A→C); recreate art originally | 3–5 FTK2-style classes/abilities shipped, no FTK2 assets reused |

### Cross-cutting (touches every phase)
- **Determinism / saves / co-op:** synthetic ids stable across machines (`IdAllocator`); set
  `SerializeEnumsAsInteger`; mod-set compatibility check between host/client.
- **Asset pipeline:** AssetBundles must be built in Unity **2017.2.2**. OBJ/PNG fallbacks for simple cases.
- **FTK2 legal:** reference-only; never redistribute FTK2 art/JSON. Recreate originally.

### Known risks
1. Whether `FTK_*.ID` enums tolerate synthetic ints everywhere (proven for items/classes; verify per DB).
2. Status-effect duration encoding (likely on the `FTK_hitEffect` prefab) — needs a trace.
3. Custom 3D voxel models may need IronOak's rig/avatar conventions.
4. Adventure/world generation is the least-mapped system — scope to a realm/encounter variant first.

Full architecture, capability matrix, and FTK2 backlog live in the Ninum Knowledge project
**"For The King Mod Framework."**
