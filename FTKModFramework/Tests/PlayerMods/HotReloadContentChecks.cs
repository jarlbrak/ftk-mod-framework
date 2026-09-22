using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using FTKModFramework.Core.Data;

internal static class HotReloadContentChecks
{
    internal static void Run()
    {
        string path = Path.GetFullPath("marketplace/packages/paladin/content.json");
        JsonSerializerSettings strict = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        ContentFile content = JsonConvert.DeserializeObject<ContentFile>(File.ReadAllText(path), strict);
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (ContentEntry entry in content.Entries)
        {
            int count; counts.TryGetValue(entry.Kind, out count); counts[entry.Kind] = count + 1;
        }
        if (content.Entries.Count != 54 || counts["class"] != 1 || counts["proficiency"] != 2 || counts["weapon"] != 14 || counts["item"] != 37)
            throw new Exception("Paladin authored shape changed.");
        bool rejected = false;
        try { JsonConvert.DeserializeObject<ContentFile>("{\"entries\":[{\"kind\":\"class\",\"unknownCapability\":true}]}", strict); }
        catch (JsonSerializationException) { rejected = true; }
        if (!rejected) throw new Exception("Unknown candidate capability was ignored.");
        rejected = false;
        try { JsonConvert.DeserializeObject<ContentFile>("{\"entries\":[{\"guardianBonuses\":{\"unknownBonus\":1}}]}", strict); }
        catch (JsonSerializationException) { rejected = true; }
        if (!rejected) throw new Exception("Unknown nested candidate capability was ignored.");
        Console.WriteLine("PASS: actual Paladin strict JSON shape and unknown capability rejection");
    }
}
