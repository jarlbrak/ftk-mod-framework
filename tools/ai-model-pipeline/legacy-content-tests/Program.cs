using System;
using Newtonsoft.Json.Linq;
class Program
{
    static int count;
    static JObject Profile() { return JObject.Parse("{bindingKind:'legacy-singular',tint:[0.85,0.95,1,1],renderers:[{rendererPath:'enFishA',glbFile:'reefstrider.glb'}]}"); }
    static void Reject(Action<JObject> change)
    {
        JObject p = Profile(); change(p);
        try { LegacyBodyFixture.Validate(p); } catch (InvalidOperationException) { count++; return; }
        throw new Exception("Invalid fixture accepted");
    }
    static void Main()
    {
        LegacyBodyFixture.Validate(Profile()); count++;
        LegacyBodyFixture.Validate(JObject.Parse("{renderers:[{}]}")); count++;
        Reject(p => p["bindingKind"] = "plural");
        Reject(p => p["bindingKind"] = new JValue((object)null));
        Reject(p => p.Remove("bindingKind"));
        Reject(p => p.Remove("tint"));
        Reject(p => p["tint"] = new JArray(1,1,1));
        Reject(p => p["tint"][0] = -0.1);
        Reject(p => p["tint"][0] = 1.1);
        Reject(p => p["tint"][0] = "1");
        Reject(p => p["tint"][0] = double.NaN);
        Reject(p => p["tint"][0] = double.PositiveInfinity);
        Reject(p => ((JArray)p["renderers"]).Add(new JObject()));
        Reject(p => p["renderers"][0]["disableNativeEmission"] = false);
        Reject(p => p["renderers"][0]["materialSlots"] = new JArray());
        Reject(p => p["resourcePrefab"] = "fishA01");
        Reject(p => p["portraitMarkerPath"] = "Head");
        Console.WriteLine("PASS " + count + " legacy content route validation assertions");
    }
}
