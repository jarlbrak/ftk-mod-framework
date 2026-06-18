using System;
using System.Collections.Generic;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Process-global map from a behaviour key (modGuid + ":" + behaviourName) to the
    /// <c>ProficiencyBase</c> subclass that implements it. The framework registers a behaviour once at
    /// startup (under the same one-shot guard the rest of content registration runs under), then content
    /// data resolves a behaviour by key when it needs to host one.
    ///
    /// Deliberately a flat string -&gt; Type dictionary: NO hook interface, NO behaviour hierarchy. The
    /// registry only answers "which Type implements this key", and the hosting lifecycle lives in
    /// <see cref="BehaviorHost"/>.
    ///
    /// Internal to Core: the public surface a modder touches is just
    /// <see cref="FTKModFramework.Behaviors.ContentBehaviorAttribute"/>.
    /// </summary>
    internal static class BehaviorRegistry
    {
        private static readonly Dictionary<string, Type> KeyToType = new Dictionary<string, Type>();

        /// <summary>Compose the registry key for a behaviour from its owning mod guid and local name.</summary>
        internal static string MakeKey(string modGuid, string behaviorName)
        {
            return modGuid + ":" + behaviorName;
        }

        /// <summary>
        /// Register <paramref name="type"/> under (<paramref name="modGuid"/> + ":" + <paramref name="behaviorName"/>).
        /// Convenience over <see cref="Register(string, Type)"/>.
        /// </summary>
        internal static void Register(string modGuid, string behaviorName, Type type)
        {
            Register(MakeKey(modGuid, behaviorName), type);
        }

        /// <summary>
        /// Register <paramref name="type"/> under <paramref name="key"/>. Idempotent and guarded:
        /// <list type="bullet">
        /// <item>A null/empty key or null type is rejected with a warning.</item>
        /// <item>A type NOT assignable to <c>ProficiencyBase</c> is rejected with a warning and not stored.</item>
        /// <item>Registering a key that already exists KEEPS the first registration, logs a warning, and
        /// skips the second (first-wins, so load order cannot silently swap a behaviour).</item>
        /// </list>
        /// </summary>
        internal static void Register(string key, Type type)
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

            if (!typeof(ProficiencyBase).IsAssignableFrom(type))
            {
                Plugin.Log.LogWarning("BehaviorRegistry: type '" + type.FullName +
                    "' for key '" + key + "' is not assignable to ProficiencyBase; rejected.");
                return;
            }

            Type existing;
            if (KeyToType.TryGetValue(key, out existing))
            {
                Plugin.Log.LogWarning("BehaviorRegistry: key '" + key + "' is already registered to '" +
                    existing.FullName + "'; keeping the first and skipping '" + type.FullName + "'.");
                return;
            }

            KeyToType[key] = type;
        }

        /// <summary>
        /// Resolve the behaviour Type registered under <paramref name="key"/>. Returns true and sets
        /// <paramref name="type"/> on a hit; returns false and sets it to null on a miss.
        /// </summary>
        internal static bool TryResolve(string key, out Type type)
        {
            if (string.IsNullOrEmpty(key))
            {
                type = null;
                return false;
            }
            return KeyToType.TryGetValue(key, out type);
        }
    }
}
