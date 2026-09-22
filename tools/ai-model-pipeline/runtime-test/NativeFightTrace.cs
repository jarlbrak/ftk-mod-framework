using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static RuntimeModelTest nativeFightObserver;
    Harmony nativeFightHarmony;
    readonly List<MethodInfo> nativeFightMethods=new List<MethodInfo>();
    readonly JArray nativeFightRecords=new JArray();
    float nativeFightDeadline;
    string nativeFightTraceId;
    int nativeFightDropped;
    ContinueFSM nativeFightContinuation;
    JToken nativeFightContinuationLast;
    static MethodInfo NativeFightContinuePrefixMethod()
    {return typeof(RuntimeModelTest).GetMethod("NativeFightContinuePrefix",Statics);}
    static MethodInfo NativeFightEventPrefixMethod()
    {return typeof(RuntimeModelTest).GetMethod("NativeFightEventPrefix",Statics);}
    static string NativeFightObjectPath(GameObject value)
    {
        if(value==null)return null;
        string path=value.name;Transform parent=value.transform.parent;int depth=0;
        while(parent!=null && depth++<32){path=parent.name+"/"+path;parent=parent.parent;}
        return path;
    }
    static JArray NativeFightTransitions(FsmTransition[] transitions)
    {
        JArray result=new JArray();
        if(transitions!=null)for(int i=0;i<Math.Min(transitions.Length,64);i++)
        {
            FsmTransition transition=transitions[i];if(transition==null)continue;
            result.Add(new JObject{{"event",transition.FsmEvent==null?null:transition.FsmEvent.Name},{"targetState",transition.ToState}});
        }
        return result;
    }
    static JObject NativeFightFsmState(Fsm fsm)
    {
        if(fsm==null)return null;
        return new JObject{{"name",fsm.Name},{"state",fsm.ActiveStateName},{"initialized",fsm.Initialized},
            {"started",fsm.Started},{"active",fsm.Active},{"finished",fsm.Finished},
            {"ownerEnabled",fsm.Owner==null?new JValue((object)null):new JValue(fsm.Owner.enabled)},
            {"ownerId",fsm.Owner==null?new JValue((object)null):new JValue(fsm.Owner.GetInstanceID())},
            {"gameObjectPath",NativeFightObjectPath(fsm.GameObject)},
            {"activeSelf",fsm.GameObject==null?new JValue((object)null):new JValue(fsm.GameObject.activeSelf)},
            {"activeInHierarchy",fsm.GameObject==null?new JValue((object)null):new JValue(fsm.GameObject.activeInHierarchy)},
            {"eventTarget",fsm.EventTarget==null?"Self":fsm.EventTarget.target.ToString()},
            {"eventTargetFsmName",fsm.EventTarget==null || fsm.EventTarget.fsmName==null?null:fsm.EventTarget.fsmName.Value},
            {"activeTransitions",NativeFightTransitions(fsm.ActiveState==null?null:fsm.ActiveState.Transitions)},
            {"globalTransitions",NativeFightTransitions(fsm.GlobalTransitions)}};
    }
    static void NativeFightEventPrefix(Fsm __instance,string fsmEventName)
    {
        RuntimeModelTest observer=nativeFightObserver;
        if(observer==null || Time.realtimeSinceStartup>=observer.nativeFightDeadline || observer.nativeFightContinuation==null ||
            !object.ReferenceEquals(__instance,observer.nativeFightContinuation.m_FSM) || fsmEventName!="menuFight")return;
        try
        {
            if(observer.nativeFightRecords.Count>=64){observer.nativeFightDropped++;return;}
            observer.nativeFightRecords.Add(new JObject{{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup},
                {"phase","pinned-fsm-event-entry"},{"event",fsmEventName},{"fsm",NativeFightFsmState(__instance)}});
        }
        catch(Exception){observer.nativeFightDropped++;}
    }
    static JObject NativeFightDelegate(Delegate value)
    {
        return value==null?null:new JObject{{"method",value.Method.DeclaringType.FullName+"."+value.Method.Name},
            {"targetType",value.Target==null?null:value.Target.GetType().FullName}};
    }
    static JObject NativeFightContinuationState(ContinueFSM continuation)
    {
        if(continuation==null)return null;
        object argument=typeof(ContinueFSM).GetField("m_ContinueObject",Members).GetValue(continuation);
        return new JObject{{"id",continuation.m_ID},{"callerId",continuation.m_CallerID},
            {"waitCount",continuation.m_WaitCount},{"waitClients",continuation.m_WaitClients.ToString()},
            {"isLocal",continuation.m_IsLocal},{"localPhotonId",PhotonNetwork.player==null?new JValue((object)null):new JValue(PhotonNetwork.player.ID)},
            {"fsmName",continuation.m_FSM==null?null:continuation.m_FSM.Name},
            {"fsmState",continuation.m_FSM==null?null:continuation.m_FSM.ActiveStateName},{"fsm",NativeFightFsmState(continuation.m_FSM)},
            {"storedArgumentType",argument==null?null:argument.GetType().FullName},
            {"storedEvent",argument is string?(string)argument:null},
            {"completeDelegate",NativeFightDelegate((Delegate)typeof(ContinueFSM).GetField("m_CompleteDelegate",Members).GetValue(continuation))},
            {"completeDelegate1",NativeFightDelegate((Delegate)typeof(ContinueFSM).GetField("m_CompleteDelegate_1",Members).GetValue(continuation))}};
    }
    static ContinueFSM NativeFightMenuContinuation()
    {return FTKUI.Instance==null || FTKUI.Instance.m_EncounterMenu==null?null:FTKUI.Instance.m_EncounterMenu.m_ContinueFSM;}
    void NativeFightObserveContinuation(string phase)
    {
        JToken state=NativeFightContinuationState(nativeFightContinuation);
        if(JToken.DeepEquals(state,nativeFightContinuationLast))return;
        nativeFightContinuationLast=state;
        if(nativeFightRecords.Count>=64){nativeFightDropped++;return;}
        nativeFightRecords.Add(new JObject{{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup},
            {"phase",phase},{"continuation",state}});
    }
    static void NativeFightContinuePrefix(ContinueFSM __instance,object _v)
    {
        RuntimeModelTest observer=nativeFightObserver;
        if(observer==null || Time.realtimeSinceStartup>=observer.nativeFightDeadline || !(_v is string) || (string)_v!="menuFight")return;
        try{observer.nativeFightContinuation=__instance;observer.nativeFightContinuationLast=null;observer.NativeFightObserveContinuation("menuFight-before-continue");}
        catch(Exception){observer.nativeFightDropped++;}
    }

    static MethodInfo NativeFightPrefixMethod()
    {return typeof(RuntimeModelTest).GetMethod("NativeFightPrefix",Statics);}
    void NativeFightDisarm()
    {
        if(nativeFightHarmony!=null)foreach(MethodInfo method in nativeFightMethods)
        {
            nativeFightHarmony.Unpatch(method,NativeFightPrefixMethod());
            nativeFightHarmony.Unpatch(method,NativeFightContinuePrefixMethod());
            nativeFightHarmony.Unpatch(method,NativeFightEventPrefixMethod());
        }
        nativeFightMethods.Clear();nativeFightHarmony=null;
        if(nativeFightObserver==this)nativeFightObserver=null;
    }
    void NativeFightTraceTick()
    {
        if(nativeFightHarmony==null)return;
        if(Time.realtimeSinceStartup>=nativeFightDeadline){NativeFightDisarm();return;}
        if(nativeFightContinuation!=null)
            try{NativeFightObserveContinuation("continuation-state-change-next-frame");}catch(Exception){nativeFightDropped++;}
    }
    static void NativeFightPrefix(object __instance,MethodBase __originalMethod)
    {
        RuntimeModelTest observer=nativeFightObserver;
        if(observer==null || Time.realtimeSinceStartup>=observer.nativeFightDeadline)return;
        try
        {
            if(observer.nativeFightRecords.Count>=64){observer.nativeFightDropped++;return;}
            UnityEngine.Object instance=__instance as UnityEngine.Object;
            observer.nativeFightRecords.Add(new JObject{{"frame",Time.frameCount},
                {"realtime",Time.realtimeSinceStartup},{"method",__originalMethod.DeclaringType.FullName+"."+__originalMethod.Name},
                {"instanceId",instance==null?new JValue((object)null):new JValue(instance.GetInstanceID())},
                {"menuContinuation",__instance is uiEnemyPoiMenu?NativeFightContinuationState(NativeFightMenuContinuation()):null},
                {"entry",__instance is uiLocationMenuEntry?NativeFightEntry((uiLocationMenuEntry)__instance):null}});
        }
        catch(Exception e)
        {
            if(observer.nativeFightRecords.Count<64)observer.nativeFightRecords.Add(new JObject{{"observerError",e.GetType().Name}});
        }
    }
    static JObject NativeFightEntry(uiLocationMenuEntry entry)
    {
        JArray persistent=new JArray();
        if(entry.m_Button!=null)for(int i=0;i<Math.Min(entry.m_Button.onClick.GetPersistentEventCount(),16);i++)
        {
            UnityEngine.Object target=entry.m_Button.onClick.GetPersistentTarget(i);
            persistent.Add(new JObject{{"method",entry.m_Button.onClick.GetPersistentMethodName(i)},
                {"targetType",target==null?null:target.GetType().FullName},
                {"targetId",target==null?new JValue((object)null):new JValue(target.GetInstanceID())}});
        }
        return new JObject{{"instanceId",entry.GetInstanceID()},{"active",entry.gameObject.activeInHierarchy},
            {"label",entry.m_Text0==null?null:entry.m_Text0.text},
            {"handler",entry.m_MethodInfo==null?null:entry.m_MethodInfo.DeclaringType.FullName+"."+entry.m_MethodInfo.Name},
            {"locationType",entry.m_Menu==null || entry.m_Menu.m_Location==null?null:entry.m_Menu.m_Location.GetType().FullName},
            {"buttonEnabled",entry.m_Button==null?new JValue((object)null):new JValue(entry.m_Button.enabled)},
            {"interactable",entry.m_Button==null?new JValue((object)null):new JValue(entry.m_Button.interactable)},
            {"persistentCallbacks",persistent}};
    }
    static JArray NativeFightVisibleEntries()
    {
        JArray entries=new JArray();
        foreach(uiLocationMenuEntry entry in UnityEngine.Object.FindObjectsOfType<uiLocationMenuEntry>())
            if(entry!=null && entry.gameObject.activeInHierarchy && entries.Count<32)
                entries.Add(NativeFightEntry(entry));
        return entries;
    }
    JObject NativeFightTrace(JObject command)
    {
        CatalogKeys(command,"id","session","op","action");
        string action=(string)command["action"];
        NativeFightTraceTick();
        if(action=="arm")
        {
            if(nativeFightHarmony!=null)throw new InvalidOperationException("Native fight trace already armed; inspect or disarm first.");
            NativeFightDisarm();nativeFightRecords.Clear();nativeFightDropped=0;nativeFightContinuation=null;nativeFightContinuationLast=null;
            nativeFightTraceId=Guid.NewGuid().ToString("N");
            MethodInfo[] methods={
                typeof(uiEnemyPoiMenu).GetMethod("UseFightButton",Members,null,Type.EmptyTypes,null),
                typeof(uiLocationMenuEntry).GetMethod("OnClick",Members,null,Type.EmptyTypes,null),
                typeof(MiniHexInfo).GetMethod("OnFight",Members,null,new[]{typeof(CharacterOverworld)},null),
                typeof(GameFlow).GetMethod("LocalInitCombatSession",Members,null,new[]{typeof(string),typeof(ContinueFSM)},null),
                typeof(EncounterSessionMC).GetMethod("InitiateEncounterSessionRPC",Members,null,new[]{typeof(FTKPlayerID),typeof(FTKPlayerID[]),typeof(string),typeof(ContinueFSM)},null)};
            foreach(MethodInfo method in methods)if(method==null || method.ReturnType!=typeof(void))
                throw new InvalidOperationException("Native fight trace signature unavailable.");
            MethodInfo continuation=typeof(ContinueFSM).GetMethod("Continue",Members,null,new[]{typeof(object)},null);
            if(continuation==null || continuation.ReturnType!=typeof(void))throw new InvalidOperationException("Native continuation signature unavailable.");
            MethodInfo fsmEvent=typeof(Fsm).GetMethod("Event",Members,null,new[]{typeof(string)},null);
            if(fsmEvent==null || fsmEvent.ReturnType!=typeof(void))throw new InvalidOperationException("Native Fsm.Event(string) signature unavailable.");
            nativeFightHarmony=new Harmony("com.ftkmf.runtime-model-test.native-fight-trace");
            nativeFightDeadline=Time.realtimeSinceStartup+60f;nativeFightObserver=this;
            try
            {
                foreach(MethodInfo method in methods)
                {nativeFightMethods.Add(method);nativeFightHarmony.Patch(method,new HarmonyMethod(NativeFightPrefixMethod()),null,null,null,null);}
                nativeFightMethods.Add(continuation);nativeFightHarmony.Patch(continuation,new HarmonyMethod(NativeFightContinuePrefixMethod()),null,null,null,null);
                nativeFightMethods.Add(fsmEvent);nativeFightHarmony.Patch(fsmEvent,new HarmonyMethod(NativeFightEventPrefixMethod()),null,null,null,null);
            }
            catch{NativeFightDisarm();throw;}
        }
        else if(action=="disarm")NativeFightDisarm();
        else if(action!="inspect")throw new ArgumentException("action must be arm, inspect or disarm");
        return new JObject{{"ok",true},{"traceId",nativeFightTraceId},{"armed",nativeFightHarmony!=null},
            {"remainingSeconds",nativeFightHarmony==null?0:Math.Max(0,nativeFightDeadline-Time.realtimeSinceStartup)},
            {"records",new JArray(nativeFightRecords)},{"droppedRecords",nativeFightDropped},
            {"visibleEntries",NativeFightVisibleEntries()},{"menuContinuation",NativeFightContinuationState(NativeFightMenuContinuation())},
            {"scope","Five callback entry prefixes plus menuFight-only continuation/pinned-FSM event entries and next-frame state observation, fixed 60-second expiry; no callbacks invoked or native state changed. Instrumentation may change runtime timing."}};
    }
}
