using System;
using GridEditor;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static JToken WorldInputRead(Func<JToken> read)
    {
        try{return read();}
        catch(Exception e){return new JObject{{"available",false},{"error",e.GetType().Name+": "+e.Message}};}
    }
    static JObject WorldInputHex(HexLand hex)
    {
        return hex==null?null:new JObject{{"instanceId",hex.GetInstanceID()},{"parentIndex",hex.m_ParentIndex},{"index",hex.m_Index}};
    }
    static JObject WorldInputFocus(FTKInputFocus focus)
    {
        if(focus==null)return null;
        return new JObject{{"instanceId",focus.GetInstanceID()},{"name",focus.gameObject.name},
            {"active",focus.gameObject.activeInHierarchy},{"enabled",focus.enabled},
            {"hasInputFocus",focus.m_HasInputFocus},{"firstFrameAfterSetFocus",focus.m_FirstFrameAfterSetFocus},
            {"inputMode",focus.m_InputMode.ToString()},
            {"selected",focus.m_CurrentSelected==null?null:focus.m_CurrentSelected.gameObject.name}};
    }
    static JObject WorldInputHero(CharacterOverworld cow)
    {
        if(cow==null)return null;
        CharacterStats stats=cow.m_CharacterStats;
        return new JObject{{"instanceId",cow.GetInstanceID()},{"identity",cow.m_FTKPlayerID.m_TurnIndex+":"+cow.m_FTKPlayerID.m_PhotonID},
            {"active",cow.gameObject.activeInHierarchy},{"enabled",cow.enabled},{"isOwner",cow.IsOwner},
            {"waitForRespawn",cow.m_WaitForRespawn},{"turnState",cow.m_TurnEngage==null?null:cow.m_TurnEngage.ActiveStateName},
            {"stats",stats==null?null:new JObject{{"isMyTurn",stats.m_IsMyTurn},{"isCombatTurn",stats.m_IsCombatTurn},
                {"inCombat",stats.m_IsInCombat},{"hp",stats.m_HealthCurrent},{"actionPoints",stats.m_ActionPoints}}},
            // m_HexLand is a property with last-hex fallback, not a reflected field.
            {"hex",WorldInputHex(cow.m_HexLand)},
            {"inputFocus",WorldInputFocus(cow.m_InputFocus)},
            {"quickUseInput",cow.m_UIPlayMainHud==null?null:WorldInputFocus(cow.m_UIPlayMainHud.m_QuickUseInput)}};
    }
    static JObject WorldInputTransform(Transform transform)
    {
        if(transform==null)return null;
        RectTransform rect=transform as RectTransform;
        Vector3 position=transform.localPosition;
        return new JObject{{"instanceId",transform.GetInstanceID()},{"name",transform.name},
            {"activeSelf",transform.gameObject.activeSelf},{"activeInHierarchy",transform.gameObject.activeInHierarchy},
            {"localPosition",new JArray(position.x,position.y,position.z)},
            {"rect",rect==null?null:new JArray(rect.rect.x,rect.rect.y,rect.rect.width,rect.rect.height)}};
    }
    static JObject WorldInputTitle(StartGameFE.MainScreen screen)
    {
        Transform parent=screen.m_SelectableParent;
        JArray mods=new JArray();
        if(parent!=null)foreach(Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if(child==null || child.name!="ModsButton")continue;
            JObject entry=WorldInputTransform(child);
            JArray texts=new JArray(),buttons=new JArray();
            foreach(Text text in child.GetComponentsInChildren<Text>(true))
            {
                JObject label=WorldInputTransform(text.transform);
                label["text"]=text.text;label["enabled"]=text.enabled;texts.Add(label);
            }
            foreach(Button button in child.GetComponentsInChildren<Button>(true))
            {
                JObject item=WorldInputTransform(button.transform);
                item["enabled"]=button.enabled;item["interactable"]=button.interactable;buttons.Add(item);
            }
            entry["texts"]=texts;entry["buttons"]=buttons;mods.Add(entry);
        }
        return new JObject{{"instanceId",screen.GetInstanceID()},{"scene",screen.gameObject.scene.name},
            {"isCurrentFocus",FTKInput.Instance!=null && FTKInput.Instance.m_CurrentInputFocus==screen},
            {"root",WorldInputTransform(screen.transform)},{"enabled",screen.enabled},
            {"selectableParent",WorldInputTransform(parent)},{"modsButtons",mods}};
    }
    static JToken WorldInputTitles()
    {
        JArray screens=new JArray();
        // Include inactive scene instances, but never report resource prefab assets as live menus.
        foreach(StartGameFE.MainScreen screen in Resources.FindObjectsOfTypeAll<StartGameFE.MainScreen>())
            if(screen!=null && screen.gameObject.scene.IsValid())
                screens.Add(WorldInputRead(delegate{return WorldInputTitle(screen);}));
        return screens;
    }
    static JToken WorldInputArrayLength(Array value)
    {return value==null?new JValue((object)null):new JValue(value.Length);}
    static JToken WorldInputInteger(object[] values,int index)
    {return values!=null && index<values.Length && values[index] is int?new JValue((int)values[index]):new JValue((object)null);}
    static JObject WorldInputCreation(uiQuickPlayerCreate screen)
    {
        object[] saved=screen.m_SerializedData==null?null:screen.m_SerializedData.m_InstData;
        JArray types=new JArray();
        if(saved!=null)for(int i=0;i<Math.Min(saved.Length,64);i++)
            types.Add(saved[i]==null?null:saved[i].GetType().FullName);
        return new JObject{{"instanceId",screen.GetInstanceID()},{"activeSelf",screen.gameObject.activeSelf},
            {"activeInHierarchy",screen.gameObject.activeInHierarchy},{"enabled",screen.enabled},
            {"turnIndex",screen.m_TurnIndex},{"classId",screen.m_ClassID},
            {"serialized",saved==null?null:new JObject{{"length",saved.Length},{"types",types},
                {"turnIndex",WorldInputInteger(saved,1)},{"classId",WorldInputInteger(saved,4)}}},
            {"paletteLengths",new JObject{{"main",WorldInputArrayLength(screen.m_MainColorArray)},
                {"skin",WorldInputArrayLength(screen.m_SkinColorArray)},{"hair",WorldInputArrayLength(screen.m_HairColorArray)}}},
            {"paletteIndices",new JObject{{"main",screen.m_MainColorIndex},{"skin",screen.m_SkinColorIndex},{"hair",screen.m_HairColorIndex}}}};
    }
    static JToken WorldInputCreations()
    {
        JArray screens=new JArray();
        foreach(uiQuickPlayerCreate screen in Resources.FindObjectsOfTypeAll<uiQuickPlayerCreate>())
            if(screen!=null && screen.gameObject.scene.IsValid())
                screens.Add(WorldInputRead(delegate{return WorldInputCreation(screen);}));
        return new JObject{{"screens",screens},
            {"classArrayLength",WorldInputRead(delegate{return WorldInputArrayLength(FTK_playerGameStartDB.GetDB().m_Array);})},
            {"createUITargetsLength",WorldInputRead(delegate{return uiStartGame.Instance==null || uiStartGame.Instance.m_CreateCharacterRoot==null?null:WorldInputArrayLength(uiStartGame.Instance.m_CreateCharacterRoot.m_CreateUITargets);})},
            {"playerTargetsLength",WorldInputRead(delegate{return SelectScreenCamera.Instance==null?null:WorldInputArrayLength(SelectScreenCamera.Instance.m_PlayerTargets);})}};
    }
    static JArray WorldInputIdentities(FTKPlayerID[] ids)
    {
        if(ids==null)return null;
        JArray result=new JArray();
        for(int i=0;i<Math.Min(ids.Length,64);i++)result.Add(ids[i].m_TurnIndex+":"+ids[i].m_PhotonID);
        return result;
    }
    static JObject WorldInputPoi(MiniHexInfo poi)
    {
        return poi==null?null:new JObject{{"instanceId",poi.GetInstanceID()},
            {"type",poi.GetType().FullName},{"miniHexType",poi.m_MiniHexType.ToString()},
            {"active",poi.gameObject.activeInHierarchy}};
    }
    static JToken WorldInputEncounterPois()
    {
        FTKHex world=FTKHex.Instance;
        if(world==null || world.m_MiniHexList==null)return null;
        JArray groups=new JArray();
        foreach(MiniHexInfo.MiniHexType type in new[]{MiniHexInfo.MiniHexType.Enemy,MiniHexInfo.MiniHexType.Dungeon})
        {
            JArray entries=new JArray();
            // GetPOIList inserts a list for a missing key, so only query existing categories.
            bool present=world.m_MiniHexList.ContainsKey(type);
            var pois=present?world.GetPOIList(type):null;
            if(pois!=null)for(int i=0;i<Math.Min(pois.Count,1024);i++)
            {
                MiniHexInfo poi=pois[i];
                if(poi==null)continue;
                MiniHexEnemy enemy=poi as MiniHexEnemy;
                MiniHexDungeon dungeon=poi as MiniHexDungeon;
                JObject entry=WorldInputPoi(poi);
                entry["hex"]=WorldInputHex(poi.m_HexLand);
                entry["nativeId"]=enemy!=null?enemy.m_EnemyType:dungeon!=null?dungeon.m_ID.ToString():null;
                entry["deactivated"]=poi.m_Deactivated;
                entry["locked"]=poi.m_Locked;
                entry["hidden"]=poi.m_Hidden;
                entries.Add(entry);
            }
            groups.Add(new JObject{{"category",type.ToString()},{"registered",present},
                {"nativeCount",pois==null?0:pois.Count},{"limit",1024},
                {"truncated",pois!=null && pois.Count>1024},{"entries",entries}});
        }
        return groups;
    }

    static JObject WorldInputEncounterFsm(PlayMakerFSM component)
    {
        if(component==null)return null;
        JObject result=NativeFightFsmState(component.Fsm);
        JArray integers=new JArray(),booleans=new JArray();
        // Enumerate existing primitive variables only; lookup helpers can synthesize missing variables.
        if(component.FsmVariables!=null)
        {
            var ints=component.FsmVariables.IntVariables;
            if(ints!=null)for(int i=0;i<Math.Min(ints.Length,64);i++)
                if(ints[i]!=null)integers.Add(new JObject{{"name",ints[i].Name},{"value",ints[i].Value}});
            var bools=component.FsmVariables.BoolVariables;
            if(bools!=null)for(int i=0;i<Math.Min(bools.Length,64);i++)
                if(bools[i]!=null)booleans.Add(new JObject{{"name",bools[i].Name},{"value",bools[i].Value}});
        }
        result["integerVariables"]=integers;result["booleanVariables"]=booleans;
        return result;
    }
    static JToken WorldInputMasterEncounter()
    {
        EncounterSessionMC master=EncounterSessionMC.Instance;if(master==null)return null;
        WaitForClientAcknowledge ack=(WaitForClientAcknowledge)typeof(EncounterSessionMC).GetField("m_WaitForClientAck",Members).GetValue(master);
        JArray waiting=null;
        if(ack!=null && ack.m_WaitList!=null)
        {waiting=new JArray();for(int i=0;i<Math.Min(ack.m_WaitList.Count,64);i++)waiting.Add(ack.m_WaitList[i]);}
        return new JObject{{"instanceId",master.GetInstanceID()},{"started",master.m_EncounterStarted},
            {"inCombat",master.m_IsInCombat},{"enabled",master.enabled},{"activeInHierarchy",master.gameObject.activeInHierarchy},
            {"lootCollectionFsm",WorldInputRead(delegate{return WorldInputEncounterFsm(master.m_LootCollectionFSM);})},
            {"voteFsm",WorldInputRead(delegate{return WorldInputEncounterFsm(master.m_VoteFSM);})},
            {"showNextXPGoldPending",master.IsInvoking("ShowNextXPGold")},{"returnToOverworldPending",master.IsInvoking("ReturnToOverworld")},
            {"completionContinuation",WorldInputRead(delegate{return NativeFightContinuationState((ContinueFSM)typeof(EncounterSessionMC).GetField("m_ContinueFSM",Members).GetValue(master));})},
            {"encounterType",typeof(EncounterSessionMC).GetField("m_EncounterType",Members).GetValue(master).ToString()},
            {"encounterLocation",typeof(EncounterSessionMC).GetField("m_EncounterLocation",Members).GetValue(master).ToString()},
            {"activeEncounterIndex",(int)typeof(EncounterSessionMC).GetField("m_ActiveEncounterIndex",Members).GetValue(master)},
            {"clients",WorldInputIdentities(master.m_AllClientAtEncounterStart)},
            {"combatants",WorldInputIdentities(master.m_AllCombtatants)},
            {"aliveCombatantCount",master.m_AllCombtatantsAlive==null?new JValue((object)null):new JValue(master.m_AllCombtatantsAlive.Count)},
            {"poi",WorldInputPoi((MiniHexInfo)typeof(EncounterSessionMC).GetField("m_POI",Members).GetValue(master))},
            {"ack",ack==null?null:new JObject{{"waitId",ack.m_WaitID},{"invokeMethod",ack.m_InvokeMethod},
                {"invokeDelay",ack.m_InvokeDelay},{"waiting",waiting},{"clients",WorldInputIdentities(ack.m_AllClients)},
                {"ownerInstanceId",ack.m_Owner==null?new JValue((object)null):new JValue(ack.m_Owner.GetInstanceID())}}}};
    }
    JObject WorldInputObservation(JObject command)
    {
        CatalogKeys(command,"id","session","op");
        JObject result=new JObject{{"ok",true},{"frame",Time.frameCount},{"timeScale",Time.timeScale},
            {"scope","Read-only native world input gates; no input polling, event dispatch, focus change or state mutation."}};
        result["creationScreens"]=WorldInputRead(WorldInputCreations);
        result["titleScreens"]=WorldInputRead(WorldInputTitles);
        result["currentHero"]=WorldInputRead(delegate{return WorldInputHero(GameLogic.Instance==null?null:GameLogic.Instance.GetCurrentCOW());});
        result["party"]=WorldInputRead(delegate{
            JArray heroes=new JArray();if(FTKHub.Instance!=null)foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
                if(cow!=null)heroes.Add(WorldInputRead(delegate{return WorldInputHero(cow);}));return heroes;});
        result["movement"]=WorldInputRead(delegate{
            Movement movement=Movement.Instance;if(movement==null)return null;
            HexLand start=(HexLand)typeof(Movement).GetField("m_StartHex",Members).GetValue(movement);
            return new JObject{{"state",movement.m_MovementFSM==null?null:movement.m_MovementFSM.ActiveStateName},
                {"mode",movement.m_Mode.ToString()},{"hero",WorldInputHero(movement.m_CharacterOverworld)},
                {"startHex",WorldInputHex(start)},{"cursorHex",WorldInputHex(movement.m_CursorHex)}};});
        result["input"]=WorldInputRead(delegate{
            FTKInput input=FTKInput.Instance;if(input==null)return null;
            Rewired.Player controller=input.GetCurrentController();
            return new JObject{{"focus",WorldInputFocus(input.m_CurrentInputFocus)},
                {"controllerCursorMode",input.m_ControllerCursorMode},{"inputHero",WorldInputHero(input.GetInputPlayer())},
                {"controller",controller==null?null:new JObject{{"id",controller.id},{"name",controller.name}}}};});
        result["ui"]=WorldInputRead(delegate{
            return new JObject{{"endTurnPresent",uiEndTurnButton.Instance!=null},
                {"endTurnInteractable",uiEndTurnButton.Instance==null?new JValue((object)null):new JValue(uiEndTurnButton.Instance.interactable)},
                {"developerConsole",FTKHub.Instance==null || FTKHub.Instance.m_DeveloperConsole==null?new JValue((object)null):new JValue(FTKHub.Instance.m_DeveloperConsole.showing)},
                {"modal",FTKUI.Instance==null?new JValue((object)null):new JValue(FTKUI.Instance.IsModal)},
                {"chatInputFocused",uiChatBox.Instance==null?new JValue((object)null):new JValue(uiChatBox.Instance.IsTextInputInFocus())},
                {"systemDialog",uiSystemDialog.Instance==null || uiSystemDialog.Instance.m_DialogRoot==null?new JValue((object)null):new JValue(uiSystemDialog.Instance.m_DialogRoot.gameObject.activeInHierarchy)},
                {"bugForm",uiSystemDialog.Instance==null || uiSystemDialog.Instance.m_BugFormRoot==null?new JValue((object)null):new JValue(uiSystemDialog.Instance.m_BugFormRoot.gameObject.activeInHierarchy)},
                {"overworldCameraEnabled",OverworldCamera.Instance==null || OverworldCamera.Instance.m_Camera==null?new JValue((object)null):new JValue(OverworldCamera.Instance.m_Camera.enabled)}};});
        result["encounterPois"]=WorldInputRead(WorldInputEncounterPois);
        result["masterEncounter"]=WorldInputRead(WorldInputMasterEncounter);
        result["currentPoi"]=WorldInputRead(delegate{
            CharacterOverworld cow=GameLogic.Instance==null?null:GameLogic.Instance.GetCurrentCOW();
            return cow==null?null:WorldInputPoi(cow.GetMiniHexInfo());});
        result["encounter"]=WorldInputRead(delegate{
            EncounterSession encounter=EncounterSession.Instance;EncounterSessionMC master=EncounterSessionMC.Instance;
            return new JObject{{"showXPGoldFsm",WorldInputRead(delegate{return encounter==null?null:WorldInputEncounterFsm(encounter.m_ShowXPGoldFSM);})},
                {"teardownFsm",WorldInputRead(delegate{return encounter==null?null:WorldInputEncounterFsm(encounter.m_FSM);})},
                {"state",encounter==null || encounter.m_FSM==null?null:encounter.m_FSM.ActiveStateName},
                {"encounterType",encounter==null?null:encounter.m_EncounterType.ToString()},
                {"encounterLocation",encounter==null?null:encounter.m_EncounterLocation.ToString()},
                {"inCombat",encounter==null?new JValue((object)null):new JValue(encounter.m_IsInCombat)},
                {"currentPlayer",encounter==null?null:encounter.m_CurrentPlayer.ToString()},
                {"voteType",encounter==null?null:encounter.m_VoteType.ToString()},
                {"keepDiorama",master==null?new JValue((object)null):new JValue(master.m_DoKeepDiorama)}};});
        return result;
    }
}
