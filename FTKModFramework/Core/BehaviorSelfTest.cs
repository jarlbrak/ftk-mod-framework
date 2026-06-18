using System;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the P3 behaviour primitives (BehaviorRegistry + BehaviorHost) generalized to the
    /// CLOSED two-kind constraint (#39). It proves, with throwaway types and keys, that:
    /// <list type="bullet">
    /// <item>[behavior-primitives] a registered ProficiencyBase (kind=Proficiency) resolves back to the EXACT
    /// Type AND kind; the closed per-kind guard is intact (a type that does not match its kind's base is
    /// rejected and never stored, both for object-under-Proficiency and a ProficiencyBase-under-QuestLogic);
    /// and BehaviorHost.Create yields a live, correctly typed instance.</item>
    /// <item>[questlogic-instantiate] a registered QuestLogicBase subclass (kind=QuestLogic) resolves with
    /// kind=QuestLogic and instantiates via Activator.CreateInstance (NOT BehaviorHost: it is not a
    /// MonoBehaviour), yielding a QuestLogicBase instance.</item>
    /// </list>
    /// Emits exactly one "SELF-TEST PASS [behavior-primitives]" and one "SELF-TEST PASS [questlogic-instantiate]"
    /// line on success (or matching FAIL lines), in the same style as the class/enemy self-tests. Cleans up its
    /// host GameObject.
    ///
    /// Run unconditionally from the plugin postfix (it does not depend on EnableSampleContent): it only touches
    /// its own throwaway keys/types and registers nothing the game or other content can observe.
    /// </summary>
    internal static class BehaviorSelfTest
    {
        // A no-op ProficiencyBase subclass used only by this self-test. It overrides nothing: the test never
        // drives combat, it only checks type identity and the hosting lifecycle.
        private sealed class _SelfTestBehavior : ProficiencyBase
        {
        }

        // A no-op QuestLogicBase subclass used only by this self-test. QuestLogicBase is a plain serializable
        // class (NOT a MonoBehaviour) with a public parameterless ctor (decompile-confirmed), so this subclass
        // inherits an implicit public parameterless ctor and is Activator.CreateInstance-able.
        private sealed class _SelfTestQuestLogic : QuestLogicBase
        {
        }

        public static void Run()
        {
            RunProficiencyPrimitives();
            RunQuestLogicInstantiate();
        }

        // [behavior-primitives]: proficiency resolve + closed-guard negatives + BehaviorHost.Create.
        private static void RunProficiencyPrimitives()
        {
            const string okKey = "com.ftkmf.framework:_selftest_behavior";
            const string rejectObjKey = "com.ftkmf.framework:_selftest_reject_object";
            const string rejectKindKey = "com.ftkmf.framework:_selftest_reject_kind";

            try
            {
                // 1) A registered ProficiencyBase subclass resolves back to the EXACT Type AND kind=Proficiency.
                BehaviorRegistry.Register(okKey, typeof(_SelfTestBehavior), BehaviorKind.Proficiency);
                Type resolved;
                BehaviorKind resolvedKind;
                bool resolvedOk = BehaviorRegistry.TryResolve(okKey, out resolved, out resolvedKind)
                    && resolved == typeof(_SelfTestBehavior)
                    && resolvedKind == BehaviorKind.Proficiency;

                // 2a) object under kind=Proficiency does not match the kind's base: rejected, never stored.
                BehaviorRegistry.Register(rejectObjKey, typeof(object), BehaviorKind.Proficiency); // warns + skips
                Type rejectedObj;
                bool rejectedObjOk = !BehaviorRegistry.TryResolve(rejectObjKey, out rejectedObj);

                // 2b) A ProficiencyBase under kind=QuestLogic does not match QuestLogicBase: rejected, never
                // stored. This proves the guard is CLOSED per kind, not just "anything-but-object".
                BehaviorRegistry.Register(rejectKindKey, typeof(_SelfTestBehavior), BehaviorKind.QuestLogic); // warns + skips
                Type rejectedKind;
                bool rejectedKindOk = !BehaviorRegistry.TryResolve(rejectKindKey, out rejectedKind);

                // 3) BehaviorHost.Create yields a live, correctly typed instance on an active GameObject.
                ProficiencyBase hosted = BehaviorHost.Create(typeof(_SelfTestBehavior), "ftkmf_SelfTestBehavior");
                bool hostOk = hosted is _SelfTestBehavior && hosted.gameObject != null && hosted.gameObject.activeInHierarchy;

                // Tear the parked host down so the self-test leaves nothing behind.
                if (hosted != null) UnityEngine.Object.Destroy(hosted.gameObject);

                bool ok = resolvedOk && rejectedObjOk && rejectedKindOk && hostOk;
                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [behavior-primitives]: registry resolves exact Type+kind " +
                        "(Proficiency), closed guard rejects object-under-Proficiency AND ProficiencyBase-under-" +
                        "QuestLogic, BehaviorHost.Create yields a live " + typeof(_SelfTestBehavior).Name + ".");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [behavior-primitives]: resolvedOk=" + resolvedOk +
                        " rejectedObjOk=" + rejectedObjOk + " rejectedKindOk=" + rejectedKindOk +
                        " hostOk=" + hostOk + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [behavior-primitives]: " + e);
            }
        }

        // [questlogic-instantiate]: questlogic resolve (kind=QuestLogic) + Activator.CreateInstance (NOT hosted).
        private static void RunQuestLogicInstantiate()
        {
            const string qlKey = "com.ftkmf.framework:_selftest_questlogic";

            try
            {
                // 1) A registered QuestLogicBase subclass resolves with kind=QuestLogic.
                BehaviorRegistry.Register(qlKey, typeof(_SelfTestQuestLogic), BehaviorKind.QuestLogic);
                Type resolved;
                BehaviorKind resolvedKind;
                bool resolvedOk = BehaviorRegistry.TryResolve(qlKey, out resolved, out resolvedKind)
                    && resolved == typeof(_SelfTestQuestLogic)
                    && resolvedKind == BehaviorKind.QuestLogic;

                // 2) Instantiate via Activator.CreateInstance (the questlogic path), NOT BehaviorHost. The
                // resolved type is dynamic, so the non-generic Activator.CreateInstance(Type) overload is used.
                // Activator throws if there is no accessible parameterless ctor, so a successful create also
                // proves QuestLogicBase's parameterless ctor is reachable through the subclass.
                object created = resolved != null ? Activator.CreateInstance(resolved) : null;
                bool createdOk = created is QuestLogicBase && created is _SelfTestQuestLogic;

                // 3) Prove it was NOT routed through the MonoBehaviour host path: a QuestLogicBase is not a
                // Component, so it has no GameObject. (BehaviorHost.Create would have returned null for it.)
                bool notHosted = !(created is UnityEngine.MonoBehaviour) && !(created is UnityEngine.Component);

                bool ok = resolvedOk && createdOk && notHosted;
                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [questlogic-instantiate]: QuestLogicBase subclass created " +
                        "via Activator (" + typeof(_SelfTestQuestLogic).Name + " resolved kind=QuestLogic, " +
                        "instance is a QuestLogicBase, not routed through BehaviorHost).");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [questlogic-instantiate]: resolvedOk=" + resolvedOk +
                        " createdOk=" + createdOk + " notHosted=" + notHosted + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [questlogic-instantiate]: " + e);
            }
        }
    }
}
