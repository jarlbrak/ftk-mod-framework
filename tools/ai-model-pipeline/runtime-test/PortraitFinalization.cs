using System;
using Newtonsoft.Json.Linq;

// Pure JSON contract shared by native finalizer, readback guard, and shipped-JSON regression tests.
internal static class PortraitFinalization
{
    internal static void Complete(JObject record,Exception exception)
    {
        if(record==null)return;
        // In shipped Newtonsoft, implicit string-null conversion creates Type.String with null Value.
        // Construct object-null explicitly so the live token and serialized/reparsed token agree.
        record["nativeSnapshotException"]=exception==null?new JValue((object)null):new JValue(exception.GetType().FullName);
        record["nativeSnapshotFinalized"]=true;
    }
    internal static bool Successful(JObject record)
    {
        return record!=null && record["telemetryComplete"]!=null && record["telemetryComplete"].Type==JTokenType.Boolean && (bool)record["telemetryComplete"]
            && record["nativeSnapshotFinalized"]!=null && record["nativeSnapshotFinalized"].Type==JTokenType.Boolean && (bool)record["nativeSnapshotFinalized"]
            && record["nativeSnapshotException"]!=null && record["nativeSnapshotException"].Type==JTokenType.Null;
    }
}
