namespace FTKModFramework.Core
{
    /// <summary>The one place framework tweaks are registered. Plugin.Awake calls RegisterAll once,
    /// before the registry initializes and before PatchAll, and each tweak patch reads its handle
    /// here. Add a new tweak by adding its descriptor, its handle and one line in RegisterAll.</summary>
    internal static class FrameworkTweaks
    {
        internal static readonly TweakDescriptor SkipIntroDescriptor = new TweakDescriptor(
            "convenience.skip-intro", TweakCategory.Convenience, TweakScope.Local,
            "Skip intro",
            "Skip the intro videos when the game starts, as if you pressed a key during each one.",
            "SplashScreen.DisplayScene plays two intro videos, each until it stops or the private "
            + "SplashScreen.GetAnyButton returns true, then loads FTK_main. The patch makes "
            + "GetAnyButton report a press, which is the exit vanilla already takes on a key press.");

        /// <summary>Self-test only. A Session tweak that no patch consults, so it cannot change
        /// gameplay; the lifecycle traces its captured value at each capture, lock and clear.</summary>
        internal static readonly TweakDescriptor SessionProbeDescriptor = new TweakDescriptor(
            "probe.session-lifecycle", TweakCategory.Convenience, TweakScope.Session,
            "Session lifecycle probe",
            "Self-test only. Changes nothing in the game; logs when a run's shared rules are captured, locked and cleared.",
            "Capture: GameLogic.CreateOnlineRoom, GameLogic.CreateOfflineRoom and "
            + "StartGameFE.GameConfig.CreateOnlineRoom postfixes. Lock: uiStartGame.EnterFahrulRPC postfix. "
            + "Clear: uiStartGame.InitializeSingleton, OnLeftRoom, OnDisconnectedFromPhoton, "
            + "OnPhotonJoinRoomFailed postfixes and a GameLogic.RestartFadeOutFinish prefix.");

        internal static int SkipIntro { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int SessionProbe { get; private set; } = TweakRegistry.InvalidHandle;

        /// <param name="selfTests">Diagnostics/RunSelfTests. The Session probe exists only then.</param>
        internal static void RegisterAll(TweakRegistry registry, bool selfTests = false)
        {
            SkipIntro = registry.Register(SkipIntroDescriptor);
            SessionProbe = selfTests ? registry.Register(SessionProbeDescriptor) : TweakRegistry.InvalidHandle;
        }

        /// <summary>The Skip intro decision for one GetAnyButton call. Off, faulted or uninitialized
        /// returns the vanilla result unchanged; on reports a press.</summary>
        internal static bool SkipIntroAnyButton(TweakRegistry registry, int handle, bool vanillaPressed)
        {
            if (!registry.IsOn(handle)) return vanillaPressed;
            return true;
        }
    }
}
