using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Session tweak lifecycle hooks (Spec #233 FR-3). Every hook catches its own exceptions and
    // never skips vanilla: the postfixes cannot, and the one prefix returns void.
    //
    // HarmonyX (2.7 referenced, 2.9 shipped with BepInEx 5.4.23) still runs postfixes after a prefix
    // returns false, so the capture and lock postfixes read __runOriginal and ignore a vetoed call,
    // such as HotReloadSessionEntry blocking GameLogic.CreateOnlineRoom. The clear postfixes ignore
    // it on purpose: the room was left or the scene reloaded whether or not a handler ran.
    internal static class TweakSessionHooks
    {
        /// <summary>GameLogic.GameMode as an int, or -1 when GameLogic is missing, which the
        /// lifecycle treats as Multiplayer so Session tweaks stay off.</summary>
        internal static int CurrentGameMode()
        {
            GameLogic logic = GameLogic.Instance;
            return logic != null ? (int)logic.m_GameMode : -1;
        }

        internal static void Capture(bool runOriginal, string via)
        {
            if (!runOriginal) return;
            try { Tweaks.Session.Capture(CurrentGameMode(), via); }
            catch (Exception e) { Failed(via, e); }
        }

        internal static void Clear(TweakClearTrigger trigger, string via)
        {
            try { Tweaks.Session.Clear(trigger, via); }
            catch (Exception e) { Failed(via, e); }
        }

        internal static void Failed(string via, Exception e)
        {
            Action<string> warn = Tweaks.Warn;
            if (warn != null) warn("Tweaks: the Session lifecycle hook " + via + " failed; vanilla is unaffected: " + e);
        }
    }

    // Capture. Every caller sets m_GameMode first: GameConfig.OnStartGame and
    // uiStartGame.OnResumeGame2 before CreateOfflineRoom, and _OnJoinedLobby picks CreateOnlineRoom
    // by reading it (solo and local play in a closed room). Neither method writes m_GameMode.
    [HarmonyPatch(typeof(GameLogic), "CreateOnlineRoom")]
    internal static class TweakCaptureOnlineRoomPatch
    {
        private static void Postfix(bool __runOriginal)
        {
            TweakSessionHooks.Capture(__runOriginal, "GameLogic.CreateOnlineRoom");
        }
    }

    [HarmonyPatch(typeof(GameLogic), "CreateOfflineRoom")]
    internal static class TweakCaptureOfflineRoomPatch
    {
        private static void Postfix(bool __runOriginal)
        {
            TweakSessionHooks.Capture(__runOriginal, "GameLogic.CreateOfflineRoom");
        }
    }

    // An online co-op host creates its room here with PhotonNetwork.CreateRoom and never calls
    // GameLogic.CreateOnlineRoom. OnStartGame or OnResumeGame2 set m_GameMode = Multiplayer first.
    // The parameterless overload forwards to this one.
    [HarmonyPatch(typeof(StartGameFE.GameConfig), "CreateOnlineRoom", new[] { typeof(bool), typeof(string), typeof(bool),
        typeof(string), typeof(GameDifficulty.DifficultyType), typeof(TimeOfDayProperties.TimeOfDay) })]
    internal static class TweakCaptureCoopRoomPatch
    {
        private static void Postfix(bool __runOriginal)
        {
            TweakSessionHooks.Capture(__runOriginal, "StartGameFE.GameConfig.CreateOnlineRoom");
        }
    }

    // Lock. EnterFahrulRPC is an RPCAllSelf, so every player runs it once per run; it sets
    // uiStartGame.m_GameStarted. A co-op client joins through Lobby.JoinGameActual or a direct join
    // (both set m_GameMode = Multiplayer, then PhotonNetwork.JoinRoom) and passes no capture hook,
    // so the lifecycle recaptures here from the run's own mode instead of trusting stale state.
    [HarmonyPatch(typeof(uiStartGame), "EnterFahrulRPC")]
    internal static class TweakLockPatch
    {
        private static void Postfix(bool __runOriginal)
        {
            if (!__runOriginal) return;
            try { Tweaks.Session.Lock(TweakSessionHooks.CurrentGameMode(), "uiStartGame.EnterFahrulRPC"); }
            catch (Exception e) { TweakSessionHooks.Failed("uiStartGame.EnterFahrulRPC", e); }
        }
    }

    // Clear. OnOffManager.Awake calls InitializeSingleton on every FTK_main load, which is how
    // RestartGame, save-and-quit and the title screen return (all through RestartFadeOutFinish).
    [HarmonyPatch(typeof(uiStartGame), "InitializeSingleton")]
    internal static class TweakClearSceneReloadPatch
    {
        private static void Postfix()
        {
            TweakSessionHooks.Clear(TweakClearTrigger.SceneReload, "uiStartGame.InitializeSingleton");
        }
    }

    // RestartFadeOutFinish ends a run (reached from RestartGame, RestartGameRT and SaveGame with
    // quit) and calls PhotonNetwork.Disconnect, which in offline mode raises OnDisconnectedFromPhoton
    // synchronously. Clearing first, as the run's end, keeps that callback from reading as a
    // mid-run clear. Last priority so __runOriginal reflects every other prefix.
    [HarmonyPatch(typeof(GameLogic), "RestartFadeOutFinish")]
    internal static class TweakClearRunEndPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(bool __runOriginal)
        {
            if (!__runOriginal) return;
            TweakSessionHooks.Clear(TweakClearTrigger.RunEnd, "GameLogic.RestartFadeOutFinish");
        }
    }

    // uiStartGame overrides all three Photon callbacks (GameLogic overrides only the last two), and
    // PUN delivers them to every listening GameObject, so one target type is enough.
    [HarmonyPatch(typeof(uiStartGame), "OnLeftRoom")]
    internal static class TweakClearLeftRoomPatch
    {
        private static void Postfix()
        {
            TweakSessionHooks.Clear(TweakClearTrigger.LeftRoom, "uiStartGame.OnLeftRoom");
        }
    }

    [HarmonyPatch(typeof(uiStartGame), "OnDisconnectedFromPhoton")]
    internal static class TweakClearDisconnectedPatch
    {
        private static void Postfix()
        {
            TweakSessionHooks.Clear(TweakClearTrigger.Disconnected, "uiStartGame.OnDisconnectedFromPhoton");
        }
    }

    [HarmonyPatch(typeof(uiStartGame), "OnPhotonJoinRoomFailed")]
    internal static class TweakClearJoinRoomFailedPatch
    {
        private static void Postfix()
        {
            TweakSessionHooks.Clear(TweakClearTrigger.JoinRoomFailed, "uiStartGame.OnPhotonJoinRoomFailed");
        }
    }
}
