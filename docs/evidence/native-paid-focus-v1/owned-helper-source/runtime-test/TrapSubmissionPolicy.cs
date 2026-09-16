using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

internal static class TrapSubmissionPolicy
{
    internal static void SameRow(object expected,object actual)
    {if(expected==null||!object.ReferenceEquals(expected,actual))throw new InvalidOperationException("Native trap DB row reference changed since observation.");}
    internal static string Key(JObject identity)
    {
        return identity["root"]+"|"+identity["session"]+"|"+identity["dungeonId"]+"|"+identity["level"]+"|"+identity["room"]+"|"+identity["trapId"];
    }
    internal static void Validate(JObject request,JObject ticket,JObject current,int frame,double realtime)
    {
        if(ticket==null || (string)request["ticketId"]!=(string)ticket["ticketId"])throw new InvalidOperationException("Latest helper-issued trap ticket required.");
        double age=realtime-(double)ticket["realtime"];
        if(double.IsNaN(age)||double.IsInfinity(age)||age<0||age>5 || frame<(int)ticket["frame"]||frame-(int)ticket["frame"]>600)
            throw new InvalidOperationException("Trap observation expired; request fresh read-only state.");
        if(!JToken.DeepEquals(request["identity"],ticket["identity"])||!JToken.DeepEquals(current["identity"],ticket["identity"]))
            throw new InvalidOperationException("Exact trap observation identity changed.");
        foreach(string gate in new[]{"scope","noModal","nativeTrap","heroQueue","nativeVote","trapActive"})
            if(current[gate]==null||current[gate].Type!=JTokenType.Boolean||!(bool)current[gate])throw new InvalidOperationException("Trap guard unavailable/false: "+gate);
        string option=(string)request["option"];
        if(option!="Disarm"&&option!="Proceed")throw new InvalidOperationException("Only explicit Disarm or Proceed allowed.");
        JObject button=current["buttons"][option]as JObject;
        if(button==null || button["usable"]==null || !(bool)button["usable"] || !JToken.DeepEquals(button,ticket["buttons"][option]))
            throw new InvalidOperationException("Exact observed native trap button unavailable/changed.");
    }
    internal static void Submit(HashSet<string> claims,string key,Action callback)
    {
        if(claims.Count>=32||!claims.Add(key))throw new InvalidOperationException("Trap room already submitted or32-claim bound reached; no retry.");
        callback(); // Claim deliberately survives any native exception.
    }
}
