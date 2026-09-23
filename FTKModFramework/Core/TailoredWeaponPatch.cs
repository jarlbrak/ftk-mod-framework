using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Native tailored rewards exclude common weapons and index the resulting pool without
    // checking its size. A custom class may introduce a primary stat with only common weapons
    // at its current tier. Preserve every populated native pool and its random draw.
    [HarmonyPatch(typeof(GameLogic), "GetTailoredWeaponItem")]
    internal static class TailoredWeaponPatch
    {
        internal static bool Prefix(CharacterOverworld _cow, ref FTK_itembase.ID __result)
        {
            try
            {
                if (_cow == null) return true;
                FTK_playerGameStart classRow = _cow.GetDBEntry();
                int classId;
                if (classRow == null || !ContentRegistry.TryGetSyntheticId(classRow.m_ID, out classId, typeof(FTK_playerGameStartDB)))
                    return true;

                int level = GameLogic.Instance.GetGameDef().GetGameStage().GetCurrentProgressionTierDB().m_ItemLevel + 1;
                List<FTK_itembase.ID> common = new List<FTK_itembase.ID>();
                foreach (FTK_weaponStats2 weapon in FTK_weaponStats2DB.GetDB().m_Array)
                {
                    if (weapon == null || weapon._skilltest != classRow.m_PrimaryWeaponStat ||
                        level < weapon.m_MinLevel || level > weapon.m_MaxLevel) continue;
                    FTK_itembase.ID id = FTK_itembase.GetEnum(weapon.m_ID);
                    if (!FTK_itemsDB.GetDB().IsItemUnlocked(id)) continue;
                    if (weapon.m_ItemRarity != FTK_itemRarityLevel.ID.common) return true;
                    common.Add(id);
                }
                if (common.Count == 0) return true;
                // Use the native reward RNG once, after discovering the complete fallback pool.
                __result = common[UnityEngine.Random.Range(0, common.Count)];
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Tailored weapon fallback unavailable: " + e.Message);
                return true;
            }
        }
    }
}
