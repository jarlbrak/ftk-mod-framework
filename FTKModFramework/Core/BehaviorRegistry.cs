using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Process-global map from a behaviour key (modGuid + ":" + behaviourName) to the game type that
    /// implements it AND the <see cref="BehaviorKind"/> it was registered under. The framework registers a
    /// behaviour once at startup (under the same one-shot guard the rest of content registration runs under),
    /// then content data resolves a behaviour by key when it needs to host (proficiency) or instantiate
    /// (questlogic) one.
    ///
    /// The constraint is CLOSED at exactly two kinds (see <see cref="BehaviorKind"/>). The registry stores the
    /// kind so each resolution can pick the correct instantiation path; it does NOT define a hook interface or
    /// a behaviour hierarchy. The hosting lifecycle for the proficiency kind lives in <see cref="BehaviorHost"/>;
    /// the questlogic Activator path is wired by the runtime resolver (#40).
    ///
    /// Internal to Core: the public surface a modder touches is just
    /// <see cref="FTKModFramework.Behaviors.ContentBehaviorAttribute"/>.
    /// </summary>
    internal static class BehaviorRegistry
    {
        /// <summary>What a key resolves to: the implementing Type plus the kind it was registered under.</summary>
        private struct Entry
        {
            public Type Type;
            public BehaviorKind Kind;
        }

        private static readonly Dictionary<string, Entry> KeyToEntry = new Dictionary<string, Entry>();

        /// <summary>Compose the registry key for a behaviour from its owning mod guid and local name.</summary>
        internal static string MakeKey(string modGuid, string behaviorName)
        {
            return modGuid + ":" + behaviorName;
        }

        /// <summary>
        /// Register <paramref name="type"/> under (<paramref name="modGuid"/> + ":" + <paramref name="behaviorName"/>)
        /// as <paramref name="kind"/>. Convenience over <see cref="Register(string, Type, BehaviorKind)"/>.
        /// </summary>
        internal static void Register(string modGuid, string behaviorName, Type type, BehaviorKind kind)
        {
            Register(MakeKey(modGuid, behaviorName), type, kind);
        }

        /// <summary>
        /// Register <paramref name="type"/> under <paramref name="key"/> as <paramref name="kind"/>. Idempotent
        /// and guarded:
        /// <list type="bullet">
        /// <item>A null/empty key or null type is rejected with a warning.</item>
        /// <item>A type that does NOT match <paramref name="kind"/>'s required base type is rejected with a
        /// precise warning (naming the kind, the expected base, and the actual type) and is NOT stored.</item>
        /// <item>Registering a key that already exists KEEPS the first registration, logs a warning, and skips
        /// the second (first-wins, so load order cannot silently swap a behaviour).</item>
        /// </list>
        /// </summary>
        internal static void Register(string key, Type type, BehaviorKind kind)
        {
            if (string.IsNullOrEmpty(key))
            {
                Plugin.Log.LogWarning("BehaviorRegistry: ignoring registration with a null/empty key.");
                return;
            }

            if (type == null)
            {
                Plugin.Log.LogWarning("BehaviorRegistry: ignoring null type for key '" + key + "'.");
                return;
            }

            if (!MatchesKindBase(type, kind))
            {
                Plugin.Log.LogWarning("BehaviorRegistry: type '" + type.FullName + "' for key '" + key +
                    "' does not match kind '" + kind + "' (expected a " + ExpectedBaseName(kind) +
                    "); rejected.");
                return;
            }

            Entry existing;
            if (KeyToEntry.TryGetValue(key, out existing))
            {
                Plugin.Log.LogWarning("BehaviorRegistry: key '" + key + "' is already registered to '" +
                    existing.Type.FullName + "' (" + existing.Kind + "); keeping the first and skipping '" +
                    type.FullName + "' (" + kind + ").");
                return;
            }

            Entry entry;
            entry.Type = type;
            entry.Kind = kind;
            KeyToEntry[key] = entry;
        }

        /// <summary>
        /// Resolve the behaviour Type and <see cref="BehaviorKind"/> registered under <paramref name="key"/>.
        /// Returns true and sets both out params on a hit; returns false and sets them to defaults on a miss.
        /// </summary>
        internal static bool TryResolve(string key, out Type type, out BehaviorKind kind)
        {
            type = null;
            kind = BehaviorKind.Proficiency;
            if (string.IsNullOrEmpty(key)) return false;

            Entry entry;
            if (!KeyToEntry.TryGetValue(key, out entry)) return false;

            type = entry.Type;
            kind = entry.Kind;
            return true;
        }

        /// <summary>
        /// Back-compat overload for callers that only need the Type (the kind-agnostic resolve). Returns true
        /// and sets <paramref name="type"/> on a hit; false and null on a miss.
        /// </summary>
        internal static bool TryResolve(string key, out Type type)
        {
            BehaviorKind ignored;
            return TryResolve(key, out type, out ignored);
        }

        /// <summary>
        /// The CLOSED per-kind base-type constraint. A behaviour Type is valid for a kind only if it is
        /// assignable to that kind's game base type. This is the single place the two kinds' base constraints
        /// are encoded; both <see cref="Register(string, Type, BehaviorKind)"/> and any defensive caller use it.
        /// </summary>
        private static bool MatchesKindBase(Type type, BehaviorKind kind)
        {
            switch (kind)
            {
                case BehaviorKind.Proficiency:
                    return typeof(ProficiencyBase).IsAssignableFrom(type);
                case BehaviorKind.QuestLogic:
                    return typeof(QuestLogicBase).IsAssignableFrom(type);
                default:
                    // Unreachable: the enum is closed at two values. Treat an unknown kind as a non-match so a
                    // hypothetical future value can never store an unvalidated type.
                    return false;
            }
        }

        /// <summary>Human-readable expected base type name for a kind, for the reject diagnostic.</summary>
        private static string ExpectedBaseName(BehaviorKind kind)
        {
            switch (kind)
            {
                case BehaviorKind.Proficiency:
                    return "ProficiencyBase";
                case BehaviorKind.QuestLogic:
                    return "QuestLogicBase";
                default:
                    return "(unknown)";
            }
        }
    }
}
