using System.Collections.Generic;
using UnityEngine;
using GridEditor;

namespace FTKModFramework
{
    /// <summary>
    /// The Thief's "Steal" combat behaviour. The chance to land is the proficiency's flat
    /// m_ChanceToAffect (set on the data row, no scaling) and it uses a single slot, so it's one roll
    /// with unlimited uses. On a successful application it does a 50/50: lift a random item from the
    /// target enemy's loot table, or grab gold (half the Entertain payout).
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

            // AddToDummy runs on EVERY client (it's reached via the [PunRPC] AddProfToDummy / combat
            // resolution), so only the master actually GRANTS the reward — ChangeGold already broadcasts to
            // all clients, and the item is added once. Every client still sets its own HUD below so the steal
            // text shows everywhere. (In single-player isMasterClient is true, so the same path runs.)
            bool grant = PhotonNetwork.isMasterClient;

            bool stoleItem = false;
            int amount;

            // 50/50: lift an item off the target's loot table, or grab gold.
            if (Random.Range(0, 2) == 0 && _dummy is EnemyDummy)
            {
                FTK_itembase.ID item = RollEnemyLootItem((EnemyDummy)_dummy);
                if (item != FTK_itembase.ID.None)
                {
                    if (grant) attacker.m_CharacterOverworld.AddItemToBackpack(item); // _hud:true -> shows the pickup
                    amount = (int)item;
                    stoleItem = true;
                    Plugin.Log.LogInfo("[Thief] Steal lifted item '" + item + "' from the enemy.");
                }
                else
                {
                    amount = GiveGold(stats, grant); // enemy had nothing liftable
                }
            }
            else
            {
                amount = GiveGold(stats, grant);
            }

            // Match the HUD label to the outcome. The steal text is formatted by the proficiency's CATEGORY:
            // StealItem renders m_ProficiencyAmount as an item NAME, StealGold renders it as a GOLD number.
            // Leaving it StealGold while storing an item id is what printed the item's id (~100000+) as a
            // giant "stole N gold" number above the enemy (no gold actually moved).
            m_Category = stoleItem ? ProficiencyBase.Category.StealItem : ProficiencyBase.Category.StealGold;

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

        private static int GiveGold(CharacterStats stats, bool grant)
        {
            // Half of the Entertain payout: Entertain = Random(2..4) * (level+1) * GoldModifier.
            int gold = FTKUtil.RoundToInt((float)(Random.Range(2, 5) * (stats.m_PlayerLevel + 1)) * stats.GoldModifier * 0.5f);
            if (gold < 1) gold = 1;
            if (grant) stats.ChangeGold(gold); // _hud:true by default; ChangeGold itself broadcasts to all clients
            Plugin.Log.LogInfo("[Thief] Steal grabbed " + gold + " gold (level " + stats.m_PlayerLevel + ").");
            return gold;
        }
    }
}
