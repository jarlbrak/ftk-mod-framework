using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using GridEditor;
using HarmonyLib;
public sealed partial class RuntimeModelTest
{
    sealed class PaidContext {internal uiBattleStanceButtons stance;internal SlotSystemBase slots;internal FTK_weaponStats2 weaponRow;internal JObject view;}
    sealed class PaidOperation {internal string id,status="submitted",error;internal PaidContext source;internal JObject baseline,before,after;internal int callbacks;internal JArray callbackEvidence=new JArray();}
    sealed class PaidCallback {internal PaidOperation operation;internal JObject before;internal int index;internal string previousStatus,error;}
    JObject paidTicket;PaidContext paidTicketContext;readonly HashSet<string> paidClaims=new HashSet<string>();PaidOperation paidOperation;
    static RuntimeModelTest paidObserver;bool paidHooks;
    PaidContext ReadPaidFocus(bool full)
    {
        JObject pins=full?TrapPins():null;
        FTKHub hub=FTKHub.Instance;GameLogic logic=GameLogic.Instance;EncounterSession es=EncounterSession.Instance;EncounterSessionMC mc=EncounterSessionMC.Instance;
        FTKUI ui=FTKUI.Instance;GameFlow flow=GameFlow.Instance;FTKInput input=FTKInput.Instance;
        if(hub==null||logic==null||es==null||mc==null||ui==null||flow==null||input==null||hub.m_CharacterOverworlds==null||hub.m_CharacterOverworlds.Count!=1)throw new InvalidOperationException("One actual SP combat hero required.");
        CharacterOverworld cow=hub.m_CharacterOverworlds[0];MiniHexDungeon dungeon=flow.m_DungeonEntered;
        uiBattleStanceButtons stance=ui.m_BattleStanceButtons;
        if(cow==null||dungeon==null||stance==null||cow.m_CurrentDummy==null||cow.m_CharacterStats==null||cow.m_UIPlayMainHud==null||ui.m_PlayerSlots==null)throw new InvalidOperationException("Native paid-focus surfaces missing.");
        CharacterDummy dummy=cow.m_CurrentDummy;CharacterEventListener cel=dummy.m_EventListener;
        EnemyDummy target=es.GetCurrentEnemy();uiBattleButton button=stance.m_AttackButton;
        if(cel==null||target==null||target.m_EventListener==null||button==null||dummy.m_CharacterDummyFSM==null)throw new InvalidOperationException("Exact acting and target identities required.");
        uiBattleStanceButtons.CombatActionProfile profile=stance.m_CombatActionProfile;
        FTK_weaponStats2 weaponRow=FTK_weaponStats2DB.GetDB().GetEntry(cow.m_WeaponID);
        if(weaponRow==null||cel.m_Weapon==null)throw new InvalidOperationException("Exact native weapon instance/table unavailable.");
        bool mapped=es.m_Dummies.ContainsKey(cow.m_FTKPlayerID)&&es.m_Dummies[cow.m_FTKPlayerID]==dummy&&es.m_EnemyDummies.ContainsKey(target.FID)&&es.m_EnemyDummies[target.FID]==target;
        bool scope=GameLogic.Instance.IsSinglePlayer()&&es.m_IsInCombat&&(bool)TrapField(mc,"m_IsInCombat")&&cow.IsOwner&&cow==logic.GetCurrentCombatCOW()&&stance.CombatCow==cow
            &&dummy.m_CharacterOverworld==cow&&cel.m_Dummy==dummy&&target.m_EventListener.m_Dummy==target&&mapped&&dummy.m_IsAlive&&cow.m_CharacterStats.m_HealthCurrent>0&&target.m_IsAlive&&target.m_CurrentHealth>0
            &&cow.GetPOI()==dungeon&&cow.m_UIPlayMainHud.m_Cow==cow&&mc.m_FightOrder!=null&&mc.m_FightOrder.Count>0&&mc.m_FightOrder[0].m_Pid==cow.m_FTKPlayerID&&mc.m_PlayerAttacker==cow.m_FTKPlayerID;
        bool noModal=false;
        if(full)
        {
            if(busy||(spawnCapture!=null&&!spawnCapture.terminal))throw new InvalidOperationException("Capture/arrival pending.");
            MessageCoordinator coordinator=MessageCoordinator.Instance;
            if(coordinator==null||ui.m_PortraitMessage==null||ui.m_QuestConfirm==null||uiOptionsMenu.Instance==null||uiPlayerInventory.Instance==null)throw new InvalidOperationException("Modal authorities unavailable.");
            noModal=Time.timeScale>0&&!input.m_WaitingForPopup&&!uiOptionsMenu.Instance.m_Showing&&!uiPlayerInventory.Instance.m_IsShowing
                &&!TrapPanelActive(TrapField(ui.m_GlobalMessage,"m_MessagePanel"))&&!TrapPanelActive(TrapField(ui.m_GlobalMessage,"m_ChoiceButtonPanel"))
                &&!TrapPanelActive(ui.m_PortraitMessage.m_MessagePanel)&&!TrapPanelActive(ui.m_QuestConfirm.m_MessagePanel)&&TrapField(coordinator,"m_CurrentMessageInstances")==null;
        }
        int turn=(int)TrapField(mc,"m_CombatTurnCount");
        JObject identity=new JObject{{"root",root},{"session",sessionId},{"dungeonId",dungeon.GetInstanceID()},{"level",dungeon.m_Level},{"room",dungeon.m_RoomIndex},
            {"esId",es.GetInstanceID()},{"mcId",mc.GetInstanceID()},{"turn",turn},{"heroId",cow.GetInstanceID()},{"heroFid",SpawnFid(cow.m_FTKPlayerID)},
            {"dummyId",dummy.GetInstanceID()},{"celId",cel.GetInstanceID()},{"targetId",target.GetInstanceID()},{"targetCelId",target.m_EventListener.GetInstanceID()},{"targetFid",SpawnFid(target.FID)},
            {"stanceId",stance.GetInstanceID()},{"buttonId",button.GetInstanceID()},{"profileButtonId",profile.m_Button==null?0:profile.m_Button.GetInstanceID()},
            {"weaponId",cow.m_WeaponID.ToString()},{"weaponInstanceId",cel.m_Weapon.GetInstanceID()},{"weaponRowKey",weaponRow.m_ID},{"weaponSlots",weaponRow._slots},{"weaponSkill",weaponRow._skilltest.ToString()},{"weaponNoFocus",weaponRow.m_NoFocus},{"weaponNoRegularAttack",weaponRow.m_NoRegularAttack},{"slotsId",ui.m_PlayerSlots.GetInstanceID()},{"hudId",cow.m_UIPlayMainHud.GetInstanceID()},{"slotCount",profile.m_Slots},{"showSlot",profile.m_ShowSlot},{"noFocus",profile.m_NoFocus}};
        bool normal=!weaponRow.m_NoRegularAttack&&profile.m_Slots==weaponRow._slots&&profile.m_ShowSlot==weaponRow._skilltest.ToString()&&profile.m_NoFocus==weaponRow.m_NoFocus&&button.m_ButtonType==uiBattleButton.BattleButtonType.attack&&button.m_Owner==stance&&profile.m_Button==button&&button.m_CanUse&&button.enabled&&button.gameObject.activeInHierarchy
            &&button.transform.IsChildOf(stance.transform)&&button.m_Button!=null&&button.m_Button.enabled&&button.m_Button.IsInteractable()&&ui.m_PlayerSlots.m_CheatAttack==SlotControl.AttackCheatType.None;
        bool inputReady=stance.m_InputFocus!=null&&input.m_CurrentInputFocus==stance.m_InputFocus&&stance.m_InputFocus.m_HasInputFocus
            &&EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject==button.gameObject;
        bool stanceReady=scope&&turn>0&&!stance.m_Hidden&&stance.enabled&&stance.gameObject.activeInHierarchy&&stance.m_DisplayRoot!=null&&stance.m_DisplayRoot.activeInHierarchy
            &&dummy.m_CharacterDummyFSM.enabled&&dummy.m_CharacterDummyFSM.ActiveStateName=="Wait For Stance";
        int available=cow.m_CharacterStats.m_FocusPoints,spent=cow.m_CharacterStats.SpentFocus;
        bool canPay=cow.m_CharacterStats.CanFocus()&&available>0&&spent>=0&&profile.m_Slots>spent&&profile.m_Slots<=16&&!profile.m_NoFocus&&!stance.m_Focusing&&!stance.m_FocusInterrupt&&cow.m_UIPlayMainHud.m_FocusAnimatingCount==0;
        return new PaidContext{stance=stance,slots=ui.m_PlayerSlots,weaponRow=weaponRow,view=new JObject{{"identity",identity},{"pins",pins},{"scope",scope},{"noModal",noModal},{"normalAttack",normal},{"inputReady",inputReady},{"stanceReady",stanceReady},{"canPay",canPay},
            {"initialized",stance.m_Initialized},{"availableFocus",available},{"spentFocus",spent},{"focusing",stance.m_Focusing},{"interrupt",stance.m_FocusInterrupt},{"animationCount",cow.m_UIPlayMainHud.m_FocusAnimatingCount},{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup}}};
    }
    void InstallPaidFocusHook()
    {
        if(paidHooks)return;
        MethodInfo method=typeof(uiBattleStanceButtons).GetMethod("FocusSlotAnimateFinish",Members,null,new[]{typeof(SlotSystemBase)},null);
        if(method==null)throw new InvalidOperationException("Native paid-focus completion absent.");
        new Harmony("com.ftkmf.runtime-model-test.paid-focus").Patch(method,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PaidPrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("PaidFinalizer",Statics)),null);
        paidHooks=true;paidObserver=this;
    }
    JObject PaidCallbackSnapshot(PaidOperation operation)
    {
        PaidContext context=ReadPaidFocus(false);
        PaidFocusPolicy.SameWeaponRow(operation.source.weaponRow,context.weaponRow);
        return context.view;
    }
    static void PaidPrefix(uiBattleStanceButtons __instance,SlotSystemBase _slotSystem,ref PaidCallback __state)
    {
        RuntimeModelTest host=paidObserver;if(host==null)return;PaidOperation operation=host.paidOperation;
        if(operation==null||__instance!=operation.source.stance)return;
        PaidCallback callback=new PaidCallback{operation=operation,index=++operation.callbacks,previousStatus=operation.status};__state=callback;
        if(callback.index>1)operation.status="duplicate-native-callback";
        try
        {
            if(_slotSystem!=operation.source.slots)throw new InvalidOperationException("Unexpected native focus completion slot.");
            callback.before=host.PaidCallbackSnapshot(operation);
            if(callback.index==1)operation.before=callback.before;
        }
        catch(Exception error){callback.error=error.ToString();operation.error=callback.error;if(callback.index==1)operation.status="callback-observation-error";}
    }
    static Exception PaidFinalizer(uiBattleStanceButtons __instance,Exception __exception,PaidCallback __state)
    {
        if(__state==null)return __exception;
        PaidOperation operation=__state.operation;JObject after=null;string classification="callback-observation-error";
        try
        {
            after=paidObserver.PaidCallbackSnapshot(operation);
            if(__state.before!=null)classification=PaidFocusPolicy.Payment(operation.baseline,__state.before,after,__exception!=null);
            if(__state.index==1)operation.after=after;

            if(__exception!=null)operation.error=__exception.ToString();
        }
        catch(Exception error){operation.error=error.ToString();__state.error=operation.error;}
        // Uncertain submission remains uncertain; any duplicate is permanently ineligible.
        operation.status=PaidFocusPolicy.CompletionStatus(__state.previousStatus,operation.callbacks,classification,__state.error!=null);
        if(operation.callbackEvidence.Count<8)operation.callbackEvidence.Add(new JObject{{"index",__state.index},{"previousStatus",__state.previousStatus},{"classification",classification},
            {"before",__state.before},{"after",after},{"observationError",__state.error},{"nativeException",__exception==null?null:__exception.ToString()}});
        return __exception;
    }
    JObject PaidOperationView()
    {return paidOperation==null?null:new JObject{{"submissionId",paidOperation.id},{"status",paidOperation.status},{"error",paidOperation.error},{"callbackCount",paidOperation.callbacks},{"callbacks",paidOperation.callbackEvidence.DeepClone()},{"callbackEvidenceTruncated",paidOperation.callbacks>8},
        {"beforeSubmission",paidOperation.baseline.DeepClone()},{"callbackBefore",paidOperation.before==null?null:paidOperation.before.DeepClone()},{"callbackAfter",paidOperation.after==null?null:paidOperation.after.DeepClone()},
        {"claimRetained",true},{"note","Payment completion is historical. Current animation/focus state may later change or refund; no automatic attack or retry."}};}
    JObject PaidFocusState(JObject command)
    {
        CatalogKeys(command,"id","session","op");paidTicket=null;paidTicketContext=null;
        try
        {
            PaidContext context=ReadPaidFocus(true);JObject view=context.view;
            if(paidOperation!=null&&!JToken.DeepEquals(paidOperation.baseline["pins"],view["pins"]))throw new InvalidOperationException("Paid-focus operation binary identity changed; prior callback snapshots are historical only.");
            view["alreadySubmitted"]=paidClaims.Contains(PaidFocusPolicy.Key((JObject)view["identity"]));
            view["ticketId"]=Guid.NewGuid().ToString("N");view["expiresAfterRealtimeSeconds"]=5;view["ok"]=true;
            paidTicket=(JObject)view.DeepClone();paidTicketContext=context;view["operation"]=PaidOperationView();return view;
        }
        catch(Exception error){return new JObject{{"ok",true},{"available",false},{"error",error.ToString()},{"operation",PaidOperationView()},{"frame",Time.frameCount}};}
    }
    JObject PaidFocusSubmit(JObject command)
    {
        CatalogKeys(command,"id","session","op","ticketId","identity");InstallPaidFocusHook();
        PaidContext context=ReadPaidFocus(true);
        PaidFocusPolicy.Validate(command,paidTicket,context.view,Time.frameCount,Time.realtimeSinceStartup);
        PaidFocusPolicy.SameWeaponRow(paidTicketContext==null?null:paidTicketContext.weaponRow,context.weaponRow);
        if(paidOperation!=null&&paidOperation.status!="payment-complete"&&paidOperation.status!="native-payment-interrupted")throw new InvalidOperationException("Prior native focus submission is pending or unproven; read-only observation only, no retry.");
        string key=PaidFocusPolicy.Key((JObject)context.view["identity"]);
        if(paidClaims.Contains(key)||paidClaims.Count>=32)throw new InvalidOperationException("Native turn already submitted or claim bound reached.");
        paidOperation=new PaidOperation{id=Str(command,"id"),source=context,baseline=(JObject)context.view.DeepClone()};paidTicket=null;paidTicketContext=null;
        try{PaidFocusPolicy.Submit(paidClaims,key,delegate{context.stance.FocusSlot(context.stance.m_AttackButton);});}
        catch(Exception error){paidOperation.status="uncertain-native-submission";paidOperation.error=error.ToString();return new JObject{{"ok",false},{"operation",PaidOperationView()}};}
        return new JObject{{"ok",true},{"operation",PaidOperationView()}};
    }
}
