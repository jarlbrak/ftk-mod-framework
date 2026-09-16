using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
internal static class PaidFocusPolicy
{
    internal static void SameWeaponRow(object expected,object actual)
    {if(expected==null||!object.ReferenceEquals(expected,actual))throw new InvalidOperationException("Native weapon table reference changed.");}
    internal static string Key(JObject id)
    {
        string value="";foreach(string field in new[]{"root","session","dungeonId","level","room","esId","mcId","turn","heroId"})value+="|"+id[field];return value;
    }
    internal static void Validate(JObject request,JObject ticket,JObject now,int frame,double realtime)
    {
        if(ticket==null||(string)request["ticketId"]!=(string)ticket["ticketId"])throw new InvalidOperationException("Latest paid-focus ticket required.");
        double age=realtime-(double)ticket["realtime"];
        if(double.IsNaN(age)||double.IsInfinity(age)||age<0||age>5||frame<(int)ticket["frame"]||frame-(int)ticket["frame"]>600)throw new InvalidOperationException("Paid-focus ticket expired.");
        foreach(string field in new[]{"identity","pins","availableFocus","spentFocus"})
            if(!JToken.DeepEquals(ticket[field],now[field]))throw new InvalidOperationException("Paid-focus observation changed: "+field);
        if(!JToken.DeepEquals(request["identity"],ticket["identity"]))throw new InvalidOperationException("Exact observed identity required.");
        foreach(string gate in new[]{"scope","noModal","normalAttack","inputReady","canPay","stanceReady"})
            if(now[gate]==null||now[gate].Type!=JTokenType.Boolean||!(bool)now[gate])throw new InvalidOperationException("Paid-focus guard false/unavailable: "+gate);
    }
    internal static void Submit(HashSet<string> claims,string key,Action invoke)
    {if(claims.Count>=32||!claims.Add(key))throw new InvalidOperationException("Already submitted this native turn or claim bound reached.");invoke();}
    internal static string CompletionStatus(string previous,int callbackCount,string classification,bool observationFailed)
    {
        if(callbackCount>1)return "duplicate-native-callback";
        if(previous!="submitted")return previous;
        return observationFailed?"callback-observation-error":classification;
    }
    internal static string Payment(JObject baseline,JObject before,JObject after,bool exception)
    {
        if(exception)return "native-callback-exception";
        if(!JToken.DeepEquals(baseline["identity"],before["identity"])||!JToken.DeepEquals(before["identity"],after["identity"]))return "callback-identity-changed";
        foreach(JObject sample in new[]{before,after})foreach(string gate in new[]{"scope","normalAttack","stanceReady"})
            if(sample[gate]==null||sample[gate].Type!=JTokenType.Boolean||!(bool)sample[gate])return "callback-native-scope-changed";
        if((int)baseline["availableFocus"]!=(int)before["availableFocus"]||(int)baseline["spentFocus"]!=(int)before["spentFocus"])return "callback-baseline-changed";
        if((bool)before["interrupt"])return "native-payment-interrupted";
        if(!(bool)before["focusing"]||(bool)after["focusing"])return "callback-focusing-mismatch";
        return (int)after["availableFocus"]==(int)before["availableFocus"]-1&&(int)after["spentFocus"]==(int)before["spentFocus"]+1?"payment-complete":"native-payment-delta-mismatch";
    }
}
