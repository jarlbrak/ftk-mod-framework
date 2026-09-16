using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
class Program
{
 static int count;
 static void Check(bool v,string why){count++;if(!v)throw new Exception(why);}
 static void Reject(Action f,string why){bool failed=false;try{f();}catch(InvalidOperationException){failed=true;}Check(failed,why);}
 static JObject Copy(JObject v){return JObject.Parse(v.ToString());}
 static JObject View(){return JObject.Parse(@"{'identity':{'root':'/owned','session':'nonce','dungeonId':1,'level':0,'room':2,'esId':3,'mcId':4,'turn':5,'heroId':6,'targetId':7,'buttonId':8,'slotCount':3},'pins':{'core':'abc'},'availableFocus':4,'spentFocus':0,'scope':true,'noModal':true,'normalAttack':true,'inputReady':true,'canPay':true,'stanceReady':true,'frame':100,'realtime':10.0,'ticketId':'ticket','focusing':false,'interrupt':false,'animationCount':0}");}
 static void Main()
 {
  object row=new object();PaidFocusPolicy.SameWeaponRow(row,row);Check(true,"same registered weapon row");Reject(()=>PaidFocusPolicy.SameWeaponRow(row,new object()),"replacement row");Reject(()=>PaidFocusPolicy.SameWeaponRow(null,null),"missing row");
  JObject ticket=View(),request=new JObject{{"ticketId","ticket"},{"identity",Copy(ticket)["identity"]}};
  PaidFocusPolicy.Validate(request,ticket,Copy(ticket),101,10.1);Check(true,"valid");
  foreach(double time in new[]{9.9,15.01,double.NaN,double.PositiveInfinity})Reject(()=>PaidFocusPolicy.Validate(request,ticket,Copy(ticket),101,time),"bad time");
  foreach(int frame in new[]{99,701})Reject(()=>PaidFocusPolicy.Validate(request,ticket,Copy(ticket),frame,11),"bad frame");
  foreach(string gate in new[]{"scope","noModal","normalAttack","inputReady","canPay","stanceReady"})
  {JObject changed=Copy(ticket);changed[gate]=false;Reject(()=>PaidFocusPolicy.Validate(request,ticket,changed,101,11),gate);changed.Remove(gate);Reject(()=>PaidFocusPolicy.Validate(request,ticket,changed,101,11),"missing "+gate);}
  foreach(string key in new[]{"turn","heroId","targetId","buttonId"}){JObject changed=Copy(ticket);changed["identity"][key]=99;Reject(()=>PaidFocusPolicy.Validate(request,ticket,changed,101,11),key);}
  foreach(string key in new[]{"availableFocus","spentFocus"}){JObject changed=Copy(ticket);changed[key]=2;Reject(()=>PaidFocusPolicy.Validate(request,ticket,changed,101,11),key);}
  JObject pins=Copy(ticket);pins["pins"]["core"]="changed";Reject(()=>PaidFocusPolicy.Validate(request,ticket,pins,101,11),"pin");
  HashSet<string> claims=new HashSet<string>();int calls=0;string claim=PaidFocusPolicy.Key((JObject)ticket["identity"]);
  PaidFocusPolicy.Submit(claims,claim,()=>calls++);Check(calls==1,"native once");
  foreach(string field in new[]{"targetId","buttonId","slotCount"}){JObject changed=Copy(ticket);changed["identity"][field]=99;Reject(()=>PaidFocusPolicy.Submit(claims,PaidFocusPolicy.Key((JObject)changed["identity"]),()=>calls++),"key excludes "+field);}
  Check(calls==1,"no replay callback");claims.Clear();Reject(()=>PaidFocusPolicy.Submit(claims,claim,()=>{calls++;throw new InvalidOperationException("native throw");}),"throw");
  Reject(()=>PaidFocusPolicy.Submit(claims,claim,()=>calls++),"claim survives");Check(calls==2,"no native retry");
  JObject before=Copy(ticket),after=Copy(ticket);before["focusing"]=true;before["animationCount"]=1;after["animationCount"]=1;after["availableFocus"]=3;after["spentFocus"]=1;
  Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="payment-complete","native callback debit before animcount decrement");
  before["animationCount"]=0;after["animationCount"]=0;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="payment-complete","native synchronous callback");
  Check(PaidFocusPolicy.Payment(ticket,before,after,true)=="native-callback-exception","exception not acceptance");
  before["interrupt"]=true;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="native-payment-interrupted","interrupted");before["interrupt"]=false;
  before["availableFocus"]=3;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="callback-baseline-changed","prior debit");before["availableFocus"]=4;
  after["spentFocus"]=0;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="native-payment-delta-mismatch","unpaid");after["spentFocus"]=1;
  after["scope"]=false;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="callback-native-scope-changed","callback lost combat scope");after["scope"]=true;
  after["identity"]["turn"]=6;Check(PaidFocusPolicy.Payment(ticket,before,after,false)=="callback-identity-changed","callback turn change");
  Check(PaidFocusPolicy.CompletionStatus("submitted",1,"payment-complete",false)=="payment-complete","first paid callback");
  Check(PaidFocusPolicy.CompletionStatus("payment-complete",2,"payment-complete",false)=="duplicate-native-callback","duplicate cannot keep success");
  Check(PaidFocusPolicy.CompletionStatus("uncertain-native-submission",1,"payment-complete",false)=="uncertain-native-submission","late callback never upgrades uncertain submission");
  Check(PaidFocusPolicy.CompletionStatus("submitted",1,"payment-complete",true)=="callback-observation-error","observation error cannot pass");
  Check(PaidFocusPolicy.CompletionStatus("duplicate-native-callback",3,"payment-complete",false)=="duplicate-native-callback","duplicate permanent");
  Console.WriteLine("PASS "+count+" assertions (actual shipped Newtonsoft; pure payment policy, no Unity integration)");
 }
}
