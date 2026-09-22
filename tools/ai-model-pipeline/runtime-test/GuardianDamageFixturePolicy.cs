using System;
using System.Collections.Generic;

internal sealed class GuardianDamageFixturePolicy
{
    internal readonly int Health, Damage, ExpectedHealth;
    internal readonly bool Rescue, ExpectAlive;
    internal readonly string Scenario;
    internal readonly string[] GuardianIds;
    internal readonly bool[] BeforeRescues, ExpectedRescues;
    internal string Stage = "armed";
    internal GuardianDamageFixturePolicy(string scenario, int health)
        : this(scenario,health,new[]{"guardian"},new[]{true}) { }
    internal GuardianDamageFixturePolicy(string scenario,int health,string[] guardians,bool[] rescues)
    {
        if(health<1 || health>999)throw new ArgumentException("Fixture HP must be 1..999");
        Scenario=scenario;Health=health;Rescue=scenario=="rescue" || scenario=="multi-rescue";
        bool multiple=scenario=="nonstack" || scenario=="multi-rescue";
        bool spent=scenario=="spent-rescue";ExpectAlive=!spent;
        if(scenario!="reduction" && !Rescue && !multiple && !spent)throw new ArgumentException("Unknown fixture case");
        if(!Rescue && !spent && health<=5)throw new ArgumentException("Reduction case needs more than 5 HP");
        if(guardians==null || rescues==null || guardians.Length!=(multiple?2:1) || rescues.Length!=guardians.Length)
            throw new ArgumentException("Exact guardian and rescue-state count required");
        GuardianIds=(string[])guardians.Clone();BeforeRescues=(bool[])rescues.Clone();
        Array.Sort(GuardianIds,BeforeRescues,StringComparer.Ordinal);
        for(int i=0;i<GuardianIds.Length;i++)
            if(string.IsNullOrEmpty(GuardianIds[i]) || i>0 && GuardianIds[i]==GuardianIds[i-1] || BeforeRescues[i]==spent)
                throw new ArgumentException("Unique guardian IDs with case-specific rescue states required");
        ExpectedRescues=(bool[])BeforeRescues.Clone();
        if(Rescue)ExpectedRescues[0]=false;
        Damage=Rescue || spent?health*2:9;ExpectedHealth=spent?0:Rescue?1:health-5;
    }
    internal bool MatchesPins(string[] ids,bool[] rescues)
    {
        if(ids==null || rescues==null || ids.Length!=GuardianIds.Length || rescues.Length!=ids.Length)return false;
        var observed=new Dictionary<string,bool>(StringComparer.Ordinal);
        for(int i=0;i<ids.Length;i++){if(ids[i]==null || observed.ContainsKey(ids[i]))return false;observed.Add(ids[i],rescues[i]);}
        for(int i=0;i<GuardianIds.Length;i++){bool value;if(!observed.TryGetValue(GuardianIds[i],out value) || value!=BeforeRescues[i])return false;}
        return true;
    }
    internal static void RequireSingleTarget(bool targetMatches,bool mainPresent,bool secondaryPresent,int aoeCount,bool aoeFlag)
    {
        if(!targetMatches)throw new InvalidOperationException("Native attack target differs from pinned victim");
        if(!mainPresent)throw new InvalidOperationException("Native main damage outcome is absent");
        if(secondaryPresent)throw new InvalidOperationException("Native secondary damage outcome is present");
        if(aoeCount!=0)throw new InvalidOperationException("Native attack has additional AoE targets");
        if(aoeFlag)throw new InvalidOperationException("Native main outcome is marked AoE");
    }
    internal void Retarget(){if(Stage!="armed")throw new InvalidOperationException("Fixture already targeted");Stage="targeted";}
    internal void Commit(){if(Stage!="targeted")throw new InvalidOperationException("Fixture cannot commit again");Stage="committed";}
    internal bool Complete(int health,bool alive,bool rescueAvailable,int transformedDamage,int transformedHealth)
    {return Complete(health,alive,new[]{rescueAvailable},transformedDamage,transformedHealth);}
    // Observed rescue states are passed in the policy's stable ordinal GuardianIds order.
    internal bool Complete(int health,bool alive,bool[] rescues,int transformedDamage,int transformedHealth)
    {
        if(Stage!="committed")throw new InvalidOperationException("No committed fixture");
        bool charges=rescues!=null && rescues.Length==ExpectedRescues.Length;
        if(charges)for(int i=0;i<rescues.Length;i++)if(rescues[i]!=ExpectedRescues[i])charges=false;
        bool pass=health==ExpectedHealth && alive==ExpectAlive && charges &&
            transformedDamage==Health-ExpectedHealth && transformedHealth==ExpectedHealth;
        Stage=pass?"passed":"failed";return pass;
    }
    internal void Abort(){if(Stage!="passed" && Stage!="failed")Stage="aborted";}
    internal bool Active{get{return Stage=="armed" || Stage=="targeted" || Stage=="committed";}}
}
