using System;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Passive, bounded provenance for a single already-bound enemy renderer.  This
// never selects a target, sends a trigger, advances combat, or changes a native
// object.  The caller still owns every gameplay action through the existing
// bridge and ordinary recorder.
public sealed partial class RuntimeModelTest
{
    sealed class CombatMotionArm
    {
        internal int rendererId, ownerId, celId, animatorId, startedFrame;
        internal float startedRealtime;
        internal EnemyDummy enemy;
        internal CharacterEventListener cel;
        internal Animator animator;
        internal FTKPlayerID targetFid;
        internal string source, error;
        internal JObject baseline;
        internal JObject termination;
        internal JArray events = new JArray();
        internal bool stopped;
    }

    CombatMotionArm combatMotionArm;
    static RuntimeModelTest combatMotionObserver;
    static bool combatMotionHooks;

    static JObject MotionFid(FTKPlayerID value)
    {
        return new JObject { { "photonId", value.m_PhotonID }, { "turnIndex", value.m_TurnIndex } };
    }

    static bool MotionFidEquals(FTKPlayerID left, FTKPlayerID right)
    {
        return left.m_PhotonID == right.m_PhotonID && left.m_TurnIndex == right.m_TurnIndex;
    }

    static JObject MotionDamage(DummyDamageInfo info)
    {
        if (info == null) return null;
        return new JObject {
            { "attackerFid", MotionFid(info.m_AttackerID) }, { "victimFid", MotionFid(info.m_VictimID) },
            { "damage", info.m_Damage }, { "newHealth", info.m_NewHealth },
            { "attackerHealthMod", info.m_AttackerHealthMod }, { "proficiencyId", (int)info.m_Prof },
            { "proficiencySuccess", info.m_ProfSuccess }, { "attackResponse", info.m_AttackResponse.ToString() }
        };
    }

