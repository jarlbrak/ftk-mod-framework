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
            if (weapon == null) return false;
            GameObject src = weapon.m_Prefab;
            if (src == null)
            {
                Plugin.Log.LogWarning("AttachProficiency: weapon '" + weapon.m_ID + "' has no prefab.");
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
                Plugin.Log.LogWarning("AttachProficiency: no Weapon component on prefab of '" + weapon.m_ID + "'.");
                UnityEngine.Object.Destroy(copy);
                return false;
            }

            if (w.m_ProficiencyEffects == null)
                w.m_ProficiencyEffects = new Dictionary<ProficiencyID, HitEffect>();

            // Reuse an existing HitEffect (the visual/impact) from this weapon so the action renders.
            HitEffect reuse = null;
            foreach (HitEffect v in w.m_ProficiencyEffects.Values) { reuse = v; break; }

            ProficiencyID key = new ProficiencyID((FTK_proficiencyTable.ID)0);
            key.m_ID = proficiencyId; // resolved back to our synthetic id via the patched GetEnum
            w.m_ProficiencyEffects[key] = reuse;

            // Push the runtime dictionary into FullInspector's serialized backing so the change
            // survives the game's Object.Instantiate(_prefab) (which re-deserializes it).
            w.SaveState();

            weapon.m_Prefab = copy;
            Plugin.Log.LogInfo("AttachProficiency: added '" + proficiencyId + "' to '" + weapon.m_ID +
                "' (now " + w.m_ProficiencyEffects.Count + " actions).");
            return true;
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
