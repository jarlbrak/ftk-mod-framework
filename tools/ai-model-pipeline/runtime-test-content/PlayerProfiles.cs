using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GridEditor;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core;

public sealed partial class RuntimeModelTestContent
{
    JArray playerProfiles;
    bool playersAttempted;
    string playerReportPath;

    void LoadPlayerProfiles()
    {
        string input = Path.Combine(root, "model-test-player-profiles.json");
        try
        {
            string report = Path.Combine(root, "model-test-player-registration.json");
            if (File.Exists(report)) NoLinks(report);
            playerReportPath = report;
            PlayerReport("initializing", new JArray(), null);
            if (!File.Exists(input))
            {
                PlayerReport("absent", new JArray(), null);
                return;
            }
            NoLinks(input);
            if (new FileInfo(input).Length > 1024 * 1024) throw new InvalidOperationException("Player profiles exceed 1 MiB.");
            JObject doc = JObject.Parse(File.ReadAllText(input));
            Keys(doc, "version", "profiles");
            if (doc["version"] == null || doc["version"].Type != JTokenType.Integer || (int)doc["version"] != 1)
                throw new InvalidOperationException("Player profile version must be integer 1.");
            JArray candidates = doc["profiles"] as JArray;
            if (candidates == null || candidates.Count < 1 || candidates.Count > 128)
                throw new InvalidOperationException("Expected 1..128 player profiles.");
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject p in candidates)
            {
                Keys(p, "key", "baseClass", "displayName", "skinset", "defaultSkinType", "renderers", "startingArmor", "apparel");
                string key = Text(p, "key");
                if (!Regex.IsMatch(key, "^ftkmf_modeltest_player_[a-z0-9_]{1,56}$") || !keys.Add(key))
                    throw new InvalidOperationException("Invalid or duplicate player key: " + key);
                if (!Enum.IsDefined(typeof(FTK_playerGameStart.ID), Text(p, "baseClass")))
                    throw new InvalidOperationException("Unknown native class.");
                if (!Enum.IsDefined(typeof(FTK_skinset.ID), Text(p, "skinset")))
                    throw new InvalidOperationException("Unknown native skinset.");
                string skinType = Text(p, "defaultSkinType");
                if (!Enum.IsDefined(typeof(FTK_playerGameStart.SkinType), skinType) || skinType == "None")
                    throw new InvalidOperationException("Expected explicit valid default skin type.");
                if (Text(p, "displayName").Length > 120) throw new InvalidOperationException("Player name exceeds 120 characters.");
                if (p["startingArmor"] != null && !Enum.IsDefined(typeof(FTK_itembase.ID), Text(p, "startingArmor")))
                    throw new InvalidOperationException("Unknown native starting armor item.");
                JArray renderers = p["renderers"] as JArray;
                if (renderers == null || renderers.Count < 1 || renderers.Count > 16)
                    throw new InvalidOperationException("Expected 1..16 player renderer assignments.");
                HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject m in renderers)
                {
                    Keys(m, "rendererPath", "glbFile", "textureFile");
                    string path = Text(m, "rendererPath");
                    if (path.Length > 512 || path.StartsWith("/") || path.EndsWith("/") || path.IndexOf('\\') >= 0 || !paths.Add(path))
                        throw new InvalidOperationException("Invalid or repeated player renderer path.");
                    if (path != ".") foreach (string part in path.Split('/'))
                        if (part == "" || part == "." || part == "..") throw new InvalidOperationException("Invalid player renderer segment.");
                    Asset(m, "glbFile", ".glb", false);
                    Asset(m, "textureFile", ".png", true);
                }
                if (p["apparel"] != null)
                {
                    JArray apparel = p["apparel"] as JArray;
                    if (apparel == null || apparel.Count > 16)
                        throw new InvalidOperationException("Expected 0..16 conditional apparel assignments.");
                    foreach (JObject m in apparel)
                    {
                        Keys(m, "rendererPath", "expectedNativeMeshName", "glbFile", "textureFile");
                        string path = Text(m, "rendererPath");
                        if (path.Length > 512 || path.StartsWith("/") || path.EndsWith("/") || path.IndexOf('\\') >= 0 || !paths.Add(path))
                            throw new InvalidOperationException("Invalid or repeated apparel renderer path.");
                        if (path != ".") foreach (string part in path.Split('/'))
                            if (part == "" || part == "." || part == "..") throw new InvalidOperationException("Invalid apparel renderer segment.");
                        string nativeName = Text(m, "expectedNativeMeshName");
                        if (nativeName.Trim().Length == 0 || nativeName.Length > 160)
                            throw new InvalidOperationException("Expected nonblank native mesh name of at most160 characters.");
                        foreach (char ch in nativeName) if (char.IsControl(ch))
                            throw new InvalidOperationException("Native mesh name contains a control character.");
                        Asset(m, "glbFile", ".glb", false);
                        Asset(m, "textureFile", ".png", true);
                    }
                }
            }
            playerProfiles = candidates;
            Logger.LogInfo("MODEL PLAYER CONTENT READY: " + playerProfiles.Count + " profiles.");
        }
        catch (Exception e)
        {
            playerProfiles = null;
            Logger.LogError("MODEL PLAYER CONTENT REFUSED: " + e.Message);
            if (playerReportPath != null) PlayerReport("refused", new JArray(), e.Message);
        }
    }

    void RegisterPlayerProfiles()
    {
        if (playerProfiles == null || playersAttempted) return;
        playersAttempted = true;
        JArray registered = new JArray();
        string error = null;
        try
        {
            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();
            FTK_skinsetDB skins = Content.Db<FTK_skinsetDB>();
            Dictionary<string, FTK_itembase.ID> startingArmor = new Dictionary<string, FTK_itembase.ID>(StringComparer.Ordinal);
            foreach (JObject p in playerProfiles)
            {
                if (db.GetEntryByStringID(Text(p, "key")) != null) throw new InvalidOperationException("Player key already exists.");
                FTK_playerGameStart.ID baseId = (FTK_playerGameStart.ID)Enum.Parse(typeof(FTK_playerGameStart.ID), Text(p, "baseClass"));
                FTK_skinset.ID skin = (FTK_skinset.ID)Enum.Parse(typeof(FTK_skinset.ID), Text(p, "skinset"));
                if (db.GetEntry(baseId) == null || skins.GetEntry(skin) == null) throw new InvalidOperationException("Missing player template or skinset.");
                if (p["startingArmor"] != null)
                {
                    string armorKey = Text(p, "startingArmor");
                    FTK_itembase.ID armorId = (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), armorKey);
                    FTK_itembase armor = FTK_itembase.GetItemBase(armorId);
                    if (armor == null || armor.m_ID != armorKey || armor.m_ObjectType != FTK_itembase.ObjectType.armor
                        || !armor.m_Equippable || armor.m_CursedItem || armor.m_WearablePrefab == null
                        || armor.m_WearablePrefab.GetComponent<Armor>() == null
                        || (armor.m_WearablePrefabM != null && armor.m_WearablePrefabM.GetComponent<Armor>() == null))
                        throw new InvalidOperationException("Starting armor must resolve exactly to native equippable, non-cursed body armor: " + armorKey);
                    startingArmor.Add(Text(p, "key"), armorId);
                }
            }
            foreach (JObject p in playerProfiles)
            {
                string key = Text(p, "key");
                FTK_playerGameStart.ID baseId = (FTK_playerGameStart.ID)Enum.Parse(typeof(FTK_playerGameStart.ID), Text(p, "baseClass"));
                FTK_skinset.ID skin = (FTK_skinset.ID)Enum.Parse(typeof(FTK_skinset.ID), Text(p, "skinset"));
                FTK_playerGameStart.SkinType defaultType = (FTK_playerGameStart.SkinType)Enum.Parse(typeof(FTK_playerGameStart.SkinType), Text(p, "defaultSkinType"));
                FTK_itembase.ID armorItem;
                bool addArmor = startingArmor.TryGetValue(key, out armorItem);
                FTK_itembase.ID[] templateItems = db.GetEntry(baseId).m_StartItems;
                FTK_playerGameStart row = Content.AddClass(Id, key, baseId, Text(p, "displayName"), delegate(FTK_playerGameStart clone)
                {
                    // Isolated fixture: all seven native skin-type slots resolve to this one chosen avatar.
                    // The fresh array cannot alter the template. UI-selected gender remains a native value.
                    clone.m_Skinsets = new FTK_skinset.ID[] { skin, skin, skin, skin, skin, skin, skin };
                    clone.m_DefaultSkinType = defaultType;
                    if (addArmor)
                    {
                        FTK_itembase.ID[] inherited = clone.m_StartItems ?? new FTK_itembase.ID[0];
                        FTK_itembase.ID[] items = new FTK_itembase.ID[inherited.Length + 1];
                        Array.Copy(inherited, items, inherited.Length);
                        items[inherited.Length] = armorItem;
                        clone.m_StartItems = items;
                    }
                });
                List<PlayerRendererMesh> meshes = new List<PlayerRendererMesh>();
                foreach (JObject m in (JArray)p["renderers"])
                    meshes.Add(new PlayerRendererMesh(Text(m, "rendererPath"), Text(m, "glbFile"), m["textureFile"] == null ? null : (string)m["textureFile"]));
                List<PlayerApparelMesh> apparel = new List<PlayerApparelMesh>();
                if (p["apparel"] != null) foreach (JObject m in (JArray)p["apparel"])
                    apparel.Add(new PlayerApparelMesh(Text(m, "rendererPath"), Text(m, "expectedNativeMeshName"),
                        Text(m, "glbFile"), m["textureFile"] == null ? null : (string)m["textureFile"]));
                bool visualRegistered = row != null && (p["apparel"] == null
                    ? Content.SetClassBodyMeshesFromGlb(row, skin, meshes.ToArray())
                    : Content.SetClassBodyMeshesFromGlb(row, skin, meshes.ToArray(), apparel.ToArray()));
                if (!visualRegistered)
                    throw new InvalidOperationException("Player visual registration failed: " + key);
                int id = db.GetIntFromID(key);
                string name;
                if (!object.ReferenceEquals(db.GetEntryByStringID(key), row) || !object.ReferenceEquals(db.GetEntry((FTK_playerGameStart.ID)id), row)
                    || row.m_ID != key || !Localization.TryGetName(key, out name) || name != Text(p, "displayName"))
                    throw new InvalidOperationException("Player class roundtrip failed: " + key);
                if (addArmor)
                {
                    int inheritedCount = templateItems == null ? 0 : templateItems.Length;
                    if (object.ReferenceEquals(row.m_StartItems, templateItems) || row.m_StartItems.Length != inheritedCount + 1
                        || row.m_StartItems[inheritedCount] != armorItem)
                        throw new InvalidOperationException("Starting armor append failed: " + key);
                    for (int i = 0; i < inheritedCount; i++)
                        if (row.m_StartItems[i] != templateItems[i]) throw new InvalidOperationException("Inherited starting items changed: " + key);
                }
                JObject registration = new JObject { { "key", key }, { "id", id }, { "baseClass", Text(p, "baseClass") },
                    { "skinset", Text(p, "skinset") }, { "defaultSkinType", Text(p, "defaultSkinType") },
                    { "startingArmor", addArmor ? Text(p, "startingArmor") : null }, { "startingArmorAppendedCount", addArmor ? 1 : 0 },
                    { "fixture", "all_seven_skin_type_slots_same_skinset" }, { "status", "registered_avatar_validation_pending" } };
                registration["renderers"] = p["renderers"].DeepClone();
                if (p["apparel"] != null) registration["apparel"] = p["apparel"].DeepClone();
                registered.Add(registration);
                Logger.LogInfo("SELF-TEST PASS [model-test-player-content]: " + key + " resolves to " + id + "; avatar validation pending.");
            }
        }
        catch (Exception e) { error = e.ToString(); Logger.LogError("SELF-TEST FAIL [model-test-player-content]: " + error); }
        PlayerReport(error == null ? "registered" : "failed", registered, error);
    }

    void PlayerReport(string status, JArray registered, string error)
    {
        try { File.WriteAllText(playerReportPath, new JObject {
            { "version", 1 }, { "run", runId }, { "updatedUtc", DateTime.UtcNow.ToString("o") }, { "status", status },
            { "requested", playerProfiles == null ? 0 : playerProfiles.Count }, { "registered", registered }, { "error", error }
        }.ToString()); }
        catch (Exception e) { Logger.LogError("MODEL PLAYER CONTENT report write failed: " + e.Message); }
    }
}
