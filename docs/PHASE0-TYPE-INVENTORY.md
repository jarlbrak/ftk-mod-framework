# Phase 0 — Verified Content-Table Inventory

Decompiled from the local install's `Assembly-CSharp.dll` (Unity 2017.2.2p2, Mono) with `ilspycmd`.
This resolves the previously-unknown enemy/encounter/adventure type names. Everything below is
**confirmed from source**, not inferred.

## How content storage works

- Singleton `GridEditor.TableManager` (`TableManager.Instance`). Each content table is a Unity
  component child, fetched with `TableManager.Instance.Get<T>()` or `.Get(Type)`.
- Each table is `GridEditor.GEDataArray<TRow> : GEDataArrayBase`, where `TRow : GEDataBase`.
  - `public TRow[] m_Array;` — the rows.
  - `public Dictionary<int, TRow> m_Dictionary;` — int-id → row index, built by `MakeIndex()`.
  - `public abstract void AddEntry(string _id);` — appends a blank `new TRow()` with `m_ID = _id`.
  - `public virtual int GetIntFromID(string _id);` — per-DB, usually
    `(int)Enum.Parse(typeof(FTK_*.ID), _id)`, returns **-1** for unknown ids.
  - `public override void CheckAndMakeIndex()` / `public void MakeIndex()` — (re)build `m_Dictionary`.
- Every row has a **string `m_ID`** AND an **int** (an `FTK_*.ID` enum value). The enum is
  compile-time fixed, which is why custom content needs synthetic ints + a `GetIntFromID` patch.

## Registration recipe (what `ContentRegistry.Register` automates)

```
1. mint synthetic int   IdAllocator.Allocate(modGuid, dbType+"/"+id)   // deterministic, >= 0x40000000
2. patch lookups        DbLookupPatcher.EnsurePatched(dbType)          // GetIntFromID resolves our id
3. db.AddEntry(id)                                                     // blank row appended, m_ID=id
4. copy a template row onto it (clone an existing entry's defaults)
5. set the fields you care about
6. db.MakeIndex()                                                      // row now indexed under synthetic int
```

## The 57 content tables (all `GEDataArrayBase`)

### Items / weapons / abilities
- `FTK_itemsDB` → `FTK_items : FTK_itembase` — items & weapons. Key fields on `FTK_itembase`:
  `m_ItemRarity` (`FTK_itemRarityLevel.ID`), `m_MinLevel`/`m_MaxLevel`, `_goldValue`, `_shopStock`,
  `m_TownMarket`, `m_NightMarket`, `m_DungeonMerchant`, `m_Dropable`, `m_DLC`, `m_ObjectSlot`,
  `m_ObjectType`. Item-id enum `FTK_itembase.ID` (regular items start at 100000).
- `FTK_weaponStats2DB` → `FTK_weaponStats2` — `_slots`, `_maxdmg`, `_dmgtype` (`DamageType`),
  `_skilltest` (`SkillType`), etc.
- `FTK_itemRarityLevelDB`, `FTK_proficiencyTableDB` (combat skills/“proficiencies”),
  `FTK_proficiencyCatDB`, `FTK_hitEffectDB` (damage/status visual+effect), `FTK_characterSkillDB`,
  `FTK_characterModifierDB`, `FTK_slotOutputDB`, `FTK_slotOutputMeaningDB`.

### Classes / characters / cosmetics
- `FTK_playerGameStartDB` → `FTK_playerGameStart` — playable classes (stats, start items/weapon,
  focus, skinsets). Stat backing fields are renamed: Strength=`_toughness`, Intelligence=`_fortitude`,
  Speed=`_quickness`, plus `_vitality`/`_talent`/`_awareness`/`luck`.
- `FTK_skinsetDB` (3D model + portrait), `FTK_stoneHeroDB`, `FTK_customizeArmorDB`,
  `FTK_customizeHelmetDB`, `FTK_customizeBackpackDB`, `FTK_talkingHeadDB`, `FTK_ragdollDeathDB`.

### Enemies  ← (was the biggest unknown; now confirmed)
- `FTK_enemyCombatDB` → `FTK_enemyCombat : GEDataBase` — full enemy definition:
  `m_EnemyLevel`, `m_IsBoss`, `m_IsScourge`, `m_HealthTotal`, `m_BaseDefPhys`, `m_BaseDefMag`,
  `m_EvadeRating`, `m_ChanceToCrit`, `m_ChanceToProf`, `_slots`, `_maxdmg`, `_dmgtype`,
  spawn flags `m_SpawnDay/Night/Land/Water/Dungeon`, realm gating `m_RealmInclude/Exclude`
  (`FTK_realm.ID[]`), and a nested loot struct (`_xpdrop`, `_golddrop`, `_itemdropcount`,
  `m_AlwaysDropItems` / `m_PossibleGuaranteedItems` as `FTK_itembase.ID[]`).
- `FTK_enemyCrewDB`, `FTK_enemyScaleDB`, `FTK_enemySetDB` (which enemies group together).

### Adventures / world / encounters  ← (was unknown; now confirmed)
- `FTK_realmDB` → `FTK_realm` — realms/biomes (the backbone of an "adventure").
- `FTK_gameParamsDB`, `FTK_gameDifficultyDB`, `FTK_progressionTierDB` (level scaling).
- `FTK_dungeonEncounterDB`, `FTK_dungeonMiniEncounterDB`, `FTK_miniEncounterDB`,
  `FTK_encounterDrawChanceDB`, `FTK_encounterPropsDB`, `FTK_dungeonDoorDB`, `FTK_dungeonTrapDB`,
  `FTK_boatDB`, `FTK_hexDecayTimeDB`, `FTK_pipeDB`, `FTK_treasureChestsDB`, `FTK_treasureChestSkinsDB`.
- Quests: `FTK_questTemplateDistDB`, `FTK_questTemplateSpecialDB`, `FTK_questDeliveryDB`,
  `FTK_questOneLineDB`, `FTK_chooseRewardDB`, `FTK_deadAdventurerDB`.
- Lore/unlocks: `FTK_loreItemDB`, `FTK_loreCategoryDB`, `FTK_lorePoisDB`, `FTK_loreExtraUnlockDB`.

### Misc/system
- `FTK_dlcDB`, `FTK_achievementDB`, `FTK_statisticDB`, `FTK_MessageDB`, `FTK_townStatsDB`,
  `FTK_sanctumStatsDB`, `FTK_hauntStatsDB`, `FTK_hwSettingDB`, `FTK_utilityDB`.

Relevant runtime systems (not data tables): `EncounterSession` / `EncounterSessionMC`
(combat rounds, loot payout), `Diorama` / `SceneDiorama` (combat scene), `GameLogic`
(`m_MapGenRandomSeed`, `RevealMap`), `CharacterOverworld`, `FTKHub`. Game-mode UI:
`uiScreenStartGameMode`, `uiFTKGameModeButton`.

## Save / multiplayer constraints

- `FullSerializer.fsConfig.SerializeEnumsAsInteger` defaults to **false** — the framework sets it
  **true** so synthetic enum ids serialize as their int and survive save/load.
- Co-op is Photon PUN; there is no asset streaming, so every client needs identical mods, and
  synthetic ids must match across machines (handled by `IdAllocator`'s pure-hash allocation).
