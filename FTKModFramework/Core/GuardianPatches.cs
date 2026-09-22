using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiBattleStanceButtons), "CreateWeaponProficiencyButtons")]
    internal static class GuardianActionButtonPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance)
        {
            try
            {
                CharacterDummy guardian = __instance.CombatCow.GetCombatDummy();
                if (!GuardianRuntime.IsGuardian(guardian)) return;
                uiBattleStanceButtons.ProfValues entry = new uiBattleStanceButtons.ProfValues();
                entry.m_Prof = GuardianRuntime.ActionId;
                entry.m_Button = UnityEngine.Object.Instantiate(__instance.m_ProficiencyButtonMaster);
                entry.m_Button.transform.SetParent(__instance.m_ProficiencyButtonMaster.transform.parent, false);
                entry.m_Button.m_Owner = __instance;
                entry.m_Button.m_ButtonType = uiBattleButton.BattleButtonType.proficiency;
                entry.m_Button.gameObject.GetComponent<Image>().sprite = FTK_proficiencyTableDB.Get(entry.m_Prof).m_BattleButton;
                __instance.m_Proficiencies.Add(entry);
                entry.m_Button.SetCanUse(GuardianRuntime.HasTarget(guardian));
                entry.m_Button.gameObject.SetActive(true);
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] action button failed: " + e); }
        }
    }

    internal static class GuardianActionUi
    {
        internal static bool IsGuard(uiBattleStanceButtons owner, uiBattleButton button)
        {
            if (owner == null || button == null || GuardianRuntime.ActionId == FTK_proficiencyTable.ID.None) return false;
            foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
                if (entry.m_Button == button) return entry.m_Prof == GuardianRuntime.ActionId;
            return false;
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class GuardianActionProfilePatch
    {
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            try
            {
                if (!_on || !GuardianActionUi.IsGuard(__instance, _button)) return;
                __instance.m_CombatActionProfile.m_Slots = 0;
                __instance.m_CombatActionProfile.m_NoFocus = true;
                __instance.m_InfoPanel.m_ACCRoot.SetActive(false);
                __instance.m_InfoPanel.m_DMGRoot.SetActive(false);
                // Native index 0 is the short target label; index 1 is the effect body.
                // Writing full rules into both also overflows the single-line target field.
                Text[] descriptions = __instance.m_InfoPanel.m_Description;
                if (descriptions != null && descriptions.Length > 0 && descriptions[0] != null)
                    descriptions[0].text = "Target: another ally";
                if (descriptions != null && descriptions.Length > 1 && descriptions[1] != null)
                    descriptions[1].text = "50% less direct damage.\nUntil your next turn or incapacitation.\nAlways succeeds. No Focus.";
                uiToolTipFocusable.gCanFocus = false;
                FTKUI.Instance.m_PlayerSlots.ResetSlots();
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] action profile failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "FocusSlot")]
    internal static class GuardianNoFocusPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            return !GuardianActionUi.IsGuard(__instance, _button);
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "AttackProficiency")]
    internal static class GuardianActionCommitPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            if (!GuardianActionUi.IsGuard(__instance, _button)) return true;
            try
            {
                CharacterDummy guardian = __instance.CombatCow.GetCombatDummy();
                if (__instance.m_Focusing || !GuardianRuntime.IsGuardian(guardian) || !GuardianRuntime.HasTarget(guardian)) return false;
                // Refund Focus chosen for a different action. The native direct path spends no Focus.
                __instance.CombatCow.m_CharacterStats.ResetSpentFocus(true);
                __instance.BattleButtonsOff(true);
                guardian.EngageDirectAttack(GuardianRuntime.ActionId);
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] action commit failed: " + e); }
            // Never fall through to weapon slot rolls after committing a class action.
            return false;
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_waitForUserPickTarget")]
    internal static class GuardianTargetFilterPatch
    {
        private static void Prefix(List<CharacterDummy> _party, AttackAttempt _av)
        {
            if (_av.m_AttackProficiency != GuardianRuntime.ActionId || GuardianRuntime.ActionId == FTK_proficiencyTable.ID.None) return;
            for (int i = _party.Count - 1; i >= 0; i--)
                if (_party[i] == _av.m_AttackingDummy || !GuardianRuntime.LivingAlly(_party[i])) _party.RemoveAt(i);
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "PlayAttackSequence")]
    internal static class GuardianDamagePatch
    {
        private static void Prefix(CharacterDummy __instance, ref DummyDamageInfo _ddi,
            ref DummyDamageInfo _ddi1, ref DummyDamageInfo _ddi2)
        {
            try
            {
                if (!GuardianRuntime.Enabled) return;
                if (!(__instance is EnemyDummy))
                {
                    GuardianRuntime.ReplayReckoning(__instance, _ddi);
                    return;
                }
                GuardianRuntime.RefreshEligibility();
                // All outcomes in an area attack use the pre-impact guardian eligibility snapshot.
                // Clone before changing anything: RPCAllSelf may still serialize its original args.
                int first, second, third;
                _ddi = Transform(__instance, _ddi, out first);
                _ddi1 = Transform(__instance, _ddi1, out second);
                _ddi2 = Transform(__instance, _ddi2, out third);
                // The native sequence sums attacker health modifiers from all victims, then routes
                // secondary damage through its normal death/FSM path. Add one bonus to the main result.
                if (_ddi != null) _ddi.m_AttackerHealthMod = GuardianEquipmentBonuses.AddRetaliation(
                    _ddi.m_AttackerHealthMod, first, second, third);
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] damage resolution failed: " + e); }
        }

        private static DummyDamageInfo Transform(CharacterDummy attacker, DummyDamageInfo original, out int retaliation)
        {
            retaliation = 0;
            if (original == null) return null;
            DummyDamageInfo copy = new DummyDamageInfo();
            copy.Set(original);
            retaliation = GuardianRuntime.ResolveDamage(attacker, copy);
            return copy;
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_playAttackSequence")]
    internal static class GuardianSequencePatch
    {
        private static void Prefix(ref AttackAttempt _atk, DummyDamageInfo _ddi0, DummyDamageInfo _ddi1, DummyDamageInfo _ddi2)
        {
            try
            {
                if (_atk.m_AttackProficiency == GuardianRuntime.ActionId && GuardianRuntime.ActionId != FTK_proficiencyTable.ID.None)
                {
                    // The harmless branch overwrites the original direct animation. Restore it so
                    // any equipped weapon can use Guard without requiring a weapon-specific clip.
                    _atk.m_AttackAnim = CharacterDummy.AttackAnim.DirectAttack;
                    _atk.m_AttackAnimOverride = CharacterEventListener.CombatAnimTrigger.None;
                }
                GuardianRuntime.QueueHealing(_atk, _ddi0, _ddi1, _ddi2);
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] attack sequence failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "RespondToHit")]
    internal static class GuardianImpactPatch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            try
            {
                if (!GuardianRuntime.CanAct(__instance)) GuardianRuntime.Expire(__instance);
                GuardianRuntime.OnImpact(__instance);
            }
            catch (Exception e) { Plugin.Log.LogError("[guardian] impact feedback/healing failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "EngageBattle")]
    internal static class GuardianTurnPatch
    {
        private static void Prefix(CharacterDummy __instance) { GuardianRuntime.BeginTurn(__instance); }
    }

    [HarmonyPatch(typeof(CharacterDummy), "AddProfToDummy")]
    internal static class GuardianIncapacityPatch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            try { if (!GuardianRuntime.CanAct(__instance)) GuardianRuntime.Expire(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[guardian] incapacitation check failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "ResetForCombat")]
    internal static class GuardianActorResetPatch
    {
        // Resurrection uses this native reset during the same combat.
        private static void Postfix(CharacterDummy __instance) { GuardianRuntime.ResetActor(__instance); }
    }

    [HarmonyPatch(typeof(CharacterDummy), "InitDummyForCombat")]
    internal static class GuardianStartPatch
    {
        // Fresh combat and the next dungeon battle initialize here; resurrection does not.
        private static void Prefix(CharacterDummy __instance) { GuardianRuntime.Reset(__instance); }
    }

    [HarmonyPatch(typeof(CharacterDummy), "CombatFinished")]
    internal static class GuardianEndPatch
    {
        // Finishing one combatant must not recharge the other guardians in an ongoing encounter.
        private static void Prefix(CharacterDummy __instance)
        {
            GuardianRuntime.Expire(__instance);
            GuardianRuntime.EndLegendaryCombat(__instance);
        }
    }
}
