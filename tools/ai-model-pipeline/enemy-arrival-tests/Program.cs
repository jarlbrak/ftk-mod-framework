using System;
using Newtonsoft.Json.Linq;
class Program
{
    static int checks;
    static void Check(bool value){checks++;if(!value)throw new Exception("Assertion "+checks);}
    static void Reject(Action action){try{action();}catch(InvalidOperationException){checks++;return;}throw new Exception("Expected rejection "+checks);}
    static JObject Target(){return new JObject{{"dummyId",-17},{"celId",-18},{"photonId",0},{"turnIndex",3}};}
    static void Main()
    {
        var original=Target();EnemyArrivalPolicy.Match(original,Target());Check(EnemyArrivalPolicy.Identity(original,"dummyId")==-17);
        foreach(string key in new[]{"dummyId","celId","photonId","turnIndex"})
        {var altered=Target();altered[key]=(int)altered[key]+1;Reject(()=>EnemyArrivalPolicy.Match(original,altered));altered[key]="1";Reject(()=>EnemyArrivalPolicy.Match(original,altered));altered.Remove(key);Reject(()=>EnemyArrivalPolicy.Match(original,altered));}
        var initial=Target();initial["celId"]=0;EnemyArrivalPolicy.MatchInit(initial,(JObject)initial.DeepClone());Check((int)initial["celId"]==0);Reject(()=>EnemyArrivalPolicy.Match(initial,initial));
        foreach(string key in new[]{"dummyId","celId","photonId","turnIndex"}){var wrong=(JObject)initial.DeepClone();wrong[key]=(int)wrong[key]+1;Reject(()=>EnemyArrivalPolicy.MatchInit(initial,wrong));}
        var zero=Target();zero["celId"]=0;Reject(()=>EnemyArrivalPolicy.Match(zero,zero));
        EnemyArrivalPolicy.Launch(false,false,1,90,false,new JArray(),-17,-18);Check(true);
        foreach(double elapsed in new[]{-1,90.001,double.NaN,double.PositiveInfinity})Reject(()=>EnemyArrivalPolicy.Launch(false,false,1,elapsed,false,new JArray(),-17,-18));
        Reject(()=>EnemyArrivalPolicy.Launch(true,false,1,0,false,new JArray(),-17,-18));Reject(()=>EnemyArrivalPolicy.Launch(false,true,1,0,false,new JArray(),-17,-18));
        foreach(int count in new[]{0,2})Reject(()=>EnemyArrivalPolicy.Launch(false,false,count,0,false,new JArray(),-17,-18));
        Reject(()=>EnemyArrivalPolicy.Launch(false,false,1,0,true,new JArray(),-17,-18));
        var attack=new JObject{{"nativeMethod","CharacterDummy.PlayAttackSequence entry"},{"dummyId",-17},{"celId",-18},{"beforeCaptureLaunch",true}};
        Reject(()=>EnemyArrivalPolicy.Launch(false,false,1,0,false,new JArray(attack),-17,-18));
        // Stale native attack fields are deliberately absent from launch authority.
        var stale=new JObject{{"currentAttackInfo",new JObject{{"proficiencyId",123}}},{"postAttackHealthMod",999}};
        EnemyArrivalPolicy.Launch(false,false,1,0,false,new JArray(),-17,-18);Check((int)stale["postAttackHealthMod"]==999);
        var copy=EnemyArrivalPolicy.Snapshot(stale);((JObject)stale["currentAttackInfo"])["proficiencyId"]=456;Check((int)copy["currentAttackInfo"]["proficiencyId"]==123);Check(EnemyArrivalPolicy.Snapshot(null)==null);
        var overflow=new JArray();for(int i=0;i<33;i++)overflow.Add(new JObject());Reject(()=>EnemyArrivalPolicy.Launch(false,false,1,0,false,overflow,-17,-18));
        Reject(()=>EnemyArrivalPolicy.Launch(false,false,1,0,false,new JArray(1),-17,-18));
        Console.WriteLine(checks+" linked shipped-Newtonsoft arrival assertions PASS");
    }
}
