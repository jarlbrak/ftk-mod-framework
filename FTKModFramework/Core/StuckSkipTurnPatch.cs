using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Unstick the skip-turn popup (fix.stuck-skip-turn-popup, spec #265 FR-4). The episode and guard
    /// decisions live in the Unity-free <c>Core/Tweaks/SkipTurnRecovery.cs</c>.
    ///
    /// The hooks only record what happened. Recovery runs from <see cref="StuckSkipTurnDriver"/> on a
    /// later frame, outside any vanilla call, because SkipTurnUI.Close continues a PlayMaker FSM
    /// synchronously and that FSM may itself show or hide the movement slots. Recovery calls
    /// SkipTurnUI.Close with vanilla's own argument, exactly once per episode, and sends no RPC. On a
    /// machine that does not own the character, IsLocal is false and Close only hides the popup.
    /// </summary>
    internal static class StuckSkipTurn
    {
        internal static readonly SkipTurnEpisode Episode = new SkipTurnEpisode();
        internal static readonly ContinueOnceGuard Guard = new ContinueOnceGuard();

        internal static long Key(FTKPlayerID id)
        {
            return RefundFocusLedger.Key(id.m_PhotonID, id.m_TurnIndex);
        }

        internal static void Abandon(string why)
        {
            if (Episode.Abandon())
                Plugin.Log.LogInfo("Tweaks: fix.stuck-skip-turn-popup left an open skip-turn popup to vanilla: " + why + ".");
        }

        /// <summary>One frame of the driver. Returns at once when no episode is open.</summary>
        internal static void Poll()
        {
            if (Episode.State == SkipTurnEpisodeState.Idle) return;
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle))
            {
                Episode.Abandon();
                return;
            }
            try
            {
                FTKUI ui = FTKUI.Instance;
                uiMovementSlots slots = ui != null ? ui.m_MovementSlots : null;
                if (slots == null || slots.m_Root == null)
                {
                    Abandon("the movement slots are gone");
                    return;
                }
                SkipTurnTrigger trigger = Episode.Poll(slots.m_Root.gameObject.activeSelf,
                    slots.gameObject.activeInHierarchy, Time.time);
                if (trigger == SkipTurnTrigger.None) return;

                GameLogic logic = GameLogic.Instance;
                CharacterOverworld current = logic != null ? logic.GetCurrentCOW() : null;
                long currentKey = current != null ? Key(current.m_FTKPlayerID) : long.MinValue;
                SkipTurnUI popup = SkipTurnUI.Instance;
                if (popup == null || !FrameworkTweaks.SkipTurnRecoveryCloses(Tweaks.Registry, handle, trigger,
                        Episode.Player, currentKey))
                {
                    Plugin.Log.LogInfo("Tweaks: fix.stuck-skip-turn-popup did not close the skip-turn popup after "
                        + trigger + ": the turn has moved on.");
                    return;
                }
                bool isLocal = current.m_FTKPlayerID.IsLocal();
                Plugin.Log.LogInfo("Tweaks: fix.stuck-skip-turn-popup closed the skip-turn popup after " + trigger
                    + " (continue=" + isLocal + ").");
                popup.Close(isLocal);
            }
            catch (Exception e)
            {
                Episode.Abandon();
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>Polls an open episode once per frame. Added once by Plugin.Awake; idle frames cost
    /// one field comparison.</summary>
    internal sealed class StuckSkipTurnDriver : MonoBehaviour
    {
        private void Update()
        {
            if (StuckSkipTurn.Episode.State != SkipTurnEpisodeState.Idle) StuckSkipTurn.Poll();
        }
    }

    /// <summary>Arms the episode after vanilla has spent the skip and started the popup coroutine.
    /// The prefix drops an episode that a new popup replaces; SkipTurnUI.Show has already stored the
    /// new continuation by then, so recovering the old one would continue the new one early.</summary>
    [HarmonyPatch(typeof(uiMovementSlots), "InitializeSkipTurn")]
    internal static class StuckSkipTurnArmPatch
    {
        private static void Prefix()
        {
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                StuckSkipTurn.Abandon("a new skip-turn popup replaced it");
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        private static void Postfix(CharacterOverworld _cow)
        {
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle) || _cow == null) return;
            try
            {
                StuckSkipTurn.Episode.Arm(StuckSkipTurn.Key(_cow.m_FTKPlayerID), Time.time);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>Initialize starts an action roll in the same panel. A roll begins only for a turn that
    /// has moved past the skip, and Close would fade the new roll out, so the episode ends without a
    /// close. Initialize's own ForceHide then finds no open episode.</summary>
    [HarmonyPatch(typeof(uiMovementSlots), "Initialize")]
    internal static class StuckSkipTurnRollPatch
    {
        private static void Prefix()
        {
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                StuckSkipTurn.Abandon("an action roll replaced it");
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>ForceHide stops every coroutine on the panel only while m_Root is active, so it
    /// records the trigger before vanilla hides the root.</summary>
    [HarmonyPatch(typeof(uiMovementSlots), "ForceHide")]
    internal static class StuckSkipTurnForceHidePatch
    {
        private static void Prefix(uiMovementSlots __instance)
        {
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle) || __instance == null) return;
            try
            {
                StuckSkipTurn.Episode.ForceHide(__instance.m_Root != null && __instance.m_Root.gameObject.activeSelf);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>Every close ends the episode. The guard lets each continuation through once; a
    /// repeat runs vanilla's Close with _continue false, which still hides the popup.</summary>
    [HarmonyPatch(typeof(SkipTurnUI), "Close")]
    internal static class StuckSkipTurnClosePatch
    {
        private static void Prefix(SkipTurnUI __instance, ref bool _continue)
        {
            int handle = FrameworkTweaks.StuckSkipTurnPopup;
            if (!Tweaks.IsOn(handle) || __instance == null) return;
            bool vanilla = _continue;
            try
            {
                StuckSkipTurn.Episode.Closed();
                bool allowed = FrameworkTweaks.SkipTurnCloseContinues(Tweaks.Registry, handle, StuckSkipTurn.Guard,
                    vanilla, __instance.m_ContinueFSM);
                if (vanilla && !allowed)
                {
                    _continue = false;
                    Plugin.Log.LogInfo("Tweaks: fix.stuck-skip-turn-popup kept a skip-turn continuation from running twice.");
                }
            }
            catch (Exception e)
            {
                _continue = vanilla;
                Tweaks.Fault(handle, e);
            }
        }
    }
}
