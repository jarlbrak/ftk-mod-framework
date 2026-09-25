using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiBattleStanceButtons), "CreateWeaponProficiencyButtons")]
    internal static class ThiefActionButtonPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance)
        {
            try
            {
                CharacterDummy thief = __instance.CombatCow.GetCombatDummy();
                if (!ThiefRuntime.IsThief(thief) || ThiefRuntime.SlipAwayId == GridEditor.FTK_proficiencyTable.ID.None) return;
                uiBattleStanceButtons.ProfValues entry = new uiBattleStanceButtons.ProfValues();
                entry.m_Prof = ThiefRuntime.SlipAwayId;
                entry.m_Button = UnityEngine.Object.Instantiate(__instance.m_ProficiencyButtonMaster);
                entry.m_Button.transform.SetParent(__instance.m_ProficiencyButtonMaster.transform.parent, false);
                entry.m_Button.m_Owner = __instance;
                entry.m_Button.m_ButtonType = uiBattleButton.BattleButtonType.proficiency;
                entry.m_Button.gameObject.GetComponent<Image>().sprite = GridEditor.FTK_proficiencyTableDB.Get(entry.m_Prof).m_BattleButton;
                __instance.m_Proficiencies.Add(entry);
                entry.m_Button.SetCanUse(ThiefRuntime.SlipAwayAvailable(thief));
                entry.m_Button.gameObject.SetActive(true);
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] Slip Away button failed: " + e); }
        }
    }

    internal static class ThiefActionUi
    {
        internal static bool IsSlipAway(uiBattleStanceButtons owner, uiBattleButton button)
        {
            if (owner == null || owner.m_Proficiencies == null || button == null ||
                ThiefRuntime.SlipAwayId == GridEditor.FTK_proficiencyTable.ID.None) return false;
            foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
                if (entry.m_Button == button) return entry.m_Prof == ThiefRuntime.SlipAwayId;
            return false;
        }

        internal static void RefreshSlipAway(uiBattleStanceButtons owner)
        {
            if (owner == null || !owner.m_Initialized || owner.CombatCow == null || owner.m_Proficiencies == null) return;
            CharacterDummy thief = owner.CombatCow.GetCombatDummy();
            if (!ThiefRuntime.IsThief(thief)) return;
            bool canUse = ThiefRuntime.SlipAwayAvailable(thief);
            bool used = ThiefRuntime.SlipAwayUsed(thief);
            foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
            {
                if (entry.m_Prof != ThiefRuntime.SlipAwayId || entry.m_Button == null) continue;
                entry.m_Button.SetCanUse(canUse);
                if (!used || owner.m_InfoPanel == null || owner.m_CombatActionProfile.m_Button != entry.m_Button) continue;
                Text[] descriptions = owner.m_InfoPanel.m_Description;
                if (descriptions != null && descriptions.Length > 1 && descriptions[1] != null)
                    descriptions[1].text = "Slip Away used.";
            }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "Update")]
    internal static class ThiefSlipAwayUiRefreshPatch
    {
        private static bool loggedRefreshFailure;

        private static void Postfix(uiBattleStanceButtons __instance)
        {
            try { ThiefActionUi.RefreshSlipAway(__instance); }
            catch (Exception e)
            {
                if (loggedRefreshFailure) return;
                loggedRefreshFailure = true;
                Plugin.Log.LogError("[thief] Slip Away UI refresh failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class ThiefActionProfilePatch
    {
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            try
            {
                if (!_on || !ThiefActionUi.IsSlipAway(__instance, _button)) return;
                __instance.m_CombatActionProfile.m_Slots = 0;
                __instance.m_CombatActionProfile.m_NoFocus = true;
                __instance.m_InfoPanel.m_ACCRoot.SetActive(false);
                __instance.m_InfoPanel.m_DMGRoot.SetActive(false);
                Text[] descriptions = __instance.m_InfoPanel.m_Description;
                if (descriptions != null && descriptions.Length > 0 && descriptions[0] != null)
                    descriptions[0].text = "Target: self";
                if (descriptions != null && descriptions.Length > 1 && descriptions[1] != null)
                    descriptions[1].text = ThiefRuntime.SlipAwayUsed(__instance.CombatCow.GetCombatDummy())
                        ? "Slip Away used."
                        : "Prepare your next precision attack.\nHalve the next direct enemy attack before your next turn.\nOnce per combat. No Focus.";
                uiToolTipFocusable.gCanFocus = false;
                FTKUI.Instance.m_PlayerSlots.ResetSlots();
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] Slip Away profile failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "FocusSlot")]
    internal static class ThiefNoFocusPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            return !ThiefActionUi.IsSlipAway(__instance, _button);
        }
    }

    [HarmonyPatch(typeof(uiBattleStanceButtons), "AttackProficiency")]
    internal static class ThiefActionCommitPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            if (!ThiefActionUi.IsSlipAway(__instance, _button)) return true;
            try
            {
                CharacterDummy thief = __instance.CombatCow.GetCombatDummy();
                if (__instance.m_Focusing || !ThiefRuntime.SlipAwayAvailable(thief)) return false;
                __instance.CombatCow.m_CharacterStats.ResetSpentFocus(true);
                __instance.BattleButtonsOff(true);
                thief.EngageDirectAttack(ThiefRuntime.SlipAwayId);
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] Slip Away commit failed: " + e); }
            return false;
        }
    }

    [HarmonyPatch(typeof(EncounterSession), "InitAttackTimeline")]
    internal static class ThiefEncounterStartPatch
    {
        private static void Postfix(EncounterSession __instance)
        {
            try { ThiefRuntime.BeginEncounter(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[thief] encounter initialization failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "EngageBattle")]
    internal static class ThiefTurnStartPatch
    {
        private static void Prefix(CharacterDummy __instance)
        {
            try { ThiefRuntime.BeginTurn(__instance); }
            catch (Exception e) { Plugin.Log.LogError("[thief] turn start failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(EncounterSession), "UpdateAttackTimeline")]
    internal static class ThiefTurnEndPatch
    {
        private static void Prefix(EncounterSession __instance, EncounterSessionMC.FightOrderEntry[] _foe)
        {
            try
            {
                if (_foe != null && _foe.Length > 0 && __instance != null)
                    ThiefRuntime.EndTurn(__instance.GetDummyByFID(_foe[0].m_Pid));
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] turn end failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_finishEngageAttack")]
    internal static class ThiefSneakAttackPatch
    {
        private static void Prefix(AttackAttempt _aa, bool _consumable, ref float _dmgMod)
        {
            try { ThiefRuntime.PrepareAttack(_aa, _consumable, ref _dmgMod); }
            catch (Exception e) { Plugin.Log.LogError("[thief] precision attack failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(DamageCalculator), "_playAttackSequence")]
    internal static class ThiefOwnerAttackPatch
    {
        private static void Prefix(ref AttackAttempt _atk, DummyDamageInfo _ddi0)
        {
            try
            {
                if (_atk.m_AttackProficiency == ThiefRuntime.SlipAwayId &&
                    ThiefRuntime.SlipAwayId != GridEditor.FTK_proficiencyTable.ID.None)
                {
                    _atk.m_AttackAnim = CharacterDummy.AttackAnim.DirectAttack;
                    _atk.m_AttackAnimOverride = CharacterEventListener.CombatAnimTrigger.None;
                }
                ThiefRuntime.RecordAttack(_atk.m_AttackingDummy, _ddi0, _atk.m_SlotSuccessPercent);
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] attack outcome failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "PlayAttackSequence")]
    internal static class ThiefReplicatedAttackPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(CharacterDummy __instance, ref DummyDamageInfo _ddi,
            ref DummyDamageInfo _ddi1, ref DummyDamageInfo _ddi2)
        {
            try
            {
                if (__instance is EnemyDummy)
                {
                    string attackId = ThiefRuntime.NextAttackReceipt(__instance);
                    _ddi = ThiefRuntime.ResolveIncoming(__instance, _ddi, attackId);
                    _ddi1 = ThiefRuntime.ResolveIncoming(__instance, _ddi1, attackId);
                    _ddi2 = ThiefRuntime.ResolveIncoming(__instance, _ddi2, attackId);
                    return;
                }
                ThiefRuntime.RecordAttack(__instance, _ddi, float.NaN);
                ThiefRuntime.RecordAttack(__instance, _ddi1, float.NaN);
                ThiefRuntime.RecordAttack(__instance, _ddi2, float.NaN);
            }
            catch (Exception e) { Plugin.Log.LogError("[thief] replicated attack outcome failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "ResetForCombat")]
    internal static class ThiefActorResetPatch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            ThiefRuntime.Expire(__instance);
            ThiefRuntime.ResetEnemy(__instance);
        }
    }

    [HarmonyPatch(typeof(DummyAttackProperties), MethodType.Constructor, new Type[] { typeof(AttackAttempt) })]
    internal static class ThiefEvasionPatch
    {
        private static void Postfix(DummyAttackProperties __instance, AttackAttempt _atkAttempt)
        {
            try { ThiefRuntime.ApplyEvasion(_atkAttempt, __instance); }
            catch (Exception e) { Plugin.Log.LogError("[thief] Evasion calculation failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "RespondToHit")]
    internal static class ThiefImpactPatch
    {
        private static void Prefix(CharacterDummy __instance, out int __state)
        {
            __state = __instance == null ? 0 : __instance.GetCurrentHealth();
        }

        private static void Postfix(CharacterDummy __instance, int __state)
        {
            try { ThiefRuntime.OnImpact(__instance, __state); }
            catch (Exception e) { Plugin.Log.LogError("[thief] artifact impact failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(CharacterDummy), "CombatFinished")]
    internal static class ThiefCombatEndPatch
    {
        private static void Prefix(CharacterDummy __instance) { ThiefRuntime.Expire(__instance); }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "CheckUpdateAvatarAndPortrait")]
    internal static class ThiefEquipmentPatch
    {
        private static void Postfix(CharacterOverworld __instance)
        {
            try { ThiefRuntime.ObserveWeapon(__instance.GetCombatDummy()); }
            catch (Exception e) { Plugin.Log.LogError("[thief] weapon change failed: " + e); }
        }
    }
}
