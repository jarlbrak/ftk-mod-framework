using System;
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
            CutpurseStealProficiency stealBehaviour =
                (CutpurseStealProficiency)BehaviorHost.Create(typeof(CutpurseStealProficiency), "ftkmf_CutpurseStealProf");
            stealBehaviour.m_Category = ProficiencyBase.Category.StealGold; // gold-only: set once, never mutated

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

                    // Custom loot. AddEnemy already deep-copies the cloned row's m_ItemDrops before this
                    // lambda runs, so we can mutate its fields directly without rewriting vanilla bandit loot.
                    e.m_ItemDrops._golddrop = 25;       // it steals coin, so it drops coin
                    e.m_ItemDrops._itemdropcount = 1;
                    e.m_ItemDrops._itemdropchance = 0.5f;
                    e.m_ItemDrops.m_AlwaysDropItems = new FTK_itembase.ID[] { FTK_itembase.ID.conLockpicks }; // a rogue's tool
                });

            // 3) Give the Cutpurse its Pilfer action (its own private weapon copy; vanilla bandits untouched).
            Content.AttachEnemyProficiencies(cutpurse, "ftkmf_cutpursesteal");

            VerifyCutpurse();
        }

        /// <summary>
        /// Self-test: prove the Cutpurse resolves by id, has its name, passes the spawn-pool filter, is
        /// actually IN the level-bucketed spawn pool (the load-bearing spawn-injection claim), exposes its
        /// Pilfer action on its weapon (via the game's own instantiate path), and carries custom loot.
        ///
        /// ROBUSTNESS (kb_7c3a82a3): this runs in the TableManager.Initialize postfix at plugin load, BEFORE
        /// every GridEditor DB child prefab is guaranteed instantiated/hydrated. The deterministic content
        /// checks (id, name, not-boss, model, loot) only need the enemyCombat DB we just registered into, so
        /// they always run. The ENVIRONMENT-dependent checks (enemy-scale lookup, forced spawn-pool rebuild,
        /// weapon instantiate) re-resolve other DBs via TableManager.Instance.Get&lt;T&gt;(), which can return
        /// null at early load and previously NRE'd intermittently, aborting registration. Each is now
        /// best-effort + null/throw guarded and reported as "deferred" when a table is not yet ready — never an
        /// uncaught NRE. A check that DID run and FAILED (e.g. genuinely not in the pool) still fails the test;
        /// only a not-ready table is treated as deferred (the content is correct regardless).
        /// </summary>
        private static void VerifyCutpurse()
        {
            FTK_enemyCombatDB db = Content.Db<FTK_enemyCombatDB>();
            int id = db.GetIntFromID("ftkmf_cutpurse");
            FTK_enemyCombat e = db.GetEntry((FTK_enemyCombat.ID)id);

            string name = "(null)";
            try { if (e != null) name = e.GetEnemyDisplay(); } catch { }

            // Deterministic content checks (need only the just-registered enemyCombat row).
            bool notBoss = e != null && !e.m_IsBoss && !e.m_IsScourge;
            bool hasModel = e != null && e.m_EnemyAsset != null;          // the cache null-checks this
            bool hasLoot = e != null && e.m_ItemDrops != null;

            // Env-dependent check 1: not enemy-scaled. FTK_enemyScaleDB.GetDB() can be null at early load.
            bool notScaled = false, scaleChecked = false;
            try
            {
                FTK_enemyScaleDB scaleDb = FTK_enemyScaleDB.GetDB();
                if (e != null && scaleDb != null) { notScaled = !scaleDb.IsContainID(e.m_ID); scaleChecked = true; }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy] scale check deferred (tables not ready at load): " + ex.Message); }

            // Env-dependent check 2: actually IN the level-bucketed spawn pool. The forced rebuild walks the
            // whole enemy table and re-resolves DBs per row, so it can throw if a table is not hydrated yet.
            int poolCount = -1; bool inPool = false, poolChecked = false;
            try
            {
                GameCache.Cache.Enemies.NeedsRebuild = true; // static field; safe
                List<FTK_enemyCombat> pool = e != null ? GameCache.Cache.Enemies.GetFromAll(e.m_EnemyLevel) : null;
                if (pool != null) { poolCount = pool.Count; inPool = pool.Contains(e); poolChecked = true; }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy] spawn-pool check deferred (tables not ready at load): " + ex.Message); }

            // Env-dependent check 3: weapon exposes Pilfer. Needs the proficiency DB (can be null at early load).
            int profCount = 0; bool weaponHasSteal = false, weaponChecked = false;
            try
            {
                FTK_proficiencyTableDB profDb = TableManager.Instance != null ? TableManager.Instance.Get<FTK_proficiencyTableDB>() : null;
                if (e != null && profDb != null)
                {
                    int stealId = profDb.GetIntFromID("ftkmf_cutpursesteal");
                    weaponHasSteal = WeaponExposes(e, stealId, out profCount);
                    weaponChecked = true;
                }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("[enemy] weapon-proficiency check deferred (tables not ready at load): " + ex.Message); }

            // Content correctness = the deterministic checks. An env check fails the test ONLY if it actually
            // ran and came back wrong; a not-ready (deferred) table never fails it.
            bool contentOk = e != null && name == "Cutpurse" && notBoss && hasModel && hasLoot;
            bool envOk = (!scaleChecked || notScaled) && (!poolChecked || inPool) && (!weaponChecked || weaponHasSteal);

            if (contentOk && envOk)
                Plugin.Log.LogInfo("SELF-TEST PASS [enemy]: Cutpurse id " + id + " in L" + (e != null ? e.m_EnemyLevel : -1) +
                    " spawn pool (" + (poolChecked ? poolCount + " enemies" : "deferred") + "), Pilfer on weapon=" +
                    (weaponChecked ? weaponHasSteal.ToString() : "deferred") + " (" + profCount + " actions), model=" + hasModel +
                    ", notScaled=" + (scaleChecked ? notScaled.ToString() : "deferred") +
                    ", HP=" + (e != null ? e.m_HealthTotal : -1) +
                    ", loot drop gold=" + (e != null && e.m_ItemDrops != null ? e.m_ItemDrops._golddrop : -1) + ".");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [enemy]: name=\"" + name + "\" notBoss=" + notBoss +
                    " model=" + hasModel + " hasLoot=" + hasLoot +
                    " notScaled=" + (scaleChecked ? notScaled.ToString() : "deferred") +
                    " inPool=" + (poolChecked ? inPool.ToString() : "deferred") +
                    " weaponHasSteal=" + (weaponChecked ? weaponHasSteal.ToString() : "deferred") + ".");
        }

        /// <summary>
        /// Replicate the game's path for reading a weapon's actions: instantiate the enemy's m_WeaponAsset,
        /// read its proficiency ids, then destroy the temp instance. Reports the action count and whether
        /// <paramref name="profId"/> is among them. Isolates the Instantiate/Destroy lifecycle in one place.
        /// </summary>
        private static bool WeaponExposes(FTK_enemyCombat e, int profId, out int profCount)
        {
            profCount = 0;
            if (e == null || e.m_WeaponAsset == null) return false;

            Weapon w = UnityEngine.Object.Instantiate(e.m_WeaponAsset);
            List<FTK_proficiencyTable.ID> ids = w.GetProficiencyIDs();
            profCount = ids.Count;
            bool has = ids.Contains((FTK_proficiencyTable.ID)profId);
            UnityEngine.Object.Destroy(w.gameObject);
            return has;
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
