using System;
using System.Collections;
using Google2u;

namespace FTKModFramework.Core
{
    internal static class BlacksmithTargeting
    {
        private sealed class Pending
        {
            internal CharacterDummy Actor;
            internal string Turn;
            internal BlacksmithTargetWait Wait;
            internal uiChooseRewardMenu Menu;
            internal uiChooseRewardButton CancelButton;
            internal bool Selected;
        }
        private static Pending current;
        private static uiChooseRewardButton lastCancelledButton;

        internal static IEnumerator Wrap(IEnumerator native, CharacterDummy actor)
        {
            Pending pending = new Pending();
            pending.Actor = actor; pending.Turn = BlacksmithRuntime.CurrentTurn(actor);
            pending.Wait = new BlacksmithTargetWait(native,
                delegate { InstallCancel(pending); },
                delegate { if (object.ReferenceEquals(current, pending)) current = null; });
            return pending.Wait;
        }
        private static void InstallCancel(Pending pending)
        {
            if (pending.CancelButton != null || pending.Selected) return;
            uiChooseRewardMenu menu = FTKUI.Instance.m_ChooseRewardMenu;
            if (menu == null || menu.m_RewardType != uiChooseRewardMenu.RewardType.PlayerDummySelect ||
                !menu.m_DisplayRoot.gameObject.activeSelf) return;
            // The native iterator just initialized this picker. Reuse its ordinary button and
            // focus-navigation construction, without adding a fake CharacterDummy target.
            pending.Menu = menu; current = pending;
            uiChooseRewardMenu.RewardButtonInfo info = new uiChooseRewardMenu.RewardButtonInfo(
                menu.m_AllButtons.Count, FTKHub.Localized<TextMenu>("STR_buttonCancel"),
                "ClickDummySelectCombat", 0f, uiChooseRewardMenu.RewardType.PlayerDummySelect);
            pending.CancelButton = menu.InstantiateRewardButton(info, menu);
        }
        internal static bool Click(uiChooseRewardMenu menu, uiChooseRewardButton button)
        {
            // The native button can already have queued a second callback when its object is
            // destroyed. Consume that exact canceled button again without touching a new picker.
            if (!object.ReferenceEquals(button, null) && object.ReferenceEquals(lastCancelledButton, button)) return true;
            Pending pending = current;
            if (pending == null || pending.Selected || !object.ReferenceEquals(pending.Menu, menu)) return false;
            if (!object.ReferenceEquals(pending.CancelButton, button))
            {
                if (menu.m_DummySelectButtons.ContainsKey(button)) pending.Selected = true;
                return false;
            }
            Cancel(pending, true);
            return true;
        }
        internal static void EndActor(CharacterDummy actor)
        {
            Pending pending = current;
            if (pending != null && object.ReferenceEquals(pending.Actor, actor)) Cancel(pending, false);
        }
        private static void Cancel(Pending pending, bool restoreStance)
        {
            if (!object.ReferenceEquals(current, pending)) return;
            current = null;
            lastCancelledButton = pending.CancelButton;
            // Stop only the IEnumerator returned for this exact Temper request. Other combat,
            // animation, target selection, and UI coroutines are not stopped.
            if (pending.Actor != null) pending.Actor.StopCoroutine(pending.Wait);
            pending.Wait.Cancel();
            DamageCalculator.CacheUserPickedTarget(null);
            pending.Menu.ClickFinished();
            pending.Menu.Close();
            if (restoreStance && GuardianRuntime.CanAct(pending.Actor) &&
                pending.Actor.m_CharacterOverworld.IsOwner &&
                BlacksmithRuntime.CurrentTurn(pending.Actor) == pending.Turn)
                FTKUI.Instance.EnableBattleStanceButtons();
        }
    }
}
