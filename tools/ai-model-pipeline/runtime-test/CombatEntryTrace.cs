using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static RuntimeModelTest combatEntryObserver;
    int combatEntryEvents;
    bool combatEntryQuiet;

    void ArmCombatEntryTrace()
    {
        bool trace=Environment.GetEnvironmentVariable("FTK_MODEL_TEST_COMBAT_ENTRY_TRACE")=="1";
        bool skipLease=Environment.GetEnvironmentVariable("FTK_MODEL_TEST_COMBAT_LEASE_FINALIZER_SKIP")=="1";
        if(!trace && !skipLease)return;
        if(skipLease)SkipCombatLeaseFinalizer();
        if(!trace)return;
        string[] selected=CombatEntryTraceSelection.Parse(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_COMBAT_ENTRY_TARGETS"));
        combatEntryQuiet=Environment.GetEnvironmentVariable("FTK_MODEL_TEST_COMBAT_ENTRY_QUIET")=="1";
        combatEntryObserver=this;
        WriteCombatEntry(new JObject{{"phase","configuration"},{"targets",new JArray(selected)},{"quiet",combatEntryQuiet}});
        Harmony harmony=new Harmony("com.ftkmf.runtime-model-test.combat-entry-trace");
        foreach(string name in selected)
        {
            if(name=="InitPlayerDummiesForCombat")continue;
            Type[] args=name=="InitDummyForCombat"?new[]{typeof(bool),typeof(bool)}:
                name=="CreateAvatar"?new[]{typeof(bool)}:Type.EmptyTypes;
            MethodInfo method=typeof(CharacterDummy).GetMethod(name,Members,null,args,null);
            if(method==null || method.ReturnType!=typeof(void))throw new InvalidOperationException("Combat trace signature unavailable: "+name);
            harmony.Patch(method,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("CombatEntryPrefix",Statics)),null,null,
                new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("CombatEntryFinalizer",Statics)),null);
            WriteCombatEntry(new JObject{{"phase","armed"},{"method",method.ToString()},{"patches",CombatEntryPatches(method)}});
        }
        if(Array.IndexOf(selected,"InitPlayerDummiesForCombat")<0)return;
        MethodInfo session=typeof(EncounterSession).GetMethod("InitPlayerDummiesForCombat",Members,null,new[]{typeof(bool)},null);
        if(session==null || session.ReturnType!=typeof(void))throw new InvalidOperationException("Combat player-loop trace signature unavailable");
        harmony.Patch(session,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("CombatEntryPlayersPrefix",Statics)),null,null,
            new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("CombatEntryPlayersFinalizer",Statics)),null);
        WriteCombatEntry(new JObject{{"phase","armed"},{"method",session.ToString()},{"patches",CombatEntryPatches(session)}});
    }

    void SkipCombatLeaseFinalizer()
    {
        MethodInfo original=typeof(CharacterDummy).GetMethod("CreateAvatar",Members,null,new[]{typeof(bool)},null);
        Type patchType=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.PlayerCombatMeshLeasePatch",true);
        MethodInfo target=patchType.GetMethod("Finalizer",Statics,null,new[]{typeof(CharacterDummy),typeof(Exception)},null);
        if(original==null || target==null || target.ReturnType!=typeof(Exception))
            throw new InvalidOperationException("Exact framework combat lease finalizer unavailable");
        Patches before=Harmony.GetPatchInfo(original);int matches=0;
        if(before!=null)foreach(Patch patch in before.Finalizers)
            if(patch.PatchMethod==target && patch.owner=="com.ftkmf.framework")matches++;
        if(matches!=1)throw new InvalidOperationException("Expected exactly one framework combat lease finalizer");
        WriteCombatEntry(new JObject{{"phase","lease-finalizer-skip-before"},{"patches",CombatEntryPatches(original)}});
        // Diagnostic isolation only: remove this exact method, never an owner-wide set of patches.
        new Harmony("com.ftkmf.runtime-model-test.combat-entry-trace").Unpatch(original,target);
        Patches after=Harmony.GetPatchInfo(original);
        if(after!=null)
        {
            foreach(Patch patch in after.Finalizers)
                if(patch.PatchMethod==target)throw new InvalidOperationException("Combat lease finalizer removal failed");
            if(after.Prefixes.Count!=before.Prefixes.Count || after.Postfixes.Count!=before.Postfixes.Count ||
                after.Transpilers.Count!=before.Transpilers.Count || after.Finalizers.Count!=before.Finalizers.Count-1)
                throw new InvalidOperationException("Unexpected patch set changed while removing combat lease finalizer");
        }
        else if(before.Prefixes.Count+before.Postfixes.Count+before.Transpilers.Count+before.Finalizers.Count!=1)
            throw new InvalidOperationException("Unexpected complete patch removal");
        WriteCombatEntry(new JObject{{"phase","lease-finalizer-skip-after"},{"patches",CombatEntryPatches(original)},
            {"scope","Test process only; inactive combat clone lease retention deliberately disabled. Restart without opt-in to restore."}});
    }

    static void CombatEntryPlayersPrefix(EncounterSession __instance,MethodBase __originalMethod)
    { RecordCombatPlayers("enter",__instance,__originalMethod,null); }
    static Exception CombatEntryPlayersFinalizer(EncounterSession __instance,MethodBase __originalMethod,Exception __exception)
    { RecordCombatPlayers("exit",__instance,__originalMethod,__exception);return __exception; }
    static void RecordCombatPlayers(string phase,EncounterSession session,MethodBase method,Exception exception)
    {
        RuntimeModelTest observer=combatEntryObserver;if(observer==null || observer.combatEntryQuiet)return;
        try
        {
            JArray party=new JArray();
            if(FTKHub.Instance!=null)foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
                if(cow!=null)party.Add(new JObject{{"cowId",cow.GetInstanceID()},{"turnIndex",cow.m_FTKPlayerID.m_TurnIndex},
                    {"photonId",cow.m_FTKPlayerID.m_PhotonID},{"classKey",cow.GetDBEntry()==null?null:cow.GetDBEntry().m_ID},
                    {"sourceAvatar",CombatEntryAvatar(cow.m_Avatar)},
                    {"combatAvatar",CombatEntryAvatar(cow.m_CurrentDummy==null?null:cow.m_CurrentDummy.m_EventListener)}});
            observer.WriteCombatEntry(new JObject{{"phase",phase},{"method",method.Name},{"patches",CombatEntryPatches(method)},
                {"exception",exception==null?null:exception.ToString()},{"requestedPlayers",session.m_Players==null?0:session.m_Players.Length},
                {"registeredPlayerDummies",session.m_PlayerDummies.Count},{"party",party}});
        }
        catch(Exception e){observer.Logger.LogWarning("COMBAT ENTRY TRACE player observation failed: "+e.Message);}
    }

    static JArray CombatEntryPatches(MethodBase method)
    {
        JArray result=new JArray();Patches patches=Harmony.GetPatchInfo(method);if(patches==null)return result;
        foreach(Patch patch in patches.Prefixes)result.Add(CombatEntryPatch("prefix",patch));
        foreach(Patch patch in patches.Postfixes)result.Add(CombatEntryPatch("postfix",patch));
        foreach(Patch patch in patches.Transpilers)result.Add(CombatEntryPatch("transpiler",patch));
        foreach(Patch patch in patches.Finalizers)result.Add(CombatEntryPatch("finalizer",patch));
        return result;
    }
    static JObject CombatEntryPatch(string kind,Patch patch)
    {
        return new JObject{{"kind",kind},{"owner",patch.owner},{"method",patch.PatchMethod.DeclaringType.FullName+"."+patch.PatchMethod.Name}};
    }
    static JObject CombatEntryAvatar(CharacterEventListener cel)
    {
        if(cel==null)return new JObject{{"unityNull",true}};
        Animator cached=cel.m_Animator,actual=cel.GetComponent<Animator>();
        // Avoid invoking Animator state/controller getters while investigating a native Animator crash.
        return new JObject{{"unityNull",false},{"celId",cel.GetInstanceID()},{"active",cel.gameObject.activeInHierarchy},
            {"cachedAnimatorNull",cached==null},{"actualAnimatorNull",actual==null},
            {"cachedAnimatorId",cached==null?0:cached.GetInstanceID()},{"actualAnimatorId",actual==null?0:actual.GetInstanceID()},
            {"cachedMatchesActual",cached==actual},{"sourceCowId",cel.m_CharacterOverworld==null?0:cel.m_CharacterOverworld.GetInstanceID()}};
    }
    static void CombatEntryPrefix(CharacterDummy __instance,MethodBase __originalMethod)
    { RecordCombatEntry("enter",__instance,__originalMethod,null); }
    static Exception CombatEntryFinalizer(CharacterDummy __instance,MethodBase __originalMethod,Exception __exception)
    {
        RecordCombatEntry("exit",__instance,__originalMethod,__exception);
        return __exception;
    }
    static void RecordCombatEntry(string phase,CharacterDummy dummy,MethodBase method,Exception exception)
    {
        RuntimeModelTest observer=combatEntryObserver;if(observer==null || observer.combatEntryQuiet)return;
        try
        {
            JObject item=new JObject{{"phase",phase},{"method",method.Name},{"exception",exception==null?null:exception.ToString()},
                {"dummyId",dummy==null?0:dummy.GetInstanceID()},{"patches",CombatEntryPatches(method)}};
            if(dummy!=null)
            {
                CharacterOverworld cow=dummy.m_CharacterOverworld;
                item["cowId"]=cow==null?0:cow.GetInstanceID();
                item["classKey"]=cow==null || cow.GetDBEntry()==null?null:cow.GetDBEntry().m_ID;
                item["sourceAvatar"]=CombatEntryAvatar(cow==null?null:cow.m_Avatar);
                item["combatAvatar"]=CombatEntryAvatar(dummy.m_EventListener);
            }
            observer.WriteCombatEntry(item);
        }
        catch(Exception e){observer.Logger.LogWarning("COMBAT ENTRY TRACE observation failed: "+e.Message);}
    }
    void WriteCombatEntry(JObject item)
    {
        if(combatEntryEvents>=256)return;
        item["sequence"]=++combatEntryEvents;item["session"]=sessionId;item["frame"]=Time.frameCount;
        item["utc"]=DateTime.UtcNow.ToString("o");
        string line=item.ToString(Newtonsoft.Json.Formatting.None);
        // Close after every record so the last completed boundary survives a native process crash.
        File.AppendAllText(Path.Combine(root,"model-test-combat-entry.jsonl"),line+Environment.NewLine);
        Logger.LogInfo("COMBAT ENTRY TRACE "+line);
    }
}
