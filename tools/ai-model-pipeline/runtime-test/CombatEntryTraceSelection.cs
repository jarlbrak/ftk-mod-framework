using System;
using System.Collections.Generic;

internal static class CombatEntryTraceSelection
{
    internal static string[] Parse(string selection)
    {
        string[] allowed={"InitDummyForCombat","ResetForCombat","_preAttackDummyReset","CreateAvatar","InitPlayerDummiesForCombat"};
        if(string.IsNullOrEmpty(selection))return allowed;
        string[] names=selection.Split(',');
        HashSet<string> seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(string name in names)
            if(Array.IndexOf(allowed,name)<0 || !seen.Add(name))
                throw new ArgumentException("Trace targets must be distinct exact native method names.");
        return names;
    }
}
