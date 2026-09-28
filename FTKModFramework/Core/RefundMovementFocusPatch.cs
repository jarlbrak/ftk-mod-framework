using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Refund movement focus (convenience.refund-movement-focus, Spec #264). Holds the
    /// per-character ledger and the one refund entry point the HUD calls. Everything here runs on the
    /// owning client only: the conversion is counted where vanilla's owner converted, and a refund
    /// mutates through the same vanilla setters, whose SyncMember(s) carry absolute values to the other
    /// machines. The ledger is never saved, so a resumed run has nothing refundable.</summary>
    internal static class RefundMovementFocus
    {
        private const string TrackingState = "Tracking";
        private const string SneakPickState = "PickSneakHex";

        internal static readonly RefundFocusLedger Ledger = new RefundFocusLedger(Tweaks.Registry);

        private static readonly GameSetters Setters = new GameSetters();

        internal static long Key(CharacterOverworld cow)
        {
            FTKPlayerID id = cow.m_FTKPlayerID;
            return RefundFocusLedger.Key(id.m_PhotonID, id.m_TurnIndex);
        }

        /// <summary>Reads every gate for one character. Movement and EncounterSession are singletons
        /// that exist for the whole run; a missing one leaves its flags false, which blocks.</summary>
        internal static RefundFocusState ReadState(CharacterOverworld cow)
        {
            var state = new RefundFocusState();
            CharacterStats stats = cow.m_CharacterStats;
            if (stats == null) return state;
            state.Count = Ledger.Count(Key(cow));
            state.FocusPoints = stats.m_FocusPoints;
            state.MaxFocus = stats.MaxFocus;
            state.ActionPoints = stats.m_ActionPoints;
            state.Owner = cow.IsOwner;
            state.MyTurn = stats.m_IsMyTurn;
            EncounterSession encounter = EncounterSession.Instance;
            state.InCombat = stats.m_IsInCombat || (encounter != null && encounter.m_IsInCombat);
            Movement movement = Movement.Instance;
            if (movement == null) return state;
            state.MovementCharacter = movement.m_CharacterOverworld == cow;
            state.TrackerActive = movement.m_Mode == Movement.TrackingMode.Movement;
            PlayMakerFSM fsm = movement.m_MovementFSM;
            string fsmState = fsm != null ? fsm.ActiveStateName : null;
            state.FsmTracking = string.Equals(fsmState, TrackingState);
            state.CommittedHexes = movement.m_HexList != null ? movement.m_HexList.Count : int.MaxValue;
            state.SneakPick = movement.m_LockedInput || string.Equals(fsmState, SneakPickState);
            return state;
        }

        /// <summary>Refunds one point for this character if every gate passes. Returns true when the
        /// refund was applied. Never throws: a failure faults the tweak and leaves vanilla running.</summary>
        internal static bool TryRefund(CharacterOverworld cow, string via)
        {
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle) || cow == null) return false;
            try
            {
                RefundFocusState state = ReadState(cow);
                Setters.Cow = cow;
                RefundFocusBlock block = FrameworkTweaks.RefundFocusApply(Tweaks.Registry, handle, Ledger, Key(cow), state, Setters);
                if (block != RefundFocusBlock.None) return false;
                Plugin.Log.LogInfo("Tweaks: convenience.refund-movement-focus refunded 1 focus via " + via + ".");
                Tweaks.Session.Trace("focus-refund", via);
                return true;
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return false;
            }
            finally
            {
                Setters.Cow = null;
            }
        }

        /// <summary>A clear trigger from a patch. Checks IsOn first and never throws.</summary>
        internal static void Clear(RefundFocusClear trigger, CharacterOverworld cow, bool inCombat = true)
        {
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                long key = cow != null ? Key(cow) : 0L;
                FrameworkTweaks.RefundFocusClear(Tweaks.Registry, handle, Ledger, trigger, key, inCombat);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        /// <summary>The vanilla setters a refund calls, over the character being refunded. One instance
        /// is reused, so a refund allocates nothing.</summary>
        private sealed class GameSetters : IRefundFocusSetters
        {
            internal CharacterOverworld Cow;

            public void UpdateFocusPoints(int delta)
            {
                Cow.m_CharacterStats.UpdateFocusPoints(delta);
            }

            public void UpdatePlayerAction(int delta)
            {
                Cow.UpdatePlayerAction(delta);
            }

            public void TrackResetList()
            {
                Movement.Instance.TrackResetList();
            }

            public void RefreshHud()
            {
                if (Cow.m_UIPlayMainHud != null) Cow.m_UIPlayMainHud.UpdateHud();
            }
        }
    }

    /// <summary>Counts conversions. The private ConvertFocusToAction has no return value, so the
    /// prefix captures vanilla's guard (m_FocusPoints > 0, m_ActionPoints < 9) and ownership, and the
    /// postfix counts only when both points actually moved. Both return void and catch everything.</summary>
    [HarmonyPatch(typeof(Movement), "ConvertFocusToAction")]
    internal static class RefundFocusConvertPatch
    {
        private static void Prefix(Movement __instance, out RefundFocusConversion __state)
        {
            __state = new RefundFocusConversion();
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                CharacterOverworld cow = __instance.m_CharacterOverworld;
                if (cow == null || cow.m_CharacterStats == null) return;
                __state = RefundFocus.Before(cow.IsOwner, cow.m_CharacterStats.m_FocusPoints, cow.m_CharacterStats.m_ActionPoints);
            }
            catch (Exception e)
            {
                __state = new RefundFocusConversion();
                Tweaks.Fault(handle, e);
            }
        }

        private static void Postfix(Movement __instance, RefundFocusConversion __state)
        {
            if (!__state.Captured) return;
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                CharacterOverworld cow = __instance.m_CharacterOverworld;
                if (cow == null || cow.m_CharacterStats == null) return;
                if (!FrameworkTweaks.RefundFocusCounts(Tweaks.Registry, handle, __state,
                        cow.m_CharacterStats.m_FocusPoints, cow.m_CharacterStats.m_ActionPoints))
                    return;
                RefundMovementFocus.Ledger.Add(RefundMovementFocus.Key(cow));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>CharacterOverworld.EndTurn is a PunRPC run on every machine; only the owner holds a
    /// count, so clearing everywhere is harmless.</summary>
    [HarmonyPatch(typeof(CharacterOverworld), "EndTurn")]
    internal static class RefundFocusEndTurnPatch
    {
        private static void Postfix(CharacterOverworld __instance)
        {
            RefundMovementFocus.Clear(RefundFocusClear.EndTurn, __instance);
        }
    }

    [HarmonyPatch(typeof(EncounterSession), "StartEncounterSession_Actual")]
    internal static class RefundFocusEncounterPatch
    {
        private static void Postfix()
        {
            RefundMovementFocus.Clear(RefundFocusClear.EncounterStart, null);
        }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "SetInCombat")]
    internal static class RefundFocusCombatPatch
    {
        private static void Postfix(CharacterOverworld __instance, bool _toggle)
        {
            if (!_toggle) return;
            RefundMovementFocus.Clear(RefundFocusClear.EnterCombat, __instance, true);
        }
    }

    [HarmonyPatch(typeof(CharacterOverworld), "SetDeath")]
    internal static class RefundFocusDeathPatch
    {
        private static void Postfix(CharacterOverworld __instance)
        {
            RefundMovementFocus.Clear(RefundFocusClear.Death, __instance);
        }
    }
}
