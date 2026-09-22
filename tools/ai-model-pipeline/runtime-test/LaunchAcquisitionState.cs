using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    static JObject LaunchModifier(FTK_characterModifier.ID id)
    {
        FTK_characterModifier row = FTK_characterModifierDB.GetDB().GetEntry(id);
        if (row == null) return new JObject { {"id", (int)id}, {"present", false} };
        return new JObject { {"id", (int)id}, {"key", row.m_ID}, {"present", true},
            {"vitality", row.m_ModVitality}, {"extraHealth", row.m_ExtraHealth},
            {"healthRegen", row.m_HealthRegen}, {"maxHealth", row.m_MaxHealth} };
    }

    static JArray LaunchSanctumIds(IList<FTK_sanctumStats.ID> source)
    {
        JArray result = new JArray();
        if (source == null) return result;
        if (source.Count > 64) throw new InvalidOperationException("Sanctum pool exceeds observation bound.");
        foreach (FTK_sanctumStats.ID id in source)
            result.Add(new JObject { {"id", (int)id}, {"key", id.ToString()} });
        return result;
    }

    static void LaunchEquipment(JArray output, FTK_itembase[] rows,
        IDictionary<FTK_itembase.ObjectType, List<FTK_itembase>> cache)
    {
        if (rows == null || rows.Length > 4096)
            throw new InvalidOperationException("Item database unavailable or exceeds observation bound.");
        foreach (FTK_itembase row in rows)
        {
            if (row == null || row.m_ID == null || !row.m_ID.StartsWith("paladin_", StringComparison.Ordinal)) continue;
            List<FTK_itembase> category;
            bool categoryAvailable = cache != null && cache.TryGetValue(row.m_ObjectType, out category);
            // Read the already-built cache without initializing or rebuilding its pools.
            category = categoryAvailable ? cache[row.m_ObjectType] : null;
            if (category != null && category.Count > 4096)
                throw new InvalidOperationException("Item category exceeds observation bound.");
            FTK_weaponStats2 weapon = row as FTK_weaponStats2;
            output.Add(new JObject { {"key", row.m_ID}, {"itemId", (int)FTK_itembase.GetEnum(row.m_ID)},
                {"rowType", row.GetType().Name}, {"category", row.m_ObjectType.ToString()},
                {"slot", row.m_ObjectSlot.ToString()}, {"equippable", row.m_Equippable},
                {"weaponStat", weapon == null ? null : weapon._skilltest.ToString()},
                {"rarity", row.m_ItemRarity.ToString()}, {"minItemLevel", row.m_MinLevel},
                {"maxItemLevel", row.m_MaxLevel}, {"dropable", row.m_Dropable},
                {"townMarket", row.m_TownMarket}, {"nightMarket", row.m_NightMarket},
                {"dungeonMerchant", row.m_DungeonMerchant}, {"filterEndDungeon", row.m_FilterEndDungeon},
                {"dlc", row.m_DLC.ToString()}, {"collectionLoreKey", row.m_CollectLoreItemUnlock},
                {"categoryCacheAvailable", category != null},
                {"categoryCacheContainsRow", category != null && category.Contains(row)} });
        }
    }

    JObject LaunchAcquisitionState(JObject command)
    {
        CatalogKeys(command, "id", "session", "op");
        CatalogNoLinks(root);
        RequireSinglePlayer();
        if (FTKHex.Instance == null || FTKHub.Instance == null || GameLogic.Instance.m_SanctumManager == null)
            throw new InvalidOperationException("An existing world, party and sanctum manager are required.");
        if (FTKHub.Instance.m_CharacterOverworlds == null || FTKHub.Instance.m_CharacterOverworlds.Count > 8)
            throw new InvalidOperationException("Party unavailable or exceeds observation bound.");

        JArray heroes = new JArray(), equipment = new JArray(), worldSanctums = new JArray();
        foreach (CharacterOverworld hero in FTKHub.Instance.m_CharacterOverworlds)
        {
            if (hero == null || hero.m_CharacterStats == null) continue;
            FTK_playerGameStart.ID id = hero.m_CharacterStats.m_CharacterClass;
            FTK_playerGameStart row = FTK_playerGameStartDB.GetDB().GetEntry(id);
            heroes.Add(new JObject { {"heroInstanceId", hero.GetInstanceID()}, {"classId", (int)id},
                {"classKey", row == null ? null : row.m_ID},
                {"classDisplayName", row == null ? null : row.m_DisplayName} });
        }
        var cache = typeof(GameCache.Cache.Items).GetField("_itemsByCategory", Statics).GetValue(null)
            as IDictionary<FTK_itembase.ObjectType, List<FTK_itembase>>;
        LaunchEquipment(equipment, FTK_itemsDB.GetDB().m_Array, cache);
        LaunchEquipment(equipment, FTK_weaponStats2DB.GetDB().m_Array, cache);

        var pois = FTKHex.Instance.GetPOIList(MiniHexInfo.MiniHexType.Sanctum);
        if (pois == null || pois.Count > 64) throw new InvalidOperationException("World sanctum list unavailable or exceeds bound.");
        foreach (MiniHexInfo poi in pois)
        {
            MiniHexSanctum sanctum = poi as MiniHexSanctum;
            if (sanctum == null) continue;
            worldSanctums.Add(new JObject { {"id", (int)sanctum.m_ID}, {"key", sanctum.m_ID.ToString()},
                {"instanceId", sanctum.GetInstanceID()}, {"grand", sanctum.m_Grand},
                {"claimed", sanctum.m_SanctumClaimed}, {"broken", sanctum.m_SanctumBroken},
                {"parentIndex", sanctum.m_HexLand == null ? -1 : sanctum.m_HexLand.m_ParentIndex},
                {"index", sanctum.m_HexLand == null ? -1 : sanctum.m_HexLand.m_Index} });
        }
        var remainingWorld = Field(FTKHex.Instance, "_sanctumsToGenerate") as IList<FTK_sanctumStats.ID>;
        var manager = GameLogic.Instance.m_SanctumManager;
        var remainingDungeon = Field(manager, "m_AvailableDungeonSanctums") as IList<FTK_sanctumStats.ID>;
        var dungeonSource = Field(manager, "m_DungeonSanctumGrandLookUp") as IDictionary<FTK_sanctumStats.ID, bool>;
        JArray dungeonPool = new JArray();
        if (dungeonSource != null)
        {
            if (dungeonSource.Count > 64) throw new InvalidOperationException("Dungeon sanctum source exceeds observation bound.");
            var ids = new List<FTK_sanctumStats.ID>(dungeonSource.Keys);
            ids.Sort();
            foreach (FTK_sanctumStats.ID id in ids)
                dungeonPool.Add(new JObject { {"id", (int)id}, {"key", id.ToString()}, {"grand", dungeonSource[id]} });
        }
        FTK_sanctumStats life = FTK_sanctumStatsDB.Get(FTK_sanctumStats.ID.Sanctum08);
        JObject lifeRow = new JObject { {"present", life != null} };
        if (life != null)
        {
            lifeRow["key"] = life.m_ID;
            lifeRow["ignore"] = life.m_Ignore;
            lifeRow["spawn"] = life.m_Spawn.ToString();
        }
        return new JObject { {"ok", true}, {"readOnly", true}, {"frame", Time.frameCount},
            {"gameAssembly", typeof(FTK_itembase).Assembly.FullName},
            {"heroes", heroes}, {"paladinEquipmentCount", equipment.Count}, {"paladinEquipment", equipment},
            {"lifeSanctum", lifeRow},
            {"lifeNormalModifier", LaunchModifier(FTK_characterModifier.ID.Sanctum08)},
            {"lifeGrandModifier", LaunchModifier(FTK_characterModifier.ID.Sanctum08E)},
            {"worldSanctums", worldSanctums}, {"remainingWorldPoolAvailable", remainingWorld != null},
            {"remainingWorldPool", LaunchSanctumIds(remainingWorld)},
            {"dungeonSourcePoolAvailable", dungeonSource != null}, {"dungeonSourcePool", dungeonPool},
            {"remainingDungeonPoolAvailable", remainingDungeon != null},
            {"remainingDungeonPool", LaunchSanctumIds(remainingDungeon)},
            {"scope", "Existing party, database rows, category cache and sanctum pools only. No random draw, generation, stock refresh, item grant, stat or devotion mutation. Pool membership does not establish an acquired item or devotion."} };
    }
}