    static JObject MotionAnimatorState(Animator animator)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool transition = animator.IsInTransition(0);
        return new JObject {
            { "animatorInstanceId", animator.GetInstanceID() },
            { "baseLayerIdle", !transition && state.IsName("Base Layer.IDLE") },
            { "baseLayerTransition", transition }, { "baseLayerStateHash", state.fullPathHash },
            { "baseLayerNormalizedTime", state.normalizedTime }, { "animatorEnabled", animator.enabled },
            { "animatorActive", animator.isActiveAndEnabled }
        };
    }

    void InstallCombatMotionHooks()
    {
        if (combatMotionHooks) return;
        MethodInfo trigger = typeof(CharacterEventListener).GetMethod("CombatTrigger", Members, null,
            new[] { typeof(CharacterEventListener.CombatAnimTrigger), typeof(bool) }, null);
        MethodInfo attack = typeof(CharacterDummy).GetMethod("PlayAttackSequence", Members, null,
            new[] { typeof(CharacterDummy.AttackAnim), typeof(CharacterEventListener.CombatAnimTrigger),
                typeof(DummyDamageInfo), typeof(DummyDamageInfo), typeof(DummyDamageInfo) }, null);
        if (trigger == null || attack == null) throw new InvalidOperationException("Exact native combat motion methods unavailable.");
        Harmony harmony = new Harmony("com.ftkmf.runtime-model-test.combat-motion");
        harmony.Patch(trigger, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("MotionTriggerPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("MotionTriggerFinalizer", Statics)), null);
        harmony.Patch(attack, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("MotionAttackPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("MotionAttackFinalizer", Statics)), null);
        combatMotionHooks = true;
    }

    static void MotionRecord(CombatMotionArm arm, JObject item)
    {
        if (arm.events.Count >= 64)
        {
            if (arm.error == null) arm.error = "Native combat motion event bound64 exceeded.";
            return;
        }
        item["sequence"] = arm.events.Count;
        arm.events.Add(item);
    }

    static void RequireMotionArmExact(CombatMotionArm arm)
    {
        if (arm == null || arm.enemy == null || arm.cel == null || arm.animator == null
            || !ReferenceEquals(arm.enemy.m_EventListener, arm.cel) || !ReferenceEquals(arm.cel.m_Dummy, arm.enemy)
            || !ReferenceEquals(arm.cel.m_Animator, arm.animator) || arm.animator.GetInstanceID() != arm.animatorId
            || !MotionFidEquals(arm.enemy.FID, arm.targetFid))
            throw new InvalidOperationException("Exact target CEL, native animator, or FID changed during motion observation.");
    }

    static void MotionTriggerPrefix(CharacterEventListener __instance, CharacterEventListener.CombatAnimTrigger _t,
        bool _forceAnim, ref JObject __state)
    {
        __state = null;
        RuntimeModelTest observer = combatMotionObserver;
        CombatMotionArm arm = observer == null ? null : observer.combatMotionArm;
        if (arm == null || arm.stopped || __instance == null || !ReferenceEquals(__instance, arm.cel)) return;
        try
        {
            RequireMotionArmExact(arm);
            JObject record = new JObject {
                { "nativeMethod", "CharacterEventListener.CombatTrigger entry" }, { "frame", Time.frameCount },
                { "realtime", Time.realtimeSinceStartup }, { "targetOwnerInstanceId", arm.ownerId },
                { "targetCelInstanceId", arm.celId }, { "targetRendererInstanceId", arm.rendererId },
                { "targetAnimatorInstanceId", arm.animatorId }, { "targetFid", MotionFid(arm.targetFid) },
                { "trigger", _t.ToString() }, { "forceAnim", _forceAnim },
                { "preIssueLastTrigger", arm.cel.m_LastTrigger.ToString() }, { "finalized", false }
            };
            MotionRecord(arm, record);
            __state = record;
        }
        catch (Exception error)
        {
            if (arm.error == null) arm.error = "Native trigger telemetry failed: " + error;
        }
    }

    static Exception MotionTriggerFinalizer(CharacterEventListener __instance, Exception __exception, JObject __state)
    {
        if (__state == null) return __exception;
        __state["finalized"] = true;
        __state["exception"] = __exception == null ? new JValue((object)null)
            : new JValue(__exception.GetType().FullName + ": " + __exception.Message);
        if (__instance != null) __state["postIssueLastTrigger"] = __instance.m_LastTrigger.ToString();
        return __exception;
    }

    static void MotionAttackPrefix(CharacterDummy __instance, CharacterDummy.AttackAnim _attackAnim,
        CharacterEventListener.CombatAnimTrigger _override, DummyDamageInfo _ddi, DummyDamageInfo _ddi1,
        DummyDamageInfo _ddi2, ref JObject __state)
    {
        __state = null;
        RuntimeModelTest observer = combatMotionObserver;
        CombatMotionArm arm = observer == null ? null : observer.combatMotionArm;
        if (arm == null || arm.stopped || __instance == null) return;
        try
        {
            RequireMotionArmExact(arm);
            bool attacker = ReferenceEquals(__instance, arm.enemy);
            bool victim = _ddi != null && MotionFidEquals(_ddi.m_VictimID, arm.enemy.FID);
            if (!attacker && !victim) return;
            __state = new JObject {
                { "nativeMethod", "CharacterDummy.PlayAttackSequence entry" }, { "frame", Time.frameCount },
                { "realtime", Time.realtimeSinceStartup }, { "targetOwnerInstanceId", arm.ownerId },
                { "targetCelInstanceId", arm.celId }, { "targetRendererInstanceId", arm.rendererId },
                { "targetAnimatorInstanceId", arm.animatorId }, { "targetFid", MotionFid(arm.targetFid) },
                { "role", attacker ? "attacker" : "victim" }, { "actorInstanceId", __instance.GetInstanceID() },
                { "actorFid", MotionFid(__instance.FID) }, { "attackAnim", _attackAnim.ToString() },
                { "override", _override.ToString() }, { "primary", MotionDamage(_ddi) },
                { "secondary", MotionDamage(_ddi1) }, { "tertiary", MotionDamage(_ddi2) },
                { "finalized", false }
            };
            MotionRecord(arm, __state);
        }
        catch (Exception error)
        {
            if (arm.error == null) arm.error = "Native attack telemetry failed: " + error;
        }
    }

    static Exception MotionAttackFinalizer(CharacterDummy __instance, Exception __exception, JObject __state)
    {
        if (__state == null) return __exception;
        __state["finalized"] = true;
        __state["exception"] = __exception == null ? new JValue((object)null)
            : new JValue(__exception.GetType().FullName + ": " + __exception.Message);
        RuntimeModelTest observer = combatMotionObserver;
        CombatMotionArm arm = observer == null ? null : observer.combatMotionArm;
        if (arm != null && !arm.stopped && arm.enemy != null)
        {
            __state["postTargetAlive"] = arm.enemy.m_IsAlive;
            __state["postTargetHealth"] = arm.enemy.m_CurrentHealth;
        }
        return __exception;
    }

    CombatMotionArm ArmCombatMotionObservation(SkinnedMeshRenderer renderer, string source)
    {
        if (combatMotionArm != null || combatMotionObserver != null)
            throw new InvalidOperationException("Another combat motion observation is active.");
        AvatarOwner owner = FindOwner(renderer, "enemies");
        EnemyDummy enemy = owner.owner as EnemyDummy;
        CharacterEventListener cel = owner.cel;
        if (enemy == null || cel == null || enemy.m_EventListener != cel || cel.m_Dummy != enemy || !enemy.m_IsAlive || enemy.m_CurrentHealth <= 0)
            throw new InvalidOperationException("Combat motion observation requires one alive exact enemy CEL.");
        Animator animator = Controller(renderer);
        if (animator == null || !ReferenceEquals(animator, cel.m_Animator))
            throw new InvalidOperationException("Combat motion observation requires the selected renderer to use the target CEL native animator.");
        if (!animator.isActiveAndEnabled || animator.layerCount < 1)
            throw new InvalidOperationException("Combat motion observation requires an enabled native animator.");
        JObject baseline = MotionAnimatorState(animator);
        if (!(bool)baseline["baseLayerIdle"])
            throw new InvalidOperationException("Combat motion observation requires settled Base Layer.IDLE before any action.");
        InstallCombatMotionHooks();
        CombatMotionArm arm = new CombatMotionArm {
            rendererId = renderer.GetInstanceID(), ownerId = enemy.GetInstanceID(), celId = cel.GetInstanceID(), animatorId = animator.GetInstanceID(),
            startedFrame = Time.frameCount, startedRealtime = Time.realtimeSinceStartup, enemy = enemy, cel = cel,
            animator = animator, targetFid = enemy.FID, source = source, baseline = baseline
        };
        combatMotionArm = arm;
        combatMotionObserver = this;
        return arm;
    }

    bool CheckCombatMotionObservation(CombatMotionArm arm, SkinnedMeshRenderer renderer)
    {
        if (arm == null) return false;
        if (!ReferenceEquals(combatMotionArm, arm) || combatMotionObserver != this || arm.stopped)
            throw new InvalidOperationException("Combat motion observation ownership changed.");
        if (arm.error != null) throw new InvalidOperationException(arm.error);
        if (renderer == null) throw new InvalidOperationException("Renderer destroyed during motion observation.");
        AvatarOwner owner = FindOwner(renderer, "enemies");
        EnemyDummy enemy = owner.owner as EnemyDummy;
        if (enemy == null || enemy.GetInstanceID() != arm.ownerId || owner.cel == null || owner.cel.GetInstanceID() != arm.celId
            || renderer.GetInstanceID() != arm.rendererId || !ReferenceEquals(enemy.m_EventListener, arm.cel))
            throw new InvalidOperationException("Exact motion observation target changed during capture.");
        RequireMotionArmExact(arm);
        Animator current;
        try { current = Controller(renderer); }
        catch (AvatarControllerResolutionException error)
        {
            if (!CombatMotionTermination.AfterObservedDeath(arm.events, error)) throw;
            arm.termination = new JObject {
                { "reason", "controller-unresolved-after-observed-native-death" },
                { "frame", Time.frameCount }, { "realtime", Time.realtimeSinceStartup },
                { "controllerError", error.Message }, { "terminalFrameCaptured", false },
                { "scope", "Accepted death observation prefix; requested capture duration was not completed." }
            };
            return true;
        }
        if (!ReferenceEquals(current, arm.animator))
            throw new InvalidOperationException("Selected renderer no longer uses the target CEL native animator.");
        return false;
    }

    static JObject CombatMotionObservationView(CombatMotionArm arm)
    {
        if (arm == null) return null;
        return new JObject {
            { "schema", "ftkmf.native-combat-motion.v1" }, { "source", arm.source },
            { "startedFrame", arm.startedFrame }, { "startedRealtime", arm.startedRealtime },
            { "targetOwnerInstanceId", arm.ownerId }, { "targetCelInstanceId", arm.celId },
            { "targetRendererInstanceId", arm.rendererId }, { "targetAnimatorInstanceId", arm.animatorId },
            { "targetFid", MotionFid(arm.targetFid) }, { "baseline", arm.baseline.DeepClone() },
            { "events", arm.events.DeepClone() }, { "eventCount", arm.events.Count },
            { "termination", arm.termination == null ? (JToken)new JValue((object)null) : arm.termination.DeepClone() },
            { "error", arm.error == null ? new JValue((object)null) : new JValue(arm.error) }
        };
    }

    void StopCombatMotionObservation(CombatMotionArm arm)
    {
        if (arm == null) return;
        arm.stopped = true;
        if (ReferenceEquals(combatMotionArm, arm)) combatMotionArm = null;
        if (combatMotionObserver == this) combatMotionObserver = null;
    }

    void ClearCombatMotionObservation()
    {
        if (combatMotionArm != null) combatMotionArm.stopped = true;
        combatMotionArm = null;
        if (combatMotionObserver == this) combatMotionObserver = null;
    }
}
