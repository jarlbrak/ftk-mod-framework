using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    // Installed-game evidence: Cache.Items.Initialize clears existing dictionaries;
    // ProficiencyManager.Start allocates children without retiring its previous children.
    internal sealed class HotReloadNativeCaches
    {
        private readonly FieldInfo[] fields;
        private readonly object[] values;
        private readonly ProficiencyManager manager;
        private readonly Dictionary<FTK_proficiencyTable.ID, ProficiencyBase> previous;
        private readonly Dictionary<FTK_proficiencyTable.ID, FTK_proficiencyTable> previousRows;
        private readonly List<GameObject> created = new List<GameObject>();
        private Dictionary<FTK_proficiencyTable.ID, ProficiencyBase> replacement;
        private bool rebuilding, finished;

        internal static HotReloadNativeCaches Capture() { return new HotReloadNativeCaches(); }

        private HotReloadNativeCaches()
        {
            string[] names = { "_itemPrefabs", "_itemIcons", "_itemIconsNonClickable", "_itemsByCategory", "_isInitialized" };
            fields = new FieldInfo[names.Length]; values = new object[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                fields[i] = Reflect.Field(typeof(GameCache.Cache.Items), names[i]);
                if (fields[i] == null) throw new MissingFieldException(typeof(GameCache.Cache.Items).FullName, names[i]);
                values[i] = fields[i].GetValue(null);
            }
            manager = ProficiencyManager.Instance;
            if (manager == null || manager.m_ProficiencyTable == null)
                throw new InvalidOperationException("Title proficiency manager has not completed initialization.");
            previous = manager.m_ProficiencyTable;
            previousRows = new Dictionary<FTK_proficiencyTable.ID, FTK_proficiencyTable>();
            foreach (FTK_proficiencyTable row in TableManager.Instance.Get<FTK_proficiencyTableDB>().m_Array)
                previousRows.Add(FTK_proficiencyTable.GetEnum(row.m_ID), row);
        }

        internal void Rebuild()
        {
            if (rebuilding || finished) throw new InvalidOperationException("Cache transaction already used.");
            if (manager == null || !object.ReferenceEquals(manager, ProficiencyManager.Instance) ||
                !object.ReferenceEquals(previous, manager.m_ProficiencyTable))
                throw new InvalidOperationException("Proficiency manager changed before activation.");
            rebuilding = true;
            // Null maps force new allocations, preserving exact prior maps/lists for rollback.
            for (int i = 0; i < fields.Length - 1; i++) fields[i].SetValue(null, null);
            fields[fields.Length - 1].SetValue(null, false);
            GameCache.Cache.Items.Initialize();
            replacement = new Dictionary<FTK_proficiencyTable.ID, ProficiencyBase>();
            foreach (FTK_proficiencyTable row in TableManager.Instance.Get<FTK_proficiencyTableDB>().m_Array)
            {
                if (row.m_ProficiencyPrefab == null) continue;
                FTK_proficiencyTable.ID id = FTK_proficiencyTable.GetEnum(row.m_ID);
                FTK_proficiencyTable oldRow;
                ProficiencyBase instance;
                if (previousRows.TryGetValue(id, out oldRow) && object.ReferenceEquals(oldRow, row) &&
                    previous.TryGetValue(id, out instance) && instance != null)
                {
                    replacement.Add(id, instance); // unchanged vanilla behavior keeps its identity/state
                    continue;
                }
                instance = UnityEngine.Object.Instantiate(row.m_ProficiencyPrefab);
                if (instance == null) throw new InvalidOperationException("Proficiency prefab instantiation failed.");
                created.Add(instance.gameObject); // own it before Init can fail
                instance.Init(id);
                instance.transform.SetParent(manager.transform);
                replacement.Add(id, instance);
            }
            manager.m_ProficiencyTable = replacement;
        }

        internal int ProficiencyCount { get { return replacement == null ? 0 : replacement.Count; } }

        internal void Validate()
        {
            if (replacement == null || !object.ReferenceEquals(replacement, manager.m_ProficiencyTable))
                throw new InvalidOperationException("Candidate proficiency table was not published.");
            int count = 0;
            foreach (FTK_proficiencyTable row in TableManager.Instance.Get<FTK_proficiencyTableDB>().m_Array)
            {
                if (row.m_ProficiencyPrefab == null) continue;
                ProficiencyBase behavior;
                if (!replacement.TryGetValue(FTK_proficiencyTable.GetEnum(row.m_ID), out behavior) || behavior == null ||
                    !object.ReferenceEquals(Reflect.GetField(behavior, "m_ProficiencyData"), row))
                    throw new InvalidOperationException("Proficiency cache retains a stale row: " + row.m_ID);
                count++;
            }
            if (count != replacement.Count) throw new InvalidOperationException("Unexpected proficiency cache entries.");
            HashSet<object> rows = new HashSet<object>();
            foreach (Type type in new[] { typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB) })
                foreach (object row in (Array)Reflect.GetField(TableManager.Instance.Get(type), "m_Array")) rows.Add(row);
            IDictionary categories = fields[3].GetValue(null) as IDictionary;
            if (categories == null || !(bool)fields[4].GetValue(null))
                throw new InvalidOperationException("Item cache did not initialize.");
            foreach (DictionaryEntry category in categories)
                foreach (object row in (IEnumerable)category.Value)
                    if (!rows.Contains(row)) throw new InvalidOperationException("Item category cache retains a stale row.");
        }

        internal void Restore()
        {
            if (finished) throw new InvalidOperationException("Cache transaction already completed.");
            if (manager == null || !object.ReferenceEquals(manager, ProficiencyManager.Instance))
                throw new InvalidOperationException("Proficiency manager changed during rollback.");
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, values[i]);
            manager.m_ProficiencyTable = previous;
            foreach (GameObject host in created) DestroyOwned(host);
            created.Clear();
            finished = true;
        }

        internal void Retire()
        {
            if (finished || replacement == null || !object.ReferenceEquals(manager.m_ProficiencyTable, replacement))
                throw new InvalidOperationException("Cannot retire an unpublished cache transaction.");
            foreach (KeyValuePair<FTK_proficiencyTable.ID, ProficiencyBase> entry in previous)
            {
                ProficiencyBase current;
                if (entry.Value != null && (!replacement.TryGetValue(entry.Key, out current) ||
                    !object.ReferenceEquals(entry.Value, current))) DestroyOwned(entry.Value.gameObject);
            }
            created.Clear(); // current instances now belong to the live manager
            finished = true;
        }

        private static void DestroyOwned(GameObject host)
        {
            if (host == null) return;
            host.SetActive(false);
            PaladinResourceState.DestroyTracked(host);
        }
    }
}
