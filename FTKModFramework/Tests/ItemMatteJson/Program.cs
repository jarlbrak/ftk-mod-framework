using System;
using FTKModFramework.Core.Data;
using Newtonsoft.Json;

internal static class Program
{
    private static void Check(bool valid, string name)
    { if (!valid) throw new Exception(name); }

    private static bool Parse(string matte, bool candidate, bool apparel)
    {
        string renderer = "{\"path\":\".\",\"model\":\"assets/hood.glb\",\"texture\":\"assets/hood.png\"" +
            (apparel ? ",\"nativeMesh\":\"nativeHood\"" : "") +
            (matte == null ? "" : ",\"matte\":" + matte) + "}";
        string property = apparel ? "\"apparelModels\":{\"renderers\":[" + renderer + "]}" :
            "\"itemModels\":[" + renderer + "]";
        string json = "{\"entries\":[{\"kind\":\"item\",\"id\":\"hood\"," + property + "}]}";
        ContentFile file = candidate ? JsonConvert.DeserializeObject<ContentFile>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error }) :
            JsonContentParser.Deserialize<ContentFile>(json);
        return apparel ? file.Entries[0].ApparelModels.Renderers[0].Matte : file.Entries[0].ItemModels[0].Matte;
    }

    private static void Main()
    {
        foreach (bool candidate in new[] { false, true })
        foreach (bool apparel in new[] { false, true })
        {
            Check(!Parse(null, candidate, apparel), "omitted matte must keep native finish");
            Check(!Parse("false", candidate, apparel), "false matte");
            Check(Parse("true", candidate, apparel), "true matte");
            foreach (string invalid in new[] { "null", "0", "1", "\"true\"", "{}", "[]" })
            {
                bool rejected = false;
                try { Parse(invalid, candidate, apparel); }
                catch (JsonException) { rejected = true; }
                Check(rejected, "nonboolean matte accepted: " + invalid);
            }
        }
        Console.WriteLine("PASS: rigid and apparel matte JSON requires explicit booleans");
    }
}
namespace FTKModFramework.Core.Data { internal sealed class ItemModifierEntry { } }
