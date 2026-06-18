using System;
using System.Collections.Generic;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The custom-objective-verb RESOLVER seam (#40). It substitutes a registered custom <c>QuestLogicBase</c>
    /// (e.g. <see cref="CollectNQuestLogic"/>) for a <see cref="ModQuestDef"/> at the single choke point where
    /// the game turns a quest DEFINITION into its runtime objective LOGIC:
    /// <c>QuestLogicBase.GetQuestLogicTypeInstanceFromQuestDef</c> (static, public). That method's body is
    /// <c>Activator.CreateInstance(_getQuestLogicSystemTypeFromQuestType(_questDef.GetQuestType()), ...)</c>, and
    /// it is the only path that builds a story/multi-subquest QuestLogic (call sites:
    /// <c>GameEventManager._startStoryQuest</c> and <c>MultiQuestLogic</c>), so this one patch covers BOTH.
    ///
    /// PREFIX, NOT POSTFIX (decompile-grounded deviation from the work-item text, which said "Postfix"): the
    /// game method has NO try/catch. If a ModQuestDef's <c>GetQuestType()</c> hit the resolver's <c>default</c>
    /// branch, <c>_getQuestLogicSystemTypeFromQuestType</c> would return null and
    /// <c>Activator.CreateInstance(null, ...)</c> would THROW before any Postfix could observe the result. A
    /// Prefix that returns <c>false</c> substitutes <c>__result</c> WITHOUT ever running the original, so the
    /// null-Type throw is impossible on the custom-verb path. (ModQuestDef's GetQuestType still returns a benign
    /// vanilla Visit so the FALL-THROUGH path below is also safe.)
    ///
    /// IDEMPOTENCY (NFR-4): this is NOT a one-shot registration patch and needs NO <c>_done</c> guard. It is a
    /// per-call PURE transform of its arguments with no shared static state, exactly mirroring the original's
    /// per-call <c>Activator.CreateInstance</c> semantics: calling it twice with the same args yields two fresh
    /// equivalent instances, just as the unpatched game would. The patch CLASS is installed exactly once via the
    /// plugin's single <c>Harmony.PatchAll()</c> (the existing one-shot registration path).
    ///
    /// CAMPAIGN-ENGINE GATE (#43): PREPARE-GATED on <c>Plugin.EnableCampaignEngine</c> (same mechanism as
    /// <see cref="QuestRouterPatch"/>). HarmonyX's <c>PatchAll</c> calls <c>static bool Prepare()</c> before
    /// installing; returning false means this Prefix is never attached to
    /// <c>GetQuestLogicTypeInstanceFromQuestDef</c> (provably identical to today when the engine is OFF). When ON
    /// (the default == today's behavior) it installs and remains a no-op for any non-ModQuestDef quest. The flag
    /// is null-guarded for test contexts where <c>Plugin.Awake</c> never ran (defaults to ON).
    /// </summary>
    [HarmonyPatch(typeof(QuestLogicBase), "GetQuestLogicTypeInstanceFromQuestDef")]
    internal static class QuestVerbResolverPatch
    {
        // HarmonyX PatchAll honors a class-level Prepare(): return false => the patch is not installed at all.
        // null (no Awake / test context) defaults to enabled, matching BehaviorLoader's null-guard convention.
        private static bool Prepare()
        {
            return Plugin.EnableCampaignEngine == null || Plugin.EnableCampaignEngine.Value;
        }

        // The method is STATIC, so there is no __instance; the def is arg 0 (_questDef). Parameter names match
        // the decompiled signature so Harmony binds them positionally regardless of name.
        private static bool Prefix(
            QuestDefBase _questDef, HexLandID _start, bool _isCurrent, int _masterQuestID,
            List<HexLand> _destHexes, ref QuestLogicBase __result)
        {
            ModQuestDef mod = _questDef as ModQuestDef;
            if (mod == null) return true; // vanilla quest def: run the original game method untouched.

            Type t;
            BehaviorKind kind;
            if (!BehaviorRegistry.TryResolve(mod.m_BehaviorKey, out t, out kind) || kind != BehaviorKind.QuestLogic)
            {
                // Dangling / wrong-kind verb key: WARN and FALL THROUGH to the vanilla path (return true), NEVER
                // throw. ModQuestDef.GetQuestType() returns Visit, which maps cleanly to VisitQuestLogic, so the
                // unpatched original builds a sane vanilla QuestLogic instead of crashing.
                Plugin.Log.LogWarning("QuestVerbResolver: ModQuestDef '" +
                    (mod.m_StoryQuestID ?? "<null>") + "' has unresolved verb key '" +
                    (mod.m_BehaviorKey ?? "<null>") + "' (not registered as a QuestLogic verb); " +
                    "falling through to the vanilla quest-type path.");
                return true;
            }

            // Build the custom QuestLogic the SAME way the original would (its 5-arg ctor), then skip the
            // original by returning false. BehaviorRegistry already guarantees t derives from QuestLogicBase
            // (kind=QuestLogic constraint, #39), so the cast and ctor match are safe.
            __result = (QuestLogicBase)Activator.CreateInstance(
                t, _questDef, _start, _isCurrent, _masterQuestID, _destHexes);
            return false;
        }
    }
}
