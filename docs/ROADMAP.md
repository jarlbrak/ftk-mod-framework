# Roadmap

Five content goals → classes, items, combat actions, enemies, adventures, plus FTK2-inspired ports.
Strategy: a generic DB-injection core with a typed `Content.AddX` helper per content kind on top.
(Classes — Phase 4 — shipped ahead of enemies; enemies/adventures are the remaining gaps.)

| Phase | Goal | Key work | Status |
|---|---|---|---|
| **0. Recon** ✅ | Ground every unknown | Decompile `Assembly-CSharp`; map all `FTK_*DB` tables incl. enemies/adventures | done (`docs/PHASE0-TYPE-INVENTORY.md`) |
| **1. Core** ✅ | Generic injection engine | `ContentRegistry`, `IdAllocator`, `DbLookupPatcher`, `TableManager.Initialize` hook | done, verified in-game |
| **2. Items + actions** ✅ | Goals 2 & 3 | `Content.AddItem`/`AddWeapon`/`AddProficiency`/`AttachProficiencies`; `EnumPatches` + `DbLookupPatcher` + `GetItemBase` routing; `Localization` (names + tooltips) | done — custom weapon casts a custom ability in-game |
| **4. Classes** ✅ | Goal 1 | `Content.AddClass` (id == array index); reused skinset; character skills; class-name + flavor patches; custom `ProficiencyBase` behaviours | done — the **Thief** (stats, dagger, Backstab/Sinister Strike/Eviscerate, Focus-guaranteeable Steal) |
| **3. Enemies** ✅ | Goal 4 | `Content.AddEnemy`/`AttachEnemyProficiencies` over `FTK_enemyCombatDB`; `GameCache.Enemies.NeedsRebuild` spawn injection (no selection patch); `FTK_enemyCombat.GetEnum` + enemy-name patches; `m_ChanceToProf` AI; master-guarded ability behaviour | done — the **Cutpurse** (custom stats, a gold-stealing Pilfer, custom loot; spawns + fights + drops in real combat) |
| **5. Adventures** | Goal 5 (hardest) | realm/encounter injection via `FTK_realmDB` + encounter draw tables; map/seed + save compatibility | DBs mapped; not started |
| **6. FTK2 ports** | Inspiration | FTK2 passives/status-effects/summons as data-driven traits (Groups A→C); recreate art originally | backlog in Ninum project |
| **Custom 3D models** | (cross-cutting) | Skinset/voxel art pipeline (Unity 2017.2.2 AssetBundles); current classes reuse existing skinsets | not started |

### Cross-cutting (touches every phase)
- **Determinism / saves / co-op:** synthetic ids stable across machines (`IdAllocator`); set
  `SerializeEnumsAsInteger`; mod-set compatibility check between host/client.
- **Asset pipeline:** AssetBundles must be built in Unity **2017.2.2**. OBJ/PNG fallbacks for simple cases.
- **FTK2 legal:** reference-only; never redistribute FTK2 art/JSON. Recreate originally.

### Lessons banked (see Ninum project for detail)
- **Classes need id == array index** (sequential), not the high-band synthetic id — character-select uses the id as
  both an enum key and an array index. `ContentRegistry.Register(..., explicitId)` handles this.
- **Difficulty applies a flat `m_StatBonus` to every class equally** (Low/Apprentice +5, Medium 0, High/Master 0) —
  there is no per-class per-difficulty table, so one stat block per class is correct everywhere.
- **Custom combat behaviour** = subclass `ProficiencyBase`, override `AddToDummy`, set it as the row's
  `m_ProficiencyPrefab`. 0-damage hits are auto-cancelled unless `m_Harmless` (which then ignores the roll); to make
  the *roll* the gate, use a tiny `m_IgnoresArmor` chip instead.

### Remaining risks
1. Enemy/adventure DB enums: verify synthetic-int tolerance per DB (proven for items/profs/classes).
2. Status-effect duration encoding (likely on the `FTK_hitEffect` prefab) — needs a trace.
3. Custom 3D voxel models may need IronOak's rig/avatar conventions.
4. Adventure/world generation is the least-mapped system — scope to a realm/encounter variant first.

Full architecture, capability matrix, and FTK2 backlog live in the Ninum Knowledge project
**"For The King Mod Framework."**
