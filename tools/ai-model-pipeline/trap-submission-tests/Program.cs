using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
class Program
{
    static int count;
    static JObject Copy(JObject o){return (JObject)o.DeepClone();}
    static void Check(bool b){count++;if(!b)throw new Exception("Assertion"+count);}
    static void Reject(Action a){bool rejected=false;try{a();}catch(InvalidOperationException){rejected=true;}Check(rejected);}
    static void Main()
    {
        object row=new object();TrapSubmissionPolicy.SameRow(row,row);Check(true);
        Reject(()=>TrapSubmissionPolicy.SameRow(row,new object()));Reject(()=>TrapSubmissionPolicy.SameRow(null,null));Reject(()=>TrapSubmissionPolicy.SameRow(row,null));
        JObject current=JObject.Parse("{identity:{root:'owned',session:'nonce',dungeonId:1,level:0,room:3,trapId:8,heroId:5,heroFid:{photonId:-1,turnIndex:0},dummyId:9,celId:10,esId:11,mcId:12,dioramaId:13,trapTableId:'poison',trapType:'Trap1',voteFsmId:14,containerId:15,pins:{core:'sha',helper:'sha'}},scope:true,noModal:true,nativeTrap:true,heroQueue:true,nativeVote:true,trapActive:true,buttons:{Disarm:{id:30,usable:true},Proceed:{id:31,usable:true}}}");
        JObject ticket=Copy(current);ticket["ticketId"]="ticket";ticket["frame"]=100;ticket["realtime"]=10;
        JObject request=new JObject{{"ticketId","ticket"},{"identity",current["identity"].DeepClone()},{"option","Disarm"}};
        TrapSubmissionPolicy.Validate(request,ticket,current,101,10.1);Check(true);
        foreach(string field in new[]{"root","session","dungeonId","level","room","trapId","heroId","heroFid","dummyId","celId","esId","mcId","dioramaId","trapTableId","trapType","voteFsmId","containerId","pins"})
        {JObject changed=Copy(current);changed["identity"][field]="changed";Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,changed,101,10.1));}
        foreach(string gate in new[]{"scope","noModal","nativeTrap","heroQueue","nativeVote","trapActive"})
        {JObject bad=Copy(current);bad[gate]=false;Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,bad,101,10.1));bad.Remove(gate);Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,bad,101,10.1));}
        foreach(double time in new[]{9,15.01,double.NaN,double.PositiveInfinity})Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,current,101,time));
        Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,current,99,11));Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,current,701,11));
        JObject wrong=Copy(request);wrong["ticketId"]="old";Reject(()=>TrapSubmissionPolicy.Validate(wrong,ticket,current,101,11));
        wrong=Copy(request);wrong["option"]="Destroy";Reject(()=>TrapSubmissionPolicy.Validate(wrong,ticket,current,101,11));
        JObject unavailable=Copy(current);unavailable["buttons"]["Disarm"]["usable"]=false;Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,unavailable,101,11));
        unavailable=Copy(current);unavailable["buttons"]["Disarm"]["id"]=99;Reject(()=>TrapSubmissionPolicy.Validate(request,ticket,unavailable,101,11));
        int calls=0;var claims=new HashSet<string>();string key=TrapSubmissionPolicy.Key((JObject)current["identity"]);
        TrapSubmissionPolicy.Submit(claims,key,()=>calls++);Check(calls==1);
        Reject(()=>TrapSubmissionPolicy.Submit(claims,key,()=>calls++));Check(calls==1);
        // Changing option/button/command cannot alter the room claim key.
        JObject other=Copy(current);other["option"]="Proceed";other["commandId"]="new";other["buttons"]["Proceed"]["id"]=99;
        Check(TrapSubmissionPolicy.Key((JObject)other["identity"])==key);
        Reject(()=>TrapSubmissionPolicy.Submit(claims,TrapSubmissionPolicy.Key((JObject)other["identity"]),()=>calls++));Check(calls==1);
        claims.Clear();Reject(()=>TrapSubmissionPolicy.Submit(claims,key,()=>{calls++;throw new InvalidOperationException("native uncertain");}));
        Check(claims.Contains(key));Reject(()=>TrapSubmissionPolicy.Submit(claims,key,()=>calls++));Check(calls==2);
        Console.WriteLine(count+" linked trap policy assertions PASS");
    }
}
