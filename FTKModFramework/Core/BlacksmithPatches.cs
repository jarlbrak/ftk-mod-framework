using System;
using System.Collections;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    internal static class BlacksmithActionUi
    {
        internal static FTK_proficiencyTable.ID Action(uiBattleStanceButtons owner, uiBattleButton button)
        {
            if (owner != null && button != null)
                foreach (uiBattleStanceButtons.ProfValues entry in owner.m_Proficiencies)
                    if (entry.m_Button == button && BlacksmithRuntime.IsAction(entry.m_Prof)) return entry.m_Prof;
            return FTK_proficiencyTable.ID.None;
        }
        internal static void Add(uiBattleStanceButtons owner, CharacterDummy actor, FTK_proficiencyTable.ID id)
        {
            if (!BlacksmithRuntime.IsAction(id)) return;
            uiBattleStanceButtons.ProfValues entry = new uiBattleStanceButtons.ProfValues();
            entry.m_Prof = id;
            entry.m_Button = UnityEngine.Object.Instantiate(owner.m_ProficiencyButtonMaster);
            entry.m_Button.transform.SetParent(owner.m_ProficiencyButtonMaster.transform.parent, false);
            entry.m_Button.m_Owner = owner; entry.m_Button.m_ButtonType = uiBattleButton.BattleButtonType.proficiency;
            entry.m_Button.gameObject.GetComponent<Image>().sprite = FTK_proficiencyTableDB.Get(id).m_BattleButton;
            owner.m_Proficiencies.Add(entry);
            entry.m_Button.gameObject.SetActive(true);
            entry.m_Button.SetCanUse(BlacksmithRuntime.Available(actor, id));
        }
        internal static void Log(Exception e) { Plugin.Log.LogWarning("[blacksmith] " + e); }
    }
    [HarmonyPatch(typeof(uiBattleStanceButtons), "CreateWeaponProficiencyButtons")]
    internal static class BlacksmithButtonsPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance)
        {
            try
            {
                CharacterDummy actor = __instance.CombatCow.GetCombatDummy();
                if (!BlacksmithRuntime.IsBlacksmith(actor)) return;
                BlacksmithEquipmentBonuses b = BlacksmithRuntime.WeaponBonus(actor);
                if (b != null && b.SetHammerArmor > 0 && BlacksmithRuntime.HasShield(actor)) BlacksmithActionUi.Add(__instance, actor, BlacksmithRuntime.ActionFor(actor, BlacksmithActionKind.SetHammer));
                if (b != null && b.OverhandArmorPenalty > 0) BlacksmithActionUi.Add(__instance, actor, BlacksmithRuntime.ActionFor(actor, BlacksmithActionKind.Overhand));
                if (BlacksmithRuntime.TemperAmount(actor) > 0) BlacksmithActionUi.Add(__instance, actor, BlacksmithRuntime.ActionFor(actor, BlacksmithActionKind.Temper));
            }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
        }
    }
    [HarmonyPatch(typeof(uiBattleStanceButtons), "DisplayBattleActionInfo")]
    internal static class BlacksmithActionInfoPatch
    {
        private static void Postfix(uiBattleStanceButtons __instance, uiBattleButton _button, bool _on)
        {
            try
            {
                if (!_on) return;
                FTK_proficiencyTable.ID action = BlacksmithActionUi.Action(__instance, _button);
                if (!BlacksmithRuntime.IsAction(action)) return;
                CharacterDummy actor = __instance.CombatCow.GetCombatDummy();
                bool temper = BlacksmithRuntime.Kind(action) == BlacksmithActionKind.Temper;
                Text[] descriptions = __instance.m_InfoPanel.m_Description;
                BlacksmithEquipmentBonuses b = BlacksmithRuntime.WeaponBonus(actor);
                BlacksmithActionKind kind = BlacksmithRuntime.Kind(action);
                int armor = temper ? BlacksmithRuntime.TemperAmount(actor) : b == null ? 0 :
                    kind == BlacksmithActionKind.SetHammer ? b.SetHammerArmor : b.OverhandArmorPenalty;
                string text = BlacksmithRuntime.CombatDescription(kind, armor,
                    !temper || BlacksmithRuntime.State.TemperAvailable(BlacksmithRuntime.Identity(actor)));
                if (descriptions != null && descriptions.Length > 1 && descriptions[1] != null) descriptions[1].text = text;
                if (temper)
                {
                    __instance.m_CombatActionProfile.m_Slots = 0; __instance.m_CombatActionProfile.m_NoFocus = true;
                    __instance.m_InfoPanel.m_ACCRoot.SetActive(false); __instance.m_InfoPanel.m_DMGRoot.SetActive(false);
                    if (descriptions != null && descriptions.Length > 0 && descriptions[0] != null) descriptions[0].text = "Target: living ally or self";
                    uiToolTipFocusable.gCanFocus = false; FTKUI.Instance.m_PlayerSlots.ResetSlots();
                }
            }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
        }
    }
    [HarmonyPatch(typeof(uiBattleStanceButtons), "FocusSlot")]
    internal static class BlacksmithFocusPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            FTK_proficiencyTable.ID action = BlacksmithActionUi.Action(__instance, _button);
            return !BlacksmithRuntime.IsAction(action) || BlacksmithRuntime.Kind(action) != BlacksmithActionKind.Temper;
        }
    }
    [HarmonyPatch(typeof(uiBattleStanceButtons), "AttackProficiency")]
    internal static class BlacksmithClickPatch
    {
        private static bool Prefix(uiBattleStanceButtons __instance, uiBattleButton _button)
        {
            FTK_proficiencyTable.ID action = BlacksmithActionUi.Action(__instance, _button);
            if (!BlacksmithRuntime.IsAction(action)) return true;
            try
            {
                CharacterDummy actor = __instance.CombatCow.GetCombatDummy();
                if (__instance.m_Focusing || !BlacksmithRuntime.Available(actor, action)) return false;
                if (BlacksmithRuntime.Kind(action) != BlacksmithActionKind.Temper) return true;
                __instance.CombatCow.m_CharacterStats.ResetSpentFocus(true);
                __instance.BattleButtonsOff(true);
                // Keyboard/bridge activation need not hover the action first. Clear the previous
                // weapon preview locally before opening the cancellable, roll-free picker.
                FTKUI.Instance.m_PlayerSlots.ResetSlots();
                // Native EngageDirectAttack marks the attempt as a consumable, which requires
                // a ProficiencyBase. This behavior-free action uses the same slot bypass and
                // friendly target flow with an ordinary harmless attempt instead.
                DamageCalculator.StartEngageAttack(actor, EncounterSession.Instance.GetCurrentEnemy(),
                    1f, 0, action, false, SlotControl.AttackCheatType.None);
            }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
            return false;
        }
    }
    [HarmonyPatch(typeof(DamageCalculator), "_waitForUserPickTarget")]
    internal static class BlacksmithTargetsPatch
    {
        private static void Prefix(List<CharacterDummy> _party, AttackAttempt _av)
        {
            if (!BlacksmithRuntime.IsAction(_av.m_AttackProficiency) || BlacksmithRuntime.Kind(_av.m_AttackProficiency) != BlacksmithActionKind.Temper) return;
            for (int i = _party.Count - 1; i >= 0; i--)
                if (!GuardianRuntime.LivingAlly(_party[i])) _party.RemoveAt(i);
        }
        private static void Postfix(AttackAttempt _av, ref IEnumerator __result)
        {
            if (BlacksmithRuntime.Kind(_av.m_AttackProficiency) == BlacksmithActionKind.Temper)
                __result = BlacksmithTargeting.Wrap(__result, _av.m_AttackingDummy);
        }
    }
    [HarmonyPatch(typeof(uiChooseRewardMenu), "ClickDummySelectCombat")]
    internal static class BlacksmithCancelTargetPatch
    {
        private static bool Prefix(uiChooseRewardMenu __instance, uiChooseRewardButton _button)
        {
            try { return !BlacksmithTargeting.Click(__instance, _button); }
            catch (Exception e) { BlacksmithActionUi.Log(e); throw; }
        }
    }
    [HarmonyPatch(typeof(DamageCalculator), "_finishEngageAttack")]
    internal static class BlacksmithValidateCommitPatch
    {
        private static bool Prefix(AttackAttempt _aa)
        {
            if (!BlacksmithRuntime.IsAction(_aa.m_AttackProficiency)) return true;
            try
            {
                if (BlacksmithRuntime.Available(_aa.m_AttackingDummy, _aa.m_AttackProficiency) &&
                    (BlacksmithRuntime.Kind(_aa.m_AttackProficiency) != BlacksmithActionKind.Temper || GuardianRuntime.LivingAlly(_aa.m_DamagedDummy)))
                {
                    // Do not broadcast slot bypass while a cancellable picker is still open.
                    if (BlacksmithRuntime.Kind(_aa.m_AttackProficiency) == BlacksmithActionKind.Temper)
                        FTKUI.Instance.m_PlayerSlots.BypassCombatSlots(_aa.m_AttackingDummy, true, false, true);
                    return true;
                }
                // A stale target/equipment selection has not committed. Restore the native menu
                // and return only reserved Focus, as the ordinary pre-action cancellation does.
                if (GuardianRuntime.CanAct(_aa.m_AttackingDummy) && _aa.m_AttackingDummy.m_CharacterOverworld.IsOwner)
                {
                    _aa.m_AttackingDummy.m_CharacterOverworld.m_CharacterStats.ResetSpentFocus(true);
                    FTKUI.Instance.m_BattleStanceButtons.Initialize(true);
                }
            }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
            return false;
        }
    }
    [HarmonyPatch(typeof(DamageCalculator), "_playAttackSequence")]
    internal static class BlacksmithSequencePatch
    {
        private static void Prefix(ref AttackAttempt _atk, DummyDamageInfo _ddi0)
        {
            BlacksmithRuntime.StampOutcome(_atk, _ddi0);
            if (BlacksmithRuntime.IsAction(_atk.m_AttackProficiency) && BlacksmithRuntime.Kind(_atk.m_AttackProficiency) == BlacksmithActionKind.Temper)
            {
                _atk.m_AttackAnim = CharacterDummy.AttackAnim.DirectAttack;
                _atk.m_AttackAnimOverride = CharacterEventListener.CombatAnimTrigger.None;
            }
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "PlayAttackSequence")]
    internal static class BlacksmithCommitPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(CharacterDummy __instance, DummyDamageInfo _ddi)
        {
            try { BlacksmithRuntime.Commit(__instance, _ddi); }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "get_ArmorMod")]
    internal static class BlacksmithArmorPatch
    {
        private static void Postfix(CharacterDummy __instance, ref int __result)
        {
            try { __result += BlacksmithRuntime.Armor(__instance); }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
        }
    }
    [HarmonyPatch(typeof(EncounterSession), "InitAttackTimeline")]
    internal static class BlacksmithEncounterPatch
    {
        private static void Prefix() { BlacksmithRuntime.BeginEncounter(); }
    }
    [HarmonyPatch(typeof(CharacterDummy), "EngageBattle")]
    internal static class BlacksmithBeginTurnPatch
    {
        private static void Prefix(CharacterDummy __instance) { BlacksmithRuntime.BeginTurn(__instance); }
    }
    [HarmonyPatch(typeof(EncounterSession), "RemoveAttacker")]
    internal static class BlacksmithEndTurnPatch
    {
        private static void Prefix(List<EncounterSessionMC.FightOrderEntry> fightOrder)
        {
            if (fightOrder != null && fightOrder.Count > 0) BlacksmithRuntime.CompleteEntry(fightOrder[0], false);
        }
    }
    [HarmonyPatch(typeof(EncounterSession), "ApplyProficiencyEffect")]
    internal static class BlacksmithSkippedTurnPatch
    {
        private static void Prefix(List<EncounterSessionMC.FightOrderEntry> _fightOrder, out List<EncounterSessionMC.FightOrderEntry> __state)
        {
            __state = new List<EncounterSessionMC.FightOrderEntry>(_fightOrder);
        }
        private static void Postfix(List<EncounterSessionMC.FightOrderEntry> _fightOrder, List<EncounterSessionMC.FightOrderEntry> __state)
        {
            // This method removes scheduled entries for Interrupt, while Stun only delays them.
            foreach (EncounterSessionMC.FightOrderEntry previous in __state)
            {
                bool exists = false;
                foreach (EncounterSessionMC.FightOrderEntry current in _fightOrder)
                    if (previous.m_EntryID == current.m_EntryID && BlacksmithRuntime.SameActor(previous.m_Pid, current.m_Pid)) { exists = true; break; }
                if (!exists) BlacksmithRuntime.CompleteEntry(previous, true);
            }
        }
    }
    [HarmonyPatch(typeof(CharacterOverworld), "CheckUpdateAvatarAndPortrait")]
    internal static class BlacksmithEquipmentPatch
    {
        private static void Postfix(CharacterOverworld __instance)
        {
            try { BlacksmithRuntime.ObserveEquipment(__instance.GetCombatDummy()); }
            catch (Exception e) { BlacksmithActionUi.Log(e); }
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "RemoveSpecificProficiency")]
    internal static class BlacksmithDispelSpecificPatch
    {
        private static void Postfix(CharacterDummy __instance, ProficiencyBase.Category _c)
        {
            if (_c == ProficiencyBase.Category.Armor) BlacksmithRuntime.ClearPositive(__instance);
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "RemoveProficiencyBuffs")]
    internal static class BlacksmithDispelBuffPatch
    {
        private static void Postfix(CharacterDummy __instance, ProficiencyBase.Category[] _c)
        {
            if (_c != null && Array.IndexOf(_c, ProficiencyBase.Category.Armor) >= 0) BlacksmithRuntime.ClearPositive(__instance);
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "RemoveAllProficiencies")]
    internal static class BlacksmithDispelAllPatch
    {
        private static void Postfix(CharacterDummy __instance) { BlacksmithRuntime.ClearPositive(__instance); }
    }
    [HarmonyPatch(typeof(CharacterDummy), "ResetForCombat")]
    internal static class BlacksmithRevivePatch
    {
        private static void Postfix(CharacterDummy __instance) { BlacksmithRuntime.ClearPositive(__instance); }
    }
    [HarmonyPatch(typeof(CharacterDummy), "RespondToHit")]
    internal static class BlacksmithDeathPatch
    {
        private static void Postfix(CharacterDummy __instance)
        {
            if (!GuardianRuntime.LivingAlly(__instance)) BlacksmithRuntime.ClearPositive(__instance);
        }
    }
    [HarmonyPatch(typeof(CharacterDummy), "CombatFinished")]
    internal static class BlacksmithEndCombatPatch
    {
        private static void Prefix(CharacterDummy __instance) { BlacksmithTargeting.EndActor(__instance); }
        private static void Postfix(CharacterDummy __instance) { BlacksmithRuntime.EndCombat(__instance); }
    }
    [HarmonyPatch(typeof(uiItemDetail), "Show")]
    internal static class BlacksmithItemUiPatch
    {
        private static void Postfix(uiItemDetail __instance, FTK_itembase.ID _itemID)
        {
            string text = BlacksmithRuntime.Description((int)_itemID);
            if (text.Length > 0 && __instance.m_EquippableProperties != null && __instance.m_ArmorPanel != null && __instance.m_ArmorPanel.gameObject.activeSelf)
                __instance.m_EquippableProperties.text = GuardianEquipmentDescription.Append(__instance.m_EquippableProperties.text, text);
        }
    }
    [HarmonyPatch(typeof(uiWeaponDetail), "ShowWeapon")]
    internal static class BlacksmithWeaponUiPatch
    {
        private static void Postfix(uiWeaponDetail __instance, FTK_itembase _itemInfo)
        {
            int id;
            if (_itemInfo != null && __instance.m_WeaponStatDisplay != null && ContentRegistry.TryGetSyntheticId(_itemInfo.m_ID, out id, typeof(FTK_weaponStats2DB)))
                __instance.m_WeaponStatDisplay.text = GuardianEquipmentDescription.Append(__instance.m_WeaponStatDisplay.text, BlacksmithRuntime.Description(id));
        }
    }
    // Attached only to live HUD icon instances. Native localization remains the tooltip authority.
    internal sealed class BlacksmithStatusLabel : MonoBehaviour
    {
        internal CharacterDummy Actor;
    }
    [HarmonyPatch(typeof(uiPlayerMainHudStatus), "SetStatusIcons")]
    internal static class BlacksmithStatusIconsPatch
    {
        private static void Postfix(uiPlayerMainHudStatus __instance, CharacterOverworld _cow)
        {
            if (_cow == null || !_cow.m_CharacterStats.m_IsInCombat || _cow.m_CurrentDummy == null) return;
            CharacterDummy actor = _cow.m_CurrentDummy;
            string status = BlacksmithRuntime.Status(actor);
            if (status.Length == 0) return;
            Bind(__instance.m_ArmorUp, actor, BlacksmithRuntime.State.HasPositive(BlacksmithRuntime.Identity(actor)));
            Bind(__instance.m_ArmorDown, actor, BlacksmithRuntime.State.HasPenalty(BlacksmithRuntime.Identity(actor)));
        }
        private static void Bind(Image icon, CharacterDummy actor, bool active)
        {
            if (icon == null) return;
            if (active) icon.gameObject.SetActive(true);
            BlacksmithStatusLabel label = icon.GetComponent<BlacksmithStatusLabel>();
            if (label == null) label = icon.gameObject.AddComponent<BlacksmithStatusLabel>();
            label.Actor = actor;
        }
    }
    [HarmonyPatch(typeof(uiToolTipGeneral), "GetToolTip")]
    internal static class BlacksmithStatusTooltipPatch
    {
        private static void Postfix(uiToolTipGeneral __instance, ref string __result)
        {
            BlacksmithStatusLabel label = __instance.GetComponent<BlacksmithStatusLabel>();
            if (label == null || label.Actor == null) return;
            string status = BlacksmithRuntime.Status(label.Actor);
            if (status.Length > 0) __result = GuardianEquipmentDescription.Append(__result, status);
        }
    }

}
