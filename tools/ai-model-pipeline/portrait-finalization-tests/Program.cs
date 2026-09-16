using System;
using System.IO;
using Newtonsoft.Json.Linq;

static class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static JObject Copy(JObject value){return (JObject)value.DeepClone();}
    static void Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("Supply actual trace and request JSON paths.");
        JObject report=JObject.Parse(File.ReadAllText(args[0])),request=JObject.Parse(File.ReadAllText(args[1]));
        JArray records=(JArray)report["records"];Check(records.Count==1,"Expected bounded actual single-record rejection fixture");
        JObject original=(JObject)records[0];
        Check((string)report["watchSession"]==(string)request["session"],"Actual session identity");
        Check((string)report["armCommandId"]==(string)request["traceArmCommandId"],"Actual trace arm identity");
        Check((int)original["frame"]==(int)request["traceFrame"] && (string)original["row"]==(string)request["enemy"],"Actual frame/row identity");
        Check((int)original["textureRequested"]["instanceId"]==(int)request["expectedTextureId"] && (int)original["textureCurrent"]["instanceId"]==(int)request["expectedTextureId"],"Actual texture identity");
        Check((int)report["telemetryErrors"]==0 && (bool)original["telemetryComplete"],"Actual complete/no-error trace");
        JObject legacy=Copy(original);Exception noException=null;
        legacy["nativeSnapshotException"]=noException==null?null:noException.GetType().FullName;
        Check(legacy["nativeSnapshotException"].Type==JTokenType.String && ((JValue)legacy["nativeSnapshotException"]).Value==null,"Reproduce shipped implicit string-null bug");
        Check(JObject.Parse(legacy.ToString())["nativeSnapshotException"].Type==JTokenType.Null,"Serialization conceals in-memory type mismatch");
        Check(!PortraitFinalization.Successful(original) && !PortraitFinalization.Successful(legacy),"Legacy/unstamped records fail closed");
        JObject success=Copy(original);PortraitFinalization.Complete(success,null);
        Check(PortraitFinalization.Successful(success),"Live successful-finalizer token accepted");
        Check(PortraitFinalization.Successful(Copy(success)),"DeepClone used by telemetry preserves accepted contract");
        Check(PortraitFinalization.Successful(JObject.Parse(success.ToString())),"JSON roundtrip preserves accepted contract");
        JObject failed=Copy(original);PortraitFinalization.Complete(failed,new InvalidOperationException("native failure"));
        Check(!PortraitFinalization.Successful(failed) && (string)failed["nativeSnapshotException"]==typeof(InvalidOperationException).FullName,"Real native exception rejected");
        Check(!PortraitFinalization.Successful(JObject.Parse(failed.ToString())),"Exception remains rejected after serialization");
        foreach(string field in new[]{"nativeSnapshotFinalized","nativeSnapshotException","telemetryComplete"})
        {JObject missing=Copy(success);missing.Remove(field);Check(!PortraitFinalization.Successful(missing),"Missing field rejected: "+field);}
        foreach(JToken marker in new JToken[]{new JValue(false),new JValue("true"),new JValue(1),new JValue((object)null)})
        {JObject malformed=Copy(success);malformed["nativeSnapshotFinalized"]=marker;Check(!PortraitFinalization.Successful(malformed),"Malformed/false finalized rejected");}
        JObject incomplete=Copy(success);incomplete["telemetryComplete"]=false;Check(!PortraitFinalization.Successful(incomplete),"Incomplete snapshot rejected");
        JObject ambiguous=Copy(success);ambiguous["nativeSnapshotException"]=new JValue((string)null);Check(!PortraitFinalization.Successful(ambiguous),"Ambiguous string-null remains rejected even with marker");
        Check(!PortraitFinalization.Successful(null),"Absent record rejected");
        Console.WriteLine("PASS "+checks+" linked shipped-Newtonsoft assertions; "+typeof(JObject).Assembly.FullName);
    }
}
