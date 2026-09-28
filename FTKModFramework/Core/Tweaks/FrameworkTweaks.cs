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

        /// <summary>Spec #260 FR-4, a #242 follow-up. Shares the Wet icon's postfix and decision.</summary>
        internal static readonly TweakDescriptor StaleGroupShieldIconDescriptor = new TweakDescriptor(
            "fix.stale-group-shield-icon", TweakCategory.Fix, TweakScope.Local,
            "Clear the group shield icon after combat",
            "Hide the group shield status icon on your HUD outside combat, as the game already does for the other combat statuses.",
            "uiPlayerMainHudStatus.SetStatusIcons sets m_GroupShield from m_CurrentDummy.Shielded, and "
            + "m_GroupShieldLvl from EncounterSession.Instance.GetPlayerShieldStrength() when shielded, only while "
            + "(bool)m_CurrentDummy && m_CharacterStats.m_IsInCombat; its else-branch hides every other "
            + "combat-only icon but never m_GroupShield, and no other Assembly-CSharp method references it. Shielded is "
            + "CharacterDummy.m_SufferingProficiencies holding Category.Shield, a combat dummy status. "
            + "The postfix hides the icon under the else-branch predicate, on every machine, for display.");

        /// <summary>Spec #269 FR-1 and FR-2. Presentation only: it switches two prefab icons that
        /// vanilla never wires and swaps tooltip keys on this machine's HUD, so it is Local.</summary>
        internal static readonly TweakDescriptor PlayerStatusIconsDescriptor = new TweakDescriptor(
            "fix.player-status-icons", TweakCategory.Fix, TweakScope.Local,
            "Show Taunt and Petrified status icons",
            "Show the game's own Taunt and Petrified status icons on your HUD in combat, and name Dazed in the stunned icon's tooltip.",
            "The player HUD prefab's playerMainHudStatus/aliments grid has taunt (sprite statusTaunt, uiToolTipGeneral "
            + "STR_statusTaunt, a key in no text table) and petrified (statusPetrified, STR_statusPetrified in TextInfo), "
            + "both inactive, and uiPlayerMainHudStatus has no field for either. CharacterDummy.Taunting is "
            + "m_SufferingProficiencies holding Category.Taunt and Petrified Category.Petrify. "
            + "uiPlayerMainHudStatus.SetStatusIcons shows combat icons only while (bool)m_CurrentDummy && "
            + "m_CharacterStats.m_IsInCombat. The postfix finds both children once per HUD, sets them under that "
            + "predicate, and points the taunt tooltip at STR_skillsTaunt / STR_skillsTauntInfo (TextInfo). "
            + "CharacterDummy.Stunned is Category.Stunned or Category.Dazed and m_Stunned's tooltip is STR_statusStunned; "
            + "with Dazed alone the postfix uses STR_statusDazed / STR_statusDazedInfo (TextInfo). On every machine, for display.");

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
            "At a shop, show what each unequipped item in your inventory sells for on its item card.",
            "The static uiItemMenu.Show forwards to uiPopupMenu.Show, which sends a uiInventory1ItemContainer "
            + "(equipped) icon to ShowPlayerEquiped, with no Sell button, and any other player icon to "
            + "ShowPlayerBackpack. That shows \"Sell (N)\" interactable only when CanSell holds: "
            + "m_Cow.m_HexLand.GetPOI() (land or air POI) is non-null and CanSellItems(), the item is neither "
            + "quest rarity nor in GameLogic.m_CantSellOrDiscardItems, and m_CanControl (outside multiplayer "
            + "always, otherwise IsOwner or m_WaitForRespawn); N is uiPopupMenu.GetSellItemValue: "
            + "FTK_weaponStats2DB or FTK_itemsDB GetSellValue(m_Cow, m_Cow.GetPOI()). GetSellValue calls "
            + "GetCost, which dereferences the POI. uiInventoryItemDisplay.Show receives the card's real mode "
            + "and character (uiPlayerInventory.SelectItemIcon sets its private m_CurrentItem to the icon, then "
            + "passes Mode.Inventory, its own transform and m_InventoryOwner) but hands uiItemDetail.Show "
            + "Mode.ItemDisplay, so the postfix is on the former. It only extends "
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

        /// <summary>Spec #260 FR-3. Changes a shared rule, so it is Session scope, and only the master
        /// clears the cooldown; clients receive it through vanilla's SyncMember.</summary>
        internal static readonly TweakDescriptor DungeonFindHerbDescriptor = new TweakDescriptor(
            "fix.dungeon-find-herb", TweakCategory.Fix, TweakScope.Session,
            "Find Herb in dungeons",
            "Refresh the party's Find Herb cooldown after each full turn cycle in a dungeon, as a new round does outside.",
            "CharacterSkills.FindHerb returns false while GameFlow.m_FindHerbRoundCoolDown ([FTKSerialize]"
            + "[ExplicitSync]) is set; a find sets it through UpdateFindHerbRoundCoolDown(true), and only "
            + "GameEventManager.UpdateTurnMC clears it, on a new round from the master's GameLogic.UpdateTurn. "
            + "GameFlowMC.EndTurn (a PunRPC sent to the master) re-points m_PlayerCurrentTurn on the first end "
            + "turn after m_JustEnterDungeon, takes next = (GetTurnIndex(current) + 1) % m_IngamePlayerIDs.Count "
            + "and starts a round when next == GameFlow.m_StartTurnIndex or _advanceRound, but not while "
            + "FTKHub.AnyPlayersInDungeon() (a cleared finite dungeon passes the round to BeginTurn, where "
            + "BeginTurnFinished2 skips UpdateTurn while AnyPlayersInDungeon() anyway). In the Endless Dungeon "
            + "IsDungeonCleared() is always false. A prefix computes that suppressed wrap before vanilla runs; "
            + "the postfix calls UpdateFindHerbRoundCoolDown(false) on the master only.",
            "Herbalists can find herbs each party turn in dungeons, not once per visit; many more herbs in the Endless Dungeon.",
            true);

        /// <summary>Spec #260 FR-1, FR-2. A shared rule, since it changes when poison wears off, so it
        /// is Session scope. The balance note makes the default an explicit decision (on, 2026-09-27).</summary>
        internal static readonly TweakDescriptor PoisonDecayResumeDescriptor = new TweakDescriptor(
            "fix.poison-decay-resume", TweakCategory.Fix, TweakScope.Session,
            "Keep poison countdowns on load",
            "A loaded run keeps each character's poison countdown, so poison wears off on the same end turn as if you had never saved.",
            "CharacterStats.m_PoisonTimeCounter is private with no sync or save attribute, and only "
            + "EndTurnActionSequence writes it: +1 per end turn while m_PoisonLvl > 0, 0 otherwise, and at "
            + "PoisonTimeRounds (3) RPCAllSelf(\"UpdatePoison\", -1) and a reset. A load restarts it at 0. "
            + "GameLogic.GetPlayerSerializeData saves PlayerSerialize.m_StateCSData from "
            + "m_CharacterStats.StateDataSerialize(), which the Session record transpiler already decorates; "
            + "the CharacterStats branch adds ftkmf.poison = v1:<1 or 2> for a locked run. On resume, "
            + "EnterFahrulRPC locks the run and fades to black before StartGame, whose CreatePlayer chain runs "
            + "uiQuickPlayerCreate.CreatePlayerRPC, PlayerSerialize.Deserialize and RPCAllSelf(\"StateDataDeserialize\", "
            + "m_StateCSData). The load prefix stashes the value per CharacterStats instance and a "
            + "StateDataDeserializeDone prefix sets the counter once m_PoisonLvl is restored, until "
            + "uiStartGame.AllCowsCreated.",
            "Poison no longer lasts extra turns after loading a save.", true);

        /// <summary>Spec #264. A refund changes a character's resources within a shared run, so it is
        /// Session scope even though it is an exact reversal. Mutation happens on the owner only.</summary>
        internal static readonly TweakDescriptor RefundMovementFocusDescriptor = new TweakDescriptor(
            "convenience.refund-movement-focus", TweakCategory.Convenience, TweakScope.Session,
            "Refund movement focus",
            "Take back focus you spent on movement this turn, up to one point per move you have left, whenever you are standing still with no path chosen. Click a faded focus pip or press F (configurable). Keyboard and mouse only.",
            "The private Movement.ConvertFocusToAction runs only when m_FocusPoints > 0 and m_ActionPoints < 9, "
            + "then calls FTKGameStats m_ActionFocus++, CharacterStats.UpdateFocusPoints(-1) (clamped to MaxFocus, "
            + "UpdateHud, SyncMembers m_FocusPoints, m_BaseMaxFocus, m_SpentFocus) and CharacterOverworld."
            + "UpdatePlayerAction(1) (clamped to 0..9, SyncMember m_ActionPoints). A prefix captures that guard "
            + "and FTKNetworkObject.IsOwner; the postfix counts per FTKPlayerID when both points moved. A refund "
            + "needs min(count, m_ActionPoints) > 0, m_FocusPoints < MaxFocus, IsOwner, m_IsMyTurn, "
            + "Movement.Instance.m_CharacterOverworld, m_Mode == TrackingMode.Movement, m_MovementFSM."
            + "ActiveStateName \"Tracking\", m_HexList.Count <= 1, neither m_LockedInput nor \"PickSneakHex\", "
            + "and neither CharacterStats.m_IsInCombat nor EncounterSession.m_IsInCombat; TrackingPathFinished sets "
            + "m_Mode None before a walk. It calls UpdateFocusPoints(1), UpdatePlayerAction(-1), "
            + "Movement.TrackResetList and uiPlayerMainHud.UpdateHud. Counts clear on CharacterOverworld.EndTurn, "
            + "EncounterSession.StartEncounterSession_Actual, SetInCombat(true), SetDeath and any Session set change. "
            + "The HUD postfixes the private uiPlayerMainHud.SetFocusMeter, whose m_FocusPoints pips show child 0 "
            + "below m_FocusPoints; FTKInput's remap table needs a Rewired action per entry, so the key is framework config. "
            + "Its default F appears in no FTKInput.m_DefaultKeys or m_DefaultKeysMacOverride entry (the override sets "
            + "EndTurn to Backspace on every platform through RestoreDefaultInputMap), no Rewired keyboard map and no "
            + "UnityEngine.Input read.");

        /// <summary>Spec #265 FR-4. Local: it fires only the continuation vanilla's own popup coroutine
        /// would have fired on this machine, sends no RPC, and otherwise only hides this machine's popup.</summary>
        internal static readonly TweakDescriptor StuckSkipTurnPopupDescriptor = new TweakDescriptor(
            "fix.stuck-skip-turn-popup", TweakCategory.Fix, TweakScope.Local,
            "Unstick the skip-turn popup",
            "If the skip-turn popup is interrupted before it closes, close it and carry on with the turn, as the game does after about five seconds.",
            "SkipTurnUI.Show stores the ContinueFSM (never cleared) and runs uiMovementSlots.ShowActionPanelRPC "
            + "here and on the other clients; its InitializeSkipTurn calls ForceHide, BaseInitialize, "
            + "CharacterOverworld.RemoveSkipTurn and StartCoroutine(SlotDisplayExpandSkipTurn), which waits "
            + "m_TransitionTime and m_SlotAppearDelay of Time.time and m_ActionSlotDisplayTimeout of Time.deltaTime "
            + "(1 + 0.8 + 3 s by default) and then calls SkipTurnUI.Close(GetCurrentCOW().m_FTKPlayerID.IsLocal()). "
            + "Close runs Disengage unless m_IsFading and continues m_ContinueFSM when _continue. ForceHide calls "
            + "StopAllCoroutines while m_Root is active, and a deactivated object loses its coroutines, so the close "
            + "never comes. The episode is armed by an InitializeSkipTurn postfix; a ForceHide that stops coroutines, "
            + "an inactive m_Root or host, or 10 s of Time.time triggers one Close(IsLocal()) from a per-frame driver, "
            + "only while the armed character's turn is current. A new InitializeSkipTurn or an Initialize action roll "
            + "ends the episode without a close. A SkipTurnUI.Close prefix turns a second _continue for the same "
            + "m_ContinueFSM into false, because a WaitClients.None ContinueFSM fires on every Continue.");

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
        internal static int StaleGroupShieldIcon { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PlayerStatusIconsFix { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PerfectChanceFix { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int XpInLevel { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PoisonTurns { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int SellPrice { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int VanishingEncounters { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int HouseRulesAchievements { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int OnePressInventory { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int DungeonFindHerb { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int PoisonDecayResume { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int RefundMovementFocus { get; private set; } = TweakRegistry.InvalidHandle;
        internal static int StuckSkipTurnPopup { get; private set; } = TweakRegistry.InvalidHandle;

        /// <param name="selfTests">Diagnostics/RunSelfTests. The Session probe exists only then.</param>
        internal static void RegisterAll(TweakRegistry registry, bool selfTests = false)
        {
            SkipIntro = registry.Register(SkipIntroDescriptor);
            QuestDungeonName = registry.Register(QuestDungeonNameDescriptor);
            StaleWetIcon = registry.Register(StaleWetIconDescriptor);
            StaleGroupShieldIcon = registry.Register(StaleGroupShieldIconDescriptor);
            PlayerStatusIconsFix = registry.Register(PlayerStatusIconsDescriptor);
            PerfectChanceFix = registry.Register(PerfectChanceDescriptor);
            XpInLevel = registry.Register(XpInLevelDescriptor);
            PoisonTurns = registry.Register(PoisonTurnsDescriptor);
            SellPrice = registry.Register(SellPriceDescriptor);
            VanishingEncounters = registry.Register(VanishingEncountersDescriptor);
            HouseRulesAchievements = registry.Register(HouseRulesAchievementsDescriptor);
            OnePressInventory = registry.Register(OnePressInventoryDescriptor);
            DungeonFindHerb = registry.Register(DungeonFindHerbDescriptor);
            PoisonDecayResume = registry.Register(PoisonDecayResumeDescriptor);
            RefundMovementFocus = registry.Register(RefundMovementFocusDescriptor);
            StuckSkipTurnPopup = registry.Register(StuckSkipTurnPopupDescriptor);
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

        /// <summary>The stale combat icon decision for one SetStatusIcons call, shared by every icon
        /// that vanilla sets only in its in-combat branch (m_wet, m_GroupShield). True only when the
        /// icon's tweak is on, vanilla took its else-branch (no combat dummy, or not in combat), and the
        /// icon is still shown. Off, faulted or uninitialized never hides it, so vanilla stands.</summary>
        internal static bool HideStaleCombatIcon(TweakRegistry registry, int handle, bool hasCurrentDummy, bool inCombat, bool iconActive)
        {
            if (!registry.IsOn(handle)) return false;
            bool vanillaInCombatBranch = hasCurrentDummy && inCombat;
            return !vanillaInCombatBranch && iconActive;
        }

        /// <summary>The player status icon decision for one SetStatusIcons call. On mirrors vanilla's
        /// combat branch for the taunt and petrified icons and names Dazed alone; off, faulted or
        /// uninitialized only undoes what the tweak left showing. See PlayerStatusIcons.Decide.</summary>
        internal static PlayerStatusIconChange PlayerStatusIconChanges(TweakRegistry registry, int handle, PlayerStatusIconState state)
        {
            return PlayerStatusIcons.Decide(registry.IsOn(handle), state);
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
        internal static bool SellPriceShown(TweakRegistry registry, int handle, bool inventoryView, bool unequippedIcon,
            bool canControl, bool shopCanSell, bool sellableItem, bool hasPricePoi)
        {
            if (!registry.IsOn(handle)) return false;
            return SellPriceText.Shown(inventoryView, unequippedIcon, canControl, shopCanSell, sellableItem, hasPricePoi);
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

        /// <summary>The prefix half of the dungeon Find Herb fix for one GameFlowMC.EndTurn call: whether
        /// this end turn completes a party turn cycle whose new round vanilla suppresses for the dungeon.
        /// Off, faulted out of the run or uninitialized returns false, so nothing is cleared.</summary>
        internal static bool DungeonHerbCycleEnds(TweakRegistry registry, int handle, DungeonEndTurnState state)
        {
            if (!registry.IsOn(handle)) return false;
            return DungeonHerbCycle.CycleEnds(state);
        }

        /// <summary>The postfix half: clear the shared cooldown only after vanilla ran, on a cycle the
        /// prefix recorded, on the master that owns the shared state, and only while it is set, so one
        /// cycle yields at most one clear and one SyncMember.</summary>
        internal static bool DungeonHerbClear(TweakRegistry registry, int handle, bool runOriginal, bool cycleEnded,
            bool isMasterClient, bool coolDownSet)
        {
            if (!registry.IsOn(handle)) return false;
            return runOriginal && cycleEnded && isMasterClient && coolDownSet;
        }
        /// <summary>The ConvertFocusToAction postfix's decision: count this call only when the tweak is
        /// on, the prefix captured vanilla's guard for the owner, and both points actually moved.</summary>
        internal static bool RefundFocusCounts(TweakRegistry registry, int handle, RefundFocusConversion before,
            int focusAfter, int actionAfter)
        {
            if (!registry.IsOn(handle)) return false;
            return RefundFocus.Converted(before, focusAfter, actionAfter);
        }

        /// <summary>The refund gate. Off, faulted out of the run or uninitialized reports Off, so
        /// nothing is refunded and no pip is shown.</summary>
        internal static RefundFocusBlock RefundFocusGate(TweakRegistry registry, int handle, RefundFocusState state)
        {
            if (!registry.IsOn(handle)) return RefundFocusBlock.Off;
            return RefundFocus.Check(state);
        }

        /// <summary>Refundable pips to show on the HUD; 0 whenever the gate refuses.</summary>
        internal static int RefundFocusPips(TweakRegistry registry, int handle, RefundFocusState state)
        {
            if (!registry.IsOn(handle)) return 0;
            return RefundFocus.Pips(state);
        }

        /// <summary>One refund: the gate, then the exact reversal. Returns the block, None when the
        /// refund was applied.</summary>
        internal static RefundFocusBlock RefundFocusApply(TweakRegistry registry, int handle, RefundFocusLedger ledger,
            long key, RefundFocusState state, IRefundFocusSetters setters)
        {
            RefundFocusBlock block = RefundFocusGate(registry, handle, state);
            if (block != RefundFocusBlock.None) return block;
            RefundFocus.Apply(ledger, key, setters);
            return RefundFocusBlock.None;
        }

        /// <summary>A clear trigger. Off does nothing: counting only happens while on, and turning a
        /// Session tweak off is itself a Session set change that empties the ledger.</summary>
        internal static bool RefundFocusClear(TweakRegistry registry, int handle, RefundFocusLedger ledger,
            RefundFocusClear trigger, long key, bool inCombat = true)
        {
            if (!registry.IsOn(handle)) return false;
            ledger.Clear(trigger, key, inCombat);
            return true;
        }

        /// <summary>The Close prefix's decision for one SkipTurnUI.Close call: the _continue value to
        /// run with. Off, faulted or uninitialized returns vanilla's value unchanged, as does a close
        /// that does not continue. On, a continuation that already went through Close once is refused.</summary>
        internal static bool SkipTurnCloseContinues(TweakRegistry registry, int handle, ContinueOnceGuard guard,
            bool vanillaContinue, object continuation)
        {
            if (!registry.IsOn(handle) || !vanillaContinue || guard == null) return vanillaContinue;
            return guard.Allow(continuation);
        }

        /// <summary>Whether a triggered recovery calls SkipTurnUI.Close: only while the tweak is on and
        /// the armed character still holds the turn. Once the turn has moved on, the continuation vanilla
        /// committed to is no longer the one waiting.</summary>
        internal static bool SkipTurnRecoveryCloses(TweakRegistry registry, int handle, SkipTurnTrigger trigger,
            long armedPlayer, long currentPlayer)
        {
            if (!registry.IsOn(handle)) return false;
            return trigger != SkipTurnTrigger.None && armedPlayer == currentPlayer;
        }
    }
}
