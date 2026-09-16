using System;
using Newtonsoft.Json.Linq;

internal static class EnemyArrivalPolicy
{
    internal static int Identity(JObject value,string key)
    {JToken token=value[key];if(token==null||token.Type!=JTokenType.Integer)throw new InvalidOperationException("Missing integer identity: "+key);int result=(int)token;if(result==0)throw new InvalidOperationException("Zero identity: "+key);return result;}
    internal static void Match(JObject expected,JObject actual)
    {
        foreach(string key in new[]{"dummyId","celId","photonId","turnIndex"})
        {
            JToken e=expected[key],a=actual[key];if(e==null||a==null||e.Type!=JTokenType.Integer||a.Type!=JTokenType.Integer||(int)e!=(int)a)throw new InvalidOperationException("Arrival identity changed: "+key);
        }
        Identity(expected,"dummyId");Identity(expected,"celId");
    }
    internal static void MatchInit(JObject expected,JObject actual)
    {
        Identity(expected,"dummyId");
        foreach(string key in new[]{"dummyId","celId","photonId","turnIndex"})
        {JToken e=expected[key],a=actual[key];if(e==null||a==null||e.Type!=JTokenType.Integer||a.Type!=JTokenType.Integer||(int)e!=(int)a)throw new InvalidOperationException("Init token identity changed: "+key);}
        // celId zero truthfully means there was no prior CEL. It never authorizes a post-init target.
    }
    internal static void Launch(bool consumed,bool terminal,int completedInitializations,double elapsed,bool busy,JArray events,int dummyId,int celId)
    {
        if(consumed||terminal||completedInitializations!=1||double.IsNaN(elapsed)||double.IsInfinity(elapsed)||elapsed<0||elapsed>90||busy||dummyId==0||celId==0)
            throw new InvalidOperationException("Arrival launch is stale, repeated, ambiguous, expired or busy.");
        if(events==null||events.Count>32)throw new InvalidOperationException("Arrival event evidence unavailable/overflow.");
        foreach(JToken token in events)
        {
            JObject item=token as JObject;if(item==null)throw new InvalidOperationException("Invalid native event.");
            if((string)item["nativeMethod"]=="CharacterDummy.PlayAttackSequence entry" && Identity(item,"dummyId")==dummyId)
                throw new InvalidOperationException("missed-before-action: native PlayAttackSequence already entered before capture launch.");
        }
    }
    internal static JObject Snapshot(JObject value){return value==null?null:(JObject)value.DeepClone();}
}
