using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FTKModFramework.Core.SaveCompatibility
{
    // The identifier kinds a resume resolves through a game table. A missing row for any of them
    // throws during the load (enemy, map encounter, class, adventure) or leaves the run without the
    // content it refers to (item, quest).
    internal enum SaveRefKind
    {
        Adventure,
        Class,
        Enemy,
        MiniEncounter,
        Item,
        Quest
    }

    internal interface ISaveContentResolver
    {
        /// <summary>The game's own lookup for a saved value. Numeric kinds receive invariant integer text.</summary>
        bool Exists(SaveRefKind kind, string id);

        /// <summary>True when a synthetic id is backed by a row this framework registered in this process.</summary>
        bool IsRegistered(SaveRefKind kind, int id);

        /// <summary>A mod or package name for an id, or null when it cannot be attributed.</summary>
        string Owner(SaveRefKind kind, string id);
    }

    /// <summary>Distinct saved identifiers per kind, with how often each occurs.</summary>
    internal sealed class SaveContentRefs
    {
        private readonly Dictionary<SaveRefKind, List<string>> _order = new Dictionary<SaveRefKind, List<string>>();
        private readonly Dictionary<SaveRefKind, Dictionary<string, int>> _counts = new Dictionary<SaveRefKind, Dictionary<string, int>>();

        internal string Adventure { get; set; }

        internal void Add(SaveRefKind kind, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Dictionary<string, int> counts;
            if (!_counts.TryGetValue(kind, out counts))
            {
                counts = new Dictionary<string, int>(StringComparer.Ordinal);
                _counts.Add(kind, counts);
                _order.Add(kind, new List<string>());
            }
            int seen;
            if (counts.TryGetValue(id, out seen)) counts[id] = seen + 1;
            else
            {
                counts.Add(id, 1);
                _order[kind].Add(id);
            }
        }

        internal IList<string> Ids(SaveRefKind kind)
        {
            List<string> ids;
            return _order.TryGetValue(kind, out ids) ? ids : new List<string>();
        }

        internal int Count(SaveRefKind kind, string id)
        {
            Dictionary<string, int> counts;
            int count;
            return _counts.TryGetValue(kind, out counts) && counts.TryGetValue(id, out count) ? count : 0;
        }

        internal int Total
        {
            get
            {
                int total = 0;
                foreach (List<string> ids in _order.Values) total += ids.Count;
                return total;
            }
        }
    }

    internal sealed class SaveMissingRef
    {
        internal string Id;
        internal int Occurrences;
        internal string Owner;
        internal bool Unregistered;
    }

    internal sealed class SaveContentVerdict
    {
        internal readonly Dictionary<SaveRefKind, List<SaveMissingRef>> Missing = new Dictionary<SaveRefKind, List<SaveMissingRef>>();
        internal int Checked;
        internal bool Refuse { get { return Missing.Count != 0; } }

        internal void Add(SaveRefKind kind, SaveMissingRef missing)
        {
            List<SaveMissingRef> list;
            if (!Missing.TryGetValue(kind, out list))
            {
                list = new List<SaveMissingRef>();
                Missing.Add(kind, list);
            }
            list.Add(missing);
        }
    }

    /// <summary>
    /// The game-free half of the resume guard. It reads identifiers out of a parsed save, decides which
    /// ones the current tables cannot resolve, and words the refusal. The tree is plain data: objects are
    /// IDictionary&lt;string, object&gt;, arrays IList&lt;object&gt;, and leaves string, long, double, bool or
    /// null. The in-game adapter converts the game's own FullSerializer parse into that shape, so this
    /// file needs no Unity or game reference and the tests exercise it directly.
    /// </summary>
    internal static class SaveContentCheck
    {
        // Every kind in the order the message lists them.
        internal static readonly SaveRefKind[] Kinds =
        {
            SaveRefKind.Adventure, SaveRefKind.Class, SaveRefKind.Enemy,
            SaveRefKind.MiniEncounter, SaveRefKind.Item, SaveRefKind.Quest
        };

        // MultiQuestDef copies a dungeon sub-quest per main dungeon at map generation, naming each copy
        // "<id>_<n>". The copies exist only in a running game, so their hashes are added up to this
        // bound. A map has far fewer main dungeons than this.
        internal const int DungeonQuestCopies = 64;

        private const int MaxDepth = 32;
        private const int MaxNestedStates = 16;

        // ---- Extraction ------------------------------------------------------------------------------

        /// <summary>
        /// Collects the checked identifiers. Throws FormatException when the save does not have the
        /// layout GameSerialize writes, so the caller can let the game load it as before.
        /// </summary>
        /// <param name="parseNested">Parses a nested state string (CharacterOverworld state) into the same
        /// tree shape.</param>
        internal static SaveContentRefs Extract(object root, Func<string, object> parseNested)
        {
            IDictionary<string, object> save = root as IDictionary<string, object>;
            if (save == null) throw new FormatException("the save root is not an object");
            IDictionary<string, object> info = Unwrap(Get(save, "m_GameInfo")) as IDictionary<string, object>;
            IDictionary<string, object> map = Unwrap(Get(save, "m_Map")) as IDictionary<string, object>;
            if (info == null || map == null) throw new FormatException("the save has no m_GameInfo or m_Map");

            SaveContentRefs refs = new SaveContentRefs();
            string adventure = Unwrap(Get(info, "m_GameDefinition")) as string;
            if (!string.IsNullOrEmpty(adventure))
            {
                refs.Adventure = adventure;
                refs.Add(SaveRefKind.Adventure, adventure);
            }
            IList<object> classes = AsList(Get(info, "m_PlayedWithClass"));
            if (classes != null)
                foreach (object value in classes) AddNumber(refs, SaveRefKind.Class, value);

            ExtractMiniHexes(refs, map);

            IDictionary<string, object> states = Unwrap(Get(save, "m_GameStates")) as IDictionary<string, object>;
            IList<object> quests = states == null ? null : AsList(Get(states, "m_QuestList"));
            if (quests != null)
                foreach (object quest in quests)
                {
                    int story;
                    IDictionary<string, object> entry = Unwrap(quest) as IDictionary<string, object>;
                    if (entry != null && TryInt(Get(entry, "m_StoryQuestID"), out story) && story != 0)
                        refs.Add(SaveRefKind.Quest, Text(story));
                }

            IList<object> players = AsList(Get(save, "m_PlayerSerialize"));
            if (players != null)
            {
                int parsed = 0;
                foreach (object player in players)
                {
                    IDictionary<string, object> entry = Unwrap(player) as IDictionary<string, object>;
                    if (entry == null) continue;
                    // PlayerSerialize.Deserialize passes m_InstData[4] as the class id.
                    IList<object> inst = AsList(Get(entry, "m_InstData"));
                    if (inst != null && inst.Count > 4) AddNumber(refs, SaveRefKind.Class, inst[4]);
                    string cow = Unwrap(Get(entry, "m_StateCOWData")) as string;
                    if (string.IsNullOrEmpty(cow) || parseNested == null || parsed >= MaxNestedStates) continue;
                    parsed++;
                    IDictionary<string, object> state = parseNested(cow) as IDictionary<string, object>;
                    if (state == null) throw new FormatException("a character state is not an object");
                    object inventory = Get(state, "m_PlayerInventory");
                    if (inventory != null) CollectItemCounts(refs, inventory, 0);
                    IList<object> quick = AsList(Get(state, "m_QuickItems"));
                    if (quick != null)
                        foreach (object item in quick) AddNumber(refs, SaveRefKind.Item, item);
                }
            }
            return refs;
        }

        private static void ExtractMiniHexes(SaveContentRefs refs, IDictionary<string, object> map)
        {
            IList<object> names = AsList(Get(map, "m_MiniHexArray"));
            IList<object> inst = AsList(Get(map, "m_MiniHexInstArray"));
            if (names == null && inst == null) return;
            if (names == null || inst == null || names.Count != inst.Count)
                throw new FormatException("m_MiniHexArray and m_MiniHexInstArray do not match");
            for (int i = 0; i < names.Count; i++)
            {
                string name = Unwrap(names[i]) as string;
                IList<object> data = AsList(inst[i]);
                // Index 3 is MiniHexEnemy.InstantiationDataEnemy.EnemyType (a string) and
                // MiniEncounter.InstantiationDataEncounter.Type (an FTK_miniEncounter.ID).
                if (name == null || data == null || data.Count <= 3) continue;
                if (Same(name, "MMenemy") || Same(name, "MMenemycamp"))
                {
                    object value = Unwrap(data[3]);
                    string enemy = value as string;
                    if (enemy == null && value is long) enemy = Text((long)value);
                    if (!string.IsNullOrEmpty(enemy)) refs.Add(SaveRefKind.Enemy, enemy);
                }
                else if (Same(name, "MMminiencounter")) AddNumber(refs, SaveRefKind.MiniEncounter, data[3]);
            }
        }

        // PlayerInventory nests containers (the shield hand sits inside the weapon hand). Every
        // container's m_ItemCounts is a Dictionary<FTK_itembase.ID, int>, written as a list of
        // {Key, Value} pairs, possibly inside a {"$content": ...} wrapper, or as an object keyed by id.
        private static void CollectItemCounts(SaveContentRefs refs, object node, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("the inventory is nested too deeply");
            IDictionary<string, object> dict = node as IDictionary<string, object>;
            if (dict != null)
            {
                foreach (KeyValuePair<string, object> pair in dict)
                {
                    if (pair.Key == "m_ItemCounts") AddItemCounts(refs, pair.Value);
                    else CollectItemCounts(refs, pair.Value, depth + 1);
                }
                return;
            }
            IList<object> list = node as IList<object>;
            if (list != null)
                foreach (object value in list) CollectItemCounts(refs, value, depth + 1);
        }

        private static void AddItemCounts(SaveContentRefs refs, object counts)
        {
            object value = Unwrap(counts);
            IList<object> pairs = value as IList<object>;
            if (pairs != null)
            {
                foreach (object pair in pairs)
                {
                    IDictionary<string, object> entry = Unwrap(pair) as IDictionary<string, object>;
                    if (entry != null) AddNumber(refs, SaveRefKind.Item, Get(entry, "Key"));
                }
                return;
            }
            IDictionary<string, object> keyed = value as IDictionary<string, object>;
            if (keyed != null)
                foreach (string key in keyed.Keys)
                    if (!key.StartsWith("$", StringComparison.Ordinal)) AddNumber(refs, SaveRefKind.Item, key);
        }

        // Enum-typed values are integers when fsConfig.SerializeEnumsAsInteger is on (the framework sets it)
        // and member names otherwise. A name is a member of the vanilla enum, since synthetic ids have no
        // names, so only numbers are checked. Negative values are the tables' None.
        private static void AddNumber(SaveContentRefs refs, SaveRefKind kind, object value)
        {
            int number;
            if (TryInt(value, out number) && number >= 0) refs.Add(kind, Text(number));
        }

        // ---- Decision --------------------------------------------------------------------------------

        /// <summary>
        /// Decides which saved identifiers are missing. A value is missing when the game's lookup finds no
        /// row, or when it is in the framework's synthetic band without a row this framework registered.
        /// Quest ids are string hashes, not allocated ids, so the band rule does not apply to them, and
        /// they are not checked when the adventure itself is missing.
        /// </summary>
        internal static SaveContentVerdict Evaluate(SaveContentRefs refs, ISaveContentResolver resolver)
        {
            SaveContentVerdict verdict = new SaveContentVerdict();
            bool adventureMissing = false;
            foreach (SaveRefKind kind in Kinds)
            {
                if (kind == SaveRefKind.Quest && adventureMissing) continue;
                foreach (string id in refs.Ids(kind))
                {
                    verdict.Checked++;
                    bool missing = !resolver.Exists(kind, id);
                    bool unregistered = false;
                    int number;
                    if (!missing && kind != SaveRefKind.Quest && kind != SaveRefKind.Adventure &&
                        int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) &&
                        IdAllocator.IsCustom(number) && !resolver.IsRegistered(kind, number))
                        missing = unregistered = true;
                    if (!missing) continue;
                    if (kind == SaveRefKind.Adventure) adventureMissing = true;
                    verdict.Add(kind, new SaveMissingRef
                    {
                        Id = id,
                        Occurrences = refs.Count(kind, id),
                        Owner = resolver.Owner(kind, id),
                        Unregistered = unregistered
                    });
                }
            }
            return verdict;
        }

        /// <summary>
        /// Runs the whole check. Returns null, after one warning, when anything about the check itself
        /// fails, so a checker problem never blocks a save the game could load.
        /// </summary>
        internal static SaveContentVerdict Run(Func<object> load, Func<string, object> parseNested,
            Func<SaveContentRefs, ISaveContentResolver> resolverFor, Action<string> warn, out SaveContentRefs refs)
        {
            refs = null;
            try
            {
                refs = Extract(load(), parseNested);
                return Evaluate(refs, resolverFor(refs));
            }
            catch (Exception e)
            {
                if (warn != null) warn(e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        // ---- Quest hashes ----------------------------------------------------------------------------

        /// <summary>
        /// The hash ids GameDefinition.GetQuestByHashID can resolve for an adventure, from the definition's
        /// JSON: every m_StoryQuestID string, plus the dungeon copies MultiQuestDef makes at map generation.
        /// A superset only risks a missed detection, never a false refusal. <paramref name="hash"/> must be the
        /// runtime's string.GetHashCode, which QuestDefBase.HashID uses.
        /// </summary>
        internal static HashSet<int> StoryQuestHashes(string definitionJson, Func<string, int> hash)
        {
            HashSet<int> hashes = new HashSet<int>();
            if (string.IsNullOrEmpty(definitionJson)) return hashes;
            const string key = "\"m_StoryQuestID\"";
            int at = 0;
            while ((at = definitionJson.IndexOf(key, at, StringComparison.Ordinal)) >= 0)
            {
                at += key.Length;
                string id;
                if (!TryReadStringValue(definitionJson, ref at, out id)) continue;
                hashes.Add(hash(id));
                for (int n = 1; n <= DungeonQuestCopies; n++) hashes.Add(hash(id + "_" + n.ToString(CultureInfo.InvariantCulture)));
            }
            return hashes;
        }

        // Reads `: "value"` after a key. False for a non-string value such as null.
        private static bool TryReadStringValue(string json, ref int at, out string value)
        {
            value = null;
            int i = SkipSpace(json, at);
            if (i >= json.Length || json[i] != ':') return false;
            i = SkipSpace(json, i + 1);
            if (i >= json.Length || json[i] != '"') return false;
            StringBuilder text = new StringBuilder();
            for (i++; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    at = i + 1;
                    value = text.ToString();
                    return true;
                }
                if (c != '\\') { text.Append(c); continue; }
                if (++i >= json.Length) return false;
                char e = json[i];
                switch (e)
                {
                    case 'n': text.Append('\n'); break;
                    case 't': text.Append('\t'); break;
                    case 'r': text.Append('\r'); break;
                    case 'b': text.Append('\b'); break;
                    case 'f': text.Append('\f'); break;
                    case 'u':
                        int code;
                        if (i + 4 >= json.Length || !int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture, out code)) return false;
                        text.Append((char)code);
                        i += 4;
                        break;
                    default: text.Append(e); break;
                }
            }
            return false;
        }

        private static int SkipSpace(string s, int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            return i;
        }

        // ---- Wording ---------------------------------------------------------------------------------

        internal const string DialogTitle = "Can't load this save";
        internal const string DialogNote = "Your save file has not been changed.";

        internal static string PlayerMessage(SaveContentVerdict verdict)
        {
            List<string> parts = new List<string>();
            List<string> owners = new List<string>();
            bool unknown = false;
            foreach (SaveRefKind kind in Kinds)
            {
                List<SaveMissingRef> missing;
                if (!verdict.Missing.TryGetValue(kind, out missing)) continue;
                parts.Add(missing.Count.ToString(CultureInfo.InvariantCulture) + " " + Noun(kind, missing.Count));
                foreach (SaveMissingRef entry in missing)
                {
                    if (string.IsNullOrEmpty(entry.Owner)) unknown = true;
                    else if (!owners.Contains(entry.Owner)) owners.Add(entry.Owner);
                }
            }
            string message = "This save uses content that isn't installed: " + string.Join(", ", parts.ToArray()) +
                ". It can't be loaded with the current mods. Start a new game or choose another save.";
            if (owners.Count != 0)
                message += " Missing content from: " + string.Join(", ", owners.ToArray()) + (unknown ? ", and unknown content." : ".");
            return message;
        }

        /// <summary>One log line: the save and the missing ids grouped by kind.</summary>
        internal static string LogLine(string saveName, SaveContentVerdict verdict, long elapsedMs)
        {
            StringBuilder line = new StringBuilder();
            line.Append("[save-guard] Refused to resume '").Append(saveName).Append("': content is not installed:");
            foreach (SaveRefKind kind in Kinds)
            {
                List<SaveMissingRef> missing;
                if (!verdict.Missing.TryGetValue(kind, out missing)) continue;
                line.Append(' ').Append(LogName(kind)).Append("=[");
                for (int i = 0; i < missing.Count; i++)
                {
                    SaveMissingRef entry = missing[i];
                    if (i != 0) line.Append(", ");
                    line.Append(entry.Id);
                    if (entry.Unregistered) line.Append(" unregistered");
                    if (!string.IsNullOrEmpty(entry.Owner)) line.Append(" from ").Append(entry.Owner);
                    if (entry.Occurrences > 1) line.Append(" x").Append(entry.Occurrences.ToString(CultureInfo.InvariantCulture));
                }
                line.Append(']');
            }
            line.Append(". Checked in ").Append(elapsedMs.ToString(CultureInfo.InvariantCulture))
                .Append(" ms; the save file was not changed.");
            return line.ToString();
        }

        internal static string Noun(SaveRefKind kind, int count)
        {
            bool one = count == 1;
            switch (kind)
            {
                case SaveRefKind.Adventure: return one ? "adventure" : "adventures";
                case SaveRefKind.Class: return one ? "class" : "classes";
                case SaveRefKind.Enemy: return one ? "enemy type" : "enemy types";
                case SaveRefKind.MiniEncounter: return one ? "map encounter" : "map encounters";
                case SaveRefKind.Item: return one ? "item" : "items";
                default: return one ? "quest" : "quests";
            }
        }

        internal static string LogName(SaveRefKind kind)
        {
            switch (kind)
            {
                case SaveRefKind.Adventure: return "adventure";
                case SaveRefKind.Class: return "class";
                case SaveRefKind.Enemy: return "enemy";
                case SaveRefKind.MiniEncounter: return "mini-encounter";
                case SaveRefKind.Item: return "item";
                default: return "quest";
            }
        }

        // ---- Tree helpers ----------------------------------------------------------------------------

        private static object Get(object node, string key)
        {
            IDictionary<string, object> dict = node as IDictionary<string, object>;
            object value;
            return dict != null && dict.TryGetValue(key, out value) ? value : null;
        }

        // FullSerializer wraps a value whose runtime type differs from the declared one as
        // {"$content": value, "$type": "..."}.
        private static object Unwrap(object node)
        {
            for (int i = 0; i < 4; i++)
            {
                IDictionary<string, object> dict = node as IDictionary<string, object>;
                object content;
                if (dict == null || !dict.TryGetValue("$content", out content)) break;
                node = content;
            }
            return node;
        }

        private static IList<object> AsList(object node)
        {
            return Unwrap(node) as IList<object>;
        }

        private static bool TryInt(object node, out int number)
        {
            number = 0;
            object value = Unwrap(node);
            if (value is long)
            {
                long whole = (long)value;
                if (whole < int.MinValue || whole > int.MaxValue) return false;
                number = (int)whole;
                return true;
            }
            if (value is int) { number = (int)value; return true; }
            if (value is double)
            {
                double real = (double)value;
                if (real != Math.Floor(real) || real < int.MinValue || real > int.MaxValue) return false;
                number = (int)real;
                return true;
            }
            string text = value as string;
            return text != null && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        }

        private static bool Same(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static string Text(long number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
