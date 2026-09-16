using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

internal static class StorySubmissionPolicy
{
    public static bool RegisteredQuest(System.Collections.IDictionary table,int id,object quest)
    {return quest!=null && id!=0 && table!=null && table.Contains(id) && ReferenceEquals(table[id],quest);}
    public static bool ExactAction(object callback,object target,string method)
    {
        Action action=callback as Action;
        return target!=null && action!=null && action.GetInvocationList().Length==1 && ReferenceEquals(action.Target,target) &&
            action.Method==target.GetType().GetMethod(method,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,null,Type.EmptyTypes,null);
    }
    static bool B(JObject value,string key){JToken token=value[key];if(token==null || token.Type!=JTokenType.Boolean)throw new InvalidOperationException("Unknown story boolean: "+key);return (bool)token;}
    static int I(JObject value,string key){JToken token=value[key];if(token==null || token.Type!=JTokenType.Integer)throw new InvalidOperationException("Unknown exact story integer: "+key);return (int)token;}
    static string S(JObject value,string key)
    {JToken token=value[key];if(token==null || token.Type!=JTokenType.String || string.IsNullOrEmpty((string)token))throw new InvalidOperationException("Unknown story string: "+key);return (string)token;}
    static bool PanelReady(JObject panel,bool portrait)
    {
        if(I(panel,"componentId")==0 || I(panel,"panelId")==0)throw new InvalidOperationException("Native story panel missing.");
        JObject focus=panel["focus"]as JObject;if(focus==null)throw new InvalidOperationException("Native story focus unavailable.");
        bool authority=I(focus,"instanceId")!=0 && I(focus,"continuationId")>=0 && B(focus,"active") && B(focus,"currentFocus") && B(focus,"canClose") && B(focus,"exactCallback") && B(focus,"localDispatchReady");
        return authority && B(panel,"hudActive") && B(panel,"activeSelf") && B(panel,"activeInHierarchy") && B(panel,"fullyOpened") && (portrait || B(panel,"clickable")) && (!portrait || B(panel,"closeOnOkay"));
    }
    public static void Classify(JObject state)
    {
        JObject signals=state["signals"]as JObject,portrait=state["portrait"]as JObject,confirm=state["questConfirm"]as JObject;
        if(signals==null || portrait==null || confirm==null)throw new InvalidOperationException("Native story metadata missing.");
        JArray warnings=signals["warnings"]as JArray,fsms=state["presenterFsms"]as JArray;
        if(warnings==null || warnings.Count!=0 || fsms==null || fsms.Count!=3)throw new InvalidOperationException("Story observation unavailable; warning/FSM shape.");
        bool portraitReady=PanelReady(portrait,true),confirmReady=PanelReady(confirm,false) && B(confirm,"samePresenterQuest") && B(confirm,"continuationPresent");
        bool modal=B(signals,"modalOpen"),choice=B(signals,"choiceOpen"),quietFsms=true;
        foreach(JObject fsm in fsms){if(!B(fsm,"present"))throw new InvalidOperationException("Presenter FSM missing.");quietFsms&=!B(fsm,"enabled");}
        bool outside=B(state,"inSession") && B(state,"livingOwnedParty") && B(state,"outsideCombat") && B(state,"outsideDungeon");
        bool present=B(state,"messagePresent"),closed=B(state,"messageClosed");string type=(string)state["messageType"];
        int count=I(state,"queueCount");if(count<0 || count>64)throw new InvalidOperationException("Queue outside0..64.");
        bool identity=present && !closed && type=="StoryQuestMessage" && I(state,"messageId")>=0 && I(state,"messageId")==I(state,"presenterMessageId") && B(state,"questPresent") && B(state,"questRegisteredReference") && I(state,"questId")!=0;
        int page=I(state,"pageIndex"),pages=I(state,"pageCount");if(page<0 || pages<0 || pages>256)throw new InvalidOperationException("Story page bounds unavailable.");
        bool knownSignals=modal && !choice && (string)signals["modalType"]=="StoryQuestMessage";
        string actionable=null;
        if(outside && identity && knownSignals && state["contentSha256"]!=null && state["contentSha256"].Type==JTokenType.String && !string.IsNullOrEmpty((string)state["contentSha256"]))
        {
            if(portraitReady && KnownPhase(S(state,"phase")) && !B(confirm,"activeSelf") && !B(confirm,"activeInHierarchy") && page<pages)actionable="portrait";
            else if(confirmReady && !portraitReady)actionable="questConfirm";
        }
        bool complete=outside && !present && type=="None" && count==0 && !B(state,"currentContinuationPresent") && quietFsms && !modal && !choice;
        foreach(JObject panel in new[]{portrait,confirm})complete&=!B(panel,"activeSelf") && !B(panel,"activeInHierarchy") && !B(panel,"fullyOpened");
        state["actionableSurface"]=actionable==null?new JValue((object)null):new JValue(actionable);
        state["complete"]=complete;
        state["status"]=complete?"native-story-setup-complete":!outside?"outside-overworld-setup":choice?"unsupported-choice":
            (type!="None" && type!="StoryQuestMessage") || (modal && !knownSignals)?"unsupported-modal":actionable!=null?"native-story-page-actionable":"native-story-pending";
    }
    static bool KnownPhase(string phase)
    {return phase=="DeliverQuestMessageClosedPart" || phase=="DeliverStartQuestMsgPartClosed" || phase=="DeliverMultiQuestMsgClosed" || phase=="DeliverSubQuestMsgClosed";}
    public static string Validate(JObject request,JObject state)
    {
        Classify(state);string surface=(string)request["surface"];
        if(surface!="portrait" && surface!="questConfirm")throw new InvalidOperationException("Exact known story surface required.");
        if((string)state["actionableSurface"]!=surface)throw new InvalidOperationException("Exact native story page is not actionable.");
        foreach(string key in new[]{"coordinatorId","messageId","presenterId","pageIndex","questId","heroInstanceId"})
            if(I(request,key)!=I(state,key))throw new InvalidOperationException("Story identity changed: "+key);
        foreach(string key in new[]{"phase","contentSha256"})if(S(request,key)!=S(state,key))throw new InvalidOperationException("Story phase/content changed: "+key);
        string hash=S(state,"contentSha256");if(hash.Length!=64 || System.Text.RegularExpressions.Regex.IsMatch(hash,"[^0-9a-f]"))throw new InvalidOperationException("Native story content hash missing.");
        JObject panel=(JObject)state[surface],focus=(JObject)panel["focus"];
        if(I(request,"clickInstanceId")!=I(focus,"instanceId") || I(request,"clickContinuationId")!=I(focus,"continuationId"))throw new InvalidOperationException("Story input callback identity changed.");
        foreach(string key in new[]{"componentId","panelId"})if(I(request,key)!=I(panel,key))throw new InvalidOperationException("Story panel identity changed: "+key);
        if(I(state,"coordinatorId")==0 || I(state,"presenterId")==0 || I(state,"heroInstanceId")==0)throw new InvalidOperationException("Native actor/component identity missing.");
        return I(state,"coordinatorId")+":"+I(state,"presenterId")+":"+I(state,"heroInstanceId")+":"+I(state,"messageId")+":"+I(state,"pageIndex")+":"+S(state,"phase")+":"+hash+":"+surface;
    }
}
