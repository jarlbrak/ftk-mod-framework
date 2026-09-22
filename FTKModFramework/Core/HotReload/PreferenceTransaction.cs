using System;
using System.IO;

namespace FTKModFramework.Core.HotReload
{
    internal interface IClassPreferenceStore
    {
        bool Has(string key);
        int Get(string key);
        void Set(string key, int value);
        void Delete(string key);
        void Save();
    }

    internal sealed class PreferenceTransaction
    {
        private readonly IClassPreferenceStore store;
        private readonly bool[] exists = new bool[3];
        private readonly int[] before = new int[3], after = new int[3];
        private readonly string[] identities = new string[3];
        private string journal;
        private bool planned;
        private bool recovering;
        internal PreferenceTransaction(IClassPreferenceStore store, string[] keys)
        {
            this.store = store;
            for (int i = 0; i < 3; i++)
            {
                exists[i] = store.Has(Key(i));
                before[i] = store.Get(Key(i));
                if (exists[i] && before[i] >= 0 && before[i] < keys.Length) identities[i] = keys[before[i]];
            }
        }
        private PreferenceTransaction(IClassPreferenceStore store) { this.store = store; }
        private static string Key(int i) { return "Player" + i + "class"; }
        internal static string PathFor(string root) { return Path.Combine(root, "class-preferences.intent"); }
        internal void Plan(string[] keys, int fallback)
        {
            for (int i = 0; i < 3; i++)
            {
                after[i] = before[i];
                if (!exists[i] || before[i] == -1) continue;
                int index = identities[i] == null ? -1 : Array.IndexOf(keys, identities[i]);
                if (index < 0) index = fallback;
                if (index < 0 || index >= keys.Length) throw new InvalidOperationException("No safe unlocked native class fallback.");
                after[i] = index;
            }
            planned = true;
        }
        internal void Prepare(string root, string oldGeneration, string newGeneration)
        {
            if (!planned) throw new InvalidOperationException("Class preferences were not planned.");
            Check(false);
            string destination = PathFor(root);
            if (File.Exists(destination)) throw new IOException("An unresolved preference journal exists.");
            Directory.CreateDirectory(root);
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (BinaryWriter writer = new BinaryWriter(file))
                {
                    writer.Write("FTK-class-preferences-1");
                    writer.Write(oldGeneration); writer.Write(newGeneration);
                    for (int i = 0; i < 3; i++) { writer.Write(exists[i]); writer.Write(before[i]); writer.Write(after[i]); }
                    writer.Flush(); file.Flush();
                }
                File.Move(temporary, destination);
                journal = destination;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private void Check(bool allowAfter)
        {
            // Check every slot before the first write; never overwrite an unrelated edit.
            for (int i = 0; i < 3; i++)
                if (store.Has(Key(i)) != exists[i] || (exists[i] && store.Get(Key(i)) != before[i] && (!allowAfter || store.Get(Key(i)) != after[i])))
                    throw new InvalidOperationException("Remembered class preference changed outside activation: " + Key(i));
        }
        internal void Complete(bool committed)
        {
            if (journal == null) return;
            Check(recovering);
            for (int i = 0; i < 3; i++)
            {
                if (exists[i]) store.Set(Key(i), committed ? after[i] : before[i]);
                else store.Delete(Key(i));
            }
            store.Save();
            File.Delete(journal);
            journal = null;
        }
        internal static void Recover(string root, string current, IClassPreferenceStore store)
        {
            string path = PathFor(root);
            if (new FileInfo(path).Length > 4096) throw new IOException("Preference journal is too large.");
            PreferenceTransaction transaction = new PreferenceTransaction(store);
            string oldGeneration, newGeneration;
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.ReadString() != "FTK-class-preferences-1") throw new IOException("Unknown preference journal format.");
                oldGeneration = reader.ReadString(); newGeneration = reader.ReadString();
                for (int i = 0; i < 3; i++) { transaction.exists[i] = reader.ReadBoolean(); transaction.before[i] = reader.ReadInt32(); transaction.after[i] = reader.ReadInt32(); }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new IOException("Trailing preference journal data.");
            }
            if (current != oldGeneration && current != newGeneration) throw new IOException("Preference journal does not match the durable generation.");
            transaction.journal = path;
            transaction.recovering = true;
            transaction.Complete(current == newGeneration);
        }
    }
}
