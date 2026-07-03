using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// An immutable description of one class-innate passive trait: WHICH class owns it (its int id), WHEN it
    /// fires, and its display name. A plain readonly class (deliberately NOT a C# record: net35 / Mono 3.5 has
    /// no records, spec #78 NFR-1). Phase 1 is DORMANT: a def is created + registered but no combat patch
    /// consumes it yet (spec #78 Phase 1). There is NO chance field: a passive fires deterministically at its
    /// trigger; probability is intentionally out of scope (spec #78 Simplicity).
    /// </summary>
    public sealed class PassiveTraitDef
    {
        /// <summary>Stable identity: modGuid + ":" + passiveId. Namespaces the trait so two mods never clash.</summary>
        public readonly string Key;

        /// <summary>The owning class's int id (an FTK_playerGameStart.ID value, resolved from the class row).</summary>
        public readonly int ClassId;

        /// <summary>When this trait fires (a closed set; see <see cref="PassiveTrigger"/>).</summary>
        public readonly PassiveTrigger Trigger;

        /// <summary>The name shown for this trait (also registered through <see cref="Localization"/>).</summary>
        public readonly string DisplayName;

        public PassiveTraitDef(string key, int classId, PassiveTrigger trigger, string displayName)
        {
            Key = key;
            ClassId = classId;
            Trigger = trigger;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Internal registry of class-innate passive traits, keyed by owning class id -&gt; its traits. Populated
    /// at TableManager.Initialize time (through <see cref="Content.AddPassive"/>) and read back by
    /// (classId, trigger). Kept INTERNAL with no mutable state exposed to modders: the only modder-facing
    /// surface is <see cref="Content.AddPassive"/> + the <see cref="PassiveTrigger"/> enum (spec #78 NFR-6).
    /// Phase 1 is DORMANT: the trigger patches that will READ this registry land in a later work item.
    /// </summary>
    internal static class PassiveRegistry
    {
        // owning class id -> its registered traits (insertion order preserved).
        private static readonly Dictionary<int, List<PassiveTraitDef>> ByClass =
            new Dictionary<int, List<PassiveTraitDef>>();

        // trait Key -> its def, so re-registration is idempotent and Remove(key) is O(1).
        private static readonly Dictionary<string, PassiveTraitDef> ByKey =
            new Dictionary<string, PassiveTraitDef>();

        /// <summary>
        /// Register a passive trait. IDEMPOTENT by <paramref name="def"/>.Key: a second call with the same key
        /// does NOT add a duplicate; it logs a warning and returns the ALREADY-registered def (first-wins).
        /// Returns the live registered def either way.
        /// </summary>
        internal static PassiveTraitDef Register(PassiveTraitDef def)
        {
            PassiveTraitDef existing;
            if (ByKey.TryGetValue(def.Key, out existing))
            {
                Plugin.Log.LogWarning("PassiveRegistry: '" + def.Key +
                    "' already registered; keeping the existing trait (idempotent).");
                return existing;
            }

            ByKey[def.Key] = def;

            List<PassiveTraitDef> list;
            if (!ByClass.TryGetValue(def.ClassId, out list))
            {
                list = new List<PassiveTraitDef>();
                ByClass[def.ClassId] = list;
            }
            list.Add(def);
            return def;
        }

        /// <summary>
        /// Resolve the first trait registered for <paramref name="classId"/> whose trigger matches
        /// <paramref name="trigger"/>. Returns true and sets <paramref name="def"/> on a hit; false otherwise.
        /// </summary>
        internal static bool TryResolve(int classId, PassiveTrigger trigger, out PassiveTraitDef def)
        {
            List<PassiveTraitDef> list;
            if (ByClass.TryGetValue(classId, out list))
            {
                for (int i = 0; i < list.Count; i++)
                    if (list[i].Trigger == trigger) { def = list[i]; return true; }
            }
            def = null;
            return false;
        }

        /// <summary>
        /// Remove a registered trait by key. Used ONLY by the startup self-test to CLEAR its probe binding so
        /// no trait remains bound to a real class: once the Phase-2 trigger patches land, a leftover probe
        /// binding on a vanilla or showcase class would become a live combat negate. No-op if the key is
        /// unknown. Returns true if a trait was removed.
        /// </summary>
        internal static bool Remove(string key)
        {
            PassiveTraitDef def;
            if (!ByKey.TryGetValue(key, out def)) return false;
            ByKey.Remove(key);

            List<PassiveTraitDef> list;
            if (ByClass.TryGetValue(def.ClassId, out list))
            {
                list.Remove(def);
                if (list.Count == 0) ByClass.Remove(def.ClassId);
            }
            return true;
        }
    }
}
