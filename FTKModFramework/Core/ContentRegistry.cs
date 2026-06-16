using System;
using System.Collections.Generic;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The generic content-injection core. Adds a new row to ANY GridEditor data table
    /// (FTK_itemsDB, FTK_weaponStats2DB, FTK_proficiencyTableDB, FTK_enemyCombatDB,
    /// FTK_playerGameStartDB, FTK_realmDB, ...) using the same verified mechanism:
    ///
    ///   1. mint a deterministic synthetic int id  (IdAllocator)
    ///   2. ensure the DB's GetIntFromID is patched to resolve our string id  (DbLookupPatcher)
    ///   3. db.AddEntry(stringId)         -> grows m_Array, appends a blank row, sets row.m_ID
    ///   4. copy a template row onto it    -> inherit icons/prefabs/sane defaults from an existing entry
    ///   5. run a caller 'configure' hook  -> override the handful of fields you care about
    ///   6. db.MakeIndex()                 -> rebuild the int->row dictionary (now incl. our row)
    ///
    /// Higher-level helpers (CustomItem / CustomEnemy / CustomClass ...) will sit on top of
    /// this, but everything ultimately funnels through Register so there is one code path to
    /// get right, save-safe and multiplayer-deterministic.
    /// </summary>
    public static class ContentRegistry
    {
        // dbType -> (our string id -> synthetic int).   Read by DbLookupPatcher.
        internal static readonly Dictionary<Type, Dictionary<string, int>> CustomIds =
            new Dictionary<Type, Dictionary<string, int>>();

        /// <param name="db">The target table, e.g. TableManager.Instance.Get&lt;FTK_itemsDB&gt;().</param>
        /// <param name="modGuid">Your plugin GUID (namespaces the id so two mods never clash).</param>
        /// <param name="id">A unique string id for the new row, e.g. "mymod_flamesword".</param>
        /// <param name="template">An existing row to clone defaults from (recommended). May be null.</param>
        /// <param name="configure">Callback to set fields on the freshly created row.</param>
        /// <returns>The new row instance (a GEDataBase subtype, e.g. FTK_items).</returns>
        public static object Register(
            GEDataArrayBase db,
            string modGuid,
            string id,
            object template = null,
            Action<object> configure = null)
        {
            if (db == null) throw new ArgumentNullException("db");
            Type dbType = db.GetType();

            int synthetic = IdAllocator.Allocate(modGuid, dbType.Name + "/" + id);
            Record(dbType, id, synthetic);

            // Patch lookups BEFORE indexing so MakeIndex() maps our row under the synthetic int.
            DbLookupPatcher.EnsurePatched(dbType);

            db.AddEntry(id); // appends a blank row with m_ID == id

            Array arr = (Array)Reflect.GetField(db, "m_Array");
            object row = arr.GetValue(arr.Length - 1);

            if (template != null) Reflect.CopyFields(template, row, "m_ID");
            if (configure != null) configure(row);

            // Force a full reindex so the int->row dictionary includes the new entry.
            Reflect.Invoke(db, "MakeIndex");

            Plugin.Log.LogInfo(
                "Registered '" + id + "' in " + dbType.Name + " (synthetic id " + synthetic + ").");
            return row;
        }

        private static void Record(Type dbType, string id, int synthetic)
        {
            Dictionary<string, int> map;
            if (!CustomIds.TryGetValue(dbType, out map))
            {
                map = new Dictionary<string, int>();
                CustomIds[dbType] = map;
            }
            map[id] = synthetic;
        }

        /// <summary>
        /// Look up the synthetic int we minted for a custom string id, searching the given DB types.
        /// Used by the GetEnum patches to make the game's string-&gt;enum conversion resolve our ids.
        /// </summary>
        public static bool TryGetSyntheticId(string contentId, out int id, params Type[] dbTypes)
        {
            for (int i = 0; i < dbTypes.Length; i++)
            {
                Dictionary<string, int> map;
                if (CustomIds.TryGetValue(dbTypes[i], out map) && map.TryGetValue(contentId, out id))
                    return true;
            }
            id = -1;
            return false;
        }
    }
}
