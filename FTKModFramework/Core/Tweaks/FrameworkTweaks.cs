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

        /// <summary>Spec #242 FR-1. Presentation only: the patch rewrites one cached message param on
        /// this machine and never touches quest state, so it is Local and runs on every machine.</summary>
        internal static readonly TweakDescriptor QuestDungeonNameDescriptor = new TweakDescriptor(
            "fix.quest-dungeon-name", TweakCategory.Fix, TweakScope.Local,
            "Dungeon names in quest text",
            "Show the dungeon's name in quest dialogue instead of STR_DungeonNoneDisplay, as after the King's Maze.",
            "QuestLogicBase.SetMessageParams sets param 8 from MiniHexInfo.GetPOIDisplayValue(Dungeon, "
            + "m_MainDungeon) of GameDefinition.GetRealmProperties(m_DestRealm, m_DestStageIndex) when "
            + "HasQuestDefID and m_Destination is set, then overwrites it only when the start hex realm's "
            + "m_MainDungeon is not None and FTKHex.GetSpecificDungeon finds it. DungeonQuestLogic."
            + "_determineDestinationFromQuestDef falls back to the dungeon in another realm when the Allocated "
            + "m_DestRealm has none, so m_MainDungeon None yields the missing key STR_DungeonNoneDisplay "
            + "(KillVexor 2_KingsMaze complete dialogue Q2_2_TALK2). The patch sets param 8 through "
            + "SetMessageParam(8, MiniHexDungeon.GetPOIDisplayValue(), true), the vanilla rich-text path.");

        internal static readonly TweakDescriptor XpInLevelDescriptor = new TweakDescriptor(
            "information.xp-in-level", TweakCategory.Information, TweakScope.Local,
            "XP within the level",
            "Show XP as progress through your current level, matching the XP bar, with your total in brackets.",
            "CharacterStats.GetXpDisplayString returns m_PlayerXP / m_LevelXpValues[m_PlayerLevel], both "
            + "cumulative, and m_LevelXpValues[m_MaxCharacterLevels] twice at max level, while "
            + "CharacterStats.GetXpPercent fills the bar from m_LevelXpValues[level - 1] (0 at level 0) to "
            + "m_LevelXpValues[level]. Its callers are uiPlayerMainHud.SetXpDisplay, run when the HUD is "
            + "flagged for update, and uiPlayerStats.UpdateDisplay, run when the stats panel opens. The "
            + "postfix only replaces the returned string, on every machine, for display.");

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
        internal static int QuestDungeonName { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int XpInLevel { get; private set; } = TweakRegistry.InvalidHandle;

        /// <param name="selfTests">Diagnostics/RunSelfTests. The Session probe exists only then.</param>
        internal static void RegisterAll(TweakRegistry registry, bool selfTests = false)
        {
            SkipIntro = registry.Register(SkipIntroDescriptor);
            QuestDungeonName = registry.Register(QuestDungeonNameDescriptor);
            XpInLevel = registry.Register(XpInLevelDescriptor);
            SessionProbe = selfTests ? registry.Register(SessionProbeDescriptor) : TweakRegistry.InvalidHandle;
        }

        /// <summary>The Skip intro decision for one GetAnyButton call. Off, faulted or uninitialized
        /// returns the vanilla result unchanged; on reports a press.</summary>
        internal static bool SkipIntroAnyButton(TweakRegistry registry, int handle, bool vanillaPressed)
        {
            if (!registry.IsOn(handle)) return vanillaPressed;
            return true;
        }

        /// <summary>The game state the quest dungeon name decision reads after vanilla SetMessageParams.
        /// Every flag is phrased so that its default, false, keeps vanilla.</summary>
        internal struct QuestDungeonNameState
        {
            /// <summary>QuestLogicBase.HasQuestDefID: vanilla fills param 8 from the destination realm only then.</summary>
            internal bool HasQuestDefId { get; set; }
            /// <summary>m_Destination is not the null hex.</summary>
            internal bool DestinationSet { get; set; }
            /// <summary>The destination realm's RealmProperties exist and their m_MainDungeon is None.</summary>
            internal bool DestinationRealmHasNoMainDungeon { get; set; }
            /// <summary>Vanilla's start-realm overwrite of param 8 did not fire.</summary>
            internal bool StartRealmDidNotOverwrite { get; set; }
            /// <summary>The destination hex's POI is a MiniHexDungeon.</summary>
            internal bool DestinationIsDungeon { get; set; }
            /// <summary>That dungeon's own GetPOIDisplayValue(), unwrapped.</summary>
            internal string DungeonDisplayValue { get; set; }
        }

        /// <summary>The quest dungeon name decision for one SetMessageParams call. Returns the display
        /// value to put in param 8, or null to leave vanilla's value. Off, faulted or uninitialized, or
        /// any condition failing, returns null.</summary>
        internal static string QuestDungeonNameReplacement(TweakRegistry registry, int handle, QuestDungeonNameState state)
        {
            if (!registry.IsOn(handle)) return null;
            if (!state.HasQuestDefId || !state.DestinationSet || !state.DestinationRealmHasNoMainDungeon) return null;
            if (!state.StartRealmDidNotOverwrite || !state.DestinationIsDungeon) return null;
            return string.IsNullOrEmpty(state.DungeonDisplayValue) ? null : state.DungeonDisplayValue;
        }

        /// <summary>The XP within the level decision for one GetXpDisplayString call. Off, faulted or
        /// uninitialized returns vanilla's text unchanged, as does a window that is not a positive span.</summary>
        internal static string XpInLevelDisplay(TweakRegistry registry, int handle, string vanillaText,
            int level, int maxLevel, int totalXp, int levelStartXp, int levelEndXp)
        {
            if (!registry.IsOn(handle)) return vanillaText;
            return XpInLevelText.Format(level, maxLevel, totalXp, levelStartXp, levelEndXp) ?? vanillaText;
        }
    }
}
