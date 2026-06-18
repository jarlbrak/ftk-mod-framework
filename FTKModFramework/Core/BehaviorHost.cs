using System;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Hosts a custom content behaviour. A <c>ProficiencyBase</c> is a MonoBehaviour
    /// (ProficiencyBase -&gt; BaseBehavior -&gt; CommonBaseBehavior -&gt; UnityEngine.MonoBehaviour, per the
    /// decompile), so it must live on a live GameObject. This centralises the exact lifecycle that was
    /// previously inlined in the demo content: create a GameObject, mark it DontDestroyOnLoad so it
    /// survives scene changes, park it far off-screen while leaving it ACTIVE (the game instantiates the
    /// behaviour as a proficiency prefab and needs it active), add the behaviour component, and hand back
    /// the instance.
    ///
    /// It does NOT call <c>Init</c>: the game's ProficiencyManager owns initialisation and passes the
    /// proficiency id into <c>Init(FTK_proficiencyTable.ID)</c> itself.
    /// </summary>
    internal static class BehaviorHost
    {
        // Far enough below the world that the parked host is never visible or interactable; matches the
        // value the demo content used so behaviour goes on behaving exactly as before.
        private static readonly Vector3 ParkPosition = new Vector3(0f, -100000f, 0f);

        /// <summary>
        /// Create a parked, persistent GameObject named <paramref name="gameObjectName"/> and add
        /// <paramref name="behaviorType"/> to it as a component, returning the <c>ProficiencyBase</c>
        /// instance. Returns null (with a warning) if <paramref name="behaviorType"/> is null or not
        /// assignable to <c>ProficiencyBase</c>; the registry already guards this, so this is defensive.
        /// </summary>
        internal static ProficiencyBase Create(Type behaviorType, string gameObjectName)
        {
            if (behaviorType == null)
            {
                Plugin.Log.LogWarning("BehaviorHost: cannot host a null behaviour type.");
                return null;
            }

            if (!typeof(ProficiencyBase).IsAssignableFrom(behaviorType))
            {
                Plugin.Log.LogWarning("BehaviorHost: type '" + behaviorType.FullName +
                    "' is not assignable to ProficiencyBase; not hosting.");
                return null;
            }

            GameObject host = new GameObject(gameObjectName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.transform.position = ParkPosition; // park off-screen, stays active

            // AddComponent(Type) (not the generic overload) because the type is dynamic at registration time.
            return host.AddComponent(behaviorType) as ProficiencyBase;
        }
    }
}
