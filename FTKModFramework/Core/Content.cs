using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// High-level authoring API: clone an existing entry, tweak it, give it a name. Each helper
    /// funnels through <see cref="ContentRegistry"/> (so IDs stay deterministic / save-safe) and
    /// registers a display name via <see cref="Localization"/>.
    ///
    /// Cloning a template means the new entry inherits a valid icon / prefab / animation set, so it
    /// is immediately usable in-game; you then override only the fields you care about.
    /// </summary>
    public static class Content
    {
        /// <summary>
        /// Fetch a DB and make sure its int-&gt;row index is built. At TableManager.Initialize time the
        /// DB components' own Awake() (which calls MakeIndex) may not have run yet, so GetEntry would
        /// return null without this.
        /// </summary>
        public static T Db<T>() where T : GEDataArrayBase
        {
            T db = TableManager.Instance.Get<T>();
            Reflect.Invoke(db, "CheckAndMakeIndex");
            return db;
        }

        /// <summary>Add a WEAPON (clones an existing weapon's FTK_weaponStats2 row).</summary>
        public static FTK_weaponStats2 AddWeapon(
            string modGuid, string id, FTK_itembase.ID template, string displayName,
            Action<FTK_weaponStats2> configure = null)
        {
            FTK_weaponStats2DB db = Db<FTK_weaponStats2DB>();
            FTK_weaponStats2 tmpl = db.GetEntry(template);
            FTK_weaponStats2 row = (FTK_weaponStats2)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_weaponStats2)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>Add a non-weapon ITEM (clones an existing FTK_items row).</summary>
        public static FTK_items AddItem(
            string modGuid, string id, FTK_itembase.ID template, string displayName,
            Action<FTK_items> configure = null)
        {
            FTK_itemsDB db = Db<FTK_itemsDB>();
            FTK_items tmpl = db.GetEntry(template);
            FTK_items row = (FTK_items)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_items)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>Add a combat action / ability (clones an existing FTK_proficiencyTable row).</summary>
        public static FTK_proficiencyTable AddProficiency(
            string modGuid, string id, FTK_proficiencyTable.ID template, string displayName,
            Action<FTK_proficiencyTable> configure = null)
        {
            FTK_proficiencyTableDB db = Db<FTK_proficiencyTableDB>();
            FTK_proficiencyTable tmpl = db.GetEntry(template);
            FTK_proficiencyTable row = (FTK_proficiencyTable)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_proficiencyTable)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>
        /// Give a weapon a proficiency (combat action) it didn't have.
        ///
        /// A weapon's available actions are the keys of its prefab's Weapon.m_ProficiencyEffects.
        /// Custom weapons clone an existing weapon and therefore SHARE its prefab, so we first
        /// Instantiate a private copy of the prefab (kept inactive + persistent), add our proficiency
        /// to the copy, and repoint the weapon at it — leaving the original weapon untouched.
        /// Returns true on success.
        /// </summary>
        public static bool AttachProficiency(FTK_weaponStats2 weapon, string proficiencyId)
        {
            return AttachProficiencies(weapon, proficiencyId);
        }

        /// <summary>Attach one or more proficiencies to a weapon in a single private prefab copy.</summary>
        public static bool AttachProficiencies(FTK_weaponStats2 weapon, params string[] proficiencyIds)
        {
            if (weapon == null) return false;
            GameObject src = weapon.m_Prefab;
            if (src == null)
            {
                Plugin.Log.LogWarning("AttachProficiencies: weapon '" + weapon.m_ID + "' has no prefab.");
                return false;
            }

            GameObject copy = UnityEngine.Object.Instantiate(src);
            UnityEngine.Object.DontDestroyOnLoad(copy);
            copy.name = src.name + "_ftkmf";
            // Keep it ACTIVE but park it far off-screen: the game re-Instantiates this prefab and reads
            // its Weapon via GetComponentInChildren<Weapon>() WITHOUT includeInactive, so an inactive
            // copy would resolve to null and NPE.
            copy.transform.position = new Vector3(0f, -100000f, 0f);

            Weapon w = copy.GetComponentInChildren<Weapon>(true);
            if (w == null)
            {
                Plugin.Log.LogWarning("AttachProficiencies: no Weapon component on prefab of '" + weapon.m_ID + "'.");
                UnityEngine.Object.Destroy(copy);
                return false;
            }

            int count = AddProfsToWeapon(w, proficiencyIds);

            weapon.m_Prefab = copy;
            Plugin.Log.LogInfo("AttachProficiencies: added " + proficiencyIds.Length + " to '" + weapon.m_ID +
                "' (now " + count + " actions).");
            return true;
        }

        /// <summary>
        /// Shared tail of the weapon / enemy proficiency-attach paths. Ensures the weapon's
        /// <see cref="Weapon.m_ProficiencyEffects"/> dictionary exists, reuses an existing
        /// <see cref="HitEffect"/> from it so the added actions render (warning if there is none to reuse,
        /// since the new actions would then have no impact visual), adds each proficiency id to the dict,
        /// and pushes the runtime dictionary into FullInspector's serialized backing via
        /// <see cref="Weapon.SaveState"/> so it survives the game's re-Instantiate of the weapon.
        /// Returns the dictionary's new entry count.
        /// </summary>
        private static int AddProfsToWeapon(Weapon w, string[] proficiencyIds)
        {
            if (w.m_ProficiencyEffects == null)
                w.m_ProficiencyEffects = new Dictionary<ProficiencyID, HitEffect>();

            // Reuse an existing HitEffect (the visual/impact) from this weapon so the actions render.
            HitEffect reuse = null;
            foreach (HitEffect v in w.m_ProficiencyEffects.Values) { reuse = v; break; }
            if (reuse == null)
                Plugin.Log.LogWarning("AddProfsToWeapon: weapon had no HitEffect to reuse; new actions may lack an impact visual.");

            foreach (string profId in proficiencyIds)
            {
                ProficiencyID key = new ProficiencyID((FTK_proficiencyTable.ID)0);
                key.m_ID = profId; // resolved back to our synthetic id via the patched GetEnum
                w.m_ProficiencyEffects[key] = reuse;
            }

            // Push the runtime dictionary into FullInspector's serialized backing so the change
            // survives the game's Object.Instantiate of the weapon (which re-deserializes it).
            w.SaveState();
            return w.m_ProficiencyEffects.Count;
        }

        /// <summary>
        /// Add a new playable CLASS (clones an existing class's FTK_playerGameStart row).
        /// Classes are registered with id == their array index (the next sequential enum value),
        /// because the character-select UI uses the class id as BOTH an enum key and an array index;
        /// any other id would be unreachable by the cycle and crash the index-based reads.
        /// </summary>
        public static FTK_playerGameStart AddClass(
            string modGuid, string id, FTK_playerGameStart.ID template, string displayName,
            Action<FTK_playerGameStart> configure = null)
        {
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            FTK_playerGameStart tmpl = db.GetEntry(template);
            int index = ((Array)Reflect.GetField(db, "m_Array")).Length; // slot this class will occupy

            FTK_playerGameStart row = (FTK_playerGameStart)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_playerGameStart)o); }, index);
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>
        /// Add a new ENEMY (clones an existing enemy's FTK_enemyCombat row). Uses a high-band synthetic id
        /// like items/weapons/proficiencies — NOT id == array index like classes; nothing indexes enemies
        /// by array position (every lookup is dictionary- or string-based, and selection round-trips the id
        /// through its decimal string over Photon).
        ///
        /// Cloning a template inherits its 3D body (m_EnemyAsset), weapon (m_WeaponAsset) and a sane field
        /// layout, so the custom enemy renders and fights immediately. Pick a NON-boss, NON-scourge template
        /// that is NOT a level-scaling enemy (i.e. not present in FTK_enemyScaleDB) and has a valid
        /// m_EnemyAsset — otherwise the spawn-pool builder silently filters it out.
        ///
        /// After registering we flag the level-bucketed spawn cache (GameCache.Enemies) for rebuild, so the
        /// new enemy becomes eligible for ordinary overworld/dungeon encounters (the cache re-reads the live
        /// DB on its next draw). To actually be drawn it must also pass the picker's gates: a nonzero
        /// m_Rarity draw-chance, matching m_SpawnDay/Night/Land/Water/Dungeon, and realm include/exclude —
        /// all inherited from a template that already spawns naturally (override in <paramref name="configure"/>).
        /// </summary>
        public static FTK_enemyCombat AddEnemy(
            string modGuid, string id, FTK_enemyCombat.ID template, string displayName,
            Action<FTK_enemyCombat> configure = null)
        {
            FTK_enemyCombatDB db = Db<FTK_enemyCombatDB>();
            FTK_enemyCombat tmpl = db.GetEntry(template);
            FTK_enemyCombat row = (FTK_enemyCombat)ContentRegistry.Register(db, modGuid, id, tmpl,
                o =>
                {
                    FTK_enemyCombat e = (FTK_enemyCombat)o;
                    // Register's CopyFields shallow-copies the template, so the clone's m_ItemDrops is the
                    // SAME instance as the template's. Deep-copy it into a fresh ItemDrops here (BEFORE the
                    // caller's configure runs) so authored content can safely mutate e.m_ItemDrops._golddrop
                    // etc. without rewriting the vanilla template's loot table.
                    if (e.m_ItemDrops != null)
                    {
                        FTK_enemyCombat.ItemDrops fresh = new FTK_enemyCombat.ItemDrops();
                        Reflect.CopyFields(e.m_ItemDrops, fresh);
                        e.m_ItemDrops = fresh;
                    }
                    if (configure != null) configure(e);
                });
            Localization.SetName(id, displayName);

            // Make the new row visible to the level-bucketed spawn pool. GameCache.Enemies rebuilds straight
            // from the live FTK_enemyCombatDB.m_Array whenever NeedsRebuild is set, so our row flows in with
            // no static-list surgery. (The game also re-flags this at every game-start, but set it now too.)
            GameCache.Cache.Enemies.NeedsRebuild = true;
            return row;
        }

        /// <summary>
        /// Give an ENEMY one or more proficiencies (combat actions) it didn't have, without mutating the
        /// shared template weapon. An enemy's attacks are the keys of its <c>m_WeaponAsset</c>'s
        /// <c>Weapon.m_ProficiencyEffects</c>. Unlike a player weapon (a <c>GameObject m_Prefab</c>),
        /// <c>m_WeaponAsset</c> is a <c>Weapon</c> COMPONENT, so we Instantiate a private copy of it, add our
        /// proficiencies, push them into the FullInspector serialized backing (<c>SaveState</c>) so they
        /// survive the game's re-Instantiate of the weapon, and repoint the enemy at the copy.
        ///
        /// We also strip any <c>AttackSchedule</c> from the copy so the enemy uses the RNG attack path
        /// (gated by <c>m_ChanceToProf</c>, picking uniformly across all of the weapon's proficiencies) —
        /// a scheduled weapon would only ever fire its fixed script and never our added action.
        /// </summary>
        public static bool AttachEnemyProficiencies(FTK_enemyCombat enemy, params string[] proficiencyIds)
        {
            if (enemy == null) return false;
            Weapon src = enemy.m_WeaponAsset;
            if (src == null)
            {
                Plugin.Log.LogWarning("AttachEnemyProficiencies: enemy '" + enemy.m_ID + "' has no m_WeaponAsset.");
                return false;
            }

            // Instantiating a Component clones its whole GameObject and returns the matching component.
            Weapon copy = UnityEngine.Object.Instantiate(src);
            UnityEngine.Object.DontDestroyOnLoad(copy.gameObject);
            copy.gameObject.name = src.gameObject.name + "_ftkmf";
            // Keep it active but parked off-screen (mirrors the proven weapon path; enemy weapons are read
            // with includeInactive:true, but this avoids relying on that).
            copy.transform.position = new Vector3(0f, -100000f, 0f);

            // Force the RNG attack path so our added action can actually be chosen.
            AttackSchedule schedule = copy.GetComponent<AttackSchedule>();
            if (schedule != null) UnityEngine.Object.Destroy(schedule);

            int count = AddProfsToWeapon(copy, proficiencyIds);

            enemy.m_WeaponAsset = copy;
            Plugin.Log.LogInfo("AttachEnemyProficiencies: added " + proficiencyIds.Length + " to '" + enemy.m_ID +
                "' (now " + count + " actions).");
            return true;
        }

        /// <summary>
        /// Add a new overworld ENCOUNTER / event (clones an existing FTK_miniEncounter row).
        ///
        /// Encounters are the events placed on overworld hexes during a run. The game's selector
        /// (<c>GameLogic.GetMiniEncounter</c>) walks the WHOLE FTK_miniEncounterDB by index, keeps every
        /// row that passes its realm gate (<c>m_RealmInclude</c>/<c>m_RealmExclude</c>; an EMPTY include
        /// list means "every realm"), tier gate, and condition gate, then rolls a weighted draw using
        /// each row's <c>m_Rarity</c> -&gt; FTK_encounterDrawChanceDB probability. So a freshly registered
        /// row is automatically a candidate — no generator/selection patch is needed; you only need the
        /// id to round-trip (handled here via ContentRegistry + the GetEnum/GetIntFromID patches).
        ///
        /// Display text: <c>FTK_miniEncounter.GetDisplay*</c> calls <c>FTKHub.Localized&lt;TextMiniEncounters&gt;</c>,
        /// which returns the key string itself when there's no text row — so we set <c>m_DisplayName</c>
        /// to your literal <paramref name="displayName"/> and it shows verbatim (no localization table edit).
        /// </summary>
        /// <param name="template">An existing encounter to clone (e.g. FTK_miniEncounter.ID.TreasureChest).</param>
        public static FTK_miniEncounter AddEncounter(
            string modGuid, string id, FTK_miniEncounter.ID template, string displayName,
            Action<FTK_miniEncounter> configure = null)
        {
            FTK_miniEncounterDB db = Db<FTK_miniEncounterDB>();
            FTK_miniEncounter tmpl = db.GetEntry(template);
            FTK_miniEncounter row = (FTK_miniEncounter)ContentRegistry.Register(db, modGuid, id, tmpl,
                o =>
                {
                    FTK_miniEncounter e = (FTK_miniEncounter)o;
                    // Literal display name (GetDisplayName returns it verbatim via Localized<>'s key-passthrough).
                    e.m_DisplayName = displayName;
                    if (configure != null) configure(e);
                });
            return row;
        }
    }

    /// <summary>
    /// Routing fix: FTK_itembase.GetItemBase sends every id >= 100000 to the weapon DB
    /// (IsItemID == id &lt; bladeShortsword). Our synthetic ids are all far above that, so custom
    /// *weapons* resolve fine, but custom *items* (which live in FTK_itemsDB) would be looked up in
    /// the wrong table and come back null. We backfill by checking both DBs for our ids.
    /// </summary>
    [HarmonyPatch(typeof(FTK_itembase), "GetItemBase")]
    internal static class GetItemBase_Patch
    {
        private static void Postfix(FTK_itembase.ID _id, ref FTK_itembase __result)
        {
            if (__result != null) return;
            if (!IdAllocator.IsCustom((int)_id)) return;

            FTK_itembase row = TableManager.Instance.Get<FTK_itemsDB>().GetEntryByInt((int)_id);
            if (row == null) row = TableManager.Instance.Get<FTK_weaponStats2DB>().GetEntryByInt((int)_id);
            __result = row;
        }
    }
}
