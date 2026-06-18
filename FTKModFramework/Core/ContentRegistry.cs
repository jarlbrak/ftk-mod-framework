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

        // OPT-IN batch mode. Non-null only between BeginBatch/EndBatch. While set, Register records
        // each touched DB here instead of reindexing it immediately, so N registrations into one DB
        // cost ONE MakeIndex (at EndBatch) instead of N (which is O(N^2) MakeIndex calls). DB rows are
        // singletons from TableManager.Instance.Get<T>(), so default reference equality is correct.
        // Only ContentLoader uses this; hand-written content keeps the immediate-MakeIndex path.
        // The content load and all hand-written registration run on the Unity main thread.
        private static HashSet<GEDataArrayBase> _batchDirty;

        /// <summary>
        /// Enter batch mode: subsequent <see cref="Register"/> calls DEFER their per-DB MakeIndex and
        /// record the touched DB instead. Pair with <see cref="EndBatch"/> in a try/finally so the
        /// indices always rebuild. Re-entrant safe: a second BeginBatch while batching is a no-op (the
        /// outermost EndBatch flushes everything).
        /// Core-internal: only the data ContentLoader batches; modders use the per-row Content.* helpers.
        /// </summary>
        internal static void BeginBatch()
        {
            if (_batchDirty == null) _batchDirty = new HashSet<GEDataArrayBase>();
        }

        /// <summary>
        /// Exit batch mode and rebuild the int-&gt;row dictionary of every DB touched while batching,
        /// exactly once each. Clears the batch state FIRST (before any MakeIndex runs) so a throwing
        /// MakeIndex cannot strand us in batch mode. A no-op if not currently batching.
        /// Core-internal: see <see cref="BeginBatch"/>.
        /// </summary>
        internal static void EndBatch()
        {
            if (_batchDirty == null) return;
            HashSet<GEDataArrayBase> snapshot = _batchDirty;
            _batchDirty = null; // leave batch mode FIRST so a MakeIndex throw can't strand us in it.
            foreach (GEDataArrayBase db in snapshot)
                Reflect.Invoke(db, "MakeIndex");
        }

        /// <param name="db">The target table, e.g. TableManager.Instance.Get&lt;FTK_itemsDB&gt;().</param>
        /// <param name="modGuid">Your plugin GUID (namespaces the id so two mods never clash).</param>
        /// <param name="id">A unique string id for the new row, e.g. "mymod_flamesword".</param>
        /// <param name="template">An existing row to clone defaults from (recommended). May be null.</param>
        /// <param name="configure">Callback to set fields on the freshly created row.</param>
        /// <returns>The new row instance (a GEDataBase subtype, e.g. FTK_items).</returns>
        /// <param name="explicitId">
        /// If &gt;= 0, use this exact int id instead of the IdAllocator's high band. Classes REQUIRE
        /// this: the character-select UI uses the class id as both an enum key and an array index, so
        /// a class's id must equal the array index it's appended at (the next sequential enum value).
        /// </param>
        public static object Register(
            GEDataArrayBase db,
            string modGuid,
            string id,
            object template = null,
            Action<object> configure = null,
            int explicitId = -1)
        {
            if (db == null) throw new ArgumentNullException("db");
            Type dbType = db.GetType();

            int synthetic = explicitId >= 0 ? explicitId : IdAllocator.Allocate(modGuid, dbType.Name + "/" + id);
            Record(dbType, id, synthetic);

            // Patch lookups BEFORE indexing so MakeIndex() maps our row under the synthetic int.
            DbLookupPatcher.EnsurePatched(dbType);

            // AddEntry stays per-row and immediate (NOT batched): positional classes register at
            // id == m_Array.Length, so the next class must see the updated length right away. Its
            // residual O(N^2) (a new T[Length+1] + element copy each call) is an accepted negligible
            // cost: the constant is an object-reference copy only. MakeIndex is the real O(N^2) cost
            // and is the only thing we defer.
            db.AddEntry(id); // appends a blank row with m_ID == id

            Array arr = (Array)Reflect.GetField(db, "m_Array");
            object row = arr.GetValue(arr.Length - 1);

            if (template != null) Reflect.CopyFields(template, row, "m_ID");
            if (configure != null) configure(row);

            // Rebuild the int->row dictionary so it includes the new entry. While batching, defer this
            // to one MakeIndex per DB at EndBatch; otherwise reindex immediately (hand-written content).
            if (_batchDirty != null) _batchDirty.Add(db);
            else Reflect.Invoke(db, "MakeIndex");

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

        /// <summary>
        /// Reverse lookup: true iff <paramref name="id"/> is a synthetic int this framework actually
        /// REGISTERED under one of <paramref name="dbTypes"/>. Unlike <see cref="IdAllocator.IsCustom"/>
        /// (which only tests the band) this confirms the int is BACKED by a real registered row, so a
        /// raw int in the custom band that no entry created can be caught as a dangling reference.
        /// </summary>
        public static bool IsRegisteredSyntheticId(int id, params Type[] dbTypes)
        {
            for (int i = 0; i < dbTypes.Length; i++)
            {
                Dictionary<string, int> map;
                if (CustomIds.TryGetValue(dbTypes[i], out map))
                    foreach (int synthetic in map.Values)
                        if (synthetic == id) return true;
            }
            return false;
        }
    }
}
