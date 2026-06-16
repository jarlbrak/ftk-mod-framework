using System.Collections.Generic;
using UnityEngine;
using GridEditor;

namespace FTKModFramework
{
    /// <summary>
    /// The Thief's "Steal" combat behaviour. The chance to land is the proficiency's flat
    /// m_ChanceToAffect (set on the data row, no scaling) and it uses a single slot, so it's one roll
    /// with unlimited uses. On a successful application it does a 50/50: lift a random item from the
    /// target enemy's loot table, or grab gold (half the Entertain payout). No damage.
    ///
    /// Wired in as the proficiency row's m_ProficiencyPrefab; ProficiencyManager Instantiate()s it,
    /// calls Init(), and combat calls AddToDummy() when the proficiency applies.
    /// </summary>
    public class ThiefStealProficiency : ProficiencyBase
    {
        public override void AddToDummy(CharacterDummy _dummy)
        {
            CharacterDummy attacker = GetAttacker(_dummy);
            if (attacker == null || !(bool)attacker.m_CharacterOverworld) return;

            // The ~20% slot roll is the gate: AddToDummy only runs when the roll lands (Focus guarantees it),
            // so a landed roll always steals.
            CharacterStats stats = attacker.m_CharacterOverworld.m_CharacterStats;
            int amount;

            // 50/50: lift an item off the target's loot table, or grab gold.
            if (Random.Range(0, 2) == 0 && _dummy is EnemyDummy)
            {
                FTK_itembase.ID item = RollEnemyLootItem((EnemyDummy)_dummy);
                if (item != FTK_itembase.ID.None)
                {
                    attacker.m_CharacterOverworld.AddItemToBackpack(item); // _hud:true -> shows the pickup
                    amount = (int)item;
                    Plugin.Log.LogInfo("[Thief] Steal lifted item '" + item + "' from the enemy.");
                }
                else
                {
                    amount = GiveGold(stats); // enemy had nothing liftable
                }
            }
            else
            {
                amount = GiveGold(stats);
            }

            // Mark a successful steal so the HUD shows the gain instead of "Nothing To Steal".
            if (_dummy.m_DamageInfo != null)
            {
                _dummy.m_DamageInfo.m_ProfHasAmount = true;
                _dummy.m_DamageInfo.m_ProficiencyAmount = amount;
            }
        }

        // Roll the enemy's real loot table (same call the game uses on death) and pick one item.
        private static FTK_itembase.ID RollEnemyLootItem(EnemyDummy enemy)
        {
            try
            {
                if (enemy.m_EnemyCombat == null || enemy.m_EnemyCombat.m_ItemDrops == null) return FTK_itembase.ID.None;
                int level = GameLogic.Instance.GetGameDef().GetGameStage().GetItemLevel();
                List<FTK_itembase.ID> loot = enemy.m_EnemyCombat.m_ItemDrops.GetLootItems(level, false);
                if (loot != null && loot.Count > 0) return loot[Random.Range(0, loot.Count)];
            }
            catch { }
            return FTK_itembase.ID.None;
        }

        private static int GiveGold(CharacterStats stats)
        {
            // Half of the Entertain payout: Entertain = Random(2..4) * (level+1) * GoldModifier.
            int gold = FTKUtil.RoundToInt((float)(Random.Range(2, 5) * (stats.m_PlayerLevel + 1)) * stats.GoldModifier * 0.5f);
            if (gold < 1) gold = 1;
            stats.ChangeGold(gold); // _hud:true by default
            Plugin.Log.LogInfo("[Thief] Steal grabbed " + gold + " gold (level " + stats.m_PlayerLevel + ").");
            return gold;
        }
    }
}
