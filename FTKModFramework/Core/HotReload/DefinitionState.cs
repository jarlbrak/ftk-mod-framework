using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.HotReload
{
    // Closed Paladin slice. This snapshot is valid only while the coordinator excludes every
    // consumer and starts each candidate from the pristine baseline. It is not general teardown.
    internal sealed class DefinitionState
    {
        private static DefinitionState baseline;
        private static readonly Type[] Tables = { typeof(FTK_playerGameStartDB), typeof(FTK_itemsDB),
            typeof(FTK_weaponStats2DB), typeof(FTK_proficiencyTableDB), typeof(FTK_characterModifierDB) };
        private readonly List<TableState> tables = new List<TableState>();
        private readonly List<MapState> maps = new List<MapState>();
        private readonly ModRegistry.Snapshot mods;
        internal static bool HasBaseline { get { return baseline != null; } }

        internal static void CaptureBaseline(TableManager manager)
        {
            if (baseline != null) throw new InvalidOperationException("Definition baseline already captured.");
            if (ContentRegistry.CustomIds.Count != 0 || IdAllocator.CustomIdCount != 0)
                throw new InvalidOperationException("Definition baseline must precede custom registration.");
            if (manager == null) throw new ArgumentNullException("manager");
            // TableManager initialization can precede lazy database indexing. An empty
            // candidate does not register rows and therefore cannot repair a null index.
            foreach (Type type in Tables)
            {
                GEDataArrayBase db = manager.Get(type);
                if (db == null) throw new InvalidOperationException("Missing baseline table: " + type.Name);
                db.CheckAndMakeIndex();
            }
            ValidateLookups(manager);
            baseline = Capture(manager);
        }

        internal static DefinitionState Capture(TableManager manager)
        {
            return new DefinitionState(manager);
        }

        internal static void RestoreBaseline(TableManager manager)
        {
            if (baseline == null) throw new InvalidOperationException("Definition baseline unavailable.");
            baseline.Restore(manager);
        }

        private DefinitionState(TableManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (Read(typeof(ContentRegistry), "_batchDirty") != null)
                throw new InvalidOperationException("Cannot snapshot a registration batch.");
            foreach (Type type in Tables) tables.Add(new TableState(manager.Get(type)));
            AddMap(typeof(ContentRegistry), "CustomIds");
            AddMap(typeof(ContentRegistry), "RetainedRows");
            AddMap(typeof(IdAllocator), "KeyToInt");
            AddMap(typeof(IdAllocator), "IntToKey");
            foreach (string name in new[] { "Names", "RealmDisplayKeys", "ClassFlavors", "ProficiencyDescriptions", "EnemyDescriptions" })
                AddMap(typeof(Localization), name);
            mods = ModRegistry.Capture();
        }

        internal void Restore(TableManager manager)
        {
            // Validate all component identities before changing anything. Scene replacement is not
            // supported inside a transaction; a failed check must fault the coordinator.
            ValidateComponents(manager);
            foreach (MapState map in maps) map.Restore();
            foreach (TableState table in tables) table.Restore();
            mods.Restore();
        }

        internal void ValidateComponents(TableManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            foreach (TableState table in tables) table.Validate(manager);
        }

        // Validate vanilla rows too: custom-only checks cannot detect an empty activation
        // restoring a pre-bootstrap null index. This deliberately does not repair candidates.
        internal static void ValidateLookups(TableManager manager)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            Dictionary<Type, FieldInfo> identities = new Dictionary<Type, FieldInfo>();
            foreach (Type type in Tables)
            {
                GEDataArrayBase db = manager.Get(type);
                if (db == null) throw new InvalidOperationException("Missing candidate table: " + type.Name);
                Array rows = Reflect.GetField(db, "m_Array") as Array;
                IDictionary index = Reflect.GetField(db, "m_Dictionary") as IDictionary;
                if (rows == null || index == null || index.Count != rows.Length)
                    throw new InvalidOperationException("Candidate table index is incomplete: " + type.Name);
                // Resolve reflection metadata once per table and concrete row type. The
                // native lookup methods still run for every row, including vanilla rows.
                MethodInfo getId = LookupMethod(db.GetType(), "GetIntFromID");
                MethodInfo getEntry = LookupMethod(db.GetType(), "GetEntryByInt");
                object[] argument = new object[1];
                foreach (object row in rows)
                {
                    string key = RowIdentity(row, identities);
                    if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("Candidate row has no identity: " + type.Name);
                    argument[0] = key;
                    object resolved = getId.Invoke(db, argument);
                    argument[0] = resolved;
                    if (!(resolved is int) || !object.ReferenceEquals(getEntry.Invoke(db, argument), row))
                        throw new InvalidOperationException("Candidate row lookup differs: " + type.Name + "/" + key);
                }
                Dictionary<string, int> custom;
                if (ContentRegistry.CustomIds.TryGetValue(type, out custom))
                    foreach (KeyValuePair<string, int> entry in custom)
                    {
                        argument[0] = entry.Value;
                        object row = getEntry.Invoke(db, argument);
                        if (row == null || !string.Equals(RowIdentity(row, identities), entry.Key, StringComparison.Ordinal))
                            throw new InvalidOperationException("Custom identity lacks its indexed row: " + type.Name + "/" + entry.Key);
                    }
            }
        }

        private static MethodInfo LookupMethod(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo method = current.GetMethod(name, Reflect.All | BindingFlags.DeclaredOnly);
                if (method != null) return method;
            }
            throw new MissingMethodException(type.FullName, name);
        }

        private static string RowIdentity(object row, Dictionary<Type, FieldInfo> identities)
        {
            if (row == null) return null;
            Type type = row.GetType();
            FieldInfo field;
            if (!identities.TryGetValue(type, out field))
            {
                field = Reflect.Field(type, "m_ID");
                identities.Add(type, field);
            }
            return field == null ? null : field.GetValue(row) as string;
        }

        private void AddMap(Type type, string name) { maps.Add(new MapState((IDictionary)Read(type, name))); }
        private static object Read(Type type, string name)
        {
            FieldInfo field = Reflect.Field(type, name);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return field.GetValue(null);
        }

        private sealed class MapState
        {
            private readonly IDictionary target;
            private readonly DictionaryEntry[] entries;
            internal MapState(IDictionary target)
            {
                this.target = target;
                entries = new DictionaryEntry[target.Count];
                target.CopyTo(entries, 0);
            }
            internal void Restore()
            {
                target.Clear();
                foreach (DictionaryEntry entry in entries) target.Add(entry.Key, entry.Value);
            }
        }

        private sealed class TableState
        {
            private readonly GEDataArrayBase db;
            private readonly FieldInfo arrayField, indexField;
            private readonly object rows, index;
            internal TableState(GEDataArrayBase db)
            {
                if (db == null) throw new InvalidOperationException("Missing Paladin database.");
                this.db = db;
                arrayField = Reflect.Field(db.GetType(), "m_Array");
                indexField = Reflect.Field(db.GetType(), "m_Dictionary");
                if (arrayField == null || indexField == null) throw new InvalidOperationException("Unsupported database shape.");
                rows = arrayField.GetValue(db);
                index = indexField.GetValue(db);
                if (rows == null) throw new InvalidOperationException("Database is not initialized.");
            }
            internal void Validate(TableManager manager)
            {
                if (!object.ReferenceEquals(db, manager.Get(db.GetType())))
                    throw new InvalidOperationException("Database component changed during hot activation.");
            }
            internal void Restore()
            {
                // Native MakeIndex replaces its dictionary, so preserving its exact old reference
                // restores both contents and any admitted cache identity without another rebuild.
                arrayField.SetValue(db, rows);
                indexField.SetValue(db, index);
            }
        }
    }
}
