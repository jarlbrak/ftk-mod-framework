using System;
using GridEditor;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    // One ticket per helper session. The native callback captures only this bounded managed record.
    sealed class EntryTicket : EntryPreparationProgress
    {
        internal string id,session,ownedRoot;
        internal JObject pins,core;
        internal ContinueFSM continuation;
        internal int quietFrame=-1;
        internal float quietSince=-1;
        internal void Completed(){RecordCallback(Time.frameCount,Time.realtimeSinceStartup);}
    }
    EntryTicket entryTicket;
    JObject entryObservationCore;
    sealed class EntryContext
    {
        internal CharacterOverworld cow;
        internal MiniHexDungeon dungeon;
        internal GameEventManager events;
        internal JObject view;
        internal ContinueFSM storedCheck;
    }
    static bool EntryConsumed(ContinueFSM cfsm){return cfsm==null||cfsm.m_WaitCount==0;}
    static JObject EntryContinuation(ContinueFSM cfsm)
    {
        return new JObject{{"present",cfsm!=null},{"id",cfsm==null?-1:cfsm.m_ID},{"callerId",cfsm==null?-1:cfsm.m_CallerID},
            {"waitCount",cfsm==null?-1:cfsm.m_WaitCount},{"waitClients",cfsm==null?"unavailable":cfsm.m_WaitClients.ToString()},{"local",cfsm!=null&&cfsm.m_IsLocal}};
    }
    EntryContext ReadEntryContext()
    {
        StoryContext story=ReadStorySetup();story.view["frame"]=Time.frameCount;story.view["session"]=sessionId;story.view["ok"]=true;GameLogic logic=GameLogic.Instance;FTKHub hub=FTKHub.Instance;
        EntryContext c=new EntryContext{cow=story.cow,events=GameEventManager.Instance};
        GameDefinition definition=logic.GetGameDef();Movement movement=Movement.Instance;
        if(definition==null||movement==null||movement.m_MovementFSM==null||c.events==null||c.events.m_CheckQuestStatusFSM==null||c.cow==null||c.cow.m_TurnEngage==null)
            throw new InvalidOperationException("Native entry definition/movement/quest FSM unavailable.");
        if(!ReferenceEquals(hub.m_Movement,movement.m_MovementFSM))throw new InvalidOperationException("Native movement owner/FSM identity mismatch.");
        c.dungeon=(MiniHexDungeon)typeof(FTKHex).GetMethod("GetSpecificDungeon",Members,null,new[]{typeof(FTK_dungeonEncounter.ID),typeof(FTK_realm.ID),typeof(int)},null)
            .Invoke(Instance(typeof(FTKHex)),new object[]{FTK_dungeonEncounter.ID.FloodedCrypt,FTK_realm.ID.None,-1});
        if(c.dungeon==null||c.dungeon.m_HexLand==null)throw new InvalidOperationException("Exact FloodedCrypt target unavailable.");
        c.storedCheck=(ContinueFSM)typeof(GameEventManager).GetField("m_CheckQuestStatusCFSM",Members).GetValue(c.events);
        ContinueFSM setup=(ContinueFSM)typeof(GameEventManager).GetField("m_ContinueFSM",Members).GetValue(c.events);
        QuestLogicBase quest=c.events.GetCurrentQuest();
        IDictionary runtime=typeof(GameLogic).GetProperty("_fullQuestTable",Members).GetValue(logic,null)as IDictionary;
        IDictionary definitions=typeof(GameDefinition).GetProperty("m_QuestLookup",Members).GetValue(definition,null)as IDictionary;
        if(runtime==null||definitions==null)throw new InvalidOperationException("Existing native quest registries unavailable.");
        QuestDefBase expected=definitions.Contains("ftkmf_hollowmire_crypt")?definitions["ftkmf_hollowmire_crypt"]as QuestDefBase:null;
        bool exactDef=quest!=null&&expected!=null&&ReferenceEquals(quest.m_QuestDef,expected)&&expected.GetType().Name=="DungeonQuestDef"&&
            expected.m_StoryQuestID=="ftkmf_hollowmire_crypt"&&quest.m_Type==QuestLogicBase.Type.Dungeon&&
            Convert.ToString(expected.GetType().GetField("m_DungeonID",Members).GetValue(expected))=="FloodedCrypt";
        var currentSub=FTKUtil.GetCurrentSubFSM(movement.m_MovementFSM);
        c.view=new JObject{{"root",root},{"coreIdentity",PreviewCoreIdentity()},{"gameAssembly",story.view["gameAssembly"]},
            {"adventure",definition.m_SaveFileName},{"dungeonKey",c.dungeon.m_ID.ToString()},
            {"heroInstanceId",c.cow.GetInstanceID()},{"dungeonInstanceId",c.dungeon.GetInstanceID()},{"hexInstanceId",c.dungeon.m_HexLand.GetInstanceID()},
            {"movementInstanceId",movement.m_MovementFSM.GetInstanceID()},{"gameDefinitionIdentity",RuntimeHelpers.GetHashCode(definition)},
            {"currentHexInstanceId",c.cow.m_HexLand==null?0:c.cow.m_HexLand.GetInstanceID()},{"atTargetHex",c.cow.m_HexLand==c.dungeon.m_HexLand},
            {"targetOwnsHex",c.dungeon.m_HexLand.m_POI==c.dungeon},{"locked",c.dungeon.m_Locked},{"deactivated",c.dungeon.m_Deactivated},
            {"livingSingleParty",hub.m_CharacterOverworlds!=null&&hub.m_CharacterOverworlds.Count==1&&(bool)story.view["livingOwnedParty"]},
            {"outsideCombat",story.view["outsideCombat"]},{"outsideDungeon",story.view["outsideDungeon"]},{"inSession",story.view["inSession"]},
            {"moving",c.cow.m_IsMoving},{"syncWalkingCow",(int)typeof(CharacterOverworld).GetField("m_SyncWalkingCow",Members).GetValue(c.cow)},
            {"waiting",logic.IsWaiting()},{"movementEnabled",movement.m_MovementFSM.enabled},{"movementState",movement.m_MovementFSM.ActiveStateName},
            {"currentSubFsmName",currentSub==null?null:currentSub.Name},{"currentSubFsmState",currentSub==null?null:currentSub.ActiveStateName},
            {"stopAtHexCheckActive",FTKUtil.FSMIsActiveState(hub.m_Movement,new[]{"OnStopAtHex"},"CheckQuest")},
            {"turnQuestCheckActive",c.cow.m_TurnEngage.enabled&&c.cow.m_TurnEngage.ActiveStateName.IndexOf("Check Quest Status",StringComparison.Ordinal)>=0},
            {"questCheckEnabled",c.events.m_CheckQuestStatusFSM.enabled},{"questCheckState",c.events.m_CheckQuestStatusFSM.ActiveStateName},
            {"storedCheck",EntryContinuation(c.storedCheck)},{"storedSetup",EntryContinuation(setup)},
            {"storedCheckConsumed",EntryConsumed(c.storedCheck)},{"storedSetupConsumed",EntryConsumed(setup)},
            {"storyComplete",story.view["complete"]},{"story",story.view},
            {"currentQuestId",quest==null?new JValue((object)null):new JValue(quest.m_QuestID)},
            {"currentQuestDefinition",quest==null||quest.m_QuestDef==null?null:quest.m_QuestDef.m_StoryQuestID},
            {"expectedQuestRegistered",quest!=null&&StorySubmissionPolicy.RegisteredQuest(runtime,quest.m_QuestID,quest)},
            {"expectedQuestDefinition",exactDef},{"expectedQuestDestination",quest!=null&&quest.m_Destination==c.dungeon.m_HexLand.GetHexLandID()}};
        return c;
    }
    void CheckEntryTicket(EntryContext c,string id)
    {
        EntryTicket t=entryTicket;
        if(t==null||t.id!=id||!t.valid)throw new InvalidOperationException("Missing/invalid entry preparation ticket; never retry uncertain operations.");
        try
        {
            if(t.session!=sessionId||t.ownedRoot!=root||!JToken.DeepEquals(t.core,PreviewCoreIdentity()))throw new InvalidOperationException("Entry session/root/Core pins changed.");
            EntryPreparationPolicy.Match(t.pins,c.view);EntryPreparationPolicy.Scope(c.view);
        }
        catch{t.valid=false;t.phase="invalid-pins-or-scope";throw;}
    }
    JObject EntryTicketView(EntryContext c)
    {
        EntryTicket t=entryTicket;if(t==null)return null;
        bool registered=false;
        if(t.continuation!=null)
        {
            ContinueFSMManager manager=ContinueFSMManager.Instance;
            if(manager==null||manager.m_ContinueFSMs==null)throw new InvalidOperationException("Native continuation manager unavailable.");
            var all=manager.m_ContinueFSMs;
            registered=all.ContainsKey(t.continuation.m_CallerID)&&all[t.continuation.m_CallerID].ContainsKey(t.continuation.m_ID)&&ReferenceEquals(all[t.continuation.m_CallerID][t.continuation.m_ID],t.continuation);
        }
        bool same=t.continuation!=null&&ReferenceEquals(c.storedCheck,t.continuation);
        bool ready=false;string waiting=null;
        if(t.valid&&t.discoverSubmitted&&t.phase!="discover-uncertain")
        {
            try{ready=EntryPreparationPolicy.Ready(c.view,t.callbacks,t.continuation==null?-1:t.continuation.m_WaitCount,same);}
            catch(InvalidOperationException e){waiting=e.Message;}
        }
        if(!ready){t.quietSince=-1;t.quietFrame=-1;}
        else if(t.quietSince<0){t.quietSince=Time.realtimeSinceStartup;t.quietFrame=Time.frameCount;}
        bool stable=ready&&Time.frameCount>t.quietFrame&&Time.realtimeSinceStartup-t.quietSince>=2f;
        return new JObject{{"ticketId",t.id},{"phase",t.phase},{"valid",t.valid},{"discoverSubmitted",t.discoverSubmitted},
            {"callbackCount",t.callbacks},{"ignoredCallbackCount",t.ignoredCallbacks},{"callbackFrame",t.callbackFrame},{"callbackRealtime",t.callbackTime},
            {"continuation",EntryContinuation(t.continuation)},{"sameStoredCheckContinuation",same},{"ownContinuationStillRegistered",registered},
            {"managerResidueNote","Local native Continue may leave its consumed registration; observer never unregisters arbitrary native entries."},
            {"quietSinceRealtime",t.quietSince},{"quietStartFrame",t.quietFrame},{"entryReady",stable},{"waitingReason",waiting}};
    }
    JObject EntryPreparationState(JObject command)
    {
        CatalogKeys(command,"id","session","op","ticketId");EntryContext c=ReadEntryContext();
        if(entryObservationCore==null)entryObservationCore=(JObject)c.view["coreIdentity"].DeepClone();
        else if(!JToken.DeepEquals(entryObservationCore,c.view["coreIdentity"]))throw new InvalidOperationException("Core identity changed since first entry observation.");
        if(command["ticketId"]!=null)CheckEntryTicket(c,Str(command,"ticketId"));
        else if(entryTicket!=null)CheckEntryTicket(c,entryTicket.id);
        bool quiet=false;string reason=null;
        try{EntryPreparationPolicy.Quiescent(c.view);quiet=true;}catch(InvalidOperationException e){reason=e.Message;}
        c.view["positionEligible"]=quiet&&entryTicket==null;
        c.view["discoveryEligible"]=quiet&&entryTicket!=null&&entryTicket.valid&&entryTicket.phase=="position-submitted"&&!entryTicket.discoverSubmitted&&(bool)c.view["atTargetHex"];
        c.view["eligibilityReason"]=reason;
        c.view["ticket"]=EntryTicketView(c);c.view["ok"]=true;c.view["provenance"]="Read-only native pre-entry discovery observation; entry is never performed by this helper.";return c.view;
    }
    JObject EntryPosition(JObject command)
    {
        CatalogKeys(command,"id","session","op","heroInstanceId","dungeonInstanceId","hexInstanceId","movementInstanceId","gameDefinitionIdentity");
        EntryContext c=ReadEntryContext();
        if(entryObservationCore==null||!JToken.DeepEquals(entryObservationCore,c.view["coreIdentity"]))throw new InvalidOperationException("Read and pin entry-preparation-state before positioning; Core must remain unchanged.");
        EntryPreparationPolicy.Match(command,c.view);EntryPreparationPolicy.Quiescent(c.view);
        if(entryTicket!=null)throw new InvalidOperationException("This session already submitted an entry position; never repeat uncertain positioning.");
        EntryTicket t=new EntryTicket{id=Str(command,"id"),session=sessionId,ownedRoot=root,pins=(JObject)c.view.DeepClone(),core=PreviewCoreIdentity()};entryTicket=t;t.ClaimPosition();
        try{c.cow.SnapTo(c.dungeon.m_HexLand,false,true);t.PositionSubmitted();}
        catch{t.phase="position-uncertain";throw;}
        return new JObject{{"ok",true},{"status","native-position-submitted"},{"ticketId",t.id},{"before",c.view},{"note","One native SnapTo submitted. Poll and service existing exact story pages; no dungeon entry or quest check yet."}};
    }
    JObject EntryDiscover(JObject command)
    {
        CatalogKeys(command,"id","session","op","ticketId");EntryContext c=ReadEntryContext();CheckEntryTicket(c,Str(command,"ticketId"));
        EntryPreparationPolicy.Quiescent(c.view);EntryPreparationPolicy.AtTarget(c.view);EntryTicket t=entryTicket;
        if(t.phase!="position-submitted"||t.discoverSubmitted)throw new InvalidOperationException("Native discovery already submitted or positioning uncertain; no retry.");
        t.ClaimDiscovery();
        try
        {
            t.continuation=new ContinueFSM(t.Completed,ContinueFSM.WaitClients.Self);
            ContinueFSM cfsm=t.continuation;
            if(cfsm.m_ID<0||cfsm.m_WaitCount!=1||cfsm.m_WaitClients!=ContinueFSM.WaitClients.Self||!cfsm.m_IsLocal||
                !StorySubmissionPolicy.ExactAction(typeof(ContinueFSM).GetField("m_CompleteDelegate",Members).GetValue(cfsm),t,"Completed"))
                throw new InvalidOperationException("Exact own local Self/count1 continuation unavailable.");
            object player=typeof(MessageCoordinator).Assembly.GetType("PhotonNetwork",true).GetProperty("player",Statics).GetValue(null,null);
            if(player==null||cfsm.m_CallerID!=(int)player.GetType().GetProperty("ID",Members).GetValue(player,null))throw new InvalidOperationException("Native discovery caller changed.");
            var manager=ContinueFSMManager.Instance;
            if(manager==null||!manager.m_ContinueFSMs.ContainsKey(cfsm.m_CallerID)||!manager.m_ContinueFSMs[cfsm.m_CallerID].ContainsKey(cfsm.m_ID)||!ReferenceEquals(manager.m_ContinueFSMs[cfsm.m_CallerID][cfsm.m_ID],cfsm))throw new InvalidOperationException("Own native discovery continuation registration missing.");
            // Callback only stamps its managed ticket. It cannot enter or mutate gameplay reentrantly.
            c.cow.CheckDiscoverHex(cfsm);t.DiscoverySubmitted();
        }
        catch{t.phase="discover-uncertain";throw;}
        return new JObject{{"ok",true},{"status","native-discovery-submitted"},{"ticketId",t.id},{"before",c.view},{"continuation",EntryContinuation(t.continuation)},
            {"callbackCount",t.callbacks},{"note","Exactly one native CheckDiscoverHex submitted; completion/quest advancement/entry readiness must be observed separately."}};
    }
}
