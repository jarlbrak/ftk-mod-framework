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
        if (content.Entries.Count != 57 || counts["class"] != 1 || counts["proficiency"] != 5 || counts["weapon"] != 14 || counts["item"] != 37)
            throw new Exception("Paladin authored shape changed.");
        bool rejected = false;
        try { JsonConvert.DeserializeObject<ContentFile>("{\"entries\":[{\"kind\":\"class\",\"unknownCapability\":true}]}", strict); }
        catch (JsonSerializationException) { rejected = true; }
        if (!rejected) throw new Exception("Unknown candidate capability was ignored.");
        rejected = false;
        try { JsonConvert.DeserializeObject<ContentFile>("{\"entries\":[{\"guardianBonuses\":{\"unknownBonus\":1}}]}", strict); }
        catch (JsonSerializationException) { rejected = true; }
        if (!rejected) throw new Exception("Unknown nested candidate capability was ignored.");
        GuardianProfileEntry legacy = JsonConvert.DeserializeObject<GuardianProfileEntry>(
            "{\"physicalPercent\":100,\"smitePercent\":100,\"healingPercent\":100,\"guardReductionPercent\":50}", strict);
        if (legacy.GuardSmiteHealing || legacy.GuardPhysicalBonusPercent != 0)
            throw new Exception("Legacy Guardian profile defaults changed.");
        GuardianProfileEntry themed = JsonConvert.DeserializeObject<GuardianProfileEntry>(
            "{\"physicalPercent\":100,\"smitePercent\":100,\"healingPercent\":100,\"guardReductionPercent\":50," +
            "\"guardSmiteHealing\":true,\"guardPhysicalBonusPercent\":50}", strict);
        if (!themed.GuardSmiteHealing || themed.GuardPhysicalBonusPercent != 50)
            throw new Exception("Guardian themes did not parse.");
        foreach (string invalid in new[] {
            "\"guardSmiteHealing\":\"true\"", "\"guardPhysicalBonusPercent\":\"50\"",
            "\"guardPhysicalBonusPercent\":true", "\"guardPhysicalBonusPercent\":51" })
        {
            rejected = false;
            try { JsonConvert.DeserializeObject<GuardianProfileEntry>(
                "{\"physicalPercent\":100,\"smitePercent\":100,\"healingPercent\":100,\"guardReductionPercent\":50," + invalid + "}", strict); }
            catch (JsonSerializationException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid Guardian theme token accepted: " + invalid);
        }
        Console.WriteLine("PASS: actual Paladin strict JSON shape and unknown capability rejection");
    }
}
