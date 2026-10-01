using System;
using FTKModFramework.Core.Data;
using Newtonsoft.Json;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new Exception(name); }
    private static ContentFile Parse(string metadata, bool candidate)
    {
        string json = "{\"entries\":[{\"kind\":\"item\",\"id\":\"hood\",\"helmetHairVisibility\":" + metadata + "}]}";
        return candidate ? JsonConvert.DeserializeObject<ContentFile>(json,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error })
            : JsonContentParser.Deserialize<ContentFile>(json);
    }
    private static void Main()
    {
        foreach (bool candidate in new[] { false, true })
        {
            foreach (bool top in new[] { false, true })
            foreach (bool bottom in new[] { false, true })
            {
                var hair = Parse("{\"top\":" + top.ToString().ToLowerInvariant() + ",\"bottom\":" + bottom.ToString().ToLowerInvariant() + "}", candidate).Entries[0].HelmetHairVisibility;
                Check(hair.Top == top && hair.Bottom == bottom, "literal booleans preserved");
            }
            foreach (string invalid in new[] { "\"true\"", "\"false\"", "1", "0", "1.0", "null", "{}", "[]" })
            foreach (bool top in new[] { false, true })
            {
                string metadata = top ? "{\"top\":" + invalid + ",\"bottom\":true}" : "{\"top\":true,\"bottom\":" + invalid + "}";
                bool rejected = false;
                try { Parse(metadata, candidate); } catch (JsonException) { rejected = true; }
                Check(rejected, "invalid token rejected in both parser paths: " + metadata);
            }
            foreach (string missing in new[] { "{}", "{\"top\":true}", "{\"bottom\":false}" })
            {
                bool rejected = false;
                try { Parse(missing, candidate); } catch (JsonException) { rejected = true; }
                Check(rejected, "missing required section rejected");
            }
            Check(Parse("null", candidate).Entries[0].HelmetHairVisibility == null, "null optional metadata preserves native flags");
        }
        Check(JsonContentParser.Deserialize<ContentFile>("{\"entries\":[{\"kind\":\"item\",\"id\":\"hood\"}]}").Entries[0].HelmetHairVisibility == null, "omitted metadata preserves native flags");
        Console.WriteLine("PASS: " + checks + " helmet hair JSON checks (startup and candidate parsers).");
    }
}
namespace FTKModFramework.Core.Data { internal sealed class ItemModifierEntry { } }
