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

            // 50/50: lift an item off the target's loot table, or grab gold. TryStealItem itself falls back
            // to gold for a non-enemy target or an empty loot roll, so the gold path covers every miss.
            bool stoleItem = false;
            int amount = (Random.Range(0, 2) == 0)
                ? TryStealItem(_dummy, attacker.m_CharacterOverworld, grant, out stoleItem)
                : GiveGold(stats, grant);

            // m_Category is a SERIALIZED field on a single SHARED ProficiencyBase instance: the game caches one
            // instance per proficiency id in ProficiencyManager.m_ProficiencyTable, shared across all dummies and
            // applications. This per-hit write is a deliberate, documented exception for the single-row 50/50 Steal:
            // the steal HUD (HitEffect.Play) and the tooltip both decide item-vs-gold by reading m_Category off this
            // shared prefab keyed by _ddi.m_Prof, and DummyDamageInfo has NO category field (it is Photon-serialized,
            // so one cannot be added). StealItem renders m_ProficiencyAmount as an item NAME, StealGold as a GOLD
            // number. It is safe in practice: the write is synchronous immediately before the same-frame HUD read,
            // combat is turn-based single-target melee (no concurrent same-frame Steal), and it mutates only a local
            // presentation field, not combat / id-allocation / wire-serialized state. The vanilla alternative is two
            // separate stable-category rows (ProficiencyStealGold / ProficiencyStealEquippedItem), deferred as a
            // future design change.
            if (_dummy.m_DamageInfo != null)
            {
                m_Category = stoleItem ? ProficiencyBase.Category.StealItem : ProficiencyBase.Category.StealGold;
                _dummy.m_DamageInfo.m_ProfHasAmount = true;     // show the gain instead of "Nothing To Steal"
                _dummy.m_DamageInfo.m_ProficiencyAmount = amount;
            }
        }

        /// <summary>
        /// Item branch of the 50/50: roll the target enemy's loot table and lift one item. Sets
        /// <paramref name="stoleItem"/> and returns the item id (as the HUD amount) on success; on a
        /// non-enemy target or an empty roll, returns gold instead (stoleItem stays false).
        /// </summary>
        private static int TryStealItem(CharacterDummy dummy, CharacterOverworld attacker, bool grant, out bool stoleItem)
        {
            stoleItem = false;
            if (!(dummy is EnemyDummy)) return GiveGold(attacker.m_CharacterStats, grant);

            FTK_itembase.ID item = RollEnemyLootItem((EnemyDummy)dummy);
            if (item == FTK_itembase.ID.None) return GiveGold(attacker.m_CharacterStats, grant); // nothing liftable

            if (grant) attacker.AddItemToBackpack(item); // _hud:true -> shows the pickup
            stoleItem = true;
            Plugin.Log.LogInfo("[Thief] Steal lifted item '" + item + "' from the enemy.");
            return (int)item;
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
            int gold = ProficiencyMath.HalfEntertainGold(stats);
            if (gold < 1) gold = 1;
            if (grant) stats.ChangeGold(gold); // _hud:true by default; ChangeGold itself broadcasts to all clients
            Plugin.Log.LogInfo("[Thief] Steal grabbed " + gold + " gold (level " + stats.m_PlayerLevel + ").");
            return gold;
        }
    }
}
