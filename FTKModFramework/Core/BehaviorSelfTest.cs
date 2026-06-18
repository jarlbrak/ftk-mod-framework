using System;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the P3 behaviour primitives (BehaviorRegistry + BehaviorHost). It proves, with
    /// a throwaway no-op behaviour, that: a registered key resolves back to the EXACT Type; a non-
    /// ProficiencyBase type is rejected by the registry; and BehaviorHost.Create yields a live, correctly
    /// typed instance. Emits exactly one "SELF-TEST PASS [behavior-primitives]" line on success (or a
    /// matching FAIL line), in the same style as the class/enemy self-tests. Cleans up its host GameObject.
    ///
    /// Run unconditionally from the plugin postfix (it does not depend on EnableSampleContent): it only
    /// touches its own throwaway keys/types and registers nothing the game or other content can observe.
    /// </summary>
    internal static class BehaviorSelfTest
    {
        // A no-op ProficiencyBase subclass used only by this self-test. It overrides nothing: the test
        // never drives combat, it only checks type identity and the hosting lifecycle.
        private sealed class _SelfTestBehavior : ProficiencyBase
        {
        }

        public static void Run()
        {
            const string okKey = "com.ftkmf.framework:_selftest_behavior";
            const string rejectKey = "com.ftkmf.framework:_selftest_reject";

            try
            {
                // 1) A registered ProficiencyBase subclass resolves back to the EXACT same Type.
                BehaviorRegistry.Register(okKey, typeof(_SelfTestBehavior));
                Type resolved;
                bool resolvedOk = BehaviorRegistry.TryResolve(okKey, out resolved) && resolved == typeof(_SelfTestBehavior);

                // 2) A non-ProficiencyBase type is rejected: it is never stored, so TryResolve misses.
                BehaviorRegistry.Register(rejectKey, typeof(object)); // logs a warning + skips storing
                Type rejected;
                bool rejectedOk = !BehaviorRegistry.TryResolve(rejectKey, out rejected);

                // 3) BehaviorHost.Create yields a live, correctly typed instance on an active GameObject.
                ProficiencyBase hosted = BehaviorHost.Create(typeof(_SelfTestBehavior), "ftkmf_SelfTestBehavior");
                bool hostOk = hosted is _SelfTestBehavior && hosted.gameObject != null && hosted.gameObject.activeInHierarchy;

                // Tear the parked host down so the self-test leaves nothing behind.
                if (hosted != null) UnityEngine.Object.Destroy(hosted.gameObject);

                bool ok = resolvedOk && rejectedOk && hostOk;
                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [behavior-primitives]: registry resolves exact Type, " +
                        "rejects non-ProficiencyBase, BehaviorHost.Create yields a live " + typeof(_SelfTestBehavior).Name + ".");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [behavior-primitives]: resolvedOk=" + resolvedOk +
                        " rejectedOk=" + rejectedOk + " hostOk=" + hostOk + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [behavior-primitives]: " + e);
            }
        }
    }
}
