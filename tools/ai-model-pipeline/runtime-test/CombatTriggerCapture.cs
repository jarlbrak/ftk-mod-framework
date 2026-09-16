using System;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    void StartCombatTriggerCapture(string id, JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "scope", "ownerInstanceId", "rendererId", "rendererPath",
            "expectedMesh", "boneSignature", "seconds", "fps", "maxWidth", "fixedStep");
        if (Scope(command) != "enemies" || Int(command, "ownerInstanceId", 0) == 0)
            throw new ArgumentException("Combat trigger capture requires an exact live enemy owner.");

        SkinnedMeshRenderer renderer = Resolve(command);
        AvatarOwner avatar = FindOwner(renderer, "enemies");
        EnemyDummy enemy = avatar.owner as EnemyDummy;
        CharacterEventListener cel = avatar.cel;
        if (enemy == null || cel == null || enemy.m_EventListener != cel || cel.m_Dummy != enemy || !enemy.m_IsAlive || enemy.m_CurrentHealth <= 0)
            throw new InvalidOperationException("Combat trigger capture requires one alive native enemy CEL.");
        EncounterSession encounter = RequireCombat();
        if (encounter.m_EnemyDummies == null || encounter.m_EnemyDummies.Count != 1)
            throw new InvalidOperationException("Combat trigger capture requires a sole current native enemy.");
        EnemyDummy mapped;
        if (!encounter.m_EnemyDummies.TryGetValue(enemy.FID, out mapped) || mapped != enemy)
            throw new InvalidOperationException("Combat trigger capture enemy FID map membership changed.");
        Animator animator = cel.m_Animator;
        if (animator == null || !animator.isActiveAndEnabled || !HasTrigger(animator, "DeathLight"))
            throw new InvalidOperationException("Combat trigger capture requires an enabled native DeathLight trigger.");
        JObject gate = RequireSettledCombatTriggerWindow(enemy, animator, encounter);

        float seconds = Number(command, "seconds", 2f), fps = Number(command, "fps", 10f);
        if (seconds <= 0 || seconds > 10 || fps < 1 || fps > 20 || seconds * fps > 120)
            throw new ArgumentException("Capture limits: 0<seconds<=10, 1<=fps<=20, <=120 frames.");
        int maxWidth = Int(command, "maxWidth", 1280);
        bool fixedStep = command["fixedStep"] != null && (bool)command["fixedStep"];
        if (maxWidth < 320 || maxWidth > 3840) throw new ArgumentException("maxWidth must be between 320 and 3840.");
        if (fixedStep && fps != (float)(int)fps) throw new ArgumentException("fixedStep requires integer fps.");

        CombatMotionArm motion = null;
        try
        {
            // The passive arm must exist before this diagnostic's direct native
            // call.  It records that one actual CEL entry, but does not turn the
            // semantic DeathLight probe into ordinary lethal-damage evidence.
            motion = ArmCombatMotionObservation(renderer, "semantic-native-deathlight");
            string before = cel.m_LastTrigger.ToString();
            enemy.PlayAnim(CharacterEventListener.CombatAnimTrigger.DeathLight);
            if (cel.m_LastTrigger != CharacterEventListener.CombatAnimTrigger.DeathLight)
                throw new InvalidOperationException("Native EnemyDummy.PlayAnim did not establish DeathLight on the CEL.");

            JObject observation = new JObject {
                { "issuedMethod", "EnemyDummy.PlayAnim" }, { "trigger", "DeathLight" },
                { "expectedLastTrigger", "DeathLight" }, { "preIssueLastTrigger", before },
                { "postIssueLastTrigger", cel.m_LastTrigger.ToString() },
                { "enemyInstanceId", enemy.GetInstanceID() }, { "celInstanceId", cel.GetInstanceID() },
                { "rendererInstanceId", renderer.GetInstanceID() }, { "soleEnemyCount", encounter.m_EnemyDummies.Count },
                { "preIssueGate", gate }
            };
            Logger.LogInfo("MODEL TEST COMBAT TRIGGER: EnemyDummy.PlayAnim(DeathLight); native CEL trigger path.");
            busy = true;
            StartCoroutine(Capture(id, renderer, seconds, fps, maxWidth, fixedStep,
                "native-combat-trigger:DeathLight", false, false, observation, motion));
        }
        catch
        {
            StopCombatMotionObservation(motion);
            throw;
        }
    }

    static bool HasTrigger(Animator animator, string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name) return true;
        return false;
    }

    static JObject CombatFid(FTKPlayerID fid)
    {
        return new JObject { { "photonId", fid.m_PhotonID }, { "turnIndex", fid.m_TurnIndex } };
    }

    static JObject RequireSettledCombatTriggerWindow(EnemyDummy enemy, Animator animator, EncounterSession encounter)
    {
        // m_ActionAnimationPlayed is latched by PlayAttackSequence and survives ActionCompleted.
        // It is recorded below as prior-action telemetry, never used as an active-action test.
        if (enemy.m_IsAttacking)
            throw new InvalidOperationException("Combat trigger capture refuses the current native attacker.");
        AnimatorStateInfo targetState = animator.GetCurrentAnimatorStateInfo(0);
        bool targetTransition = animator.IsInTransition(0);
        bool targetIdle = !targetTransition && targetState.IsName("Base Layer.IDLE");
        if (!targetIdle)
            throw new InvalidOperationException("Combat trigger capture requires the target at settled Base Layer.IDLE.");

        EncounterSessionMC master = (EncounterSessionMC)Instance(typeof(EncounterSessionMC));
        if (master == null || master.m_FightOrder == null || master.m_FightOrder.Count == 0)
            throw new InvalidOperationException("Combat trigger capture requires a current native fight-order head.");
        FTKPlayerID headFid = master.m_FightOrder[0].m_Pid;
        if (!headFid.IsPlayer())
            throw new InvalidOperationException("Combat trigger capture requires a player, not enemy, at the native fight-order head.");
        CharacterDummy player = encounter.GetDummyByFID(headFid);
        if (player == null || !player.FID.IsPlayer() || player.FID != headFid || !player.m_IsAlive)
            throw new InvalidOperationException("Combat trigger capture could not resolve the exact current player dummy.");
        if (player.m_ActionAnimationPlayed)
            throw new InvalidOperationException("Combat trigger capture refuses a player turn with an action animation already played.");
        if (player.m_CharacterDummyFSM == null || !player.m_CharacterDummyFSM.enabled || !player.m_CharacterDummyFSM.gameObject.activeInHierarchy
            || player.m_CharacterDummyFSM.ActiveStateName != "Wait For Stance")
            throw new InvalidOperationException("Combat trigger capture requires the current player in native Wait For Stance.");

        return new JObject {
            { "target", new JObject {
                { "enemyInstanceId", enemy.GetInstanceID() }, { "isAttacking", enemy.m_IsAttacking },
                { "priorActionAnimationPlayed", enemy.m_ActionAnimationPlayed }, { "baseLayerIdle", targetIdle },
                { "baseLayerTransition", targetTransition }, { "baseLayerStateHash", targetState.fullPathHash },
                { "baseLayerNormalizedTime", targetState.normalizedTime }
            } },
            { "playerTurn", new JObject {
                { "fightOrderHead", CombatFid(headFid) }, { "playerInstanceId", player.GetInstanceID() },
                { "alive", player.m_IsAlive }, { "actionAnimationPlayed", player.m_ActionAnimationPlayed },
                { "fsmState", player.m_CharacterDummyFSM.ActiveStateName }
            } },
            { "note", "Target priorActionAnimationPlayed is native historical telemetry; live safety requires a non-attacking settled target and a player Wait For Stance turn." }
        };
    }

    static EncounterSession RequireCombat()
    {
        object session = Instance(typeof(EncounterSession));
        object master = Instance(typeof(EncounterSessionMC));
        if (session == null || master == null ||
            !(bool)typeof(EncounterSession).GetField("m_IsInCombat", Members).GetValue(session) ||
            !(bool)typeof(EncounterSessionMC).GetField("m_IsInCombat", Members).GetValue(master))
            throw new InvalidOperationException("Combat trigger capture requires both native encounter sessions in combat.");
        return (EncounterSession)session;
    }
}
