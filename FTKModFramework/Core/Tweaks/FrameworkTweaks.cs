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

        internal static readonly TweakDescriptor StaleWetIconDescriptor = new TweakDescriptor(
            "fix.stale-wet-icon", TweakCategory.Fix, TweakScope.Local,
            "Clear the Wet icon after combat",
            "Hide the Wet status icon on your HUD outside combat, as the game already does for the other combat statuses.",
            "uiPlayerMainHudStatus.SetStatusIcons sets m_wet from m_CurrentDummy.Wet only while "
            + "(bool)m_CurrentDummy && m_CharacterStats.m_IsInCombat; its else-branch hides every other "
            + "combat-only icon but never m_wet. Wet is CharacterDummy.m_SufferingProficiencies holding "
            + "Category.Water, a combat dummy status that CharacterStats.HasImmunity also reads only in "
            + "combat. SetStatusIcons runs from uiPlayerMainHud.Update when the HUD is flagged for update. "
            + "The postfix hides the icon under the else-branch predicate, on every machine, for display.");

        internal static readonly TweakDescriptor PerfectChanceDescriptor = new TweakDescriptor(
            "fix.perfect-chance", TweakCategory.Fix, TweakScope.Local,
            "Correct Perfect chances",
            "Show Perfect chances that count Shocked, Illuminated and Darkness, and Taunt's own accuracy. Rolls are unchanged.",
            "CharacterStats.CalculateFullSkillChance returns GetSkillValue(skill, true, modify)^(slots - SpentFocus) "
            + "and ignores the dummy statuses that SlotControl.ComputeAttackSlotResults, ComputeFleeSlotResults "
            + "and ComputeShieldTauntSlotResults apply to each unfocused slot before the roll: Illuminated "
            + "succeeds, then Darkness fails, then slot 0 fails when Shocked. Its only callers are display: "
            + "uiBattleStanceButtons.DisplayBattleActionInfo (flee and shieldtaunt) and "
            + "FTK_proficiencyTable.GetBattleButtonInfo, itself called only there. The shieldtaunt branch passes 0f "
            + "while ComputeShieldTauntSlotResults rolls m_SkillRoll[vitality].Roll(taunt m_PerSlotSkillRoll). "
            + "Postfixes change only the returned figure and the Taunt Perfect line, on every machine, for display.");

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

        internal static readonly TweakDescriptor PoisonTurnsDescriptor = new TweakDescriptor(
            "information.poison-turns", TweakCategory.Information, TweakScope.Local,
            "Poison turns left",
            "Show how many end turns remain until your poison wears off in the poison status tooltip.",
            "CharacterStats.EndTurnActionSequence adds 1 to the private, unsynced and unsaved "
            + "m_PoisonTimeCounter at each end turn while m_HealthCurrent > 0 and "
            + "!CharacterOverworld.m_WaitForRespawn, then at PoisonTimeRounds (3) calls "
            + "RPCAllSelf(\"UpdatePoison\", -1) and resets it; UpdatePoison never resets it. The turn "
            + "FSM state that calls CheckEndTurnAction is activated only where m_PhotonView.isMine, and "
            + "in dungeons DoRemoteEndTurnAction arrives through RPCOwner, so the patch requires "
            + "CharacterOverworld.IsOwner (FTKNetworkObject: ownerId == PhotonNetwork.player.ID). The postfix on "
            + "uiToolTipGeneral.GetMoreToolTip matches m_DetailInfo STR_statusPoisonInfo on the HUD "
            + "(uiPlayerMainHud.m_Cow) and inventory (uiPlayerInventory.m_InventoryOwner) icons and "
            + "only extends the returned string, on this client, for display.");

        internal static readonly TweakDescriptor SellPriceDescriptor = new TweakDescriptor(
            "information.sell-price", TweakCategory.Information, TweakScope.Local,
            "Sell price in item details",
            "At a shop, show what each item in your inventory sells for on its item card.",
            "uiItemMenu.ShowPlayerInventory adds \"Sell (N)\" only when m_Cow.m_HexLand.m_POI is non-null, "
            + "CanSellItems() and the item is neither quest rarity nor in GameLogic.m_CantSellOrDiscardItems; "
            + "N is uiItemMenu.GetSellItemValue: FTK_weaponStats2DB or FTK_itemsDB GetSellValue(m_Cow, "
            + "m_Cow.GetPOI()), and uiPopupMenu uses the same gate and value. GetSellValue calls GetCost, "
            + "which dereferences the POI. uiInventoryItemDisplay.Show receives the card's real mode and "
            + "character (uiPlayerInventory.SelectItemIcon passes Mode.Inventory and m_InventoryOwner) but "
            + "hands uiItemDetail.Show Mode.ItemDisplay, so the postfix is on the former. It only extends "
            + "uiItemDetail.m_ItemRarityDisplay, which uiItemDetail.Show rewrites on every call, on this client.");
        internal static readonly TweakDescriptor VanishingEncountersDescriptor = new TweakDescriptor(
            "information.vanishing-encounters", TweakCategory.Information, TweakScope.Local,
            "Mark encounters that vanish",
            "On a known encounter's hover card, say when leaving it or ending your turn there removes it.",
            "MiniEncounterMenuBase.UseLeaveOrEndTurnButton runs the base leave or end turn, then sends "
            + "RPCAllSelf(\"DecayHexRPC\", m_DecayTime) when GetDBEntry().m_DestroyOnLeave. "
            + "uiHexStatusOverworld.DisplayPoiStatus copies MiniEncounter.GetPOIProfile().m_Effect into the "
            + "hover card once per hover (m_UpdatePanel), not per frame; GetPOIProfile builds a new PoiProfile "
            + "each call and shows GetDBEntry().m_MouseOver only when m_Known, else STR_couldBeAnything. The "
            + "postfix extends m_Effect of the returned profile when m_Known and m_DestroyOnLeave, on this "
            + "client, and never touches GetPOIDisplayValue, which quest params, MessageCoordinator, "
            + "uiBuyMenuHud, OnlineText and the remote-info HUD also read.");

        internal static readonly TweakDescriptor HouseRulesAchievementsDescriptor = new TweakDescriptor(
            "information.house-rules-achievements", TweakCategory.Information, TweakScope.Local,
            "Name the achievements House Rules disable",
            "When your House Rules count as easier, say that the three Defeat Vexor achievements and win statistics won't be recorded.",
            "sPlayerAchievement_trigger.CheckHouseRules sets IsAchieved back to false for a newly achieved row "
            + "when GameFlow.Instance.IsDifficultyEasier() and its sAchievement has !HouseRulesEasyEnabled; "
            + "sPlayerStatistic_trigger.CheckHouseRules restores the old Value for each sStatistic with "
            + "!HouseRulesEasyEnabled. In main.db those are ACH_STORY_KILL_VEXOR_EASY/NORMAL/HARD (3 of 101) and "
            + "15 STAT_GAMEWIN_* (of 254). IsDifficultyEasier is Rules2.IsEasier(m_Rules, GameDif.m_CustomizableRules): "
            + "chaos above, life pool above, or inflation below the difficulty's own, so infinite lives (-1) is "
            + "not easier. GameDifficulty.GetDynamicDifficultyText(Rules2) feeds GameConfig.RefreshDiff, "
            + "WaitingRoom and ResumeBrowser; FTK_gameDifficulty.GetDynamicDifficultyText(Rules) has no caller. "
            + "The postfix applies Rules2.IsEasier to the passed rules and that GameDifficulty's "
            + "m_CustomizableRules and only extends the returned text, on this client.");

        internal static readonly TweakDescriptor OnePressInventoryDescriptor = new TweakDescriptor(
            "convenience.one-press-inventory", TweakCategory.Convenience, TweakScope.Local,
            "One-press inventory",
            "Open your inventory with one press of the Inventory key on keyboard and mouse. Controllers keep the two-press belt.",
            "The private CharacterOverworld.CheckInput sends the first Inventory press to "
            + "SetFocus(m_UIPlayMainHud.m_QuickUseInput), and FTKInputFocus.GetButtonDown ignores the frame "
            + "after SetFocus. A second press reaches uiPlayerMainHud.Update, which starts "
            + "InventoryToggleSequence(true) on the HUD when GameLogic.m_GameAborted is false, "
            + "FTKUI.m_BattleStanceButtons.m_Initialized is false and m_OpenInventory.interactable. The "
            + "CheckInput postfix starts that same coroutine under the same conditions, only for a press on a "
            + "key or mouse button bound to Inventory by a character with CharacterOverworld.m_IsUseMouse, "
            + "two frames later so a same-frame vanilla open is seen first. Input only, on this client.");

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
        internal static int StaleWetIcon { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PerfectChanceFix { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int XpInLevel { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PoisonTurns { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int SellPrice { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int VanishingEncounters { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int HouseRulesAchievements { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int OnePressInventory { get; private set; } = TweakRegistry.InvalidHandle;

        /// <param name="selfTests">Diagnostics/RunSelfTests. The Session probe exists only then.</param>
        internal static void RegisterAll(TweakRegistry registry, bool selfTests = false)
        {
            SkipIntro = registry.Register(SkipIntroDescriptor);
            QuestDungeonName = registry.Register(QuestDungeonNameDescriptor);
            StaleWetIcon = registry.Register(StaleWetIconDescriptor);
            PerfectChanceFix = registry.Register(PerfectChanceDescriptor);
            XpInLevel = registry.Register(XpInLevelDescriptor);
            PoisonTurns = registry.Register(PoisonTurnsDescriptor);
            SellPrice = registry.Register(SellPriceDescriptor);
            VanishingEncounters = registry.Register(VanishingEncountersDescriptor);
            HouseRulesAchievements = registry.Register(HouseRulesAchievementsDescriptor);
            OnePressInventory = registry.Register(OnePressInventoryDescriptor);
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

        /// <summary>The stale Wet icon decision for one SetStatusIcons call. True only when the tweak
        /// is on, vanilla took its else-branch (no combat dummy, or not in combat), and the icon is
        /// still shown. Off, faulted or uninitialized never hides it, so vanilla stands.</summary>
        internal static bool HideStaleWetIcon(TweakRegistry registry, int handle, bool hasCurrentDummy, bool inCombat, bool wetIconActive)
        {
            if (!registry.IsOn(handle)) return false;
            bool vanillaInCombatBranch = hasCurrentDummy && inCombat;
            return !vanillaInCombatBranch && wetIconActive;
        }

        /// <summary>The Perfect chance decision for one CalculateFullSkillChance call. Off, faulted or
        /// uninitialized returns vanilla's figure unchanged; on applies the slot statuses.</summary>
        internal static float PerfectChanceDisplay(TweakRegistry registry, int handle, float vanillaChance,
            int slots, int spentFocus, PerfectChance.SlotStatus statuses)
        {
            if (!registry.IsOn(handle)) return vanillaChance;
            return PerfectChance.WithStatuses(vanillaChance, slots, spentFocus, statuses);
        }

        /// <summary>The Taunt Perfect decision for one DisplayBattleActionInfo call. False leaves
        /// vanilla's line alone; true gives the figure from the per-slot value the taunt roll uses.</summary>
        internal static bool TauntPerfectChance(TweakRegistry registry, int handle, float perSlotSkill,
            int slots, int spentFocus, PerfectChance.SlotStatus statuses, out float chance)
        {
            chance = 0f;
            if (!registry.IsOn(handle)) return false;
            chance = PerfectChance.Full(perSlotSkill, slots, spentFocus, statuses);
            return true;
        }

        /// <summary>The XP within the level decision for one GetXpDisplayString call. Off, faulted or
        /// uninitialized returns vanilla's text unchanged, as does a window that is not a positive span.</summary>
        internal static string XpInLevelDisplay(TweakRegistry registry, int handle, string vanillaText,
            int level, int maxLevel, int totalXp, int levelStartXp, int levelEndXp)
        {
            if (!registry.IsOn(handle)) return vanillaText;
            return XpInLevelText.Format(level, maxLevel, totalXp, levelStartXp, levelEndXp) ?? vanillaText;
        }

        /// <summary>The poison tooltip decision for one GetMoreToolTip call on a poison status icon.
        /// Off, faulted or uninitialized returns vanilla's text unchanged, as does a character this
        /// client does not own (its counter copy stays 0), a dead or respawning one (the counter is
        /// paused), or one that is not poisoned.</summary>
        internal static string PoisonTurnsDetail(TweakRegistry registry, int handle, PoisonTurnsText text,
            string vanillaText, bool owned, bool alive, bool waitingForRespawn, int level, int counter)
        {
            if (!registry.IsOn(handle)) return vanillaText;
            if (!owned || !alive || waitingForRespawn || level <= 0) return vanillaText;
            return text.Append(vanillaText, PoisonTurnsText.Remaining(level, counter));
        }

        /// <summary>Whether one item card gets a sell price. Off, faulted or uninitialized never shows
        /// one; on shows it only where the Sell button's own gate would offer the sale.</summary>
        internal static bool SellPriceShown(TweakRegistry registry, int handle, bool inventoryView, bool shopCanSell,
            bool sellableItem, bool hasPricePoi)
        {
            if (!registry.IsOn(handle)) return false;
            return SellPriceText.Shown(inventoryView, shopCanSell, sellableItem, hasPricePoi);
        }

        /// <summary>The hover card effect for one MiniEncounter.GetPOIProfile call. Off, faulted or
        /// uninitialized returns vanilla's effect unchanged, as does an unknown encounter or one that
        /// stays when the player leaves.</summary>
        internal static string VanishingEncounterEffect(TweakRegistry registry, int handle, string vanillaEffect,
            bool known, bool destroyOnLeave)
        {
            if (!registry.IsOn(handle)) return vanillaEffect;
            if (!VanishingEncounterText.Shown(known, destroyOnLeave)) return vanillaEffect;
            return VanishingEncounterText.Append(vanillaEffect);
        }

        /// <summary>The dynamic difficulty text for one GetDynamicDifficultyText call. Off, faulted or
        /// uninitialized returns vanilla's text unchanged, as do rules that are not easier.</summary>
        /// <param name="easier">GameFlow.Rules2.IsEasier(rules, difficulty.m_CustomizableRules), the
        /// test GameFlow.IsDifficultyEasier applies when the achievement triggers run.</param>
        internal static string HouseRulesDifficultyText(TweakRegistry registry, int handle, string vanillaText, bool easier)
        {
            if (!registry.IsOn(handle)) return vanillaText;
            return HouseRulesText.Append(vanillaText, easier);
        }
    }
}
