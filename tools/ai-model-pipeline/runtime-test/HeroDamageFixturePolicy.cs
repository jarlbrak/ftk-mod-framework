using System;

// Native stat mutation is supplied by the helper; tests exercise the same transaction.
internal sealed class HeroDamageFixturePolicy
{
    internal readonly int before, applied, minimum, maximumBefore;
    internal bool restoreRequired;
    internal bool nativeRestoreUncertain;
    internal HeroDamageFixturePolicy(int augmentation,int nativeMaximum,int requestedMinimum)
    {
        if (requestedMinimum < 1 || requestedMinimum > 100 || nativeMaximum < 1 || nativeMaximum >= requestedMinimum)
            throw new ArgumentException("Requested minimum native weapon damage must exceed current damage and be 1..100.");
        int delta=requestedMinimum-nativeMaximum;
        if (delta>50 || augmentation<0 || augmentation>100)throw new ArgumentException("Physical augmentation fixture exceeds conservative bounds.");
        before=augmentation;applied=checked(augmentation+delta);minimum=requestedMinimum;maximumBefore=nativeMaximum;
    }
    internal void Apply(Func<int> read,Action<int> augment,Func<int> maximum,Action exact)
    {
        exact();if(restoreRequired || read()!=before)throw new InvalidOperationException("Hero physical augmentation changed before apply.");
        restoreRequired=true;
        try
        {
            augment(applied-before);exact();
            if(read()!=applied || maximum()<minimum || maximum()>100)
                throw new InvalidOperationException("Native damage readback did not meet the bounded requested minimum.");
        }
        catch(Exception failure)
        {
            try{Restore(read,augment,maximum,exact);}
            catch(Exception rollback){throw new InvalidOperationException("Damage fixture failed and restoration remains pending: "+rollback.Message,failure);}
            throw;
        }
    }
    internal void Restore(Func<int> read,Action<int> augment,Func<int> maximum,Action exact,int? expectedMaximum=null)
    {
        if(!restoreRequired)throw new InvalidOperationException("No pending damage restoration.");
        if(nativeRestoreUncertain)throw new InvalidOperationException("Prior native restoration threw; baseline field alone cannot prove SyncMember/stat recomputation completed.");
        exact();int current=read();
        if(current!=before && current!=applied)throw new InvalidOperationException("Physical augmentation drifted; refusing to overwrite it.");
        if(current==applied)
        {
            try{augment(before-applied);}
            catch{nativeRestoreUncertain=true;throw;}
        }
        exact();
        if(read()!=before || maximum()!=(expectedMaximum??maximumBefore))throw new InvalidOperationException("Native damage baseline was not restored.");
        restoreRequired=false;
    }
    internal static void RequireLevelProgression(int oldLevel,int oldXp,int level,int xp,int[] thresholds)
    {
        if(level<oldLevel || xp<oldXp)throw new InvalidOperationException("Hero level or XP regressed.");
        if(level==oldLevel)return;
        // Mirrors CharacterStats.Update: first XP threshold greater than current XP.
        int nativeLevel=0;
        for(int i=0;i<thresholds.Length;i++)if(xp<thresholds[i]){nativeLevel=i;break;}
        if(level!=nativeLevel)throw new InvalidOperationException("Hero level progression does not match native XP thresholds.");
    }
}
