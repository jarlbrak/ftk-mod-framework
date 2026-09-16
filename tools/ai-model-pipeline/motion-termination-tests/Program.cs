using System;
using System.IO;
using Newtonsoft.Json.Linq;

static class Program
{
    static int checks;
    static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
    static JObject Death()
    {
        return new JObject { { "nativeMethod", "CharacterEventListener.CombatTrigger entry" },
            { "trigger", "Death" }, { "finalized", true }, { "exception", new JValue((object)null) } };
    }
    static bool Accept(JObject item,Exception error=null)
    {return CombatMotionTermination.AfterObservedDeath(new JArray(item),error??new AvatarControllerResolutionException());}
    static void Main()
    {
        Check(Accept(Death()),"Successful finalized native Death allows controller-loss termination");
        Check(!CombatMotionTermination.AfterObservedDeath(new JArray(),new AvatarControllerResolutionException()),"Pre-death absence fails closed");
        Check(!CombatMotionTermination.AfterObservedDeath(null,new AvatarControllerResolutionException()),"Missing observation fails closed");
        Check(!Accept(Death(),new InvalidOperationException("Avatar controller ambiguous or absent.")),"Same text from unrelated failure is not accepted");
        Check(!Accept(Death(),new Exception("Renderer changed")),"Identity failure is not accepted");
        var item=Death();item["finalized"]=false;Check(!Accept(item),"Unfinalized trigger is insufficient");
        item=Death();item.Remove("finalized");Check(!Accept(item),"Missing finalization is insufficient");
        item=Death();item["exception"]="native failure";Check(!Accept(item),"Failed native Death is insufficient");
        item=Death();item.Remove("exception");Check(!Accept(item),"Absent exception evidence is insufficient");
        item=Death();item["exception"]=new JValue((string)null);Check(!Accept(item),"Old Newtonsoft string-null token is not successful finalization");
        foreach(string trigger in new[]{"Damaged","2HandWield_Death","DeathLight","death"})
        {item=Death();item["trigger"]=trigger;Check(!Accept(item),"Only exact native Death is sufficient: "+trigger);}
        item=Death();item["nativeMethod"]="CharacterDummy.PlayAttackSequence entry";Check(!Accept(item),"Attack entry is not native Death observation");
        Check(CombatMotionTermination.AfterObservedDeath(new JArray(item,Death()),new AvatarControllerResolutionException()),"Earlier unrelated events do not hide exact Death");

        // Integration boundary checks complement the linked actual policy tests above.
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../runtime-test"));
        string source=File.ReadAllText(Path.Combine(root,"CombatMotionObservation.cs"));
        source=source.Substring(source.IndexOf("bool CheckCombatMotionObservation"));
        int resolve=source.IndexOf("try { current = Controller(renderer); }");
        Check(source.IndexOf("renderer.GetInstanceID() != arm.rendererId")<resolve && source.IndexOf("RequireMotionArmExact(arm);")<resolve,"Renderer and native arm identities validated before terminal exception");
        Check(source.Contains("if (!ReferenceEquals(current, arm.animator))"),"Resolved replacement animator remains rejected");
        string capture=File.ReadAllText(Path.Combine(root,"Plugin.cs"));
        int stop=capture.IndexOf("if(CheckCombatMotionObservation(motion,renderer))");
        Check(stop>=0 && stop<capture.IndexOf("JObject pose=Snapshot(renderer,false)",stop),"Terminal frame stops before controller-dependent snapshot");
        Check(capture.Contains("{error=\"Controller resolution unavailable after observed native Death.\";break;}"),"Terminal prefix keeps raw capture unsuccessful");
        Console.WriteLine("PASS "+checks+" death-termination checks (linked policy and source integration; no live proof).");
    }
}
