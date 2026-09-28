using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    string nativeDungeonEntryToken;
    MiniHexDungeon nativeDungeonEntryTarget;
    CharacterOverworld nativeDungeonEntryActor;
    HexLand nativeDungeonEntryStagingHex;
    bool nativeDungeonEntryConsumed;
    bool nativeDungeonMenuOpenRequested;
    uiLocationMenuEntry nativeDungeonEntryButton;

    JObject NativeDungeonEntry(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "dungeonInstanceId", "heroInstanceId", "inspectionToken");
        RequireSinglePlayer(); RequireOutsideCombat(); CatalogNoLinks(root);
        string action = Str(command, "action");
        if (action != "open" && action != "inspect" && action != "submit") throw new ArgumentException("action must be open, inspect or submit.");
        if (nativeDungeonEntryConsumed) throw new InvalidOperationException("Native entry already submitted this process; no retry or regeneration.");
        if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted || GameFlow.Instance == null ||
            GameFlow.Instance.m_DungeonEntered != null || FTKHub.Instance == null || FTKHub.Instance.AnyPlayersInDungeon())
            throw new InvalidOperationException("Fresh overworld required; existing or partial dungeon entry cannot be retried.");
        object session = Instance(typeof(EncounterSession)), master = Instance(typeof(EncounterSessionMC));
        IList order = master == null ? null : Field(master, "m_FightOrder") as IList;
        // Native encounter singletons and fight order may not exist before the first encounter.
        // RequireOutsideCombat rejects either existing session in combat; every hero is checked below.
        if ((order != null && order.Count != 0) ||
            (FTKUI.Instance != null && FTKUI.Instance.m_BattleStanceButtons != null && FTKUI.Instance.m_BattleStanceButtons.m_Initialized))
            throw new InvalidOperationException("Idle native encounter sessions and no initialized combat UI required.");
        int dungeonId = LeaseObservationPin.ExactId(command, "dungeonInstanceId", false);
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        MiniHexDungeon target = null;
        foreach (MiniHexDungeon candidate in Resources.FindObjectsOfTypeAll<MiniHexDungeon>())
            if (candidate != null && SceneOwner(candidate) && candidate.GetInstanceID() == dungeonId)
            {
                if (target != null) throw new InvalidOperationException("Ambiguous dungeon identity.");
                target = candidate;
            }
        if (target == null || target.m_HexLand == null || target.m_HexLand.m_HexInfo == null ||
            target.m_HexLand.GetPOI() != target || target.m_Locked || target.m_Deactivated ||
            target.m_DungeonEncounters == null || target.m_DungeonEncounters.Count != 0)
            throw new InvalidOperationException("Exact unlocked, uncleared, ungenerated native dungeon POI required.");
        CharacterOverworld actor = GameLogic.Instance == null ? null : GameLogic.Instance.GetCurrentCOW();
        if (actor == null || actor.GetInstanceID() != heroId || actor.m_TurnEngage == null ||
            actor.m_HexLand == null || actor.m_HexLand.m_HexInfo == null || uiLocationMenuDisplay.Instance == null)
            throw new InvalidOperationException("Exact native current actor, staging tile and location menu required.");
        var party = FTKHub.Instance.m_CharacterOverworlds;
        if (party == null || party.Count != 3) throw new InvalidOperationException("Exactly three living native party members required.");
        List<FTKPlayerID> nativeParty = target.GetLoadPartyPlayers(actor);
        if (nativeParty == null || nativeParty.Count != 3) throw new InvalidOperationException("Native dungeon callback must select all three party members.");
        var seen = new HashSet<int>();
        JArray heroes = new JArray();
        foreach (CharacterOverworld hero in party)
        {
            if (hero == null || !seen.Add(hero.GetInstanceID()) || hero.m_CharacterStats == null ||
                hero.m_CharacterStats.m_HealthCurrent <= 0 || hero.m_CharacterStats.m_IsInCombat || hero.m_WaitForRespawn ||
                hero.IsInDungeon() || hero.m_HexLand != actor.m_HexLand ||
                hero.m_HexLand.m_HexInfo.m_Realm != target.m_HexLand.m_HexInfo.m_Realm || !nativeParty.Contains(hero.m_FTKPlayerID))
                throw new InvalidOperationException("All three living heroes must share one staging tile in the target realm and exact native load-party membership.");
            heroes.Add(hero.GetInstanceID());
        }
        if (!target.ShowLocationMenu()) throw new InvalidOperationException("Native dungeon readiness rejects location entry.");
        uiLocationMenuDisplay display = uiLocationMenuDisplay.Instance;
        if (action == "open")
        {
            if (nativeDungeonMenuOpenRequested || display.IsShowing())
                throw new InvalidOperationException("Native menu already showing or open requested; inspect instead of reopening.");
            // Show handles pre-encounter messages before Show2. Null continuation is supported by ShutdownComplete.
            nativeDungeonMenuOpenRequested = true;
            target.ShowLocationMenu(actor, true, null, false);
            return new JObject { { "ok", true }, { "openRequested", true }, { "dungeonInstanceId", dungeonId },
                { "heroInstanceId", heroId }, { "scope", "Native menu requested. Wait for its visible matching entry, then inspect. No dungeon entry submitted." } };
        }
        if (!display.IsShowing() || display.m_IsPeep || display.m_FSM == null || !display.m_FSM.enabled ||
            display.m_FSM.ActiveStateName != "Showing" || display.m_CanvasGroup == null ||
            !display.m_CanvasGroup.interactable || display.m_CanvasGroup.alpha <= 0f ||
            !ReferenceEquals(Field(display, "m_CurrentCow"), actor) || !ReferenceEquals(Field(display, "m_MiniHexInfo"), target) ||
            display.m_MenuPanel == null || !display.m_MenuPanel.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Visible initialized native menu for this exact actor and dungeon required; wait and inspect again.");
        uiLocationMenuEntry entry = null;
        foreach (uiLocationMenuEntry candidate in display.m_MenuPanel.GetComponentsInChildren<uiLocationMenuEntry>(true))
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy || candidate.m_Menu == null ||
                candidate.m_Menu.m_Location != target || candidate.m_Menu.m_Cow != actor || candidate.m_MethodInfo == null ||
                candidate.m_MethodInfo != typeof(MiniHexDungeon).GetMethod("OnLoadParty", new[] { typeof(CharacterOverworld) })) continue;
            if (entry != null) throw new InvalidOperationException("Ambiguous native dungeon entry button.");
            entry = candidate;
        }
        if (entry == null || entry.m_Button == null || !entry.m_Button.isActiveAndEnabled || !entry.m_Button.IsInteractable())
            throw new InvalidOperationException("Exact active interactable native OnLoadParty entry required.");
        if (action == "inspect")
        {
            nativeDungeonEntryToken = Guid.NewGuid().ToString("N");
            nativeDungeonEntryTarget = target; nativeDungeonEntryActor = actor; nativeDungeonEntryStagingHex = actor.m_HexLand;
            nativeDungeonEntryButton = entry;
        }
        else
        {
            if (string.IsNullOrEmpty(nativeDungeonEntryToken) || Str(command, "inspectionToken") != nativeDungeonEntryToken ||
                target != nativeDungeonEntryTarget || actor != nativeDungeonEntryActor || actor.m_HexLand != nativeDungeonEntryStagingHex ||
                entry != nativeDungeonEntryButton)
                throw new InvalidOperationException("Inspect current exact native entry context and button before submitting.");
            // Consumed before callback because it generates encounters and cannot be safely retried.
            nativeDungeonEntryConsumed = true;
            entry.OnClick();
        }
        return new JObject { { "ok", true }, { "submitted", action == "submit" }, { "inspectionToken", nativeDungeonEntryToken },
            { "entryInstanceId", entry.GetInstanceID() }, { "dungeonInstanceId", dungeonId }, { "dungeonKey", target.m_ID.ToString() }, { "heroInstanceId", heroId },
            { "partyHeroInstanceIds", heroes }, { "nativeEnteredPointerPresent", GameFlow.Instance.m_DungeonEntered != null },
            { "sessionPresent", session != null }, { "masterSessionPresent", master != null }, { "fightOrderPresent", order != null },
            { "scope", "Isolated staging fixture invokes the visible native entry OnClick once. Native callback transports party, ends turn and generates encounters. No forced combat acknowledgements, encounter edits, stats or saves. Submission does not establish completed entry; observe native dungeon UI and room state." } };
    }
}
