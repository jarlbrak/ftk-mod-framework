using System;
using System.Collections.Generic;
using System.Text.Json;
using FTKModFramework.Core;
using FTKModFramework.Core.SaveCompatibility;

// The resume guard's decision layer: which saved ids the current tables cannot resolve, what the
// player reads, what the log says, and that a checker problem never blocks a save.
internal static class Program
{
    private static int _checks;

    private static void Check(bool value, string message)
    {
        _checks++;
        if (!value) throw new Exception("FAIL: " + message);
    }

    private static void Equal(string expected, string actual, string message)
    {
        _checks++;
        if (expected != actual) throw new Exception("FAIL: " + message + "\n  expected: " + expected + "\n  actual:   " + actual);
    }

    // Mono's string.GetHashCode, which QuestDefBase.HashID uses in the game. The test runtime randomizes
    // its own string hashes, so the fixture uses this function on both sides.
    private static int MonoHash(string s)
    {
        int h = 0;
        foreach (char c in s) h = unchecked(h * 31 + c);
        return h;
    }

    private static void Main()
    {
        EvidenceIds();
        EvidenceShapeIsRefused();
        VanillaShapeIsAdmitted();
        EnumNamesAreNotChecked();
        SyntheticBandNeedsRegistration();
        Attribution();
        Plurals();
        MissingAdventureSkipsQuests();
        QuestHashes();
        FailSafe();
        Console.WriteLine("PASS: " + _checks + " save content guard checks.");
    }

    // ---- Fixture -------------------------------------------------------------------------------------

    // Shapes as GameSerialize writes them with fsConfig.SerializeEnumsAsInteger on: MiniHex instantiation
    // data wrapped in {"$content", "$type"}, a CharacterOverworld state as a nested JSON string, and
    // m_ItemCounts in each of the three forms seen in saves.
    private const string KillVexorDefinition =
        "{\"m_SaveFileName\":\"KillVexor\",\"m_Stages\":[{\"m_Quests\":[" +
        "{\"m_StoryQuestID\": \"1_GoToWoodsmoke\"},{\"m_StoryQuestID\":\"1_Multi\",\"m_SubQuests\":[{\"m_StoryQuestID\":\"1_GlitteringMines\"}]}," +
        "{\"m_StoryQuestID\":null}]}]}";

