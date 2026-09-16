using System;
using Newtonsoft.Json.Linq;
static class Program
{
    static int tests;
    static void Pass(Action action){action();tests++;}
    static void Reject(Action action){try{action();}catch(InvalidOperationException){tests++;return;}throw new Exception("Expected identity/bounds rejection");}
    static void Arm(int owner=1,int cel=2,int lease=3,bool active=true,bool acquired=true,bool applied=true,bool present=true,bool duplicate=false,int count=0,int oldResources=0,int newResources=3)
    {LeaseObservationPin.ValidateArm(owner,1,cel,2,lease,3,active,acquired,applied,present,duplicate,count,oldResources,newResources);}
    static void Main()
    {
        Pass(()=>Arm());Pass(()=>Arm(count:7,oldResources:253));
        Reject(()=>Arm(owner:4));Reject(()=>Arm(cel:4));Reject(()=>Arm(lease:4));Reject(()=>Arm(owner:0));Reject(()=>Arm(cel:0));Reject(()=>Arm(lease:0));
        Reject(()=>Arm(active:false));Reject(()=>Arm(acquired:false));Reject(()=>Arm(applied:false));Reject(()=>Arm(present:false));Reject(()=>Arm(duplicate:true));
        Reject(()=>Arm(count:8));Reject(()=>Arm(oldResources:254));Reject(()=>Arm(newResources:0));Reject(()=>Arm(newResources:257));Reject(()=>Arm(count:-1));
        JObject core=new JObject{{"location","/scratch/game/Core.dll"},{"loadedModuleVersionId","module1"},{"assemblyFileSha256","hash1"}};
        LeaseObservationPin pin=new LeaseObservationPin("/scratch/game","nonce","arm-id",core);
        Pass(()=>pin.Check("/scratch/game","nonce",core));
        Reject(()=>pin.Check("/scratch/other","nonce",core));Reject(()=>pin.Check("/scratch/game","newnonce",core));
        foreach(string key in new[]{"location","loadedModuleVersionId","assemblyFileSha256"})
        {JObject changed=(JObject)core.DeepClone();changed[key]="changed";Reject(()=>pin.Check("/scratch/game","nonce",changed));}
        core["assemblyFileSha256"]="changed";Reject(()=>pin.Check("/scratch/game","nonce",core));
        JObject view=pin.View();view["coreIdentity"]["assemblyFileSha256"]="changed";
        Pass(()=>pin.Check("/scratch/game","nonce",new JObject{{"location","/scratch/game/Core.dll"},{"loadedModuleVersionId","module1"},{"assemblyFileSha256","hash1"}}));
        if((string)pin.View()["armCommandId"]!="arm-id")throw new Exception("arm provenance lost");tests++;
        if(LeaseObservationPin.ExactId(new JObject(),"id",false)!=0 || LeaseObservationPin.ExactId(new JObject{{"id",-5}},"id",true)!=-5)throw new Exception("Optional/negative Unity IDs");tests++;
        foreach(JToken bad in new JToken[]{new JValue(3.1),new JValue("3"),new JValue(0),new JValue((object)null),new JValue(true)})
        {bool rejected=false;try{LeaseObservationPin.ExactId(new JObject{{"id",bad}},"id",true);}catch(ArgumentException){rejected=true;}if(!rejected)throw new Exception("Coerced ID");tests++;}
        Console.WriteLine(tests+" linked guard/pin tests PASS using "+typeof(JObject).Assembly.FullName);
    }
}
