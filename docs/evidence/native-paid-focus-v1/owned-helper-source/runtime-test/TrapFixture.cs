using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject trapTicket;
    FTK_dungeonTrap trapTicketRow;
    readonly HashSet<string> trapClaims=new HashSet<string>();
    sealed class TrapContext { public JObject view; public FTK_dungeonTrap row; public readonly Dictionary<string,VoteButton> buttons=new Dictionary<string,VoteButton>(); }
    static object TrapField(object obj,string name)
    {
        if(obj==null)throw new InvalidOperationException("Missing native object for "+name);
        FieldInfo field=obj.GetType().GetField(name,Members);
        if(field==null)throw new InvalidOperationException("Missing native field "+name);
        return field.GetValue(obj);
    }
    static bool TrapPanelActive(object panel)
    {
        GameObject go=panel as GameObject;Component component=panel as Component;
        if(go==null&&component!=null)go=component.gameObject;
        if(go==null)throw new InvalidOperationException("Native modal panel unavailable.");
        return go.activeInHierarchy;
    }
    JObject TrapPins()
    {
        CatalogNoLinks(root);RequireSinglePlayer();
        CatalogNoLinks(Path.Combine(root,"model-test-session.json"));
        if((string)JObject.Parse(File.ReadAllText(Path.Combine(root,"model-test-session.json")))["session"]!=sessionId)throw new InvalidOperationException("Trap helper nonce file changed.");
        foreach(Assembly assembly in new[]{typeof(EnemyDummy).Assembly,typeof(RuntimeModelTest).Assembly})
        {string path=Path.GetFullPath(assembly.Location);CatalogNoLinks(path);if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("Trap assembly outside owned root.");}
        JObject game=ScaleIdentity(typeof(EnemyDummy).Assembly);
        if((string)game["assemblyFileSha256"]!=KrakenControllerGameAssembly)throw new InvalidOperationException("Trap native source pin mismatch.");
        return new JObject{{"core",PreviewCoreIdentity()},{"helper",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},{"gameAssembly",game}};
    }
    TrapContext ReadTrap()
    {
        JObject pins=TrapPins();
        if(busy || (spawnCapture!=null && !spawnCapture.terminal))throw new InvalidOperationException("Another capture/arrival is pending.");
        // Read modal signals BEFORE native identity/predicate resolution. No bridge call between final predicate and native click.
        JObject bridge=JObject.FromObject(CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Agent.StateReader",true).GetMethod("ReadState",Statics,null,Type.EmptyTypes,null).Invoke(null,null));
        JObject signals=bridge["signals"]as JObject;
        if(signals==null||signals["modalOpen"]==null||signals["modalOpen"].Type!=JTokenType.Boolean||signals["choiceOpen"]==null||signals["choiceOpen"].Type!=JTokenType.Boolean)throw new InvalidOperationException("Native modal observation unavailable.");
        JArray warnings=signals["warnings"]as JArray;
        if(warnings==null)throw new InvalidOperationException("Native modal warning status unavailable.");
        foreach(JToken warning in warnings){string text=(string)warning;if(text!=null&&(text.StartsWith("choices:",StringComparison.Ordinal)||text.StartsWith("modalType:",StringComparison.Ordinal)))throw new InvalidOperationException("Native modal read failed: "+text);}
        MessageCoordinator coordinator=MessageCoordinator.Instance;FTKUI ui=FTKUI.Instance;FTKInput input=FTKInput.Instance;
        if(coordinator==null||ui==null||input==null||ui.m_PortraitMessage==null||ui.m_QuestConfirm==null||ui.m_PortraitMessage.m_MessagePanel==null||ui.m_QuestConfirm.m_MessagePanel==null)
            throw new InvalidOperationException("Native modal surfaces unavailable.");
        if(uiOptionsMenu.Instance==null||uiPlayerInventory.Instance==null)throw new InvalidOperationException("Native options/inventory status unavailable.");
        bool globalOpen=TrapPanelActive(TrapField(ui.m_GlobalMessage,"m_MessagePanel"))||TrapPanelActive(TrapField(ui.m_GlobalMessage,"m_ChoiceButtonPanel"));
        bool noModal=!globalOpen&&Time.timeScale>0&&!uiOptionsMenu.Instance.m_Showing&&!uiPlayerInventory.Instance.m_IsShowing&&!(bool)signals["modalOpen"]&&!(bool)signals["choiceOpen"]&&!input.m_WaitingForPopup
            &&!ui.m_PortraitMessage.m_MessagePanel.gameObject.activeInHierarchy&&!ui.m_QuestConfirm.m_MessagePanel.gameObject.activeInHierarchy
            &&TrapField(coordinator,"m_CurrentMessageInstances")==null;
        FTKHub hub=FTKHub.Instance;GameLogic logic=GameLogic.Instance;GameFlow flow=GameFlow.Instance;
        EncounterSession es=EncounterSession.Instance;EncounterSessionMC mc=EncounterSessionMC.Instance;
        if(hub==null||logic==null||flow==null||es==null||mc==null||hub.m_CharacterOverworlds==null||hub.m_CharacterOverworlds.Count!=1)
            throw new InvalidOperationException("One actual owned single-player hero and native sessions required.");
        CharacterOverworld cow=hub.m_CharacterOverworlds[0];MiniHexDungeon dungeon=flow.m_DungeonEntered;
        if(cow==null||dungeon==null||cow.m_CharacterStats==null||cow.m_CurrentDummy==null||cow.m_CurrentDummy.m_EventListener==null)
            throw new InvalidOperationException("Native dungeon/hero/dummy unavailable.");
        CharacterDummy dummy=cow.m_CurrentDummy;CharacterEventListener cel=dummy.m_EventListener;
        DioramaDungeon diorama=es.GetDioramaDungeon();DungeonTrap trap=diorama==null?null:diorama.m_ActiveTrap;
        if(trap==null)throw new InvalidOperationException("Actual native active trap unavailable.");
        FTK_dungeonTrap row=trap.GetDB();if(row==null||string.IsNullOrEmpty(row.m_ID))throw new InvalidOperationException("Exact native trap DB row unavailable.");
        MiniHexDungeon.EncounterType kind=dungeon.m_EncounterType;
        bool nativeTrap=(kind==MiniHexDungeon.EncounterType.Trap1||kind==MiniHexDungeon.EncounterType.Trap2||kind==MiniHexDungeon.EncounterType.Trap3)
            &&kind.Equals(TrapField(mc,"m_EncounterType"))&&trap.m_Type==kind;
        bool scope=(bool)bridge["inSession"]&&cow.IsOwner&&cow==logic.GetCurrentCOW()&&cow.m_CharacterStats.m_HealthCurrent>0
            &&cow.GetPOI()==dungeon&&dummy.m_CharacterOverworld==cow&&cel.m_Dummy==dummy&&dummy.FID==cow.m_FTKPlayerID&&!es.m_IsInCombat;
        IList order=TrapField(mc,"m_FightOrder")as IList;IList queue=TrapField(mc,"m_VoteQueue")as IList;
        PropertyInfo votingProperty=typeof(EncounterSessionMC).GetProperty("m_VotingPlayerID",Members);
        if(votingProperty==null)throw new InvalidOperationException("Native voting FID property missing.");
        FTKPlayerID voting=(FTKPlayerID)votingProperty.GetValue(mc,null);
        bool heroQueue=order!=null&&order.Count==1&&((EncounterSessionMC.FightOrderEntry)order[0]).m_Pid==cow.m_FTKPlayerID
            &&queue!=null&&queue.Count==1&&(FTKPlayerID)queue[0]==cow.m_FTKPlayerID&&voting==cow.m_FTKPlayerID;
        PlayMakerFSM fsm=mc.m_VoteFSM;
        bool nativeVote=es.m_VoteType==EncounterSessionMC.VoteType.Trap&&mc.m_VoteType==EncounterSessionMC.VoteType.Trap
            &&fsm!=null&&fsm.enabled&&fsm.gameObject.activeInHierarchy&&fsm.ActiveStateName=="Show Vote Buttons";
        bool trapActive=trap.gameObject.activeInHierarchy&&trap.enabled&&!trap.m_Disarmed&&trap.m_DungeonHex==dungeon&&trap.m_ParentDiorama==diorama;
        if(cow.m_UIPlayMainHud==null||cow.m_UIPlayMainHud.m_LootCollectionButtons==null)throw new InvalidOperationException("Native hero vote UI unavailable.");
        VoteButtonContainer container=cow.m_UIPlayMainHud.m_LootCollectionButtons;JObject buttons=new JObject();TrapContext context=new TrapContext{row=row};
        foreach(VoteButton.VoteOption option in new[]{VoteButton.VoteOption.Disarm,VoteButton.VoteOption.Proceed})
        {
            VoteButton button;container.m_VoteButtonTable.TryGetValue(option,out button);Button unity=button==null?null:button.GetComponent<Button>();
            bool usable=container.gameObject.activeInHierarchy&&container.m_VoteType==EncounterSessionMC.VoteType.Trap&&NativeButtonUsable(button)
                &&button.transform.IsChildOf(container.transform)&&button.m_Option==option&&button.m_Hud==cow.m_UIPlayMainHud&&button.m_Hud.m_Cow==cow;
            buttons[option.ToString()]=new JObject{{"id",button==null?0:button.GetInstanceID()},{"unityButtonId",unity==null?0:unity.GetInstanceID()},
                {"active",button!=null&&button.gameObject.activeInHierarchy},{"enabled",button!=null&&button.enabled},{"interactable",unity!=null&&unity.enabled&&unity.IsInteractable()},
                {"focusing",button!=null&&button.m_Focusing},{"usable",usable}};
            if(button!=null)context.buttons.Add(option.ToString(),button);
        }
        JObject identity=new JObject{{"root",root},{"session",sessionId},{"pins",pins},{"heroId",cow.GetInstanceID()},{"heroFid",SpawnFid(cow.m_FTKPlayerID)},
            {"dummyId",dummy.GetInstanceID()},{"celId",cel.GetInstanceID()},{"esId",es.GetInstanceID()},{"mcId",mc.GetInstanceID()},
            {"dungeonId",dungeon.GetInstanceID()},{"level",dungeon.m_Level},{"room",dungeon.m_RoomIndex},{"dioramaId",diorama.GetInstanceID()},
            {"trapId",trap.GetInstanceID()},{"trapTableId",trap.m_ID.ToString()},{"trapDbRowKey",row.m_ID},{"trapType",kind.ToString()},{"voteFsmId",fsm==null?0:fsm.GetInstanceID()},
            {"containerId",container.GetInstanceID()}};
        context.view=new JObject{{"identity",identity},{"scope",scope},{"noModal",noModal},{"nativeTrap",nativeTrap},{"heroQueue",heroQueue},{"nativeVote",nativeVote},{"trapActive",trapActive},
            {"buttons",buttons},{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup},{"signals",signals.DeepClone()},
            {"mcInCombat",(bool)TrapField(mc,"m_IsInCombat")},{"voteFsm",FsmView(fsm)},{"queueCount",queue==null?-1:queue.Count},
            {"fightOrderCount",order==null?-1:order.Count},{"votingFid",SpawnFid(voting)},{"health",cow.m_CharacterStats.m_HealthCurrent}};
        return context;
    }
    JObject TrapState(JObject command)
    {
        CatalogKeys(command,"id","session","op");trapTicket=null;trapTicketRow=null;
        try
        {
            TrapContext context=ReadTrap();JObject view=context.view;view["ticketId"]=Guid.NewGuid().ToString("N");view["expiresAfterRealtimeSeconds"]=5;
            view["alreadySubmitted"]=trapClaims.Contains(TrapSubmissionPolicy.Key((JObject)view["identity"]));view["ok"]=true;
            trapTicket=(JObject)view.DeepClone();trapTicketRow=context.row;return view;
        }
        catch(Exception error){return new JObject{{"ok",true},{"available",false},{"error",error.Message},{"frame",Time.frameCount}};}
    }
    JObject TrapSubmit(JObject command)
    {
        CatalogKeys(command,"id","session","op","ticketId","identity","option");
        TrapContext current=ReadTrap();
        TrapSubmissionPolicy.SameRow(trapTicketRow,current.row);
        TrapSubmissionPolicy.Validate(command,trapTicket,current.view,Time.frameCount,Time.realtimeSinceStartup);
        string option=Str(command,"option");VoteButton button=current.buttons[option];
        MethodInfo click=typeof(VoteButton).GetMethod("OnLeftClick",Members,null,Type.EmptyTypes,null);
        if(click==null)throw new InvalidOperationException("Exact native trap click method missing.");
        JObject before=(JObject)current.view.DeepClone();
        string key=TrapSubmissionPolicy.Key((JObject)before["identity"]);
        if(trapClaims.Contains(key)||trapClaims.Count>=32)throw new InvalidOperationException("Trap room already submitted or claim limit; no retry.");
        try{TrapSubmissionPolicy.Submit(trapClaims,key,delegate{click.Invoke(button,null);});}
        catch(Exception error)
        {
            trapTicket=null;trapTicketRow=null;
            return new JObject{{"ok",false},{"status","uncertain-native-submission"},{"error",error.ToString()},{"before",before},{"claimRetained",trapClaims.Contains(key)},
                {"note","No retry. Observe fresh trap-state only; native callback may have partially progressed."}};
        }
        trapTicket=null;trapTicketRow=null;
        return new JObject{{"ok",true},{"status","submitted"},{"option",option},{"before",before},{"afterFrame",Time.frameCount},
            {"afterButtonActive",button!=null&&button.gameObject.activeInHierarchy},{"note","One native VoteButton.OnLeftClick submitted. Native EndOfFrame vote/slots/trap outcome pending; no retry or success guarantee."}};
    }
}
