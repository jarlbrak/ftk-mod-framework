using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FullSerializer;
using GridEditor;
using HarmonyLib;
using FTKModFramework.Core.HotReload;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.SaveCompatibility
{
    /// <summary>
    /// Refuses to resume a save that refers to content the current tables cannot resolve, before the game
    /// starts loading it. Without this, a missing enemy row makes MiniHexEnemy.Awake2 throw inside
    /// SerializeGO.LoadCR_MiniHex, the coroutine dies, GameFlowMC.m_IsMapReady never becomes true, and
    /// the game waits on "Crafting adventure" forever.
    ///
    /// The check runs in prefixes on the two title-screen entries that choose a save: MainScreen.OnResume
    /// (the Resume button) and ResumeBrowser.OnStart (Start in the Load menu). Both run on the machine
    /// that loads the file, before any title state changes, so a refusal leaves the player on the same
    /// screen with a native dialog. Co-op clients never resume a file: they join the host's room and
    /// receive the map from the host's load, so refusing on the host means no client starts loading.
    ///
    /// The check reads the file the entry is about to load, decompresses it with GameSerialize.Decompress
    /// and parses it with fsJsonParser, the parser behind the game's own SerializationHelpers load. It only
    /// reads; the save is never written. Any failure of the check itself logs one warning and lets the
    /// game proceed as it would have without the guard.
    /// </summary>
    internal static class SaveContentGuard
    {
        // A .run file is a few hundred KB. A much larger one is not a save this check understands.
        private const long MaxSaveBytes = 64L * 1024 * 1024;
        private const int MaxTreeDepth = 64;

        /// <summary>True to let the resume proceed.</summary>
        internal static bool Admit(string path, string via)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return true;
                // Only the machine that loads the file decides. A client in a room never reaches these entries.
                if (PhotonNetwork.inRoom && !PhotonNetwork.isMasterClient) return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[save-guard] Could not inspect the resume path (" + e.Message + "); resuming as before.");
                return true;
            }

            string name = Path.GetFileName(path);
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            SaveContentRefs refs;
            SaveContentVerdict verdict = SaveContentCheck.Run(
                delegate { return LoadSave(path); },
                ParseTree,
                delegate(SaveContentRefs found) { return new GameResolver(found.Adventure); },
                delegate(string reason)
                {
                    Plugin.Log.LogWarning("[save-guard] Could not check '" + name + "' (" + reason + "); resuming as before.");
                },
                out refs);
            clock.Stop();
            if (verdict == null) return true;
            if (!verdict.Refuse)
            {
                Plugin.Log.LogInfo("[save-guard] '" + name + "' refers only to installed content: " +
                    verdict.Checked + " distinct ids checked in " + clock.ElapsedMilliseconds + " ms via " + via + ".");
                return true;
            }
            // A warning, not an error: the player's content changed, the framework did not fail.
            Plugin.Log.LogWarning(SaveContentCheck.LogLine(name, verdict, clock.ElapsedMilliseconds) + " Via " + via + ".");
            ShowRefusal(verdict);
            return false;
        }

        private static void ShowRefusal(SaveContentVerdict verdict)
        {
            try
            {
                uiSystemDialog dialog = uiSystemDialog.Instance;
                if (dialog == null)
                {
                    Plugin.Log.LogWarning("[save-guard] The native dialog is unavailable; the refusal is only in this log.");
                    return;
                }
                // The OK overload saves and restores the current input focus, so the player returns to the
                // Resume button or the Load menu, and every later attempt shows the same explanation.
                dialog.Show(SaveContentCheck.DialogTitle, SaveContentCheck.PlayerMessage(verdict),
                    SaveContentCheck.DialogNote, "OK", null);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[save-guard] Could not show the refusal dialog: " + e.Message);
            }
        }

        private static object LoadSave(string path)
        {
            if (new FileInfo(path).Length > MaxSaveBytes) throw new FormatException("the file is larger than any save");
            int status = 0;
            string content = GameSerialize.Decompress(File.ReadAllBytes(path), ref status);
            if (status <= 0 || string.IsNullOrEmpty(content))
                throw new FormatException("the file did not decompress (" + status + ")");
            fsData root = Parse(content);
            if (!root.IsDictionary) throw new FormatException("the save root is not an object");
            // Only the parts the check reads are converted; map geometry and reveal arrays are skipped.
            Dictionary<string, fsData> save = root.AsDictionary;
            Dictionary<string, object> tree = new Dictionary<string, object>(StringComparer.Ordinal);
            Copy(save, tree, "m_GameInfo");
            Copy(save, tree, "m_PlayerSerialize");
            fsData map;
            if (save.TryGetValue("m_Map", out map) && map != null && map.IsDictionary)
            {
                Dictionary<string, object> mini = new Dictionary<string, object>(StringComparer.Ordinal);
                Copy(map.AsDictionary, mini, "m_MiniHexArray");
                Copy(map.AsDictionary, mini, "m_MiniHexInstArray");
                tree["m_Map"] = mini;
            }
            fsData states;
            if (save.TryGetValue("m_GameStates", out states) && states != null && states.IsDictionary)
            {
                Dictionary<string, object> quests = new Dictionary<string, object>(StringComparer.Ordinal);
                Copy(states.AsDictionary, quests, "m_QuestList");
                tree["m_GameStates"] = quests;
            }
            return tree;
        }

        private static object ParseTree(string json)
        {
            return Convert(Parse(json), 0);
        }

        private static fsData Parse(string json)
        {
            fsData data;
            fsResult result = fsJsonParser.Parse(json, out data);
            if (result.Failed || data == null) throw new FormatException("the save JSON did not parse: " + result.FormattedMessages);
            return data;
        }

        private static void Copy(Dictionary<string, fsData> from, Dictionary<string, object> to, string key)
        {
            fsData value;
            if (from.TryGetValue(key, out value)) to[key] = Convert(value, 1);
        }

        private static object Convert(fsData data, int depth)
        {
            if (depth > MaxTreeDepth) throw new FormatException("the save is nested too deeply");
            if (data == null || data.IsNull) return null;
            if (data.IsDictionary)
            {
                Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, fsData> pair in data.AsDictionary) dict[pair.Key] = Convert(pair.Value, depth + 1);
                return dict;
            }
            if (data.IsList)
            {
                List<fsData> source = data.AsList;
                List<object> list = new List<object>(source.Count);
                foreach (fsData item in source) list.Add(Convert(item, depth + 1));
                return list;
            }
            if (data.IsString) return data.AsString;
            if (data.IsInt64) return data.AsInt64;
            if (data.IsDouble) return data.AsDouble;
            if (data.IsBool) return data.AsBool;
            return null;
        }

        /// <summary>One log line for a map point that threw while a save was being restored.</summary>
        internal static string DescribeMiniHexFailure(int k, object[] prefabs, object[] init, Exception error)
        {
            string prefab = prefabs != null && k >= 0 && k < prefabs.Length ? prefabs[k] as string : null;
            StringBuilder data = new StringBuilder();
            object[] values = init != null && k >= 0 && k < init.Length ? init[k] as object[] : null;
            if (values != null)
                for (int i = 3; i < values.Length && data.Length < 120; i++)
                {
                    if (data.Length != 0) data.Append(", ");
                    data.Append(values[i] == null ? "null" : System.Convert.ToString(values[i], CultureInfo.InvariantCulture));
                }
            return "[save-guard] Resume could not restore map point '" + (prefab ?? "?") + "' (saved data: " +
                (data.Length == 0 ? "none" : data.ToString()) + "): " + error.GetType().Name + ": " + error.Message +
                ". The game cannot finish loading this save and will wait on \"Crafting adventure\"; the save probably" +
                " uses content that isn't installed. Quit the game, then restore that content or choose another save.";
        }

        private sealed class GameResolver : ISaveContentResolver
        {
            private readonly string _adventure;
            private bool _questsRead;
            private HashSet<int> _quests;

            internal GameResolver(string adventure)
            {
                _adventure = adventure;
            }

            public bool Exists(SaveRefKind kind, string id)
            {
                switch (kind)
                {
                    case SaveRefKind.Adventure:
                        return Preview(id) != null;
                    case SaveRefKind.Class:
                        return TableManager.Instance.Get<FTK_playerGameStartDB>().GetEntryByInt(Number(id)) != null;
                    case SaveRefKind.Enemy:
                        // MiniHexEnemy.Awake2's own lookup.
                        return TableManager.Instance.Get<FTK_enemyCombatDB>().GetEntryByStringID(id) != null;
                    case SaveRefKind.MiniEncounter:
                        // MiniEncounter.GetDBEntry resolves FTK_miniEncounterDB.GetEntry, an int lookup.
                        return TableManager.Instance.Get<FTK_miniEncounterDB>().GetEntryByInt(Number(id)) != null;
                    case SaveRefKind.Item:
                        // Routed through the framework's GetItemBase postfix, like every in-game item lookup.
                        return FTK_itembase.GetItemBase((FTK_itembase.ID)Number(id)) != null;
                    default:
                        HashSet<int> quests = Quests();
                        return quests == null || quests.Contains(Number(id));
                }
            }

            public bool IsRegistered(SaveRefKind kind, int id)
            {
                return ContentRegistry.IsRegisteredSyntheticId(id, Tables(kind));
            }

            public string Owner(SaveRefKind kind, string id)
            {
                int number;
                string key;
                if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ||
                    !IdAllocator.TryGetKey(number, out key)) return null;
                int colon = key.IndexOf(':');
                string guid = colon > 0 ? key.Substring(0, colon) : key;
                ManagedSnapshot active = MarketplaceRuntime.Active;
                if (active != null && active.Packages != null)
                    foreach (PackageDescriptor package in active.Packages)
                        if (package != null && package.ModGuid == guid && !string.IsNullOrEmpty(package.Name)) return package.Name;
                return guid;
            }

            // GameDefinition.GetQuestByHashID resolves quests only after map generation, so the known ids
            // are rebuilt from the adventure's definition text. Null skips the quest check.
            private HashSet<int> Quests()
            {
                if (_questsRead) return _quests;
                _questsRead = true;
                GameDefinitionPreview preview = Preview(_adventure);
                if (preview != null && !string.IsNullOrEmpty(preview.m_FullFileData))
                {
                    HashSet<int> hashes = SaveContentCheck.StoryQuestHashes(preview.m_FullFileData,
                        delegate(string questId) { return questId.GetHashCode(); });
                    if (hashes.Count != 0) _quests = hashes;
                }
                if (_quests == null)
                    Plugin.Log.LogWarning("[save-guard] The adventure definition has no readable quest ids; quest ids are not checked.");
                return _quests;
            }

            private static GameDefinitionPreview Preview(string name)
            {
                if (string.IsNullOrEmpty(name)) return null;
                // GameSerialize.GameInfo's copy constructor renames legacy definitions before every resume.
                string current = GameDefinitionBase.GetUpdatedGameDefinitionName(name) ?? name;
                return GameCache.Cache.GameDefinitions.GetPreview(current);
            }

            private static Type[] Tables(SaveRefKind kind)
            {
                switch (kind)
                {
                    case SaveRefKind.Class: return new[] { typeof(FTK_playerGameStartDB) };
                    case SaveRefKind.Enemy: return new[] { typeof(FTK_enemyCombatDB) };
                    case SaveRefKind.MiniEncounter: return new[] { typeof(FTK_miniEncounterDB) };
                    case SaveRefKind.Item: return new[] { typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB) };
                    default: return new Type[0];
                }
            }

            private static int Number(string id)
            {
                return int.Parse(id, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }
        }
    }

    // The Resume button. Low priority, after SaveNamespace's LastSave sanitizing prefix, and it reads
    // the same key that prefix and the native method read (SaveNamespace.LastSaveKey is "LastSave"
    // outside title-screen activation). A veto from an earlier prefix skips the check.
    [HarmonyPatch(typeof(StartGameFE.MainScreen), "OnResume")]
    internal static class SaveContentResumeButtonGuard
    {
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(bool __runOriginal)
        {
            if (!__runOriginal) return true;
            string path;
            try { path = UnityEngine.PlayerPrefs.GetString(SaveNamespace.LastSaveKey(), string.Empty); }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[save-guard] Could not read the last save path (" + e.Message + "); resuming as before.");
                return true;
            }
            return SaveContentGuard.Admit(path, "MainScreen.OnResume");
        }
    }

    // Start in the Load menu. Refusing keeps the browser open on the chosen save.
    [HarmonyPatch(typeof(StartGameFE.ResumeBrowser), "OnStart")]
    internal static class SaveContentLoadMenuGuard
    {
        [HarmonyPriority(Priority.Low)]
        private static bool Prefix(StartGameFE.ResumeBrowser __instance, bool __runOriginal)
        {
            if (!__runOriginal || __instance == null || __instance.m_RunInfo == null) return true;
            return SaveContentGuard.Admit(__instance.m_RunInfo.m_Filename, "ResumeBrowser.OnStart");
        }
    }

    // Backstop for anything the guard does not catch, including a co-op client that lacks the host's
    // content. GameLogic.ResumeMiniHex has no guard, and the coroutine that calls it dies on the first
    // exception. A void finalizer observes the exception, and HarmonyX rethrows it with `rethrow`, so the
    // stack trace and the game's behavior are unchanged. It never tries to recover.
    [HarmonyPatch(typeof(GameLogic), "ResumeMiniHex")]
    internal static class SaveContentMiniHexFailureLog
    {
        private const int MaxLines = 8;
        private static int _lines;

        private static void Finalizer(Exception __exception, int k, object[] _prefabs, object[] _init)
        {
            if (__exception == null || _lines >= MaxLines) return;
            _lines++;
            try { Plugin.Log.LogError(SaveContentGuard.DescribeMiniHexFailure(k, _prefabs, _init, __exception)); }
            catch { }
        }
    }
}