    private static string Cow(string backpack)
    {
        string state = "{\"m_PlayerInventory\":{\"m_ContainerBackpack\":{\"m_ID\":7,\"m_ItemCounts\":{\"$content\":[" + backpack + "],\"$id\":\"3\"}}," +
            "\"m_ContainerRightHand\":{\"m_ShieldHand\":{\"m_WeaponHand\":{\"$ref\":\"4\"},\"m_ID\":0,\"m_ItemCounts\":[{\"Key\":180,\"Value\":1}],\"$id\":\"5\"}," +
            "\"m_ID\":1,\"m_ItemCounts\":[{\"Key\":100006,\"Value\":1}],\"$id\":\"4\"},\"m_ContainerLeftHand\":{\"$ref\":\"5\"}," +
            "\"m_ContainerHead\":{\"m_ID\":2,\"m_ItemCounts\":{}},\"$type\":\"PlayerInventory\"}," +
            "\"m_QuickItems\":{\"$content\":[87],\"$type\":\"System.Collections.Generic.List`1\"}}";
        // Escaped the way FullSerializer writes a string: only backslashes and quotes.
        return "\"" + state.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string Wrapped(object value, string type)
    {
        return "{\"$content\":" + JsonSerializer.Serialize(value) + ",\"$type\":\"" + type + "\"}";
    }

    private static string Enemy(string prefab, string enemy)
    {
        return "[" + Wrapped(false, "System.Boolean") + "," + Wrapped(0, "System.Int32") + "," + Wrapped(15, "System.Int32") + "," +
            Wrapped(enemy, "System.String") + "]";
    }

    private static string Encounter(int type)
    {
        return "[" + Wrapped(false, "System.Boolean") + "," + Wrapped(25, "System.Int32") + "," + Wrapped(1, "System.Int32") + "," +
            Wrapped(type, "System.Int32") + "," + Wrapped(true, "System.Boolean") + "," + Wrapped(6, "System.Int32") + "," +
            Wrapped(false, "System.Boolean") + "]";
    }

    private static string Utility()
    {
        return "[" + Wrapped(false, "System.Boolean") + "," + Wrapped(25, "System.Int32") + "," + Wrapped(13, "System.Int32") + "," +
            Wrapped(3, "GridEditor.FTK_utility+ID") + "]";
    }

    private static string Save(string enemyA, string enemyCamp, int encounter, string backpack, int storyQuest, int playedClass)
    {
        return "{\"m_GameInfo\":{\"m_Version\":\"1.1.00\",\"m_GameDefinition\":\"KillVexor\",\"m_PlayedWithClass\":[" + playedClass + "]}," +
            "\"m_Map\":{\"m_BigHexArray\":[\"hexPlains03.GoldenPlains.1\"]," +
            "\"m_MiniHexArray\":[\"MMenemy\",\"MMEnemyCamp\",\"MMenemy\",\"MMminiencounter\",\"MMminiencounter\",\"MMutility\"]," +
            "\"m_MiniHexInstArray\":[" + Enemy("MMenemy", enemyA) + "," + Enemy("MMEnemyCamp", enemyCamp) + "," + Enemy("MMenemy", enemyA) + "," +
            Encounter(23) + "," + Encounter(encounter) + "," + Utility() + "]}," +
            "\"m_GameStates\":{\"m_QuestList\":[" +
            "{\"m_QuestID\":-2147483648,\"m_StoryQuestID\":0,\"$type\":\"FTKModFramework.Core.CampaignStateQuest\"}," +
            "{\"m_QuestID\":-1,\"m_StoryQuestID\":" + storyQuest + ",\"$type\":\"VisitQuestLogic\"}," +
            "{\"m_QuestID\":2,\"m_StoryQuestID\":0,\"$type\":\"BountyQuestLogic\"}]}," +
            "\"m_PlayerSerialize\":[{\"m_InstData\":[" + Wrapped(false, "System.Boolean") + "," + Wrapped(0, "System.Int32") + "," +
            Wrapped(1, "System.Int32") + "," + Wrapped("Player 1", "System.String") + "," + Wrapped(playedClass, "System.Int32") + "]," +
            "\"m_PlayerID\":{\"m_TurnIndex\":0},\"m_StateCOWData\":" + Cow(backpack) + "}]}";
    }

    // The Aug 2 development save's shape: a removed dev enemy on every enemy point and camp, a removed
    // dev mini-encounter, and a removed synthetic item in the backpack.
    private static string EvidenceSave()
    {
        return Save("1487246809", "1487246809", 1257739546, "{\"Key\":1453116279,\"Value\":1},{\"Key\":364,\"Value\":1},{\"Key\":0,\"Value\":1}",
            MonoHash("1_GoToWoodsmoke"), 15);
    }

    private static string VanillaSave()
    {
        return Save("banditA", "skellyA", 13, "{\"Key\":364,\"Value\":1},{\"Key\":0,\"Value\":1}", MonoHash("1_GoToWoodsmoke"), 3);
    }

    private static object Tree(string json)
    {
        using (JsonDocument document = JsonDocument.Parse(json)) return Convert(document.RootElement);
    }

    private static object Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject()) dict[property.Name] = Convert(property.Value);
                return dict;
            case JsonValueKind.Array:
                var list = new List<object>();
                foreach (JsonElement item in element.EnumerateArray()) list.Add(Convert(item));
                return list;
            case JsonValueKind.String: return element.GetString();
            case JsonValueKind.Number:
                long whole;
                return element.TryGetInt64(out whole) ? whole : (object)element.GetDouble();
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return null;
        }
    }

    private sealed class FakeResolver : ISaveContentResolver
    {
        internal readonly HashSet<string> Adventures = new HashSet<string> { "KillVexor" };
        internal readonly HashSet<string> Enemies = new HashSet<string> { "banditA", "skellyA" };
        internal readonly HashSet<int> Encounters = new HashSet<int> { 13, 23, 37, 97 };
        internal readonly HashSet<int> Items = new HashSet<int> { 0, 12, 87, 180, 351, 364, 376, 100006 };
        internal readonly HashSet<int> Classes = new HashSet<int> { 0, 1, 2, 3, 13, 14, 15 };
        internal readonly HashSet<int> Registered = new HashSet<int>();
        internal readonly Dictionary<string, string> Owners = new Dictionary<string, string>();
        internal HashSet<int> Quests = SaveContentCheck.StoryQuestHashes(KillVexorDefinition, MonoHash);
        internal bool Throw;

        public bool Exists(SaveRefKind kind, string id)
        {
            if (Throw) throw new InvalidOperationException("table not ready");
            switch (kind)
            {
                case SaveRefKind.Adventure: return Adventures.Contains(id);
                case SaveRefKind.Enemy: return Enemies.Contains(id) || (int.TryParse(id, out int e) && Registered.Contains(e));
                case SaveRefKind.MiniEncounter: return Encounters.Contains(int.Parse(id));
                case SaveRefKind.Item: return Items.Contains(int.Parse(id));
                case SaveRefKind.Class: return Classes.Contains(int.Parse(id));
                default: return Quests.Contains(int.Parse(id));
            }
        }

        public bool IsRegistered(SaveRefKind kind, int id) { return Registered.Contains(id); }

        public string Owner(SaveRefKind kind, string id)
        {
            string owner;
            return Owners.TryGetValue(id, out owner) ? owner : null;
        }
    }

    private static SaveContentVerdict Run(string json, FakeResolver resolver, List<string> warnings, out SaveContentRefs refs)
    {
        return SaveContentCheck.Run(() => Tree(json), Tree, found => resolver, warnings.Add, out refs);
    }

    private static List<string> Ids(SaveContentVerdict verdict, SaveRefKind kind)
    {
        var ids = new List<string>();
        List<SaveMissingRef> missing;
        if (verdict.Missing.TryGetValue(kind, out missing)) foreach (SaveMissingRef entry in missing) ids.Add(entry.Id);
        return ids;
    }

    // ---- Checks --------------------------------------------------------------------------------------

    private static void EvidenceIds()
    {
        // The dev content removed in 69aeefd6, as the allocator mints it.
        Check(IdAllocator.Allocate("com.ftkmf.framework", "FTK_enemyCombatDB/ftkmf_cutpurse") == 1487246809, "cutpurse id");
        Check(IdAllocator.Allocate("com.ftkmf.framework", "FTK_miniEncounterDB/ftkmf_smugglers_cache") == 1257739546, "smugglers cache id");
        // The save's story quest is vanilla KillVexor's first quest, not a synthetic id.
        Check(MonoHash("1_GoToWoodsmoke") == 1450288369, "story quest 1450288369 is 1_GoToWoodsmoke");
    }

    private static void EvidenceShapeIsRefused()
    {
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(EvidenceSave(), new FakeResolver(), warnings, out refs);
        Check(warnings.Count == 0 && verdict != null, "the evidence shape parses without warnings");
        Check(refs.Count(SaveRefKind.Enemy, "1487246809") == 3, "enemy points and the camp are both counted");
        Check(refs.Ids(SaveRefKind.Item).Count == 6, "items from the backpack wrapper, both hands, the nested shield hand and quick items");
        Check(refs.Count(SaveRefKind.Class, "15") == 2, "class from game info and from the player's instantiation data");
        Check(verdict.Refuse, "the evidence save is refused");
        Equal("1487246809", string.Join(",", Ids(verdict, SaveRefKind.Enemy)), "missing enemy");
        Equal("1257739546", string.Join(",", Ids(verdict, SaveRefKind.MiniEncounter)), "missing mini-encounter");
        Equal("1453116279", string.Join(",", Ids(verdict, SaveRefKind.Item)), "missing item");
        Check(Ids(verdict, SaveRefKind.Quest).Count == 0 && Ids(verdict, SaveRefKind.Class).Count == 0, "vanilla quest and installed class pass");
        Equal("This save uses content that isn't installed: 1 enemy type, 1 map encounter, 1 item. It can't be loaded with the current mods. Start a new game or choose another save.",
            SaveContentCheck.PlayerMessage(verdict), "player message");
        Equal("[save-guard] Refused to resume 'KillVexor_2026_8_2_1.run': content is not installed: enemy=[1487246809 x3] mini-encounter=[1257739546] item=[1453116279]. Checked in 42 ms; the save file was not changed.",
            SaveContentCheck.LogLine("KillVexor_2026_8_2_1.run", verdict, 42), "log line");
    }

    private static void VanillaShapeIsAdmitted()
    {
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(VanillaSave(), new FakeResolver(), warnings, out refs);
        Check(warnings.Count == 0 && verdict != null && !verdict.Refuse, "a vanilla save with vanilla ids is admitted");
        Check(verdict.Checked == refs.Total && refs.Total > 8, "every extracted id was checked");
    }

    private static void EnumNamesAreNotChecked()
    {
        // Without SerializeEnumsAsInteger, enums are member names; synthetic ids never have one.
        string json = VanillaSave().Replace("\"m_PlayedWithClass\":[3]", "\"m_PlayedWithClass\":[\"herbalist\"]");
        json = json.Replace("{\\\"Key\\\":364,\\\"Value\\\":1}", "{\\\"Key\\\":\\\"bladeShortsword\\\",\\\"Value\\\":1}");
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(json, new FakeResolver(), warnings, out refs);
        Check(verdict != null && !verdict.Refuse, "names are admitted");
        Check(refs.Count(SaveRefKind.Item, "364") == 0 && refs.Count(SaveRefKind.Class, "3") == 1, "names are skipped, numbers still read");

        // m_ItemCounts written as an object keyed by id.
        string keyed = VanillaSave().Replace("\\\"m_ItemCounts\\\":{}", "\\\"m_ItemCounts\\\":{\\\"376\\\":1,\\\"bladeShortsword\\\":1,\\\"$id\\\":\\\"9\\\"}");
        Check(keyed != VanillaSave(), "fixture rewrite applied");
        verdict = Run(keyed, new FakeResolver(), warnings, out refs);
        Check(verdict != null && !verdict.Refuse && refs.Count(SaveRefKind.Item, "376") == 1, "keyed item counts are read");
    }

    private static void SyntheticBandNeedsRegistration()
    {
        int synthetic = IdAllocator.Allocate("com.example.pack", "FTK_itemsDB/example_ring");
        string json = Save("banditA", "skellyA", 13, "{\"Key\":" + synthetic + ",\"Value\":1}", MonoHash("1_GoToWoodsmoke"), 3);
        var resolver = new FakeResolver();
        resolver.Items.Add(synthetic); // the lookup finds a row, but not one this framework registered
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(json, resolver, warnings, out refs);
        Check(verdict.Refuse && verdict.Missing[SaveRefKind.Item][0].Unregistered, "an unregistered synthetic id is refused even when a row answers");
        Check(SaveContentCheck.LogLine("a.run", verdict, 1).Contains("item=[" + synthetic + " unregistered]"), "the log marks it unregistered");

        resolver.Registered.Add(synthetic);
        verdict = Run(json, resolver, warnings, out refs);
        Check(!verdict.Refuse, "a registered synthetic id is admitted");

        // A registered synthetic enemy, written as its numeric string, is admitted.
        resolver.Registered.Add(1487246809);
        verdict = Run(EvidenceSave(), resolver, warnings, out refs);
        Check(Ids(verdict, SaveRefKind.Enemy).Count == 0, "the cutpurse is admitted while registered");
    }

    private static void Attribution()
    {
        var resolver = new FakeResolver();
        resolver.Owners["1453116279"] = "Example Pack";
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(EvidenceSave(), resolver, warnings, out refs);
        Check(SaveContentCheck.PlayerMessage(verdict).EndsWith(" Missing content from: Example Pack, and unknown content."), "owner and unknown content are named");
        Check(SaveContentCheck.LogLine("a.run", verdict, 1).Contains("item=[1453116279 from Example Pack]"), "the log names the owner");
        resolver.Items.Add(1453116279);
        resolver.Encounters.Add(1257739546);
        resolver.Registered.Add(1453116279);
        resolver.Registered.Add(1257739546);
        resolver.Owners["1487246809"] = "Example Pack";
        verdict = Run(EvidenceSave(), resolver, warnings, out refs);
        Check(SaveContentCheck.PlayerMessage(verdict).EndsWith(" Missing content from: Example Pack."), "only attributed content");
    }

    private static void Plurals()
    {
        var refs = new SaveContentRefs();
        foreach (string enemy in new[] { "a", "b", "c", "a" }) refs.Add(SaveRefKind.Enemy, enemy);
        refs.Add(SaveRefKind.Item, "900");
        refs.Add(SaveRefKind.Item, "901");
        refs.Add(SaveRefKind.Quest, "77");
        refs.Add(SaveRefKind.Class, "40");
        SaveContentVerdict verdict = SaveContentCheck.Evaluate(refs, new FakeResolver());
        Equal("This save uses content that isn't installed: 1 class, 3 enemy types, 2 items, 1 quest. It can't be loaded with the current mods. Start a new game or choose another save.",
            SaveContentCheck.PlayerMessage(verdict), "plural message");
        Check(verdict.Missing[SaveRefKind.Enemy][0].Occurrences == 2, "occurrences are counted per id");
    }

    private static void MissingAdventureSkipsQuests()
    {
        var resolver = new FakeResolver();
        resolver.Adventures.Clear();
        resolver.Quests = new HashSet<int>();
        var warnings = new List<string>();
        SaveContentRefs refs;
        SaveContentVerdict verdict = Run(VanillaSave(), resolver, warnings, out refs);
        Check(verdict.Refuse && Ids(verdict, SaveRefKind.Adventure).Count == 1, "a missing adventure is refused");
        Check(!verdict.Missing.ContainsKey(SaveRefKind.Quest), "quests are not checked without their adventure");
        Check(SaveContentCheck.PlayerMessage(verdict).StartsWith("This save uses content that isn't installed: 1 adventure."), "adventure message");
    }

    private static void QuestHashes()
    {
        HashSet<int> hashes = SaveContentCheck.StoryQuestHashes(KillVexorDefinition, MonoHash);
        Check(hashes.Contains(MonoHash("1_GoToWoodsmoke")) && hashes.Contains(MonoHash("1_GlitteringMines")), "every m_StoryQuestID is read, nested too");
        Check(hashes.Contains(MonoHash("1_GlitteringMines_1")) && hashes.Contains(MonoHash("1_GlitteringMines_" + SaveContentCheck.DungeonQuestCopies)),
            "dungeon copies are included");
        Check(!hashes.Contains(MonoHash("1_GlitteringMines_" + (SaveContentCheck.DungeonQuestCopies + 1))), "copies are bounded");
        Check(hashes.Count == 3 * (SaveContentCheck.DungeonQuestCopies + 1), "a null m_StoryQuestID adds nothing");
        HashSet<int> escaped = SaveContentCheck.StoryQuestHashes("{\"m_StoryQuestID\" : \"a\\\"b\\u0041\"}", MonoHash);
        Check(escaped.Contains(MonoHash("a\"bA")), "JSON escapes are decoded");
        Check(SaveContentCheck.StoryQuestHashes(null, MonoHash).Count == 0 && SaveContentCheck.StoryQuestHashes("{\"m_StoryQuestID\":\"x", MonoHash).Count == 0,
            "empty and truncated definitions add nothing");
    }

    private static void FailSafe()
    {
        SaveContentRefs refs;
        var cases = new Dictionary<string, Func<object>>
        {
            { "load throws", () => throw new System.IO.IOException("sharing violation") },
            { "root is a list", () => new List<object>() },
            { "no map", () => Tree("{\"m_GameInfo\":{}}") },
            { "mismatched mini-hex arrays", () => Tree("{\"m_GameInfo\":{},\"m_Map\":{\"m_MiniHexArray\":[\"MMenemy\"],\"m_MiniHexInstArray\":[]}}") },
            { "character state is not an object", () => Tree("{\"m_GameInfo\":{},\"m_Map\":{},\"m_PlayerSerialize\":[{\"m_StateCOWData\":\"[1]\"}]}") },
        };
        foreach (KeyValuePair<string, Func<object>> item in cases)
        {
            var warnings = new List<string>();
            SaveContentVerdict verdict = SaveContentCheck.Run(item.Value, Tree, found => new FakeResolver(), warnings.Add, out refs);
            Check(verdict == null && warnings.Count == 1, "fail open with one warning: " + item.Key);
        }

        var broken = new FakeResolver { Throw = true };
        var lookupWarnings = new List<string>();
        Check(SaveContentCheck.Run(() => Tree(EvidenceSave()), Tree, found => broken, lookupWarnings.Add, out refs) == null &&
            lookupWarnings[0].Contains("table not ready"), "a failing lookup lets the game load as before");

        var nestedWarnings = new List<string>();
        Check(SaveContentCheck.Run(() => Tree(EvidenceSave()), json => throw new FormatException("bad nested json"), found => new FakeResolver(),
            nestedWarnings.Add, out refs) == null && nestedWarnings[0] == "FormatException: bad nested json", "a nested parse error fails open");
    }
}
