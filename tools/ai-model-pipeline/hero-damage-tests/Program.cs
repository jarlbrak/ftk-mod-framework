using System;
using System.IO;

static class Program
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Reject(Action action,string label){bool rejected=false;try{action();}catch(Exception){rejected=true;}Check(rejected,label);}
    static void Main()
    {
        foreach(int target in new[]{0,10,101,int.MaxValue})Reject(()=>new HeroDamageFixturePolicy(0,10,target),"Invalid requested target rejected");
        Reject(()=>new HeroDamageFixturePolicy(0,10,61),"More than fifty augmentation rejected");
        Reject(()=>new HeroDamageFixturePolicy(-1,10,30),"Negative baseline rejected");
        int field=0,calls=0;bool exact=true;
        Func<int> read=()=>field;Action<int> augment=delta=>{field+=delta;calls++;};Func<int> maximum=()=>10+field;
        Action guard=()=>{if(!exact)throw new Exception("identity drift");};
        var plan=new HeroDamageFixturePolicy(0,10,30);
        plan.Apply(read,augment,maximum,guard);
        Check(field==20&&calls==1&&plan.restoreRequired,"One native augmentation meets requested minimum");
        Reject(()=>plan.Apply(read,augment,maximum,guard),"Repeated apply rejected");
        Check(calls==1,"Repeated apply does not mutate");
        plan.Restore(read,augment,maximum,guard);
        Check(field==0&&maximum()==10&&!plan.restoreRequired&&calls==2,"Exact inverse restores original native maximum");
        Reject(()=>plan.Restore(read,augment,maximum,guard),"Repeated restoration rejected");

        plan=new HeroDamageFixturePolicy(0,10,30);
        Reject(()=>plan.Apply(read,augment,()=>10+field/2,guard),"Under-target native scaling rejected");
        Check(field==0&&!plan.restoreRequired,"Under-target readback rolls back");
        plan=new HeroDamageFixturePolicy(0,10,30);
        Reject(()=>plan.Apply(read,augment,()=>field==0?10:101,guard),"Excess native multiplier rejected");
        Check(field==0&&!plan.restoreRequired,"Over-limit readback rolls back");
        plan=new HeroDamageFixturePolicy(0,10,30);
        Reject(()=>plan.Apply(read,delta=>{field+=delta;if(delta>0)throw new Exception("native method threw after write");},maximum,guard),"Native partial failure propagated");
        Check(field==0&&!plan.restoreRequired,"Native partial write restored via inverse");
        plan=new HeroDamageFixturePolicy(0,10,30);
        plan.Apply(read,augment,maximum,guard);field=21;int beforeCalls=calls;
        Reject(()=>plan.Restore(read,augment,maximum,guard),"Unexpected field drift refuses restoration");
        Check(field==21&&calls==beforeCalls&&plan.restoreRequired,"Drift preserved and receipt remains pending");
        field=20;exact=false;
        Reject(()=>plan.Restore(read,augment,maximum,guard),"Owner/weapon/focus drift refuses restoration");
        Check(field==20&&calls==beforeCalls&&plan.restoreRequired,"Identity drift cannot mutate stats");
        exact=true;plan.Restore(read,augment,maximum,guard);
        Check(field==0&&!plan.restoreRequired,"Same receipt can restore after exact authority returns");
        plan=new HeroDamageFixturePolicy(0,10,30);
        Reject(()=>plan.Apply(read,delta=>{field+=delta;exact=false;},maximum,guard),"Post-write authority loss stops rollback");
        Check(field==20&&plan.restoreRequired,"Unproven rollback retains receipt rather than overwriting");

        field=0;exact=true;plan=new HeroDamageFixturePolicy(0,10,30);
        plan.Apply(read,augment,maximum,guard);
        Reject(()=>plan.Restore(read,delta=>{field+=delta;throw new Exception("native inverse failed after write");},maximum,guard),"Throwing native inverse remains uncertain");
        Check(field==0&&plan.restoreRequired&&plan.nativeRestoreUncertain,"Baseline value cannot hide incomplete native restore");
        beforeCalls=calls;
        Reject(()=>plan.Restore(read,augment,maximum,guard),"Second restore cannot claim success from baseline field");
        Check(plan.restoreRequired&&plan.nativeRestoreUncertain&&calls==beforeCalls,"Uncertain native restoration stays latched without mutation");

        field=0;exact=true;int level=0;
        var progressed=new HeroDamageFixturePolicy(0,10,33);
        Func<int> leveledMaximum=()=>10+level+field;
        progressed.Apply(read,augment,leveledMaximum,guard);
        Check(field==23&&leveledMaximum()==33,"Bramblecoil original augmentation and maximum reproduced");
        level=2;
        HeroDamageFixturePolicy.RequireLevelProgression(0,0,2,25,new[]{10,20,30,40});
        Check(leveledMaximum()==35,"Native two-level weapon gain changes current maximum to35");
        progressed.Restore(read,augment,leveledMaximum,guard,12);
        Check(field==0&&leveledMaximum()==12&&!progressed.restoreRequired,"Restoration removes only23 augmentation and retains native level gain");
        HeroDamageFixturePolicy.RequireLevelProgression(0,0,0,9,new[]{10,20,30});
        checks++;
        Reject(()=>HeroDamageFixturePolicy.RequireLevelProgression(2,25,1,25,new[]{10,20,30}),"Level regression rejected");
        Reject(()=>HeroDamageFixturePolicy.RequireLevelProgression(0,5,2,4,new[]{10,20,30}),"XP regression rejected");
        Reject(()=>HeroDamageFixturePolicy.RequireLevelProgression(0,0,2,11,new[]{10,20,30}),"Level increase without matching XP rejected");
        field=0;level=0;progressed=new HeroDamageFixturePolicy(0,10,33);progressed.Apply(read,augment,leveledMaximum,guard);level=2;
        Reject(()=>progressed.Restore(read,augment,leveledMaximum,guard),"Original strict rollback expectation is unchanged without explicit level baseline");
        Check(progressed.restoreRequired,"Wrong native maximum cannot clear receipt");

        string source=File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../runtime-test/HeroDamageFixture.cs")));
        Check(source.Contains("AugmentCharacterOther(FTK_miniEncounter.TrainerType.PhysDmg,delta)"),"Native augmentation and inverse only");
        Check(source.Contains("receipt.token")&&source.Contains("ReferenceEquals(hero.m_CharacterStats,receipt.stats)"),"Exact receipt and stats authority guarded");
        Check(source.Contains("ReferenceEquals(receipt.cel.m_Weapon,receipt.nativeWeapon)")&&source.Contains("ReferenceEquals(FTK_weaponStats2DB.GetDB().GetEntry(hero.m_WeaponID),receipt.weaponRow)"),"Native weapon object and exact DB row guarded");
        Check(source.Contains("stats.SpentFocus!=0")&&source.Contains("JToken.DeepEquals(current,expected)"),"Zero-focus and immutable authority checked");
        Check(source.Contains("if(allowLevelProgression)")&&source.Contains("RequireLevelProgression((int)expected[\"playerLevel\"]"),"Level allowance is explicit and XP-validated");
        foreach(string fixedField in new[]{"rawSkill","focusPoints","rawPhysicalModifier","rawAllDamageModifier","weaponBaseDamage","weaponDamageGain","controllerId"})
            Check(!source.Contains("expected[\""+fixedField+"\"]="),"Level restoration cannot normalize unrelated field: "+fixedField);
        Check(source.Contains("()=>ExactDamageReceipt(pending)")&&source.Contains("()=>ExactDamageReceipt(receipt,true),expectedMaximum"),"Apply remains exact; only explicit restore receives progressed baseline");
        foreach(string forbidden in new[]{"EquipItem(","SetTrigger(",".Play(","Random.","m_CurrentHealth=","m_AugmentedDamagePhysical="})
            Check(!source.Contains(forbidden),"No direct weapon/animation/RNG/enemy or raw damage-field write: "+forbidden);
        Console.WriteLine("PASS "+checks+" hero damage transaction checks (linked policy and source guards; no live proof).");
    }
}
