# Roadmap

Five content goals → classes, items, combat actions, enemies, adventures, plus FTK2-inspired ports.
Strategy: a generic DB-injection core with a typed `Content.AddX` helper per content kind on top.
(All five goals now work and are verified in-game solo; co-op verification of adventures and the
FTK2 ports are the active fronts.)

| Phase | Goal | Key work | Status |
|---|---|---|---|
| **0. Recon** ✅ | Ground every unknown | Decompile `Assembly-CSharp`; map all `FTK_*DB` tables incl. enemies/adventures | done (`docs/PHASE0-TYPE-INVENTORY.md`) |
| **1. Core** ✅ | Generic injection engine | `ContentRegistry`, `IdAllocator`, `DbLookupPatcher`, `TableManager.Initialize` hook | done, verified in-game |
| **2. Items + actions** ✅ | Goals 2 & 3 | `Content.AddItem`/`AddWeapon`/`AddProficiency`/`AttachProficiencies`; `EnumPatches` + `DbLookupPatcher` + `GetItemBase` routing; `Localization` (names + tooltips) | done: custom weapon casts a custom ability in-game |
| **4. Classes** ✅ | Goal 1 | `Content.AddClass` (id == array index); reused skinset; character skills; class-name + flavor patches; custom `ProficiencyBase` behaviours | done: the **Thief** (stats, dagger, Backstab/Sinister Strike/Eviscerate, Focus-guaranteeable Steal) |
| **3. Enemies** ✅ | Goal 4 | `Content.AddEnemy`/`AttachEnemyProficiencies` over `FTK_enemyCombatDB`; `GameCache.Enemies.NeedsRebuild` spawn injection (no selection patch); `FTK_enemyCombat.GetEnum` + enemy-name patches; `m_ChanceToProf` AI; master-guarded ability behaviour | done: the **Cutpurse** (custom stats, a gold-stealing Pilfer, custom loot; spawns + fights + drops in real combat) |
| **5. Adventures** 🟡 | Goal 5 (hardest) | `Content.AddEncounter` (inject `FTK_miniEncounterDB` rows) + `Adventures.AddFromTemplate` (clone a `.ftk2` `GameDefinition` at runtime, whitelist via `IsValidSaveFileName` patch); `AddCampaignFromTemplate` (branching questlines, flags, custom verbs; `docs/CAMPAIGNS.md`); `CanUseClass` char-create guard | D1 done (solo, verified): cloned **"Smuggler's Run"** plays; the bespoke realm + boss **"The Hollow Mire"** plays to victory (driven by the agent harness). D2 next: 2-client co-op parity + save round-trip |
| **6. FTK2 ports** 🟡 | Inspiration | FTK2 passives/status-effects/summons as data-driven traits (Groups A→C); recreate art originally | in progress: passive traits shipped (`Content.AddPassive`, trigger patches, the **Innkeeper** sample); combat status effects authored as a clone-and-register recipe with no new API (the **Hoarfrost Maul**: Frozen and Warding Roar, spec #85, PR #119): the Warding Roar showcase is live-verified on macOS, the Frozen checks remain outstanding; summons next (epic #77) |
| **Custom 3D models** 🟡 | (cross-cutting) | Editor-free GLB authoring; strict enemy, resource-prefab, and player-skinset renderer APIs; route-specific live validation (`docs/CUSTOM-MODELS.md`) | pipeline shipped; all 48 supported topology groups have canonical route representatives, while per-model art approval and broader state, equipment, lifecycle, and co-op validation remain incremental |

### Cross-cutting (touches every phase)
- **Determinism / saves / co-op:** synthetic ids stable across machines (`IdAllocator`); set
  `SerializeEnumsAsInteger`; mod-set compatibility check between host/client.
- **Asset pipeline:** AssetBundles must be built in Unity **2017.2.2**. OBJ/PNG fallbacks for simple cases.
- **FTK2 legal:** reference-only; never redistribute FTK2 art/JSON. Recreate originally.

### Lessons banked
- **Classes need id == array index** (sequential), not the high-band synthetic id; character-select uses the id as
  both an enum key and an array index. `ContentRegistry.Register(..., explicitId)` handles this.
- **Difficulty applies a flat `m_StatBonus` to every class equally** (Low/Apprentice +5, Medium 0, High/Master 0):
  there is no per-class per-difficulty table, so one stat block per class is correct everywhere.
- **Custom combat behaviour** = subclass `ProficiencyBase`, override `AddToDummy`, set it as the row's
  `m_ProficiencyPrefab`. 0-damage hits are auto-cancelled unless `m_Harmless`; whether the slot roll gates the effect
  is the separate `m_FullSlots` field (true requires a perfect roll). To make the *roll* the gate on a hostile effect,
  use a tiny `m_IgnoresArmor` chip; on a self-targeted (`m_TargetFriendly`) row use `m_Harmless`, never the chip.
- **Combat statuses need no new code**: duration is `FTK_proficiencyTable.m_RepeatCount` on the row, ticked by
  `CharacterDummy.UpdateProficiency`, refreshed (not stacked) by Category. Clone a vanilla status row through
  `Content.AddProficiency` (`docs/WRITING-CONTENT.md` §5.1).

### Remaining risks
1. 2-client co-op for custom adventures/campaigns is designed-for but unverified: the overworld
   map-sync mechanism and a host/client mod-set parity check are open (Adventures Slice D2).
2. Custom 3D model support is route-specific. Documented player skinsets and enemy/resource routes
   have strict APIs and canonical representatives, but one result does not transfer to another
   skinset, renderer path, controller, equipment combination, or authored model. See the
   [skeleton and route register](MODEL-SKELETONS.md).

Architecture, the capability matrix, and the FTK2-ports backlog are tracked as epics and specs in
[GitHub Issues](https://github.com/jarlbrak/ftk-mod-framework/issues).
