using System;
using Newtonsoft.Json.Linq;

internal static class EntryPreparationPolicy
{
    internal static int I(JObject o,string k){JToken t=o[k];if(t==null||t.Type!=JTokenType.Integer)throw new InvalidOperationException("Missing integer entry field: "+k);return (int)t;}
    internal static bool B(JObject o,string k){JToken t=o[k];if(t==null||t.Type!=JTokenType.Boolean)throw new InvalidOperationException("Missing boolean entry field: "+k);return (bool)t;}
    internal static string S(JObject o,string k){JToken t=o[k];if(t==null||t.Type!=JTokenType.String)throw new InvalidOperationException("Missing string entry field: "+k);return (string)t;}
    internal static void Scope(JObject state)
    {
        if(S(state,"adventure")!="HollowMire"||S(state,"dungeonKey")!="FloodedCrypt"||!B(state,"livingSingleParty")||
            !B(state,"outsideCombat")||!B(state,"outsideDungeon")||!B(state,"inSession")||B(state,"locked")||B(state,"deactivated")||
            !B(state,"targetOwnsHex")||I(state,"heroInstanceId")==0||I(state,"dungeonInstanceId")==0||I(state,"hexInstanceId")==0)
            throw new InvalidOperationException("Entry preparation requires exact living owned HollowMire/FloodedCrypt overworld scope.");
    }
    internal static void Quiescent(JObject state)
    {
        Scope(state);
        string movement=S(state,"movementState");
        if(B(state,"moving")||I(state,"syncWalkingCow")!=0||B(state,"waiting")||!B(state,"movementEnabled")||
            (movement!="Tracking"&&movement!="NoMoreActions")||B(state,"stopAtHexCheckActive")||B(state,"turnQuestCheckActive")||
            B(state,"questCheckEnabled")||!B(state,"storedCheckConsumed")||!B(state,"storedSetupConsumed")||!B(state,"storyComplete"))
            throw new InvalidOperationException("Entry preparation waits for exact quiet movement/story/native quest-check state.");
    }
    internal static void Match(JObject request,JObject state)
    {
        foreach(string key in new[]{"heroInstanceId","dungeonInstanceId","hexInstanceId","movementInstanceId","gameDefinitionIdentity"})
            if(I(request,key)!=I(state,key))throw new InvalidOperationException("Entry identity changed: "+key);
    }
    internal static void AtTarget(JObject state)
    {if(!B(state,"atTargetHex"))throw new InvalidOperationException("Position has not reached the pinned native target hex.");}
    internal static bool Ready(JObject state,int callbacks,int waitCount,bool sameContinuation)
    {
        Quiescent(state);AtTarget(state);
        return callbacks==1 && waitCount==0 && sameContinuation && B(state,"expectedQuestRegistered") && B(state,"expectedQuestDefinition") && B(state,"expectedQuestDestination");
    }
}

// Pure, bounded once-only state used by the native ticket; contains no Unity/game object references.
internal class EntryPreparationProgress
{
    internal string phase;
    internal bool valid=true,positionClaimed,discoverSubmitted;
    internal int callbacks,ignoredCallbacks,callbackFrame;
    internal float callbackTime;
    internal void ClaimPosition()
    {if(positionClaimed)throw new InvalidOperationException("Position already claimed.");positionClaimed=true;phase="position-intent";}
    internal void PositionSubmitted(){if(phase!="position-intent")throw new InvalidOperationException("Invalid position transition.");phase="position-submitted";}
    internal void ClaimDiscovery()
    {if(!valid||phase!="position-submitted"||discoverSubmitted)throw new InvalidOperationException("Discovery already claimed or position uncertain.");discoverSubmitted=true;phase="discover-intent";}
    internal void DiscoverySubmitted()
    {if(!valid||callbacks>1||phase!="discover-intent")throw new InvalidOperationException("Invalid discovery transition.");phase="discover-submitted";}
    internal void RecordCallback(int frame,float time)
    {if(!valid){ignoredCallbacks++;return;}callbacks++;callbackFrame=frame;callbackTime=time;if(callbacks>1){valid=false;phase="invalid-double-callback";}}
}
