using System;
using System.IO;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    const string GloamfinCapturePlanFile="gloamfin-four-scenario-capture-plan-v1.json";
    const string GloamfinCapturePlanHash="1229bb59766cc1b5e47bdcb21448d85adeb07290df0c8c60dd1b450b07dbe21b";
    JObject KrakenCapturePlan(string variant,string scenario,string requestedHash,JObject asset)
    {
        if(scenario=="appear")
        {
            if(requestedHash!=null)throw new ArgumentException("Appearance retains its original framing and does not accept a companion plan.");
            return null;
        }
        KrakenScenarioTrigger(scenario);
        if(variant!="gloamfin-v1" || requestedHash!=GloamfinCapturePlanHash)throw new ArgumentException("Nonappearance skin requires exact organic companion plan.");
        string path=KrakenSkinFile(GloamfinCapturePlanFile);
        if(CatalogHash(path)!=GloamfinCapturePlanHash)throw new InvalidOperationException("Companion capture plan hash changed.");
        JObject plan=JObject.Parse(File.ReadAllText(path));
        if((string)plan["schema"]!="ftkmf.gloamfin.capture-plan.v1" || (string)plan["variant"]!=variant
            || (string)plan["sourceResourcesSha256"]!=KrakenControllerAssets
            || (string)plan["assetManifest"]["sha256"]!=GloamfinSkinManifestHash || (string)plan["assetManifest"]["file"]!=GloamfinSkinManifest
            || !JToken.DeepEquals(plan["glb"],asset["glb"]) || !JToken.DeepEquals(plan["png"],asset["png"]))
            throw new InvalidOperationException("Companion plan original asset identity differs.");
        JArray scenarios=plan["scenarios"]as JArray;if(scenarios==null || scenarios.Count!=4)throw new InvalidOperationException("Four fixed companion scenarios required.");
        JObject selected=null;foreach(JObject item in scenarios)if((string)item["scenario"]==scenario){if(selected!=null)throw new InvalidOperationException("Duplicate companion scenario.");selected=item;}
        if(selected==null)throw new InvalidOperationException("Missing exact companion scenario.");
        return plan;
    }
    static JObject KrakenCaptureScenario(JObject plan,string scenario)
    {foreach(JObject item in (JArray)plan["scenarios"])if((string)item["scenario"]==scenario)return item;throw new InvalidOperationException("Missing pinned scenario.");}
    static int[] KrakenCaptureSteps(JToken token,int count)
    {
        JArray array=token as JArray;if(array==null || array.Count!=count)throw new InvalidOperationException("Fixed bounded capture count required.");
        int[] steps=new int[count];for(int i=0;i<count;i++){if(array[i].Type!=JTokenType.Integer)throw new InvalidOperationException("Integer step required.");steps[i]=(int)array[i];if(steps[i]<0 || steps[i]>240 || (i>0 && steps[i]<=steps[i-1]))throw new InvalidOperationException("Ordered bounded steps required.");}return steps;
    }
}
