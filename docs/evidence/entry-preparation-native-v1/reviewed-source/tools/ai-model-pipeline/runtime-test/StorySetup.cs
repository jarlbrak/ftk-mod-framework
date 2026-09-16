using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    readonly HashSet<string> submittedStoryPages=new HashSet<string>();
    sealed class StoryContext
    {
        public MessageCoordinator coordinator;public MessagePresenter presenter;public CharacterOverworld cow;
        public FTKClickAnywhere click;public uiPortraitMessageHud portrait;public uiQuestConfirmHud confirm;public JObject view;
    }
    static JObject StoryPanel(Component hud,Component panel,bool fully,bool clickable,bool closeOnOkay)
    {
        return new JObject{{"componentId",hud==null?0:hud.GetInstanceID()},{"panelId",panel==null?0:panel.GetInstanceID()},
            {"hudActive",hud!=null && hud.gameObject.activeInHierarchy},{"activeSelf",panel!=null && panel.gameObject.activeSelf},
            {"activeInHierarchy",panel!=null && panel.gameObject.activeInHierarchy},{"fullyOpened",fully},{"clickable",clickable},{"closeOnOkay",closeOnOkay}};
    }
    static JObject StoryFocus(FTKClickAnywhere click,Component hud)
    {
        object continuation=click==null?null:typeof(FTKClickAnywhere).GetField("m_Continue",Members).GetValue(click);
        object callback=continuation==null?null:typeof(ContinueFSM).GetField("m_CompleteDelegate",Members).GetValue(continuation);
        bool exact=continuation!=null && hud!=null && StorySubmissionPolicy.ExactAction(callback,hud,"UseOkayButton") &&
            typeof(ContinueFSM).GetField("m_CompleteDelegate_1",Members).GetValue(continuation)==null &&
            typeof(ContinueFSM).GetField("m_FSM",Members).GetValue(continuation)==null &&
            typeof(ContinueFSM).GetField("m_ContinueObject",Members).GetValue(continuation)==null;
        ContinueFSM cfsm=continuation as ContinueFSM;
        object player=typeof(MessageCoordinator).Assembly.GetType("PhotonNetwork",true).GetProperty("player",Statics).GetValue(null,null);
        if(player==null)throw new InvalidOperationException("Native story input caller unavailable.");
        int localPlayerId=(int)player.GetType().GetProperty("ID",Members).GetValue(player,null);
        bool dispatch=cfsm!=null && cfsm.m_ID>=0 && cfsm.m_WaitClients==ContinueFSM.WaitClients.Self && cfsm.m_WaitCount==1 &&
            (cfsm.m_CallerID==localPlayerId || (localPlayerId==-1 && cfsm.m_IsLocal));
        return new JObject{{"instanceId",click==null?0:click.GetInstanceID()},{"active",click!=null && click.enabled && click.gameObject.activeInHierarchy},
            {"currentFocus",click!=null && FTKInput.Instance!=null && ReferenceEquals(FTKInput.Instance.m_CurrentInputFocus,click) && click.m_HasInputFocus},
            {"canClose",click!=null && (bool)typeof(FTKClickAnywhere).GetField("m_CanClose",Members).GetValue(click)},
            {"exactCallback",exact},{"localDispatchReady",dispatch},{"localPlayerId",localPlayerId},{"waitCount",cfsm==null?-1:cfsm.m_WaitCount},{"waitClients",cfsm==null?"unavailable":cfsm.m_WaitClients.ToString()},{"continuationId",cfsm==null?-1:cfsm.m_ID},{"continuationCallerId",cfsm==null?-1:cfsm.m_CallerID},
            {"callbackTargetId",callback is Action && ((Action)callback).Target is Component?((Component)((Action)callback).Target).GetInstanceID():0},
            {"callbackMethod",callback is Action?((Action)callback).Method.Name:null}};
    }
    static string StoryContentHash(MessagePresenter presenter)
    {
        if(presenter.m_Messages==null)return null;
        if(presenter.m_Messages.Count>256)throw new InvalidOperationException("Native story content exceeds256 pages.");
        JArray entries=new JArray();int chars=0;
        foreach(MessagePresenter.PresentEntry entry in presenter.m_Messages)
        {
            if(entry==null)throw new InvalidOperationException("Missing native story page.");
            chars+=(entry.m_Message??"").Length+(entry.m_UserNPC??"").Length;
            if(chars>262144)throw new InvalidOperationException("Native story content exceeds bounded262144 characters.");
            entries.Add(new JObject{{"talker",(int)entry.m_Talker},{"userNpc",entry.m_UserNPC},{"message",entry.m_Message}});
        }
        using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(entries.ToString()))).Replace("-","").ToLowerInvariant();
    }
    static string StoryPhase(StoryContext context)
    {
        object callback=typeof(uiPortraitMessageHud).GetField("m_OnMessageClosed",Members).GetValue(context.portrait);
        foreach(string name in new[]{"DeliverQuestMessageClosedPart","DeliverStartQuestMsgPartClosed","DeliverMultiQuestMsgClosed","DeliverSubQuestMsgClosed"})
            if(StorySubmissionPolicy.ExactAction(callback,context.presenter,name))return name;
        return "unavailable";
    }
    StoryContext ReadStorySetup()
    {
        CatalogNoLinks(root);RequireSinglePlayer();
        JObject assembly=ScaleIdentity(typeof(MessageCoordinator).Assembly);
        if((string)assembly["assemblyFileSha256"]!=KrakenControllerGameAssembly)throw new InvalidOperationException("Story source audit requires exact game assembly.");
        StoryContext context=new StoryContext{coordinator=MessageCoordinator.Instance,presenter=MessagePresenter.Instance};
        FTKUI ui=FTKUI.Instance;GameLogic logic=GameLogic.Instance;GameFlow flow=GameFlow.Instance;FTKHub hub=FTKHub.Instance;
        if(context.coordinator==null || context.presenter==null || ui==null || logic==null || flow==null || hub==null)
            throw new InvalidOperationException("Native story coordinator/presenter/UI/world unavailable.");
        context.click=FTKClickAnywhere.Instance;context.portrait=ui.m_PortraitMessage;context.confirm=ui.m_QuestConfirm;context.cow=logic.GetCurrentCOW();
        MessageCoordinator.MessageInstance current=typeof(MessageCoordinator).GetField("m_CurrentMessageInstances",Members).GetValue(context.coordinator)as MessageCoordinator.MessageInstance;
        ICollection queue=typeof(MessageCoordinator).GetField("m_RequestQueue",Members).GetValue(context.coordinator)as ICollection;
        if(queue==null || queue.Count>64)throw new InvalidOperationException("Native story queue unavailable or outside bound64.");
        bool pendingContinuation=typeof(MessageCoordinator).GetField("m_CurrentCFSM",Members).GetValue(context.coordinator)!=null;
        QuestLogicBase quest=typeof(MessagePresenter).GetField("m_Quest",Members).GetValue(context.presenter)as QuestLogicBase;
        IDictionary questTable=typeof(GameLogic).GetProperty("_fullQuestTable",Members).GetValue(logic,null)as IDictionary;
        bool registeredQuest=quest!=null && StorySubmissionPolicy.RegisteredQuest(questTable,quest.m_QuestID,quest);
        QuestLogicBase confirmQuest=context.confirm==null?null:typeof(uiQuestConfirmHud).GetField("m_Quest",Members).GetValue(context.confirm)as QuestLogicBase;
        JObject portrait=StoryPanel(context.portrait,context.portrait==null?null:context.portrait.m_MessagePanel,
            context.portrait!=null && context.portrait.m_IsFullyOpened,context.portrait!=null && (bool)typeof(uiPortraitMessageHud).GetField("m_Clickable",Members).GetValue(context.portrait),context.portrait!=null && context.portrait.m_CloseOnOkay);
        JObject confirm=StoryPanel(context.confirm,context.confirm==null?null:context.confirm.m_MessagePanel,
            context.confirm!=null && context.confirm.m_IsFullyOpened,context.confirm!=null && (bool)typeof(uiQuestConfirmHud).GetField("m_ClickEnable",Members).GetValue(context.confirm),true);
        portrait["focus"]=StoryFocus(context.click,context.portrait);confirm["focus"]=StoryFocus(context.click,context.confirm);
        confirm["closeOnOkay"]=new JValue((object)null);
        confirm["samePresenterQuest"]=quest!=null && confirmQuest==quest;
        confirm["continuationPresent"]=context.confirm!=null && typeof(uiQuestConfirmHud).GetField("m_ContinueFSM",Members).GetValue(context.confirm)!=null;
        JObject bridge=JObject.FromObject(CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Agent.StateReader",true).GetMethod("ReadState",Statics,null,Type.EmptyTypes,null).Invoke(null,null));
        bool outside=Instance(typeof(EncounterSession))!=null && Instance(typeof(EncounterSessionMC))!=null;try{RequireOutsideCombat();}catch(InvalidOperationException){outside=false;}
        bool liveParty=hub.m_CharacterOverworlds!=null && hub.m_CharacterOverworlds.Count>0,ownedCow=false;
        if(hub.m_CharacterOverworlds!=null)foreach(CharacterOverworld member in hub.m_CharacterOverworlds)
        {liveParty&=member!=null && member.m_CharacterStats!=null && member.m_CharacterStats.m_HealthCurrent>0;if(member==context.cow)ownedCow=true;}
        JArray fsms=new JArray{FsmView(typeof(MessagePresenter).GetField("m_MessageLoopFSM",Members).GetValue(context.presenter)),FsmView(typeof(MessagePresenter).GetField("m_StartQuestMsgLoopFSM",Members).GetValue(context.presenter)),FsmView(typeof(MessagePresenter).GetField("m_StartMultiQuestMsgLoopFSM",Members).GetValue(context.presenter))};
        string contentHash=StoryContentHash(context.presenter);
        context.view=new JObject{{"root",root},{"timeScale",Time.timeScale},{"provenance","read-only native story setup state"},{"coreIdentity",PreviewCoreIdentity()},{"gameAssembly",assembly},
            {"coordinatorId",context.coordinator.GetInstanceID()},{"messagePresent",current!=null},{"messageType",current==null?"None":current.m_MessageType.ToString()},
            {"messageId",current==null?-1:current.m_ID},{"messageClosed",current!=null && current.m_Closed},{"queueCount",queue.Count},{"currentContinuationPresent",pendingContinuation},
            {"presenterId",context.presenter.GetInstanceID()},{"presenterMessageId",context.presenter.m_MessageInstanceID},{"pageIndex",context.presenter.m_CurrentPageIndex},
            {"pageCount",context.presenter.m_Messages==null?0:context.presenter.m_Messages.Count},{"questId",quest==null?new JValue((object)null):new JValue(quest.m_QuestID)},{"questPresent",quest!=null},{"questRegisteredReference",registeredQuest},
            {"phase",context.portrait==null?"unavailable":StoryPhase(context)},{"contentAvailable",contentHash!=null},{"contentSha256",contentHash==null?new JValue((object)null):new JValue(contentHash)},
            {"presenterFsms",fsms},{"portrait",portrait},{"questConfirm",confirm},
            {"heroInstanceId",context.cow==null?0:context.cow.GetInstanceID()},{"livingOwnedParty",liveParty && ownedCow},
            {"outsideCombat",outside},{"outsideDungeon",flow.m_DungeonEntered==null && !hub.AnyPlayersInDungeon()},
            {"inSession",bridge["inSession"]},{"signals",bridge["signals"]},{"cameraWait",new JObject{{"available",false},{"note","No authoritative coroutine-wait flag. Page/FSM/panel observations describe pending work; do not infer a camera waiter."}}}};
        StorySubmissionPolicy.Classify(context.view);
        return context;
    }
    JObject StorySetupState(JObject command)
    {CatalogKeys(command,"id","session","op");JObject result=ReadStorySetup().view;result["ok"]=true;return result;}
    JObject SubmitStorySetup(JObject command)
    {
        CatalogKeys(command,"id","session","op","surface","coordinatorId","messageId","presenterId","pageIndex","questId","heroInstanceId","componentId","panelId","phase","contentSha256","clickInstanceId","clickContinuationId");
        StoryContext context=ReadStorySetup();JObject before=context.view;
        string key=StorySubmissionPolicy.Validate(command,before);
        if(submittedStoryPages.Count>=64 || !submittedStoryPages.Add(key))throw new InvalidOperationException("Story page already submitted or bounded64 submission limit reached; never retry uncertain actions.");
        // Record before the native call. An exception must never authorize another submission.
        context.click.OnClick();
        return new JObject{{"ok",true},{"status","native-story-page-submitted"},{"submissionKey",key},{"surface",command["surface"]},
            {"before",before},{"note","One native FTKClickAnywhere.OnClick call submitted to the exact UseOkayButton callback. Delayed closure/page advancement/completion is not yet observed; never retry this page."}};
    }
}
