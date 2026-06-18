using System.Collections.Generic;
using UnityEngine;
using GridEditor;
using FTKModFramework.Behaviors;

namespace FTKMF.SampleBehaviorMod
{
    /// <summary>
    /// A FAITHFUL sample steal behaviour shipped by an EXTERNAL mod DLL (com.ftkmf.samplebehaviormod),
    /// to prove the framework's BehaviorLoader external-DLL path (#33) and the FR-9 closing predicate (#34):
    /// a JSON-named, DLL-supplied behaviour is in-game IDENTICAL to the compiled <c>ThiefStealProficiency</c>.
    /// It references ONLY the public <see cref="ContentBehaviorAttribute"/> (from FTKModFramework.dll) and the
    /// stock game types; it touches NO framework Core/ internal (it cannot: those are internal) and NO
    /// main-plugin helper (no Plugin.Log, no ProficiencyMath: those live in the plugin assembly). Logging uses
    /// <c>UnityEngine.Debug.Log</c> and the half-Entertain-gold math is INLINED (a faithful copy of the
    /// framework's <c>ProficiencyMath.HalfEntertainGold</c>) so the sample DLL stays self-contained.
    ///
    /// The framework reflects on the <c>[ContentBehavior("Steal")]</c> attribute and registers this type under
    /// (modGuid + ":Steal"), so the mod's content entry <c>behavior:"Steal"</c> wires it into the proficiency
    /// row's m_ProficiencyPrefab. ProficiencyManager Instantiate()s it; combat calls AddToDummy() on a hit.
    ///
    /// EVERY member this class reaches is PUBLIC in the stock Assembly-CSharp (decompile-verified, #34):
    /// EnemyDummy/EnemyInfo.m_EnemyCombat, FTK_enemyCombat.m_ItemDrops, FTK_enemyCombat.ItemDrops.GetLootItems,
    /// GameLogic.Instance/GetGameDef/GameDefinition.GetGameStage/GameStage.GetItemLevel,
    /// CharacterOverworld.AddItemToBackpack, CharacterStats.ChangeGold/m_PlayerLevel/GoldModifier,
    /// FTKUtil.RoundToInt, PhotonNetwork.isMasterClient, ProficiencyBase.GetAttacker/m_Category. So this sample
    /// does NOT depend on the publicizer the main plugin imports.
    /// </summary>
    [ContentBehavior("Steal")]
    public class SampleStealBehavior : ProficiencyBase
    {
        /// <summary>
        /// In-game identical to <c>ThiefStealProficiency.AddToDummy</c>: on a landed roll (the ~20% slot roll
        /// is the gate, so this only runs when the steal applies), do a 50/50: lift a random item off the
        /// target enemy's loot table, or grab gold (half the Entertain payout, floored at 1). Master-only
        /// grant; every client sets its own HUD; per-hit m_Category = StealItem / StealGold.
        /// </summary>
        public override void AddToDummy(CharacterDummy _dummy)
        {
            CharacterDummy attacker = GetAttacker(_dummy);
            if (attacker == null || !(bool)attacker.m_CharacterOverworld) return;

            CharacterStats stats = attacker.m_CharacterOverworld.m_CharacterStats;

            // AddToDummy runs on EVERY client (it is reached via the combat resolution / [PunRPC] path), so
            // only the master actually GRANTS the reward: ChangeGold already broadcasts to all clients, and the
            // item is added once. Every client still sets its own HUD below. (In single-player isMasterClient
            // is true, so the same path runs.) Mirrors the compiled Thief exactly.
            bool grant = PhotonNetwork.isMasterClient;

            // 50/50: lift an item off the target's loot table, or grab gold. TryStealItem itself falls back to
            // gold for a non-enemy target or an empty loot roll, so the gold path covers every miss.
            bool stoleItem = false;
            int amount = (Random.Range(0, 2) == 0)
                ? TryStealItem(_dummy, attacker.m_CharacterOverworld, grant, out stoleItem)
                : GiveGold(stats, grant);

            // m_Category is a SERIALIZED field on a single SHARED ProficiencyBase instance; this per-hit write
            // is the documented disposition ruling (Approach A): the steal HUD reads item-vs-gold off this
            // shared prefab keyed by m_Prof, and DummyDamageInfo has NO category slot. Mirrors the compiled
            // Thief's same per-hit write exactly.
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
            Debug.Log("[SampleBehaviorMod] Steal lifted item '" + item + "' from the enemy.");
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
            // Inlined copy of FTKModFramework Content/ProficiencyMath.HalfEntertainGold; kept in sync
            // deliberately (this separate assembly cannot reference the plugin-internal helper). Half the
            // Entertain payout: Entertain = Random(2..4) * (level+1) * GoldModifier; halve and round. Floor at
            // 1 so a landed steal always grants something.
            int gold = FTKUtil.RoundToInt((float)(Random.Range(2, 5) * (stats.m_PlayerLevel + 1)) * stats.GoldModifier * 0.5f);
            if (gold < 1) gold = 1;
            if (grant) stats.ChangeGold(gold); // _hud:true by default; ChangeGold itself broadcasts to all clients
            Debug.Log("[SampleBehaviorMod] Steal grabbed " + gold + " gold (level " + stats.m_PlayerLevel + ").");
            return gold;
        }
    }
}
