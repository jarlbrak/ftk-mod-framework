using System;
internal static class Program
{
    static int checks;
    static void Check(bool value){checks++;if(!value)throw new Exception("Trace target selection failed");}
    static void Main()
    {
        Check(CombatEntryTraceSelection.Parse(null).Length==5);
        Check(CombatEntryTraceSelection.Parse("").Length==5);
        foreach(string name in CombatEntryTraceSelection.Parse(null))
        {string[] selected=CombatEntryTraceSelection.Parse(name);Check(selected.Length==1&&selected[0]==name);}
        string[] pair=CombatEntryTraceSelection.Parse("CreateAvatar,InitDummyForCombat");
        Check(pair.Length==2&&pair[0]=="CreateAvatar"&&pair[1]=="InitDummyForCombat");
        foreach(string bad in new[]{"CreateAvatar,CreateAvatar","createavatar","CreateAvatar,"," InitDummyForCombat","*","NoSuchMethod"})
        {bool rejected=false;try{CombatEntryTraceSelection.Parse(bad);}catch(ArgumentException){rejected=true;}Check(rejected);}
        var first=CombatEntryTraceSelection.Parse(null);first[0]="corrupt";
        Check(CombatEntryTraceSelection.Parse(null)[0]=="InitDummyForCombat");
        Console.WriteLine("PASS "+checks+" linked trace selector checks");
    }
}
