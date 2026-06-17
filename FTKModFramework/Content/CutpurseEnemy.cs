using System.Collections.Generic;
using UnityEngine;
using GridEditor;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// The Cutpurse — a custom enemy and the Thief's foil: a nimble, evasive bandit that occasionally robs
    /// a party member of gold (a custom <see cref="CutpurseStealProficiency"/> behaviour) and drops a little
    /// coin and a set of lockpicks when slain. Demonstrates the full enemy stack: a cloned FTK_enemyCombat
    /// (reusing a bandit's model + weapon so it renders and fights), a custom ability attached to its weapon,
    /// custom stats, and custom loot — and that it actually enters the spawn pool.
    /// </summary>
    internal static class CutpurseEnemy
    {
        public static void Register()
        {
            // 1) The Cutpurse's signature ability: a light, armor-ignoring hit that also steals party gold.
            //    A pure 0-damage proficiency is auto-cancelled, so we give it a real (small) chip of damage.
            GameObject stealPrefab = new GameObject("ftkmf_CutpurseStealProf");
            UnityEngine.Object.DontDestroyOnLoad(stealPrefab);
            stealPrefab.transform.position = new Vector3(0f, -100000f, 0f); // park off-screen, stays active
            CutpurseStealProficiency stealBehaviour = stealPrefab.AddComponent<CutpurseStealProficiency>();
            stealBehaviour.m_Category = ProficiencyBase.Category.StealGold;

            Content.AddProficiency(Plugin.Guid, "ftkmf_cutpursesteal", FTK_proficiencyTable.ID.bladeDamage, "Pilfer",
                p =>
                {
                    p.m_DmgMultiplier = 0.5f;               // a real (if light) hit so it isn't auto-cancelled
                    p.m_IgnoresArmor = true;                // the chip must land regardless of the target's armor
                    p.m_ProficiencyPrefab = stealBehaviour; // custom: steal gold on a successful hit
                });
            Localization.SetProficiencyDescription("ftkmf_cutpursesteal",
                "A quick slash that also lifts some of the party's gold.");

            // 2) The enemy itself: clone a plain humanoid bandit (non-boss, non-scourge, non-scaling,
            //    valid m_EnemyAsset body + m_WeaponAsset). Override stats + give it its own loot table.
            FTK_enemyCombat cutpurse = Content.AddEnemy(Plugin.Guid, "ftkmf_cutpurse", FTK_enemyCombat.ID.banditA, "Cutpurse",
                e =>
                {
                    e.m_EnemyLevel = 1;                 // early-game pool, easy to meet while testing
                    e.m_ArchType = FTK_enemyCombat.EnemyArchType.Evade; // evasive rogue archetype
                    e.m_HealthTotal = 26;               // squishier than a normal bandit
                    e.m_BaseDefPhys = 1;
                    e.m_BaseDefMag = 0;
                    e.m_EvadeRating = 0.20f;            // hard to pin down
                    e.m_ChanceToCrit = 0.10f;
                    e.m_ChanceToProf = 0.5f;            // fires Pilfer ~half its turns
                    e.m_UseFirstProfAsReg = false;      // pick uniformly among all actions (incl. Pilfer)
                    e.m_IsBoss = false;
                    e.m_IsScourge = false;
                    e.m_Rarity = "Common";              // most common draw weight -> easy to encounter
                    e.m_SpawnDay = true;
                    e.m_SpawnNight = true;
                    e.m_SpawnLand = true;
                    e.m_SpawnWater = false;
                    e.m_SpawnDungeon = true;
                    e.m_RealmInclude = new FTK_realm.ID[0]; // empty include => eligible in every realm
                    e.m_RealmExclude = new FTK_realm.ID[0];

                    // Custom loot. The clone shares the template bandit's ItemDrops instance by reference, so
                    // duplicate it into a fresh one before editing (else we'd rewrite vanilla bandit loot).
                    FTK_enemyCombat.ItemDrops drops = new FTK_enemyCombat.ItemDrops();
                    if (e.m_ItemDrops != null) Reflect.CopyFields(e.m_ItemDrops, drops);
                    drops._golddrop = 25;               // it steals coin, so it drops coin
                    drops._itemdropcount = 1;
                    drops._itemdropchance = 0.5f;
                    drops.m_AlwaysDropItems = new FTK_itembase.ID[] { FTK_itembase.ID.conLockpicks }; // a rogue's tool
                    e.m_ItemDrops = drops;
                });

            // 3) Give the Cutpurse its Pilfer action (its own private weapon copy; vanilla bandits untouched).
            Content.AttachEnemyProficiencies(cutpurse, "ftkmf_cutpursesteal");

            VerifyCutpurse();
        }

        /// <summary>
        /// Self-test: prove the Cutpurse resolves by id, has its name, passes the spawn-pool filter, is
        /// actually IN the level-bucketed spawn pool (the load-bearing spawn-injection claim), exposes its
        /// Pilfer action on its weapon (via the game's own instantiate path), and carries custom loot.
        /// </summary>
        private static void VerifyCutpurse()
        {
            FTK_enemyCombatDB db = Content.Db<FTK_enemyCombatDB>();
            int id = db.GetIntFromID("ftkmf_cutpurse");
            FTK_enemyCombat e = db.GetEntry((FTK_enemyCombat.ID)id);

            string name = "(null)";
            try { if (e != null) name = e.GetEnemyDisplay(); } catch { }

            bool notBoss = e != null && !e.m_IsBoss && !e.m_IsScourge;
            bool hasModel = e != null && e.m_EnemyAsset != null;          // the cache null-checks this
            bool notScaled = e != null && !FTK_enemyScaleDB.GetDB().IsContainID(e.m_ID);

            // Is it actually in the spawn pool at its level? This directly verifies spawn injection.
            GameCache.Cache.Enemies.NeedsRebuild = true;
            List<FTK_enemyCombat> pool = e != null ? GameCache.Cache.Enemies.GetFromAll(e.m_EnemyLevel) : null;
            bool inPool = pool != null && pool.Contains(e);

            // Does its weapon expose Pilfer? Replicate the game path: instantiate m_WeaponAsset, read profs.
            int stealId = TableManager.Instance.Get<FTK_proficiencyTableDB>().GetIntFromID("ftkmf_cutpursesteal");
            bool weaponHasSteal = false;
            int profCount = 0;
            if (e != null && e.m_WeaponAsset != null)
            {
                Weapon w = UnityEngine.Object.Instantiate(e.m_WeaponAsset);
                List<FTK_proficiencyTable.ID> ids = w.GetProficiencyIDs();
                profCount = ids.Count;
                weaponHasSteal = ids.Contains((FTK_proficiencyTable.ID)stealId);
                UnityEngine.Object.Destroy(w.gameObject);
            }

            bool hasLoot = e != null && e.m_ItemDrops != null;

            bool ok = e != null && name == "Cutpurse" && notBoss && hasModel && notScaled && inPool && weaponHasSteal && hasLoot;
            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS [enemy]: Cutpurse id " + id + " in L" + e.m_EnemyLevel +
                    " spawn pool (" + (pool != null ? pool.Count : 0) + " enemies), Pilfer on weapon=" + weaponHasSteal +
                    " (" + profCount + " actions), model=" + hasModel + ", HP=" + e.m_HealthTotal + ", loot drop gold=" + e.m_ItemDrops._golddrop + ".");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [enemy]: name=\"" + name + "\" notBoss=" + notBoss +
                    " model=" + hasModel + " notScaled=" + notScaled + " inPool=" + inPool +
                    " weaponHasSteal=" + weaponHasSteal + " hasLoot=" + hasLoot + ".");
        }
    }

    /// <summary>
    /// DEBUG verification aid (Plugin.ForceCustomEnemy): force every overworld LAND enemy the game rolls to
    /// be the Cutpurse, so you can confirm the custom enemy actually fights and drops loot without relying on
    /// the weighted draw. _getRealmSpawnEnemy is the overworld land/sea picker (only reached for non-water via
    /// GetOverworldEnemyEncounter) and the roll is master-authoritative, so overriding its result is co-op
    /// safe — clients receive the chosen id as a string over Photon. Off for normal play.
    /// </summary>
    [HarmonyPatch(typeof(EnemyManager), "_getRealmSpawnEnemy")]
    internal static class ForceCutpurse_Patch
    {
        private static int _cutpurseId = -2; // -2 = not yet resolved

        private static void Postfix(ref FTK_enemyCombat.ID __result)
        {
            if (Plugin.ForceCustomEnemy == null || !Plugin.ForceCustomEnemy.Value) return;
            if (__result == FTK_enemyCombat.ID.None) return; // don't force where the game spawns nothing

            if (_cutpurseId == -2)
                _cutpurseId = TableManager.Instance.Get<FTK_enemyCombatDB>().GetIntFromID("ftkmf_cutpurse");
            if (_cutpurseId >= 0) __result = (FTK_enemyCombat.ID)_cutpurseId;
        }
    }
}
