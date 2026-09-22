using System;
internal static class Program
{
    static int checks;
    static void Check(bool value){checks++;if(!value)throw new Exception("Guardian fixture policy failure");}
    static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}Check(rejected);}
    static void Main()
    {
        GuardianDamageFixturePolicy.RequireSingleTarget(true,true,false,0,false);Check(true);
        Reject(()=>GuardianDamageFixturePolicy.RequireSingleTarget(false,true,false,0,false));
        Reject(()=>GuardianDamageFixturePolicy.RequireSingleTarget(true,false,false,0,false));
        Reject(()=>GuardianDamageFixturePolicy.RequireSingleTarget(true,true,true,0,false));
        Reject(()=>GuardianDamageFixturePolicy.RequireSingleTarget(true,true,false,1,false));
        Reject(()=>GuardianDamageFixturePolicy.RequireSingleTarget(true,true,false,0,true));
        foreach(int hp in new[]{6,31,999})
        {
            var p=new GuardianDamageFixturePolicy("reduction",hp);Check(p.Damage==9&&p.ExpectedHealth==hp-5);Reject(()=>p.Commit());
            p.Retarget();Reject(()=>p.Retarget());p.Commit();Reject(()=>p.Commit());
            Check(p.Complete(hp-5,true,true,5,hp-5)&&p.Stage=="passed"&&!p.Active);Reject(()=>p.Complete(hp-5,true,true,5,hp-5));
        }
        foreach(int hp in new[]{1,32,999})
        {
            var p=new GuardianDamageFixturePolicy("rescue",hp);Check(p.Damage==2*hp&&p.ExpectedHealth==1);
            p.Retarget();p.Commit();Check(p.Complete(1,true,false,hp-1,1));
        }
        foreach(int hp in new[]{0,1000})Reject(()=>new GuardianDamageFixturePolicy("rescue",hp));
        Reject(()=>new GuardianDamageFixturePolicy("reduction",5));Reject(()=>new GuardianDamageFixturePolicy("other",10));
        foreach(string stage in new[]{"armed","targeted","committed"})
        {var p=new GuardianDamageFixturePolicy("reduction",10);if(stage!="armed")p.Retarget();if(stage=="committed")p.Commit();p.Abort();Check(!p.Active&&p.Stage=="aborted");Reject(()=>p.Commit());}
        var bad=new GuardianDamageFixturePolicy("reduction",20);bad.Retarget();bad.Commit();Check(!bad.Complete(15,true,true,9,15)&&bad.Stage=="failed");
        var wrongCharge=new GuardianDamageFixturePolicy("rescue",20);wrongCharge.Retarget();wrongCharge.Commit();Check(!wrongCharge.Complete(1,true,true,19,1));
        var nonstack=new GuardianDamageFixturePolicy("nonstack",20,new[]{"2:1","1:1"},new[]{true,true});
        Check(nonstack.GuardianIds[0]=="1:1" && nonstack.Damage==9 && nonstack.ExpectedHealth==15);
        Check(nonstack.MatchesPins(new[]{"2:1","1:1"},new[]{true,true}));
        Check(!nonstack.MatchesPins(new[]{"2:1","1:1"},new[]{false,true}));
        Check(!nonstack.MatchesPins(new[]{"1:1","1:1"},new[]{true,true}));
        Check(!nonstack.MatchesPins(new[]{"1:1"},new[]{true}));
        nonstack.Retarget();nonstack.Commit();Check(nonstack.Complete(15,true,new[]{true,true},5,15));
        var stacked=new GuardianDamageFixturePolicy("nonstack",20,new[]{"1:1","2:1"},new[]{true,true});
        stacked.Retarget();stacked.Commit();Check(!stacked.Complete(17,true,new[]{true,true},3,17));
        var multi=new GuardianDamageFixturePolicy("multi-rescue",20,new[]{"2:1","1:1"},new[]{true,true});
        Check(multi.ExpectedRescues[0]==false && multi.ExpectedRescues[1]);
        multi.Retarget();multi.Commit();Check(multi.Complete(1,true,new[]{false,true},19,1));
        foreach(bool[] wrong in new[]{new[]{true,false},new[]{false,false},new[]{true,true}})
        {
            var p=new GuardianDamageFixturePolicy("multi-rescue",20,new[]{"1:1","2:1"},new[]{true,true});
            p.Retarget();p.Commit();Check(!p.Complete(1,true,wrong,19,1));
        }
        foreach(int hp in new[]{1,31,999})
        {
            var p=new GuardianDamageFixturePolicy("spent-rescue",hp,new[]{"1:1"},new[]{false});
            Check(p.Damage==hp*2 && p.ExpectedHealth==0 && !p.ExpectAlive);
            p.Retarget();p.Commit();Check(p.Complete(0,false,new[]{false},hp,0));
        }
        var recharged=new GuardianDamageFixturePolicy("spent-rescue",31,new[]{"1:1"},new[]{false});
        recharged.Retarget();recharged.Commit();Check(!recharged.Complete(1,true,new[]{false},30,1));
        Reject(()=>new GuardianDamageFixturePolicy("nonstack",20,new[]{"1:1"},new[]{true}));
        Reject(()=>new GuardianDamageFixturePolicy("nonstack",20,new[]{"1:1","1:1"},new[]{true,true}));
        Reject(()=>new GuardianDamageFixturePolicy("multi-rescue",20,new[]{"1:1","2:1"},new[]{true,false}));
        Reject(()=>new GuardianDamageFixturePolicy("spent-rescue",20,new[]{"1:1"},new[]{true}));
        Reject(()=>new GuardianDamageFixturePolicy("spent-rescue",20,new[]{"1:1","2:1"},new[]{false,false}));
        Console.WriteLine("PASS "+checks+" linked one-shot Guardian fixture policy checks");
    }
}
