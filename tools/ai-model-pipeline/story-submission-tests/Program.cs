using System;
using Newtonsoft.Json.Linq;
static class Program
{
    sealed class CallbackTarget{public void UseOkayButton(){}public void Other(){}}
    static int tests;
    static JObject Panel(int id,bool open){return new JObject{{"componentId",id},{"panelId",id+1},{"hudActive",true},{"activeSelf",open},{"activeInHierarchy",open},{"fullyOpened",open},{"clickable",open},{"closeOnOkay",true},{"focus",new JObject{{"instanceId",40},{"continuationId",50},{"active",open},{"currentFocus",open},{"canClose",true},{"exactCallback",open},{"localDispatchReady",true}}}};}
    static JObject State()
    {
        JObject confirm=Panel(20,false);confirm["samePresenterQuest"]=true;confirm["continuationPresent"]=true;
        return new JObject{{"coordinatorId",1},{"presenterId",2},{"heroInstanceId",3},{"messageId",4},{"presenterMessageId",4},{"questId",5},{"questPresent",true},{"questRegisteredReference",true},{"pageIndex",0},{"pageCount",2},{"phase","DeliverStartQuestMsgPartClosed"},{"contentSha256",new string('a',64)},
            {"messagePresent",true},{"messageClosed",false},{"messageType","StoryQuestMessage"},{"queueCount",0},{"currentContinuationPresent",true},
            {"inSession",true},{"livingOwnedParty",true},{"outsideCombat",true},{"outsideDungeon",true},{"portrait",Panel(10,true)},{"questConfirm",confirm},
            {"signals",new JObject{{"modalOpen",true},{"choiceOpen",false},{"modalType","StoryQuestMessage"},{"warnings",new JArray()}}},
            {"presenterFsms",new JArray{new JObject{{"present",true},{"enabled",true}},new JObject{{"present",true},{"enabled",false}},new JObject{{"present",true},{"enabled",false}}}}};
    }
    static JObject Request(JObject s,string surface="portrait")
    {JObject r=new JObject{{"surface",surface}};foreach(string k in new[]{"coordinatorId","presenterId","heroInstanceId","messageId","questId","pageIndex"})r[k]=s[k].DeepClone();foreach(string k in new[]{"phase","contentSha256"})r[k]=s[k].DeepClone();r["clickInstanceId"]=s[surface]["focus"]["instanceId"].DeepClone();r["clickContinuationId"]=s[surface]["focus"]["continuationId"].DeepClone();foreach(string k in new[]{"componentId","panelId"})r[k]=s[surface][k].DeepClone();return r;}
    static void Check(bool b){tests++;if(!b)throw new Exception("Assertion failed");}
    static void Reject(JObject s,JObject r){try{StorySubmissionPolicy.Validate(r,s);}catch(InvalidOperationException){tests++;return;}throw new Exception("Unsafe submission accepted");}
    static void Main()
    {
        JObject s=State();Check(StorySubmissionPolicy.Validate(Request(s),s)=="1:2:3:4:0:DeliverStartQuestMsgPartClosed:"+new string('a',64)+":portrait");
        foreach(string key in new[]{"coordinatorId","presenterId","heroInstanceId","messageId","questId","pageIndex","componentId","panelId","clickInstanceId","clickContinuationId"})
        {s=State();JObject r=Request(s);r[key]=999;Reject(s,r);}
        foreach(string key in new[]{"inSession","livingOwnedParty","outsideCombat","outsideDungeon"}){s=State();JObject r=Request(s);s[key]=false;Reject(s,r);}
        foreach(string key in new[]{"hudActive","activeSelf","activeInHierarchy","fullyOpened","closeOnOkay"}){s=State();JObject r=Request(s);s["portrait"][key]=false;Reject(s,r);}
        object nativeQuest=new object(),differentQuest=new object();System.Collections.Hashtable nativeTable=new System.Collections.Hashtable();nativeTable[-1]=nativeQuest;
        Check(StorySubmissionPolicy.RegisteredQuest(nativeTable,-1,nativeQuest));
        Check(!StorySubmissionPolicy.RegisteredQuest(nativeTable,-1,differentQuest));
        Check(!StorySubmissionPolicy.RegisteredQuest(nativeTable,-2,nativeQuest));
        Check(!StorySubmissionPolicy.RegisteredQuest(nativeTable,0,nativeQuest));
        Check(!StorySubmissionPolicy.RegisteredQuest(null,-1,nativeQuest));
        Check(!StorySubmissionPolicy.RegisteredQuest(nativeTable,-1,null));Check(nativeTable.Count==1);
        s=State();s["questPresent"]=false;s["questRegisteredReference"]=false;s["questId"]=new JValue((object)null);Reject(s,Request(State()));
        s=State();s["questId"]=-1;Check(StorySubmissionPolicy.Validate(Request(s),s).EndsWith("portrait"));
        foreach(string field in new[]{"questPresent","questRegisteredReference"}){s=State();s["questId"]=-1;s[field]=false;Reject(s,Request(s));}
        s=State();s["questId"]=0;Reject(s,Request(s));
        s=State();s["questId"]=-1;JObject wrongQuest=Request(s);wrongQuest["questId"]=-2;Reject(s,wrongQuest);
        s=State();s["portrait"]["clickable"]=false;Check(StorySubmissionPolicy.Validate(Request(s),s).EndsWith("portrait"));
        foreach(string key in new[]{"active","currentFocus","canClose","exactCallback","localDispatchReady"}){s=State();JObject r=Request(s);s["portrait"]["focus"][key]=false;Reject(s,r);}
        foreach(string key in new[]{"phase","contentSha256"}){s=State();JObject r=Request(s);r[key]="wrong";Reject(s,r);}
        s=State();s["phase"]="unavailable";Reject(s,Request(s));
        s=State();s["portrait"]["focus"]["continuationId"]=-1;Reject(s,Request(s));
        CallbackTarget target=new CallbackTarget(),other=new CallbackTarget();Action action=target.UseOkayButton;
        Check(StorySubmissionPolicy.ExactAction(action,target,"UseOkayButton"));
        Check(!StorySubmissionPolicy.ExactAction(action,other,"UseOkayButton"));
        Check(!StorySubmissionPolicy.ExactAction((Action)target.Other,target,"UseOkayButton"));
        Check(!StorySubmissionPolicy.ExactAction(action+(Action)target.Other,target,"UseOkayButton"));
        Check(!StorySubmissionPolicy.ExactAction(null,target,"UseOkayButton"));
        s=State();s["signals"]["choiceOpen"]=true;Reject(s,Request(s));
        s=State();s["messageType"]="GlobalMessage";Reject(s,Request(s));
        s=State();s["messageClosed"]=true;Reject(s,Request(s));
        s=State();s["presenterMessageId"]=99;Reject(s,Request(s));
        s=State();JObject bad=Request(s);bad["pageIndex"]=0.1;Reject(s,bad);
        s=State();s["portrait"]=Panel(10,false);s["questConfirm"]=Panel(20,true);s["questConfirm"]["samePresenterQuest"]=true;s["questConfirm"]["continuationPresent"]=true;
        Check(StorySubmissionPolicy.Validate(Request(s,"questConfirm"),s).EndsWith("questConfirm"));
        foreach(string key in new[]{"samePresenterQuest","continuationPresent","clickable","fullyOpened"}){JObject c=(JObject)s.DeepClone();c["questConfirm"][key]=false;Reject(c,Request(c,"questConfirm"));}
        s=State();s["messageType"]="None";s["signals"]["modalOpen"]=false;StorySubmissionPolicy.Classify(s);Check(!(bool)s["complete"]);
        s["messagePresent"]=false;s["currentContinuationPresent"]=false;s["portrait"]=Panel(10,false);foreach(JObject f in (JArray)s["presenterFsms"])f["enabled"]=false;
        StorySubmissionPolicy.Classify(s);Check((bool)s["complete"]);
        foreach(string key in new[]{"queueCount","currentContinuationPresent"}){JObject c=(JObject)s.DeepClone();c[key]=key=="queueCount"?(JToken)new JValue(1):new JValue(true);StorySubmissionPolicy.Classify(c);Check(!(bool)c["complete"]);}
        JObject noContent=(JObject)s.DeepClone();noContent["contentSha256"]=new JValue((object)null);StorySubmissionPolicy.Classify(noContent);Check((bool)noContent["complete"]);
        noContent=State();noContent["contentSha256"]=new JValue((object)null);StorySubmissionPolicy.Classify(noContent);Check(noContent["actionableSurface"].Type==JTokenType.Null);Reject(noContent,Request(State()));
        JObject missing=(JObject)s.DeepClone();((JObject)missing["presenterFsms"][0]).Remove("enabled");Reject(missing,Request(State()));
        Console.WriteLine(tests+" linked native story predicate tests PASS using "+typeof(JObject).Assembly.FullName);
    }
}
